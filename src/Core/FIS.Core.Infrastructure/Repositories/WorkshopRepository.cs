using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists workshop records against both the original client table and the
/// expanded table. The original schema stores time-only values and contains
/// operational fields that are absent from the expanded EF model.
/// </summary>
public sealed class WorkshopRepository : IWorkshopRepository
{
    private const string TableName = "workshop";

    private static readonly string[] LegacyColumns =
    [
        "ww_code",
        "vmf_code",
        "receive_time",
        "receive_date",
        "complete_time",
        "complete_date",
        "fetch_time",
        "fetch_date",
        "driver_name",
        "contact_name",
        "contact_tel",
        "ww_remarks",
        "ww_reason",
        "accid_mech",
        "ww_site",
        "contact_fax",
        "contact_email",
        "call_refer",
        "ww_km",
        "recover_cost",
        "rem_other_repair",
        "tow_comp",
        "tow_amount",
        "spare_wheel",
        "jack",
        "wheel_spanner",
        "radio",
        "2way",
        "gear_lock",
        "keys",
        "fuel",
        "inter_exter",
        "merch_code",
        "date_to_merch",
        "km_to_merch",
        "date_from_merch",
        "km_from_merch",
        "cost_repair",
        "points",
        "fa_auth_num",
        "test_name",
        "inform_admin",
        "date_from_ww",
        "time_from_ww",
        "fetch_name",
        "job_close",
        "garage",
        "blue_light",
        "monitor_refer",
    ];

    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly HashSet<string> TimeColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "receive_time",
        "complete_time",
        "fetch_time",
        "time_from_ww",
    };

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "receive_date",
        "complete_date",
        "fetch_date",
        "date_to_merch",
        "date_from_merch",
        "date_from_ww",
        "date_created",
        "date_updated",
    };

    private readonly FisDbContext _context;

    public WorkshopRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Workshop?> GetByIdAsync(short code) =>
        (
            await QueryAsync(
                "[ww_code] = @wwCode",
                command => AddParameter(command, "@wwCode", DbType.Int16, code)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Workshop>> GetAllAsync() => await QueryAsync();

    public async Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.ContainsKey("vmf_code"))
        {
            return Array.Empty<Workshop>();
        }

        return await QueryAsync(
            "[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            columns
        );
    }

    public async Task<Workshop> CreateAsync(Workshop item, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var columns = await GetAvailableColumnsAsync();
        var values = BuildCoreValues(item, columns, includeNulls: false);
        AddLegacyValues(values, item, columns, includeNulls: false);

        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false,
            includeNull: false
        );

        var code = await ExecuteInsertAsync(values);
        return await GetByIdAsync(code)
            ?? throw new InvalidOperationException(
                $"Workshop with ww_code {code} could not be read after creation."
            );
    }

    public async Task<Workshop> UpdateAsync(Workshop item, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existing =
            await GetByIdAsync(item.ww_code)
            ?? throw new InvalidOperationException(
                $"Workshop with ww_code {item.ww_code} not found"
            );
        var columns = await GetAvailableColumnsAsync();
        var values = BuildCoreValues(item, columns, includeNulls: true);

        // Only non-null legacy values are included. This preserves fields
        // captured by the client-era workflow when a modern form updates the
        // core fields without posting the rest of the legacy record.
        AddLegacyValues(values, item, columns, includeNulls: false);
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null,
            includeNull: false
        );

        await ExecuteUpdateAsync(item.ww_code, values, columns);
        return await GetByIdAsync(item.ww_code) ?? existing;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(short code, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            if (columns.ContainsKey("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (columns.ContainsKey("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (columns.ContainsKey("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [ww_code] = @wwCode
                      AND {GetActiveFilter(columns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [ww_code] = @wwCode
                    """;
            }

            AddParameter(command, "@wwCode", DbType.Int16, code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list, predicates, and filters use only fixed legacy columns and parameterized values."
    )]
    private async Task<List<Workshop>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlyDictionary<string, ColumnInfo>? suppliedColumns = null
    )
    {
        var columns = suppliedColumns ?? await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var projection = LegacyColumns
                .Concat(OptionalAuditColumns)
                .Select(column => GetProjection(columns, column))
                .ToArray();
            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            conditions.Add(GetActiveFilter(columns));
            var order = columns.ContainsKey("receive_date")
                ? "[receive_date] DESC, "
                : string.Empty;
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY {order}[ww_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Workshop>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapWorkshop(reader, columns));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static List<WriteValue> BuildCoreValues(
        Workshop item,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        bool includeNulls
    )
    {
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "vmf_code",
            "@vmfCode",
            DbType.Int32,
            item.vmf_code,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "receive_time",
            "@receiveTime",
            item.receive_time,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "receive_date",
            "@receiveDate",
            DbType.DateTime2,
            item.receive_date,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "complete_time",
            "@completeTime",
            item.complete_time,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "complete_date",
            "@completeDate",
            DbType.DateTime2,
            item.complete_date,
            includeNulls
        );
        return values;
    }

    private static void AddLegacyValues(
        ICollection<WriteValue> values,
        Workshop item,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        bool includeNulls
    )
    {
        AddTimeValue(values, columns, "fetch_time", "@fetchTime", item.fetch_time, includeNulls);
        AddValue(
            values,
            columns,
            "fetch_date",
            "@fetchDate",
            DbType.DateTime2,
            item.fetch_date,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "driver_name",
            "@driverName",
            DbType.String,
            item.driver_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_name",
            "@contactName",
            DbType.String,
            item.contact_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_tel",
            "@contactTel",
            DbType.String,
            item.contact_tel,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "ww_remarks",
            "@wwRemarks",
            DbType.String,
            item.ww_remarks,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "ww_reason",
            "@wwReason",
            DbType.String,
            item.ww_reason,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "accid_mech",
            "@accidMech",
            DbType.String,
            item.accid_mech,
            includeNulls
        );
        AddValue(values, columns, "ww_site", "@wwSite", DbType.Int16, item.ww_site, includeNulls);
        AddValue(
            values,
            columns,
            "contact_fax",
            "@contactFax",
            DbType.String,
            item.contact_fax,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_email",
            "@contactEmail",
            DbType.String,
            item.contact_email,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "call_refer",
            "@callRefer",
            DbType.Decimal,
            item.call_refer,
            includeNulls
        );
        AddValue(values, columns, "ww_km", "@wwKm", DbType.Decimal, item.ww_km, includeNulls);
        AddValue(
            values,
            columns,
            "recover_cost",
            "@recoverCost",
            DbType.Decimal,
            item.recover_cost,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "rem_other_repair",
            "@remOtherRepair",
            DbType.String,
            item.rem_other_repair,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "tow_comp",
            "@towComp",
            DbType.Int32,
            item.tow_comp,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "tow_amount",
            "@towAmount",
            DbType.Decimal,
            item.tow_amount,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "spare_wheel",
            "@spareWheel",
            DbType.String,
            item.spare_wheel,
            includeNulls
        );
        AddValue(values, columns, "jack", "@jack", DbType.String, item.jack, includeNulls);
        AddValue(
            values,
            columns,
            "wheel_spanner",
            "@wheelSpanner",
            DbType.String,
            item.wheel_spanner,
            includeNulls
        );
        AddValue(values, columns, "radio", "@radio", DbType.String, item.radio, includeNulls);
        AddValue(values, columns, "2way", "@twoWay", DbType.String, item.two_way, includeNulls);
        AddValue(
            values,
            columns,
            "gear_lock",
            "@gearLock",
            DbType.String,
            item.gear_lock,
            includeNulls
        );
        AddValue(values, columns, "keys", "@keys", DbType.String, item.keys, includeNulls);
        AddValue(values, columns, "fuel", "@fuel", DbType.String, item.fuel, includeNulls);
        AddValue(
            values,
            columns,
            "inter_exter",
            "@interExter",
            DbType.String,
            item.inter_exter,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "merch_code",
            "@merchCode",
            DbType.Int32,
            item.merch_code,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_to_merch",
            "@dateToMerch",
            DbType.DateTime2,
            item.date_to_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "km_to_merch",
            "@kmToMerch",
            DbType.Decimal,
            item.km_to_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_from_merch",
            "@dateFromMerch",
            DbType.DateTime2,
            item.date_from_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "km_from_merch",
            "@kmFromMerch",
            DbType.Decimal,
            item.km_from_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "cost_repair",
            "@costRepair",
            DbType.Decimal,
            item.cost_repair,
            includeNulls
        );
        AddValue(values, columns, "points", "@points", DbType.Decimal, item.points, includeNulls);
        AddValue(
            values,
            columns,
            "fa_auth_num",
            "@faAuthNum",
            DbType.String,
            item.fa_auth_num,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "test_name",
            "@testName",
            DbType.String,
            item.test_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "inform_admin",
            "@informAdmin",
            DbType.String,
            item.inform_admin,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_from_ww",
            "@dateFromWw",
            DbType.DateTime2,
            item.date_from_ww,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "time_from_ww",
            "@timeFromWw",
            item.time_from_ww,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "fetch_name",
            "@fetchName",
            DbType.String,
            item.fetch_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "job_close",
            "@jobClose",
            DbType.String,
            item.job_close,
            includeNulls
        );
        AddValue(values, columns, "garage", "@garage", DbType.String, item.garage, includeNulls);
        AddValue(
            values,
            columns,
            "blue_light",
            "@blueLight",
            DbType.String,
            item.blue_light,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "monitor_refer",
            "@monitorRefer",
            DbType.Int16,
            item.monitor_refer,
            includeNulls
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task<short> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                values.Count == 0
                    ? $"INSERT INTO [dbo].[{TableName}] DEFAULT VALUES OUTPUT INSERTED.[ww_code]"
                    : $"""
                        INSERT INTO [dbo].[{TableName}] ({string.Join(
                            ", ",
                            values.Select(value => $"[{value.Column}]")
                        )})
                        OUTPUT INSERTED.[ww_code]
                        VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                        """;
            AddParameters(command, values);
            return Convert.ToInt16(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        short code,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (values.Count == 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [ww_code] = @wwCode
                  AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@wwCode", DbType.Int16, code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME], [DATA_TYPE]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                columns[name] = new ColumnInfo(name, reader.GetString(1));
            }

            if (!columns.ContainsKey("ww_code"))
            {
                throw new InvalidOperationException(
                    "The required workshop compatibility column ww_code is not available."
                );
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Workshop MapWorkshop(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        var completeTime = ReadTimeSpanIfAvailable(reader, columns, "complete_time");
        var completeDate = ReadDateTimeIfAvailable(reader, columns, "complete_date");
        if (
            !completeDate.HasValue
            && columns.TryGetValue("complete_time", out var completeTimeColumn)
            && !string.Equals(
                completeTimeColumn.DataType,
                "time",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            completeDate = ReadDateTimeIfAvailable(reader, columns, "complete_time")?.Date;
        }

        return new Workshop
        {
            ww_code = ReadInt16(reader, "ww_code") ?? 0,
            vmf_code = ReadInt32IfAvailable(reader, columns, "vmf_code"),
            receive_time = ReadTimeSpanIfAvailable(reader, columns, "receive_time"),
            receive_date = ReadDateTimeIfAvailable(reader, columns, "receive_date"),
            complete_time = completeTime,
            complete_date = completeDate,
            fetch_time = ReadTimeSpanIfAvailable(reader, columns, "fetch_time"),
            fetch_date = ReadDateTimeIfAvailable(reader, columns, "fetch_date"),
            driver_name = ReadStringIfAvailable(reader, columns, "driver_name"),
            contact_name = ReadStringIfAvailable(reader, columns, "contact_name"),
            contact_tel = ReadStringIfAvailable(reader, columns, "contact_tel"),
            ww_remarks = ReadStringIfAvailable(reader, columns, "ww_remarks"),
            ww_reason = ReadStringIfAvailable(reader, columns, "ww_reason"),
            accid_mech = ReadStringIfAvailable(reader, columns, "accid_mech"),
            ww_site = ReadInt16IfAvailable(reader, columns, "ww_site"),
            contact_fax = ReadStringIfAvailable(reader, columns, "contact_fax"),
            contact_email = ReadStringIfAvailable(reader, columns, "contact_email"),
            call_refer = ReadDecimalIfAvailable(reader, columns, "call_refer"),
            ww_km = ReadDecimalIfAvailable(reader, columns, "ww_km"),
            recover_cost = ReadDecimalIfAvailable(reader, columns, "recover_cost"),
            rem_other_repair = ReadStringIfAvailable(reader, columns, "rem_other_repair"),
            tow_comp = ReadInt32IfAvailable(reader, columns, "tow_comp"),
            tow_amount = ReadDecimalIfAvailable(reader, columns, "tow_amount"),
            spare_wheel = ReadStringIfAvailable(reader, columns, "spare_wheel"),
            jack = ReadStringIfAvailable(reader, columns, "jack"),
            wheel_spanner = ReadStringIfAvailable(reader, columns, "wheel_spanner"),
            radio = ReadStringIfAvailable(reader, columns, "radio"),
            two_way = ReadStringIfAvailable(reader, columns, "2way"),
            gear_lock = ReadStringIfAvailable(reader, columns, "gear_lock"),
            keys = ReadStringIfAvailable(reader, columns, "keys"),
            fuel = ReadStringIfAvailable(reader, columns, "fuel"),
            inter_exter = ReadStringIfAvailable(reader, columns, "inter_exter"),
            merch_code = ReadInt32IfAvailable(reader, columns, "merch_code"),
            date_to_merch = ReadDateTimeIfAvailable(reader, columns, "date_to_merch"),
            km_to_merch = ReadDecimalIfAvailable(reader, columns, "km_to_merch"),
            date_from_merch = ReadDateTimeIfAvailable(reader, columns, "date_from_merch"),
            km_from_merch = ReadDecimalIfAvailable(reader, columns, "km_from_merch"),
            cost_repair = ReadDecimalIfAvailable(reader, columns, "cost_repair"),
            points = ReadDecimalIfAvailable(reader, columns, "points"),
            fa_auth_num = ReadStringIfAvailable(reader, columns, "fa_auth_num"),
            test_name = ReadStringIfAvailable(reader, columns, "test_name"),
            inform_admin = ReadStringIfAvailable(reader, columns, "inform_admin"),
            date_from_ww = ReadDateTimeIfAvailable(reader, columns, "date_from_ww"),
            time_from_ww = ReadTimeSpanIfAvailable(reader, columns, "time_from_ww"),
            fetch_name = ReadStringIfAvailable(reader, columns, "fetch_name"),
            job_close = ReadStringIfAvailable(reader, columns, "job_close"),
            garage = ReadStringIfAvailable(reader, columns, "garage"),
            blue_light = ReadStringIfAvailable(reader, columns, "blue_light"),
            monitor_refer = ReadInt16IfAvailable(reader, columns, "monitor_refer"),
            date_created =
                ReadDateTimeIfAvailable(reader, columns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };
    }

    private static void AddTimeValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        TimeSpan? value,
        bool includeNull
    )
    {
        if (!columns.TryGetValue(column, out var info) || (!value.HasValue && !includeNull))
        {
            return;
        }

        if (string.Equals(info.DataType, "time", StringComparison.OrdinalIgnoreCase))
        {
            values.Add(new WriteValue(column, parameter, DbType.Time, value));
        }
        else
        {
            values.Add(
                new WriteValue(
                    column,
                    parameter,
                    DbType.DateTime2,
                    value.HasValue ? DateTime.Today.Add(value.Value) : null
                )
            );
        }
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value,
        bool includeNull
    )
    {
        if (columns.ContainsKey(column) && (includeNull || value is not null))
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column)
    {
        if (TimeColumns.Contains(column))
        {
            return "time";
        }

        if (DateColumns.Contains(column))
        {
            return "datetime2";
        }

        return column switch
        {
            "ww_code" or "ww_site" or "monitor_refer" => "smallint",
            "vmf_code"
            or "tow_comp"
            or "merch_code"
            or "created_by_user_code"
            or "modified_by_user_code" => "int",
            "call_refer"
            or "ww_km"
            or "recover_cost"
            or "tow_amount"
            or "km_to_merch"
            or "km_from_merch"
            or "cost_repair"
            or "points" => "decimal(18, 2)",
            _ => "varchar(1)",
        };
    }

    private static string? ReadStringIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadString(reader, column) : null;

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadDateTime(reader, column) : null;

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static TimeSpan? ReadTimeSpanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is TimeSpan time)
        {
            return time;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.TimeOfDay;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.TimeOfDay;
        }

        return TimeSpan.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt32(reader, column) : null;

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static short? ReadInt16IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt16(reader, column) : null;

    private static decimal? ReadDecimalIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

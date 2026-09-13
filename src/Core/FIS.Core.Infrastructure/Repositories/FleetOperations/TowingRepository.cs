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
/// Persists the original Towing business columns against both the client
/// schema and the expanded schema. The expanded audit columns are optional,
/// so this repository deliberately avoids EF materialization for this table.
/// </summary>
public class TowingRepository : ITowingRepository
{
    private const string TableName = "Towing";
    private const string TowTruckTableName = "Tow_Truck";
    private const int MaximumReportPageSize = 24;

    private static readonly string[] LegacyColumns =
    [
        "Towing_code",
        "vmf_code",
        "Call_refer",
        "Tow_request_date",
        "Tow_request_time",
        "Tow_location_start",
        "Vehicle_problem",
        "Keys",
        "Site_code",
        "Contact_person_name",
        "Contact_person_tel",
        "Contact_person_cell",
        "Person_at_vehicle_name",
        "Person_at_vehicle_cell",
        "Remaks",
        "Tow_Truck_code",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns = ["Towing_code", "vmf_code"];

    private readonly FisDbContext _context;

    public TowingRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Towing?> GetByIdAsync(short towingCode) =>
        (
            await QueryAsync(
                "WHERE [Towing_code] = @towingCode",
                command => AddParameter(command, "@towingCode", DbType.Int16, towingCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Towing>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table, columns, ordering, and generated parameter names are fixed; VMF values, pagination values, and every filter value are database parameters."
    )]
    public async Task<TowingPage> GetPageAsync(TowingPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var vmfCodes = query.VmfCodes?.Where(code => code > 0).Distinct().ToArray() ?? [];
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var availableColumns = await GetAvailableColumnsAsync();
            var predicates = new List<string> { GetActiveFilter(availableColumns) };
            if (vmfCodes.Length > 0)
            {
                predicates.Add(
                    $"[vmf_code] IN ({string.Join(", ", vmfCodes.Select((_, index) => $"@vmfCode{index}"))})"
                );
            }

            var whereClause = string.Join(" AND ", predicates);
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText =
                $"SELECT COUNT(1) FROM [dbo].[{TableName}] WHERE {whereClause}";
            for (var index = 0; index < vmfCodes.Length; index++)
            {
                AddParameter(countCommand, $"@vmfCode{index}", DbType.Int32, vmfCodes[index]);
            }

            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var projection = LegacyColumns
                .Select(column => GetColumnProjection(availableColumns, column))
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                WHERE {whereClause}
                ORDER BY [Tow_request_date] DESC, [Towing_code] DESC
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            for (var index = 0; index < vmfCodes.Length; index++)
            {
                AddParameter(dataCommand, $"@vmfCode{index}", DbType.Int32, vmfCodes[index]);
            }
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Towing>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapTowing(reader, availableColumns));
            }

            return new TowingPage(items, page, pageSize, total);
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
        Justification = "The report query uses fixed table, column, and ordering identifiers; report filters and pagination values are database parameters."
    )]
    public async Task<TowingReportPage> GetReportPageAsync(TowingReportPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, MaximumReportPageSize);
        var requestedPage = Math.Max(1, query.Page);
        var (startDate, endDate) = NormalizeDateRange(query.StartDate, query.EndDate);
        var firmName = query.FirmName?.Trim();
        var callReference = query.CallReference;
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var availableColumns = await GetAvailableColumnsAsync();
            var towTruckColumns =
                query.ReportKind == TowingReportKind.FirmDate
                && !string.IsNullOrWhiteSpace(firmName)
                    ? await GetAvailableColumnsAsync(TowTruckTableName)
                    : null;

            var predicates = BuildReportPredicates(
                query.ReportKind,
                availableColumns,
                towTruckColumns,
                !string.IsNullOrWhiteSpace(firmName),
                callReference.HasValue
            );
            var whereClause = string.Join(" AND ", predicates);
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}] AS t
                WHERE {whereClause}
                """;
            AddReportParameters(
                countCommand,
                query.ReportKind,
                startDate,
                endDate,
                firmName,
                callReference
            );
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var projection = LegacyColumns
                .Select(column => GetColumnProjection(availableColumns, column))
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = transaction;
            dataCommand.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS t
                WHERE {whereClause}
                ORDER BY {GetReportOrderExpression(availableColumns)}
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddReportParameters(
                dataCommand,
                query.ReportKind,
                startDate,
                endDate,
                firmName,
                callReference
            );
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Towing>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapTowing(reader, availableColumns));
            }

            return new TowingReportPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Towing>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "WHERE [vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<IEnumerable<Towing>> GetBySiteAsync(short siteCode) =>
        await QueryAsync(
            "WHERE [Site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode)
        );

    public async Task<Towing> CreateAsync(Towing towing, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(towing);

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(towing)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(
            values,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        towing.Towing_code = await ExecuteInsertAsync(values);
        towing.date_created = now;
        towing.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        towing.is_deleted = false;
        return towing;
    }

    public async Task<Towing> UpdateAsync(Towing towing, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(towing);

        var existing = await GetByIdAsync(towing.Towing_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Towing with Towing_code {towing.Towing_code} not found"
            );
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(towing)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            towing.is_deleted
        );

        await ExecuteUpdateAsync(towing.Towing_code, values, availableColumns);
        towing.date_created = existing.date_created;
        towing.created_by_user_code = existing.created_by_user_code;
        towing.date_updated = now;
        towing.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return towing;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(short towingCode, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync();
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
            if (availableColumns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
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
                    WHERE [Towing_code] = @towingCode
                    AND {GetActiveFilter(availableColumns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [Towing_code] = @towingCode
                    """;
            }

            AddParameter(command, "@towingCode", DbType.Int16, towingCode);
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

    private static List<string> BuildReportPredicates(
        TowingReportKind reportKind,
        IReadOnlySet<string> availableColumns,
        IReadOnlySet<string>? towTruckColumns,
        bool hasFirmName,
        bool hasCallReference
    )
    {
        var predicates = new List<string> { GetActiveFilter(availableColumns, "t") };

        switch (reportKind)
        {
            case TowingReportKind.Request:
                predicates.Add(
                    availableColumns.Contains("Tow_request_date")
                        ? "CAST(t.[Tow_request_date] AS date) BETWEEN @startDate AND @endDate"
                        : "1 = 0"
                );
                if (hasCallReference)
                {
                    predicates.Add(
                        availableColumns.Contains("Call_refer")
                            ? "t.[Call_refer] = @callReference"
                            : "1 = 0"
                    );
                }
                break;

            case TowingReportKind.AllTowtrucks:
                var populatedFields = new[]
                {
                    "Tow_location_start",
                    "Keys",
                    "Vehicle_problem",
                }
                    .Where(availableColumns.Contains)
                    .Select(column =>
                        $"NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max), t.[{column}]))), N'') IS NOT NULL"
                    )
                    .ToArray();
                predicates.Add(
                    populatedFields.Length == 0
                        ? "1 = 0"
                        : $"({string.Join(" OR ", populatedFields)})"
                );
                break;

            case TowingReportKind.FirmDate:
                predicates.Add(
                    availableColumns.Contains("Tow_request_date")
                        ? "CAST(t.[Tow_request_date] AS date) BETWEEN @startDate AND @endDate"
                        : "1 = 0"
                );
                if (hasFirmName)
                {
                    predicates.Add(
                        towTruckColumns is not null
                        && availableColumns.Contains("Tow_Truck_code")
                        && towTruckColumns.Contains("Tow_code")
                        && towTruckColumns.Contains("Tow_name")
                            ? $"""
                                EXISTS (
                                    SELECT 1
                                    FROM [dbo].[{TowTruckTableName}] AS tt
                                    WHERE tt.[Tow_code] = t.[Tow_Truck_code]
                                      AND {GetActiveFilter(towTruckColumns, "tt")}
                                      AND CHARINDEX(
                                          @firmName,
                                          LOWER(COALESCE(CONVERT(nvarchar(max), tt.[Tow_name]), N''))
                                      ) > 0
                                )
                                """
                            : "1 = 0"
                    );
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(reportKind), reportKind, null);
        }

        return predicates;
    }

    private static void AddReportParameters(
        DbCommand command,
        TowingReportKind reportKind,
        DateTime startDate,
        DateTime endDate,
        string? firmName,
        decimal? callReference
    )
    {
        if (reportKind is TowingReportKind.Request or TowingReportKind.FirmDate)
        {
            AddParameter(command, "@startDate", DbType.Date, startDate);
            AddParameter(command, "@endDate", DbType.Date, endDate);
        }

        if (reportKind == TowingReportKind.FirmDate && !string.IsNullOrWhiteSpace(firmName))
        {
            AddParameter(command, "@firmName", DbType.String, firmName.ToLowerInvariant());
        }

        if (reportKind == TowingReportKind.Request && callReference.HasValue)
        {
            AddParameter(command, "@callReference", DbType.Decimal, callReference.Value);
        }
    }

    private static (DateTime StartDate, DateTime EndDate) NormalizeDateRange(
        DateTime startDate,
        DateTime endDate
    )
    {
        var start = startDate.Date;
        var end = endDate.Date;
        return end < start ? (end, start) : (start, end);
    }

    private static string GetReportOrderExpression(IReadOnlySet<string> availableColumns) =>
        availableColumns.Contains("Tow_request_date")
            ? "t.[Tow_request_date] DESC, t.[Towing_code] DESC"
            : "CAST(NULL AS date) DESC, t.[Towing_code] DESC";

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and filters are composed only from fixed legacy columns and allowlisted optional columns; values are parameters."
    )]
    private async Task<List<Towing>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
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
                .Select(column => GetColumnProjection(availableColumns, column))
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            var whereClause = string.IsNullOrWhiteSpace(predicate)
                ? $"WHERE {GetActiveFilter(availableColumns)}"
                : $"{predicate} AND {GetActiveFilter(availableColumns)}";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                {whereClause}
                ORDER BY [Tow_request_date] DESC, [Towing_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Towing>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapTowing(reader, availableColumns));
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized."
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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[Towing_code]
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
        Justification = "The UPDATE statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        short towingCode,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> availableColumns
    )
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
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [Towing_code] = @towingCode
                AND {GetActiveFilter(availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@towingCode", DbType.Int16, towingCode);
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

    private Task<HashSet<string>> GetAvailableColumnsAsync() =>
        GetAvailableColumnsAsync(TableName);

    private async Task<HashSet<string>> GetAvailableColumnsAsync(string tableName)
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
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            var requiredColumns = tableName == TableName
                ? RequiredColumns
                : ["Tow_code"];
            var missingColumns = requiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required {tableName} compatibility columns are not available: {string.Join(", ", missingColumns)}"
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

    private static Towing MapTowing(DbDataReader reader, IReadOnlySet<string> availableColumns) =>
        new()
        {
            Towing_code = ReadInt16(reader, "Towing_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            Call_refer = ReadDecimal(reader, "Call_refer"),
            Tow_request_date = ReadDateTime(reader, "Tow_request_date"),
            Tow_request_time = ReadTime(reader, "Tow_request_time"),
            Tow_location_start = ReadString(reader, "Tow_location_start"),
            Vehicle_problem = ReadString(reader, "Vehicle_problem"),
            Keys = ReadString(reader, "Keys"),
            Site_code = ReadInt16(reader, "Site_code"),
            Contact_person_name = ReadString(reader, "Contact_person_name"),
            Contact_person_tel = ReadString(reader, "Contact_person_tel"),
            Contact_person_cell = ReadString(reader, "Contact_person_cell"),
            Person_at_vehicle_name = ReadString(reader, "Person_at_vehicle_name"),
            Person_at_vehicle_cell = ReadString(reader, "Person_at_vehicle_cell"),
            Remaks = ReadString(reader, "Remaks"),
            Tow_Truck_code = ReadInt16(reader, "Tow_Truck_code"),
            date_created =
                ReadDateTimeIfAvailable(reader, availableColumns, "date_created")
                ?? ReadDateTime(reader, "Tow_request_date")
                ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "created_by_user_code"
            ),
            modified_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "modified_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
        };

    private static List<WriteValue> BuildLegacyWriteValues(Towing towing) =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, towing.vmf_code),
            new("Call_refer", "@callRefer", DbType.Decimal, towing.Call_refer),
            new("Tow_request_date", "@requestDate", DbType.Date, towing.Tow_request_date?.Date),
            // The legacy table stores time(3), while the expanded table uses
            // datetime2. SQL Server converts this parameter to the target
            // column without losing the legacy time-only behavior.
            new("Tow_request_time", "@requestTime", DbType.DateTime2, towing.Tow_request_time),
            new("Tow_location_start", "@locationStart", DbType.String, towing.Tow_location_start),
            new("Vehicle_problem", "@vehicleProblem", DbType.String, towing.Vehicle_problem),
            new("Keys", "@keys", DbType.String, towing.Keys),
            new("Site_code", "@siteCode", DbType.Int16, towing.Site_code),
            new(
                "Contact_person_name",
                "@contactPersonName",
                DbType.String,
                towing.Contact_person_name
            ),
            new(
                "Contact_person_tel",
                "@contactPersonTel",
                DbType.String,
                towing.Contact_person_tel
            ),
            new(
                "Contact_person_cell",
                "@contactPersonCell",
                DbType.String,
                towing.Contact_person_cell
            ),
            new(
                "Person_at_vehicle_name",
                "@personAtVehicleName",
                DbType.String,
                towing.Person_at_vehicle_name
            ),
            new(
                "Person_at_vehicle_cell",
                "@personAtVehicleCell",
                DbType.String,
                towing.Person_at_vehicle_cell
            ),
            new("Remaks", "@remaks", DbType.String, towing.Remaks),
            new("Tow_Truck_code", "@towTruckCode", DbType.Int16, towing.Tow_Truck_code),
        ];

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (availableColumns.Contains(column))
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

    private static string GetActiveFilter(
        IReadOnlySet<string> availableColumns,
        string? tableAlias = null
    )
    {
        var prefix = string.IsNullOrWhiteSpace(tableAlias) ? "" : $"{tableAlias}.";
        return availableColumns.Contains("is_deleted")
            ? $"ISNULL({prefix}[is_deleted], 0) = 0"
            : "1 = 1";
    }

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "sql_variant",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetColumnProjection(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetLegacySqlType(column)}) AS [{column}]";

    private static string GetLegacySqlType(string column) =>
        column switch
        {
            "Towing_code" or "Site_code" or "Tow_Truck_code" => "smallint",
            "vmf_code" => "int",
            "Call_refer" => "numeric(18, 0)",
            "Tow_request_date" => "date",
            "Tow_request_time" => "time",
            _ => "varchar(1)",
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).TrimEnd();
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadDateTime(reader, column) : null;

    private static DateTime? ReadTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            TimeSpan time => DateTime.MinValue.Add(time),
            DateTime dateTime => DateTime.MinValue.Add(dateTime.TimeOfDay),
            _ => DateTime.MinValue.Add(TimeSpan.Parse(Convert.ToString(value)!)),
        };
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    ) => columns.Contains(column) ? ReadInt32(reader, column) : null;

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

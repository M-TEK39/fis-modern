using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes the legacy losses table without requiring the expanded
/// audit/status columns to exist. The client database has the original loss
/// fields only, while newer databases may contain additional columns.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come only from fixed compatibility allowlists; all values are parameters."
)]
public sealed class LossRepository : ILossRepository
{
    private const string LossTableName = "losses";
    private const string LossOrderBy = "[l].[loss_date] DESC, [l].[loss_code] DESC";

    private static readonly string[] LegacyColumns =
    [
        "loss_code",
        "vmf_code",
        "loss_date",
        "loss_reference",
        "loss_type_code",
        "site_code",
        "dept_contact",
        "loss_amount",
        "dept_claim",
        "sapd",
        "inspector",
        "case_number",
        "cancelled",
        "cover_forfeit",
        "prosecute",
        "compensation_order",
        "remarks",
        "hq_reference",
        "place_of_loss",
        "garaging_authority",
        "driver_name",
        "report_from_dept",
        "date_reported_ggmt",
        "date_reported_sapd",
        "Call_Refer",
        "Tow_need",
    ];

    private static readonly string[] OptionalColumns =
    [
        "loss_status",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns = ["loss_code", "vmf_code", "loss_date"];

    private static readonly string[] VehicleLookupColumns =
    [
        "vmf_code",
        "fleet_number",
        "registration_number",
    ];

    private static readonly string[] LossTypeLookupColumns = ["loss_type_code", "loss_description"];

    private static readonly string[] SiteLookupColumns = ["site_code", "description"];

    private static readonly IReadOnlyDictionary<string, PropertyInfo> LossProperties = typeof(Loss)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Select(property => new
        {
            Property = property,
            Column = property.GetCustomAttribute<ColumnAttribute>()?.Name,
        })
        .Where(item => !string.IsNullOrWhiteSpace(item.Column))
        .ToDictionary(
            item => item.Column!,
            item => item.Property,
            StringComparer.OrdinalIgnoreCase
        );

    private readonly FisDbContext _context;

    public LossRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Loss?> GetByIdAsync(short lossCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return (
            await QueryAsync(
                $"WHERE [l].[loss_code] = @lossCode AND {GetActiveFilter("l", columns)}",
                command => AddParameter(command, "@lossCode", DbType.Int16, lossCode),
                columns
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Loss>> GetAllAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"WHERE {GetActiveFilter("l", columns)} ORDER BY {LossOrderBy}",
            knownColumns: columns
        );
    }

    public async Task<LossPage> GetPageAsync(LossPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var vmfCode = query.VmfCode is > 0 ? query.VmfCode : null;
        var columns = await GetAvailableColumnsAsync();
        var activeFilter = GetActiveFilter("l", columns);
        var whereClause = vmfCode is null
            ? activeFilter
            : $"[l].[vmf_code] = @vmfCode AND {activeFilter}";
        var orderBy = vmfCode is null ? LossOrderBy : "[l].[loss_date] ASC, [l].[loss_code] ASC";

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{LossTableName}] AS [l]
                WHERE {whereClause}
                """;
            if (vmfCode is not null)
            {
                AddParameter(countCommand, "@vmfCode", DbType.Int32, vmfCode.Value);
            }
            var total = Convert.ToInt32(
                await countCommand.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
            );

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var items = await QueryAsync(
                $"WHERE {whereClause} ORDER BY {orderBy} OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY",
                command =>
                {
                    if (vmfCode is not null)
                    {
                        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode.Value);
                    }
                    AddParameter(command, "@skip", DbType.Int64, skip);
                    AddParameter(command, "@pageSize", DbType.Int32, pageSize);
                },
                columns
            );

            return new LossPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Loss>> GetByVehicleAsync(int vmfCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"WHERE [l].[vmf_code] = @vmfCode AND {GetActiveFilter("l", columns)} ORDER BY [l].[loss_date] ASC, [l].[loss_code] ASC",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            columns
        );
    }

    public async Task<IEnumerable<Loss>> GetBySiteAsync(short siteCode)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.Contains("site_code"))
        {
            return [];
        }

        return await QueryAsync(
            $"WHERE [l].[site_code] = @siteCode AND {GetActiveFilter("l", columns)} ORDER BY [l].[loss_date] DESC, [l].[loss_code] DESC",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode),
            columns
        );
    }

    public async Task<Loss> CreateAsync(Loss loss, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(loss);

        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        loss.loss_date = loss.loss_date == DateTime.MinValue ? now.Date : loss.loss_date;
        loss.date_created = now;
        loss.date_updated = now;
        loss.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        loss.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        loss.is_deleted = false;

        var values = BuildWriteValues(loss, columns, includeKey: false);
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
                INSERT INTO [dbo].[{LossTableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[loss_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            loss.loss_code = Convert.ToInt16(
                await command.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
            );
            return loss;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Loss> UpdateAsync(Loss loss, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(loss);

        var columns = await GetAvailableColumnsAsync();
        var existing =
            await GetByIdIncludingDeletedAsync(loss.loss_code, columns)
            ?? throw new InvalidOperationException(
                $"Loss with loss_code {loss.loss_code} not found"
            );

        var now = DateTime.UtcNow;
        loss.date_created = existing.date_created;
        loss.created_by_user_code = existing.created_by_user_code;
        loss.date_updated = now;
        loss.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        loss.is_deleted = existing.is_deleted;

        var values = BuildWriteValues(loss, columns, includeKey: false);
        if (values.Count == 0)
        {
            return loss;
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
                UPDATE [dbo].[{LossTableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [loss_code] = @lossCode
                """;
            AddParameters(command, values);
            AddParameter(command, "@lossCode", DbType.Int16, loss.loss_code);
            await command.ExecuteNonQueryAsync();
            return loss;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task DeleteAsync(short lossCode, int currentUserId)
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
            if (columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (columns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (columns.Contains("modified_by_user_code"))
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
                    UPDATE [dbo].[{LossTableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [loss_code] = @lossCode
                    AND {GetActiveFilter("", columns)}
                    """;
            }
            else
            {
                // The client-era table has no deletion marker. Preserve its
                // original hard-delete behavior only on that schema.
                command.CommandText = $"""
                    DELETE FROM [dbo].[{LossTableName}]
                    WHERE [loss_code] = @lossCode
                    """;
            }

            AddParameter(command, "@lossCode", DbType.Int16, lossCode);
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

    private async Task<Loss?> GetByIdIncludingDeletedAsync(
        short lossCode,
        IReadOnlySet<string> columns
    ) =>
        (
            await QueryAsync(
                "WHERE [l].[loss_code] = @lossCode",
                command => AddParameter(command, "@lossCode", DbType.Int16, lossCode),
                columns
            )
        ).SingleOrDefault();

    private async Task<List<Loss>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlySet<string>? knownColumns = null
    )
    {
        var columns = knownColumns ?? await GetAvailableColumnsAsync();
        var vehicleColumns = await GetTableColumnsAsync("vehicle_master");
        var lossTypeColumns = await GetTableColumnsAsync("Loss_type");
        var siteColumns = await GetTableColumnsAsync("Site");

        var hasVehicle = VehicleLookupColumns.All(vehicleColumns.Contains);
        var hasLossType =
            columns.Contains("loss_type_code")
            && LossTypeLookupColumns.All(lossTypeColumns.Contains);
        var hasSite = columns.Contains("site_code") && SiteLookupColumns.All(siteColumns.Contains);

        var projection = LegacyColumns
            .Concat(OptionalColumns)
            .Select(column => GetColumnProjection("l", column, columns))
            .Concat(
                GetLookupProjection(
                    "v",
                    "vehicle_fleet_number",
                    "fleet_number",
                    vehicleColumns,
                    hasVehicle
                )
            )
            .Concat(
                GetLookupProjection(
                    "v",
                    "vehicle_registration_number",
                    "registration_number",
                    vehicleColumns,
                    hasVehicle
                )
            )
            .Concat(
                GetLookupProjection(
                    "lt",
                    "loss_type_description",
                    "loss_description",
                    lossTypeColumns,
                    hasLossType
                )
            )
            .Concat(
                GetLookupProjection("s", "site_description", "description", siteColumns, hasSite)
            )
            .ToArray();

        var joins = string.Join(
            Environment.NewLine,
            new[]
            {
                hasVehicle
                    ? "LEFT JOIN [dbo].[vehicle_master] AS [v] ON [v].[vmf_code] = [l].[vmf_code]"
                    : string.Empty,
                hasLossType
                    ? "LEFT JOIN [dbo].[Loss_type] AS [lt] ON [lt].[loss_type_code] = [l].[loss_type_code]"
                    : string.Empty,
                hasSite
                    ? "LEFT JOIN [dbo].[Site] AS [s] ON [s].[site_code] = [l].[site_code]"
                    : string.Empty,
            }.Where(join => !string.IsNullOrWhiteSpace(join))
        );

        var normalizedPredicate = predicate?.Trim();
        var whereClause =
            string.IsNullOrWhiteSpace(normalizedPredicate) ? string.Empty
            : normalizedPredicate.StartsWith("WHERE ", StringComparison.OrdinalIgnoreCase)
            || normalizedPredicate.StartsWith("ORDER BY ", StringComparison.OrdinalIgnoreCase)
                ? normalizedPredicate
            : $"WHERE {normalizedPredicate}";

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
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{LossTableName}] AS [l]
                {joins}
                {whereClause}
                """;
            configure?.Invoke(command);

            var results = new List<Loss>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapLoss(reader));
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        var columns = await GetTableColumnsAsync(LossTableName);
        var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required losses compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }

        return columns;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
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

    private static Loss MapLoss(DbDataReader reader)
    {
        var loss = new Loss();
        foreach (var column in LegacyColumns.Concat(OptionalColumns))
        {
            if (!LossProperties.TryGetValue(column, out var property))
            {
                continue;
            }

            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal))
            {
                continue;
            }

            property.SetValue(loss, ConvertValue(reader.GetValue(ordinal), property.PropertyType));
        }

        loss.vehicle_identifier =
            ReadString(reader, "vehicle_fleet_number")
            ?? ReadString(reader, "vehicle_registration_number");
        loss.loss_type_description = ReadString(reader, "loss_type_description");
        loss.site_description = ReadString(reader, "site_description");
        return loss;
    }

    private static List<WriteValue> BuildWriteValues(
        Loss loss,
        IReadOnlySet<string> availableColumns,
        bool includeKey
    )
    {
        var values = new List<WriteValue>();
        foreach (var column in LegacyColumns.Concat(OptionalColumns))
        {
            if (column.Equals("loss_code", StringComparison.OrdinalIgnoreCase) && !includeKey)
            {
                continue;
            }

            if (
                !availableColumns.Contains(column)
                || !LossProperties.TryGetValue(column, out var property)
            )
            {
                continue;
            }

            values.Add(
                new WriteValue(
                    column,
                    $"@loss_{values.Count}",
                    GetDbType(property.PropertyType),
                    property.GetValue(loss)
                )
            );
        }

        return values;
    }

    private static IEnumerable<string> GetLookupProjection(
        string alias,
        string outputColumn,
        string sourceColumn,
        IReadOnlySet<string> availableColumns,
        bool tableAvailable
    ) =>
        tableAvailable
            ? [$"[{alias}].[{sourceColumn}] AS [{outputColumn}]"]
            : [$"CAST(NULL AS varchar(500)) AS [{outputColumn}]"];

    private static string GetColumnProjection(
        string alias,
        string column,
        IReadOnlySet<string> availableColumns
    ) =>
        availableColumns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetActiveFilter(string alias, IReadOnlySet<string> availableColumns)
    {
        if (!availableColumns.Contains("is_deleted"))
        {
            return "1 = 1";
        }

        var qualifiedColumn = string.IsNullOrWhiteSpace(alias)
            ? "[is_deleted]"
            : $"[{alias}].[is_deleted]";
        return $"ISNULL({qualifiedColumn}, 0) = 0";
    }

    private static string GetSqlType(string column) =>
        column switch
        {
            "loss_code" or "loss_type_code" or "site_code" => "smallint",
            "vmf_code" or "created_by_user_code" or "modified_by_user_code" => "int",
            "loss_amount"
            or "dept_claim"
            or "cancelled"
            or "cover_forfeit"
            or "prosecute"
            or "compensation_order"
            or "garaging_authority"
            or "report_from_dept"
            or "Call_Refer" => "decimal(18, 4)",
            "loss_date"
            or "date_reported_ggmt"
            or "date_reported_sapd"
            or "date_created"
            or "date_updated" => "datetime2",
            "is_deleted" => "bit",
            _ => "varchar(500)",
        };

    private static DbType GetDbType(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => DbType.Boolean,
            TypeCode.Int16 => DbType.Int16,
            TypeCode.Int32 => DbType.Int32,
            TypeCode.Decimal => DbType.Decimal,
            TypeCode.DateTime => DbType.DateTime2,
            TypeCode.String => DbType.String,
            _ => throw new InvalidOperationException(
                $"Unsupported loss property type: {propertyType}"
            ),
        };
    }

    private static object ConvertValue(object value, Type propertyType)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.TrimEnd();
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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads the vehicle status report from the original FIS tables while
/// negotiating columns introduced by the modern schema. This repository must
/// not query the Vehicle entity because EF would select every mapped property,
/// including columns that do not exist in the client's legacy database.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all user values are parameters.")]
public sealed class VehicleStatusReportRepository : IVehicleStatusReportRepository
{
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string MakeTableName = "make";
    private const string TypeTableName = "type";
    private const string SiteTableName = "site";
    private const string StatusTableName = "vehicle_status";
    private const string RemarksTableName = "vehicle_remarks";

    private static readonly string[] RequiredVehicleColumns =
    [
        "vmf_code",
        "model_code",
        "type_code",
        "vehicle_status_code",
        "location_code",
        "fleet_number",
        "registration_number",
        "take_on_date",
        "current_odo",
        "engine_number_1",
        "chassis_number",
        "year_manufactured",
        "invoice_number",
        "vs_code"
    ];

    private static readonly string[] RequiredRemarkColumns =
    [
        "remark_id",
        "vmf_code",
        "remark_category",
        "remark_text",
        "is_resolved"
    ];

    private readonly FisDbContext _context;

    public VehicleStatusReportRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VehicleStatusReportPage> GetPageAsync(VehicleStatusReportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var vehicleColumns = await GetColumnsAsync(connection, VehicleTableName, RequiredVehicleColumns, transaction);
            var modelColumns = await GetColumnsAsync(connection, ModelTableName, transaction);
            var makeColumns = await GetColumnsAsync(connection, MakeTableName, transaction);
            var typeColumns = await GetColumnsAsync(connection, TypeTableName, transaction);
            var siteColumns = await GetColumnsAsync(connection, SiteTableName, transaction);
            var statusColumns = await GetColumnsAsync(connection, StatusTableName, transaction);
            var remarkColumns = await GetColumnsAsync(connection, RemarksTableName, transaction);

            var vehicles = await QueryVehiclesAsync(connection, transaction, vehicleColumns, modelColumns, query);
            var remarksAvailable = RequiredRemarkColumns.All(remarkColumns.Contains);
            if (remarksAvailable && vehicles.Count > 0)
            {
                var remarks = await QueryActiveRemarksAsync(
                    connection,
                    transaction,
                    remarkColumns,
                    vehicles.Select(vehicle => vehicle.VmfCode));
                vehicles = vehicles
                    .Select(vehicle => vehicle with
                    {
                        ActiveRemark = remarks.TryGetValue(vehicle.VmfCode, out var remark) ? remark : null
                    })
                    .ToList();
            }

            return new VehicleStatusReportPage(
                vehicles,
                await QueryLookupAsync(connection, transaction, SiteTableName, "Site_code", "description", siteColumns),
                await QueryLookupAsync(connection, transaction, TypeTableName, "type_code", "type_description", typeColumns),
                await QueryLookupAsync(connection, transaction, MakeTableName, "make_code", "make_description", makeColumns),
                await QueryModelLookupAsync(connection, transaction, modelColumns),
                await QueryLookupAsync(connection, transaction, StatusTableName, "vehicle_status_code", "status_description", statusColumns),
                remarksAvailable);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<VehicleStatusReportVehicle>> QueryVehiclesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> vehicleColumns,
        IReadOnlySet<string> modelColumns,
        VehicleStatusReportQuery query)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        var conditions = new List<string>
        {
            GetNotDeletedFilter("v", vehicleColumns)
        };

        if (query.VehicleStatusCode.HasValue)
        {
            conditions.Add("[v].[vehicle_status_code] = @vehicleStatusCode");
            AddParameter(command, "@vehicleStatusCode", DbType.Int16, query.VehicleStatusCode.Value);
        }
        else
        {
            conditions.Add("[v].[vehicle_status_code] IN (@inServiceStatus, @withdrawnStatus)");
            AddParameter(command, "@inServiceStatus", DbType.Int16, 1);
            AddParameter(command, "@withdrawnStatus", DbType.Int16, 2);
        }

        AddCodeFilter(conditions, command, "[v].[vs_code]", "@vehicleSourceCode", DbType.Byte, query.VehicleSourceCode);
        AddCodeFilter(conditions, command, "[v].[type_code]", "@typeCode", DbType.Int16, query.TypeCode);
        AddCodeFilter(conditions, command, "[v].[location_code]", "@locationCode", DbType.Int16, query.LocationCode);
        AddCodeFilter(conditions, command, "[v].[model_code]", "@modelCode", DbType.Int16, query.ModelCode);

        if (query.MakeCode.HasValue)
        {
            if (modelColumns.Contains("model_code") && modelColumns.Contains("make_code"))
            {
                conditions.Add("EXISTS (SELECT 1 FROM [dbo].[model] AS [make_filter_model] WHERE [make_filter_model].[model_code] = [v].[model_code] AND [make_filter_model].[make_code] = @makeCode)");
                AddParameter(command, "@makeCode", DbType.Int16, query.MakeCode.Value);
            }
            else
            {
                // Ignoring a requested make filter would return incorrect rows.
                conditions.Add("1 = 0");
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            conditions.Add("(" + string.Join(
                " OR ",
                [
                    "LOWER(COALESCE([v].[fleet_number], '')) LIKE @search",
                    "LOWER(COALESCE([v].[registration_number], '')) LIKE @search",
                    "LOWER(COALESCE([v].[chassis_number], '')) LIKE @search",
                    "LOWER(COALESCE([v].[engine_number_1], '')) LIKE @search",
                    "LOWER(COALESCE([v].[invoice_number], '')) LIKE @search"
                ]) + ")");
            AddParameter(command, "@search", DbType.String, $"%{query.Search.Trim().ToLowerInvariant()}%");
        }

        var dateCreated = GetDateExpression("v", vehicleColumns, "date_created", "captured_date", "take_on_date");
        command.CommandText = $"""
            SELECT
                [v].[vmf_code] AS [vmf_code],
                [v].[fleet_number] AS [fleet_number],
                [v].[registration_number] AS [registration_number],
                [v].[vehicle_status_code] AS [vehicle_status_code],
                [v].[type_code] AS [type_code],
                [v].[vs_code] AS [vs_code],
                [v].[model_code] AS [model_code],
                [v].[location_code] AS [location_code],
                [v].[chassis_number] AS [chassis_number],
                [v].[engine_number_1] AS [engine_number_1],
                [v].[year_manufactured] AS [year_manufactured],
                [v].[take_on_date] AS [take_on_date],
                [v].[invoice_number] AS [invoice_number],
                {dateCreated} AS [date_created],
                [v].[current_odo] AS [current_odo]
            FROM [dbo].[{VehicleTableName}] AS [v]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY COALESCE([v].[fleet_number], ''), [v].[vmf_code]
            """;

        var results = new List<VehicleStatusReportVehicle>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new VehicleStatusReportVehicle(
                ReadInt32(reader, "vmf_code") ?? 0,
                ReadString(reader, "fleet_number"),
                ReadString(reader, "registration_number"),
                ReadInt16(reader, "vehicle_status_code"),
                ReadInt16(reader, "type_code"),
                ReadByte(reader, "vs_code"),
                ReadInt16(reader, "model_code"),
                ReadInt16(reader, "location_code"),
                ReadString(reader, "chassis_number"),
                ReadString(reader, "engine_number_1"),
                ReadInt16(reader, "year_manufactured"),
                ReadDateTime(reader, "take_on_date"),
                ReadString(reader, "invoice_number"),
                ReadDateTime(reader, "date_created"),
                ReadInt32(reader, "current_odo"),
                null));
        }

        return results;
    }

    private static async Task<IReadOnlyList<VehicleStatusReportLookup>> QueryLookupAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string tableName,
        string codeColumn,
        string descriptionColumn,
        IReadOnlySet<string> columns)
    {
        if (!columns.Contains(codeColumn) || !columns.Contains(descriptionColumn))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var notDeleted = GetNotDeletedFilter(string.Empty, columns);
        command.CommandText = $"""
            SELECT [{codeColumn}] AS [lookup_code], [{descriptionColumn}] AS [lookup_description]
            FROM [dbo].[{tableName}]
            WHERE {notDeleted}
            ORDER BY COALESCE([{descriptionColumn}], ''), [{codeColumn}]
            """;

        var results = new List<VehicleStatusReportLookup>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = ReadInt32(reader, "lookup_code");
            if (code.HasValue)
            {
                results.Add(new VehicleStatusReportLookup(code.Value, ReadString(reader, "lookup_description") ?? string.Empty));
            }
        }

        return results;
    }

    private static async Task<IReadOnlyList<VehicleStatusReportModelLookup>> QueryModelLookupAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> columns)
    {
        if (!columns.Contains("model_code") || !columns.Contains("model_description"))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var makeCode = columns.Contains("make_code")
            ? "[make_code]"
            : "CAST(NULL AS smallint)";
        var notDeleted = GetNotDeletedFilter(string.Empty, columns);
        command.CommandText = $"""
            SELECT [model_code] AS [model_code],
                   [model_description] AS [model_description],
                   {makeCode} AS [make_code]
            FROM [dbo].[{ModelTableName}]
            WHERE {notDeleted}
            ORDER BY COALESCE([model_description], ''), [model_code]
            """;

        var results = new List<VehicleStatusReportModelLookup>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var code = ReadInt32(reader, "model_code");
            if (code.HasValue)
            {
                results.Add(new VehicleStatusReportModelLookup(
                    code.Value,
                    ReadString(reader, "model_description") ?? string.Empty,
                    ReadInt32(reader, "make_code")));
            }
        }

        return results;
    }

    private static async Task<Dictionary<int, VehicleStatusReportRemark>> QueryActiveRemarksAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> columns,
        IEnumerable<int> vmfCodes)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var codeParameters = vmfCodes
            .Distinct()
            .Select((vmfCode, index) =>
            {
                var name = $"@vmfCode{index}";
                AddParameter(command, name, DbType.Int32, vmfCode);
                return name;
            })
            .ToArray();

        if (codeParameters.Length == 0)
        {
            return [];
        }

        var conditions = new List<string>
        {
            "[vmf_code] IN (" + string.Join(", ", codeParameters) + ")",
            "[is_resolved] = 0",
            GetNotDeletedFilter(string.Empty, columns)
        };
        var dateCreated = GetDateExpression(string.Empty, columns, "date_created");
        command.CommandText = $"""
            SELECT [remark_id] AS [remark_id],
                   [vmf_code] AS [vmf_code],
                   [remark_category] AS [remark_category],
                   [remark_text] AS [remark_text],
                   {dateCreated} AS [date_created]
            FROM [dbo].[{RemarksTableName}]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY {dateCreated} DESC, [remark_id] DESC
            """;

        var results = new Dictionary<int, VehicleStatusReportRemark>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var vmfCode = ReadInt32(reader, "vmf_code");
            if (!vmfCode.HasValue || results.ContainsKey(vmfCode.Value))
            {
                continue;
            }

            results[vmfCode.Value] = new VehicleStatusReportRemark(
                ReadInt32(reader, "remark_id") ?? 0,
                ReadString(reader, "remark_category"),
                ReadString(reader, "remark_text"),
                ReadDateTime(reader, "date_created"));
        }

        return results;
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        DbTransaction? transaction)
        => await GetColumnsAsync(connection, tableName, null, transaction);

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyCollection<string>? requiredColumns,
        DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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

        if (requiredColumns is not null)
        {
            var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException($"The required vehicle status report columns are not available on {tableName}: {string.Join(", ", missingColumns)}");
            }
        }

        return columns;
    }

    private static void AddCodeFilter(
        ICollection<string> conditions,
        DbCommand command,
        string column,
        string parameterName,
        DbType type,
        object? value)
    {
        if (value is null)
        {
            return;
        }

        conditions.Add($"{column} = {parameterName}");
        AddParameter(command, parameterName, type, value);
    }

    private static string GetNotDeletedFilter(string alias, IReadOnlySet<string> columns)
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : $"[{alias}].";
        return columns.Contains("is_deleted")
            ? $"({prefix}[is_deleted] = 0 OR {prefix}[is_deleted] IS NULL)"
            : "1 = 1";
    }

    private static string GetDateExpression(string alias, IReadOnlySet<string> columns, params string[] candidates)
    {
        var prefix = string.IsNullOrEmpty(alias) ? string.Empty : $"[{alias}].";
        var expressions = candidates
            .Where(columns.Contains)
            .Select(column => $"{prefix}[{column}]")
            .ToArray();
        return expressions.Length switch
        {
            0 => "CAST(NULL AS datetime2)",
            1 => expressions[0],
            _ => $"COALESCE({string.Join(", ", expressions)})"
        };
    }

    private static string? ReadString(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : reader[column]?.ToString();

    private static int? ReadInt32(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static short? ReadInt16(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static byte? ReadByte(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToByte(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

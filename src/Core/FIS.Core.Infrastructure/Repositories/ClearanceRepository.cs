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
/// Persists the original clearance workflow against both the client schema
/// and the expanded schema. The audit columns are optional, so this repository
/// deliberately avoids EF materialization for this table.
/// </summary>
public class ClearanceRepository : IClearanceRepository
{
    private const string TableName = "clearance";

    private static readonly string[] LegacyColumns =
    [
        "clearance_code",
        "vmf_code",
        "clearance_number",
        "Clearance_date",
        "Merchant_code",
        "Clearance_amount",
        "clearance_comment",
        "clearance_kilo",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredColumns =
    [
        "clearance_code",
        "vmf_code",
        "clearance_number",
        "Clearance_date",
        "Merchant_code",
        "Clearance_amount",
        "clearance_comment",
        "clearance_kilo",
    ];

    private readonly FisDbContext _context;

    public ClearanceRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Clearance?> GetByIdAsync(int clearanceCode) =>
        (
            await QueryAsync(
                "[clearance_code] = @clearanceCode",
                command => AddParameter(command, "@clearanceCode", DbType.Int32, clearanceCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Clearance>> GetAllAsync() => await QueryAsync();

    public async Task<IEnumerable<Clearance>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<ClearanceLookupResult?> LookupVehicleAsync(string fleetOrReg)
    {
        if (string.IsNullOrWhiteSpace(fleetOrReg))
        {
            return null;
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
            command.CommandText = """
                SELECT TOP (1) [vmf_code], [fleet_number], [registration_number]
                FROM [dbo].[vehicle_master]
                WHERE [fleet_number] = @identifier
                   OR [registration_number] = @identifier
                ORDER BY [fleet_number]
                """;
            AddParameter(command, "@identifier", DbType.String, fleetOrReg.Trim());

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            return new ClearanceLookupResult
            {
                vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
                fleet_number = ReadString(reader, "fleet_number"),
                registration_number = ReadString(reader, "registration_number"),
            };
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
        Justification = "The report query is composed only from fixed legacy columns, fixed table names, and allowlisted optional filters; values are parameters."
    )]
    public async Task<IReadOnlyList<ClearanceReportRow>> GetUniversalReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? merchantCode
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

            var conditions = new List<string>
            {
                "c.[vmf_code] > 1",
                GetActiveFilter("c", availableColumns),
            };

            if (startDate.HasValue)
            {
                conditions.Add("c.[Clearance_date] > @startDate");
                AddParameter(command, "@startDate", DbType.DateTime2, startDate.Value);
            }

            if (endDate.HasValue)
            {
                conditions.Add("c.[Clearance_date] < @endDate");
                AddParameter(command, "@endDate", DbType.DateTime2, endDate.Value);
            }

            if (merchantCode.HasValue && merchantCode.Value > 0)
            {
                conditions.Add("c.[Merchant_code] = @merchantCode");
                AddParameter(command, "@merchantCode", DbType.Int32, merchantCode.Value);
            }

            command.CommandText = $"""
                SELECT c.[clearance_code],
                       v.[fleet_number],
                       c.[clearance_comment],
                       m.[Merchant_Name],
                       c.[clearance_number],
                       c.[Clearance_date]
                FROM [dbo].[{TableName}] AS c
                INNER JOIN [dbo].[vehicle_master] AS v ON v.[vmf_code] = c.[vmf_code]
                INNER JOIN [dbo].[Merchant] AS m ON m.[Merchant_code] = c.[Merchant_code]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY c.[vmf_code]
                """;

            var results = new List<ClearanceReportRow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(
                    new ClearanceReportRow
                    {
                        clearance_code = ReadInt32(reader, "clearance_code"),
                        fleet_number = ReadString(reader, "fleet_number"),
                        clearance_comment = ReadString(reader, "clearance_comment"),
                        merchant_name = ReadString(reader, "Merchant_Name"),
                        clearance_number = ReadInt32(reader, "clearance_number"),
                        clearance_date = ReadDateTime(reader, "Clearance_date"),
                    }
                );
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

    public async Task<Clearance> CreateAsync(Clearance clearance, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(clearance);

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(clearance)
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

        clearance.clearance_code = await ExecuteInsertAsync(values);
        clearance.date_created = now;
        clearance.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        clearance.is_deleted = false;
        return clearance;
    }

    public async Task<Clearance> UpdateAsync(Clearance clearance, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(clearance);

        var existing = await GetByIdAsync(clearance.clearance_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Clearance with clearance_code {clearance.clearance_code} not found"
            );
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(clearance)
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
            clearance.is_deleted
        );

        await ExecuteUpdateAsync(clearance.clearance_code, values, availableColumns);
        clearance.date_created = existing.date_created;
        clearance.created_by_user_code = existing.created_by_user_code;
        clearance.date_updated = now;
        clearance.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return clearance;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(int clearanceCode, int currentUserId)
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
                    WHERE [clearance_code] = @clearanceCode
                      AND {GetActiveFilter(null, availableColumns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [clearance_code] = @clearanceCode
                    """;
            }

            AddParameter(command, "@clearanceCode", DbType.Int32, clearanceCode);
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
        Justification = "The SELECT list and filters are composed only from fixed legacy columns and allowlisted optional columns; values are parameters."
    )]
    private async Task<List<Clearance>> QueryAsync(
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
            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            conditions.Add(GetActiveFilter(null, availableColumns));
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY [Clearance_date] DESC, [clearance_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Clearance>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapClearance(reader, availableColumns));
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
    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
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
                OUTPUT INSERTED.[clearance_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
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
        int clearanceCode,
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
                WHERE [clearance_code] = @clearanceCode
                  AND {GetActiveFilter(null, availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@clearanceCode", DbType.Int32, clearanceCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
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
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required clearance compatibility columns are not available: {string.Join(", ", missingColumns)}"
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

    private static Clearance MapClearance(
        DbDataReader reader,
        IReadOnlySet<string> availableColumns
    ) =>
        new()
        {
            clearance_code = ReadInt32(reader, "clearance_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            clearance_number = ReadInt32(reader, "clearance_number"),
            Clearance_date = ReadDateTime(reader, "Clearance_date"),
            Merchant_code = ReadInt32(reader, "Merchant_code"),
            Clearance_amount = ReadDecimal(reader, "Clearance_amount"),
            clearance_comment = ReadString(reader, "clearance_comment"),
            clearance_kilo = ReadInt32(reader, "clearance_kilo"),
            date_created =
                ReadDateTimeIfAvailable(reader, availableColumns, "date_created")
                ?? ReadDateTime(reader, "Clearance_date")
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

    private static List<WriteValue> BuildLegacyWriteValues(Clearance clearance) =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, clearance.vmf_code),
            new("clearance_number", "@clearanceNumber", DbType.Int32, clearance.clearance_number),
            new("Clearance_date", "@clearanceDate", DbType.DateTime2, clearance.Clearance_date),
            new("Merchant_code", "@merchantCode", DbType.Int32, clearance.Merchant_code),
            new("Clearance_amount", "@clearanceAmount", DbType.Decimal, clearance.Clearance_amount),
            new(
                "clearance_comment",
                "@clearanceComment",
                DbType.String,
                clearance.clearance_comment
            ),
            new("clearance_kilo", "@clearanceKilo", DbType.Int32, clearance.clearance_kilo),
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

    private static string GetActiveFilter(string? alias, IReadOnlySet<string> availableColumns)
    {
        var prefix = string.IsNullOrWhiteSpace(alias) ? string.Empty : $"{alias}.";
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
            "clearance_code"
            or "vmf_code"
            or "clearance_number"
            or "Merchant_code"
            or "clearance_kilo" => "int",
            "Clearance_date" => "datetime2",
            "Clearance_amount" => "decimal(18, 2)",
            _ => "varchar(1)",
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
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

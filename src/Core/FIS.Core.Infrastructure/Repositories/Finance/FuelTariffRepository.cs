using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes dbo.fuel_tariff without requiring expanded audit columns.
/// Soft-delete is used only when is_deleted exists; otherwise delete is a
/// labelled hard DELETE of the live row.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come from fixed compatibility allowlists; predicates and values are parameterized."
)]
public class FuelTariffRepository : IFuelTariffRepository
{
    private const string TableName = "fuel_tariff";
    private const string SchemaName = "dbo";
    private static readonly string[] RequiredColumns =
    [
        "fuel_tariff_code",
        "fuel_type_code",
        "fuel_tariff",
        "start_date",
        "end_date",
    ];
    private static readonly string[] OptionalColumns =
    [
        "fuel_tariff_notes",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<FuelTariffRepository> _logger;

    public FuelTariffRepository(FisDbContext context, ILogger<FuelTariffRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FuelTariff?> GetByIdAsync(short fuelTariffCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            $"[fuel_tariff_code] = @fuelTariffCode AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@fuelTariffCode", DbType.Int16, fuelTariffCode),
            columns
        );
    }

    public async Task<FuelTariff?> GetCurrentTariffAsync(short fuelTypeCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            $"[fuel_type_code] = @fuelTypeCode AND [end_date] IS NULL AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@fuelTypeCode", DbType.Int16, fuelTypeCode),
            columns
        );
    }

    public async Task<IEnumerable<FuelTariff>> GetTariffHistoryAsync(short fuelTypeCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"[fuel_type_code] = @fuelTypeCode AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@fuelTypeCode", DbType.Int16, fuelTypeCode),
            columns
        );
    }

    public async Task<IEnumerable<FuelTariff>> GetAllCurrentTariffsAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            $"[end_date] IS NULL AND {GetNotDeletedPredicate(columns)}",
            configure: null,
            columns
        );
    }

    public async Task<FuelTariff> CreateAsync(FuelTariff fuelTariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fuelTariff);
        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = new List<WriteValue>();
        AddValue(values, columns, "fuel_type_code", "@fuelTypeCode", DbType.Int16, fuelTariff.fuel_type_code);
        AddValue(values, columns, "fuel_tariff", "@fuelTariff", DbType.Decimal, fuelTariff.fuel_tariff);
        AddValue(values, columns, "fuel_tariff_notes", "@notes", DbType.String, fuelTariff.fuel_tariff_notes);
        AddValue(values, columns, "start_date", "@startDate", DbType.DateTime2, fuelTariff.start_date);
        AddValue(values, columns, "end_date", "@endDate", DbType.DateTime2, fuelTariff.end_date);
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(values, columns, "created_by_user_code", "@createdBy", DbType.Int32, currentUserId);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for inserting into dbo.fuel_tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [{SchemaName}].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[fuel_tariff_code]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        fuelTariff.fuel_tariff_code = Convert.ToInt16(await command.ExecuteScalarAsync());

        _logger.LogInformation(
            "Created fuel tariff {TariffCode} for fuel type {FuelTypeCode} with rate {Rate}",
            fuelTariff.fuel_tariff_code,
            fuelTariff.fuel_type_code,
            fuelTariff.fuel_tariff
        );

        return await GetByIdAsync(fuelTariff.fuel_tariff_code)
            ?? throw new InvalidOperationException("Created fuel tariff could not be read.");
    }

    public async Task<FuelTariff> UpdateAsync(FuelTariff fuelTariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fuelTariff);
        _ =
            await GetByIdAsync(fuelTariff.fuel_tariff_code)
            ?? throw new KeyNotFoundException($"Fuel tariff {fuelTariff.fuel_tariff_code} not found");

        var columns = await GetAvailableColumnsAsync();
        var assignments = new List<string>();
        var values = new List<WriteValue>();
        AddAssignment(assignments, values, columns, "fuel_tariff", "@fuelTariff", DbType.Decimal, fuelTariff.fuel_tariff);
        AddAssignment(assignments, values, columns, "fuel_tariff_notes", "@notes", DbType.String, fuelTariff.fuel_tariff_notes);
        AddAssignment(assignments, values, columns, "start_date", "@startDate", DbType.DateTime2, fuelTariff.start_date);
        AddAssignment(assignments, values, columns, "end_date", "@endDate", DbType.DateTime2, fuelTariff.end_date);
        AddAssignment(assignments, values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
        AddAssignment(assignments, values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, currentUserId);

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for updating dbo.fuel_tariff."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [{SchemaName}].[{TableName}]
            SET {string.Join(", ", assignments)}
            WHERE [fuel_tariff_code] = @fuelTariffCode
            """;
        AddParameters(command, values);
        AddParameter(command, "@fuelTariffCode", DbType.Int16, fuelTariff.fuel_tariff_code);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"Fuel tariff {fuelTariff.fuel_tariff_code} not found");
        }

        _logger.LogInformation(
            "Updated fuel tariff {TariffCode} for fuel type {FuelTypeCode}",
            fuelTariff.fuel_tariff_code,
            fuelTariff.fuel_type_code
        );

        return await GetByIdAsync(fuelTariff.fuel_tariff_code)
            ?? throw new InvalidOperationException("Updated fuel tariff could not be read.");
    }

    public async Task<FuelTariff> CreateNewRateAsync(
        short fuelTypeCode,
        decimal newRate,
        string? notes,
        int currentUserId
    )
    {
        var currentTariff = await GetCurrentTariffAsync(fuelTypeCode);
        if (currentTariff != null)
        {
            currentTariff.end_date = DateTime.UtcNow;
            await UpdateAsync(currentTariff, currentUserId);
            _logger.LogInformation(
                "Closed fuel tariff {TariffCode} for fuel type {FuelTypeCode}",
                currentTariff.fuel_tariff_code,
                fuelTypeCode
            );
        }

        return await CreateAsync(
            new FuelTariff
            {
                fuel_type_code = fuelTypeCode,
                fuel_tariff = newRate,
                fuel_tariff_notes = notes,
                start_date = DateTime.UtcNow,
                end_date = null,
            },
            currentUserId
        );
    }

    public async Task DeleteAsync(short fuelTariffCode, int currentUserId)
    {
        var existing =
            await GetByIdIncludingDeletedAsync(fuelTariffCode)
            ?? throw new KeyNotFoundException($"Fuel tariff {fuelTariffCode} not found");

        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (columns.Contains("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            var values = new List<WriteValue>
            {
                new("is_deleted", "@isDeleted", DbType.Boolean, true),
            };
            AddAssignment(assignments, values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            AddAssignment(assignments, values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, currentUserId);
            command.CommandText = $"""
                UPDATE [{SchemaName}].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [fuel_tariff_code] = @fuelTariffCode
                """;
            AddParameters(command, values);
        }
        else
        {
            command.CommandText = $"""
                DELETE FROM [{SchemaName}].[{TableName}]
                WHERE [fuel_tariff_code] = @fuelTariffCode
                """;
        }

        AddParameter(command, "@fuelTariffCode", DbType.Int16, fuelTariffCode);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"Fuel tariff {fuelTariffCode} not found");
        }

        _logger.LogInformation(
            columns.Contains("is_deleted")
                ? "Soft deleted fuel tariff {TariffCode}"
                : "Deleted fuel tariff {TariffCode} because dbo.fuel_tariff has no is_deleted column",
            existing.fuel_tariff_code
        );
    }

    private async Task<FuelTariff?> GetByIdIncludingDeletedAsync(short fuelTariffCode)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            "[fuel_tariff_code] = @fuelTariffCode",
            command => AddParameter(command, "@fuelTariffCode", DbType.Int16, fuelTariffCode),
            columns
        );
    }

    private async Task<List<FuelTariff>> QueryAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns,
        bool firstOnly = false
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            $"SELECT {(firstOnly ? "TOP (1) " : string.Empty)}{BuildProjection(columns)} FROM [{SchemaName}].[{TableName}] WHERE {predicate} ORDER BY [start_date] DESC, [fuel_tariff_code] DESC";
        configure?.Invoke(command);

        var result = new List<FuelTariff>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapFuelTariff(reader));
        }

        return result;
    }

    private async Task<FuelTariff?> QueryOneAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns
    ) => (await QueryAsync(predicate, configure, columns, firstOnly: true)).SingleOrDefault();

    private static string BuildProjection(IReadOnlySet<string> columns) =>
        string.Join(
            ", ",
            RequiredColumns
                .Concat(OptionalColumns)
                .Select(column =>
                    columns.Contains(column)
                        ? $"[{column}] AS [{column}]"
                        : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]"
                )
        );

    private static string GetNotDeletedPredicate(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted")
            ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
            : "1 = 1";

    private static string GetSqlType(string column) =>
        column switch
        {
            "fuel_tariff_code" or "fuel_type_code" => "smallint",
            "fuel_tariff" => "decimal(18, 4)",
            "fuel_tariff_notes" => "varchar(1000)",
            "is_deleted" => "bit",
            "created_by_user_code" or "modified_by_user_code" => "int",
            _ => "datetime2",
        };

    private static FuelTariff MapFuelTariff(DbDataReader reader) =>
        new()
        {
            fuel_tariff_code = Convert.ToInt16(reader["fuel_tariff_code"]),
            fuel_type_code = Convert.ToInt16(reader["fuel_type_code"]),
            fuel_tariff = reader["fuel_tariff"] is DBNull ? 0m : Convert.ToDecimal(reader["fuel_tariff"]),
            fuel_tariff_notes = reader["fuel_tariff_notes"] is DBNull
                ? null
                : reader["fuel_tariff_notes"]?.ToString(),
            start_date = reader["start_date"] is DBNull
                ? default
                : Convert.ToDateTime(reader["start_date"]),
            end_date = reader["end_date"] is DBNull ? null : Convert.ToDateTime(reader["end_date"]),
            date_created = reader["date_created"] is DBNull
                ? default
                : Convert.ToDateTime(reader["date_created"]),
            date_updated = reader["date_updated"] is DBNull
                ? null
                : Convert.ToDateTime(reader["date_updated"]),
            created_by_user_code = reader["created_by_user_code"] is DBNull
                ? null
                : Convert.ToInt32(reader["created_by_user_code"]),
            modified_by_user_code = reader["modified_by_user_code"] is DBNull
                ? null
                : Convert.ToInt32(reader["modified_by_user_code"]),
            is_deleted = reader["is_deleted"] is not DBNull && Convert.ToBoolean(reader["is_deleted"]),
        };

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, SchemaName);
        AddParameter(command, "@table", DbType.String, TableName);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        if (columns.Count == 0)
        {
            throw new InvalidOperationException(
                "The legacy dbo.fuel_tariff table is unavailable. No modern fuel-tariff fallback was run."
            );
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The legacy dbo.fuel_tariff table is missing required columns: {string.Join(", ", missing)}"
            );
        }

        return columns;
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (columns.Contains(column))
            values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddAssignment(
        ICollection<string> assignments,
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (!columns.Contains(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.DbType, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType DbType, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection => connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}

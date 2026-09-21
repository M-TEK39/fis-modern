using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads fin.MaintenanceValue without requiring TariffParameterID or audit
/// columns. The 2012 archive is a single class/age matrix. Updates go through
/// fin.DEV_UPD_MaintenanceValue when that procedure exists.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Schema, table, and column identifiers are fixed compatibility allowlists; values are parameterized."
)]
public class MaintenanceValueRepository : IMaintenanceValueRepository
{
    private const string SchemaName = "fin";
    private const string TableName = "MaintenanceValue";
    private static readonly string[] RequiredColumns =
    [
        "class_code",
        "class_number",
        "months_age",
        "kilometer_age",
        "amount",
        "RandPerKilometer",
    ];
    private static readonly string[] OptionalColumns =
    [
        "TariffParameterID",
        "CaptureDate",
        "ModifiedDate",
        "user_access_code",
        "user_access_name",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public MaintenanceValueRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MaintenanceValue?> GetByClassAndAgeAsync(
        int tariffParameterId,
        int classCode,
        int monthsAge,
        int kilometerAge
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var predicate =
            $"{GetNotDeletedPredicate(columns)} AND [class_code] = @classCode AND [months_age] <= @monthsAge AND [kilometer_age] <= @kilometerAge";
        if (columns.Contains("TariffParameterID"))
            predicate += " AND [TariffParameterID] = @tariffParameterId";

        var rows = await QueryAsync(
            predicate,
            command =>
            {
                AddParameter(command, "@classCode", DbType.Int16, (short)classCode);
                AddParameter(command, "@monthsAge", DbType.Int16, (short)monthsAge);
                AddParameter(command, "@kilometerAge", DbType.Int32, kilometerAge);
                if (columns.Contains("TariffParameterID"))
                    AddParameter(command, "@tariffParameterId", DbType.Int32, tariffParameterId);
            },
            columns,
            "[months_age] DESC, [kilometer_age] DESC",
            firstOnly: true
        );
        return rows.SingleOrDefault();
    }

    public async Task<List<MaintenanceValue>> GetByTariffParameterAsync(int tariffParameterId)
    {
        var columns = await GetAvailableColumnsAsync();
        var predicate = GetNotDeletedPredicate(columns);
        Action<DbCommand>? configure = null;
        if (columns.Contains("TariffParameterID"))
        {
            predicate += " AND [TariffParameterID] = @tariffParameterId";
            configure = command =>
                AddParameter(command, "@tariffParameterId", DbType.Int32, tariffParameterId);
        }

        return await QueryAsync(
            predicate,
            configure,
            columns,
            "[class_code], [months_age], [kilometer_age]"
        );
    }

    public async Task<List<MaintenanceValue>> GetByClassAsync(int tariffParameterId, int classCode)
    {
        var columns = await GetAvailableColumnsAsync();
        var predicate = $"{GetNotDeletedPredicate(columns)} AND [class_code] = @classCode";
        Action<DbCommand> configure = command =>
            AddParameter(command, "@classCode", DbType.Int16, (short)classCode);
        if (columns.Contains("TariffParameterID"))
        {
            predicate += " AND [TariffParameterID] = @tariffParameterId";
            configure = command =>
            {
                AddParameter(command, "@classCode", DbType.Int16, (short)classCode);
                AddParameter(command, "@tariffParameterId", DbType.Int32, tariffParameterId);
            };
        }

        return await QueryAsync(
            predicate,
            configure,
            columns,
            "[months_age], [kilometer_age]"
        );
    }

    public async Task<MaintenanceValue?> GetByIdAsync(int maintenanceValueId)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.Contains("TariffParameterID"))
        {
            throw new InvalidOperationException(
                "The archive fin.MaintenanceValue table has no TariffParameterID. Lookup requires class_number, months_age, and kilometer_age. No substitute row was returned."
            );
        }

        var rows = await QueryAsync(
            $"{GetNotDeletedPredicate(columns)} AND [TariffParameterID] = @id",
            command => AddParameter(command, "@id", DbType.Int32, maintenanceValueId),
            columns,
            "[class_code], [months_age], [kilometer_age]",
            firstOnly: true
        );
        return rows.SingleOrDefault();
    }

    public async Task<MaintenanceValue> CreateAsync(
        MaintenanceValue maintenanceValue,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(maintenanceValue);
        return await UpsertAsync(maintenanceValue, currentUserId);
    }

    public async Task<MaintenanceValue> UpdateAsync(
        MaintenanceValue maintenanceValue,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(maintenanceValue);
        return await UpsertAsync(maintenanceValue, currentUserId);
    }

    public async Task DeleteAsync(int maintenanceValueId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.Contains("TariffParameterID"))
        {
            throw new InvalidOperationException(
                "The archive fin.MaintenanceValue table has no TariffParameterID. Delete requires class_number, months_age, and kilometer_age. No rows were deleted."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = columns.Contains("is_deleted")
            ? """
                UPDATE [fin].[MaintenanceValue]
                SET [is_deleted] = 1
                WHERE [TariffParameterID] = @id
                """
            : """
                DELETE FROM [fin].[MaintenanceValue]
                WHERE [TariffParameterID] = @id
                """;
        AddParameter(command, "@id", DbType.Int32, maintenanceValueId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.Contains("TariffParameterID"))
        {
            throw new InvalidOperationException(
                "The archive fin.MaintenanceValue table has no TariffParameterID. Deleting the whole class/age matrix was not run."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = columns.Contains("is_deleted")
            ? """
                UPDATE [fin].[MaintenanceValue]
                SET [is_deleted] = 1
                WHERE [TariffParameterID] = @id
                """
            : """
                DELETE FROM [fin].[MaintenanceValue]
                WHERE [TariffParameterID] = @id
                """;
        AddParameter(command, "@id", DbType.Int32, tariffParameterId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<MaintenanceValue> UpsertAsync(MaintenanceValue value, int currentUserId)
    {
        if (await ProcedureExistsAsync("DEV_UPD_MaintenanceValue"))
        {
            await ExecuteUpdateProcedureAsync(value, currentUserId);
            var rows = await GetByClassAsync(value.TariffParameterID, value.class_code);
            return rows.FirstOrDefault(row =>
                    string.Equals(row.class_number, value.class_number, StringComparison.OrdinalIgnoreCase)
                    && row.months_age == value.months_age
                    && row.kilometer_age == value.kilometer_age
                )
                ?? throw new InvalidOperationException(
                    "fin.DEV_UPD_MaintenanceValue completed but the maintenance value could not be read. A zero RandPerKilometer deletes the row."
                );
        }

        return await MergeDirectAsync(value, currentUserId);
    }

    private async Task ExecuteUpdateProcedureAsync(MaintenanceValue value, int currentUserId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "[fin].[DEV_UPD_MaintenanceValue]";
        AddParameter(command, "@TariffParameterID", DbType.Int32, value.TariffParameterID);
        AddParameter(command, "@class_code", DbType.Int16, value.class_code);
        AddParameter(command, "@class_number", DbType.String, value.class_number);
        AddParameter(command, "@months_age", DbType.Int16, value.months_age);
        AddParameter(command, "@kilometer_age", DbType.Int32, value.kilometer_age);
        AddParameter(command, "@RandPerKilometer", DbType.Decimal, value.RandPerKilometer);
        AddParameter(
            command,
            "@user_access_code",
            DbType.Int16,
            (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue)
        );
        await command.ExecuteNonQueryAsync();
    }

    private async Task<MaintenanceValue> MergeDirectAsync(MaintenanceValue value, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "TariffParameterID", "@tariffParameterId", DbType.Int32, value.TariffParameterID);
        AddValue(values, columns, "class_code", "@classCode", DbType.Int16, value.class_code);
        AddValue(values, columns, "class_number", "@classNumber", DbType.String, value.class_number);
        AddValue(values, columns, "months_age", "@monthsAge", DbType.Int16, value.months_age);
        AddValue(values, columns, "kilometer_age", "@kilometerAge", DbType.Int32, value.kilometer_age);
        AddValue(values, columns, "amount", "@amount", DbType.Decimal, value.amount);
        AddValue(values, columns, "RandPerKilometer", "@randPerKm", DbType.Decimal, value.RandPerKilometer);
        AddValue(values, columns, "CaptureDate", "@captureDate", DbType.DateTime2, DateTime.Now);
        AddValue(
            values,
            columns,
            "user_access_code",
            "@userCode",
            DbType.Int16,
            (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue)
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for writing fin.MaintenanceValue."
            );
        }

        var keyPredicate = columns.Contains("TariffParameterID")
            ? "[TariffParameterID] = @tariffParameterId AND [class_number] = @classNumber AND [months_age] = @monthsAge AND [kilometer_age] = @kilometerAge"
            : "[class_number] = @classNumber AND [months_age] = @monthsAge AND [kilometer_age] = @kilometerAge";

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [{SchemaName}].[{TableName}]
            SET {string.Join(", ", values.Where(item => item.Column is not "TariffParameterID" and not "class_number" and not "months_age" and not "kilometer_age").Select(item => $"[{item.Column}] = {item.Parameter}"))}
            WHERE {keyPredicate};
            IF @@ROWCOUNT = 0
            INSERT INTO [{SchemaName}].[{TableName}] ({string.Join(", ", values.Select(item => $"[{item.Column}]"))})
            VALUES ({string.Join(", ", values.Select(item => item.Parameter))});
            """;
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();

        var rows = await GetByClassAsync(value.TariffParameterID, value.class_code);
        return rows.FirstOrDefault(row =>
                string.Equals(row.class_number, value.class_number, StringComparison.OrdinalIgnoreCase)
                && row.months_age == value.months_age
                && row.kilometer_age == value.kilometer_age
            )
            ?? throw new InvalidOperationException("Written maintenance value could not be read.");
    }

    private async Task<bool> ProcedureExistsAsync(string procedureName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT COUNT(1)
            FROM [sys].[procedures] AS [p]
            INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [p].[schema_id]
            WHERE [s].[name] = @schema AND [p].[name] = @name
            """;
        AddParameter(command, "@schema", DbType.String, SchemaName);
        AddParameter(command, "@name", DbType.String, procedureName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private async Task<List<MaintenanceValue>> QueryAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns,
        string orderBy,
        bool firstOnly = false
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            $"SELECT {(firstOnly ? "TOP (1) " : string.Empty)}{BuildProjection(columns)} FROM [{SchemaName}].[{TableName}] WHERE {predicate} ORDER BY {orderBy}";
        configure?.Invoke(command);
        var result = new List<MaintenanceValue>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapMaintenanceValue(reader));
        return result;
    }

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
            "TariffParameterID" or "kilometer_age" or "created_by_user_code" or "modified_by_user_code" =>
                "int",
            "class_code" or "months_age" or "user_access_code" => "smallint",
            "is_deleted" => "bit",
            "class_number" or "user_access_name" => "varchar(255)",
            "CaptureDate" or "ModifiedDate" or "date_created" or "date_updated" => "datetime2",
            _ => "decimal(18, 4)",
        };

    private static MaintenanceValue MapMaintenanceValue(DbDataReader reader) =>
        new()
        {
            TariffParameterID = reader["TariffParameterID"] is DBNull
                ? 0
                : Convert.ToInt32(reader["TariffParameterID"]),
            class_code = reader["class_code"] is DBNull
                ? (short)0
                : Convert.ToInt16(reader["class_code"]),
            class_number = reader["class_number"] is DBNull
                ? string.Empty
                : reader["class_number"].ToString() ?? string.Empty,
            months_age = reader["months_age"] is DBNull
                ? (short)0
                : Convert.ToInt16(reader["months_age"]),
            kilometer_age = reader["kilometer_age"] is DBNull
                ? 0
                : Convert.ToInt32(reader["kilometer_age"]),
            amount = reader["amount"] is DBNull ? 0m : Convert.ToDecimal(reader["amount"]),
            RandPerKilometer = reader["RandPerKilometer"] is DBNull
                ? 0m
                : Convert.ToDecimal(reader["RandPerKilometer"]),
            CaptureDate = reader["CaptureDate"] is DBNull
                ? default
                : Convert.ToDateTime(reader["CaptureDate"]),
            ModifiedDate = reader["ModifiedDate"] is DBNull
                ? null
                : Convert.ToDateTime(reader["ModifiedDate"]),
            user_access_code = reader["user_access_code"] is DBNull
                ? null
                : Convert.ToInt16(reader["user_access_code"]),
            user_access_name = reader["user_access_name"] is DBNull
                ? null
                : reader["user_access_name"]?.ToString(),
            date_created = reader["date_created"] is DBNull
                ? default
                : Convert.ToDateTime(reader["date_created"]),
            date_updated = reader["date_updated"] is DBNull
                ? null
                : Convert.ToDateTime(reader["date_updated"]),
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
            columns.Add(reader.GetString(0));

        if (columns.Count == 0)
        {
            throw new InvalidOperationException(
                "The legacy fin.MaintenanceValue table is unavailable. No modern maintenance-value fallback was run."
            );
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The legacy fin.MaintenanceValue table is missing required columns: {string.Join(", ", missing)}"
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

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
/// Reads fin.TariffParameter without requiring expanded audit or approval
/// columns. Updates go through fin.DEV_UPD_TariffParameter when that
/// procedure exists; otherwise only columns confirmed on the live table are
/// written. EffectiveInterestRate is computed and is never written.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Schema, table, and column identifiers are fixed compatibility allowlists; values are parameterized."
)]
public class TariffParameterRepository : ITariffParameterRepository
{
    private const string SchemaName = "fin";
    private const string TableName = "TariffParameter";
    private static readonly string[] RequiredColumns =
    [
        "TariffParameterID",
        "TariffParameterYear",
        "AnnualInterestRatePercentage",
        "AnnualPayments",
        "PoolVehicleChargedDaysPerMonth",
        "CostCategoryMultiple",
        "AnnualRecoveredKilos",
    ];
    private static readonly string[] OptionalColumns =
    [
        "EffectiveInterestRate",
        "AverageFuelPrice",
        "EffectiveDate",
        "CaptureDate",
        "ModifiedDate",
        "user_access_code",
        "user_access_name",
        "Approved",
        "ApprovalDate",
        "Approval_user_access_code",
        "Approval_user_access_name",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public TariffParameterRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TariffParameter?> GetByYearAsync(int year)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            $"[TariffParameterYear] = @year AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@year", DbType.Int32, year),
            columns
        );
    }

    public async Task<TariffParameter?> GetCurrentAsync()
    {
        var current = await GetByYearAsync(DateTime.Now.Year);
        if (current is not null && current.Approved)
            return current;

        var approved = await GetApprovedAsync();
        return approved.FirstOrDefault();
    }

    public async Task<TariffParameter?> GetByIdAsync(int tariffParameterId)
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryOneAsync(
            $"[TariffParameterID] = @id AND {GetNotDeletedPredicate(columns)}",
            command => AddParameter(command, "@id", DbType.Int32, tariffParameterId),
            columns
        );
    }

    public async Task<List<TariffParameter>> GetAllAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        return await QueryAsync(
            GetNotDeletedPredicate(columns),
            configure: null,
            columns,
            "[TariffParameterYear] DESC, [TariffParameterID] DESC"
        );
    }

    public async Task<List<TariffParameter>> GetApprovedAsync()
    {
        var columns = await GetAvailableColumnsAsync();
        var predicate = columns.Contains("Approved")
            ? $"[Approved] = 1 AND {GetNotDeletedPredicate(columns)}"
            : GetNotDeletedPredicate(columns);
        return await QueryAsync(
            predicate,
            configure: null,
            columns,
            "[TariffParameterYear] DESC"
        );
    }

    public async Task<TariffParameter> CreateAsync(
        TariffParameter tariffParameter,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(tariffParameter);
        tariffParameter.Approved = false;
        tariffParameter.CaptureDate = DateTime.Now;
        tariffParameter.user_access_code = (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue);
        return await UpsertAsync(tariffParameter, currentUserId, isCreate: true);
    }

    public async Task<TariffParameter> UpdateAsync(
        TariffParameter tariffParameter,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(tariffParameter);
        _ =
            await GetByIdAsync(tariffParameter.TariffParameterID)
            ?? throw new InvalidOperationException(
                $"TariffParameter with TariffParameterID {tariffParameter.TariffParameterID} not found"
            );
        tariffParameter.ModifiedDate = DateTime.Now;
        return await UpsertAsync(tariffParameter, currentUserId, isCreate: false);
    }

    public async Task DeleteAsync(int tariffParameterId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (columns.Contains("is_deleted"))
        {
            command.CommandText = $"""
                UPDATE [{SchemaName}].[{TableName}]
                SET [is_deleted] = 1
                WHERE [TariffParameterID] = @id
                """;
        }
        else
        {
            command.CommandText = $"""
                DELETE FROM [{SchemaName}].[{TableName}]
                WHERE [TariffParameterID] = @id
                """;
        }

        AddParameter(command, "@id", DbType.Int32, tariffParameterId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExistsForYearAsync(int year) =>
        await GetByYearAsync(year) is not null;

    private async Task<TariffParameter> UpsertAsync(
        TariffParameter tariffParameter,
        int currentUserId,
        bool isCreate
    )
    {
        if (await ProcedureExistsAsync("DEV_UPD_TariffParameter"))
        {
            await ExecuteUpdateProcedureAsync(tariffParameter, currentUserId);
            return await GetByYearAsync(tariffParameter.TariffParameterYear)
                ?? throw new InvalidOperationException(
                    "fin.DEV_UPD_TariffParameter completed but the tariff parameter could not be read."
                );
        }

        if (isCreate)
            return await InsertDirectAsync(tariffParameter, currentUserId);

        await UpdateDirectAsync(tariffParameter, currentUserId);
        return await GetByIdAsync(tariffParameter.TariffParameterID)
            ?? throw new InvalidOperationException("Updated tariff parameter could not be read.");
    }

    private async Task ExecuteUpdateProcedureAsync(TariffParameter tariff, int currentUserId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "[fin].[DEV_UPD_TariffParameter]";
        AddParameter(command, "@TariffParameterID", DbType.Int32, tariff.TariffParameterID);
        AddParameter(command, "@TariffParameterYear", DbType.Int32, tariff.TariffParameterYear);
        AddParameter(
            command,
            "@AnnualInterestRatePercentage",
            DbType.Decimal,
            tariff.AnnualInterestRatePercentage
        );
        AddParameter(command, "@AnnualPayments", DbType.Byte, tariff.AnnualPayments);
        AddParameter(
            command,
            "@PoolVehicleChargedDaysPerMonth",
            DbType.Byte,
            tariff.PoolVehicleChargedDaysPerMonth
        );
        AddParameter(command, "@CostCategoryMultiple", DbType.Int32, tariff.CostCategoryMultiple);
        AddParameter(
            command,
            "@AnnualRecoveredKilos",
            DbType.Int32,
            tariff.AnnualRecoveredKilos ?? 0
        );
        AddParameter(command, "@AverageFuelPrice", DbType.Decimal, tariff.AverageFuelPrice);
        AddParameter(command, "@EffectiveDate", DbType.Date, tariff.EffectiveDate);
        AddParameter(command, "@user_access_code", DbType.Int16, (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue));
        AddParameter(command, "@Approved", DbType.Boolean, tariff.Approved);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<TariffParameter> InsertDirectAsync(TariffParameter tariff, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "TariffParameterYear", "@year", DbType.Int32, tariff.TariffParameterYear);
        AddValue(values, columns, "AnnualInterestRatePercentage", "@interest", DbType.Decimal, tariff.AnnualInterestRatePercentage);
        AddValue(values, columns, "AnnualPayments", "@payments", DbType.Byte, tariff.AnnualPayments);
        AddValue(values, columns, "PoolVehicleChargedDaysPerMonth", "@poolDays", DbType.Byte, tariff.PoolVehicleChargedDaysPerMonth);
        AddValue(values, columns, "CostCategoryMultiple", "@costMultiple", DbType.Int32, tariff.CostCategoryMultiple);
        AddValue(values, columns, "AnnualRecoveredKilos", "@kilos", DbType.Int32, tariff.AnnualRecoveredKilos ?? 0);
        AddValue(values, columns, "AverageFuelPrice", "@fuelPrice", DbType.Decimal, tariff.AverageFuelPrice);
        AddValue(values, columns, "EffectiveDate", "@effectiveDate", DbType.DateTime2, tariff.EffectiveDate);
        AddValue(values, columns, "CaptureDate", "@captureDate", DbType.DateTime2, DateTime.Now);
        AddValue(values, columns, "user_access_code", "@userCode", DbType.Int16, (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue));
        AddValue(values, columns, "Approved", "@approved", DbType.Boolean, false);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for inserting into fin.TariffParameter."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [{SchemaName}].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[TariffParameterID]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        tariff.TariffParameterID = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetByIdAsync(tariff.TariffParameterID)
            ?? throw new InvalidOperationException("Created tariff parameter could not be read.");
    }

    private async Task UpdateDirectAsync(TariffParameter tariff, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var assignments = new List<string>();
        var values = new List<WriteValue>();
        AddAssignment(assignments, values, columns, "AnnualInterestRatePercentage", "@interest", DbType.Decimal, tariff.AnnualInterestRatePercentage);
        AddAssignment(assignments, values, columns, "AnnualPayments", "@payments", DbType.Byte, tariff.AnnualPayments);
        AddAssignment(assignments, values, columns, "PoolVehicleChargedDaysPerMonth", "@poolDays", DbType.Byte, tariff.PoolVehicleChargedDaysPerMonth);
        AddAssignment(assignments, values, columns, "CostCategoryMultiple", "@costMultiple", DbType.Int32, tariff.CostCategoryMultiple);
        AddAssignment(assignments, values, columns, "AnnualRecoveredKilos", "@kilos", DbType.Int32, tariff.AnnualRecoveredKilos ?? 0);
        AddAssignment(assignments, values, columns, "AverageFuelPrice", "@fuelPrice", DbType.Decimal, tariff.AverageFuelPrice);
        AddAssignment(assignments, values, columns, "EffectiveDate", "@effectiveDate", DbType.DateTime2, tariff.EffectiveDate);
        AddAssignment(assignments, values, columns, "ModifiedDate", "@modifiedDate", DbType.DateTime2, DateTime.Now);
        AddAssignment(assignments, values, columns, "user_access_code", "@userCode", DbType.Int16, (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue));
        AddAssignment(assignments, values, columns, "Approved", "@approved", DbType.Boolean, tariff.Approved);
        AddAssignment(assignments, values, columns, "ApprovalDate", "@approvalDate", DbType.DateTime2, tariff.ApprovalDate);
        AddAssignment(assignments, values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.Now);

        if (assignments.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for updating fin.TariffParameter."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [{SchemaName}].[{TableName}]
            SET {string.Join(", ", assignments)}
            WHERE [TariffParameterID] = @id
            """;
        AddParameters(command, values);
        AddParameter(command, "@id", DbType.Int32, tariff.TariffParameterID);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException(
                $"TariffParameter with TariffParameterID {tariff.TariffParameterID} not found"
            );
        }
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

    private async Task<List<TariffParameter>> QueryAsync(
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
        var result = new List<TariffParameter>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapTariffParameter(reader));
        return result;
    }

    private async Task<TariffParameter?> QueryOneAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns
    ) =>
        (
            await QueryAsync(
                predicate,
                configure,
                columns,
                "[TariffParameterYear] DESC, [TariffParameterID] DESC",
                firstOnly: true
            )
        ).SingleOrDefault();

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
            "TariffParameterID" or "TariffParameterYear" or "CostCategoryMultiple"
                or "AnnualRecoveredKilos" or "created_by_user_code" or "modified_by_user_code"
                or "Approval_user_access_code" => "int",
            "AnnualPayments" or "PoolVehicleChargedDaysPerMonth" or "user_access_code" => "tinyint",
            "Approved" or "is_deleted" => "bit",
            "user_access_name" or "Approval_user_access_name" => "varchar(255)",
            "CaptureDate" or "ModifiedDate" or "EffectiveDate" or "ApprovalDate"
                or "date_created" or "date_updated" => "datetime2",
            _ => "decimal(18, 4)",
        };

    private static TariffParameter MapTariffParameter(DbDataReader reader) =>
        new()
        {
            TariffParameterID = Convert.ToInt32(reader["TariffParameterID"]),
            TariffParameterYear = Convert.ToInt32(reader["TariffParameterYear"]),
            AnnualInterestRatePercentage = reader["AnnualInterestRatePercentage"] is DBNull
                ? 0m
                : Convert.ToDecimal(reader["AnnualInterestRatePercentage"]),
            AnnualPayments = reader["AnnualPayments"] is DBNull
                ? (byte)0
                : Convert.ToByte(reader["AnnualPayments"]),
            EffectiveInterestRate = reader["EffectiveInterestRate"] is DBNull
                ? null
                : Convert.ToDecimal(reader["EffectiveInterestRate"]),
            PoolVehicleChargedDaysPerMonth = reader["PoolVehicleChargedDaysPerMonth"] is DBNull
                ? (byte)0
                : Convert.ToByte(reader["PoolVehicleChargedDaysPerMonth"]),
            CostCategoryMultiple = reader["CostCategoryMultiple"] is DBNull
                ? 0
                : Convert.ToInt32(reader["CostCategoryMultiple"]),
            AnnualRecoveredKilos = reader["AnnualRecoveredKilos"] is DBNull
                ? null
                : Convert.ToInt32(reader["AnnualRecoveredKilos"]),
            AverageFuelPrice = reader["AverageFuelPrice"] is DBNull
                ? null
                : Convert.ToDecimal(reader["AverageFuelPrice"]),
            EffectiveDate = reader["EffectiveDate"] is DBNull
                ? null
                : Convert.ToDateTime(reader["EffectiveDate"]),
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
            Approved = reader["Approved"] is not DBNull && Convert.ToBoolean(reader["Approved"]),
            ApprovalDate = reader["ApprovalDate"] is DBNull
                ? null
                : Convert.ToDateTime(reader["ApprovalDate"]),
            Approval_user_access_code = reader["Approval_user_access_code"] is DBNull
                ? null
                : Convert.ToInt16(reader["Approval_user_access_code"]),
            Approval_user_access_name = reader["Approval_user_access_name"] is DBNull
                ? null
                : reader["Approval_user_access_name"]?.ToString(),
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
                "The legacy fin.TariffParameter table is unavailable. No modern tariff-parameter fallback was run."
            );
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The legacy fin.TariffParameter table is missing required columns: {string.Join(", ", missing)}"
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

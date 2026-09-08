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
/// Keeps FML tariff operations compatible with the original LeaseTariff table
/// and the expanded audit schema. Optional modern columns are negotiated at
/// runtime so the legacy client database remains usable.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are selected only from fixed allowlists after schema inspection; all values are parameterized."
)]
public sealed class LeaseTariffRepository : ILeaseTariffRepository
{
    private const string TableName = "LeaseTariff";

    private static readonly string[] RequiredColumns =
    [
        "lease_tariff_code",
        "vmf_code",
        "start_date",
        "end_date",
        "fixed_tariff",
        "active",
    ];

    public LeaseTariffRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private readonly FisDbContext _context;

    public async Task<LeaseTariff?> GetByVehicleAndDateAsync(int vmfCode, DateTime effectiveDate) =>
        (
            await QueryAsync(
                "[l].[vmf_code] = @vmfCode AND [l].[active] = 1 AND [l].[start_date] <= @effectiveDate AND [l].[end_date] >= @effectiveDate",
                command =>
                {
                    AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                    AddParameter(command, "@effectiveDate", DbType.DateTime2, effectiveDate);
                }
            )
        ).FirstOrDefault();

    public async Task<List<LeaseTariff>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "[l].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<LeaseTariff?> GetActiveByVehicleAsync(int vmfCode) =>
        (
            await QueryAsync(
                "[l].[vmf_code] = @vmfCode AND [l].[active] = 1 AND [l].[start_date] <= @today AND [l].[end_date] >= @today",
                command =>
                {
                    AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                    AddParameter(command, "@today", DbType.Date, DateTime.Today);
                }
            )
        ).FirstOrDefault();

    public async Task<LeaseTariff?> GetByIdAsync(int leaseTariffCode) =>
        (
            await QueryAsync(
                "[l].[lease_tariff_code] = @leaseTariffCode",
                command => AddParameter(command, "@leaseTariffCode", DbType.Int32, leaseTariffCode)
            )
        ).SingleOrDefault();

    public async Task<List<LeaseTariff>> GetAllAsync() => await QueryAsync();

    public async Task<List<LeaseTariff>> GetAllActiveAsync() =>
        await QueryAsync("[l].[active] = 1");

    public async Task<LeaseTariff> CreateAsync(LeaseTariff leaseTariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(leaseTariff);
        Validate(leaseTariff);

        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildWriteValues(leaseTariff, columns).ToList();
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        leaseTariff.lease_tariff_code = await ExecuteInsertAsync(values);
        leaseTariff.date_created = now;
        leaseTariff.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        leaseTariff.is_deleted = false;
        return await GetByIdAsync(leaseTariff.lease_tariff_code) ?? leaseTariff;
    }

    public async Task<LeaseTariff> UpdateAsync(LeaseTariff leaseTariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(leaseTariff);
        Validate(leaseTariff);

        _ =
            await GetByIdAsync(leaseTariff.lease_tariff_code)
            ?? throw new InvalidOperationException(
                $"LeaseTariff with lease_tariff_code {leaseTariff.lease_tariff_code} not found"
            );

        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildWriteValues(leaseTariff, columns).ToList();
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        await ExecuteUpdateAsync(leaseTariff.lease_tariff_code, values, columns);
        return await GetByIdAsync(leaseTariff.lease_tariff_code) ?? leaseTariff;
    }

    public async Task DeleteAsync(int leaseTariffCode, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            if (columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                AddAssignment(
                    assignments,
                    command,
                    columns,
                    "date_updated",
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow
                );
                AddAssignment(
                    assignments,
                    command,
                    columns,
                    "modified_by_user_code",
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId > 0 ? currentUserId : null
                );
                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [lease_tariff_code] = @leaseTariffCode
                    AND ISNULL([is_deleted], 0) = 0
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [lease_tariff_code] = @leaseTariffCode
                    """;
            }

            AddParameter(command, "@leaseTariffCode", DbType.Int32, leaseTariffCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task DeactivateAsync(int leaseTariffCode)
    {
        var tariff = await GetByIdAsync(leaseTariffCode);
        if (tariff is null)
            return;
        tariff.active = false;
        await UpdateAsync(tariff, 0);
    }

    public async Task<LeaseTariffImportResult> ImportAsync(
        IReadOnlyList<LeaseTariffImportRow> rows,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(rows);
        var validRows = rows.Where(row =>
                row.VmfCode > 0
                && row.EndDate >= row.StartDate
                && row.FixedTariff >= 0
                && (!row.ExcessKiloTariff.HasValue || row.ExcessKiloTariff >= 0)
            )
            .ToList();
        var failed = rows.Count - validRows.Count;
        if (validRows.Count == 0)
        {
            return new LeaseTariffImportResult(0, failed);
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
            try
            {
                var fileColumns = await GetColumnsAsync(
                    connection,
                    "LeaseTariff_File",
                    transaction
                );
                var canUseLegacyImport =
                    HasColumns(
                        fileColumns,
                        "No",
                        "VMF_Code",
                        "GGNumber",
                        "GPNumber",
                        "Start_Date",
                        "End_Date",
                        "Fixed_Tariff",
                        "Excess_Kilo_Tariff"
                    )
                    && await ObjectExistsAsync(
                        connection,
                        "dbo.ADM_IMPORT_LeaseTariffFile",
                        transaction
                    );

                if (canUseLegacyImport)
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        transaction,
                        "DELETE FROM [dbo].[LeaseTariff_File]"
                    );
                    foreach (var (row, index) in validRows.Select((row, index) => (row, index)))
                    {
                        await InsertImportFileRowAsync(
                            connection,
                            transaction,
                            fileColumns,
                            row,
                            index + 1,
                            currentUserId
                        );
                    }

                    await ExecuteStoredProcedureAsync(
                        connection,
                        transaction,
                        "ADM_IMPORT_LeaseTariffFile"
                    );
                    if (
                        await ObjectExistsAsync(
                            connection,
                            "dbo.ADM_UPD_LeaseFixedTariff",
                            transaction
                        )
                    )
                    {
                        await ExecuteStoredProcedureAsync(
                            connection,
                            transaction,
                            "ADM_UPD_LeaseFixedTariff"
                        );
                    }
                }
                else
                {
                    var tariffColumns = await GetAvailableColumnsAsync(connection, transaction);
                    foreach (var row in validRows)
                    {
                        await InsertTariffRowAsync(
                            connection,
                            transaction,
                            tariffColumns,
                            row,
                            currentUserId
                        );
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new LeaseTariffImportResult(validRows.Count, failed);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<LeaseTariff>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var projection = new[]
            {
                "[l].[lease_tariff_code] AS [lease_tariff_code]",
                "[l].[vmf_code] AS [vmf_code]",
                "[l].[start_date] AS [start_date]",
                "[l].[end_date] AS [end_date]",
                "[l].[fixed_tariff] AS [fixed_tariff]",
                "[l].[active] AS [active]",
                GetProjection(columns, "excess_kilo_tariff", "excess_kilo_tariff", "money"),
                GetOptionalProjection(columns, "date_created", "datetime2", "NULL"),
                GetOptionalProjection(columns, "date_updated", "datetime2", "NULL"),
                GetOptionalProjection(columns, "created_by_user_code", "int", "NULL"),
                GetOptionalProjection(columns, "modified_by_user_code", "int", "NULL"),
                GetOptionalProjection(columns, "is_deleted", "bit", "0"),
            };
            var where = string.IsNullOrWhiteSpace(predicate)
                ? GetActiveFilter(columns)
                : $"{predicate} AND {GetActiveFilter(columns)}";
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [l]
                WHERE {where}
                ORDER BY [l].[start_date] DESC, [l].[lease_tariff_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<LeaseTariff>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(Map(reader));
            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required LeaseTariff compatibility columns are not available: {string.Join(", ", missing)}"
                );
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        DbConnection connection,
        DbTransaction transaction
    )
    {
        var columns = await GetColumnsAsync(connection, TableName, transaction);
        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required LeaseTariff compatibility columns are not available: {string.Join(", ", missing)}"
            );
        }

        return columns;
    }

    private static bool HasColumns(IReadOnlySet<string> columns, params string[] required) =>
        required.All(columns.Contains);

    private static async Task<bool> ObjectExistsAsync(
        DbConnection connection,
        string objectName,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@objectName) IS NULL THEN 0 ELSE 1 END";
        AddParameter(command, "@objectName", DbType.String, objectName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteStoredProcedureAsync(
        DbConnection connection,
        DbTransaction transaction,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertImportFileRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        LeaseTariffImportRow row,
        int rowNumber,
        int currentUserId
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, columns, "No", "@no", DbType.Int16, rowNumber);
        AddValue(values, columns, "VMF_Code", "@vmfCode", DbType.Int32, row.VmfCode);
        AddValue(values, columns, "GGNumber", "@ggNumber", DbType.String, row.GgNumber);
        AddValue(values, columns, "GPNumber", "@gpNumber", DbType.String, row.GpNumber);
        AddValue(values, columns, "Start_Date", "@startDate", DbType.DateTime2, row.StartDate);
        AddValue(values, columns, "End_Date", "@endDate", DbType.DateTime2, row.EndDate);
        AddValue(values, columns, "Fixed_Tariff", "@fixedTariff", DbType.Decimal, row.FixedTariff);
        AddValue(
            values,
            columns,
            "Excess_Kilo_Tariff",
            "@excessKiloTariff",
            DbType.Decimal,
            row.ExcessKiloTariff
        );
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        await InsertRowAsync(connection, transaction, "LeaseTariff_File", values);
    }

    private static async Task InsertTariffRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> columns,
        LeaseTariffImportRow row,
        int currentUserId
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, row.VmfCode);
        AddValue(values, columns, "start_date", "@startDate", DbType.DateTime2, row.StartDate);
        AddValue(values, columns, "end_date", "@endDate", DbType.DateTime2, row.EndDate);
        AddValue(values, columns, "fixed_tariff", "@fixedTariff", DbType.Decimal, row.FixedTariff);
        AddValue(
            values,
            columns,
            "excess_kilo_tariff",
            "@excessKiloTariff",
            DbType.Decimal,
            row.ExcessKiloTariff
        );
        AddValue(values, columns, "active", "@active", DbType.Boolean, true);
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        await InsertRowAsync(connection, transaction, TableName, values);
    }

    private static async Task InsertRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO [dbo].[{tableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private static List<WriteValue> BuildWriteValues(
        LeaseTariff tariff,
        IReadOnlySet<string> columns
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, tariff.vmf_code);
        AddValue(values, columns, "start_date", "@startDate", DbType.DateTime2, tariff.start_date);
        AddValue(values, columns, "end_date", "@endDate", DbType.DateTime2, tariff.end_date);
        AddValue(
            values,
            columns,
            "fixed_tariff",
            "@fixedTariff",
            DbType.Decimal,
            tariff.fixed_tariff
        );
        AddValue(values, columns, "active", "@active", DbType.Boolean, tariff.active);
        AddValue(
            values,
            columns,
            "excess_kilo_tariff",
            "@excessKiloTariff",
            DbType.Decimal,
            tariff.excess_kilo_tariff
        );
        return values;
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[lease_tariff_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteUpdateAsync(
        int code,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> columns
    )
    {
        if (values.Count == 0)
            return;
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
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
                WHERE [lease_tariff_code] = @leaseTariffCode
                AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@leaseTariffCode", DbType.Int32, code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static LeaseTariff Map(DbDataReader reader) =>
        new()
        {
            lease_tariff_code = ReadInt32(reader, "lease_tariff_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            start_date = ReadDateTime(reader, "start_date") ?? DateTime.MinValue,
            end_date = ReadDateTime(reader, "end_date") ?? DateTime.MinValue,
            fixed_tariff = ReadDecimal(reader, "fixed_tariff") ?? 0,
            active = ReadBoolean(reader, "active") ?? false,
            excess_kilo_tariff = ReadDecimal(reader, "excess_kilo_tariff"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static void Validate(LeaseTariff tariff)
    {
        if (tariff.vmf_code <= 0)
            throw new ArgumentException("A valid vehicle code is required.", nameof(tariff));
        if (tariff.end_date < tariff.start_date)
            throw new ArgumentException(
                "The tariff end date cannot be earlier than its start date.",
                nameof(tariff)
            );
    }

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([l].[is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlySet<string> columns,
        string alias,
        string column,
        string sqlType
    ) =>
        columns.Contains(column)
            ? $"[l].[{column}] AS [{alias}]"
            : $"CAST(NULL AS {sqlType}) AS [{alias}]";

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string sqlType,
        string fallback
    ) =>
        columns.Contains(column)
            ? $"[l].[{column}] AS [{column}]"
            : $"CAST({fallback} AS {sqlType}) AS [{column}]";

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.Contains(column))
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddAssignment(
        ICollection<string> assignments,
        DbCommand command,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.Contains(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

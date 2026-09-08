using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads the expanded <c>license</c> table first and falls back to the
/// original <c>licence_fees</c> table when the expanded table is not present
/// or has no rows. Optional audit/category columns are negotiated at runtime
/// so a client-era database remains usable.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come only from fixed compatibility sources; values are parameters."
)]
public sealed class LicenseRepository : ILicenseRepository
{
    private const string ModernTableName = "license";
    private const string LegacyTableName = "licence_fees";

    private readonly FisDbContext _context;

    public LicenseRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<License?> GetByIdAsync(short licenceCode)
    {
        var modern = await GetSourceAsync(ModernTableName);
        var result = modern is null
            ? null
            : (
                await QueryAsync(
                    modern,
                    $"[{modern.CodeColumn}] = @licenceCode",
                    command => AddParameter(command, "@licenceCode", DbType.Int16, licenceCode)
                )
            ).SingleOrDefault();

        if (result is not null)
        {
            return result;
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        return legacy is null
            ? null
            : (
                await QueryAsync(
                    legacy,
                    $"[{legacy.CodeColumn}] = @licenceCode",
                    command => AddParameter(command, "@licenceCode", DbType.Int16, licenceCode)
                )
            ).SingleOrDefault();
    }

    public async Task<License?> GetByDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var predicate = "LOWER(COALESCE([licence_description], '')) = @description";
        var configure = new Action<DbCommand>(command =>
            AddParameter(
                command,
                "@description",
                DbType.String,
                description.Trim().ToLowerInvariant()
            )
        );

        var modern = await GetSourceAsync(ModernTableName);
        var result = modern is null
            ? null
            : (await QueryAsync(modern, predicate, configure)).SingleOrDefault();
        if (result is not null)
        {
            return result;
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        return legacy is null
            ? null
            : (await QueryAsync(legacy, predicate, configure)).SingleOrDefault();
    }

    public async Task<IEnumerable<License>> GetAllLicensesAsync()
    {
        var modern = await GetSourceAsync(ModernTableName);
        var legacy = await GetSourceAsync(LegacyTableName);
        var modernRows = modern is null ? [] : await QueryAsync(modern);
        var legacyRows = legacy is null ? [] : await QueryAsync(legacy);
        return MergePreferModern(modernRows, legacyRows);
    }

    public async Task<IEnumerable<License>> SearchLicensesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllLicensesAsync();
        }

        var predicate = "LOWER(COALESCE([licence_description], '')) LIKE @searchTerm";
        var configure = new Action<DbCommand>(command =>
            AddParameter(
                command,
                "@searchTerm",
                DbType.String,
                $"%{searchTerm.Trim().ToLowerInvariant()}%"
            )
        );

        var modern = await GetSourceAsync(ModernTableName);
        var legacy = await GetSourceAsync(LegacyTableName);
        var modernRows = modern is null ? [] : await QueryAsync(modern, predicate, configure);
        var legacyRows = legacy is null ? [] : await QueryAsync(legacy, predicate, configure);
        return MergePreferModern(modernRows, legacyRows);
    }

    public async Task<License> CreateAsync(License license, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(license);
        var source =
            await GetSourceAsync(ModernTableName)
            ?? await GetSourceAsync(LegacyTableName)
            ?? throw new InvalidOperationException(
                "Neither the modern license table nor the legacy licence_fees table is available."
            );

        var now = DateTime.UtcNow;
        var values = new List<WriteValue>();
        AddValue(
            values,
            source,
            "licence_description",
            "@description",
            DbType.String,
            license.licence_description,
            true
        );
        AddValue(
            values,
            source,
            "licence_category",
            "@category",
            DbType.String,
            license.licence_category,
            true
        );
        // Keep the legacy fee table's numeric field valid when it is the only
        // available source. Existing fee values are never changed by updates.
        AddValue(values, source, "licence_fee", "@fee", DbType.Decimal, 0m, false);
        AddAuditValues(values, source, now, currentUserId, includeCreated: true);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{source.TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[{source.CodeColumn}] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var code = Convert.ToInt16(await command.ExecuteScalarAsync());
        return await GetByIdAsync(code)
            ?? throw new InvalidOperationException("Created license could not be read.");
    }

    public async Task<License> UpdateAsync(License license, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(license);
        var source =
            await FindSourceByIdAsync(license.licence_code)
            ?? throw new InvalidOperationException(
                $"License with licence_code {license.licence_code} not found"
            );

        var values = new List<WriteValue>();
        AddValue(
            values,
            source,
            "licence_description",
            "@description",
            DbType.String,
            license.licence_description,
            true
        );
        AddValue(
            values,
            source,
            "licence_category",
            "@category",
            DbType.String,
            license.licence_category,
            true
        );
        AddAuditValues(values, source, DateTime.UtcNow, currentUserId, includeCreated: false);
        await ExecuteUpdateAsync(source, license.licence_code, values);
        return await GetByIdAsync(license.licence_code)
            ?? throw new InvalidOperationException(
                $"Updated license with licence_code {license.licence_code} could not be read."
            );
    }

    public async Task<bool> DeleteAsync(short licenceCode, int currentUserId)
    {
        var source = await FindSourceByIdAsync(licenceCode);
        if (source is null)
        {
            return false;
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        if (source.Columns.Contains("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            if (source.Columns.Contains("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }

            if (source.Columns.Contains("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedBy");
                AddParameter(command, "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId));
            }

            command.CommandText =
                $"UPDATE [dbo].[{source.TableName}] SET {string.Join(", ", assignments)} WHERE [{source.CodeColumn}] = @licenceCode";
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{source.TableName}] WHERE [{source.CodeColumn}] = @licenceCode";
        }

        AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task<LicenseSource?> FindSourceByIdAsync(short licenceCode)
    {
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null && await HasRowAsync(modern, licenceCode))
        {
            return modern;
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        return legacy is not null && await HasRowAsync(legacy, licenceCode) ? legacy : null;
    }

    private async Task<List<License>> QueryAsync(
        LicenseSource source,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var where = BuildWhere(source, predicate);
        command.CommandText =
            $"SELECT {BuildProjection(source)} FROM [dbo].[{source.TableName}] WHERE {where} ORDER BY [licence_description], [{source.CodeColumn}]";
        configure?.Invoke(command);

        var result = new List<License>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(Map(reader));
        }

        return result;
    }

    private async Task<bool> HasRowAsync(LicenseSource source, short licenceCode)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"SELECT COUNT(1) FROM [dbo].[{source.TableName}] WHERE [{source.CodeColumn}] = @licenceCode AND {ActiveFilter(source)}";
        AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private async Task ExecuteUpdateAsync(
        LicenseSource source,
        short licenceCode,
        IReadOnlyList<WriteValue> values
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"UPDATE [dbo].[{source.TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [{source.CodeColumn}] = @licenceCode";
        AddParameters(command, values);
        AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"License with licence_code {licenceCode} not found.");
        }
    }

    private async Task<LicenseSource?> GetSourceAsync(string tableName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
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

        var codeColumn = tableName.Equals(LegacyTableName, StringComparison.OrdinalIgnoreCase)
            ? "licence_fee_code"
            : "licence_code";
        return columns.Contains(codeColumn) && columns.Contains("licence_description")
            ? new LicenseSource(tableName, codeColumn, columns)
            : null;
    }

    private static string BuildProjection(LicenseSource source) =>
        string.Join(
            ", ",
            [
                $"[{source.CodeColumn}] AS [licence_code]",
                "[licence_description] AS [licence_description]",
                OptionalExpression(source, "licence_category", "nvarchar(20)")
                    + " AS [licence_category]",
                OptionalExpression(source, "date_created", "datetime2") + " AS [date_created]",
                OptionalExpression(source, "date_updated", "datetime2") + " AS [date_updated]",
                OptionalExpression(source, "created_by_user_code", "int")
                    + " AS [created_by_user_code]",
                OptionalExpression(source, "modified_by_user_code", "int")
                    + " AS [modified_by_user_code]",
                OptionalExpression(source, "is_deleted", "bit") + " AS [is_deleted]",
            ]
        );

    private static string BuildWhere(LicenseSource source, string? predicate)
    {
        var active = ActiveFilter(source);
        return string.IsNullOrWhiteSpace(predicate) ? active : $"{active} AND ({predicate})";
    }

    private static string ActiveFilter(LicenseSource source) =>
        source.Columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static IEnumerable<License> MergePreferModern(
        IEnumerable<License> modernRows,
        IEnumerable<License> legacyRows
    ) =>
        modernRows
            .Concat(
                legacyRows.Where(legacy =>
                    !modernRows.Any(modern => modern.licence_code == legacy.licence_code)
                )
            )
            .OrderBy(license => license.licence_description);

    private static string OptionalExpression(LicenseSource source, string column, string sqlType) =>
        source.Columns.Contains(column) ? $"[{column}]"
        : column.Equals("is_deleted", StringComparison.OrdinalIgnoreCase) ? "CAST(0 AS bit)"
        : $"CAST(NULL AS {sqlType})";

    private static License Map(DbDataReader reader) =>
        new()
        {
            licence_code = ReadInt16(reader, "licence_code") ?? 0,
            licence_description = ReadString(reader, "licence_description") ?? string.Empty,
            licence_category = ReadString(reader, "licence_category"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = ReadInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
        };

    private static void AddAuditValues(
        List<WriteValue> values,
        LicenseSource source,
        DateTime now,
        int currentUserId,
        bool includeCreated
    )
    {
        if (includeCreated)
        {
            AddValue(values, source, "date_created", "@dateCreated", DbType.DateTime2, now, false);
            AddValue(
                values,
                source,
                "created_by_user_code",
                "@createdBy",
                DbType.Int32,
                UserIdOrNull(currentUserId),
                false
            );
            AddValue(values, source, "is_deleted", "@isDeleted", DbType.Boolean, false, false);
        }

        AddValue(values, source, "date_updated", "@dateUpdated", DbType.DateTime2, now, false);
        AddValue(
            values,
            source,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            UserIdOrNull(currentUserId),
            false
        );
    }

    private static void AddValue(
        List<WriteValue> values,
        LicenseSource source,
        string column,
        string parameter,
        DbType type,
        object? value,
        bool includeNull
    )
    {
        if (source.Columns.Contains(column) && (includeNull || value is not null))
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

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        return new ConnectionScope(connection, shouldClose);
    }

    private DbTransaction? CurrentTransaction =>
        _context.Database.CurrentTransaction?.GetDbTransaction();

    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

    private static short? ReadInt16(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt16(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt32(reader[name]);

    private static string? ReadString(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name]);

    private static DateTime? ReadDate(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

    private static bool ReadBool(DbDataReader reader, string name) =>
        !reader.IsDBNull(reader.GetOrdinal(name)) && Convert.ToBoolean(reader[name]);

    private sealed record LicenseSource(
        string TableName,
        string CodeColumn,
        IReadOnlySet<string> Columns
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose)
        : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
            {
                await Connection.CloseAsync();
            }
        }
    }
}

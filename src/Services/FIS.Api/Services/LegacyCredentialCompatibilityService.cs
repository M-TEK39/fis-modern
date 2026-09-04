using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Reads and writes optional columns on Legacy_User_Credentials without making
/// them static EF mappings. The expanded credential table was deployed with
/// different column sets during the migration, while the client database still
/// uses user_access_old1 as the authoritative credential store.
/// </summary>
public sealed class LegacyCredentialCompatibilityService
{
    private readonly FisDbContext _context;
    private readonly ILogger<LegacyCredentialCompatibilityService> _logger;

    public LegacyCredentialCompatibilityService(
        FisDbContext context,
        ILogger<LegacyCredentialCompatibilityService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HydrateAsync(LegacyUserCredential credential)
    {
        var values = await ReadAsync(credential.credential_id);
        if (values is null)
        {
            return;
        }

        credential.password_expiry_date = values.PasswordExpiryDate;
        credential.changed_by_user_code = values.ChangedByUserCode;
    }

    public async Task<LegacyCredentialOptionalFields?> ReadAsync(int credentialId)
    {
        var values = await ReadManyAsync([credentialId]);
        return values.TryGetValue(credentialId, out var result) ? result : null;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Command text is selected from fixed statements containing only allowlisted schema identifiers.")]
    public async Task<IReadOnlyDictionary<int, LegacyCredentialOptionalFields>> ReadManyAsync(
        IEnumerable<int> credentialIds)
    {
        var ids = credentialIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, LegacyCredentialOptionalFields>();
        }

        try
        {
            var availableColumns = await GetAvailableColumnsAsync();
            if (availableColumns.Count == 0)
            {
                return new Dictionary<int, LegacyCredentialOptionalFields>();
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

                command.CommandText = GetReadCommandText(availableColumns);
                AddParameter(command, "@credentialId", DbType.Int32, 0);

                var result = new Dictionary<int, LegacyCredentialOptionalFields>();
                foreach (var id in ids)
                {
                    command.Parameters["@credentialId"].Value = id;
                    await using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        var credentialId = reader.GetInt32(reader.GetOrdinal("credential_id"));
                        var expiry = ReadDateTime(reader, availableColumns, "password_expiry_date");
                        var changedBy = ReadInt32(reader, availableColumns, "changed_by_user_code");
                        result[credentialId] = new LegacyCredentialOptionalFields(expiry, changedBy);
                    }
                }

                return result;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (SqlException ex)
        {
            // Optional expanded columns must never stop the legacy login path.
            _logger.LogWarning(
                ex,
                "Could not read optional Legacy_User_Credentials columns; continuing with legacy credential data");
            return new Dictionary<int, LegacyCredentialOptionalFields>();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Command text is selected from fixed statements containing only allowlisted schema identifiers.")]
    public async Task PersistAsync(LegacyUserCredential credential)
    {
        try
        {
            var availableColumns = await GetAvailableColumnsAsync();
            if (availableColumns.Count == 0)
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
                command.CommandText = GetWriteCommandText(availableColumns);

                AddParameter(command, "@credentialId", DbType.Int32, credential.credential_id);
                if (availableColumns.Contains("password_expiry_date"))
                {
                    AddParameter(
                        command,
                        "@passwordExpiryDate",
                        DbType.DateTime2,
                        credential.password_expiry_date ?? (object)DBNull.Value);
                }

                if (availableColumns.Contains("changed_by_user_code"))
                {
                    AddParameter(
                        command,
                        "@changedByUserCode",
                        DbType.Int32,
                        credential.changed_by_user_code ?? (object)DBNull.Value);
                }

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
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not persist optional Legacy_User_Credentials columns; legacy credential state remains authoritative");
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
                  AND [COLUMN_NAME] IN ('password_expiry_date', 'changed_by_user_code')
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, "Legacy_User_Credentials");

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

    private static string GetReadCommandText(IReadOnlySet<string> columns)
    {
        var hasExpiry = columns.Contains("password_expiry_date");
        var hasChangedBy = columns.Contains("changed_by_user_code");

        if (hasExpiry && hasChangedBy)
        {
            return """
                SELECT [credential_id], [password_expiry_date], [changed_by_user_code]
                FROM [dbo].[Legacy_User_Credentials]
                WHERE [credential_id] = @credentialId
                """;
        }

        if (hasExpiry)
        {
            return """
                SELECT [credential_id], [password_expiry_date]
                FROM [dbo].[Legacy_User_Credentials]
                WHERE [credential_id] = @credentialId
                """;
        }

        return """
            SELECT [credential_id], [changed_by_user_code]
            FROM [dbo].[Legacy_User_Credentials]
            WHERE [credential_id] = @credentialId
            """;
    }

    private static string GetWriteCommandText(IReadOnlySet<string> columns)
    {
        var hasExpiry = columns.Contains("password_expiry_date");
        var hasChangedBy = columns.Contains("changed_by_user_code");

        if (hasExpiry && hasChangedBy)
        {
            return """
                UPDATE [dbo].[Legacy_User_Credentials]
                SET [password_expiry_date] = @passwordExpiryDate,
                    [changed_by_user_code] = @changedByUserCode
                WHERE [credential_id] = @credentialId
                """;
        }

        if (hasExpiry)
        {
            return """
                UPDATE [dbo].[Legacy_User_Credentials]
                SET [password_expiry_date] = @passwordExpiryDate
                WHERE [credential_id] = @credentialId
                """;
        }

        return """
            UPDATE [dbo].[Legacy_User_Credentials]
            SET [changed_by_user_code] = @changedByUserCode
            WHERE [credential_id] = @credentialId
            """;
    }

    private static DateTime? ReadDateTime(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string columnName)
    {
        if (!columns.Contains(columnName))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static int? ReadInt32(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string columnName)
    {
        if (!columns.Contains(columnName))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed record LegacyCredentialOptionalFields(
    DateTime? PasswordExpiryDate,
    int? ChangedByUserCode);

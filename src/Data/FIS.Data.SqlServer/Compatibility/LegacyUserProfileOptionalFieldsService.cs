using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Domain.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Data.SqlServer.Compatibility;

/// <summary>
/// Reads and writes optional fields on user_access_old1 without making them
/// static EF mappings. The client database may not contain fields introduced
/// by the expanded user-profile model.
/// </summary>
public sealed class LegacyUserProfileOptionalFieldsService
{
    private const string TableSchema = "dbo";
    private const string TableName = "user_access_old1";
    private const string ApproverColumn = "approver_code_at_gfleet";

    private readonly FisDbContext _context;
    private readonly ILogger<LegacyUserProfileOptionalFieldsService> _logger;

    public LegacyUserProfileOptionalFieldsService(
        FisDbContext context,
        ILogger<LegacyUserProfileOptionalFieldsService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    public async Task HydrateAsync(
        UserAccessOld user,
        CancellationToken cancellationToken = default
    )
    {
        await HydrateManyAsync([user], cancellationToken);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The schema, table, and column identifiers are fixed constants; only generated parameter names are interpolated."
    )]
    public async Task HydrateManyAsync(
        IEnumerable<UserAccessOld> users,
        CancellationToken cancellationToken = default
    )
    {
        var userList = users.DistinctBy(user => user.user_access_code).ToArray();
        if (userList.Length == 0)
        {
            return;
        }

        try
        {
            var availableColumns = await GetAvailableColumnsAsync(cancellationToken);
            if (!availableColumns.Contains(ApproverColumn))
            {
                return;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

                var parameters = new string[userList.Length];
                for (var index = 0; index < userList.Length; index++)
                {
                    parameters[index] = $"@userAccessCode{index}";
                    AddParameter(
                        command,
                        parameters[index],
                        DbType.Int16,
                        userList[index].user_access_code
                    );
                }

                command.CommandText = $"""
                    SELECT [user_access_code], [{ApproverColumn}]
                    FROM [{TableSchema}].[{TableName}]
                    WHERE [user_access_code] IN ({string.Join(", ", parameters)})
                    """;

                var values = new Dictionary<short, int?>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var userAccessCode = Convert.ToInt16(reader.GetValue(0));
                    var approverCode = reader.IsDBNull(1)
                        ? (int?)null
                        : Convert.ToInt32(reader.GetValue(1));
                    values[userAccessCode] = approverCode;
                }

                foreach (var user in userList)
                {
                    if (values.TryGetValue(user.user_access_code, out var approverCode))
                    {
                        user.approver_code_at_gfleet = approverCode;
                    }
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (DbException ex)
        {
            // Optional profile fields must never make the legacy profile path
            // unavailable when a client database predates the column.
            _logger.LogWarning(
                ex,
                "Could not read optional {Column} from {Schema}.{Table}; continuing without the optional profile field",
                ApproverColumn,
                TableSchema,
                TableName
            );
        }
    }

    public async Task PersistAsync(
        UserAccessOld user,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var availableColumns = await GetAvailableColumnsAsync(cancellationToken);
            if (!availableColumns.Contains(ApproverColumn))
            {
                return;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $"""
                    UPDATE [{TableSchema}].[{TableName}]
                    SET [{ApproverColumn}] = @approverCode
                    WHERE [user_access_code] = @userAccessCode
                    """;

                AddParameter(
                    command,
                    "@approverCode",
                    DbType.Int32,
                    user.approver_code_at_gfleet ?? (object)DBNull.Value
                );
                AddParameter(command, "@userAccessCode", DbType.Int16, user.user_access_code);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (DbException ex)
        {
            _logger.LogWarning(
                ex,
                "Could not persist optional {Column} on {Schema}.{Table}; the legacy profile write remains authoritative",
                ApproverColumn,
                TableSchema,
                TableName
            );
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
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
                  AND [COLUMN_NAME] = @column
                """;
            AddParameter(command, "@schema", DbType.String, TableSchema);
            AddParameter(command, "@table", DbType.String, TableName);
            AddParameter(command, "@column", DbType.String, ApproverColumn);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The schema, table, and column identifiers are fixed constants; request values are parameters."
    )]
    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Capture-form lookup procedures decide membership and order. Leftover table
/// queries still hydrate the modern entity shape.
/// </summary>
internal static class LegacySelectorProcedure
{
    internal static async Task<IReadOnlyList<int>?> TryReadOrderedKeysAsync(
        FisDbContext context,
        string procedureName,
        string[] expectedParameters,
        Action<DbCommand>? bind,
        params string[] keyColumnNames
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            if (
                !await ProcedureMatchesAsync(
                    connection,
                    transaction,
                    procedureName,
                    expectedParameters
                )
            )
            {
                return null;
            }

            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                procedureName,
                bind
            );
            if (rows.Count == 0)
            {
                return [];
            }

            var keys = new List<int>();
            var seen = new HashSet<int>();
            foreach (var row in rows)
            {
                var key = ReadInt32(row, keyColumnNames);
                if (key is null or <= 0 || !seen.Add(key.Value))
                {
                    continue;
                }

                keys.Add(key.Value);
            }

            return keys.Count == 0 ? null : keys;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static async Task<IReadOnlyList<string>?> TryReadOrderedStringKeysAsync(
        FisDbContext context,
        string procedureName,
        string[] expectedParameters,
        Action<DbCommand>? bind,
        params string[] keyColumnNames
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            if (
                !await ProcedureMatchesAsync(
                    connection,
                    transaction,
                    procedureName,
                    expectedParameters
                )
            )
            {
                return null;
            }

            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                procedureName,
                bind
            );
            if (rows.Count == 0)
            {
                return [];
            }

            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var key = ReadString(row, keyColumnNames);
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
                {
                    continue;
                }

                keys.Add(key);
            }

            return keys.Count == 0 ? null : keys;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static async Task<IReadOnlyList<string>?> TryGetProcedureParametersAsync(
        FisDbContext context,
        string procedureName
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            return await GetProcedureParametersAsync(
                connection,
                context.Database.CurrentTransaction?.GetDbTransaction(),
                procedureName
            );
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static IReadOnlyList<T> OrderByKeys<T>(
        IReadOnlyList<T> leftover,
        IReadOnlyList<int> keys,
        Func<T, int> keySelector
    )
    {
        var byKey = leftover
            .GroupBy(keySelector)
            .ToDictionary(group => group.Key, group => group.First());
        var ordered = new List<T>(keys.Count);
        foreach (var key in keys)
        {
            if (byKey.TryGetValue(key, out var item))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    internal static IReadOnlyList<T> OrderByKeys<T>(
        IReadOnlyList<T> leftover,
        IReadOnlyList<string> keys,
        Func<T, string?> keySelector
    )
    {
        var byKey = leftover
            .Where(item => !string.IsNullOrWhiteSpace(keySelector(item)))
            .GroupBy(item => keySelector(item)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var ordered = new List<T>(keys.Count);
        foreach (var key in keys)
        {
            if (byKey.TryGetValue(key, out var item))
            {
                ordered.Add(item);
            }
        }

        return ordered;
    }

    internal static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> TryReadRowsAsync(
        FisDbContext context,
        string procedureName,
        IReadOnlyList<string[]> acceptedParameterSets,
        Func<IReadOnlyList<string>, Action<DbCommand>?> bindForActualParameters
    )
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            var actualParameters = await GetProcedureParametersAsync(
                connection,
                transaction,
                procedureName
            );
            if (actualParameters is null)
            {
                return null;
            }

            var matchesAcceptedSet = acceptedParameterSets.Any(expected =>
                actualParameters.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)
            );
            if (!matchesAcceptedSet)
            {
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match its verified parameter contract. No direct-DML fallback was run."
                );
            }

            var rows = await ExecuteProcedureRowsAsync(
                connection,
                transaction,
                procedureName,
                bindForActualParameters(actualParameters)
            );
            return rows.ConvertAll(row => (IReadOnlyDictionary<string, object?>)row);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static int? ReadInt32(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => ReadInt32(row, (IReadOnlyList<string>)keys);

    internal static string? ReadString(
        IReadOnlyDictionary<string, object?> row,
        params string[] keys
    ) => ReadString(row, (IReadOnlyList<string>)keys);

    private static async Task<bool> ProcedureMatchesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        string[] expectedParameters
    )
    {
        var actualParameters = await GetProcedureParametersAsync(
            connection,
            transaction,
            procedureName
        );
        if (actualParameters is null)
        {
            return false;
        }

        if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match its verified parameter contract. No direct-DML fallback was run."
            );
        }

        return true;
    }

    private static async Task<List<string>?> GetProcedureParametersAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [parameterObject].[name]
            FROM [sys].[procedures] AS [procedureObject]
            INNER JOIN [sys].[schemas] AS [schemaObject]
                ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
            LEFT JOIN [sys].[parameters] AS [parameterObject]
                ON [parameterObject].[object_id] = [procedureObject].[object_id]
               AND [parameterObject].[parameter_id] > 0
            WHERE [schemaObject].[name] = N'dbo'
              AND [procedureObject].[name] = @procedureName
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);

        var actualParameters = new List<string>();
        var procedureFound = false;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            procedureFound = true;
            if (!reader.IsDBNull(0))
            {
                actualParameters.Add(reader.GetString(0));
            }
        }

        return procedureFound ? actualParameters : null;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Procedure names come from fixed capture-lookup allowlists; values are parameters."
    )]
    private static async Task<List<Dictionary<string, object?>>> ExecuteProcedureRowsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedureName,
        Action<DbCommand>? bind
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"dbo.{procedureName}";
        bind?.Invoke(command);

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static int? ReadInt32(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<string> keys
    )
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value is null or DBNull)
            {
                continue;
            }

            if (
                int.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
            )
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, object?> row,
        IReadOnlyList<string> keys
    )
    {
        foreach (var key in keys)
        {
            if (!row.TryGetValue(key, out var value) || value is null or DBNull)
            {
                continue;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

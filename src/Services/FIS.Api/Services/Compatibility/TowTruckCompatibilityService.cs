using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Reads the legacy Tow_Truck lookup without requiring the expanded audit
/// columns that may only exist in the modern database.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "The command text uses only fixed table and column names; no user input is concatenated."
)]
public sealed class TowTruckCompatibilityService
{
    private const string TableName = "Tow_Truck";
    private static readonly string[] LegacyColumns =
    [
        "Tow_code",
        "Tow_area",
        "Tow_name",
        "Tow_tel",
        "Tow_fax",
    ];
    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];
    private readonly FisDbContext _context;

    public TowTruckCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TowTruckOption>> GetAllAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await QueryAsync(null, null, cancellationToken);
    }

    public async Task<TowTruckOption?> GetByIdAsync(
        short towCode,
        CancellationToken cancellationToken = default
    )
    {
        return (
            await QueryAsync(
                "WHERE [Tow_code] = @towCode",
                command => AddParameter(command, "@towCode", DbType.Int16, towCode),
                cancellationToken
            )
        ).SingleOrDefault();
    }

    public async Task<TowTruckOption> CreateAsync(
        TowTruckRequest request,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var values = BuildLegacyWriteValues(request)
            .Where(value => columns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var towCode = await ExecuteInsertAsync(values, cancellationToken);
        return new TowTruckOption(
            towCode,
            request.TowArea,
            request.TowName,
            request.TowTel,
            request.TowFax
        );
    }

    public async Task<TowTruckOption?> UpdateAsync(
        short towCode,
        TowTruckRequest request,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await GetByIdAsync(towCode, cancellationToken);
        if (existing == null)
        {
            return null;
        }

        var columns = await GetAvailableColumnsAsync(cancellationToken);
        var values = BuildLegacyWriteValues(request)
            .Where(value => columns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(towCode, values, columns, cancellationToken);
        return new TowTruckOption(
            towCode,
            request.TowArea,
            request.TowName,
            request.TowTel,
            request.TowFax
        );
    }

    public async Task<bool> DeleteAsync(
        short towCode,
        int currentUserId,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
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
            if (columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (columns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (columns.Contains("modified_by_user_code"))
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
                    WHERE [Tow_code] = @towCode
                    AND {GetActiveFilter(columns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [Tow_code] = @towCode
                    """;
            }

            AddParameter(command, "@towCode", DbType.Int16, towCode);
            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
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
        Justification = "The SELECT list and filters use only fixed legacy columns and allowlisted optional columns; values are parameters."
    )]
    private async Task<List<TowTruckOption>> QueryAsync(
        string? predicate,
        Action<DbCommand>? configure,
        CancellationToken cancellationToken
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
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
            var projection = LegacyColumns
                .Select(column => GetProjection(columns, column))
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(columns, column)))
                .ToArray();
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                {(
                    string.IsNullOrWhiteSpace(predicate)
                        ? $"WHERE {GetActiveFilter(columns)}"
                        : $"{predicate} AND {GetActiveFilter(columns)}"
                )}
                ORDER BY {GetOrderExpression(columns, "Tow_name")}, [Tow_code]
                """;
            configure?.Invoke(command);

            var options = new List<TowTruckOption>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(MapTowTruck(reader));
            }

            return options;
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
        Justification = "The INSERT statement uses fixed legacy columns and allowlisted optional values; every value is parameterized."
    )]
    private async Task<short> ExecuteInsertAsync(
        IReadOnlyList<WriteValue> values,
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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[Tow_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt16(await command.ExecuteScalarAsync(cancellationToken));
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
        Justification = "The UPDATE statement uses fixed legacy columns and allowlisted optional values; every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        short towCode,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> columns,
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
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [Tow_code] = @towCode
                AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@towCode", DbType.Int16, towCode);
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
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            if (!columns.Contains("Tow_code"))
            {
                throw new InvalidOperationException(
                    "The required Tow_Truck compatibility column Tow_code is not available."
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

    private static string GetProjection(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS varchar(1)) AS [{column}]";

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

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetOrderExpression(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column) ? $"[{column}]" : "[Tow_code]";

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).TrimEnd();
    }

    private static TowTruckOption MapTowTruck(DbDataReader reader) =>
        new(
            Convert.ToInt16(reader.GetValue(reader.GetOrdinal("Tow_code"))),
            ReadString(reader, "Tow_area"),
            ReadString(reader, "Tow_name"),
            ReadString(reader, "Tow_tel"),
            ReadString(reader, "Tow_fax")
        );

    private static List<WriteValue> BuildLegacyWriteValues(TowTruckRequest request) =>
        [
            new("Tow_area", "@towArea", DbType.String, request.TowArea),
            new("Tow_name", "@towName", DbType.String, request.TowName),
            new("Tow_tel", "@towTel", DbType.String, request.TowTel),
            new("Tow_fax", "@towFax", DbType.String, request.TowFax),
        ];

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.Contains(column))
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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}

public sealed record TowTruckOption(
    short TowCode,
    string? TowArea,
    string? TowName,
    string? TowTel,
    string? TowFax
);

public sealed record TowTruckRequest(
    string? TowArea,
    string? TowName,
    string? TowTel,
    string? TowFax
);

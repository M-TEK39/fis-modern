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
    Justification = "The command text uses only fixed table and column names; no user input is concatenated.")]
public sealed class TowTruckCompatibilityService
{
    private const string TableName = "Tow_Truck";
    private readonly FisDbContext _context;

    public TowTruckCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TowTruckOption>> GetAllAsync(
        CancellationToken cancellationToken = default)
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
            command.CommandText = $"""
                SELECT
                    [Tow_code],
                    {GetProjection(columns, "Tow_area")},
                    {GetProjection(columns, "Tow_name")},
                    {GetProjection(columns, "Tow_tel")},
                    {GetProjection(columns, "Tow_fax")}
                FROM [dbo].[{TableName}]
                WHERE {GetActiveFilter(columns)}
                ORDER BY {GetOrderExpression(columns, "Tow_name")}, [Tow_code]
                """;

            var options = new List<TowTruckOption>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                options.Add(new TowTruckOption(
                    Convert.ToInt16(reader.GetValue(reader.GetOrdinal("Tow_code"))),
                    ReadString(reader, "Tow_area"),
                    ReadString(reader, "Tow_name"),
                    ReadString(reader, "Tow_tel"),
                    ReadString(reader, "Tow_fax")));
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync(CancellationToken cancellationToken)
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
                throw new InvalidOperationException("The required Tow_Truck compatibility column Tow_code is not available.");
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

    private static string GetProjection(IReadOnlySet<string> columns, string column)
        => columns.Contains(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS varchar(1)) AS [{column}]";

    private static string GetActiveFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetOrderExpression(IReadOnlySet<string> columns, string column)
        => columns.Contains(column) ? $"[{column}]" : "[Tow_code]";

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).TrimEnd();
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed record TowTruckOption(
    short TowCode,
    string? TowArea,
    string? TowName,
    string? TowTel,
    string? TowFax);

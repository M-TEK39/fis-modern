using System.Data;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Satellite Workflow.* tables are not in the 2012 GGFIS archive.
/// Reads return empty when the table is absent; writes stay labelled.
/// </summary>
internal static class WorkflowOptionalTable
{
    internal static string MissingMessage(string tableName) =>
        MissingMessage("Workflow", tableName);

    internal static string MissingMessage(string schemaName, string tableName) =>
        $"{schemaName}.{tableName} is unavailable on this database. No compatibility fallback was used.";

    internal static Task<bool> ExistsAsync(FisDbContext context, string tableName) =>
        ExistsInSchemaAsync(context, "Workflow", tableName);

    internal static async Task<bool> ExistsInSchemaAsync(
        FisDbContext context,
        string schemaName,
        string tableName
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
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [INFORMATION_SCHEMA].[TABLES]
                    WHERE [TABLE_SCHEMA] = @schemaName
                      AND [TABLE_NAME] = @tableName
                ) THEN 1 ELSE 0 END
                """;
            var schema = command.CreateParameter();
            schema.ParameterName = "@schemaName";
            schema.DbType = DbType.String;
            schema.Value = schemaName;
            command.Parameters.Add(schema);
            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.DbType = DbType.String;
            table.Value = tableName;
            command.Parameters.Add(table);
            var result = await command.ExecuteScalarAsync();
            return result is not null && Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static async Task<bool> ColumnExistsAsync(
        FisDbContext context,
        string schemaName,
        string tableName,
        string columnName
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
            await using var command = connection.CreateCommand();
            command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [INFORMATION_SCHEMA].[COLUMNS]
                    WHERE [TABLE_SCHEMA] = @schemaName
                      AND [TABLE_NAME] = @tableName
                      AND [COLUMN_NAME] = @columnName
                ) THEN 1 ELSE 0 END
                """;
            var schema = command.CreateParameter();
            schema.ParameterName = "@schemaName";
            schema.DbType = DbType.String;
            schema.Value = schemaName;
            command.Parameters.Add(schema);
            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.DbType = DbType.String;
            table.Value = tableName;
            command.Parameters.Add(table);
            var column = command.CreateParameter();
            column.ParameterName = "@columnName";
            column.DbType = DbType.String;
            column.Value = columnName;
            command.Parameters.Add(column);
            var result = await command.ExecuteScalarAsync();
            return result is not null && Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}

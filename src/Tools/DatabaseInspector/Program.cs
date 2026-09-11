using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace FIS.Tools.DatabaseInspector;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Database Schema Inspector");
        Console.WriteLine("============================");

        var connectionString = SqlServerConnectionStringHelper.Resolve(
            Environment.GetEnvironmentVariable("ConnectionStrings__Default"),
            SqlServerConnectionStringHelper.IsDevelopmentEnvironment()
        );

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            Console.WriteLine("✅ Connected to database successfully");

            // Check what tables exist
            var command = new SqlCommand(
                @"
                SELECT 
                    TABLE_SCHEMA,
                    TABLE_NAME,
                    TABLE_TYPE 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME",
                connection
            );

            using var reader = await command.ExecuteReaderAsync();

            Console.WriteLine("\n📋 Tables found in database:");
            var tableCount = 0;
            while (await reader.ReadAsync())
            {
                tableCount++;
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                Console.WriteLine($"  ✓ {schema}.{tableName}");
            }

            if (tableCount == 0)
            {
                Console.WriteLine("  ⚠️ No tables found in database");
            }
            else
            {
                Console.WriteLine($"\nTotal tables: {tableCount}");
            }

            await PrintIndexesAsync(connection);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task PrintIndexesAsync(SqlConnection connection)
    {
        Console.WriteLine("\n📊 Physical indexes in the connected database:");

        var command = new SqlCommand(
            """
            SELECT
                schema_ref.[name] AS [schema_name],
                table_ref.[name] AS [table_name],
                index_ref.[name] AS [index_name],
                index_ref.[type_desc] AS [index_type],
                index_ref.[is_unique],
                index_ref.[is_primary_key],
                index_ref.[is_disabled],
                STUFF((
                    SELECT ', ' + QUOTENAME(column_ref.[name])
                        + CASE WHEN key_ref.[is_descending_key] = 1 THEN ' DESC' ELSE '' END
                    FROM sys.index_columns AS key_ref
                    INNER JOIN sys.columns AS column_ref
                        ON column_ref.[object_id] = key_ref.[object_id]
                        AND column_ref.[column_id] = key_ref.[column_id]
                    WHERE key_ref.[object_id] = index_ref.[object_id]
                        AND key_ref.[index_id] = index_ref.[index_id]
                        AND key_ref.[key_ordinal] > 0
                    ORDER BY key_ref.[key_ordinal]
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'), 1, 2, '') AS [key_columns]
            FROM sys.tables AS table_ref
            INNER JOIN sys.schemas AS schema_ref ON schema_ref.[schema_id] = table_ref.[schema_id]
            INNER JOIN sys.indexes AS index_ref ON index_ref.[object_id] = table_ref.[object_id]
            WHERE index_ref.[index_id] > 0
                AND index_ref.[is_hypothetical] = 0
            ORDER BY schema_ref.[name], table_ref.[name], index_ref.[index_id];
            """,
            connection
        );

        var indexCount = 0;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexCount++;
                var keyColumns = reader.IsDBNull(7) ? "(no key columns)" : reader.GetString(7);
                Console.WriteLine(
                    $"  ✓ {reader.GetString(0)}.{reader.GetString(1)} | {reader.GetString(2)} | {reader.GetString(3)} | keys: {keyColumns} | unique: {reader.GetBoolean(4)} | primary: {reader.GetBoolean(5)} | disabled: {reader.GetBoolean(6)}"
                );
            }
        }

        Console.WriteLine($"Total physical indexes: {indexCount}");
        await PrintIndexUsageAsync(connection);
    }

    private static async Task PrintIndexUsageAsync(SqlConnection connection)
    {
        Console.WriteLine("\n📈 Index usage since the SQL Server instance started:");

        var command = new SqlCommand(
            """
            SELECT TOP (50)
                schema_ref.[name] AS [schema_name],
                table_ref.[name] AS [table_name],
                index_ref.[name] AS [index_name],
                usage_ref.[user_seeks],
                usage_ref.[user_scans],
                usage_ref.[user_lookups],
                usage_ref.[user_updates],
                usage_ref.[last_user_seek],
                usage_ref.[last_user_scan]
            FROM sys.dm_db_index_usage_stats AS usage_ref
            INNER JOIN sys.indexes AS index_ref
                ON index_ref.[object_id] = usage_ref.[object_id]
                AND index_ref.[index_id] = usage_ref.[index_id]
            INNER JOIN sys.tables AS table_ref ON table_ref.[object_id] = index_ref.[object_id]
            INNER JOIN sys.schemas AS schema_ref ON schema_ref.[schema_id] = table_ref.[schema_id]
            WHERE usage_ref.[database_id] = DB_ID()
                AND index_ref.[index_id] > 0
            ORDER BY
                usage_ref.[user_seeks] + usage_ref.[user_scans] + usage_ref.[user_lookups] DESC,
                usage_ref.[user_updates] DESC;
            """,
            connection
        );

        try
        {
            var usageCount = 0;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                usageCount++;
                Console.WriteLine(
                    $"  ✓ {reader.GetString(0)}.{reader.GetString(1)} | {reader.GetString(2)} | seeks: {FormatNullableInt64(reader, 3)} | scans: {FormatNullableInt64(reader, 4)} | lookups: {FormatNullableInt64(reader, 5)} | updates: {FormatNullableInt64(reader, 6)} | last seek: {FormatNullableDateTime(reader, 7)} | last scan: {FormatNullableDateTime(reader, 8)}"
                );
            }

            if (usageCount == 0)
            {
                Console.WriteLine("  ⚠️ No usage rows were returned for this database.");
            }
        }
        catch (SqlException exception)
        {
            Console.WriteLine(
                $"  ⚠️ Index usage could not be read ({exception.Number}). The login may need the SQL Server performance-state permission."
            );
        }
    }

    private static string FormatNullableDateTime(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "never" : reader.GetDateTime(ordinal).ToString("O");

    private static string FormatNullableInt64(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "0" : reader.GetInt64(ordinal).ToString();
}

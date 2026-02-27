using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FIS.Data.SqlServer;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;

namespace FIS.Tools.DatabaseMigrationTool;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 FIS Database Migration Tool - Idempotent Schema Sync");
        Console.WriteLine("======================================================");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            Console.WriteLine("🔍 Checking database state...");

            // Check if we can connect
            bool canConnect = await dbContext.Database.CanConnectAsync();
            if (!canConnect)
            {
                Console.WriteLine("📦 Database does not exist. Creating...");
                await dbContext.Database.EnsureCreatedAsync();
                Console.WriteLine("✅ Database created successfully!");
            }
            else
            {
                Console.WriteLine("✅ Database exists. Checking for schema updates...");
                await SyncSchemaAsync(dbContext, logger);
            }

            Console.WriteLine();

            // Verify schema integrity
            Console.WriteLine("🛡️ Verifying schema integrity...");
            await VerifySchemaIntegrity(dbContext);

            // Test connection
            var vehicleCount = await dbContext.Vehicles.CountAsync();

            Console.WriteLine();
            Console.WriteLine("🔌 Database Connection Test:");
            Console.WriteLine($"  ✓ Connected to database successfully");
            Console.WriteLine($"  ✓ Vehicles table accessible ({vehicleCount} records)");
            Console.WriteLine();

            Console.WriteLine("🎉 SUCCESS: Database schema is synchronized and up-to-date!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to sync database schema");
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"   {ex.InnerException?.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task SyncSchemaAsync(FisDbContext dbContext, ILogger logger)
    {
        var connectionString = dbContext.Database.GetConnectionString();
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Get all entity types from EF Core model
        var entityTypes = dbContext.Model.GetEntityTypes();
        int tablesCreated = 0;
        int tablesUpdated = 0;
        int columnsAdded = 0;

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "dbo";

            if (string.IsNullOrEmpty(tableName))
                continue;

            Console.WriteLine($"  🔍 Checking table [{schema}].[{tableName}]...");

            // Ensure non-default schemas exist before checking/creating tables.
            await EnsureSchemaExistsAsync(connection, schema);

            // Check if table exists
            bool tableExists = await TableExistsAsync(connection, tableName, schema);

            if (!tableExists)
            {
                Console.WriteLine($"    ➕ Table does not exist. Creating...");
                await CreateTableAsync(dbContext, entityType, schema, tableName);
                tablesCreated++;
                Console.WriteLine($"    ✅ Table created successfully");
            }
            else
            {
                // Table exists, check for missing columns
                var missingColumns = await GetMissingColumnsAsync(connection, entityType, schema, tableName);

                if (missingColumns.Any())
                {
                    Console.WriteLine($"    🔧 Found {missingColumns.Count} missing columns. Adding...");
                    foreach (var column in missingColumns)
                    {
                        await AddColumnAsync(connection, schema, tableName, column);
                        columnsAdded++;
                        Console.WriteLine($"       ✅ Added column: {column.ColumnName} ({column.DataType})");
                    }
                    tablesUpdated++;
                }
                else
                {
                    Console.WriteLine($"    ✅ Table is up-to-date");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("📊 Schema Sync Summary:");
        Console.WriteLine($"  • Tables created: {tablesCreated}");
        Console.WriteLine($"  • Tables updated: {tablesUpdated}");
        Console.WriteLine($"  • Columns added: {columnsAdded}");
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName, string schema)
    {
        var sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName
            ) THEN 1 ELSE 0 END";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Schema", schema);
        cmd.Parameters.AddWithValue("@TableName", tableName);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    private static async Task EnsureSchemaExistsAsync(SqlConnection connection, string schema)
    {
        if (string.Equals(schema, "dbo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @SchemaName)
            BEGIN
                DECLARE @createSql NVARCHAR(MAX) = N'CREATE SCHEMA ' + QUOTENAME(@SchemaName);
                EXEC (@createSql);
            END";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SchemaName", schema);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateTableAsync(FisDbContext dbContext, IEntityType entityType, string schema, string tableName)
    {
        // Use EF Core to generate CREATE TABLE script
        var createScript = dbContext.Database.GenerateCreateScript();

        // Extract just the CREATE TABLE statement for this table
        var tableCreateStart = createScript.IndexOf($"CREATE TABLE [{schema}].[{tableName}]");
        if (tableCreateStart == -1)
        {
            throw new Exception($"Could not find CREATE TABLE script for {schema}.{tableName}");
        }

        // Find the end of this CREATE TABLE statement (next CREATE TABLE or end of script)
        var tableCreateEnd = createScript.IndexOf("CREATE TABLE", tableCreateStart + 1);
        if (tableCreateEnd == -1)
            tableCreateEnd = createScript.Length;

        var tableScript = createScript.Substring(tableCreateStart, tableCreateEnd - tableCreateStart).Trim();

        // Remove any ALTER TABLE statements and inline FOREIGN KEY constraints
        var lines = tableScript.Split('\n');
        var createTableLines = new List<string>();
        bool insideCreateTable = false;

        foreach (var line in lines)
        {
            if (line.Contains("CREATE TABLE"))
            {
                insideCreateTable = true;
                createTableLines.Add(line);
            }
            else if (insideCreateTable)
            {
                if (line.Contains("ALTER TABLE"))
                    break;

                // Skip lines that define FOREIGN KEY constraints (they cause dependency issues)
                if (line.TrimStart().StartsWith("CONSTRAINT") && line.Contains("FOREIGN KEY"))
                {
                    // If this line ends with a comma, we need to handle the previous line's trailing comma
                    if (createTableLines.Count > 0 && createTableLines[^1].TrimEnd().EndsWith(","))
                    {
                        // Check if the next non-FK line exists - if not, remove the trailing comma
                        var nextNonFkLineIndex = lines.ToList().IndexOf(line) + 1;
                        var hasMoreColumns = false;
                        for (int i = nextNonFkLineIndex; i < lines.Length; i++)
                        {
                            var nextLine = lines[i].TrimStart();
                            if (nextLine.StartsWith("CONSTRAINT") && nextLine.Contains("FOREIGN KEY"))
                                continue;
                            if (nextLine.StartsWith(")") || nextLine.Contains(");"))
                                break;
                            if (!string.IsNullOrWhiteSpace(nextLine))
                            {
                                hasMoreColumns = true;
                                break;
                            }
                        }

                        if (!hasMoreColumns)
                        {
                            // Remove trailing comma from last column
                            createTableLines[^1] = createTableLines[^1].TrimEnd().TrimEnd(',');
                        }
                    }
                    continue;
                }

                createTableLines.Add(line);
                if (line.TrimEnd().EndsWith(";"))
                    break;
            }
        }

        var finalScript = string.Join("\n", createTableLines);
        await dbContext.Database.ExecuteSqlRawAsync(finalScript);
    }

    private static async Task<List<ColumnDefinition>> GetMissingColumnsAsync(
        SqlConnection connection, IEntityType entityType, string schema, string tableName)
    {
        // Get columns from database
        var dbColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sql = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

        using (var cmd = new SqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@Schema", schema);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dbColumns.Add(reader.GetString(0));
            }
        }

        // Get columns from EF Core model
        var missingColumns = new List<ColumnDefinition>();
        var properties = entityType.GetProperties();

        foreach (var property in properties)
        {
            var columnName = property.GetColumnName();
            if (string.IsNullOrEmpty(columnName))
                continue;

            if (!dbColumns.Contains(columnName))
            {
                var columnDef = new ColumnDefinition
                {
                    ColumnName = columnName,
                    DataType = GetSqlDataType(property),
                    IsNullable = property.IsNullable,
                    DefaultValue = property.GetDefaultValueSql()
                };
                missingColumns.Add(columnDef);
            }
        }

        return missingColumns;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL is constructed from EF Core metadata, not user input")]
    private static async Task AddColumnAsync(SqlConnection connection, string schema, string tableName, ColumnDefinition column)
    {
        // For non-nullable columns without an explicit default, provide a sensible default
        // This allows adding columns to tables with existing data
        string? defaultValue = column.DefaultValue;

        if (!column.IsNullable && string.IsNullOrEmpty(defaultValue))
        {
            // Provide type-appropriate default values
            defaultValue = column.DataType.ToUpperInvariant() switch
            {
                var t when t.Contains("INT") || t.Contains("NUMERIC") || t.Contains("DECIMAL") => "0",
                var t when t.Contains("BIT") => "0",
                var t when t.Contains("DATETIME") => "GETDATE()",
                var t when t.Contains("UNIQUEIDENTIFIER") => "NEWID()",
                var t when t.Contains("NVARCHAR") || t.Contains("VARCHAR") || t.Contains("CHAR") => "''",
                _ => (string?)null
            };
        }

        var nullability = column.IsNullable ? "NULL" : "NOT NULL";
        var defaultClause = !string.IsNullOrEmpty(defaultValue) ? $" DEFAULT {defaultValue}" : "";

        var sql = $@"
            ALTER TABLE [{schema}].[{tableName}]
            ADD [{column.ColumnName}] {column.DataType} {nullability}{defaultClause}";

        using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string GetSqlDataType(IProperty property)
    {
        var storeType = property.GetColumnType();
        if (!string.IsNullOrEmpty(storeType))
            return storeType;

        // Fallback mapping for common types
        var clrType = property.ClrType;
        var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return underlyingType.Name switch
        {
            "Int32" => "INT",
            "Int16" => "SMALLINT",
            "Int64" => "BIGINT",
            "String" => $"NVARCHAR({property.GetMaxLength() ?? 255})",
            "Boolean" => "BIT",
            "DateTime" => "DATETIME2",
            "Decimal" => "DECIMAL(18,2)",
            "Double" => "FLOAT",
            "Guid" => "UNIQUEIDENTIFIER",
            "Byte" => "TINYINT",
            _ => "NVARCHAR(MAX)"
        };
    }

    private static async Task VerifySchemaIntegrity(FisDbContext dbContext)
    {
        // Verify core legacy tables exist
        var tables = new[] { "vehicle_master", "contract", "site", "TS_Users", "department" };
        foreach (var table in tables)
        {
            try
            {
                string sql = "SELECT TOP 1 * FROM " + table;
                await dbContext.Database.ExecuteSqlRawAsync(sql);
                Console.WriteLine($"  ✓ Table '{table}' verified.");
            }
            catch (Exception)
            {
                throw new Exception($"Critical legacy table '{table}' is missing or inaccessible!");
            }
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var connectionString = context.Configuration["ConnectionStrings:Default"];
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = "Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;";
                }

                services.AddDbContext<FisDbContext>(options =>
                    options.UseSqlServer(connectionString)
                );

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            });
}

public class ColumnDefinition
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
}

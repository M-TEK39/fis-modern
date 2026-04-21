using System.Data;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            Console.WriteLine("🛡️ Verifying schema integrity...");
            await VerifySchemaIntegrity(dbContext);

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

        var entityTypes = dbContext.Model.GetEntityTypes();
        int tablesCreated = 0;
        int tablesUpdated = 0;
        int columnsAdded = 0;
        int tablesRebuilt = 0;

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema() ?? "dbo";

            if (string.IsNullOrEmpty(tableName))
                continue;

            Console.WriteLine($"  🔍 Checking table [{schema}].[{tableName}]...");

            await EnsureSchemaExistsAsync(connection, schema);

            bool tableExists = await TableExistsAsync(connection, tableName, schema);

            if (!tableExists)
            {
                // Clean up any leftover __identity_old table from a previous failed rebuild.
                // It may still hold the PK constraint name that the new table needs.
                var leftoverName = $"{tableName}__identity_old";
                bool leftoverExists = await TableExistsAsync(connection, leftoverName, schema);
                if (leftoverExists)
                {
                    Console.WriteLine(
                        $"    🧹 Cleaning up leftover '{leftoverName}' from previous run..."
                    );
                    await DropTableAsync(connection, schema, leftoverName);
                }

                Console.WriteLine($"    ➕ Table does not exist. Creating...");
                await CreateTableAsync(dbContext, entityType, schema, tableName);
                tablesCreated++;
                Console.WriteLine($"    ✅ Table created successfully");
            }
            else
            {
                // Check for IDENTITY mismatches first — requires a table rebuild
                var identityMismatches = await GetIdentityMismatchColumnsAsync(
                    connection,
                    entityType,
                    schema,
                    tableName
                );

                if (identityMismatches.Any())
                {
                    Console.WriteLine(
                        $"    ⚠️  IDENTITY missing on: {string.Join(", ", identityMismatches)}"
                    );
                    Console.WriteLine(
                        $"    🔄 Rebuilding table to add IDENTITY property (data preserved)..."
                    );
                    await RebuildTableWithIdentityAsync(
                        dbContext,
                        connection,
                        entityType,
                        schema,
                        tableName
                    );
                    tablesRebuilt++;
                    tablesUpdated++;
                    Console.WriteLine($"    ✅ Table rebuilt with IDENTITY columns");
                }
                else
                {
                    // IDENTITY is fine — just check for missing columns
                    var missingColumns = await GetMissingColumnsAsync(
                        connection,
                        entityType,
                        schema,
                        tableName
                    );

                    if (missingColumns.Any())
                    {
                        Console.WriteLine(
                            $"    🔧 Found {missingColumns.Count} missing columns. Adding..."
                        );
                        foreach (var column in missingColumns)
                        {
                            await AddColumnAsync(connection, schema, tableName, column);
                            columnsAdded++;
                            Console.WriteLine(
                                $"       ✅ Added column: {column.ColumnName} ({column.DataType})"
                            );
                        }
                        tablesUpdated++;
                    }
                    else
                    {
                        Console.WriteLine($"    ✅ Table is up-to-date");
                    }
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("📊 Schema Sync Summary:");
        Console.WriteLine($"  • Tables created:  {tablesCreated}");
        Console.WriteLine($"  • Tables rebuilt (IDENTITY fix): {tablesRebuilt}");
        Console.WriteLine($"  • Tables updated (columns added): {tablesUpdated - tablesRebuilt}");
        Console.WriteLine($"  • Columns added:   {columnsAdded}");
    }

    // -----------------------------------------------------------------------
    // IDENTITY mismatch detection
    // -----------------------------------------------------------------------

    private static async Task<List<string>> GetIdentityMismatchColumnsAsync(
        SqlConnection connection,
        IEntityType entityType,
        string schema,
        string tableName
    )
    {
        var mismatches = new List<string>();

        foreach (var property in entityType.GetProperties())
        {
            // Only PK columns should have IDENTITY(1,1) in SQL Server
            if (property.ValueGenerated != ValueGenerated.OnAdd || !property.IsPrimaryKey())
                continue;

            var columnName = property.GetColumnName();
            if (string.IsNullOrEmpty(columnName))
                continue;

            var sql =
                @"
                SELECT COLUMNPROPERTY(OBJECT_ID(@FullName), @Col, 'IsIdentity')";

            using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@FullName", $"{schema}.{tableName}");
            cmd.Parameters.AddWithValue("@Col", columnName);

            var result = await cmd.ExecuteScalarAsync();
            bool isIdentity = result != DBNull.Value && Convert.ToInt32(result) == 1;

            if (!isIdentity)
                mismatches.Add(columnName);
        }

        return mismatches;
    }

    // -----------------------------------------------------------------------
    // Table rebuild to add IDENTITY — preserves all existing data
    // -----------------------------------------------------------------------

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL is constructed from EF Core metadata, not user input"
    )]
    private static async Task RebuildTableWithIdentityAsync(
        FisDbContext dbContext,
        SqlConnection connection,
        IEntityType entityType,
        string schema,
        string tableName
    )
    {
        var oldName = $"{tableName}__identity_old";

        // Ensure no leftover temp table from a previous failed run
        bool oldExists = await TableExistsAsync(connection, oldName, schema);
        if (oldExists)
            await DropTableAsync(connection, schema, oldName);

        // 1. Rename original → temp
        using (
            var cmd = new SqlCommand(
                $"EXEC sp_rename '[{schema}].[{tableName}]', '{oldName}'",
                connection
            )
        )
            await cmd.ExecuteNonQueryAsync();

        // 1b. Drop the PK constraint on the old table so the new table can reuse the same constraint name
        var dropPkSql =
            $@"
            DECLARE @pkName NVARCHAR(256)
            SELECT @pkName = kc.name
            FROM sys.key_constraints kc
            JOIN sys.tables t ON kc.parent_object_id = t.object_id
            WHERE kc.type = 'PK'
              AND t.name = '{oldName}'
              AND SCHEMA_NAME(t.schema_id) = '{schema}'
            IF @pkName IS NOT NULL
            BEGIN
                DECLARE @dropSql NVARCHAR(MAX) = 'ALTER TABLE [{schema}].[{oldName}] DROP CONSTRAINT [' + @pkName + ']'
                EXEC (@dropSql)
            END";
        using (var cmd = new SqlCommand(dropPkSql, connection))
            await cmd.ExecuteNonQueryAsync();

        // 2. Create new table with correct schema (IDENTITY included)
        await CreateTableAsync(dbContext, entityType, schema, tableName);

        // 3. Build column list from EF model (columns that exist in both tables)
        var efColumns = entityType
            .GetProperties()
            .Select(p => p.GetColumnName())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        // Only copy columns that physically exist in the old table
        var oldColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var colSql =
            @"
            SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";
        using (var cmd = new SqlCommand(colSql, connection))
        {
            cmd.Parameters.AddWithValue("@Schema", schema);
            cmd.Parameters.AddWithValue("@TableName", oldName);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                oldColumns.Add(reader.GetString(0));
        }

        var copyColumns = efColumns
            .Where(c => oldColumns.Contains(c!))
            .Select(c => $"[{c}]")
            .ToList();

        if (!copyColumns.Any())
        {
            // Nothing to copy (empty table or schema mismatch) — just drop old
            using var drop = new SqlCommand($"DROP TABLE [{schema}].[{oldName}]", connection);
            await drop.ExecuteNonQueryAsync();
            return;
        }

        var columnList = string.Join(", ", copyColumns);

        // 4. Copy data — use IDENTITY_INSERT so existing IDs are preserved
        var identityColumns = entityType
            .GetProperties()
            .Where(p => p.ValueGenerated == ValueGenerated.OnAdd && p.IsPrimaryKey())
            .Select(p => p.GetColumnName())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        if (identityColumns.Any())
        {
            using var setOn = new SqlCommand(
                $"SET IDENTITY_INSERT [{schema}].[{tableName}] ON",
                connection
            );
            await setOn.ExecuteNonQueryAsync();
        }

        using (
            var copy = new SqlCommand(
                $"INSERT INTO [{schema}].[{tableName}] ({columnList}) "
                    + $"SELECT {columnList} FROM [{schema}].[{oldName}]",
                connection
            )
        )
            await copy.ExecuteNonQueryAsync();

        if (identityColumns.Any())
        {
            using var setOff = new SqlCommand(
                $"SET IDENTITY_INSERT [{schema}].[{tableName}] OFF",
                connection
            );
            await setOff.ExecuteNonQueryAsync();

            // Reseed so next INSERT gets MAX + 1
            foreach (var col in identityColumns)
            {
                var reseedSql =
                    $@"
                    DECLARE @max BIGINT = (SELECT ISNULL(MAX([{col}]), 0) FROM [{schema}].[{tableName}])
                    DBCC CHECKIDENT('[{schema}].[{tableName}]', RESEED, @max)";
                using var reseed = new SqlCommand(reseedSql, connection);
                await reseed.ExecuteNonQueryAsync();
            }
        }

        // 5. Drop old table
        await DropTableAsync(connection, schema, oldName);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL is constructed from EF Core metadata, not user input"
    )]
    private static async Task DropTableAsync(
        SqlConnection connection,
        string schema,
        string tableName
    )
    {
        // Drop PK constraint first (prevents name conflicts when recreating)
        var dropPkSql =
            $@"
            DECLARE @pkName NVARCHAR(256)
            SELECT @pkName = kc.name
            FROM sys.key_constraints kc
            JOIN sys.tables t ON kc.parent_object_id = t.object_id
            WHERE kc.type = 'PK' AND t.name = '{tableName}' AND SCHEMA_NAME(t.schema_id) = '{schema}'
            IF @pkName IS NOT NULL
            BEGIN
                DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE [{schema}].[{tableName}] DROP CONSTRAINT [' + @pkName + ']'
                EXEC (@sql)
            END";
        using (var cmd = new SqlCommand(dropPkSql, connection))
            await cmd.ExecuteNonQueryAsync();

        using var drop = new SqlCommand($"DROP TABLE [{schema}].[{tableName}]", connection);
        await drop.ExecuteNonQueryAsync();
    }

    // -----------------------------------------------------------------------
    // Existing helpers (unchanged)
    // -----------------------------------------------------------------------

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string tableName,
        string schema
    )
    {
        var sql =
            @"
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
            return;

        const string sql =
            @"
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @SchemaName)
            BEGIN
                DECLARE @createSql NVARCHAR(MAX) = N'CREATE SCHEMA ' + QUOTENAME(@SchemaName);
                EXEC (@createSql);
            END";

        using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@SchemaName", schema);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateTableAsync(
        FisDbContext dbContext,
        IEntityType entityType,
        string schema,
        string tableName
    )
    {
        var createScript = dbContext.Database.GenerateCreateScript();

        var tableCreateStart = createScript.IndexOf($"CREATE TABLE [{schema}].[{tableName}]");
        if (tableCreateStart == -1)
            throw new Exception($"Could not find CREATE TABLE script for {schema}.{tableName}");

        var tableCreateEnd = createScript.IndexOf("CREATE TABLE", tableCreateStart + 1);
        if (tableCreateEnd == -1)
            tableCreateEnd = createScript.Length;

        var tableScript = createScript
            .Substring(tableCreateStart, tableCreateEnd - tableCreateStart)
            .Trim();

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

                if (line.TrimStart().StartsWith("CONSTRAINT") && line.Contains("FOREIGN KEY"))
                {
                    if (createTableLines.Count > 0 && createTableLines[^1].TrimEnd().EndsWith(","))
                    {
                        var nextNonFkLineIndex = lines.ToList().IndexOf(line) + 1;
                        var hasMoreColumns = false;
                        for (int i = nextNonFkLineIndex; i < lines.Length; i++)
                        {
                            var nextLine = lines[i].TrimStart();
                            if (
                                nextLine.StartsWith("CONSTRAINT")
                                && nextLine.Contains("FOREIGN KEY")
                            )
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
                            createTableLines[^1] = createTableLines[^1].TrimEnd().TrimEnd(',');
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
        SqlConnection connection,
        IEntityType entityType,
        string schema,
        string tableName
    )
    {
        var dbColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sql =
            @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

        using (var cmd = new SqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("@Schema", schema);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                dbColumns.Add(reader.GetString(0));
        }

        var missingColumns = new List<ColumnDefinition>();

        foreach (var property in entityType.GetProperties())
        {
            var columnName = property.GetColumnName();
            if (string.IsNullOrEmpty(columnName))
                continue;

            if (!dbColumns.Contains(columnName))
            {
                missingColumns.Add(
                    new ColumnDefinition
                    {
                        ColumnName = columnName,
                        DataType = GetSqlDataType(property),
                        IsNullable = property.IsNullable,
                        DefaultValue = property.GetDefaultValueSql(),
                    }
                );
            }
        }

        return missingColumns;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL is constructed from EF Core metadata, not user input"
    )]
    private static async Task AddColumnAsync(
        SqlConnection connection,
        string schema,
        string tableName,
        ColumnDefinition column
    )
    {
        string? defaultValue = column.DefaultValue;

        if (!column.IsNullable && string.IsNullOrEmpty(defaultValue))
        {
            defaultValue = column.DataType.ToUpperInvariant() switch
            {
                var t when t.Contains("INT") || t.Contains("NUMERIC") || t.Contains("DECIMAL") =>
                    "0",
                var t when t.Contains("BIT") => "0",
                var t when t.Contains("DATETIME") => "GETDATE()",
                var t when t.Contains("UNIQUEIDENTIFIER") => "NEWID()",
                var t when t.Contains("NVARCHAR") || t.Contains("VARCHAR") || t.Contains("CHAR") =>
                    "''",
                _ => (string?)null,
            };
        }

        var nullability = column.IsNullable ? "NULL" : "NOT NULL";
        var defaultClause = !string.IsNullOrEmpty(defaultValue) ? $" DEFAULT {defaultValue}" : "";

        var sql =
            $@"
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
            _ => "NVARCHAR(MAX)",
        };
    }

    private static async Task VerifySchemaIntegrity(FisDbContext dbContext)
    {
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
            .ConfigureServices(
                (context, services) =>
                {
                    var connectionString = SqlServerConnectionStringHelper.Resolve(
                        context.Configuration["ConnectionStrings:Default"],
                        context.HostingEnvironment.IsDevelopment()
                    );

                    services.AddDbContext<FisDbContext>(options =>
                        options.UseSqlServer(connectionString)
                    );

                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                }
            );
}

public class ColumnDefinition
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
}

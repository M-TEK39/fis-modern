using Microsoft.Extensions.Hosting;
using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseInspector;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔍 Database Schema Inspector");
        Console.WriteLine("============================");

        var connectionString = "Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;";
        
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            Console.WriteLine("✅ Connected to database successfully");
            
            // Check what tables exist
            var command = new SqlCommand(@"
                SELECT 
                    TABLE_SCHEMA,
                    TABLE_NAME,
                    TABLE_TYPE 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME", connection);
                
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
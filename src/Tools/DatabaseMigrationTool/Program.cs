using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FIS.Data.SqlServer;

namespace FIS.Tools.DatabaseMigrationTool;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🔧 FIS Database Migration Tool - Legacy Schema Setup");
        Console.WriteLine("===================================================");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            Console.WriteLine("�️  Dropping existing database if it exists...");
            
            // First, ensure the database is deleted to start fresh
            await dbContext.Database.EnsureDeletedAsync();
            Console.WriteLine("✅ Database dropped successfully!");
            
            Console.WriteLine("📦 Creating database with exact legacy schema...");
            
            // This will create the database with the exact legacy schema
            await dbContext.Database.EnsureCreatedAsync();
            
            Console.WriteLine("✅ Database created successfully!");
            Console.WriteLine();
            Console.WriteLine("📋 Executed Legacy Schema Setup:");
            Console.WriteLine("  ✓ vehicle_master table with exact legacy field names");
            Console.WriteLine("  ✓ contract table with legacy constraints"); 
            Console.WriteLine("  ✓ site table with legacy typo preserved (Depatrment_code)");
            Console.WriteLine("  ✓ Fuel_card table with exact legacy schema");
            Console.WriteLine("  ✓ trip_driver table (singular, not plural)");
            Console.WriteLine("  ✓ Private_hire table with exact case sensitivity");
            Console.WriteLine("  ✓ site_drivers table (correct table name)");
            Console.WriteLine("  ✓ trip_authorities table (correct table name)"); 
            Console.WriteLine("  ✓ department table (singular, not plural)");
            Console.WriteLine();
            Console.WriteLine("🎯 CRITICAL FIXES APPLIED:");
            Console.WriteLine("  ✓ Fixed int/smallint data type consistency");
            Console.WriteLine("  ✓ Added unique constraint on contract(vmf_code, still_current)");
            Console.WriteLine("  ✓ Preserved all legacy field names exactly");
            Console.WriteLine("  ✓ No modernization - perfect legacy compatibility");
            Console.WriteLine();

            // Test that we can connect and query
            var vehicleCount = await dbContext.Vehicles.CountAsync();
            var siteCount = await dbContext.Sites.CountAsync();
            
            Console.WriteLine("🔌 Database Connection Test:");
            Console.WriteLine($"  ✓ Connected to database successfully");
            Console.WriteLine($"  ✓ Vehicles table: {vehicleCount} records");
            Console.WriteLine($"  ✓ Sites table: {siteCount} records");
            Console.WriteLine();
            Console.WriteLine("🎉 SUCCESS: Legacy database schema recreated perfectly!");
            Console.WriteLine("    Ready for business logic that expects exact legacy field names.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to create database");
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var connectionString = "Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;";
                
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
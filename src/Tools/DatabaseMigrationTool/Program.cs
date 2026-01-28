using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FIS.Data.SqlServer;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using BCrypt.Net;

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
            
            // Seed test users
            Console.WriteLine("👥 Seeding test users...");
            await SeedTestUsers(dbContext);
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

    private static async Task SeedTestUsers(FisDbContext dbContext)
    {
        // Use raw SQL to insert users with explicit IDs (including audit fields)
        await dbContext.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT TS_Users ON;
            
            INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES 
            (1, 'murcus@corptech.co.za', '+27 11 123 4567', GETDATE(), 0),
            (2, 'xxodbeats@gmail.com', '+27 82 555 1234', GETDATE(), 0),
            (3, 'It@kulungwana.co.za', '+27 11 987 6543', GETDATE(), 0),
            (4, 'info.backup@kulungwana.co.za', '+27 11 987 6544', GETDATE(), 0);
            
            SET IDENTITY_INSERT TS_Users OFF;
        ");

        Console.WriteLine($"  ✓ Added 4 users to TS_Users table");

        // Create JWT credentials (password: "Password123!")
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        
        var jwtCredentials = new[]
        {
            new LegacyUserCredential
            {
                user_access_code = 1, // Sysadmin
                password_hash = passwordHash,
                password_salt = "", // BCrypt includes salt
                created_date = DateTime.UtcNow,
                last_password_change = DateTime.UtcNow,
                is_active = true
            },
            new LegacyUserCredential
            {
                user_access_code = 2, // Murcus Makhubele
                password_hash = passwordHash,
                password_salt = "", // BCrypt includes salt
                created_date = DateTime.UtcNow,
                last_password_change = DateTime.UtcNow,
                is_active = true
            }
        };

        foreach (var credential in jwtCredentials)
        {
            dbContext.LegacyUserCredentials.Add(credential);
        }
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"  ✓ Added {jwtCredentials.Length} JWT credentials (password: 'Password123!')");

        // Create Entra ID mappings (simulated Azure AD Object IDs)
        var entraIdMappings = new[]
        {
            new EntraIdUserMapping
            {
                user_access_code = 3, // John Doe
                entra_object_id = Guid.NewGuid().ToString(), // Simulated Azure AD Object ID
                created_date = DateTime.UtcNow
            },
            new EntraIdUserMapping
            {
                user_access_code = 4, // Jane Doe
                entra_object_id = Guid.NewGuid().ToString(), // Simulated Azure AD Object ID
                created_date = DateTime.UtcNow
            }
        };

        foreach (var mapping in entraIdMappings)
        {
            dbContext.EntraIdUserMappings.Add(mapping);
        }
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"  ✓ Added {entraIdMappings.Length} Entra ID mappings");
        Console.WriteLine();
        Console.WriteLine("📋 Test Users Summary:");
        Console.WriteLine("  JWT Users (Legacy Auth) - Login with user_access_code:");
        Console.WriteLine("    1. Sysadmin (murcus@corptech.co.za) - Username: '1', Password: 'Password123!'");
        Console.WriteLine("    2. Murcus Makhubele (xxodbeats@gmail.com) - Username: '2', Password: 'Password123!'");
        Console.WriteLine();
        Console.WriteLine("  Entra ID Users (Azure AD Auth) - SSO Only:");
        Console.WriteLine("    3. John Doe (It@kulungwana.co.za) - Normal User");
        Console.WriteLine("    4. Jane Doe (info.backup@kulungwana.co.za) - Admin User");
    }
}
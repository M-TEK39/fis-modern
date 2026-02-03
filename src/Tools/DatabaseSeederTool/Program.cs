using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FIS.Data.SqlServer;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.ReferenceData;
using BCrypt.Net;

namespace FIS.Tools.DatabaseSeederTool;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🌱 FIS Database Seeder Tool - Test Data Setup");
        Console.WriteLine("=============================================");
        Console.WriteLine();

        var host = CreateHostBuilder(args).Build();
        
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FisDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            Console.WriteLine("👥 Seeding users and credentials...");
            await SeedTestUsers(dbContext);
            
            Console.WriteLine("📑 Seeding reference data (Batch 1)...");
            await SeedReferenceData(dbContext);

            Console.WriteLine();
            Console.WriteLine("🎉 SUCCESS: Database seeding complete!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Failed to seed database");
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async Task SeedTestUsers(FisDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync())
        {
            Console.WriteLine("  ✓ Users already exist. Skipping user seeding.");
            return;
        }

        // Use raw SQL to insert users with explicit IDs
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

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        
        var jwtCredentials = new[]
        {
            new LegacyUserCredential
            {
                user_access_code = 1,
                password_hash = passwordHash,
                password_salt = "",
                created_date = DateTime.UtcNow,
                last_password_change = DateTime.UtcNow,
                is_active = true
            },
            new LegacyUserCredential
            {
                user_access_code = 2,
                password_hash = passwordHash,
                password_salt = "",
                created_date = DateTime.UtcNow,
                last_password_change = DateTime.UtcNow,
                is_active = true
            }
        };

        dbContext.LegacyUserCredentials.AddRange(jwtCredentials);
        
        var entraIdMappings = new[]
        {
            new EntraIdUserMapping
            {
                user_access_code = 3,
                entra_object_id = "simulated-guid-3",
                created_date = DateTime.UtcNow
            },
            new EntraIdUserMapping
            {
                user_access_code = 4,
                entra_object_id = "simulated-guid-4",
                created_date = DateTime.UtcNow
            }
        };

        dbContext.EntraIdUserMappings.AddRange(entraIdMappings);
        await dbContext.SaveChangesAsync();

        Console.WriteLine("  ✓ Credentials and mappings seeded.");
    }

    private static async Task SeedReferenceData(FisDbContext dbContext)
    {
        // Seed Provinces
        if (!await dbContext.Provinces.AnyAsync())
        {
            var provinces = new[]
            {
                new Province { province_code = 1, province_name = "Gauteng", province_abbreviation = "GT", date_created = DateTime.UtcNow },
                new Province { province_code = 2, province_name = "Western Cape", province_abbreviation = "WC", date_created = DateTime.UtcNow },
                new Province { province_code = 3, province_name = "KwaZulu-Natal", province_abbreviation = "KZN", date_created = DateTime.UtcNow },
                new Province { province_code = 4, province_name = "Eastern Cape", province_abbreviation = "EC", date_created = DateTime.UtcNow },
                new Province { province_code = 5, province_name = "Limpopo", province_abbreviation = "LP", date_created = DateTime.UtcNow },
                new Province { province_code = 6, province_name = "Mpumalanga", province_abbreviation = "MP", date_created = DateTime.UtcNow },
                new Province { province_code = 7, province_name = "North West", province_abbreviation = "NW", date_created = DateTime.UtcNow },
                new Province { province_code = 8, province_name = "Free State", province_abbreviation = "FS", date_created = DateTime.UtcNow },
                new Province { province_code = 9, province_name = "Northern Cape", province_abbreviation = "NC", date_created = DateTime.UtcNow }
            };
            dbContext.Provinces.AddRange(provinces);
            Console.WriteLine("  ✓ Provinces seeded.");
        }

        // Seed Ranks
        if (!await dbContext.Ranks.AnyAsync())
        {
            var ranks = new[]
            {
                new Rank { description = "Director", date_created = DateTime.UtcNow },
                new Rank { description = "Manager", date_created = DateTime.UtcNow },
                new Rank { description = "Officer", date_created = DateTime.UtcNow },
                new Rank { description = "Driver", date_created = DateTime.UtcNow }
            };
            dbContext.Ranks.AddRange(ranks);
            Console.WriteLine("  ✓ Ranks seeded.");
        }

        // Seed Vehicle Statuses
        if (!await dbContext.VehicleStatuses.AnyAsync())
        {
            var statuses = new[]
            {
                new VehicleStatus { status_description = "Active", date_created = DateTime.UtcNow },
                new VehicleStatus { status_description = "Maintenance", date_created = DateTime.UtcNow },
                new VehicleStatus { status_description = "Disposed", date_created = DateTime.UtcNow },
                new VehicleStatus { status_description = "Stolen", date_created = DateTime.UtcNow },
                new VehicleStatus { status_description = "Written Off", date_created = DateTime.UtcNow }
            };
            dbContext.VehicleStatuses.AddRange(statuses);
            Console.WriteLine("  ✓ Vehicle Statuses seeded.");
        }

        await dbContext.SaveChangesAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var connectionString = "Server=localhost,1433;Database=legacy;User Id=sa;Password=Behox@1903;Encrypt=True;TrustServerCertificate=True;";
                services.AddDbContext<FisDbContext>(options =>
                    options.UseSqlServer(connectionString)
                );
                services.AddLogging(builder => builder.AddConsole());
            });
}

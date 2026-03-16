using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Core.Domain.Entities.Logistics;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Domain.Entities.WorkshopEntities;
using Task = System.Threading.Tasks.Task; // Disambiguate from WorkshopEntities.Task

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

            Console.WriteLine("🔐 Seeding access levels (bitwise permissions)...");
            await SeedAccessLevelData(dbContext);

            Console.WriteLine("📑 Seeding reference data (Batch 1)...");
            await SeedReferenceData(dbContext);

            Console.WriteLine("🏢 Seeding organization data...");
            await SeedOrganizationData(dbContext);

            Console.WriteLine("🚗 Seeding vehicle data...");
            await SeedVehicleData(dbContext);

            Console.WriteLine("📜 Seeding contract data...");
            await SeedContractData(dbContext);

            Console.WriteLine("💳 Seeding lease tariff data...");
            await SeedLeaseTariffData(dbContext);

            Console.WriteLine("🧪 Seeding frontend demo coverage data...");
            await SeedFrontendDemoCoverage(dbContext);

            Console.WriteLine("⛽ Seeding fuel card data...");
            await SeedFuelCardData(dbContext);

            Console.WriteLine("🚚 Seeding trip data...");
            await SeedTripData(dbContext);

            Console.WriteLine("👨‍✈️ Seeding driver data...");
            await SeedDriverData(dbContext);

            Console.WriteLine("🔧 Seeding maintenance data...");
            await SeedMaintenanceData(dbContext);

            Console.WriteLine("👮 Seeding fine data...");
            await SeedFineData(dbContext);

            Console.WriteLine("💥 Seeding accident data...");
            await SeedAccidentData(dbContext);

            Console.WriteLine("🪪 Seeding license data...");
            await SeedLicenseData(dbContext);

            Console.WriteLine("📒 Seeding logbook data...");
            await SeedLogbookData(dbContext);

            Console.WriteLine("🏬 Seeding merchant & supplier data...");
            await SeedMerchantAndSupplierData(dbContext);

            Console.WriteLine("🚔 Seeding traffic department data...");
            await SeedTrafficDeptData(dbContext);

            Console.WriteLine("🚛 Seeding towing data...");
            await SeedTowingData(dbContext);

            Console.WriteLine("📸 Seeding vehicle photo data...");
            await SeedVehiclePhotoData(dbContext);

            Console.WriteLine("💰 Seeding financial data...");
            await SeedFinancialData(dbContext);

            Console.WriteLine("🛠️ Seeding workshop data...");
            await SeedWorkshopData(dbContext);

            Console.WriteLine("📡 Seeding tracking data...");
            await SeedTrackingData(dbContext);

            Console.WriteLine("🏗️ Seeding extended operations data...");
            await SeedOperationsExtendedData(dbContext);

            Console.WriteLine("🧩 Applying Batch 1 vehicle-master completeness pass...");
            await ApplyBatch1VehicleMasterCompletenessAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 2 operational coverage pass...");
            await ApplyBatch2OperationalCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 3 finance billing coverage pass...");
            await ApplyBatch3FinanceBillingCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 4 trip/workshop/verification coverage pass...");
            await ApplyBatch4TripWorkshopVerificationCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 5 audit/history coverage pass...");
            await ApplyBatch5AuditHistoryCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 6 reference-data breadth pass...");
            await ApplyBatch6ReferenceBreadthCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 7 notice/workflow coverage pass...");
            await ApplyBatch7NoticeWorkflowCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 8 legacy-bridge coverage pass...");
            await ApplyBatch8LegacyBridgeCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 9 system-configuration coverage pass...");
            await ApplyBatch9SystemConfigurationCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 10 tariff-segment coverage pass...");
            await ApplyBatch10TariffSegmentCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 11 contract-finance mapping coverage pass...");
            await ApplyBatch11ContractFinanceMappingCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 12 segment-anchor mapping coverage pass...");
            await ApplyBatch12SegmentAnchorCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 13 auth-ops coverage pass...");
            await ApplyBatch13AuthOperationsCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 14 maintenance-workshop coverage pass...");
            await ApplyBatch14MaintenanceWorkshopCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 15 financial-integration coverage pass...");
            await ApplyBatch15FinancialIntegrationCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 16 legacy-identifiers coverage pass...");
            await ApplyBatch16LegacyIdentifiersCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 17 location-temp coverage pass...");
            await ApplyBatch17LocationTempCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 18 taxi-thirdparty coverage pass...");
            await ApplyBatch18TaxiThirdPartyCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 19 contracts-docs coverage pass...");
            await ApplyBatch19ContractsDocsCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 20 legacy-ops-tail coverage pass...");
            await ApplyBatch20LegacyOpsTailCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 21 financial-ledger tail coverage pass...");
            await ApplyBatch21FinancialLedgerTailCoverageAsync(dbContext);

            Console.WriteLine("🧩 Applying Batch 22 workflow-analytics coverage pass...");
            await ApplyBatch22WorkflowAnalyticsCoverageAsync(dbContext);

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
        // Keep seeded users deterministic for demo and UI role testing.
        await dbContext.Database.ExecuteSqlRawAsync(@"
            UPDATE TS_Users SET email = 'murcus@corptech.co.za', tel_no = '+27 11 123 4567', is_deleted = 0 WHERE user_access_code = 1;
            UPDATE TS_Users SET email = 'admin@fis.local', tel_no = '+27 11 000 0001', is_deleted = 0 WHERE user_access_code = 2;
            UPDATE TS_Users SET email = 'it.support@fis.local', tel_no = '+27 11 000 0002', is_deleted = 0 WHERE user_access_code = 3;
            UPDATE TS_Users SET email = 'contracts.capturer@fis.local', tel_no = '+27 11 000 0003', is_deleted = 0 WHERE user_access_code = 4;
            UPDATE TS_Users SET email = 'contracts.reviewer@fis.local', tel_no = '+27 11 000 0004', is_deleted = 0 WHERE user_access_code = 5;
            UPDATE TS_Users SET email = 'vehicles.capturer@fis.local', tel_no = '+27 11 000 0005', is_deleted = 0 WHERE user_access_code = 6;
            UPDATE TS_Users SET email = 'workshop.manager@fis.local', tel_no = '+27 11 000 0006', is_deleted = 0 WHERE user_access_code = 7;
            UPDATE TS_Users SET email = 'finance.officer@fis.local', tel_no = '+27 11 000 0007', is_deleted = 0 WHERE user_access_code = 8;
            UPDATE TS_Users SET email = 'fleet.supervisor@fis.local', tel_no = '+27 11 000 0008', is_deleted = 0 WHERE user_access_code = 9;
            UPDATE TS_Users SET email = 'reports.analyst@fis.local', tel_no = '+27 11 000 0009', is_deleted = 0 WHERE user_access_code = 10;
            UPDATE TS_Users SET email = 'user.admin@fis.local', tel_no = '+27 11 000 0010', is_deleted = 0 WHERE user_access_code = 11;
            UPDATE TS_Users SET email = 'trip.coordinator@fis.local', tel_no = '+27 11 000 0011', is_deleted = 0 WHERE user_access_code = 12;

            SET IDENTITY_INSERT TS_Users ON;
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 1) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (1,  'murcus@corptech.co.za', '+27 11 123 4567', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 2) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (2,  'admin@fis.local', '+27 11 000 0001', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 3) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (3,  'it.support@fis.local', '+27 11 000 0002', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 4) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (4,  'contracts.capturer@fis.local', '+27 11 000 0003', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 5) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (5,  'contracts.reviewer@fis.local', '+27 11 000 0004', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 6) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (6,  'vehicles.capturer@fis.local', '+27 11 000 0005', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 7) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (7,  'workshop.manager@fis.local', '+27 11 000 0006', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 8) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (8,  'finance.officer@fis.local', '+27 11 000 0007', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 9) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (9,  'fleet.supervisor@fis.local', '+27 11 000 0008', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 10) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (10, 'reports.analyst@fis.local', '+27 11 000 0009', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 11) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (11, 'user.admin@fis.local', '+27 11 000 0010', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM TS_Users WHERE user_access_code = 12) INSERT INTO TS_Users (user_access_code, email, tel_no, date_created, is_deleted) VALUES (12, 'trip.coordinator@fis.local', '+27 11 000 0011', GETDATE(), 0);
            SET IDENTITY_INSERT TS_Users OFF;
        ");

        Console.WriteLine("  ✓ TS_Users seeded (12 demo users)");

        var seededCodes = Enumerable.Range(1, 12).ToList();
        var existingCreds = await dbContext.LegacyUserCredentials
            .Where(c => seededCodes.Contains(c.user_access_code))
            .ToListAsync();
        var existingMappings = await dbContext.EntraIdUserMappings
            .Where(m => seededCodes.Contains(m.user_access_code))
            .ToListAsync();

        if (existingCreds.Count > 0)
        {
            dbContext.LegacyUserCredentials.RemoveRange(existingCreds);
        }

        if (existingMappings.Count > 0)
        {
            dbContext.EntraIdUserMappings.RemoveRange(existingMappings);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        var now = DateTime.UtcNow;

        var jwtCredentials = seededCodes
            .Select(code => new LegacyUserCredential
            {
                user_access_code = code,
                password_hash = passwordHash,
                password_salt = "",
                created_date = now,
                last_password_change = now,
                is_active = true
            })
            .ToList();

        dbContext.LegacyUserCredentials.AddRange(jwtCredentials);

        // Keep a couple of Entra mappings available for dual-auth testing.
        dbContext.EntraIdUserMappings.AddRange(
            new EntraIdUserMapping { user_access_code = 3, entra_object_id = "simulated-guid-it-support", created_date = now },
            new EntraIdUserMapping { user_access_code = 5, entra_object_id = "simulated-guid-contract-reviewer", created_date = now });

        await dbContext.SaveChangesAsync();
        Console.WriteLine("  ✓ Legacy credentials + Entra mappings seeded.");

        // Seed User Profiles (user_access_old1) used by first-name login + bitwise authorization.
        await dbContext.Database.ExecuteSqlRawAsync(@"
            UPDATE user_access_old1 SET FirstName='Murcus', LastName='Developer', name='Murcus Developer', E_Mail='murcus@corptech.co.za', telephone='+27 11 123 4567', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=1, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=1;
            UPDATE user_access_old1 SET FirstName='Admin', LastName='User', name='Admin User', E_Mail='admin@fis.local', telephone='+27 11 000 0001', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=32767, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=2;
            UPDATE user_access_old1 SET FirstName='IT', LastName='Support', name='IT Support', E_Mail='it.support@fis.local', telephone='+27 11 000 0002', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=15, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=3;
            UPDATE user_access_old1 SET FirstName='Contract', LastName='Capturer', name='Contract Capturer', E_Mail='contracts.capturer@fis.local', telephone='+27 11 000 0003', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=11, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=4;
            UPDATE user_access_old1 SET FirstName='Contract', LastName='Reviewer', name='Contract Reviewer', E_Mail='contracts.reviewer@fis.local', telephone='+27 11 000 0004', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=8202, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=5;
            UPDATE user_access_old1 SET FirstName='Vehicle', LastName='Master', name='Vehicle Master Capturer', E_Mail='vehicles.capturer@fis.local', telephone='+27 11 000 0005', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=1, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=6;
            UPDATE user_access_old1 SET FirstName='Workshop', LastName='Manager', name='Workshop Manager', E_Mail='workshop.manager@fis.local', telephone='+27 11 000 0006', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=2, AccessLevel=296, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=7;
            UPDATE user_access_old1 SET FirstName='Finance', LastName='Officer', name='Finance Officer', E_Mail='finance.officer@fis.local', telephone='+27 11 000 0007', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=24, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=8;
            UPDATE user_access_old1 SET FirstName='Fleet', LastName='Supervisor', name='Fleet Supervisor', E_Mail='fleet.supervisor@fis.local', telephone='+27 11 000 0008', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=451, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=9;
            UPDATE user_access_old1 SET FirstName='Reports', LastName='Analyst', name='Reports Analyst', E_Mail='reports.analyst@fis.local', telephone='+27 11 000 0009', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=8, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=10;
            UPDATE user_access_old1 SET FirstName='User', LastName='Admin', name='User Admin', E_Mail='user.admin@fis.local', telephone='+27 11 000 0010', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=1, AccessLevel=12, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=11;
            UPDATE user_access_old1 SET FirstName='Trip', LastName='Coordinator', name='Trip Coordinator', E_Mail='trip.coordinator@fis.local', telephone='+27 11 000 0011', password='Password123!', user_status='Active', user_active=1, Site_code=1, Position_Code=2, AccessLevel=200, is_deleted=0, date_updated=GETDATE() WHERE user_access_code=12;

            SET IDENTITY_INSERT user_access_old1 ON;
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 1) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (1,  'Murcus', 'Developer', 'Murcus Developer', 'murcus@corptech.co.za', '+27 11 123 4567', 'Password123!', 'Active', 1, 1, 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 2) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (2,  'Admin', 'User', 'Admin User', 'admin@fis.local', '+27 11 000 0001', 'Password123!', 'Active', 1, 1, 1, 32767, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 3) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (3,  'IT', 'Support', 'IT Support', 'it.support@fis.local', '+27 11 000 0002', 'Password123!', 'Active', 1, 1, 1, 15, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 4) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (4,  'Contract', 'Capturer', 'Contract Capturer', 'contracts.capturer@fis.local', '+27 11 000 0003', 'Password123!', 'Active', 1, 1, 1, 11, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 5) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (5,  'Contract', 'Reviewer', 'Contract Reviewer', 'contracts.reviewer@fis.local', '+27 11 000 0004', 'Password123!', 'Active', 1, 1, 1, 8202, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 6) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (6,  'Vehicle', 'Master', 'Vehicle Master Capturer', 'vehicles.capturer@fis.local', '+27 11 000 0005', 'Password123!', 'Active', 1, 1, 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 7) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (7,  'Workshop', 'Manager', 'Workshop Manager', 'workshop.manager@fis.local', '+27 11 000 0006', 'Password123!', 'Active', 1, 1, 2, 296, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 8) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (8,  'Finance', 'Officer', 'Finance Officer', 'finance.officer@fis.local', '+27 11 000 0007', 'Password123!', 'Active', 1, 1, 1, 24, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 9) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (9,  'Fleet', 'Supervisor', 'Fleet Supervisor', 'fleet.supervisor@fis.local', '+27 11 000 0008', 'Password123!', 'Active', 1, 1, 1, 451, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 10) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (10, 'Reports', 'Analyst', 'Reports Analyst', 'reports.analyst@fis.local', '+27 11 000 0009', 'Password123!', 'Active', 1, 1, 1, 8, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 11) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (11, 'User', 'Admin', 'User Admin', 'user.admin@fis.local', '+27 11 000 0010', 'Password123!', 'Active', 1, 1, 1, 12, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE user_access_code = 12) INSERT INTO user_access_old1 (user_access_code, FirstName, LastName, name, E_Mail, telephone, password, user_status, user_active, Site_code, Position_Code, AccessLevel, date_created, is_deleted) VALUES (12, 'Trip', 'Coordinator', 'Trip Coordinator', 'trip.coordinator@fis.local', '+27 11 000 0011', 'Password123!', 'Active', 1, 1, 2, 200, GETDATE(), 0);
            SET IDENTITY_INSERT user_access_old1 OFF;
        ");

        Console.WriteLine("  ✓ User profiles seeded (12 role-based profiles)");
        Console.WriteLine("  📝 Demo login: FirstName='Murcus', Password='Password123!'");
        Console.WriteLine("  👑 Admin login: FirstName='Admin', Password='Password123!'");
        Console.WriteLine("  ✅ Contract reviewer login: FirstName='Contract', Password='Password123!'");
    }

    private static async Task SeedAccessLevelData(FisDbContext dbContext)
    {
        if (await dbContext.AccessLevels.AnyAsync())
        {
            Console.WriteLine("  ✓ Access levels already exist. Skipping access level seeding.");
            return;
        }

        // Seed module-based permissions using bitwise values (powers of 2)
        // Users can have multiple permissions by summing the values
        // Example: Admin = 1+2+4+8 = 15 (Vehicle, Contract, User Admin, Reports)

        await dbContext.Database.ExecuteSqlRawAsync(@"
            SET IDENTITY_INSERT AccessLevels ON;
            INSERT INTO AccessLevels (AccessLevelID, AccessLevelName, AccessLevelValue, date_created, created_by_user_code, is_deleted) VALUES
            (1, 'Vehicle Management', 1, GETDATE(), 1, 0),
            (2, 'Contract Management', 2, GETDATE(), 1, 0),
            (3, 'User Administration', 4, GETDATE(), 1, 0),
            (4, 'Reports', 8, GETDATE(), 1, 0),
            (5, 'Financial', 16, GETDATE(), 1, 0),
            (6, 'Workshop', 32, GETDATE(), 1, 0),
            (7, 'Trip Management', 64, GETDATE(), 1, 0),
            (8, 'Driver Management', 128, GETDATE(), 1, 0),
            (9, 'Maintenance', 256, GETDATE(), 1, 0),
            (10, 'Accident Management', 512, GETDATE(), 1, 0),
            (11, 'Fine Management', 1024, GETDATE(), 1, 0),
            (12, 'Fuel Card Management', 2048, GETDATE(), 1, 0),
            (13, 'Asset Verification', 4096, GETDATE(), 1, 0),
            (14, 'Workflow Management', 8192, GETDATE(), 1, 0),
            (15, 'Third Party Integration', 16384, GETDATE(), 1, 0);
            SET IDENTITY_INSERT AccessLevels OFF;
        ");

        Console.WriteLine("  ✓ Access levels seeded (15 module permissions)");
        Console.WriteLine("  📊 Bitwise Permission Examples:");
        Console.WriteLine("     Admin (All Access) = 32767 (sum of all 15 permissions)");
        Console.WriteLine("     Fleet Manager = 451 (Vehicle + Contract + Reports + Trip + Maintenance = 1+2+8+64+256+128)");
        Console.WriteLine("     Workshop Manager = 288 (Workshop + Maintenance = 32+256)");
        Console.WriteLine("     Basic User = 9 (Vehicle + Reports = 1+8)");
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

        // Seed Driver Licence Types
        if (!await dbContext.DriverLicenceTypes.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT driver_licence_types ON;
                INSERT INTO driver_licence_types (driver_licence_type_id, driver_licence_type_code, driver_licence_type_description, date_created, is_deleted) VALUES 
                (1, 'C1', 'Code 10', GETDATE(), 0),
                (2, 'EB', 'Code 08', GETDATE(), 0),
                (3, 'EC', 'Code 14', GETDATE(), 0);
                SET IDENTITY_INSERT driver_licence_types OFF;
            ");
            Console.WriteLine("  ✓ Driver Licence Types seeded.");
        }

        // Seed Trip Incident Types
        if (!await dbContext.TripIncidentTypes.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT trip_incident_types ON;
                INSERT INTO trip_incident_types (trip_incident_type_code, trip_incident_type_name, date_created, is_deleted) VALUES 
                (1, 'Accident', GETDATE(), 0),
                (2, 'Breakdown', GETDATE(), 0),
                (3, 'Theft', GETDATE(), 0),
                (4, 'None', GETDATE(), 0);
                SET IDENTITY_INSERT trip_incident_types OFF;
            ");
            Console.WriteLine("  ✓ Trip Incident Types seeded.");
        }

        // Seed Extra Codes
        if (!await dbContext.ExtraCodes.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT extra_codes ON;
                INSERT INTO extra_codes (extra_code, extra_description, category_type_code, specific, Additional, date_created, is_deleted) VALUES 
                (1, 'Radio', 1, 1, 0, GETDATE(), 0),
                (2, 'Canopy', 1, 1, 0, GETDATE(), 0),
                (3, 'Towbar', 1, 1, 0, GETDATE(), 0),
                (4, 'Aircon', 1, 1, 0, GETDATE(), 0),
                (5, 'Tracking Unit', 1, 1, 0, GETDATE(), 0);
                SET IDENTITY_INSERT extra_codes OFF;
            ");
            Console.WriteLine("  ✓ Extra Codes seeded.");
        }

        // Seed Classes (Validation Data)
        if (!await dbContext.Classes.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT class ON;
                INSERT INTO class (class_code, description, date_created, is_deleted) VALUES
                (1, 'Passenger Vehicle', GETDATE(), 0),
                (2, 'Light Commercial Vehicle', GETDATE(), 0),
                (3, 'Heavy Commercial Vehicle', GETDATE(), 0),
                (4, 'Motorcycle', GETDATE(), 0),
                (5, 'Special Purpose Vehicle', GETDATE(), 0);
                SET IDENTITY_INSERT class OFF;
            ");
            Console.WriteLine("  ✓ Vehicle Classes seeded.");
        }

        // Seed Units of Measure (Validation Data)
        if (!await dbContext.UnitsOfMeasure.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT unit_of_measure ON;
                INSERT INTO unit_of_measure (unit_of_measure_code, unit_description, unit_abbreviation, unit_category, date_created, is_deleted) VALUES
                (1, 'Kilometers', 'km', 'Distance', GETDATE(), 0),
                (2, 'Miles', 'mi', 'Distance', GETDATE(), 0),
                (3, 'Litres', 'L', 'Volume', GETDATE(), 0),
                (4, 'Gallons', 'gal', 'Volume', GETDATE(), 0),
                (5, 'Hours', 'hr', 'Time', GETDATE(), 0);
                SET IDENTITY_INSERT unit_of_measure OFF;
            ");
            Console.WriteLine("  ✓ Units of Measure seeded.");
        }

        // Seed Maintenance Triggers (Validation Data)
        if (!await dbContext.MaintenanceTriggers.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT maintenance_trigger ON;
                INSERT INTO maintenance_trigger (maint_trigger_code, description, trigger_id, date_created, is_deleted) VALUES
                (1, 'Mileage Based', 'KM', GETDATE(), 0),
                (2, 'Time Based', 'TIME', GETDATE(), 0),
                (3, 'Hours Based', 'HOURS', GETDATE(), 0),
                (4, 'Condition Based', 'COND', GETDATE(), 0);
                SET IDENTITY_INSERT maintenance_trigger OFF;
            ");
            Console.WriteLine("  ✓ Maintenance Triggers seeded.");
        }

        // Seed License Fees (Validation Data)
        if (!await dbContext.LicenseFees.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT licence_fee ON;
                INSERT INTO licence_fee (licence_fee_code, licence_description, licence_fee, date_created, is_deleted) VALUES
                (1, 'Passenger Vehicle Annual License', 680.00, GETDATE(), 0),
                (2, 'LDV Annual License', 940.00, GETDATE(), 0),
                (3, 'Truck Annual License', 1850.00, GETDATE(), 0),
                (4, 'Motorcycle Annual License', 320.00, GETDATE(), 0);
                SET IDENTITY_INSERT licence_fee OFF;
            ");
            Console.WriteLine("  ✓ License Fees seeded.");
        }

        // Seed Loss Types (Validation Data)
        if (!await dbContext.LossTypes.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Loss_type ON;
                INSERT INTO Loss_type (loss_type_code, loss_description, date_created, is_deleted) VALUES
                (1, 'Theft', GETDATE(), 0),
                (2, 'Accident Write-Off', GETDATE(), 0),
                (3, 'Fire Damage', GETDATE(), 0),
                (4, 'Flood Damage', GETDATE(), 0),
                (5, 'Hijacking', GETDATE(), 0);
                SET IDENTITY_INSERT Loss_type OFF;
            ");
            Console.WriteLine("  ✓ Loss Types seeded.");
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedOperationsExtendedData(FisDbContext dbContext)
    {
        Console.WriteLine("  ├── Seeding Extended Operations (Collection, Clearance, Auction, Loss, CallCentre, Extras)...");

        // 1. Collections
        if (!await dbContext.Collections.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    SET IDENTITY_INSERT Collection ON;
                    INSERT INTO Collection (Collection_code, Sessionid, vmf_code, site_code, fleet_number, date_created, is_deleted) VALUES 
                    (1, 'SES-001', 1, 1, 'FLT-001', GETDATE(), 0),
                    (2, 'SES-002', 2, 2, 'FLT-002', GETDATE(), 0);
                    SET IDENTITY_INSERT Collection OFF;
                ");
                Console.WriteLine("  ✓ Collections seeded.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: Collection seed failed: {ex.Message}");
            }
        }

        // 2. Clearances
        if (!await dbContext.Clearances.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    SET IDENTITY_INSERT clearance ON;
                    INSERT INTO clearance (clearance_code, vmf_code, clearance_number, Clearance_date, Merchant_code, Clearance_amount, clearance_comment, clearance_kilo, date_created, is_deleted) VALUES 
                    (1, 3, 1001, DATEADD(month, -1, GETDATE()), 1, 450.00, 'Cleared for disposal', 85000, GETDATE(), 0),
                    (2, 5, 1002, DATEADD(month, -2, GETDATE()), 2, 600.00, 'Cleared for auction', 120000, GETDATE(), 0);
                    SET IDENTITY_INSERT clearance OFF;
                ");
                Console.WriteLine("  ✓ Clearances seeded.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: Clearance seed failed: {ex.Message}");
            }
        }

        // 3. Auctions
        if (!await dbContext.Auctions.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    SET IDENTITY_INSERT auction ON;
                    INSERT INTO auction (auction_code, vmf_code, auction_number, camp, lot, auction_garage, auth_number, auth_date, auction_km, garage_owner, reason_sold, estimate_amount, reserve_amount, sold_id, remark, date_created, is_deleted) VALUES 
                    (1, 7, 'AUC-2023-001', 'Bloemfontein', 101, 1, 'AUTH-001', DATEADD(month, -1, GETDATE()), 150000, 'Auto Auctions', 'High Mileage', 120000.00, 100000.00, 'SOLD', 'Sold above reserve', GETDATE(), 0),
                    (2, 5, 'AUC-2023-002', 'JHB South', 102, 1, 'AUTH-002', DATEADD(month, -2, GETDATE()), 120000, 'JHB Auctions', 'Redundant', 90000.00, 80000.00, 'UNSOLD', 'Did not reach reserve', GETDATE(), 0);
                    SET IDENTITY_INSERT auction OFF;
                ");
                Console.WriteLine("  ✓ Auctions seeded.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: Auction seed failed: {ex.Message}");
            }
        }

        // 4. Losses (Theft/Write-off)
        if (!await dbContext.Losses.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    SET IDENTITY_INSERT losses ON;
                    INSERT INTO losses (loss_code, vmf_code, loss_date, loss_reference, loss_type_code, site_code, dept_contact, loss_amount, dept_claim, sapd, inspector, case_number, date_created, is_deleted) VALUES 
                    (1, 8, DATEADD(month, -3, GETDATE()), 'LOSS-001', 3, 2, 'Mr. Smith', 350000.00, 320000.00, 'JHB Central', 'Insp. Gadget', 'CAS-123/10/2023', GETDATE(), 0),
                    (2, 6, DATEADD(month, -1, GETDATE()), 'LOSS-002', 1, 1, 'Mrs. Jones', 50000.00, 45000.00, 'Sandton', 'Insp. Clouseau', 'CAS-456/11/2023', GETDATE(), 0);
                    SET IDENTITY_INSERT losses OFF;
                ");
                Console.WriteLine("  ✓ Losses seeded.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: Loss seed failed: {ex.Message}");
            }
        }

        // 5. Call Centre
        if (!await dbContext.CallCentres.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(@"
                    SET IDENTITY_INSERT Call_centre ON;
                    INSERT INTO Call_centre (Call_centre_code, vmf_code, Call_time, Call_date, Capture_name, User_access_code, Caller_name, Driver_name, Driver_persalno, Driver_Licno, GG_number, Driver_base_station, Driver_Site, Driver_tel, Driver_cell, date_created, is_deleted) VALUES 
                    (1, 1, GETDATE(), GETDATE(), 'Operator A', 1, 'John Doe', 'John Doe', 'PERS-001', 'LIC-001', 'GG-001', 'Midrand', 1, '011-123-4567', '082-123-4567', GETDATE(), 0),
                    (2, 2, DATEADD(hour, -2, GETDATE()), GETDATE(), 'Operator B', 2, 'Jane Smith', 'Jane Smith', 'PERS-002', 'LIC-002', 'GG-002', 'Cape Town', 2, '021-123-4567', '083-123-4567', GETDATE(), 0);
                    SET IDENTITY_INSERT Call_centre OFF;
                ");
                Console.WriteLine("  ✓ Call Centre logs seeded.");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: CallCentre seed failed: {ex.Message}");
            }
        }

        // 6. Extras
        if (!await dbContext.Extras.AnyAsync())
        {
            // Note: extras_code is short, assuming not identity by default on legacy but let's check. 
            // Previous seeding for short PKs like cost_category needed explicit insert or identity insert depending on config.
            // Let's try explicit first, if fail then identity. 
            // Actually, for consistency with other legacy tables, assuming Identity Insert ON is safest if it's an IDENTITY column.
            // If it's NOT identity, SET IDENTITY_INSERT ON throws error.
            // Let's try standard insert first for extras (often manual).
            try
            {
                 // Try standard insert first
                 await dbContext.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO extras (extras_code, vmf_code, extra_code, quantity, amount, serial_number, date_created, is_deleted) VALUES 
                    (1, 1, 1, 1, 2500.00, 'RAD-001', GETDATE(), 0),
                    (2, 1, 2, 1, 15000.00, 'CAN-001', GETDATE(), 0),
                    (3, 2, 3, 1, 3500.00, 'TOW-001', GETDATE(), 0);
                ");
                 Console.WriteLine("  ✓ Extras seeded.");
            }
            catch
            {
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(@"
                        SET IDENTITY_INSERT extras ON;
                        INSERT INTO extras (extras_code, vmf_code, extra_code, quantity, amount, serial_number, date_created, is_deleted) VALUES 
                        (1, 1, 1, 1, 2500.00, 'RAD-001', GETDATE(), 0),
                        (2, 1, 2, 1, 15000.00, 'CAN-001', GETDATE(), 0),
                        (3, 2, 3, 1, 3500.00, 'TOW-001', GETDATE(), 0);
                        SET IDENTITY_INSERT extras OFF;
                    ");
                     Console.WriteLine("  ✓ Extras seeded (with IDENTITY_INSERT).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ! Warning: Extras seed failed: {ex.Message}");
                }
            }
        }
    }

    private static async Task SeedOrganizationData(FisDbContext dbContext)
    {
        // South African Government Departments — FIS manages government fleet
        if (!await dbContext.Departments.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT department ON;
                INSERT INTO department
                    (department_code, description, department_abbr, company_code,
                     dept_active, Service_Years, Overhead_Percentage, Service_Kilometres,
                     date_created, is_deleted)
                VALUES
                ( 1, 'The Presidency of South Africa',                     'PRES',  1, 1, 5, 10.0, 15000, GETDATE(), 0),
                ( 2, 'National Treasury',                                   'NT',    1, 1, 5, 10.0, 15000, GETDATE(), 0),
                ( 3, 'Department of Tourism',                               'DT',    1, 1, 5, 10.0, 15000, GETDATE(), 0),
                ( 4, 'Department of Trade, Industry and Competition',       'DTIC',  1, 1, 5, 10.0, 20000, GETDATE(), 0),
                ( 5, 'Department of Communications and Digital Technologies','DCDT', 1, 1, 5, 10.0, 20000, GETDATE(), 0),
                ( 6, 'Department of Mineral Resources and Energy',          'DMRE',  1, 1, 5, 10.0, 25000, GETDATE(), 0),
                ( 7, 'Department of Forestry, Fisheries and the Environment','DFFE', 1, 1, 5, 10.0, 20000, GETDATE(), 0),
                ( 8, 'Department of Agriculture, Land Reform and Rural Development','DALRRD',1,1,5,10.0,25000,GETDATE(),0),
                ( 9, 'Department of Transport',                             'DOT',   1, 1, 5, 10.0, 30000, GETDATE(), 0),
                (10, 'Department of Health',                                'DOH',   1, 1, 5, 10.0, 20000, GETDATE(), 0),
                (11, 'Department of Higher Education and Training',         'DHET',  1, 1, 5, 10.0, 15000, GETDATE(), 0),
                (12, 'Department of Employment and Labour',                 'DEL',   1, 1, 5, 10.0, 15000, GETDATE(), 0),
                (13, 'Department of Justice and Constitutional Development', 'DOJ',  1, 1, 5, 10.0, 20000, GETDATE(), 0),
                (14, 'Department of Social Development',                    'DSD',   1, 1, 5, 10.0, 15000, GETDATE(), 0),
                (15, 'Department of Human Settlements',                     'DHS',   1, 1, 5, 10.0, 15000, GETDATE(), 0),
                (16, 'Department of Sport, Arts and Culture',               'DSAC',  1, 1, 5, 10.0, 15000, GETDATE(), 0);
                SET IDENTITY_INSERT department OFF;
            ");
            Console.WriteLine("  ✓ SA Government Departments seeded (16).");
        }

        // Entities / Sites under each department.
        // IMPORTANT: Sites 1 and 2 are placed under DIFFERENT departments (1 and 2) so
        // that demo vehicles (location_code alternates 1/2) produce spread across depts
        // in financial reports.
        if (!await dbContext.Sites.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT site ON;
                INSERT INTO site (Site_code, description, Depatrment_code, site_active, date_created, is_deleted) VALUES
                -- Dept 1: The Presidency
                ( 1, 'National Planning Commission',                                        1, 1, GETDATE(), 0),
                ( 3, 'Presidential Climate Commission',                                     1, 1, GETDATE(), 0),
                ( 4, 'Government Communication and Information System (GCIS)',              1, 1, GETDATE(), 0),
                -- Dept 2: National Treasury  (site 2 intentionally in NT for billing spread)
                ( 2, 'Accounting Standards Board',                                          2, 1, GETDATE(), 0),
                ( 5, 'Government Technical Advisory Centre',                                2, 1, GETDATE(), 0),
                ( 6, 'Financial Intelligence Centre',                                       2, 1, GETDATE(), 0),
                ( 7, 'Cooperative Banks Development Agency',                                2, 1, GETDATE(), 0),
                -- Dept 3: Tourism
                ( 8, 'South African Tourism',                                               3, 1, GETDATE(), 0),
                ( 9, 'Tourism Transformation Council',                                      3, 1, GETDATE(), 0),
                -- Dept 4: Trade, Industry and Competition
                (10, 'Companies and Intellectual Property Commission',                      4, 1, GETDATE(), 0),
                (11, 'Companies Tribunal',                                                  4, 1, GETDATE(), 0),
                (12, 'Competition Commission South Africa',                                 4, 1, GETDATE(), 0),
                (13, 'Competition Tribunal South Africa',                                   4, 1, GETDATE(), 0),
                (14, 'National Consumer Commission',                                        4, 1, GETDATE(), 0),
                (15, 'National Consumer Tribunal',                                          4, 1, GETDATE(), 0),
                (16, 'National Gambling Board',                                             4, 1, GETDATE(), 0),
                (17, 'Industrial Development Corporation',                                  4, 1, GETDATE(), 0),
                (18, 'National Empowerment Fund',                                           4, 1, GETDATE(), 0),
                -- Dept 5: Communications and Digital Technologies
                (19, 'State Information Technology Agency',                                 5, 1, GETDATE(), 0),
                (20, 'Independent Communications Authority of South Africa',                5, 1, GETDATE(), 0),
                (21, 'South African Broadcasting Corporation',                              5, 1, GETDATE(), 0),
                (22, 'South African Post Office',                                           5, 1, GETDATE(), 0),
                (23, '.ZA Domain Name Authority',                                           5, 1, GETDATE(), 0),
                (24, 'Broadband Infraco',                                                   5, 1, GETDATE(), 0),
                -- Dept 6: Mineral Resources and Energy
                (25, 'Council for Geoscience',                                              6, 1, GETDATE(), 0),
                (26, 'National Energy Regulator of South Africa',                           6, 1, GETDATE(), 0),
                (27, 'Mine Health and Safety Council',                                      6, 1, GETDATE(), 0),
                (28, 'South African Diamond and Precious Metals Regulator',                 6, 1, GETDATE(), 0),
                (29, 'Mintek',                                                              6, 1, GETDATE(), 0),
                -- Dept 7: Forestry, Fisheries and the Environment
                (30, 'South African National Parks',                                        7, 1, GETDATE(), 0),
                (31, 'South African Weather Service',                                       7, 1, GETDATE(), 0),
                (32, 'iSimangaliso Wetland Park Authority',                                 7, 1, GETDATE(), 0),
                (33, 'South African National Biodiversity Institute',                       7, 1, GETDATE(), 0),
                -- Dept 8: Agriculture, Land Reform and Rural Development
                (34, 'Agricultural Research Council',                                       8, 1, GETDATE(), 0),
                (35, 'Ingonyama Trust Board',                                               8, 1, GETDATE(), 0),
                (36, 'Onderstepoort Biological Products',                                   8, 1, GETDATE(), 0),
                (37, 'Perishable Products Export Control Board',                            8, 1, GETDATE(), 0),
                -- Dept 9: Transport
                (38, 'South African National Roads Agency',                                 9, 1, GETDATE(), 0),
                (39, 'Road Traffic Management Corporation',                                 9, 1, GETDATE(), 0),
                (40, 'South African Civil Aviation Authority',                              9, 1, GETDATE(), 0),
                (41, 'Railway Safety Regulator',                                            9, 1, GETDATE(), 0),
                (42, 'Passenger Rail Agency of South Africa',                               9, 1, GETDATE(), 0),
                (43, 'Airports Company South Africa',                                       9, 1, GETDATE(), 0),
                -- Dept 10: Health
                (44, 'South African Health Products Regulatory Authority',                 10, 1, GETDATE(), 0),
                (45, 'National Health Laboratory Service',                                 10, 1, GETDATE(), 0),
                (46, 'Council for Medical Schemes',                                        10, 1, GETDATE(), 0),
                -- Dept 11: Higher Education and Training
                (47, 'National Student Financial Aid Scheme',                              11, 1, GETDATE(), 0),
                (48, 'Quality Council for Trades and Occupations',                         11, 1, GETDATE(), 0),
                (49, 'Council on Higher Education',                                        11, 1, GETDATE(), 0),
                (50, '21 Sector Education and Training Authorities (SETAs)',               11, 1, GETDATE(), 0),
                -- Dept 12: Employment and Labour
                (51, 'Unemployment Insurance Fund',                                        12, 1, GETDATE(), 0),
                (52, 'Compensation Fund',                                                  12, 1, GETDATE(), 0),
                (53, 'Commission for Conciliation Mediation and Arbitration',              12, 1, GETDATE(), 0),
                -- Dept 13: Justice and Constitutional Development
                (54, 'National Prosecuting Authority',                                     13, 1, GETDATE(), 0),
                (55, 'Legal Aid South Africa',                                             13, 1, GETDATE(), 0),
                (56, 'Special Investigating Unit',                                         13, 1, GETDATE(), 0),
                (57, 'Public Protector South Africa',                                      13, 1, GETDATE(), 0),
                -- Dept 14: Social Development
                (58, 'South African Social Security Agency',                               14, 1, GETDATE(), 0),
                (59, 'National Development Agency',                                        14, 1, GETDATE(), 0),
                -- Dept 15: Human Settlements
                (60, 'National Housing Finance Corporation',                               15, 1, GETDATE(), 0),
                (61, 'Social Housing Regulatory Authority',                                15, 1, GETDATE(), 0),
                (62, 'Housing Development Agency',                                         15, 1, GETDATE(), 0),
                -- Dept 16: Sport, Arts and Culture
                (63, 'National Arts Council',                                              16, 1, GETDATE(), 0),
                (64, 'National Heritage Council',                                          16, 1, GETDATE(), 0),
                (65, 'South African Library for the Blind',                                16, 1, GETDATE(), 0),
                (66, 'Freedom Park',                                                       16, 1, GETDATE(), 0);
                SET IDENTITY_INSERT site OFF;
            ");
            Console.WriteLine("  ✓ SA Government Entity Sites seeded (66 across 16 departments).");
        }
    }

    private static async Task SeedVehicleData(FisDbContext dbContext)
    {
        // Makes
        if (!await dbContext.Makes.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT make ON;
                INSERT INTO make (make_code, make_description, date_created, is_deleted) VALUES 
                (1, 'Toyota', GETDATE(), 0),
                (2, 'Ford', GETDATE(), 0),
                (3, 'Volkswagen', GETDATE(), 0);
                SET IDENTITY_INSERT make OFF;
            ");
             Console.WriteLine("  ✓ Makes seeded.");
        }

        // Models
        if (!await dbContext.Models.AnyAsync())
        {
             // Note: Providing default values for required legacy columns
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT model ON;
                INSERT INTO model (model_code, model_description, make_code, type_code, fuel_type_code, unit_of_measure_code, licence_code, class_code, date_created, is_deleted) VALUES 
                (1, 'Hilux', 1, 1, 2, 1, 1, 1, GETDATE(), 0),
                (2, 'Corolla', 1, 2, 1, 1, 1, 1, GETDATE(), 0),
                (3, 'Ranger', 2, 1, 2, 1, 1, 1, GETDATE(), 0),
                (4, 'Polo', 3, 2, 1, 1, 1, 1, GETDATE(), 0);
                SET IDENTITY_INSERT model OFF;
            ");
             Console.WriteLine("  ✓ Models seeded.");
        }

        // Vehicle Types
        if (!await dbContext.VehicleTypes.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT type ON;
                INSERT INTO type (type_code, type_description, date_created, is_deleted) VALUES 
                (1, 'LDV', GETDATE(), 0),
                (2, 'Sedan', GETDATE(), 0),
                (3, 'Truck', GETDATE(), 0);
                SET IDENTITY_INSERT type OFF;
            ");
             Console.WriteLine("  ✓ Vehicle Types seeded.");
        }

        // Fuel Types
        if (!await dbContext.FuelTypes.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT fuel_type ON;
                INSERT INTO fuel_type (fuel_type_code, fuel_description, date_created, is_deleted) VALUES 
                (1, 'Petrol', GETDATE(), 0),
                (2, 'Diesel', GETDATE(), 0);
                SET IDENTITY_INSERT fuel_type OFF;
            ");
             Console.WriteLine("  ✓ Fuel Types seeded.");
        }

        // Vehicles
        // Check if we have the full set (checking count < 10 to trigger update)
        if (await dbContext.Vehicles.CountAsync() < 10)
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT vehicle_master ON;

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (1, 'ABC 123 GP', 1, 1, 'White', 'ENG123456', 'CHS123456', 2023, 15000, 0, DATEADD(year, -1, GETDATE()), 1, 1, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 2)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (2, 'XYZ 987 GP', 3, 1, 'Silver', 'ENG987654', 'CHS987654', 2022, 45000, 1000, DATEADD(year, -2, GETDATE()), 1, 2, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 3)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (3, 'CA 123-456', 2, 2, 'Blue', 'ENG111222', 'CHS111222', 2021, 85000, 500, DATEADD(year, -3, GETDATE()), 1, 2, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 4)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (4, 'ND 555-666', 4, 2, 'Red', 'ENG333444', 'CHS333444', 2024, 5000, 0, DATEADD(month, -6, GETDATE()), 1, 1, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 5)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (5, 'GP 999-000', 1, 1, 'White', 'ENG555666', 'CHS555666', 2020, 120000, 2000, DATEADD(year, -4, GETDATE()), 2, 1, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 6)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (6, 'MP 777-888', 3, 1, 'Grey', 'ENG777888', 'CHS777888', 2023, 25000, 100, DATEADD(year, -1, GETDATE()), 1, 2, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 7)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (7, 'FS 222-333', 2, 2, 'Black', 'ENG999000', 'CHS999000', 2019, 150000, 5000, DATEADD(year, -5, GETDATE()), 3, 1, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 8)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (8, 'L 444-555', 4, 2, 'White', 'ENG000111', 'CHS000111', 2022, 35000, 0, DATEADD(year, -2, GETDATE()), 1, 2, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 9)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (9, 'NW 666-777', 1, 1, 'Silver', 'ENG222333', 'CHS222333', 2024, 2000, 0, DATEADD(month, -2, GETDATE()), 1, 1, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 10)
                INSERT INTO vehicle_master (vmf_code, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, date_created, is_deleted) VALUES 
                (10, 'EC 888-999', 3, 1, 'Blue', 'ENG444555', 'CHS444555', 2021, 65000, 1000, DATEADD(year, -3, GETDATE()), 4, 2, GETDATE(), 0);

                SET IDENTITY_INSERT vehicle_master OFF;
            ");
            Console.WriteLine("  ✓ Vehicles seeded (ensured 10 vehicles).");
        }
    }

    private static async Task SeedContractData(FisDbContext dbContext)
    {
        // Seed 10 base contracts for legacy vehicles 1-10
        if (await dbContext.Contracts.CountAsync() < 10)
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT contract ON;

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 1)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (1, 1, 1, 'H', DATEADD(month, -6, GETDATE()), DATEADD(month, -6, GETDATE()), 1000, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 2)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (2, 2, 2, 'H', DATEADD(month, -12, GETDATE()), DATEADD(month, -12, GETDATE()), 500, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 3)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (3, 3, 2, 'H', DATEADD(year, -2, GETDATE()), DATEADD(year, -2, GETDATE()), 500, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 4)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (4, 4, 1, 'H', DATEADD(month, -5, GETDATE()), DATEADD(month, -5, GETDATE()), 0, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 5)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (5, 5, 1, 'H', DATEADD(year, -3, GETDATE()), DATEADD(year, -3, GETDATE()), 2000, 'N', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 6)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (6, 6, 2, 'H', DATEADD(month, -10, GETDATE()), DATEADD(month, -10, GETDATE()), 100, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 7)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (7, 7, 1, 'H', DATEADD(year, -4, GETDATE()), DATEADD(year, -4, GETDATE()), 5000, 'N', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 8)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (8, 8, 2, 'H', DATEADD(year, -1, GETDATE()), DATEADD(year, -1, GETDATE()), 0, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (9, 9, 1, 'H', DATEADD(month, -1, GETDATE()), DATEADD(month, -1, GETDATE()), 0, 'Y', 0, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 10)
                INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, date_created, is_deleted) VALUES
                (10, 10, 2, 'H', DATEADD(year, -2, GETDATE()), DATEADD(year, -2, GETDATE()), 1000, 'N', 0, GETDATE(), 0);

                SET IDENTITY_INSERT contract OFF;
            ");
            Console.WriteLine("  ✓ Base contracts seeded (10).");
        }

        // Seed 50 active demo contracts for vehicles 1001-1050 (mixed H/R/D types, backdated 1-24 months)
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @vmf     INT;
            DECLARE @ctype   CHAR(1);
            DECLARE @mback   INT;
            DECLARE @site    SMALLINT;
            DECLARE @s_odo   INT;
            DECLARE @mthly   INT;

            SET @vmf = 1001;
            WHILE @vmf <= 1050
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM contract
                    WHERE vmf_code = @vmf AND still_current = 'Y' AND is_deleted = 0
                )
                BEGIN
                    SET @ctype  = CASE WHEN @vmf <= 1020 THEN 'H'
                                       WHEN @vmf <= 1035 THEN 'R'
                                       ELSE 'D' END;
                    SET @mback  = ((@vmf - 1001) % 24) + 1;
                    SET @site   = CASE WHEN @vmf % 2 = 0 THEN 2 ELSE 1 END;
                    SET @s_odo  = (@vmf - 1001) * 500;
                    SET @mthly  = 1500 + ((@vmf - 1001) % 10) * 200;

                    INSERT INTO contract
                    (vmf_code, site_code, contract_type,
                     start_date, start_time, start_odometer, monthly_km,
                     still_current, locked_for_transfer,
                     date_created, is_deleted)
                    VALUES
                    (@vmf, @site, @ctype,
                     DATEADD(month, -@mback, GETDATE()),
                     DATEADD(month, -@mback, GETDATE()),
                     @s_odo, @mthly,
                     'Y', 0, GETDATE(), 0);
                END

                SET @vmf = @vmf + 1;
            END;
        ");

        var activeCount = await dbContext.Database.SqlQueryRaw<int>(
            "SELECT COUNT(1) AS [Value] FROM contract WHERE still_current = 'Y' AND is_deleted = 0"
        ).FirstOrDefaultAsync();
        Console.WriteLine($"  ✓ Demo contracts seeded — {activeCount} active contracts total.");
    }

    private static async Task SeedLeaseTariffData(FisDbContext dbContext)
    {
        // Create one active LeaseTariff record per demo vehicle that has an active contract.
        // fixed_tariff cycles between 3000–6500 to give varied billing amounts for reports.
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @vmf     INT;
            DECLARE @tariff  DECIMAL(18,2);

            SET @vmf = 1001;
            WHILE @vmf <= 1050
            BEGIN
                IF EXISTS (SELECT 1 FROM contract WHERE vmf_code = @vmf AND still_current = 'Y' AND is_deleted = 0)
                   AND NOT EXISTS (SELECT 1 FROM LeaseTariff WHERE vmf_code = @vmf AND active = 1 AND is_deleted = 0)
                BEGIN
                    SET @tariff = CAST(3000 + ((@vmf - 1001) % 18) * 200 AS DECIMAL(18,2));

                    INSERT INTO LeaseTariff
                    (vmf_code, start_date, end_date, fixed_tariff, active, date_created, is_deleted)
                    VALUES
                    (@vmf,
                     DATEADD(year, -3, GETDATE()),
                     DATEADD(year,  2, GETDATE()),
                     @tariff,
                     1,
                     GETDATE(),
                     0);
                END

                SET @vmf = @vmf + 1;
            END;
        ");

        var tariffCount = await dbContext.LeaseTariffs.CountAsync(t => t.active && !t.is_deleted);
        Console.WriteLine($"  ✓ Lease tariffs seeded ({tariffCount} active records for demo vehicles).");
    }

    private static async Task SeedFuelCardData(FisDbContext dbContext)
    {
        if (!await dbContext.FuelCards.AnyAsync())
        {
            // Seed Fuel Cards linked to Vehicles (1, 2) and Sites (1, 2)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Fuel_card ON;
                INSERT INTO Fuel_card (Fuel_card_code, vmf_code, card_number, PAN_number, Petrecsite, Status_date, date_created, is_deleted) VALUES 
                (1, 1, '123456789012345', 'PAN123456', 1, GETDATE(), GETDATE(), 0),
                (2, 2, '987654321098765', 'PAN987654', 2, GETDATE(), GETDATE(), 0);
                SET IDENTITY_INSERT Fuel_card OFF;
            ");
            Console.WriteLine("  ✓ Fuel Cards seeded.");
        }
    }

    private static async Task SeedTripData(FisDbContext dbContext)
    {
        if (!await dbContext.Trips.AnyAsync())
        {
            // Seed Trips linked to Contracts (1, 2)
            // Trip 1 for Contract 1 (Vehicle 1)
            // Trip 2 for Contract 2 (Vehicle 2)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT trip_authorities ON;
                INSERT INTO trip_authorities (
                    trip_authority_code, contract_code, trip_request_number, issue_date, 
                    trip_type_code, trip_incident_type_code, user_access_code, 
                    locked_for_transfer, Trip_Is_Monthly, date_created, is_deleted
                ) VALUES 
                (1, 1, 'REQ001', GETDATE(), 1, 4, 1, 0, 0, GETDATE(), 0),
                (2, 2, 'REQ002', GETDATE(), 1, 4, 1, 0, 1, GETDATE(), 0);
                SET IDENTITY_INSERT trip_authorities OFF;
            ");
            Console.WriteLine("  ✓ Trips seeded.");
        }
    }

    private static async Task SeedDriverData(FisDbContext dbContext)
    {
        // Seed trip_driver records for trip authorities
        if (!await dbContext.TripDrivers.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT trip_driver ON;
                INSERT INTO trip_driver (
                    trip_driver_code, trip_authority_code, trip_driver_name, trip_driver_id,
                    trip_driver_primary, driver_licence_type_id, driver_active, driver_hasPDP, date_created, is_deleted
                ) VALUES
                (1, 1, 'Sipho Dlamini',  '8001015009087', 1, 2, 1, 1, GETDATE(), 0),
                (2, 2, 'Fatima Motaung', '8505050050080', 1, 1, 1, 0, GETDATE(), 0);
                SET IDENTITY_INSERT trip_driver OFF;
            ");
            Console.WriteLine("  ✓ Trip drivers seeded.");
        }

        // Seed site_drivers — the actual driver roster per site
        if (!await dbContext.Drivers.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                -- driver_licence_type_id 1 and 2 must exist (seeded in reference data)
                -- SA IDs are realistic test values (format: YYMMDD SSSS C Z)
                INSERT INTO site_drivers
                (site_code, driver_licence_type_id,
                 driver_surname, driver_firstname,
                 driver_SA_id, driver_persalnumber, driver_contractnumber,
                 driver_licence_number,
                 driver_licence_issuedate, driver_licence_lastVerifiedDate,
                 driver_hasPDP, driver_PDP_ExpiryDate, driver_licence_ExpiryDate,
                 driver_active, date_created, is_deleted)
                VALUES
                -- Site 1 — 15 drivers
                (1, 1, 'Dlamini',   'Sipho',      '8001015009087', 'P001234', 'C001001', '12345678ABCD', '2015-03-10', DATEADD(month,-3,GETDATE()), 1, DATEADD(year,1,GETDATE()), '2030-03-10', 1, GETDATE(), 0),
                (1, 2, 'Nkosi',     'Thabo',       '7507105800082', 'P001235', 'C001002', '23456789BCDE', '2012-07-20', DATEADD(month,-6,GETDATE()), 0, NULL,                      '2027-07-20', 1, GETDATE(), 0),
                (1, 1, 'Motaung',   'Fatima',      '9202140800087', 'P001236', 'C001003', '34567890CDEF', '2018-02-15', DATEADD(month,-2,GETDATE()), 1, DATEADD(year,2,GETDATE()), '2028-02-15', 1, GETDATE(), 0),
                (1, 2, 'Zulu',      'Bongani',     '8806056800083', 'P001237', 'C001004', '45678901DEFG', '2014-06-05', DATEADD(month,-4,GETDATE()), 0, NULL,                      '2029-06-05', 1, GETDATE(), 0),
                (1, 1, 'Khumalo',   'Zanele',      '9510280800081', 'P001238', 'C001005', '56789012EFGH', '2020-10-28', DATEADD(month,-1,GETDATE()), 0, NULL,                      '2030-10-28', 1, GETDATE(), 0),
                (1, 2, 'Mokoena',   'Lerato',      '8304135800086', 'P001239', 'C001006', '67890123FGHI', '2013-04-13', DATEADD(month,-8,GETDATE()), 1, DATEADD(month,6,GETDATE()), '2026-04-13', 1, GETDATE(), 0),
                (1, 1, 'Sithole',   'Mandla',      '7912115800089', 'P001240', 'C001007', '78901234GHIJ', '2010-12-11', DATEADD(month,-12,GETDATE()),0, NULL,                      '2025-12-11', 1, GETDATE(), 0),
                (1, 2, 'Ngcobo',    'Nompumelelo', '9108296800080', 'P001241', 'C001008', '89012345HIJK', '2017-08-29', DATEADD(month,-5,GETDATE()), 0, NULL,                      '2027-08-29', 1, GETDATE(), 0),
                (1, 1, 'Mahlangu',  'Sifiso',      '8703025800084', 'P001242', 'C001009', '90123456IJKL', '2016-03-02', DATEADD(month,-9,GETDATE()), 1, DATEADD(year,3,GETDATE()), '2026-03-02', 1, GETDATE(), 0),
                (1, 2, 'Cele',      'Nokuthula',   '9406194800082', 'P001243', 'C001010', '01234567JKLM', '2019-06-19', DATEADD(month,-3,GETDATE()), 0, NULL,                      '2029-06-19', 1, GETDATE(), 0),
                (1, 1, 'Mthembu',   'Lungelo',     '8512046800085', 'P001244', 'C001011', '12345678KLMN', '2015-12-04', DATEADD(month,-6,GETDATE()), 0, NULL,                      '2028-12-04', 1, GETDATE(), 0),
                (1, 2, 'Buthelezi', 'Thandeka',    '7101015800088', 'P001245', 'C001012', '23456789LMNO', '2008-01-01', DATEADD(month,-18,GETDATE()),1, DATEADD(year,1,GETDATE()), '2026-01-01', 1, GETDATE(), 0),
                (1, 1, 'Ntuli',     'Sandile',     '9301015800081', 'P001246', 'C001013', '34567890MNOP', '2018-01-01', DATEADD(month,-2,GETDATE()), 0, NULL,                      '2028-01-01', 1, GETDATE(), 0),
                (1, 2, 'Shabalala', 'Phiwayinkosi','8209036800087', 'P001247', 'C001014', '45678901NOPQ', '2011-09-03', DATEADD(month,-10,GETDATE()),0, NULL,                      '2026-09-03', 1, GETDATE(), 0),
                (1, 1, 'Mnguni',    'Lindiwe',     '9704110800083', 'P001248', 'C001015', '56789012OPQR', '2022-04-11', DATEADD(month,-1,GETDATE()), 0, NULL,                      '2032-04-11', 1, GETDATE(), 0),
                -- Site 2 — 15 drivers
                (2, 2, 'van der Berg','Pieter',    '7608165800082', 'P002001', 'C002001', '67890123PQRS', '2009-08-16', DATEADD(month,-14,GETDATE()),1, DATEADD(month,8,GETDATE()), '2026-08-16', 1, GETDATE(), 0),
                (2, 1, 'Botha',     'Anri',        '8902270800085', 'P002002', 'C002002', '78901234QRST', '2016-02-27', DATEADD(month,-4,GETDATE()), 0, NULL,                      '2028-02-27', 1, GETDATE(), 0),
                (2, 2, 'Swanepoel', 'Hennie',      '7403265800089', 'P002003', 'C002003', '89012345RSTU', '2007-03-26', DATEADD(month,-20,GETDATE()),1, DATEADD(year,2,GETDATE()), '2025-03-26', 1, GETDATE(), 0),
                (2, 1, 'Joubert',   'Marinda',     '9105120800081', 'P002004', 'C002004', '90123456STUV', '2018-05-12', DATEADD(month,-6,GETDATE()), 0, NULL,                      '2028-05-12', 1, GETDATE(), 0),
                (2, 2, 'Patel',     'Rajan',       '8001185800086', 'P002005', 'C002005', '01234567TUVW', '2013-01-18', DATEADD(month,-8,GETDATE()), 1, DATEADD(year,1,GETDATE()), '2027-01-18', 1, GETDATE(), 0),
                (2, 1, 'Singh',     'Priya',       '9306264800083', 'P002006', 'C002006', '12345678UVWX', '2019-06-26', DATEADD(month,-3,GETDATE()), 0, NULL,                      '2029-06-26', 1, GETDATE(), 0),
                (2, 2, 'Adams',     'Yusuf',       '7805125800087', 'P002007', 'C002007', '23456789VWXY', '2010-05-12', DATEADD(month,-11,GETDATE()),1, DATEADD(year,2,GETDATE()), '2026-05-12', 1, GETDATE(), 0),
                (2, 1, 'Hendricks', 'Megan',       '9010254800082', 'P002008', 'C002008', '34567890WXYZ', '2017-10-25', DATEADD(month,-5,GETDATE()), 0, NULL,                      '2027-10-25', 1, GETDATE(), 0),
                (2, 2, 'Abrahams',  'Gareth',      '8507185800080', 'P002009', 'C002009', '45678901XYZA', '2014-07-18', DATEADD(month,-7,GETDATE()), 1, DATEADD(month,18,GETDATE()),'2027-07-18', 1, GETDATE(), 0),
                (2, 1, 'Daniels',   'Liezel',      '9212064800084', 'P002010', 'C002010', '56789012YZAB', '2020-12-06', DATEADD(month,-2,GETDATE()), 0, NULL,                      '2030-12-06', 1, GETDATE(), 0),
                (2, 2, 'Isaacs',    'Ferdie',      '7706285800081', 'P002011', 'C002011', '67890123ZABC', '2008-06-28', DATEADD(month,-15,GETDATE()),0, NULL,                      '2025-06-28', 1, GETDATE(), 0),
                (2, 1, 'Thomas',    'Marlene',     '8810144800086', 'P002012', 'C002012', '78901234ABCD', '2016-10-14', DATEADD(month,-9,GETDATE()), 1, DATEADD(year,4,GETDATE()), '2028-10-14', 1, GETDATE(), 0),
                (2, 2, 'Jansen',    'Werner',      '9402145800089', 'P002013', 'C002013', '89012345BCDE', '2021-02-14', DATEADD(month,-1,GETDATE()), 0, NULL,                      '2031-02-14', 1, GETDATE(), 0),
                (2, 1, 'Coetzee',   'Elmarie',     '8006094800083', 'P002014', 'C002014', '90123456CDEF', '2011-06-09', DATEADD(month,-13,GETDATE()),0, NULL,                      '2026-06-09', 1, GETDATE(), 0),
                (2, 2, 'Petersen',  'Ashraf',      '7309115800085', 'P002015', 'C002015', '01234567DEFG', '2006-09-11', DATEADD(month,-22,GETDATE()),1, DATEADD(year,1,GETDATE()), '2025-09-11', 1, GETDATE(), 0);
            ");
            var driverCount = await dbContext.Drivers.CountAsync();
            Console.WriteLine($"  ✓ Site drivers seeded ({driverCount} records across sites 1 and 2).");
        }
    }

    private static async Task SeedMaintenanceData(FisDbContext dbContext)
    {
        if (!await dbContext.MaintenanceRecords.AnyAsync())
        {
            // Seed Maintenance Records linked to Vehicles (1, 2)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT maintenance_records ON;
                INSERT INTO maintenance_records (
                    maintenance_id, vmf_code, maintenance_date, maintenance_type, 
                    odometer_reading, description, status, still_current, 
                    warranty_work, vehicle_roadworthy,
                    total_cost, labour_cost, parts_cost,
                    created_date, date_created, is_deleted
                ) VALUES 
                (1, 1, DATEADD(month, -1, GETDATE()), 'SERVICE', 10000, '10,000km Service', 'COMPLETED', 'Y', 'N', 'Y', 5000, 2000, 3000, GETDATE(), GETDATE(), 0),
                (2, 2, DATEADD(month, -2, GETDATE()), 'REPAIR', 40000, 'Brake Pad Replacement', 'COMPLETED', 'Y', 'N', 'Y', 2500, 1000, 1500, GETDATE(), GETDATE(), 0);
                SET IDENTITY_INSERT maintenance_records OFF;
            ");
            Console.WriteLine("  ✓ Maintenance Records seeded.");
        }
    }

    private static async Task SeedFineData(FisDbContext dbContext)
    {
        if (!await dbContext.Fines.AnyAsync())
        {
            // Seed Fines linked to Vehicles (1, 2) and Sites (1, 2)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Fines ON;
                INSERT INTO Fines (
                    Fine_code, vmf_code, Site_code, Offence_date, Offence_reference, 
                    Offence_issuer, Fine_amount, Offence_name, date_created, is_deleted
                ) VALUES 
                (1, 1, 1, DATEADD(day, -5, GETDATE()), 'REF12345', 'JMPD', 500.00, 'Speeding', GETDATE(), 0),
                (2, 2, 2, DATEADD(day, -10, GETDATE()), 'REF67890', 'Cape Town Traffic', 1000.00, 'Illegal Parking', GETDATE(), 0);
                SET IDENTITY_INSERT Fines OFF;
            ");
            Console.WriteLine("  ✓ Fines seeded.");
        }
    }

    private static async Task SeedAccidentData(FisDbContext dbContext)
    {
        if (!await dbContext.Accidents.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT accident ON;
                INSERT INTO accident (
                    accident_code, vmf_code, description, driver_name, 
                    occurence_date, occurence_time, reported_date, 
                    claim_amount, excess_amount, date_created, is_deleted
                ) VALUES 
                (1, 1, 'Fender bender in parking lot', 'John Doe', DATEADD(month, -1, GETDATE()), DATEADD(month, -1, GETDATE()), DATEADD(month, -1, GETDATE()), 5000.00, 500.00, GETDATE(), 0),
                (2, 3, 'Windscreen crack from stone', 'Mike Ross', DATEADD(month, -3, GETDATE()), DATEADD(month, -3, GETDATE()), DATEADD(month, -3, GETDATE()), 2500.00, 0.00, GETDATE(), 0),
                (3, 5, 'Side swipe on highway', 'Harvey Specter', DATEADD(year, -1, GETDATE()), DATEADD(year, -1, GETDATE()), DATEADD(year, -1, GETDATE()), 15000.00, 1000.00, GETDATE(), 0);
                SET IDENTITY_INSERT accident OFF;
            ");
            Console.WriteLine("  ✓ Accidents seeded.");
        }
    }

    private static async Task SeedLicenseData(FisDbContext dbContext)
    {
        if (!await dbContext.Licenses.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT license ON;
                INSERT INTO license (licence_code, licence_description, licence_category, date_created, is_deleted) VALUES 
                (1, 'Code 08 (B)', 'B', GETDATE(), 0),
                (2, 'Code 10 (C1)', 'C1', GETDATE(), 0),
                (3, 'Code 14 (EC)', 'EC', GETDATE(), 0),
                (4, 'Professional Driving Permit', 'PrDP', GETDATE(), 0);
                SET IDENTITY_INSERT license OFF;
            ");
            Console.WriteLine("  ✓ Licenses seeded.");
        }
    }

    private static async Task SeedLogbookData(FisDbContext dbContext)
    {
        if (!await dbContext.Logbooks.AnyAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT logbook ON;
                INSERT INTO logbook (
                    logbookcode, vmf_code, begin_num, end_num, handout_date, 
                    site_code, lb_receiver_name, lb_tel_num, lb_comment, 
                    date_created, is_deleted
                ) VALUES 
                (1, 1, 'LB001-001', 'LB001-100', DATEADD(year, -1, GETDATE()), 1, 'John Doe', '0821234567', 'Initial issue', GETDATE(), 0),
                (2, 2, 'LB002-001', 'LB002-100', DATEADD(year, -1, GETDATE()), 2, 'Jane Smith', '0829876543', 'Initial issue', GETDATE(), 0),
                (3, 1, 'LB001-101', 'LB001-200', DATEADD(month, -6, GETDATE()), 1, 'John Doe', '0821234567', 'Replacement', GETDATE(), 0);
                SET IDENTITY_INSERT logbook OFF;
            ");
            Console.WriteLine("  ✓ Logbooks seeded.");
        }
    }

    private static async Task SeedMerchantAndSupplierData(FisDbContext dbContext)
    {
        // Merchants (wwmerchant)
        if (!await dbContext.Merchants.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT wwmerchant ON;
                INSERT INTO wwmerchant (wwmerch_code, wwmerch_name, wwmerch_tel, wwmerch_email, date_created, is_deleted) VALUES 
                (1, 'AutoZone', '011 123 4567', 'sales@autozone.co.za', GETDATE(), 0),
                (2, 'Tiger Wheel & Tyre', '011 987 6543', 'info@twt.co.za', GETDATE(), 0),
                (3, 'Shell Garage Sandton', '011 555 1234', 'manager@shellsandton.co.za', GETDATE(), 0);
                SET IDENTITY_INSERT wwmerchant OFF;
            ");
            Console.WriteLine("  ✓ Merchants seeded.");
        }

        // Suppliers (Suppliers)
        if (!await dbContext.Suppliers.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Suppliers ON;
                INSERT INTO Suppliers (supplier_id, supplier_name, address, phone_number, contact_person, email, is_active, date_created, is_deleted) VALUES 
                (1, 'Toyota SA', 'Sandton, JHB', '011 809 9111', 'Sales Manager', 'sales@toyota.co.za', 1, GETDATE(), 0),
                (2, 'Ford SA', 'Silverton, Pretoria', '012 800 1234', 'Fleet Sales', 'fleet@ford.co.za', 1, GETDATE(), 0),
                (3, 'Avis Fleet', 'Isando, JHB', '011 923 3900', 'Account Mgr', 'accounts@avisfleet.co.za', 1, GETDATE(), 0);
                SET IDENTITY_INSERT Suppliers OFF;
            ");
            Console.WriteLine("  ✓ Suppliers seeded.");
        }
    }

    private static async Task SeedTrafficDeptData(FisDbContext dbContext)
    {
        if (!await dbContext.TrafficDepts.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Traffic_Dept ON;
                INSERT INTO Traffic_Dept (Traffic_dept_code, Traf_name, Traf_telephone, Traf_email, date_created, is_deleted) VALUES 
                (1, 'JMPD', '011 375 5911', 'fines@joburg.org.za', GETDATE(), 0),
                (2, 'Cape Town Traffic', '021 444 3333', 'traffic@capetown.gov.za', GETDATE(), 0),
                (3, 'Tshwane Metro Police', '012 358 7095', 'tmpd@tshwane.gov.za', GETDATE(), 0);
                SET IDENTITY_INSERT Traffic_Dept OFF;
            ");
            Console.WriteLine("  ✓ Traffic Departments seeded.");
        }
    }

    private static async Task SeedTowingData(FisDbContext dbContext)
    {
        if (!await dbContext.Towings.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT Towing ON;
                INSERT INTO Towing (
                    Towing_code, vmf_code, Call_refer, Tow_request_date, Tow_request_time, 
                    Tow_location_start, Vehicle_problem, Keys, Site_code, 
                    date_created, is_deleted
                ) VALUES 
                (1, 1, 1001, DATEADD(month, -2, GETDATE()), DATEADD(month, -2, GETDATE()), 'N1 Highway Midrand', 'Engine failure', 'With Driver', 1, GETDATE(), 0),
                (2, 4, 1002, DATEADD(month, -4, GETDATE()), DATEADD(month, -4, GETDATE()), 'Main Rd, Cape Town', 'Accident damage', 'At Police Station', 2, GETDATE(), 0);
                SET IDENTITY_INSERT Towing OFF;
            ");
            Console.WriteLine("  ✓ Towing data seeded.");
        }
    }

    private static async Task SeedVehiclePhotoData(FisDbContext dbContext)
    {
        if (!await dbContext.VehiclePhotos.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT VehiclePhotoInfo ON;
                INSERT INTO VehiclePhotoInfo (VehiclePhotoInfoCode, VehicleMasterCode, FileUrl, Description, Orientation, date_created, is_deleted) VALUES 
                (1, 1, 'https://example.com/photos/abc123gp_front.jpg', 'Front view', 1, GETDATE(), 0),
                (2, 1, 'https://example.com/photos/abc123gp_side.jpg', 'Side view', 2, GETDATE(), 0),
                (3, 2, 'https://example.com/photos/xyz987gp_accident.jpg', 'Accident damage', 1, GETDATE(), 0);
                SET IDENTITY_INSERT VehiclePhotoInfo OFF;
            ");
            Console.WriteLine("  ✓ Vehicle Photos seeded.");
        }
    }

    private static async Task SeedFinancialData(FisDbContext dbContext)
    {
        Console.WriteLine("  ├── Seeding Financial Data (Years, Costs, Tariffs)...");

        // 1. Financial Years
        // Using explicit insert to avoid identity issues if any, though financial_year_code is short PK
        if (!await dbContext.FinancialYears.AnyAsync(y => y.financial_year_code == 2024))
        {
             try
             {
                 // Try with IDENTITY_INSERT ON in single batch
                 await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT financial_year ON;
                      INSERT INTO financial_year (financial_year_code, financial_year, start_date, end_date, date_created, is_deleted)
                      VALUES 
                      (2023, '2023/2024', '2023-04-01', '2024-03-31', GETDATE(), 0),
                      (2024, '2024/2025', '2024-04-01', '2025-03-31', GETDATE(), 0),
                      (2025, '2025/2026', '2025-04-01', '2026-03-31', GETDATE(), 0);
                      SET IDENTITY_INSERT financial_year OFF;");
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for financial_year ({ex.Message}). Trying without...");
                 // Fallback if not identity (e.g. if previous failed because table has no identity)
                 await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO financial_year (financial_year_code, financial_year, start_date, end_date, date_created, is_deleted)
                      VALUES 
                      (2023, '2023/2024', '2023-04-01', '2024-03-31', GETDATE(), 0),
                      (2024, '2024/2025', '2024-04-01', '2025-03-31', GETDATE(), 0),
                      (2025, '2025/2026', '2025-04-01', '2026-03-31', GETDATE(), 0)");
             }
             Console.WriteLine("  ✓ Financial Years seeded.");
        }

        // 2. Cost Categories
        // Check if exists
        if (!await dbContext.CostCategories.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT cost_category ON;
                      INSERT INTO cost_category (cost_category_code, description, vat_recoverable, cpk_contribution, date_created, is_deleted)
                      VALUES 
                      (1, 'Fuel', 'Y', 'Y', GETDATE(), 0),
                      (2, 'Maintenance', 'Y', 'Y', GETDATE(), 0),
                      (3, 'Tyres', 'Y', 'Y', GETDATE(), 0),
                      (4, 'Tolls', 'N', 'N', GETDATE(), 0),
                      (5, 'Admin Fees', 'Y', 'N', GETDATE(), 0),
                      (6, 'Insurance', 'N', 'Y', GETDATE(), 0);
                      SET IDENTITY_INSERT cost_category OFF;");
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for cost_category ({ex.Message}). Trying without...");
                 await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO cost_category (cost_category_code, description, vat_recoverable, cpk_contribution, date_created, is_deleted)
                      VALUES 
                      (1, 'Fuel', 'Y', 'Y', GETDATE(), 0),
                      (2, 'Maintenance', 'Y', 'Y', GETDATE(), 0),
                      (3, 'Tyres', 'Y', 'Y', GETDATE(), 0),
                      (4, 'Tolls', 'N', 'N', GETDATE(), 0),
                      (5, 'Admin Fees', 'Y', 'N', GETDATE(), 0),
                      (6, 'Insurance', 'N', 'Y', GETDATE(), 0)");
            }
             Console.WriteLine("  ✓ Cost Categories seeded.");
        }

        // 3. Fuel Tariffs
        if (!await dbContext.FuelTariffs.AnyAsync())
        {
             // fuel_tariff_code might be identity. Let's try SET IDENTITY_INSERT just in case.
             try 
             {
                 await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT fuel_tariff ON;
                      INSERT INTO fuel_tariff (fuel_tariff_code, fuel_type_code, fuel_tariff, fuel_tariff_notes, start_date, date_created, is_deleted)
                      VALUES 
                      (1, 1, 23.50, 'Standard Inland Rate', DATEADD(month, -1, GETDATE()), GETDATE(), 0),
                      (2, 2, 24.10, 'Wholesale Diesel Rate', DATEADD(month, -1, GETDATE()), GETDATE(), 0);
                      SET IDENTITY_INSERT fuel_tariff OFF;");
             }
             catch (Exception ex)
             {
                 Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for fuel_tariff ({ex.Message}). Trying without...");
                 // Fallback if not identity
                 await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO fuel_tariff (fuel_tariff_code, fuel_type_code, fuel_tariff, fuel_tariff_notes, start_date, date_created, is_deleted)
                      VALUES 
                      (1, 1, 23.50, 'Standard Inland Rate', DATEADD(month, -1, GETDATE()), GETDATE(), 0),
                      (2, 2, 24.10, 'Wholesale Diesel Rate', DATEADD(month, -1, GETDATE()), GETDATE(), 0)");
             }
             Console.WriteLine("  ✓ Fuel Tariffs seeded.");
        }
    }

    private static async Task SeedWorkshopData(FisDbContext dbContext)
    {
        Console.WriteLine("  ├── Seeding Workshop Data (Tasks, Parts)...");

        // 1. Tasks
        if (!await dbContext.Tasks.AnyAsync())
        {
            try 
            {
                // task_code is int, likely identity
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT task ON;
                      INSERT INTO task (task_code, profile_code, description, duration_hours, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'Major Service (15000km)', 2.5, GETDATE(), 0),
                      (2, 1, 'Oil Change & Filter', 0.5, GETDATE(), 0),
                      (3, 1, 'Brake Pad Replacement (Front)', 1.0, GETDATE(), 0),
                      (4, 1, 'Clutch Overhaul', 4.5, GETDATE(), 0),
                      (5, 1, 'Check Engine Light Diagnostics', 0.5, GETDATE(), 0);
                      SET IDENTITY_INSERT task OFF;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for task ({ex.Message}). Trying without...");
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO task (task_code, profile_code, description, duration_hours, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'Major Service (15000km)', 2.5, GETDATE(), 0),
                      (2, 1, 'Oil Change & Filter', 0.5, GETDATE(), 0),
                      (3, 1, 'Brake Pad Replacement (Front)', 1.0, GETDATE(), 0),
                      (4, 1, 'Clutch Overhaul', 4.5, GETDATE(), 0),
                      (5, 1, 'Check Engine Light Diagnostics', 0.5, GETDATE(), 0)");
            }
            Console.WriteLine("  ✓ Workshop Tasks seeded.");
        }

        // 2. Parts
        if (!await dbContext.Parts.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT part ON;
                      INSERT INTO part (part_code, part_number, description, qty_on_hand, qty_on_order, date_created, is_deleted)
                      VALUES 
                      (1, 'OIL-FIL-001', 'Oil Filter Standard', 50, 100, GETDATE(), 0),
                      (2, 'BRK-PAD-FT', 'Brake Pads Front Set', 12, 20, GETDATE(), 0),
                      (3, 'SPK-PLG-NGK', 'Spark Plug NGK R', 200, 0, GETDATE(), 0),
                      (4, 'AIR-FIL-099', 'Air Filter Assy', 5, 50, GETDATE(), 0),
                      (5, 'WIP-BLD-22', 'Wiper Blade 22 inch', 30, 0, GETDATE(), 0);
                      SET IDENTITY_INSERT part OFF;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for part ({ex.Message}). Trying without...");
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO part (part_code, part_number, description, qty_on_hand, qty_on_order, date_created, is_deleted)
                      VALUES 
                      (1, 'OIL-FIL-001', 'Oil Filter Standard', 50, 100, GETDATE(), 0),
                      (2, 'BRK-PAD-FT', 'Brake Pads Front Set', 12, 20, GETDATE(), 0),
                      (3, 'SPK-PLG-NGK', 'Spark Plug NGK R', 200, 0, GETDATE(), 0),
                      (4, 'AIR-FIL-099', 'Air Filter Assy', 5, 50, GETDATE(), 0),
                      (5, 'WIP-BLD-22', 'Wiper Blade 22 inch', 30, 0, GETDATE(), 0)");
            }
            Console.WriteLine("  ✓ Workshop Parts seeded.");
        }
    }

    private static async Task SeedTrackingData(FisDbContext dbContext)
    {
        Console.WriteLine("  ├── Seeding Tracking Data (GPS Units, Monitor)...");

        // 1. Tracking Units
        if (!await dbContext.Trackings.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT Tracking ON;
                      INSERT INTO Tracking (track_code, vmf_code, track_num, track_status, track_type, install_date, remove_date, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'TRK-001-GPS', 'Active', 'GPS', DATEADD(year, -2, GETDATE()), NULL, GETDATE(), 0),
                      (2, 2, 'TRK-002-GPS', 'Active', 'GPS', DATEADD(year, -1, GETDATE()), NULL, GETDATE(), 0),
                      (3, 3, 'TRK-003-RF', 'Inactive', 'RF', DATEADD(year, -3, GETDATE()), DATEADD(month, -1, GETDATE()), GETDATE(), 0);
                      SET IDENTITY_INSERT Tracking OFF;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for Tracking ({ex.Message}). Trying without...");
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO Tracking (track_code, vmf_code, track_num, track_status, track_type, install_date, remove_date, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'TRK-001-GPS', 'Active', 'GPS', DATEADD(year, -2, GETDATE()), NULL, GETDATE(), 0),
                      (2, 2, 'TRK-002-GPS', 'Active', 'GPS', DATEADD(year, -1, GETDATE()), NULL, GETDATE(), 0),
                      (3, 3, 'TRK-003-RF', 'Inactive', 'RF', DATEADD(year, -3, GETDATE()), DATEADD(month, -1, GETDATE()), GETDATE(), 0)");
            }
            Console.WriteLine("  ✓ Tracking units seeded.");
        }

        // 2. Monitor (Inquiries/Alerts)
        if (!await dbContext.Monitors.AnyAsync())
        {
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"SET IDENTITY_INSERT Monitor ON;
                      INSERT INTO Monitor (monitor_code, vmf_code, Inquiry_type, Inquiry_Desc, Capture_dat, Driver_name, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'Speeding', 'Vehicle exceeded 120km/h on N1', DATEADD(day, -5, GETDATE()), 'John Doe', GETDATE(), 0),
                      (2, 2, 'Route Deviation', 'Vehicle left designated zone', DATEADD(day, -2, GETDATE()), 'Jane Smith', GETDATE(), 0),
                      (3, 1, 'Harsh Braking', 'Excessive g-force recorded', DATEADD(day, -1, GETDATE()), 'John Doe', GETDATE(), 0);
                      SET IDENTITY_INSERT Monitor OFF;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ! Warning: IDENTITY_INSERT failed for Monitor ({ex.Message}). Trying without...");
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"INSERT INTO Monitor (monitor_code, vmf_code, Inquiry_type, Inquiry_Desc, Capture_dat, Driver_name, date_created, is_deleted)
                      VALUES 
                      (1, 1, 'Speeding', 'Vehicle exceeded 120km/h on N1', DATEADD(day, -5, GETDATE()), 'John Doe', GETDATE(), 0),
                      (2, 2, 'Route Deviation', 'Vehicle left designated zone', DATEADD(day, -2, GETDATE()), 'Jane Smith', GETDATE(), 0),
                      (3, 1, 'Harsh Braking', 'Excessive g-force recorded', DATEADD(day, -1, GETDATE()), 'John Doe', GETDATE(), 0)");
            }
            Console.WriteLine("  ✓ Monitor alerts seeded.");
        }
    }

    private static async Task SeedFrontendDemoCoverage(FisDbContext dbContext)
    {
        Console.WriteLine("  ├── Seeding frontend demo coverage (validation + vehicles + contract workflow)...");

        await SeedContractWorkflowReferenceData(dbContext);
        await SeedValidationTopUpData(dbContext);
        await SeedDemoVehicles(dbContext);
        await SeedDemoContracts(dbContext);
        await SeedVehicleAuthorizationQueue(dbContext);

        Console.WriteLine("  ✓ Frontend demo coverage scenarios ready.");
    }

    private static async Task SeedContractWorkflowReferenceData(FisDbContext dbContext)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT contract_status ON;
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 1)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (1, 'Pending Approval', 'PEND', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 2)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (2, 'Approved', 'APPR', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 3)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (3, 'Active', 'ACTV', 1, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 4)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (4, 'Declined For Correction', 'CORR', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 5)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (5, 'Declined', 'DECL', 0, 1, GETDATE(), 0);
                SET IDENTITY_INSERT contract_status OFF;
            ");
        }
        catch
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 1)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (1, 'Pending Approval', 'PEND', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 2)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (2, 'Approved', 'APPR', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 3)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (3, 'Active', 'ACTV', 1, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 4)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (4, 'Declined For Correction', 'CORR', 0, 0, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM contract_status WHERE contract_status_code = 5)
                    INSERT INTO contract_status (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, is_deleted)
                    VALUES (5, 'Declined', 'DECL', 0, 1, GETDATE(), 0);
            ");
        }

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT 1 FROM Contract_type WHERE contract_type = 'H')
                INSERT INTO Contract_type (contract_type, CT_description, CT_Active, date_created, is_deleted)
                VALUES ('H', 'Hire Contract', 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM Contract_type WHERE contract_type = 'R')
                INSERT INTO Contract_type (contract_type, CT_description, CT_Active, date_created, is_deleted)
                VALUES ('R', 'Relief Contract', 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM Contract_type WHERE contract_type = 'D')
                INSERT INTO Contract_type (contract_type, CT_description, CT_Active, date_created, is_deleted)
                VALUES ('D', 'Daily Contract', 1, GETDATE(), 0);
        ");
    }

    private static async Task SeedValidationTopUpData(FisDbContext dbContext)
    {
        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT class ON;
                IF NOT EXISTS (SELECT 1 FROM class WHERE class_code = 6)
                    INSERT INTO class (class_code, description, date_created, is_deleted) VALUES (6, 'Bus', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM class WHERE class_code = 7)
                    INSERT INTO class (class_code, description, date_created, is_deleted) VALUES (7, 'Trailer', GETDATE(), 0);
                SET IDENTITY_INSERT class OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM class WHERE class_code = 6)
                    INSERT INTO class (class_code, description, date_created, is_deleted) VALUES (6, 'Bus', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM class WHERE class_code = 7)
                    INSERT INTO class (class_code, description, date_created, is_deleted) VALUES (7, 'Trailer', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT unit_of_measure ON;
                IF NOT EXISTS (SELECT 1 FROM unit_of_measure WHERE unit_of_measure_code = 6)
                    INSERT INTO unit_of_measure (unit_of_measure_code, unit_description, unit_abbreviation, unit_category, date_created, is_deleted)
                    VALUES (6, 'Days', 'day', 'Time', GETDATE(), 0);
                SET IDENTITY_INSERT unit_of_measure OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM unit_of_measure WHERE unit_of_measure_code = 6)
                    INSERT INTO unit_of_measure (unit_of_measure_code, unit_description, unit_abbreviation, unit_category, date_created, is_deleted)
                    VALUES (6, 'Days', 'day', 'Time', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT maintenance_trigger ON;
                IF NOT EXISTS (SELECT 1 FROM maintenance_trigger WHERE maint_trigger_code = 5)
                    INSERT INTO maintenance_trigger (maint_trigger_code, description, trigger_id, date_created, is_deleted)
                    VALUES (5, 'Hybrid (Time + KM)', 'HYBRID', GETDATE(), 0);
                SET IDENTITY_INSERT maintenance_trigger OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM maintenance_trigger WHERE maint_trigger_code = 5)
                    INSERT INTO maintenance_trigger (maint_trigger_code, description, trigger_id, date_created, is_deleted)
                    VALUES (5, 'Hybrid (Time + KM)', 'HYBRID', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT licence_fee ON;
                IF NOT EXISTS (SELECT 1 FROM licence_fee WHERE licence_fee_code = 5)
                    INSERT INTO licence_fee (licence_fee_code, licence_description, licence_fee, date_created, is_deleted)
                    VALUES (5, 'Bus Annual License', 2400.00, GETDATE(), 0);
                SET IDENTITY_INSERT licence_fee OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM licence_fee WHERE licence_fee_code = 5)
                    INSERT INTO licence_fee (licence_fee_code, licence_description, licence_fee, date_created, is_deleted)
                    VALUES (5, 'Bus Annual License', 2400.00, GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT Loss_type ON;
                IF NOT EXISTS (SELECT 1 FROM Loss_type WHERE loss_type_code = 6)
                    INSERT INTO Loss_type (loss_type_code, loss_description, date_created, is_deleted)
                    VALUES (6, 'Natural Disaster', GETDATE(), 0);
                SET IDENTITY_INSERT Loss_type OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM Loss_type WHERE loss_type_code = 6)
                    INSERT INTO Loss_type (loss_type_code, loss_description, date_created, is_deleted)
                    VALUES (6, 'Natural Disaster', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT fuel_type ON;
                IF NOT EXISTS (SELECT 1 FROM fuel_type WHERE fuel_type_code = 3)
                    INSERT INTO fuel_type (fuel_type_code, fuel_description, date_created, is_deleted)
                    VALUES (3, 'Hybrid', GETDATE(), 0);
                SET IDENTITY_INSERT fuel_type OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM fuel_type WHERE fuel_type_code = 3)
                    INSERT INTO fuel_type (fuel_type_code, fuel_description, date_created, is_deleted)
                    VALUES (3, 'Hybrid', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT driver_licence_types ON;
                IF NOT EXISTS (SELECT 1 FROM driver_licence_types WHERE driver_licence_type_id = 4)
                    INSERT INTO driver_licence_types (driver_licence_type_id, driver_licence_type_code, driver_licence_type_description, date_created, is_deleted)
                    VALUES (4, 'B', 'Code B', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM driver_licence_types WHERE driver_licence_type_id = 5)
                    INSERT INTO driver_licence_types (driver_licence_type_id, driver_licence_type_code, driver_licence_type_description, date_created, is_deleted)
                    VALUES (5, 'C', 'Code C', GETDATE(), 0);
                SET IDENTITY_INSERT driver_licence_types OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM driver_licence_types WHERE driver_licence_type_id = 4)
                    INSERT INTO driver_licence_types (driver_licence_type_id, driver_licence_type_code, driver_licence_type_description, date_created, is_deleted)
                    VALUES (4, 'B', 'Code B', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM driver_licence_types WHERE driver_licence_type_id = 5)
                    INSERT INTO driver_licence_types (driver_licence_type_id, driver_licence_type_code, driver_licence_type_description, date_created, is_deleted)
                    VALUES (5, 'C', 'Code C', GETDATE(), 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT vehicle_status ON;
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 1)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (1, 'In Service', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 2)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (2, 'Out Of Service', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 3)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (3, 'Sold', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 4)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (4, 'Accident Hold', GETDATE(), 0);
                SET IDENTITY_INSERT vehicle_status OFF;
            ",
            @"
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 1)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (1, 'In Service', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 2)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (2, 'Out Of Service', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 3)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (3, 'Sold', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 4)
                    INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted)
                    VALUES (4, 'Accident Hold', GETDATE(), 0);
            ");
    }

    private static async Task SeedDemoVehicles(FisDbContext dbContext)
    {
        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            @"
                SET IDENTITY_INSERT vehicle_master ON;

                DECLARE @vmf INT = 1001;
                WHILE @vmf <= 1120
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = @vmf)
                    BEGIN
                        INSERT INTO vehicle_master
                        (
                            vmf_code, fleet_number, registration_number, model_code, type_code,
                            colour, engine_number_1, chassis_number, year_manufactured,
                            current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code,
                            purchase_date, purchase_amount, monthly_overhead, service_last_done, service_last_odo,
                            date_created, is_deleted
                        )
                        VALUES
                        (
                            @vmf,
                            CONCAT('GGD', RIGHT('0000' + CAST(@vmf AS varchar(4)), 4)),
                            CONCAT('DEM ', RIGHT('000' + CAST(@vmf - 1000 AS varchar(3)), 3), ' GP'),
                            ((@vmf - 1001) % 4) + 1,
                            ((@vmf - 1001) % 3) + 1,
                            CASE WHEN @vmf % 5 = 0 THEN 'White' WHEN @vmf % 5 = 1 THEN 'Silver' WHEN @vmf % 5 = 2 THEN 'Blue' WHEN @vmf % 5 = 3 THEN 'Grey' ELSE 'Black' END,
                            CONCAT('ENG', @vmf, 'X'),
                            CONCAT('CHS', @vmf, 'Z'),
                            2020 + ((@vmf - 1001) % 6),
                            12000 + ((@vmf - 1001) * 850),
                            0,
                            DATEADD(day, -(@vmf - 950), GETDATE()),
                            CASE WHEN @vmf % 12 = 0 THEN 2 ELSE 1 END,
                            CASE WHEN @vmf % 2 = 0 THEN 2 ELSE 1 END,
                            DATEADD(day, -(@vmf - 980), GETDATE()),
                            320000 + ((@vmf - 1001) * 1200),
                            1450 + ((@vmf - 1001) * 5),
                            DATEADD(day, -30, GETDATE()),
                            10000 + ((@vmf - 1001) * 700),
                            GETDATE(),
                            0
                        );
                    END

                    SET @vmf = @vmf + 1;
                END;

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1201)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1201, 'GGNO0001', 'NOC 001 GP', 1, 1, 'White', 'ENG1201A', 'CHS1201A', 2025, 2100, 0, DATEADD(month, -1, GETDATE()), 1, 1, 345000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1202)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1202, 'GGPD0002', 'PND 002 GP', 2, 2, 'Silver', 'ENG1202A', 'CHS1202A', 2024, 16000, 0, DATEADD(month, -6, GETDATE()), 1, 1, 380000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1203)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1203, 'GGAC0003', 'ACT 003 GP', 3, 1, 'Blue', 'ENG1203A', 'CHS1203A', 2023, 45200, 0, DATEADD(year, -2, GETDATE()), 1, 2, 415000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1204)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1204, 'GGCR0004', 'COR 004 GP', 4, 2, 'Grey', 'ENG1204A', 'CHS1204A', 2022, 69300, 0, DATEADD(year, -3, GETDATE()), 1, 2, 295000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1205)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1205, 'GGDC0005', 'DEC 005 GP', 1, 1, 'Black', 'ENG1205A', 'CHS1205A', 2021, 98500, 0, DATEADD(year, -4, GETDATE()), 1, 1, 255000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1206)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1206, 'GGPR0006', 'PAR 006 GP', 2, 2, 'White', 'ENG1206A', 'CHS1206A', 2024, 22100, 0, DATEADD(month, -8, GETDATE()), 1, 1, 420000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1207)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1207, 'GGRE0007', 'REL 007 GP', 3, 1, 'Silver', 'ENG1207A', 'CHS1207A', 2025, 5500, 0, DATEADD(month, -2, GETDATE()), 1, 2, 450000, GETDATE(), 0);

                SET IDENTITY_INSERT vehicle_master OFF;
            ",
            @"
                DECLARE @vmf INT = 1001;
                WHILE @vmf <= 1120
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = @vmf)
                    BEGIN
                        INSERT INTO vehicle_master
                        (
                            vmf_code, fleet_number, registration_number, model_code, type_code,
                            colour, engine_number_1, chassis_number, year_manufactured,
                            current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code,
                            purchase_date, purchase_amount, monthly_overhead, service_last_done, service_last_odo,
                            date_created, is_deleted
                        )
                        VALUES
                        (
                            @vmf,
                            CONCAT('GGD', RIGHT('0000' + CAST(@vmf AS varchar(4)), 4)),
                            CONCAT('DEM ', RIGHT('000' + CAST(@vmf - 1000 AS varchar(3)), 3), ' GP'),
                            ((@vmf - 1001) % 4) + 1,
                            ((@vmf - 1001) % 3) + 1,
                            CASE WHEN @vmf % 5 = 0 THEN 'White' WHEN @vmf % 5 = 1 THEN 'Silver' WHEN @vmf % 5 = 2 THEN 'Blue' WHEN @vmf % 5 = 3 THEN 'Grey' ELSE 'Black' END,
                            CONCAT('ENG', @vmf, 'X'),
                            CONCAT('CHS', @vmf, 'Z'),
                            2020 + ((@vmf - 1001) % 6),
                            12000 + ((@vmf - 1001) * 850),
                            0,
                            DATEADD(day, -(@vmf - 950), GETDATE()),
                            CASE WHEN @vmf % 12 = 0 THEN 2 ELSE 1 END,
                            CASE WHEN @vmf % 2 = 0 THEN 2 ELSE 1 END,
                            DATEADD(day, -(@vmf - 980), GETDATE()),
                            320000 + ((@vmf - 1001) * 1200),
                            1450 + ((@vmf - 1001) * 5),
                            DATEADD(day, -30, GETDATE()),
                            10000 + ((@vmf - 1001) * 700),
                            GETDATE(),
                            0
                        );
                    END

                    SET @vmf = @vmf + 1;
                END;

                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1201)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1201, 'GGNO0001', 'NOC 001 GP', 1, 1, 'White', 'ENG1201A', 'CHS1201A', 2025, 2100, 0, DATEADD(month, -1, GETDATE()), 1, 1, 345000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1202)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1202, 'GGPD0002', 'PND 002 GP', 2, 2, 'Silver', 'ENG1202A', 'CHS1202A', 2024, 16000, 0, DATEADD(month, -6, GETDATE()), 1, 1, 380000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1203)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1203, 'GGAC0003', 'ACT 003 GP', 3, 1, 'Blue', 'ENG1203A', 'CHS1203A', 2023, 45200, 0, DATEADD(year, -2, GETDATE()), 1, 2, 415000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1204)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1204, 'GGCR0004', 'COR 004 GP', 4, 2, 'Grey', 'ENG1204A', 'CHS1204A', 2022, 69300, 0, DATEADD(year, -3, GETDATE()), 1, 2, 295000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1205)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1205, 'GGDC0005', 'DEC 005 GP', 1, 1, 'Black', 'ENG1205A', 'CHS1205A', 2021, 98500, 0, DATEADD(year, -4, GETDATE()), 1, 1, 255000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1206)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1206, 'GGPR0006', 'PAR 006 GP', 2, 2, 'White', 'ENG1206A', 'CHS1206A', 2024, 22100, 0, DATEADD(month, -8, GETDATE()), 1, 1, 420000, GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_master WHERE vmf_code = 1207)
                    INSERT INTO vehicle_master (vmf_code, fleet_number, registration_number, model_code, type_code, colour, engine_number_1, chassis_number, year_manufactured, current_odo, take_on_odo, take_on_date, vehicle_status_code, location_code, purchase_amount, date_created, is_deleted)
                    VALUES (1207, 'GGRE0007', 'REL 007 GP', 3, 1, 'Silver', 'ENG1207A', 'CHS1207A', 2025, 5500, 0, DATEADD(month, -2, GETDATE()), 1, 2, 450000, GETDATE(), 0);
            ");
    }

    private static async Task SeedDemoContracts(FisDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @can_seed_with_explicit_ids BIT = 1;

            BEGIN TRY
                SET IDENTITY_INSERT contract ON;
            END TRY
            BEGIN CATCH
                SET @can_seed_with_explicit_ids = 0;
            END CATCH

                IF @can_seed_with_explicit_ids = 1
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9001)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, date_created, is_deleted)
                    VALUES (9001, 1203, 2, 'H', DATEADD(month, -5, GETDATE()), DATEADD(month, -5, GETDATE()), 12000, 'Y', 0, 3, GETDATE(), 'Active contract for UX demo', 4, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9002)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, date_created, is_deleted)
                    VALUES (9002, 1202, 1, 'H', DATEADD(day, -2, GETDATE()), DATEADD(day, -2, GETDATE()), 15900, 'N', 0, 1, GETDATE(), 'Pending approval contract for reviewer flow', 4, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9003)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                    VALUES (9003, 1204, 2, 'H', DATEADD(day, -7, GETDATE()), DATEADD(day, -7, GETDATE()), 68800, 'N', 0, 4, GETDATE(), 'Declined for correction example', 4, 5, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9004)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                    VALUES (9004, 1205, 1, 'H', DATEADD(day, -10, GETDATE()), DATEADD(day, -10, GETDATE()), 98000, 'N', 0, 5, GETDATE(), 'Declined example', 4, 5, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9005)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, end_date, end_time, end_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                    VALUES (9005, 1206, 1, 'H', DATEADD(month, -9, GETDATE()), DATEADD(month, -9, GETDATE()), 5000, DATEADD(month, -1, GETDATE()), DATEADD(month, -1, GETDATE()), 21900, 'N', 0, 2, GETDATE(), 'Previously approved and ended contract', 4, 5, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9006)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                    VALUES (9006, 1206, 1, 'H', DATEADD(day, -20, GETDATE()), DATEADD(day, -20, GETDATE()), 21950, 'Y', 0, 3, GETDATE(), 'Parent active contract for relief flow', 4, 5, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9007)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, relief_for_contract, parent_contract_code, date_created, is_deleted)
                    VALUES (9007, 1207, 1, 'R', DATEADD(day, -1, GETDATE()), DATEADD(day, -1, GETDATE()), 5300, 'N', 0, 1, GETDATE(), 'Relief contract pending approval', 4, 9006, 9006, GETDATE(), 0);

                    IF NOT EXISTS (SELECT 1 FROM contract WHERE contract_code = 9008)
                    INSERT INTO contract (contract_code, vmf_code, site_code, contract_type, start_date, start_time, start_odometer, end_date, end_time, end_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                    VALUES (9008, 1001, 1, 'H', DATEADD(month, -8, GETDATE()), DATEADD(month, -8, GETDATE()), 6000, DATEADD(month, -3, GETDATE()), DATEADD(month, -3, GETDATE()), 18200, 'N', 0, 2, GETDATE(), 'Historical completed contract on generated demo vehicle', 4, 5, GETDATE(), 0);

                BEGIN TRY
                    SET IDENTITY_INSERT contract OFF;
                END TRY
                BEGIN CATCH
                END CATCH
            END
        ");
    }

    private static async Task SeedVehicleAuthorizationQueue(FisDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @can_seed_explicit_pre_vehicle_ids BIT = 1;

            BEGIN TRY
                SET IDENTITY_INSERT pre_vehicle_master ON;
            END TRY
            BEGIN CATCH
                SET @can_seed_explicit_pre_vehicle_ids = 0;
            END CATCH

            IF @can_seed_explicit_pre_vehicle_ids = 1
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pre_vehicle_master WHERE temp_vmf_code = 3001)
                    INSERT INTO pre_vehicle_master (temp_vmf_code, chassis_number, engine_number, model_code, registration_number, colour, purchase_amount, purchase_date, purchase_from, take_on_date, take_on_odo, Fleet_Notes, damage_status, Authority_Status, created_by_user_code, date_created, is_deleted)
                    VALUES (3001, 'PRE-CHS-3001', 'PRE-ENG-3001', 1, 'PRC 301 GP', 'White', 365000, DATEADD(day, -7, GETDATE()), 'Toyota SA', DATEADD(day, -2, GETDATE()), 0, 'Awaiting approval by vehicle reviewer', 'N', 'Awaiting Authorization', 4, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM pre_vehicle_master WHERE temp_vmf_code = 3002)
                    INSERT INTO pre_vehicle_master (temp_vmf_code, chassis_number, engine_number, model_code, registration_number, colour, purchase_amount, purchase_date, purchase_from, take_on_date, take_on_odo, Fleet_Notes, damage_status, Authority_Status, authorized_by_user_code, authorization_date, authorization_comment, vmf_code, created_by_user_code, date_created, is_deleted)
                    VALUES (3002, 'PRE-CHS-3002', 'PRE-ENG-3002', 2, 'PRA 302 GP', 'Silver', 402000, DATEADD(day, -25, GETDATE()), 'Ford SA', DATEADD(day, -20, GETDATE()), 120, 'Approved pre-capture', 'N', 'Authorized', 5, DATEADD(day, -18, GETDATE()), 'All documents verified', 1203, 4, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM pre_vehicle_master WHERE temp_vmf_code = 3003)
                    INSERT INTO pre_vehicle_master (temp_vmf_code, chassis_number, engine_number, model_code, registration_number, colour, purchase_amount, purchase_date, purchase_from, take_on_date, take_on_odo, Fleet_Notes, damage_status, Authority_Status, authorized_by_user_code, authorization_date, rejection_reason, authorization_comment, created_by_user_code, date_created, is_deleted)
                    VALUES (3003, 'PRE-CHS-3003', 'PRE-ENG-3003', 3, 'PRR 303 GP', 'Blue', 331000, DATEADD(day, -9, GETDATE()), 'VW Fleet', DATEADD(day, -3, GETDATE()), 0, 'Rejected sample for queue testing', 'Y', 'Rejected', 5, DATEADD(day, -2, GETDATE()), 'Missing finance approval docs', 'Please re-upload signed approval', 4, GETDATE(), 0);

                IF NOT EXISTS (SELECT 1 FROM pre_vehicle_master WHERE temp_vmf_code = 3004)
                    INSERT INTO pre_vehicle_master (temp_vmf_code, chassis_number, engine_number, model_code, registration_number, colour, purchase_amount, purchase_date, purchase_from, take_on_date, take_on_odo, Fleet_Notes, damage_status, Authority_Status, created_by_user_code, date_created, is_deleted)
                    VALUES (3004, 'PRE-CHS-3004', 'PRE-ENG-3004', 4, 'PRC 304 GP', 'Grey', 288500, DATEADD(day, -1, GETDATE()), 'Avis Fleet', GETDATE(), 0, 'Second pending record to test paging/filtering', 'N', 'Awaiting Authorization', 6, GETDATE(), 0);

                BEGIN TRY
                    SET IDENTITY_INSERT pre_vehicle_master OFF;
                END TRY
                BEGIN CATCH
                END CATCH
            END
        ");
    }

    private static async Task ExecuteIdentityAwareSqlAsync(FisDbContext dbContext, string sqlWithIdentityInsert, string sqlFallback)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(sqlWithIdentityInsert);
        }
        catch
        {
            await dbContext.Database.ExecuteSqlRawAsync(sqlFallback);
        }
    }

    private static async Task ApplyBatch1VehicleMasterCompletenessAsync(FisDbContext dbContext)
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            -- Batch 1: Vehicle Master dependent tables and visible columns

            -- 1) department
            IF NOT EXISTS (SELECT 1 FROM department WHERE department_code = 1)
                INSERT INTO department (department_code, description, company_code, dept_active, Service_Years, Overhead_Percentage, Service_Kilometres, date_created, is_deleted)
                VALUES (1, 'Transport', 1, 1, 1, 10, 15000, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM department WHERE department_code = 2)
                INSERT INTO department (department_code, description, company_code, dept_active, Service_Years, Overhead_Percentage, Service_Kilometres, date_created, is_deleted)
                VALUES (2, 'Logistics', 1, 1, 1, 10, 15000, GETDATE(), 0);

            -- 2) site
            IF NOT EXISTS (SELECT 1 FROM site WHERE Site_code = 1)
                INSERT INTO site (Site_code, description, Depatrment_code, site_active, date_created, is_deleted)
                VALUES (1, 'Head Office', 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM site WHERE Site_code = 2)
                INSERT INTO site (Site_code, description, Depatrment_code, site_active, date_created, is_deleted)
                VALUES (2, 'Cape Town Branch', 2, 1, GETDATE(), 0);

            -- 3) make
            IF NOT EXISTS (SELECT 1 FROM make WHERE make_code = 1)
                INSERT INTO make (make_code, make_description, date_created, is_deleted) VALUES (1, 'Toyota', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM make WHERE make_code = 2)
                INSERT INTO make (make_code, make_description, date_created, is_deleted) VALUES (2, 'Ford', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM make WHERE make_code = 3)
                INSERT INTO make (make_code, make_description, date_created, is_deleted) VALUES (3, 'Volkswagen', GETDATE(), 0);

            -- 4) type
            IF NOT EXISTS (SELECT 1 FROM type WHERE type_code = 1)
                INSERT INTO type (type_code, type_description, date_created, is_deleted) VALUES (1, 'LDV', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM type WHERE type_code = 2)
                INSERT INTO type (type_code, type_description, date_created, is_deleted) VALUES (2, 'Sedan', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM type WHERE type_code = 3)
                INSERT INTO type (type_code, type_description, date_created, is_deleted) VALUES (3, 'Truck', GETDATE(), 0);

            -- 5) fuel_type
            IF NOT EXISTS (SELECT 1 FROM fuel_type WHERE fuel_type_code = 1)
                INSERT INTO fuel_type (fuel_type_code, fuel_description, date_created, is_deleted) VALUES (1, 'Petrol', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM fuel_type WHERE fuel_type_code = 2)
                INSERT INTO fuel_type (fuel_type_code, fuel_description, date_created, is_deleted) VALUES (2, 'Diesel', GETDATE(), 0);

            -- 6) vehicle_status
            IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 1)
                INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted) VALUES (1, 'In Service', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 2)
                INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted) VALUES (2, 'Out Of Service', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 3)
                INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted) VALUES (3, 'Sold', GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM vehicle_status WHERE vehicle_status_code = 4)
                INSERT INTO vehicle_status (vehicle_status_code, status_description, date_created, is_deleted) VALUES (4, 'Accident Hold', GETDATE(), 0);

            -- 7) model
            IF NOT EXISTS (SELECT 1 FROM model WHERE model_code = 1)
                INSERT INTO model (model_code, model_description, make_code, type_code, fuel_type_code, unit_of_measure_code, licence_code, class_code, date_created, is_deleted)
                VALUES (1, 'Hilux', 1, 1, 2, 1, 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM model WHERE model_code = 2)
                INSERT INTO model (model_code, model_description, make_code, type_code, fuel_type_code, unit_of_measure_code, licence_code, class_code, date_created, is_deleted)
                VALUES (2, 'Corolla', 1, 2, 1, 1, 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM model WHERE model_code = 3)
                INSERT INTO model (model_code, model_description, make_code, type_code, fuel_type_code, unit_of_measure_code, licence_code, class_code, date_created, is_deleted)
                VALUES (3, 'Ranger', 2, 1, 2, 1, 1, 1, GETDATE(), 0);
            IF NOT EXISTS (SELECT 1 FROM model WHERE model_code = 4)
                INSERT INTO model (model_code, model_description, make_code, type_code, fuel_type_code, unit_of_measure_code, licence_code, class_code, date_created, is_deleted)
                VALUES (4, 'Polo', 3, 2, 1, 1, 1, 1, GETDATE(), 0);

            -- 8) vehicle_master - fill missing visible fields for all active rows
            UPDATE vm
            SET fleet_number = CONCAT('GGX', RIGHT('000000' + CAST(vm.vmf_code AS varchar(10)), 6))
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND (vm.fleet_number IS NULL OR LTRIM(RTRIM(vm.fleet_number)) = '');

            UPDATE vm
            SET registration_number = CONCAT('REG ', RIGHT('000000' + CAST(vm.vmf_code AS varchar(10)), 6), ' GP')
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND (vm.registration_number IS NULL OR LTRIM(RTRIM(vm.registration_number)) = '');

            UPDATE vm
            SET model_code = ((vm.vmf_code - 1) % 4) + 1
            FROM vehicle_master vm
            LEFT JOIN model m ON m.model_code = vm.model_code
            WHERE vm.is_deleted = 0
              AND (vm.model_code IS NULL OR m.model_code IS NULL);

            UPDATE vm
            SET type_code = ((vm.vmf_code - 1) % 3) + 1
            FROM vehicle_master vm
            LEFT JOIN type t ON t.type_code = vm.type_code
            WHERE vm.is_deleted = 0
              AND (vm.type_code IS NULL OR t.type_code IS NULL);

            UPDATE vm
            SET vehicle_status_code = 1
            FROM vehicle_master vm
            LEFT JOIN vehicle_status vs ON vs.vehicle_status_code = vm.vehicle_status_code
            WHERE vm.is_deleted = 0
              AND (vm.vehicle_status_code IS NULL OR vs.vehicle_status_code IS NULL);

            UPDATE vm
            SET location_code = CASE WHEN vm.vmf_code % 2 = 0 THEN 2 ELSE 1 END
            FROM vehicle_master vm
            LEFT JOIN site s ON s.Site_code = vm.location_code
            WHERE vm.is_deleted = 0
              AND (vm.location_code IS NULL OR s.Site_code IS NULL);

            -- fuel_type_code on vehicle_master is optional across legacy variants.
            -- handled outside this static SQL batch to avoid compile-time invalid-column failures.

            UPDATE vm
            SET year_manufactured = COALESCE(vm.year_manufactured, CAST((2020 + (vm.vmf_code % 6)) AS smallint)),
                engine_number_1 = COALESCE(NULLIF(LTRIM(RTRIM(vm.engine_number_1)), ''), CONCAT('ENG', vm.vmf_code, 'A')),
                chassis_number = COALESCE(NULLIF(LTRIM(RTRIM(vm.chassis_number)), ''), CONCAT('CHS', vm.vmf_code, 'A')),
                colour = COALESCE(NULLIF(LTRIM(RTRIM(vm.colour)), ''), 'White'),
                current_odo = CASE WHEN vm.current_odo IS NULL OR vm.current_odo < 0 THEN (10000 + (vm.vmf_code % 90000)) ELSE vm.current_odo END,
                take_on_odo = CASE WHEN vm.take_on_odo IS NULL OR vm.take_on_odo < 0 THEN 0 ELSE vm.take_on_odo END,
                take_on_date = COALESCE(vm.take_on_date, DATEADD(year, -2, GETDATE())),
                date_created = COALESCE(vm.date_created, GETDATE()),
                is_deleted = COALESCE(vm.is_deleted, 0)
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0;

            ;WITH duplicate_gg AS (
                SELECT vmf_code,
                       ROW_NUMBER() OVER (PARTITION BY UPPER(LTRIM(RTRIM(fleet_number))) ORDER BY vmf_code) AS rn
                FROM vehicle_master
                WHERE is_deleted = 0 AND fleet_number IS NOT NULL AND LTRIM(RTRIM(fleet_number)) <> ''
            )
            UPDATE vm
            SET fleet_number = LEFT(CONCAT(vm.fleet_number, '-', d.rn), 50)
            FROM vehicle_master vm
            JOIN duplicate_gg d ON d.vmf_code = vm.vmf_code
            WHERE d.rn > 1;

            ;WITH duplicate_reg AS (
                SELECT vmf_code,
                       ROW_NUMBER() OVER (PARTITION BY UPPER(LTRIM(RTRIM(registration_number))) ORDER BY vmf_code) AS rn
                FROM vehicle_master
                WHERE is_deleted = 0 AND registration_number IS NOT NULL AND LTRIM(RTRIM(registration_number)) <> ''
            )
            UPDATE vm
            SET registration_number = LEFT(CONCAT(vm.registration_number, '-', d.rn), 50)
            FROM vehicle_master vm
            JOIN duplicate_reg d ON d.vmf_code = vm.vmf_code
            WHERE d.rn > 1;

            -- 9) contract - ensure spread exists for frontend scenarios
            IF NOT EXISTS (SELECT 1 FROM contract WHERE is_deleted = 0 AND contract_status_code = 1)
            BEGIN
                INSERT INTO contract (vmf_code, site_code, contract_type, start_date, start_time, start_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, date_created, is_deleted)
                SELECT TOP 1 vm.vmf_code, vm.location_code, 'H', DATEADD(day, -2, GETDATE()), DATEADD(day, -2, GETDATE()), vm.current_odo, 'N', 0, 1, GETDATE(), 'Seeded pending contract', 4, GETDATE(), 0
                FROM vehicle_master vm WHERE vm.is_deleted = 0 ORDER BY vm.vmf_code;
            END

            IF NOT EXISTS (SELECT 1 FROM contract WHERE is_deleted = 0 AND contract_status_code = 3 AND still_current = 'Y')
            BEGIN
                INSERT INTO contract (vmf_code, site_code, contract_type, start_date, start_time, start_odometer, target_return_date, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                SELECT TOP 1 vm.vmf_code, vm.location_code, 'H', DATEADD(month, -3, GETDATE()), DATEADD(month, -3, GETDATE()), vm.current_odo - 1000, DATEADD(month, 1, GETDATE()), 'Y', 0, 3, GETDATE(), 'Seeded active contract', 4, 5, GETDATE(), 0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (SELECT 1 FROM contract c WHERE c.vmf_code = vm.vmf_code AND c.is_deleted = 0 AND c.still_current = 'Y')
                ORDER BY vm.vmf_code DESC;
            END

            IF NOT EXISTS (SELECT 1 FROM contract WHERE is_deleted = 0 AND contract_status_code = 7)
            BEGIN
                INSERT INTO contract (vmf_code, site_code, contract_type, start_date, start_time, end_date, end_time, start_odometer, end_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                SELECT TOP 1 vm.vmf_code, vm.location_code, 'H', DATEADD(month, -9, GETDATE()), DATEADD(month, -9, GETDATE()), DATEADD(month, -1, GETDATE()), DATEADD(month, -1, GETDATE()), vm.current_odo - 5000, vm.current_odo - 1000, 'N', 0, 7, GETDATE(), 'Seeded closed contract', 4, 5, GETDATE(), 0
                FROM vehicle_master vm WHERE vm.is_deleted = 0 ORDER BY vm.vmf_code;
            END

            IF NOT EXISTS (SELECT 1 FROM contract WHERE is_deleted = 0 AND end_date < CAST(GETDATE() AS date))
            BEGIN
                INSERT INTO contract (vmf_code, site_code, contract_type, start_date, start_time, end_date, end_time, start_odometer, end_odometer, still_current, locked_for_transfer, contract_status_code, contract_status_date, Notes, user_code, approver_code, date_created, is_deleted)
                SELECT TOP 1 vm.vmf_code, vm.location_code, 'H', DATEADD(month, -6, GETDATE()), DATEADD(month, -6, GETDATE()), DATEADD(day, -7, GETDATE()), DATEADD(day, -7, GETDATE()), vm.current_odo - 3000, vm.current_odo - 50, 'N', 0, 2, GETDATE(), 'Seeded expired contract example', 4, 5, GETDATE(), 0
                FROM vehicle_master vm WHERE vm.is_deleted = 0 ORDER BY vm.vmf_code DESC;
            END

            -- 10) vehicle_history
            INSERT INTO vehicle_history (hist_vmf_code, hist_vehicle_status_code, hist_date_changed, hist_user_access_code, hist_fleet_number, date_created, created_by_user_code, is_deleted)
            SELECT vm.vmf_code, vm.vehicle_status_code, COALESCE(vm.date_updated, vm.date_created, GETDATE()), 1, vm.fleet_number, GETDATE(), 1, 0
            FROM vehicle_master vm
            LEFT JOIN vehicle_history vh ON vh.hist_vmf_code = vm.vmf_code AND vh.is_deleted = 0
            WHERE vm.is_deleted = 0
              AND vh.hist_code IS NULL;
        ");

        Console.WriteLine("  ✓ Batch 1 vehicle-master completeness updates applied.");

        var missingGg = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE is_deleted = 0 AND (fleet_number IS NULL OR LTRIM(RTRIM(fleet_number)) = '')");
        var missingReg = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE is_deleted = 0 AND (registration_number IS NULL OR LTRIM(RTRIM(registration_number)) = '')");
        var duplicateGg = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM (SELECT UPPER(LTRIM(RTRIM(fleet_number))) v FROM vehicle_master WHERE is_deleted = 0 AND fleet_number IS NOT NULL AND LTRIM(RTRIM(fleet_number)) <> '' GROUP BY UPPER(LTRIM(RTRIM(fleet_number))) HAVING COUNT(1) > 1) x");
        var duplicateReg = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM (SELECT UPPER(LTRIM(RTRIM(registration_number))) v FROM vehicle_master WHERE is_deleted = 0 AND registration_number IS NOT NULL AND LTRIM(RTRIM(registration_number)) <> '' GROUP BY UPPER(LTRIM(RTRIM(registration_number))) HAVING COUNT(1) > 1) x");
        if (await HasColumnAsync(dbContext, "vehicle_master", "fuel_type_code"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                UPDATE vm
                SET fuel_type_code = CASE WHEN vm.type_code = 1 THEN 2 ELSE 1 END
                FROM vehicle_master vm
                LEFT JOIN fuel_type ft ON ft.fuel_type_code = vm.fuel_type_code
                WHERE vm.is_deleted = 0
                  AND (vm.fuel_type_code IS NULL OR ft.fuel_type_code IS NULL);");
        }

        var unresolvedCodesSql = await HasColumnAsync(dbContext, "vehicle_master", "fuel_type_code")
            ? "SELECT COUNT(1) FROM vehicle_master vm LEFT JOIN site s ON s.Site_code = vm.location_code LEFT JOIN model m ON m.model_code = vm.model_code LEFT JOIN type t ON t.type_code = vm.type_code LEFT JOIN vehicle_status vs ON vs.vehicle_status_code = vm.vehicle_status_code LEFT JOIN fuel_type ft ON ft.fuel_type_code = vm.fuel_type_code WHERE vm.is_deleted = 0 AND (s.Site_code IS NULL OR m.model_code IS NULL OR t.type_code IS NULL OR vs.vehicle_status_code IS NULL OR ft.fuel_type_code IS NULL)"
            : "SELECT COUNT(1) FROM vehicle_master vm LEFT JOIN site s ON s.Site_code = vm.location_code LEFT JOIN model m ON m.model_code = vm.model_code LEFT JOIN type t ON t.type_code = vm.type_code LEFT JOIN vehicle_status vs ON vs.vehicle_status_code = vm.vehicle_status_code WHERE vm.is_deleted = 0 AND (s.Site_code IS NULL OR m.model_code IS NULL OR t.type_code IS NULL OR vs.vehicle_status_code IS NULL)";
        var unresolvedCodes = await QueryCountAsync(dbContext, unresolvedCodesSql);
        var vehiclesWithoutHistory = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master vm WHERE vm.is_deleted = 0 AND NOT EXISTS (SELECT 1 FROM vehicle_history vh WHERE vh.hist_vmf_code = vm.vmf_code AND vh.is_deleted = 0)");
        var pendingCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE is_deleted = 0 AND contract_status_code = 1");
        var activeCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE is_deleted = 0 AND contract_status_code = 3 AND still_current = 'Y'");
        var closedCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE is_deleted = 0 AND (contract_status_code = 7 OR still_current = 'N')");
        var expiredCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE is_deleted = 0 AND end_date < CAST(GETDATE() AS date)");

        Console.WriteLine($"  📊 Validation | Missing GG: {missingGg} | Missing Registration: {missingReg}");
        Console.WriteLine($"  📊 Validation | Duplicate GG groups: {duplicateGg} | Duplicate Registration groups: {duplicateReg}");
        Console.WriteLine($"  📊 Validation | Vehicles with unresolved lookups: {unresolvedCodes} | Vehicles without history: {vehiclesWithoutHistory}");
        Console.WriteLine($"  📊 Validation | Contracts -> Pending: {pendingCount}, Active: {activeCount}, Closed: {closedCount}, Expired: {expiredCount}");
    }

    private static async Task ApplyBatch2OperationalCoverageAsync(FisDbContext dbContext)
    {
        // Batch 2 tables:
        // Fuel_card, logbook, Tracking, Monitor, VehiclePhotoInfo, Fines, accident, Towing, vehicle_remarks, vehicle_documents

        await dbContext.Database.ExecuteSqlRawAsync(@"
            -- Fuel_card: ensure a card for vehicles missing one
            ;WITH missing_cards AS (
                SELECT vm.vmf_code,
                       vm.fleet_number,
                       vm.location_code,
                       ROW_NUMBER() OVER (ORDER BY vm.vmf_code) AS rn
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND vm.vmf_code > 0
                  AND NOT EXISTS (
                      SELECT 1 FROM Fuel_card fc
                      WHERE fc.vmf_code = vm.vmf_code AND fc.is_deleted = 0
                  )
            )
            INSERT INTO Fuel_card
            (
                vmf_code, card_number, PAN_number, PetReceiver, PetRecTel,
                PetTaken, PetExpire, PetComment, LinkGGNum, Status_date, Petrecsite,
                date_created, created_by_user_code, is_deleted
            )
            SELECT m.vmf_code,
                   RIGHT(CONCAT('000000000000000', CAST(500000000000000 + m.vmf_code AS varchar(20))), 15),
                   RIGHT(CONCAT('000000000000000', CAST(600000000000000 + m.vmf_code AS varchar(20))), 15),
                   'Fleet Office',
                   '0110000000',
                   GETDATE(),
                   DATEADD(year, 3, GETDATE()),
                   'Auto-seeded for frontend coverage',
                   LEFT(COALESCE(m.fleet_number, CONCAT('GGX', m.vmf_code)), 10),
                   GETDATE(),
                   m.location_code,
                   GETDATE(),
                   1,
                   0
            FROM missing_cards m;

            -- logbook: ensure at least one active logbook per vehicle
            INSERT INTO logbook
            (
                vmf_code, begin_num, end_num, handout_date, site_code, lb_receiver_name, lb_tel_num, lb_comment,
                date_created, created_by_user_code, is_deleted
            )
            SELECT vm.vmf_code,
                   CONCAT('LB', RIGHT('000000' + CAST(vm.vmf_code AS varchar(10)), 6), '-001'),
                   CONCAT('LB', RIGHT('000000' + CAST(vm.vmf_code AS varchar(10)), 6), '-100'),
                   DATEADD(day, -30, GETDATE()),
                   vm.location_code,
                   'Fleet Clerk',
                   '0110000000',
                   'Auto-seeded logbook',
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM logbook lb
                  WHERE lb.vmf_code = vm.vmf_code AND lb.is_deleted = 0
              );

            -- Tracking: ensure at least one tracking row per vehicle
            INSERT INTO Tracking
            (
                vmf_code, track_num, track_status, track_type, install_date, track_note,
                date_created, created_by_user_code, is_deleted
            )
            SELECT vm.vmf_code,
                   CONCAT('TRK-', RIGHT('000000' + CAST(vm.vmf_code AS varchar(10)), 6)),
                   'Active',
                   'GPS',
                   DATEADD(day, -90, GETDATE()),
                   'Auto-seeded tracker',
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM Tracking t
                  WHERE t.vmf_code = vm.vmf_code AND t.is_deleted = 0
              );

            -- Monitor: at least one monitor/inquiry row per vehicle
            INSERT INTO Monitor
            (
                vmf_code, Capture_dat, User_access_code, Inquiry_type, Inquiry_Desc, Driver_name, Driver_Site,
                date_created, created_by_user_code, is_deleted
            )
            SELECT vm.vmf_code,
                   DATEADD(day, -5, GETDATE()),
                   1,
                   'Routine Check',
                   'Auto-seeded monitor coverage event',
                   'Unknown Driver',
                   vm.location_code,
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM Monitor mo
                  WHERE mo.vmf_code = vm.vmf_code AND mo.is_deleted = 0
              );

            -- Vehicle photos: one representative photo record per vehicle
            INSERT INTO VehiclePhotoInfo
            (
                VehicleMasterCode, FileUrl, Description, Orientation,
                date_created, created_by_user_code, is_deleted
            )
            SELECT vm.vmf_code,
                   CONCAT('https://example.com/photos/', LOWER(REPLACE(COALESCE(vm.fleet_number, CONCAT('ggx', vm.vmf_code)), ' ', '')), '.jpg'),
                   'Auto-seeded vehicle image',
                   1,
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM VehiclePhotoInfo vp
                  WHERE vp.VehicleMasterCode = vm.vmf_code AND vp.is_deleted = 0
              );

            -- Fines: seed across first subset of vehicles for realistic module data
            INSERT INTO Fines
            (
                vmf_code, Offence_date, Offence_reference, Offence_issuer, Fine_amount, Site_code, Offence_name, Pay_due_date,
                date_created, created_by_user_code, is_deleted
            )
            SELECT TOP 40
                   vm.vmf_code,
                   DATEADD(day, -((vm.vmf_code % 45) + 5), GETDATE()),
                   CONCAT('FINE-', vm.vmf_code),
                   CASE WHEN vm.vmf_code % 2 = 0 THEN 'JMPD' ELSE 'TMPD' END,
                   CAST((250 + (vm.vmf_code % 8) * 150) AS decimal(18,2)),
                   vm.location_code,
                   'Speeding',
                   DATEADD(day, 14, GETDATE()),
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM Fines f
                  WHERE f.vmf_code = vm.vmf_code AND f.is_deleted = 0
              )
            ORDER BY vm.vmf_code;

            -- Accident: seed across subset to populate reports/details
            INSERT INTO accident
            (
                vmf_code, description, driver_name, occurence_date, occurence_time, reported_date, claim_amount, excess_amount,
                date_created, created_by_user_code, is_deleted
            )
            SELECT TOP 25
                   vm.vmf_code,
                   'Auto-seeded accident record',
                   'Unknown Driver',
                   DATEADD(day, -((vm.vmf_code % 120) + 10), GETDATE()),
                   DATEADD(day, -((vm.vmf_code % 120) + 10), GETDATE()),
                   DATEADD(day, -((vm.vmf_code % 120) + 8), GETDATE()),
                   CAST((3500 + (vm.vmf_code % 10) * 700) AS decimal(18,2)),
                   CAST((500 + (vm.vmf_code % 4) * 250) AS decimal(18,2)),
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM accident a
                  WHERE a.vmf_code = vm.vmf_code AND a.is_deleted = 0
              )
            ORDER BY vm.vmf_code DESC;

            -- Towing: seed across subset
            INSERT INTO Towing
            (
                vmf_code, Call_refer, Tow_request_date, Tow_request_time, Tow_location_start, Vehicle_problem, Keys, Site_code,
                date_created, created_by_user_code, is_deleted
            )
            SELECT TOP 25
                   vm.vmf_code,
                   CAST(7000 + vm.vmf_code AS decimal(18,2)),
                   DATEADD(day, -((vm.vmf_code % 60) + 3), GETDATE()),
                   DATEADD(day, -((vm.vmf_code % 60) + 3), GETDATE()),
                   CONCAT('Site ', vm.location_code, ' Yard'),
                   'Breakdown',
                   'With Driver',
                   vm.location_code,
                   GETDATE(),
                   1,
                   0
            FROM vehicle_master vm
            WHERE vm.is_deleted = 0
              AND NOT EXISTS (
                  SELECT 1 FROM Towing tw
                  WHERE tw.vmf_code = vm.vmf_code AND tw.is_deleted = 0
              )
            ORDER BY vm.vmf_code;

            -- vehicle_remarks
            IF OBJECT_ID('vehicle_remarks', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_remarks
                (
                    vmf_code, remark_category, remark_text, is_resolved, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 50
                       vm.vmf_code,
                       CASE WHEN vm.vmf_code % 3 = 0 THEN 'General' WHEN vm.vmf_code % 3 = 1 THEN 'Missing' ELSE 'UnderInvestigation' END,
                       'Auto-seeded operational remark for testing',
                       0,
                       GETDATE(),
                       1,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM vehicle_remarks vr
                      WHERE vr.vmf_code = vm.vmf_code AND vr.is_deleted = 0 AND vr.is_resolved = 0
                  )
                ORDER BY vm.vmf_code;
            END

            -- vehicle_documents
            IF OBJECT_ID('vehicle_documents', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_documents
                (
                    vmf_code, document_category, document_description, original_file_name, stored_file_path, mime_type, file_size_bytes,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 80
                       vm.vmf_code,
                       CASE WHEN vm.vmf_code % 4 = 0 THEN 'Licence' WHEN vm.vmf_code % 4 = 1 THEN 'Accident' WHEN vm.vmf_code % 4 = 2 THEN 'Fine' ELSE 'General' END,
                       'Auto-seeded document metadata',
                       CONCAT('doc_', vm.vmf_code, '.pdf'),
                       CONCAT('seeded/', vm.vmf_code, '/doc_', vm.vmf_code, '.pdf'),
                       'application/pdf',
                       102400,
                       GETDATE(),
                       1,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM vehicle_documents vd
                      WHERE vd.vmf_code = vm.vmf_code AND vd.is_deleted = 0
                  )
                ORDER BY vm.vmf_code;
            END
        ");

        Console.WriteLine("  ✓ Batch 2 operational coverage updates applied.");

        var vehiclesWithFuelCard = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM Fuel_card WHERE is_deleted = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithLogbook = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM logbook WHERE is_deleted = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithTracking = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM Tracking WHERE is_deleted = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithMonitor = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM Monitor WHERE is_deleted = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithPhoto = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT VehicleMasterCode) FROM VehiclePhotoInfo WHERE is_deleted = 0 AND VehicleMasterCode IS NOT NULL");
        var finesCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Fines WHERE is_deleted = 0");
        var accidentCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM accident WHERE is_deleted = 0");
        var towingCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Towing WHERE is_deleted = 0");
        var hasVehicleRemarks = await HasTableAsync(dbContext, "vehicle_remarks");
        var hasVehicleDocuments = await HasTableAsync(dbContext, "vehicle_documents");
        var remarkCount = hasVehicleRemarks
            ? await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_remarks WHERE is_deleted = 0")
            : 0;
        var documentCount = hasVehicleDocuments
            ? await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_documents WHERE is_deleted = 0")
            : 0;

        Console.WriteLine($"  📊 Batch 2 | Vehicles with Fuel Card: {vehiclesWithFuelCard} | Logbook: {vehiclesWithLogbook} | Tracking: {vehiclesWithTracking} | Monitor: {vehiclesWithMonitor} | Photo: {vehiclesWithPhoto}");
        Console.WriteLine($"  📊 Batch 2 | Fines: {finesCount} | Accidents: {accidentCount} | Towing: {towingCount} | Remarks: {remarkCount} | Documents: {documentCount}");
    }

    private static async Task ApplyBatch3FinanceBillingCoverageAsync(FisDbContext dbContext)
    {
        // Step 1: Reference data (financial_system, cost_category, posting years + months for 24 months)
        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @yearNow   SMALLINT = CAST(YEAR(GETDATE())   AS SMALLINT);
            DECLARE @yearPrev  SMALLINT = CAST(YEAR(GETDATE())-1 AS SMALLINT);
            DECLARE @monthNow  TINYINT  = CAST(MONTH(GETDATE())  AS TINYINT);

            -- Financial systems
            IF OBJECT_ID('financial_system','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM financial_system WHERE financial_system_code = 1)
                    INSERT INTO financial_system (financial_system_code, financial_system_name, date_created, is_deleted)
                    VALUES (1, 'BAS', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM financial_system WHERE financial_system_code = 2)
                    INSERT INTO financial_system (financial_system_code, financial_system_name, date_created, is_deleted)
                    VALUES (2, 'Pastel', GETDATE(), 0);
            END

            -- Cost categories
            IF OBJECT_ID('cost_category','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM cost_category WHERE cost_category_code = 1)
                    INSERT INTO cost_category (cost_category_code, description, vat_recoverable, cpk_contribution, date_created, is_deleted)
                    VALUES (1, 'Tariff Charge', 'N', 'Y', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM cost_category WHERE cost_category_code = 2)
                    INSERT INTO cost_category (cost_category_code, description, vat_recoverable, cpk_contribution, date_created, is_deleted)
                    VALUES (2, 'Fuel Cost', 'Y', 'N', GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM cost_category WHERE cost_category_code = 3)
                    INSERT INTO cost_category (cost_category_code, description, vat_recoverable, cpk_contribution, date_created, is_deleted)
                    VALUES (3, 'Maintenance Cost', 'Y', 'N', GETDATE(), 0);
            END

            -- Posting years: previous year + current year
            IF OBJECT_ID('posting_year','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM posting_year WHERE year_start_date = DATEFROMPARTS(@yearPrev,1,1) AND is_deleted = 0)
                    INSERT INTO posting_year (description, year_start_date, year_end_date, date_created, is_deleted)
                    VALUES (CONCAT('Posting Year ',@yearPrev), DATEFROMPARTS(@yearPrev,1,1), DATEFROMPARTS(@yearPrev,12,31), GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM posting_year WHERE year_start_date = DATEFROMPARTS(@yearNow,1,1) AND is_deleted = 0)
                    INSERT INTO posting_year (description, year_start_date, year_end_date, date_created, is_deleted)
                    VALUES (CONCAT('Posting Year ',@yearNow), DATEFROMPARTS(@yearNow,1,1), DATEFROMPARTS(@yearNow,12,31), GETDATE(), 0);
            END

            -- Financial years
            IF OBJECT_ID('financial_year','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM financial_year WHERE start_date = DATEFROMPARTS(@yearPrev,1,1) AND is_deleted = 0)
                    INSERT INTO financial_year (financial_year, start_date, end_date, date_created, is_deleted)
                    VALUES (CONCAT(@yearPrev,'/',@yearNow), DATEFROMPARTS(@yearPrev,1,1), DATEFROMPARTS(@yearPrev,12,31), GETDATE(), 0);
                IF NOT EXISTS (SELECT 1 FROM financial_year WHERE start_date = DATEFROMPARTS(@yearNow,1,1) AND is_deleted = 0)
                    INSERT INTO financial_year (financial_year, start_date, end_date, date_created, is_deleted)
                    VALUES (CONCAT(@yearNow,'/',@yearNow+1), DATEFROMPARTS(@yearNow,1,1), DATEFROMPARTS(@yearNow,12,31), GETDATE(), 0);
            END

            -- Posting months: all 12 months for both years
            IF OBJECT_ID('posting_month','U') IS NOT NULL
            BEGIN
                ;WITH months AS (
                    SELECT 1 n,'January'   nm UNION ALL SELECT 2,'February'  UNION ALL SELECT 3,'March'
                    UNION ALL  SELECT 4,'April'     UNION ALL SELECT 5,'May'        UNION ALL SELECT 6,'June'
                    UNION ALL  SELECT 7,'July'      UNION ALL SELECT 8,'August'     UNION ALL SELECT 9,'September'
                    UNION ALL  SELECT 10,'October'  UNION ALL SELECT 11,'November'  UNION ALL SELECT 12,'December'
                ),
                years AS (
                    SELECT py.posting_year_code, YEAR(py.year_start_date) yr
                    FROM posting_year py
                    WHERE py.is_deleted = 0
                      AND YEAR(py.year_start_date) IN (@yearPrev, @yearNow)
                )
                INSERT INTO posting_month (posting_year_code, month_number, month_name, is_closed, date_created, is_deleted)
                SELECT y.posting_year_code,
                       CAST(m.n AS TINYINT),
                       m.nm,
                       CASE WHEN y.yr < @yearNow THEN 1
                            WHEN y.yr = @yearNow AND m.n < @monthNow THEN 1
                            ELSE 0 END,
                       GETDATE(),
                       0
                FROM years y
                CROSS JOIN months m
                WHERE NOT EXISTS (
                    SELECT 1 FROM posting_month pm
                    WHERE pm.posting_year_code = y.posting_year_code
                      AND pm.month_number = m.n
                );
            END

            -- Batches: one per month for the past 24 months
            IF OBJECT_ID('batch','U') IS NOT NULL
            BEGIN
                DECLARE @bi INT = 0;
                WHILE @bi < 24
                BEGIN
                    DECLARE @bdate DATE = CAST(DATEADD(month,-@bi,GETDATE()) AS DATE);
                    IF NOT EXISTS (SELECT 1 FROM batch WHERE batch_header_date = CONVERT(varchar(10),@bdate,120) AND is_deleted = 0)
                        INSERT INTO batch (batch_date, batch_turnover, batch_header_date, batch_header_time, financial_system_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@bdate, 0.00, CONVERT(varchar(10),@bdate,120), '08:00:00', 1, GETDATE(), 1, 0);
                    SET @bi = @bi + 1;
                END
            END
        ");

        // Step 2: For every posting month in the last 24 months, create invoices per department
        //         and invoice_items + daily_transactions per active-contract vehicle.
        //         We loop month-by-month in C# to keep individual SQL statements simple.
        for (int mOffset = 23; mOffset >= 0; mOffset--)
        {
            var targetDate = DateTime.Now.AddMonths(-mOffset);
            int targetYear = targetDate.Year;
            int targetMonth = targetDate.Month;

            await dbContext.Database.ExecuteSqlRawAsync(@"
                DECLARE @ty INT = {0};
                DECLARE @tm INT = {1};
                DECLARE @txDate DATE = DATEFROMPARTS(@ty, @tm, 15);

                -- Find posting_month_code for this year/month
                DECLARE @pmCode INT = (
                    SELECT pm.posting_month_code
                    FROM posting_month pm
                    JOIN posting_year py ON py.posting_year_code = pm.posting_year_code
                    WHERE YEAR(py.year_start_date) = @ty
                      AND pm.month_number = @tm
                      AND pm.is_deleted = 0
                );

                IF @pmCode IS NOT NULL
                BEGIN

                -- Invoice per department (only depts that have active-contract vehicles)
                INSERT INTO invoice (posting_month_code, department_code, date_created, created_by_user_code, is_deleted)
                SELECT @pmCode,
                       CAST(s.Depatrment_code AS SMALLINT),
                       GETDATE(), 1, 0
                FROM (
                    SELECT DISTINCT s.Depatrment_code
                    FROM contract c
                    JOIN vehicle_master vm ON vm.vmf_code = c.vmf_code AND vm.is_deleted = 0
                    JOIN site s ON s.Site_code = vm.location_code AND s.is_deleted = 0
                    WHERE c.still_current = 'Y' AND c.is_deleted = 0
                      AND s.Depatrment_code IS NOT NULL
                ) s
                WHERE NOT EXISTS (
                    SELECT 1 FROM invoice i
                    WHERE i.posting_month_code = @pmCode
                      AND i.department_code = CAST(s.Depatrment_code AS SMALLINT)
                      AND i.is_deleted = 0
                );

                -- invoice_item per vehicle per invoice for this month
                INSERT INTO invoice_item
                (invoice_code, vmf_code, site_code,
                 fixed_tariff_amount, start_odometer, start_odo_derived, start_odo_date,
                 end_odometer, end_odo_derived, end_odo_date,
                 odo_tariff_amount, driver_rate,
                 contract_start_date, contract_end_date,
                 contract_type, contract_start_time, contract_end_time,
                 date_created, created_by_user_code, is_deleted)
                SELECT i.invoice_code,
                       c.vmf_code,
                       CAST(vm.location_code AS SMALLINT),
                       ISNULL(lt.fixed_tariff, 3500.00),
                       ISNULL(c.start_odometer, 0),
                       'Captured',
                       @txDate,
                       ISNULL(c.start_odometer, 0) + ISNULL(c.monthly_km, 1500),
                       'Estimated',
                       @txDate,
                       CAST(ISNULL(c.monthly_km, 1500) * 0.60 AS DECIMAL(18,2)),
                       0.00,
                       c.start_date,
                       c.end_date,
                       ISNULL(NULLIF(c.contract_type,''), 'H'),
                       c.start_time,
                       c.end_time,
                       GETDATE(), 1, 0
                FROM contract c
                JOIN vehicle_master vm ON vm.vmf_code = c.vmf_code AND vm.is_deleted = 0
                JOIN site s            ON s.Site_code  = vm.location_code AND s.is_deleted = 0
                JOIN invoice i         ON i.posting_month_code = @pmCode
                                     AND i.department_code = CAST(s.Depatrment_code AS SMALLINT)
                                     AND i.is_deleted = 0
                LEFT JOIN LeaseTariff lt ON lt.vmf_code = c.vmf_code
                                       AND lt.active = 1 AND lt.is_deleted = 0
                WHERE c.still_current = 'Y' AND c.is_deleted = 0
                  AND s.Depatrment_code IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM invoice_item ii
                      WHERE ii.invoice_code = i.invoice_code
                        AND ii.vmf_code = c.vmf_code
                        AND ii.is_deleted = 0
                  );

                -- daily_transactions: one tariff transaction per vehicle per month
                INSERT INTO daily_transactions
                (vmf_code, cost_category_code, posting_month_code,
                 file_sequence_number, transaction_date,
                 date_created, created_by_user_code, is_deleted)
                SELECT c.vmf_code, 1, @pmCode, 1, @txDate,
                       GETDATE(), 1, 0
                FROM contract c
                WHERE c.still_current = 'Y' AND c.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM daily_transactions dt
                      WHERE dt.vmf_code = c.vmf_code
                        AND dt.posting_month_code = @pmCode
                        AND dt.is_deleted = 0
                  );

                -- Journal entries for batches in this month
                IF OBJECT_ID('journal','U') IS NOT NULL
                BEGIN
                    INSERT INTO journal (batch_code, journal_date, journal_installation_link, date_created, created_by_user_code, is_deleted)
                    SELECT b.batch_code, @txDate, CONCAT('BATCH-',b.batch_code), GETDATE(), 1, 0
                    FROM batch b
                    WHERE YEAR(CAST(b.batch_header_date AS DATE)) = @ty
                      AND MONTH(CAST(b.batch_header_date AS DATE)) = @tm
                      AND b.is_deleted = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM journal j
                          WHERE j.batch_code = b.batch_code AND j.is_deleted = 0
                      );
                END

                END -- IF @pmCode IS NOT NULL
            ", targetYear, targetMonth);
        }

        Console.WriteLine("  ✓ Batch 3 finance billing coverage updated (24 months backdated).");

        var financialSystemCount   = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM financial_system WHERE is_deleted = 0");
        var postingYearCount       = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM posting_year WHERE is_deleted = 0");
        var postingMonthCount      = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM posting_month WHERE is_deleted = 0");
        var invoiceCount           = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM invoice WHERE is_deleted = 0");
        var invoiceItemCount       = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM invoice_item WHERE is_deleted = 0");
        var dailyTransactionCount  = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM daily_transactions WHERE is_deleted = 0");
        var journalCount           = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal WHERE is_deleted = 0");
        var activeContractCount    = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE is_deleted = 0 AND still_current = 'Y'");
        var billedVehicleCount     = await QueryCountAsync(dbContext, @"
            SELECT COUNT(DISTINCT c.vmf_code) FROM contract c
            WHERE c.is_deleted = 0 AND c.still_current = 'Y'
              AND EXISTS (SELECT 1 FROM invoice_item ii WHERE ii.vmf_code = c.vmf_code AND ii.is_deleted = 0)");

        Console.WriteLine($"  📊 Batch 3 | Financial Systems: {financialSystemCount} | Posting Years: {postingYearCount} | Posting Months: {postingMonthCount}");
        Console.WriteLine($"  📊 Batch 3 | Invoices: {invoiceCount} | Invoice Items: {invoiceItemCount} | Daily Tx: {dailyTransactionCount} | Journals: {journalCount}");
        Console.WriteLine($"  📊 Batch 3 | Active Contracts: {activeContractCount} | Billed Vehicles (all months): {billedVehicleCount}");
    }

    private static async Task ApplyBatch4TripWorkshopVerificationCoverageAsync(FisDbContext dbContext)
    {
        // Batch 4 tables:
        // trip_authorities, trip_driver, trip_passengers, bookings, Collection,
        // job_cards, workshop, Asset_Verification, vehicle_assessment, Vehicle_Damages

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultTripTypeCode smallint = 1;
            DECLARE @defaultIncidentTypeCode smallint = 1;
            DECLARE @defaultDriverLicenceTypeId int = 1;
            DECLARE @defaultExtraCode smallint = 1;
            DECLARE @defaultClassCode smallint = 1;
            DECLARE @defaultLocationCode int = NULL;
            DECLARE @defaultUserCode int = 1;

            IF OBJECT_ID('trip_type', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultTripTypeCode = CAST(trip_type_code AS smallint)
                FROM trip_type
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY trip_type_code;
            END

            IF OBJECT_ID('trip_incident_type', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultIncidentTypeCode = CAST(trip_incident_type_code AS smallint)
                FROM trip_incident_type
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY trip_incident_type_code;
            END

            IF OBJECT_ID('driver_licence_types', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultDriverLicenceTypeId = driver_licence_type_id
                FROM driver_licence_types
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY driver_licence_type_id;
            END

            IF OBJECT_ID('extra_code', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultExtraCode = CAST(extra_code AS smallint)
                FROM extra_code
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY extra_code;
            END

            IF OBJECT_ID('class', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultClassCode = CAST(class_code AS smallint)
                FROM class
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY class_code;
            END

            IF OBJECT_ID('location', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultLocationCode = location_code
                FROM location
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY location_code;
            END

            IF OBJECT_ID('TS_Users', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultUserCode = user_access_code
                FROM TS_Users
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY user_access_code;
            END

            IF OBJECT_ID('trip_authorities', 'U') IS NOT NULL
            BEGIN
                ;WITH missing AS (
                    SELECT TOP 80 c.contract_code, c.vmf_code
                    FROM contract c
                    WHERE c.is_deleted = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM trip_authorities ta
                          WHERE ta.contract_code = c.contract_code
                            AND ta.is_deleted = 0
                      )
                    ORDER BY c.contract_code
                )
                INSERT INTO trip_authorities
                (
                    contract_code, trip_request_number, issue_date, trip_type_code, trip_incident_type_code, user_access_code,
                    locked_for_transfer, Trip_Is_Monthly, trip_reason, date_created, created_by_user_code, is_deleted
                )
                SELECT m.contract_code,
                       CONCAT('TA-', m.contract_code),
                       GETDATE(),
                       ISNULL(@defaultTripTypeCode, 1),
                       ISNULL(@defaultIncidentTypeCode, 1),
                       CAST(ISNULL(@defaultUserCode, 1) AS smallint),
                       0,
                       CASE WHEN m.vmf_code % 4 = 0 THEN 1 ELSE 0 END,
                       'Auto-seeded trip authority for module coverage',
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM missing m;
            END

            IF OBJECT_ID('trip_driver', 'U') IS NOT NULL
            BEGIN
                ;WITH missing AS (
                    SELECT TOP 100 ta.trip_authority_code, ta.contract_code
                    FROM trip_authorities ta
                    WHERE ISNULL(ta.is_deleted, 0) = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM trip_driver td
                          WHERE td.trip_authority_code = ta.trip_authority_code
                            AND ISNULL(td.is_deleted, 0) = 0
                      )
                    ORDER BY ta.trip_authority_code
                )
                INSERT INTO trip_driver
                (
                    trip_authority_code, trip_driver_name, trip_driver_id, trip_driver_primary, site_code,
                    driver_licence_type_id, driver_passportnumber, driver_persalnumber, driver_contractnumber,
                    driver_licence_number, driver_licence_issuedate, driver_licence_lastVerifiedDate, driver_hasPDP,
                    driver_PDP_ExpiryDate, driver_licence_ExpiryDate, driver_active,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT m.trip_authority_code,
                       CONCAT('Driver ', m.trip_authority_code),
                       RIGHT(CONCAT('0000000000000', CAST(9000000000000 + m.trip_authority_code AS varchar(20))), 13),
                       1,
                       c.site_code,
                       @defaultDriverLicenceTypeId,
                       CONCAT('P', m.trip_authority_code),
                       CONCAT('PER', m.trip_authority_code),
                       CONCAT('CON', m.trip_authority_code),
                       CONCAT('LIC', m.trip_authority_code),
                       DATEADD(year, -3, GETDATE()),
                       GETDATE(),
                       1,
                       DATEADD(year, 2, GETDATE()),
                       DATEADD(year, 4, GETDATE()),
                       1,
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM missing m
                LEFT JOIN contract c ON c.contract_code = m.contract_code;
            END

            IF OBJECT_ID('trip_passengers', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 60 vmf_code,
                           ROW_NUMBER() OVER (ORDER BY vmf_code) AS rn
                    FROM vehicle_master
                    WHERE is_deleted = 0
                    ORDER BY vmf_code
                )
                INSERT INTO trip_passengers
                (
                    trip_passenger_name, date_created, created_by_user_code, is_deleted
                )
                SELECT CONCAT('Passenger ', s.vmf_code),
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src s
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM trip_passengers tp
                    WHERE tp.trip_passenger_name = CONCAT('Passenger ', s.vmf_code)
                      AND ISNULL(tp.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('bookings', 'U') IS NOT NULL AND @defaultLocationCode IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 60 vm.vmf_code, vm.location_code, vm.fleet_number
                    FROM vehicle_master vm
                    WHERE vm.is_deleted = 0
                      AND vm.vmf_code > 0
                    ORDER BY vm.vmf_code
                )
                INSERT INTO bookings
                (
                    site_code, name, start_date, end_date, class_code, user_id, booking_date, telephone, collected,
                    location_code, vmf_code, booking_status, notes, date_created, created_by_user_code, is_deleted
                )
                SELECT CAST(ISNULL(src.location_code, 1) AS varchar(20)),
                       CONCAT('Booking for ', COALESCE(src.fleet_number, CONCAT('GGX', src.vmf_code))),
                       DATEADD(day, -1, GETDATE()),
                       DATEADD(day, 2, GETDATE()),
                       ISNULL(@defaultClassCode, 1),
                       CAST(ISNULL(@defaultUserCode, 1) AS smallint),
                       GETDATE(),
                       '0110000000',
                       0,
                       @defaultLocationCode,
                       src.vmf_code,
                       'Booked',
                       'Auto-seeded booking for coverage',
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1 FROM bookings b
                    WHERE b.vmf_code = src.vmf_code
                      AND ISNULL(b.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('Collection', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 60 vm.vmf_code, vm.location_code, vm.fleet_number
                    FROM vehicle_master vm
                    WHERE vm.is_deleted = 0
                    ORDER BY vm.vmf_code
                )
                INSERT INTO Collection
                (
                    Sessionid, vmf_code, site_code, fleet_number, date_created, created_by_user_code, is_deleted
                )
                SELECT CONCAT('AUTO-', src.vmf_code),
                       src.vmf_code,
                       src.location_code,
                       COALESCE(src.fleet_number, CONCAT('GGX', src.vmf_code)),
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM Collection c
                    WHERE c.vmf_code = src.vmf_code
                      AND ISNULL(c.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('job_cards', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 100 vm.vmf_code
                    FROM vehicle_master vm
                    WHERE vm.is_deleted = 0
                    ORDER BY vm.vmf_code
                )
                INSERT INTO job_cards
                (
                    vmf_code, extra_code, status_code, priority, assigned_to, assigned_date, jcs_comment, damages, comments,
                    reviewed, labour_cost, parts_cost, other_cost, total_cost, invoice_number, invoice_date, service_provider,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT src.vmf_code,
                       ISNULL(@defaultExtraCode, 1),
                       CASE WHEN src.vmf_code % 5 = 0 THEN 3 ELSE 1 END,
                       CASE WHEN src.vmf_code % 3 = 0 THEN 'H' ELSE 'N' END,
                       ISNULL(@defaultUserCode, 1),
                       GETDATE(),
                       'Auto-seeded job card',
                       'General wear and tear',
                       'Requires inspection',
                       'N',
                       CAST((500 + (src.vmf_code % 10) * 100) AS decimal(10,2)),
                       CAST((300 + (src.vmf_code % 8) * 80) AS decimal(10,2)),
                       CAST((100 + (src.vmf_code % 5) * 25) AS decimal(10,2)),
                       CAST((900 + (src.vmf_code % 10) * 180) AS decimal(10,2)),
                       CONCAT('JC-INV-', src.vmf_code),
                       CAST(GETDATE() AS date),
                       'Fleet Workshop',
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM job_cards jc
                    WHERE jc.vmf_code = src.vmf_code
                      AND ISNULL(jc.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('workshop', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 100 vmf_code
                    FROM vehicle_master
                    WHERE is_deleted = 0
                    ORDER BY vmf_code
                )
                INSERT INTO workshop
                (
                    vmf_code, receive_time, receive_date, complete_time,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT src.vmf_code,
                       DATEADD(hour, -6, GETDATE()),
                       CAST(GETDATE() AS date),
                       DATEADD(hour, 2, GETDATE()),
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM workshop w
                    WHERE w.vmf_code = src.vmf_code
                      AND ISNULL(w.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('Asset_Verification', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 100 vm.vmf_code, vm.registration_number, vm.location_code, s.description AS site_name, d.description AS dept_name
                    FROM vehicle_master vm
                    LEFT JOIN site s ON s.Site_code = vm.location_code
                    LEFT JOIN department d ON d.department_code = s.Depatrment_code
                    WHERE vm.is_deleted = 0
                    ORDER BY vm.vmf_code
                )
                INSERT INTO Asset_Verification
                (
                    province, department_name, site_code, site_name, responsible_manager, tel_no, fax_no, vehicle_reg_no, vmf_code,
                    verification_date, verified_by, verification_status, notes, date_created, created_by_user_code, is_deleted
                )
                SELECT 'Gauteng',
                       COALESCE(src.dept_name, 'Transport'),
                       CAST(ISNULL(src.location_code, 1) AS smallint),
                       COALESCE(src.site_name, 'Head Office'),
                       'Fleet Manager',
                       '0110000000',
                       '0110000001',
                       src.registration_number,
                       src.vmf_code,
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       CASE WHEN src.vmf_code % 4 = 0 THEN 'Missing' ELSE 'Verified' END,
                       'Auto-seeded asset verification',
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM Asset_Verification av
                    WHERE av.vmf_code = src.vmf_code
                      AND ISNULL(av.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('vehicle_assessment', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 100 vmf_code
                    FROM vehicle_master
                    WHERE is_deleted = 0
                    ORDER BY vmf_code
                )
                INSERT INTO vehicle_assessment
                (
                    vmf_code, spare_wheel, jack, wheel_spanner, wheel_lock_key, fuel_card, license_disc, cof_disc, fire_extinguisher,
                    first_aid_kit, assessment_date, notes, date_created, created_by_user_code, is_deleted
                )
                SELECT src.vmf_code,
                       1, 1, 1, 1, 1, 1, 1, 1, 1,
                       GETDATE(),
                       'Auto-seeded vehicle assessment',
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM vehicle_assessment va
                    WHERE va.vmf_code = src.vmf_code
                      AND ISNULL(va.is_deleted, 0) = 0
                );
            END

            IF OBJECT_ID('Vehicle_Damages', 'U') IS NOT NULL
            BEGIN
                ;WITH src AS (
                    SELECT TOP 100 vmf_code
                    FROM vehicle_master
                    WHERE is_deleted = 0
                    ORDER BY vmf_code
                )
                INSERT INTO Vehicle_Damages
                (
                    vmf_code, damage_status, damages_comment, status_date, modified_by_user_access_code,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT src.vmf_code,
                       CASE WHEN src.vmf_code % 6 = 0 THEN 'Y' ELSE 'N' END,
                       CASE WHEN src.vmf_code % 6 = 0 THEN 'Panel scratch / bumper scuff' ELSE 'No visible damage' END,
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       GETDATE(),
                       ISNULL(@defaultUserCode, 1),
                       0
                FROM src
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM Vehicle_Damages vd
                    WHERE vd.vmf_code = src.vmf_code
                      AND ISNULL(vd.is_deleted, 0) = 0
                );
            END
        ");

        Console.WriteLine("  ✓ Batch 4 trip/workshop/verification coverage updates applied.");

        var tripAuthorityCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM trip_authorities WHERE ISNULL(is_deleted, 0) = 0");
        var tripDriverCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM trip_driver WHERE ISNULL(is_deleted, 0) = 0");
        var tripPassengerCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM trip_passengers WHERE ISNULL(is_deleted, 0) = 0");
        var bookingCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM bookings WHERE ISNULL(is_deleted, 0) = 0");
        var collectionCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Collection WHERE ISNULL(is_deleted, 0) = 0");
        var jobCardCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM job_cards WHERE ISNULL(is_deleted, 0) = 0");
        var workshopCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM workshop WHERE ISNULL(is_deleted, 0) = 0");
        var assetVerificationCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Asset_Verification WHERE ISNULL(is_deleted, 0) = 0");
        var vehicleAssessmentCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_assessment WHERE ISNULL(is_deleted, 0) = 0");
        var vehicleDamageCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Vehicle_Damages WHERE ISNULL(is_deleted, 0) = 0");

        var vehiclesWithTrips = await QueryCountAsync(dbContext, @"
            SELECT COUNT(DISTINCT c.vmf_code)
            FROM contract c
            WHERE c.is_deleted = 0
              AND EXISTS (
                  SELECT 1
                  FROM trip_authorities ta
                  WHERE ta.contract_code = c.contract_code
                    AND ISNULL(ta.is_deleted, 0) = 0
              )");
        var vehiclesWithJobCards = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM job_cards WHERE ISNULL(is_deleted, 0) = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithAssessments = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM vehicle_assessment WHERE ISNULL(is_deleted, 0) = 0 AND vmf_code IS NOT NULL");
        var vehiclesWithDamageRows = await QueryCountAsync(dbContext, "SELECT COUNT(DISTINCT vmf_code) FROM Vehicle_Damages WHERE ISNULL(is_deleted, 0) = 0 AND vmf_code IS NOT NULL");

        Console.WriteLine($"  📊 Batch 4 | Trip Authorities: {tripAuthorityCount} | Trip Drivers: {tripDriverCount} | Trip Passengers: {tripPassengerCount} | Bookings: {bookingCount} | Collections: {collectionCount}");
        Console.WriteLine($"  📊 Batch 4 | Job Cards: {jobCardCount} | Workshop: {workshopCount} | Asset Verifications: {assetVerificationCount} | Assessments: {vehicleAssessmentCount} | Damages: {vehicleDamageCount}");
        Console.WriteLine($"  📊 Batch 4 | Vehicles with Trips: {vehiclesWithTrips} | With JobCards: {vehiclesWithJobCards} | With Assessments: {vehiclesWithAssessments} | With Damage Rows: {vehiclesWithDamageRows}");
    }

    private static async Task ApplyBatch5AuditHistoryCoverageAsync(FisDbContext dbContext)
    {
        // Batch 5 tables:
        // contract_status_history, contract_audit_log, vehicle_status_history, vehicle_type_history, vehicle_licence_history,
        // fleet_notes, PreVehicle_Master_notes, TS_Log, error_log, vehicle_history

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = 1;
            DECLARE @defaultContractStatusCode smallint = NULL;
            IF OBJECT_ID('TS_Users', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultUserCode = user_access_code
                FROM TS_Users
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY user_access_code;
            END

            IF OBJECT_ID('contract_status', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultContractStatusCode = contract_status_code
                FROM contract_status
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY contract_status_code;
            END

            IF OBJECT_ID('contract_status_history', 'U') IS NOT NULL AND @defaultContractStatusCode IS NOT NULL
            BEGIN
                INSERT INTO contract_status_history
                (
                    contract_code, contract_status_code, contract_status_description, status_start_date, status_end_date,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT c.contract_code,
                       COALESCE(cs.contract_status_code, @defaultContractStatusCode),
                       COALESCE(cs.status_description, CONCAT('Status ', COALESCE(cs.contract_status_code, @defaultContractStatusCode))),
                       ISNULL(c.start_date, DATEADD(day, -30, GETDATE())),
                       ISNULL(c.end_date, GETDATE()),
                       GETDATE(),
                       ISNULL(c.user_code, @defaultUserCode),
                       0
                FROM contract c
                LEFT JOIN contract_status cs ON cs.contract_status_code = c.contract_status_code AND ISNULL(cs.is_deleted, 0) = 0
                WHERE c.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM contract_status_history csh
                      WHERE csh.contract_code = c.contract_code
                        AND ISNULL(csh.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('contract_audit_log', 'U') IS NOT NULL
            BEGIN
                INSERT INTO contract_audit_log
                (
                    contract_code, action, performed_by_user_code, performed_at, old_status_code, new_status_code,
                    field_changed, old_value, new_value, notes
                )
                SELECT c.contract_code,
                       CASE WHEN c.contract_status_code = 3 THEN 'ApprovedAndActivated'
                            WHEN c.contract_status_code = 1 THEN 'Submitted'
                            WHEN c.contract_status_code = 2 THEN 'Closed'
                            ELSE 'Edited'
                       END,
                       ISNULL(c.approver_code, ISNULL(c.user_code, @defaultUserCode)),
                       GETDATE(),
                       NULL,
                       CAST(c.contract_status_code AS smallint),
                       'contract_status_code',
                       NULL,
                       CAST(c.contract_status_code AS varchar(10)),
                       'Auto-seeded contract audit row'
                FROM contract c
                WHERE c.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM contract_audit_log cal
                      WHERE cal.contract_code = c.contract_code
                  );
            END

            IF OBJECT_ID('vehicle_status_history', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_status_history
                (
                    vmf_code, vehicle_status_code, vehicle_status_description, status_start_date, status_end_date,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT vm.vmf_code,
                       CAST(ISNULL(vm.vehicle_status_code, 1) AS smallint),
                       CONCAT('Status ', ISNULL(vm.vehicle_status_code, 1)),
                       ISNULL(vm.date_created, DATEADD(day, -90, GETDATE())),
                       GETDATE(),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM vehicle_status_history vsh
                      WHERE vsh.vmf_code = vm.vmf_code
                        AND ISNULL(vsh.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('vehicle_type_history', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_type_history
                (
                    vmf_code, type_code, type_start_date, type_end_date, date_created, created_by_user_code, is_deleted
                )
                SELECT vm.vmf_code,
                       CAST(ISNULL(vm.type_code, 1) AS smallint),
                       ISNULL(vm.date_created, DATEADD(day, -90, GETDATE())),
                       NULL,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM vehicle_type_history vth
                      WHERE vth.vmf_code = vm.vmf_code
                        AND ISNULL(vth.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('vehicle_licence_history', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_licence_history
                (
                    vmf_code, licence_due_date, lic_register_number, lic_registration_doc, licence_comments,
                    cof_last_done, cof_required, tare, Licence_receiver, Licence_receiver_id, Licence_receiver_tel,
                    Licence_receiver_site, Licence_date_taken, captured_at, captured_by_user_code, update_notes
                )
                SELECT vm.vmf_code,
                       DATEADD(year, 1, CAST(GETDATE() AS date)),
                       COALESCE(vm.registration_number, CONCAT('REG-', vm.vmf_code)),
                       CONCAT('LICDOC-', vm.vmf_code),
                       'Auto-seeded licence history snapshot',
                       DATEADD(month, -3, CAST(GETDATE() AS date)),
                       'Y',
                       1500 + (vm.vmf_code % 1000),
                       'Fleet Office',
                       CONCAT('ID', vm.vmf_code),
                       '0110000000',
                       CAST(ISNULL(vm.location_code, 1) AS smallint),
                       CAST(GETDATE() AS date),
                       GETDATE(),
                       @defaultUserCode,
                       'Auto seed batch 5'
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM vehicle_licence_history vlh
                      WHERE vlh.vmf_code = vm.vmf_code
                  );
            END

            IF OBJECT_ID('fleet_notes', 'U') IS NOT NULL
            BEGIN
                INSERT INTO fleet_notes
                (
                    vmf_code, fleet_note, date_created, created_by_user_code, is_deleted
                )
                SELECT vm.vmf_code,
                       CONCAT('Auto-seeded fleet note for vehicle ', COALESCE(vm.fleet_number, CONCAT('GGX', vm.vmf_code))),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM fleet_notes fn
                      WHERE fn.vmf_code = vm.vmf_code
                        AND ISNULL(fn.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('PreVehicle_Master_notes', 'U') IS NOT NULL
            BEGIN
                INSERT INTO PreVehicle_Master_notes
                (
                    Temp_vmf_code, Comment_Date, Commented_By_User_Code, Comment, is_deleted
                )
                SELECT pvm.temp_vmf_code,
                       GETDATE(),
                       @defaultUserCode,
                       'Auto-seeded pre-vehicle note',
                       0
                FROM pre_vehicle_master pvm
                WHERE ISNULL(pvm.is_deleted, 0) = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM PreVehicle_Master_notes pvn
                      WHERE pvn.Temp_vmf_code = pvm.temp_vmf_code
                        AND ISNULL(pvn.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('TS_Log', 'U') IS NOT NULL
            BEGIN
                INSERT INTO TS_Log
                (
                    TSDate, TSTime, user_access_code, SiteCode, ErrorCode, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 50
                       CAST(GETDATE() AS date),
                       GETDATE(),
                       CAST(@defaultUserCode AS smallint),
                       CAST(ISNULL(vm.location_code, 1) AS smallint),
                       CONCAT('INFO-', vm.vmf_code),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM TS_Log tsl
                      WHERE tsl.ErrorCode = CONCAT('INFO-', vm.vmf_code)
                        AND ISNULL(tsl.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('error_log', 'U') IS NOT NULL
            BEGIN
                INSERT INTO error_log
                (
                    description, data, log_date, log_time, log_read, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 30
                       CONCAT('Auto-seeded diagnostic log for vmf_code ', vm.vmf_code),
                       CONCAT('{{""vmf_code"":', vm.vmf_code, ', ""source"":""batch5""}}'),
                       CAST(GETDATE() AS date),
                       GETDATE(),
                       'N',
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM error_log el
                      WHERE el.description = CONCAT('Auto-seeded diagnostic log for vmf_code ', vm.vmf_code)
                        AND ISNULL(el.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('vehicle_history', 'U') IS NOT NULL
            BEGIN
                INSERT INTO vehicle_history
                (
                    hist_vmf_code, hist_vehicle_status_code, hist_date_changed, hist_user_access_code, hist_fleet_number,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT vm.vmf_code,
                       ISNULL(vm.vehicle_status_code, 1),
                       GETDATE(),
                       CAST(@defaultUserCode AS smallint),
                       vm.fleet_number,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM vehicle_history vh
                      WHERE vh.hist_vmf_code = vm.vmf_code
                        AND vh.hist_date_changed > DATEADD(hour, -1, GETDATE())
                        AND ISNULL(vh.is_deleted, 0) = 0
                  );
            END
        ");

        Console.WriteLine("  ✓ Batch 5 audit/history coverage updates applied.");

        var contractStatusHistoryCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract_status_history WHERE ISNULL(is_deleted, 0) = 0");
        var contractAuditLogCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract_audit_log");
        var vehicleStatusHistoryCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_status_history WHERE ISNULL(is_deleted, 0) = 0");
        var vehicleTypeHistoryCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_type_history WHERE ISNULL(is_deleted, 0) = 0");
        var vehicleLicenceHistoryCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_licence_history");
        var fleetNotesCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fleet_notes WHERE ISNULL(is_deleted, 0) = 0");
        var preVehicleNotesCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM PreVehicle_Master_notes WHERE ISNULL(is_deleted, 0) = 0");
        var tsLogCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Log WHERE ISNULL(is_deleted, 0) = 0");
        var errorLogCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM error_log WHERE ISNULL(is_deleted, 0) = 0");
        var vehicleHistoryCount = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_history WHERE ISNULL(is_deleted, 0) = 0");

        Console.WriteLine($"  📊 Batch 5 | Contract Status History: {contractStatusHistoryCount} | Contract Audit Log: {contractAuditLogCount} | Vehicle Status History: {vehicleStatusHistoryCount} | Vehicle Type History: {vehicleTypeHistoryCount} | Vehicle Licence History: {vehicleLicenceHistoryCount}");
        Console.WriteLine($"  📊 Batch 5 | Fleet Notes: {fleetNotesCount} | Pre-Vehicle Notes: {preVehicleNotesCount} | TS Log: {tsLogCount} | Error Log: {errorLogCount} | Vehicle History: {vehicleHistoryCount}");
    }

    private static async Task ApplyBatch6ReferenceBreadthCoverageAsync(FisDbContext dbContext)
    {
        // Batch 6 tables:
        // province, ranks, Positions, Incident_Area, vehicle_source,
        // fuel_tariff, extra_codes, acc_type, Loss_type, licence_fee

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = 1;
            IF OBJECT_ID('TS_Users', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultUserCode = user_access_code
                FROM TS_Users
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY user_access_code;
            END

            IF OBJECT_ID('province', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM province WHERE province_code = 1)
                    INSERT INTO province (province_code, province_name, province_abbreviation, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'Gauteng', 'GP', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM province WHERE province_code = 2)
                    INSERT INTO province (province_code, province_name, province_abbreviation, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'Western Cape', 'WC', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('ranks', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM ranks WHERE rank_code = 1)
                    INSERT INTO ranks (rank_code, description, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'Director', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM ranks WHERE rank_code = 2)
                    INSERT INTO ranks (rank_code, description, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'Manager', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM ranks WHERE rank_code = 3)
                    INSERT INTO ranks (rank_code, description, date_created, created_by_user_code, is_deleted)
                    VALUES (3, 'Officer', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('Positions', 'U') IS NOT NULL
            BEGIN
                SET IDENTITY_INSERT Positions ON;
                IF NOT EXISTS (SELECT 1 FROM Positions WHERE Position_Code = 1)
                    INSERT INTO Positions (Position_Code, Position_Name, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'Fleet Administrator', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Positions WHERE Position_Code = 2)
                    INSERT INTO Positions (Position_Code, Position_Name, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'Transport Officer', GETDATE(), @defaultUserCode, 0);
                SET IDENTITY_INSERT Positions OFF;
            END

            IF OBJECT_ID('Incident_Area', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Incident_Area WHERE Town_SubArea = 'Johannesburg CBD' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Incident_Area (Town_SubArea, date_created, created_by_user_code, is_deleted)
                    VALUES ('Johannesburg CBD', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Incident_Area WHERE Town_SubArea = 'Pretoria Central' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Incident_Area (Town_SubArea, date_created, created_by_user_code, is_deleted)
                    VALUES ('Pretoria Central', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Incident_Area WHERE Town_SubArea = 'Ekurhuleni East' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Incident_Area (Town_SubArea, date_created, created_by_user_code, is_deleted)
                    VALUES ('Ekurhuleni East', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('vehicle_source', 'U') IS NOT NULL
            BEGIN
                SET IDENTITY_INSERT vehicle_source ON;
                IF NOT EXISTS (SELECT 1 FROM vehicle_source WHERE vs_code = 1)
                    INSERT INTO vehicle_source (vs_code, name, physical_address, postal_address, tel_number, fax_number, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'OEM Direct', '1 Industry Rd, Midrand', 'PO Box 100, Midrand', '0110000000', '0110000001', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM vehicle_source WHERE vs_code = 2)
                    INSERT INTO vehicle_source (vs_code, name, physical_address, postal_address, tel_number, fax_number, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'Auction House', '22 Auction Ave, JHB', 'PO Box 220, JHB', '0110000002', '0110000003', GETDATE(), @defaultUserCode, 0);
                SET IDENTITY_INSERT vehicle_source OFF;
            END

            IF OBJECT_ID('fuel_tariff', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM fuel_tariff WHERE fuel_tariff_code = 1)
                    INSERT INTO fuel_tariff (fuel_tariff_code, fuel_type_code, fuel_tariff, fuel_tariff_notes, start_date, end_date, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 1, 23.45, 'Petrol baseline tariff', DATEADD(month, -2, GETDATE()), NULL, GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM fuel_tariff WHERE fuel_tariff_code = 2)
                    INSERT INTO fuel_tariff (fuel_tariff_code, fuel_type_code, fuel_tariff, fuel_tariff_notes, start_date, end_date, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 2, 24.90, 'Diesel baseline tariff', DATEADD(month, -2, GETDATE()), NULL, GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('extra_codes', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM extra_codes WHERE extra_description = 'General Mechanical' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO extra_codes (extra_description, category_type_code, specific, Additional, date_created, created_by_user_code, is_deleted)
                    VALUES ('General Mechanical', 1, 0, 0, GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM extra_codes WHERE extra_description = 'Electrical Repair' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO extra_codes (extra_description, category_type_code, specific, Additional, date_created, created_by_user_code, is_deleted)
                    VALUES ('Electrical Repair', 1, 0, 0, GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('acc_type', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM acc_type WHERE acc_type_description = 'Minor' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO acc_type (acc_type_description, date_created, created_by_user_code, is_deleted)
                    VALUES ('Minor', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM acc_type WHERE acc_type_description = 'Major' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO acc_type (acc_type_description, date_created, created_by_user_code, is_deleted)
                    VALUES ('Major', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('Loss_type', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Loss_type WHERE loss_type_code = 1)
                    INSERT INTO Loss_type (loss_type_code, loss_description, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'Theft', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Loss_type WHERE loss_type_code = 2)
                    INSERT INTO Loss_type (loss_type_code, loss_description, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'Write-Off', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('licence_fee', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM licence_fee WHERE licence_fee_code = 1)
                    INSERT INTO licence_fee (licence_fee_code, licence_description, licence_fee, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'Passenger Vehicle Annual License', 680.00, GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM licence_fee WHERE licence_fee_code = 2)
                    INSERT INTO licence_fee (licence_fee_code, licence_description, licence_fee, date_created, created_by_user_code, is_deleted)
                    VALUES (2, 'LDV Annual License', 940.00, GETDATE(), @defaultUserCode, 0);
            END
        ");

        Console.WriteLine("  ✓ Batch 6 reference-data breadth updates applied.");

        var provinceCount = await (await HasTableAsync(dbContext, "province") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM province WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var rankCount = await (await HasTableAsync(dbContext, "ranks") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM ranks WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var positionCount = await (await HasTableAsync(dbContext, "Positions") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Positions WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var incidentAreaCount = await (await HasTableAsync(dbContext, "Incident_Area") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Incident_Area WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vehicleSourceCount = await (await HasTableAsync(dbContext, "vehicle_source") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_source WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var fuelTariffCount = await (await HasTableAsync(dbContext, "fuel_tariff") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fuel_tariff WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var extraCodeCount = await (await HasTableAsync(dbContext, "extra_codes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM extra_codes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var accTypeCount = await (await HasTableAsync(dbContext, "acc_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM acc_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var lossTypeCount = await (await HasTableAsync(dbContext, "Loss_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Loss_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var licenceFeeCount = await (await HasTableAsync(dbContext, "licence_fee") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM licence_fee WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 6 | Province: {provinceCount} | Ranks: {rankCount} | Positions: {positionCount} | Incident Areas: {incidentAreaCount} | Vehicle Sources: {vehicleSourceCount}");
        Console.WriteLine($"  📊 Batch 6 | Fuel Tariffs: {fuelTariffCount} | Extra Codes: {extraCodeCount} | Accident Types: {accTypeCount} | Loss Types: {lossTypeCount} | Licence Fees: {licenceFeeCount}");
    }

    private static async Task ApplyBatch7NoticeWorkflowCoverageAsync(FisDbContext dbContext)
    {
        // Batch 7 tables:
        // Notices, NoticeSchedule, Workflow.Workflow, Workflow.StepType, Workflow.Step,
        // Workflow.Status, Workflow.NotificationTemplate, Workflow.WorkflowNotification,
        // Workflow.NotificationLog, Workflow.StepExecutionHistory

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = 1;
            IF OBJECT_ID('TS_Users', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultUserCode = user_access_code
                FROM TS_Users
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY user_access_code;
            END

            IF OBJECT_ID('Notices', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Notices WHERE notice_title = 'Fleet Billing Reminder' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Notices
                    (
                        notice_date, notice_from, notice_title, notice_body, notice_person, notice_person_title,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        GETDATE(), 'Fleet Finance', 'Fleet Billing Reminder',
                        'Please confirm all still-current contracts have billing reviewed this week.',
                        'Fleet Superintendent', 'Operations Lead',
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('NoticeSchedule', 'U') IS NOT NULL AND OBJECT_ID('Notices', 'U') IS NOT NULL
            BEGIN
                DECLARE @noticeId int = (
                    SELECT TOP 1 notice_id
                    FROM Notices
                    WHERE notice_title = 'Fleet Billing Reminder'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY notice_id DESC
                );

                IF @noticeId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM NoticeSchedule WHERE notice_id = @noticeId AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO NoticeSchedule
                    (
                        notice_id, title_field, start_date, end_date, sort_order,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        @noticeId, 'Billing Reminder',
                        CAST(GETDATE() AS date), DATEADD(day, 30, CAST(GETDATE() AS date)), 1,
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('Workflow.Workflow', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Workflow.Workflow WHERE WorkflowName = 'Seeded Contract Approval Workflow' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.Workflow (WorkflowName, AlwaysExecute, date_created, created_by_user_code, is_deleted)
                    VALUES ('Seeded Contract Approval Workflow', 0, GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('Workflow.StepType', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Workflow.StepType WHERE StepTypeName = 'ManualReview' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.StepType (StepTypeName, StepTypeData, date_created, created_by_user_code, is_deleted)
                    VALUES ('ManualReview', '{{""mode"":""approval""}}', GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('Workflow.Step', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Workflow', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.StepType', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (
                    SELECT TOP 1 WorkflowID
                    FROM Workflow.Workflow
                    WHERE WorkflowName = 'Seeded Contract Approval Workflow'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY WorkflowID DESC
                );
                DECLARE @stepTypeId int = (
                    SELECT TOP 1 StepTypeID
                    FROM Workflow.StepType
                    WHERE StepTypeName = 'ManualReview'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY StepTypeID
                );

                IF @workflowId IS NOT NULL
                   AND @stepTypeId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Workflow.Step WHERE WorkflowID = @workflowId AND StepName = 'Review Contract' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.Step
                    (
                        StepName, StepOrder, StepTypeID, WorkflowID, ParentStepID, StepParameters, HandlerType,
                        IsConditional, ConditionExpression, TrueStepID, FalseStepID,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        'Review Contract', 1, @stepTypeId, @workflowId, NULL, '{{""source"":""batch7""}}', 'ManualApproval',
                        0, NULL, NULL, NULL,
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('Workflow.Status', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Step', 'U') IS NOT NULL
            BEGIN
                DECLARE @statusStepId int = (
                    SELECT TOP 1 StepID
                    FROM Workflow.Step
                    WHERE StepName = 'Review Contract'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY StepID DESC
                );

                IF @statusStepId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Workflow.Status WHERE StepID = @statusStepId AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.Status
                    (
                        StepID, DateCompleted, DateStarted, IsBusy, StartedByUserName,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        @statusStepId, NULL, GETDATE(), 1, 'SeedUser',
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('Workflow.NotificationTemplate', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Workflow.NotificationTemplate WHERE TemplateName = 'Contract Approval Alert' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.NotificationTemplate
                    (
                        TemplateName, Description, Subject, Body, TemplateType, Variables, IsActive,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        'Contract Approval Alert', 'Seeded template for workflow notifications',
                        'Contract Requires Review', 'A contract requires your review in FIS.',
                        'Email', '[""ContractCode"",""Vehicle""]', 1,
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('Workflow.WorkflowNotification', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Workflow', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.NotificationTemplate', 'U') IS NOT NULL
            BEGIN
                DECLARE @wnWorkflowId int = (
                    SELECT TOP 1 WorkflowID
                    FROM Workflow.Workflow
                    WHERE WorkflowName = 'Seeded Contract Approval Workflow'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY WorkflowID DESC
                );
                DECLARE @templateId int = (
                    SELECT TOP 1 TemplateID
                    FROM Workflow.NotificationTemplate
                    WHERE TemplateName = 'Contract Approval Alert'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY TemplateID DESC
                );

                IF @wnWorkflowId IS NOT NULL
                   AND @templateId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Workflow.WorkflowNotification WHERE WorkflowID = @wnWorkflowId AND EventType = 'StepCompleted' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.WorkflowNotification
                    (
                        WorkflowID, StepID, EventType, RecipientType, RecipientIdentifier, NotificationTemplateID,
                        Subject, Body, IsActive, SendDelay,
                        date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        @wnWorkflowId, NULL, 'StepCompleted', 'Email', 'fleet.alerts@fis.local', @templateId,
                        'Step Completed', 'A workflow step completed successfully.', 1, 0,
                        GETDATE(), @defaultUserCode, 0
                    );
            END

            IF OBJECT_ID('Workflow.NotificationLog', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Workflow', 'U') IS NOT NULL
            BEGIN
                DECLARE @logWorkflowId int = (
                    SELECT TOP 1 WorkflowID
                    FROM Workflow.Workflow
                    WHERE WorkflowName = 'Seeded Contract Approval Workflow'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY WorkflowID DESC
                );
                DECLARE @logNotificationId int = (
                    SELECT TOP 1 NotificationID
                    FROM Workflow.WorkflowNotification
                    WHERE WorkflowID = @logWorkflowId
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY NotificationID DESC
                );

                IF @logWorkflowId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Workflow.NotificationLog WHERE WorkflowID = @logWorkflowId AND EventType = 'StepCompleted' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.NotificationLog
                    (
                        NotificationID, WorkflowID, StepID, StatusID, EventType, RecipientEmail, Subject, Body,
                        SentAt, DeliveryStatus, ErrorMessage, RetryCount, ExternalMessageId, date_created, is_deleted
                    )
                    VALUES
                    (
                        @logNotificationId, @logWorkflowId, NULL, NULL, 'StepCompleted', 'fleet.alerts@fis.local',
                        'Seeded Notification', 'Batch 7 seeded notification log.',
                        GETDATE(), 'Sent', NULL, 0, NULL, GETDATE(), 0
                    );
            END

            IF OBJECT_ID('Workflow.StepExecutionHistory', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Status', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Step', 'U') IS NOT NULL
               AND OBJECT_ID('Workflow.Workflow', 'U') IS NOT NULL
            BEGIN
                DECLARE @sehWorkflowId int = (
                    SELECT TOP 1 WorkflowID
                    FROM Workflow.Workflow
                    WHERE WorkflowName = 'Seeded Contract Approval Workflow'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY WorkflowID DESC
                );
                DECLARE @sehStepId int = (
                    SELECT TOP 1 StepID
                    FROM Workflow.Step
                    WHERE WorkflowID = @sehWorkflowId
                      AND StepName = 'Review Contract'
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY StepID DESC
                );
                DECLARE @sehStatusId int = (
                    SELECT TOP 1 StatusID
                    FROM Workflow.Status
                    WHERE StepID = @sehStepId
                      AND ISNULL(is_deleted, 0) = 0
                    ORDER BY StatusID DESC
                );

                IF @sehWorkflowId IS NOT NULL
                   AND @sehStepId IS NOT NULL
                   AND @sehStatusId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Workflow.StepExecutionHistory WHERE StatusID = @sehStatusId AND StepID = @sehStepId AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Workflow.StepExecutionHistory
                    (
                        StatusID, StepID, WorkflowID, StepName, HandlerType, StartedAt, CompletedAt, DurationSeconds,
                        ExecutionStatus, ErrorMessage, InputData, OutputData, ExecutedByUserCode, date_created, is_deleted
                    )
                    VALUES
                    (
                        @sehStatusId, @sehStepId, @sehWorkflowId, 'Review Contract', 'ManualApproval',
                        DATEADD(minute, -5, GETDATE()), GETDATE(), 300,
                        'Completed', NULL, '{{""seed"":""batch7""}}', '{{""result"":""ok""}}', @defaultUserCode, GETDATE(), 0
                    );
            END
        ");

        Console.WriteLine("  ✓ Batch 7 notice/workflow coverage updates applied.");

        var noticesCount = await (await HasTableAsync(dbContext, "Notices") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Notices WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var noticeScheduleCount = await (await HasTableAsync(dbContext, "NoticeSchedule") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM NoticeSchedule WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var workflowCount = await (await HasTableAsync(dbContext, "Workflow") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.Workflow WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var stepTypeCount = await (await HasTableAsync(dbContext, "StepType") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.StepType WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var stepCount = await (await HasTableAsync(dbContext, "Step") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.Step WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var statusCount = await (await HasTableAsync(dbContext, "Status") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.Status WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notificationTemplateCount = await (await HasTableAsync(dbContext, "NotificationTemplate") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.NotificationTemplate WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var workflowNotificationCount = await (await HasTableAsync(dbContext, "WorkflowNotification") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.WorkflowNotification WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notificationLogCount = await (await HasTableAsync(dbContext, "NotificationLog") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.NotificationLog WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var executionHistoryCount = await (await HasTableAsync(dbContext, "StepExecutionHistory") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Workflow.StepExecutionHistory WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 7 | Notices: {noticesCount} | Notice Schedules: {noticeScheduleCount} | Workflows: {workflowCount} | Step Types: {stepTypeCount} | Steps: {stepCount}");
        Console.WriteLine($"  📊 Batch 7 | Status: {statusCount} | Notification Templates: {notificationTemplateCount} | Workflow Notifications: {workflowNotificationCount} | Notification Logs: {notificationLogCount} | Step Exec History: {executionHistoryCount}");
    }

    private static async Task ApplyBatch8LegacyBridgeCoverageAsync(FisDbContext dbContext)
    {
        // Batch 8 tables:
        // VehicleKilos, Model_KilosPerFuelLitre, Wesbank_KilosPerFuelLitre, Report_vehicles, Req_num,
        // HistStatus, fleet_notes_bkp, fleet_notes_restore, TripsWithoutRoutes_Backup, EduCodes

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = 1;
            IF OBJECT_ID('TS_Users', 'U') IS NOT NULL
            BEGIN
                SELECT TOP 1 @defaultUserCode = user_access_code
                FROM TS_Users
                WHERE ISNULL(is_deleted, 0) = 0
                ORDER BY user_access_code;
            END

            IF OBJECT_ID('VehicleKilos', 'U') IS NOT NULL
            BEGIN
                INSERT INTO VehicleKilos
                (
                    vmf_code, registration_number, fleet_number, start_odo, end_odo, contract_code,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 100
                       vm.vmf_code,
                       vm.registration_number,
                       vm.fleet_number,
                       CAST(ISNULL(vm.take_on_odo, 0) AS float),
                       CAST(ISNULL(vm.current_odo, ISNULL(vm.take_on_odo, 0) + 500) AS float),
                       c.contract_code,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                LEFT JOIN contract c ON c.vmf_code = vm.vmf_code AND c.is_deleted = 0
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM VehicleKilos vk
                      WHERE vk.vmf_code = vm.vmf_code
                        AND ISNULL(vk.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('Model_KilosPerFuelLitre', 'U') IS NOT NULL
            BEGIN
                INSERT INTO Model_KilosPerFuelLitre
                (
                    model_code, kilos_per_litre, date_created, created_by_user_code, is_deleted
                )
                SELECT m.model_code,
                       CAST(10.0 + (m.model_code % 7) AS float),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM model m
                WHERE ISNULL(m.is_deleted, 0) = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Model_KilosPerFuelLitre mk
                      WHERE mk.model_code = m.model_code
                        AND ISNULL(mk.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('Wesbank_KilosPerFuelLitre', 'U') IS NOT NULL
            BEGIN
                INSERT INTO Wesbank_KilosPerFuelLitre
                (
                    registration_number, kilos_per_litre, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 80
                       vm.registration_number,
                       CAST(9.5 + (vm.vmf_code % 6) AS float),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND vm.registration_number IS NOT NULL
                  AND LTRIM(RTRIM(vm.registration_number)) <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Wesbank_KilosPerFuelLitre wk
                      WHERE wk.registration_number = vm.registration_number
                        AND ISNULL(wk.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('Report_vehicles', 'U') IS NOT NULL
            BEGIN
                INSERT INTO Report_vehicles
                (
                    vmf_code, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 120
                       vm.vmf_code,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM Report_vehicles rv
                      WHERE rv.vmf_code = vm.vmf_code
                        AND ISNULL(rv.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('Req_num', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Req_num WHERE series = 'TRIP' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Req_num (series, number, date_created, created_by_user_code, is_deleted)
                    VALUES ('TRIP', 1000, GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Req_num WHERE series = 'CONTRACT' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Req_num (series, number, date_created, created_by_user_code, is_deleted)
                    VALUES ('CONTRACT', 2000, GETDATE(), @defaultUserCode, 0);
            END

            IF OBJECT_ID('HistStatus', 'U') IS NOT NULL
            BEGIN
                INSERT INTO HistStatus
                (
                    vmf_code, status_code, status_date, ggno, date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 120
                       vm.vmf_code,
                       vm.vehicle_status_code,
                       GETDATE(),
                       vm.fleet_number,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM HistStatus hs
                      WHERE hs.vmf_code = vm.vmf_code
                        AND ISNULL(hs.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END

            IF OBJECT_ID('fleet_notes_bkp', 'U') IS NOT NULL
            BEGIN
                INSERT INTO fleet_notes_bkp
                (
                    fleet_notes_code, vmf_code, notes, update_date, date_created, created_by_user_code, is_deleted
                )
                SELECT fn.fleet_notes_code,
                       fn.vmf_code,
                       LEFT(COALESCE(fn.fleet_note, 'Auto-seeded note backup'), 255),
                       GETDATE(),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM fleet_notes fn
                WHERE ISNULL(fn.is_deleted, 0) = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM fleet_notes_bkp fb
                      WHERE fb.fleet_notes_code = fn.fleet_notes_code
                        AND fb.vmf_code = fn.vmf_code
                        AND ISNULL(fb.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('fleet_notes_restore', 'U') IS NOT NULL
            BEGIN
                INSERT INTO fleet_notes_restore
                (
                    fleet_notes_code, vmf_code, notes, update_date, date_created, created_by_user_code, is_deleted
                )
                SELECT fn.fleet_notes_code,
                       fn.vmf_code,
                       LEFT(COALESCE(fn.fleet_note, 'Auto-seeded note restore'), 255),
                       GETDATE(),
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM fleet_notes fn
                WHERE ISNULL(fn.is_deleted, 0) = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM fleet_notes_restore fr
                      WHERE fr.fleet_notes_code = fn.fleet_notes_code
                        AND fr.vmf_code = fn.vmf_code
                        AND ISNULL(fr.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('TripsWithoutRoutes_Backup', 'U') IS NOT NULL
            BEGIN
                INSERT INTO TripsWithoutRoutes_Backup
                (
                    trip_authority_code, contract_code, approver_name, approver_rank, approver_tel, end_odo_meter, expiry_date,
                    trip_reason, trip_request_number, issue_date, trip_type_code, trip_incident_type_code, user_access_code,
                    locked_for_transfer, Trip_Is_Monthly, date_created, created_by_user_code, is_deleted
                )
                SELECT ta.trip_authority_code,
                       ta.contract_code,
                       ta.approver_name,
                       ta.approver_rank,
                       ta.approver_tel,
                       ta.end_odo_meter,
                       ta.expiry_date,
                       ta.trip_reason,
                       ta.trip_request_number,
                       ta.issue_date,
                       ta.trip_type_code,
                       ta.trip_incident_type_code,
                       ta.user_access_code,
                       ta.locked_for_transfer,
                       ta.Trip_Is_Monthly,
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM trip_authorities ta
                WHERE ISNULL(ta.is_deleted, 0) = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM TripsWithoutRoutes_Backup twr
                      WHERE twr.trip_authority_code = ta.trip_authority_code
                        AND ISNULL(twr.is_deleted, 0) = 0
                  );
            END

            IF OBJECT_ID('EduCodes', 'U') IS NOT NULL
            BEGIN
                INSERT INTO EduCodes
                (
                    vmf_Code, Registration, RespNumber, RespName, ObjNumber, ObjName,
                    date_created, created_by_user_code, is_deleted
                )
                SELECT TOP 120
                       vm.vmf_code,
                       vm.registration_number,
                       CONCAT('RESP', vm.vmf_code),
                       'Department Responsible',
                       CONCAT('OBJ', vm.vmf_code),
                       'Fleet Asset',
                       GETDATE(),
                       @defaultUserCode,
                       0
                FROM vehicle_master vm
                WHERE vm.is_deleted = 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM EduCodes ec
                      WHERE ec.vmf_Code = vm.vmf_code
                        AND ISNULL(ec.is_deleted, 0) = 0
                  )
                ORDER BY vm.vmf_code;
            END
        ");

        Console.WriteLine("  ✓ Batch 8 legacy-bridge coverage updates applied.");

        var vehicleKilosCount = await (await HasTableAsync(dbContext, "VehicleKilos") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM VehicleKilos WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var modelKplCount = await (await HasTableAsync(dbContext, "Model_KilosPerFuelLitre") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Model_KilosPerFuelLitre WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var wesbankKplCount = await (await HasTableAsync(dbContext, "Wesbank_KilosPerFuelLitre") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Wesbank_KilosPerFuelLitre WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var reportVehiclesCount = await (await HasTableAsync(dbContext, "Report_vehicles") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Report_vehicles WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var reqNumCount = await (await HasTableAsync(dbContext, "Req_num") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Req_num WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var histStatusCount = await (await HasTableAsync(dbContext, "HistStatus") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM HistStatus WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var fleetNotesBkpCount = await (await HasTableAsync(dbContext, "fleet_notes_bkp") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fleet_notes_bkp WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var fleetNotesRestoreCount = await (await HasTableAsync(dbContext, "fleet_notes_restore") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fleet_notes_restore WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tripsWithoutRoutesCount = await (await HasTableAsync(dbContext, "TripsWithoutRoutes_Backup") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TripsWithoutRoutes_Backup WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var eduCodesCount = await (await HasTableAsync(dbContext, "EduCodes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM EduCodes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 8 | VehicleKilos: {vehicleKilosCount} | Model KPL: {modelKplCount} | Wesbank KPL: {wesbankKplCount} | Report Vehicles: {reportVehiclesCount} | Req Num: {reqNumCount}");
        Console.WriteLine($"  📊 Batch 8 | HistStatus: {histStatusCount} | Fleet Notes BKP: {fleetNotesBkpCount} | Fleet Notes Restore: {fleetNotesRestoreCount} | TripsWithoutRoutes BKP: {tripsWithoutRoutesCount} | EduCodes: {eduCodesCount}");
    }

    private static async Task ApplyBatch9SystemConfigurationCoverageAsync(FisDbContext dbContext)
    {
        // Batch 9 tables:
        // system_parameters, version, db_version, db_ddl_log, SSIS Configurations,
        // dtproperties, Configuration.UserCompany, Configuration.Parameter,
        // Configuration.ParameterValue, Holidays

        var defaultUserCode = await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT system_parameters ON;
                IF NOT EXISTS (SELECT 1 FROM system_parameters WHERE sys_parameters_code = 1)
                    INSERT INTO system_parameters
                    (sys_parameters_code, database_version, app_version, vat_percent, daily_weight, hourly_weight, date_created, created_by_user_code, is_deleted)
                    VALUES
                    (1, 'v1.0', 'app-1.0', 15.00, 0.60, 0.40, GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT system_parameters OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM system_parameters WHERE ISNULL(is_deleted, 0) = 0)
                    INSERT INTO system_parameters
                    (database_version, app_version, vat_percent, daily_weight, hourly_weight, date_created, created_by_user_code, is_deleted)
                    VALUES
                    ('v1.0', 'app-1.0', 15.00, 0.60, 0.40, GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT version ON;
                IF NOT EXISTS (SELECT 1 FROM version WHERE version_code = 1)
                    INSERT INTO version (version_code, version_number, version_date, date_created, created_by_user_code, is_deleted)
                    VALUES (1, '1.0.0', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT version OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM version WHERE ISNULL(is_deleted, 0) = 0)
                    INSERT INTO version (version_number, version_date, date_created, created_by_user_code, is_deleted)
                    VALUES ('1.0.0', GETDATE(), GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT db_version ON;
                IF NOT EXISTS (SELECT 1 FROM db_version WHERE db_version_code = 1)
                    INSERT INTO db_version (db_version_code, db_version_number, db_version_date, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'legacy-sync-1', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT db_version OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM db_version WHERE ISNULL(is_deleted, 0) = 0)
                    INSERT INTO db_version (db_version_number, db_version_date, date_created, created_by_user_code, is_deleted)
                    VALUES ('legacy-sync-1', GETDATE(), GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT db_ddl_log ON;
                IF NOT EXISTS (SELECT 1 FROM db_ddl_log WHERE db_ddl_log_code = 1)
                    INSERT INTO db_ddl_log (db_ddl_log_code, post_time, database_user, [event], [schema], [object], tsql, date_created, created_by_user_code, is_deleted)
                    VALUES (1, GETDATE(), 'fis_seed', 'INSERT', 'dbo', 'vehicle_master', '/* seeded ddl log */', GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT db_ddl_log OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM db_ddl_log WHERE database_user = 'fis_seed' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO db_ddl_log (post_time, database_user, [event], [schema], [object], tsql, date_created, created_by_user_code, is_deleted)
                    VALUES (GETDATE(), 'fis_seed', 'INSERT', 'dbo', 'vehicle_master', '/* seeded ddl log */', GETDATE(), {defaultUserCode}, 0);
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = ISNULL(
                (SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code),
                1
            );
            IF OBJECT_ID('SSIS Configurations', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM [SSIS Configurations] WHERE ConfigurationFilter = 'FIS' AND PackagePath = '\\Package.Variables[User::Conn].Properties[Value]' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO [SSIS Configurations]
                    (ConfigurationFilter, ConfiguredValue, PackagePath, ConfiguredValueType, date_created, created_by_user_code, is_deleted)
                    VALUES ('FIS', 'Server=legacy;Database=fis;', '\\Package.Variables[User::Conn].Properties[Value]', 'String', GETDATE(), @defaultUserCode, 0);
            END
        ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT dtproperties ON;
                IF NOT EXISTS (SELECT 1 FROM dtproperties WHERE id = 1)
                    INSERT INTO dtproperties (id, objectid, property, value, version, uvalue, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 1, 'DatabaseVersion', '1.0', 1, 'seeded', GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT dtproperties OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM dtproperties WHERE property = 'DatabaseVersion' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO dtproperties (objectid, property, value, version, uvalue, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'DatabaseVersion', '1.0', 1, 'seeded', GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT [Configuration].[UserCompany] ON;
                IF NOT EXISTS (SELECT 1 FROM [Configuration].[UserCompany] WHERE UserCompanyID = 1)
                    INSERT INTO [Configuration].[UserCompany] (UserCompanyID, Code, Name, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'GFLEET', 'g-FleeT', GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT [Configuration].[UserCompany] OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM [Configuration].[UserCompany] WHERE Code = 'GFLEET' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO [Configuration].[UserCompany] (Code, Name, date_created, created_by_user_code, is_deleted)
                    VALUES ('GFLEET', 'g-FleeT', GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                SET IDENTITY_INSERT [Configuration].[Parameter] ON;
                IF NOT EXISTS (SELECT 1 FROM [Configuration].[Parameter] WHERE ParameterID = 1)
                    INSERT INTO [Configuration].[Parameter] (ParameterID, Name, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'BillingGraceDays', GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT [Configuration].[Parameter] OFF;
            ",
            $@"
                IF NOT EXISTS (SELECT 1 FROM [Configuration].[Parameter] WHERE Name = 'BillingGraceDays' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO [Configuration].[Parameter] (Name, date_created, created_by_user_code, is_deleted)
                    VALUES ('BillingGraceDays', GETDATE(), {defaultUserCode}, 0);
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                DECLARE @pId int = (SELECT TOP 1 ParameterID FROM [Configuration].[Parameter] WHERE Name = 'BillingGraceDays' AND ISNULL(is_deleted, 0) = 0 ORDER BY ParameterID DESC);
                DECLARE @ucId int = (SELECT TOP 1 UserCompanyID FROM [Configuration].[UserCompany] WHERE Code = 'GFLEET' AND ISNULL(is_deleted, 0) = 0 ORDER BY UserCompanyID DESC);
                SET IDENTITY_INSERT [Configuration].[ParameterValue] ON;
                IF @pId IS NOT NULL AND @ucId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Configuration].[ParameterValue] WHERE ParameterValueID = 1)
                    INSERT INTO [Configuration].[ParameterValue] (ParameterValueID, UserCompanyID, ParameterID, Value, date_created, created_by_user_code, is_deleted)
                    VALUES (1, @ucId, @pId, '14', GETDATE(), {defaultUserCode}, 0);
                SET IDENTITY_INSERT [Configuration].[ParameterValue] OFF;
            ",
            $@"
                DECLARE @pId int = (SELECT TOP 1 ParameterID FROM [Configuration].[Parameter] WHERE Name = 'BillingGraceDays' AND ISNULL(is_deleted, 0) = 0 ORDER BY ParameterID DESC);
                DECLARE @ucId int = (SELECT TOP 1 UserCompanyID FROM [Configuration].[UserCompany] WHERE Code = 'GFLEET' AND ISNULL(is_deleted, 0) = 0 ORDER BY UserCompanyID DESC);
                IF @pId IS NOT NULL AND @ucId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Configuration].[ParameterValue] WHERE ParameterID = @pId AND UserCompanyID = @ucId AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO [Configuration].[ParameterValue] (UserCompanyID, ParameterID, Value, date_created, created_by_user_code, is_deleted)
                    VALUES (@ucId, @pId, '14', GETDATE(), {defaultUserCode}, 0);
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            DECLARE @defaultUserCode int = ISNULL(
                (SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code),
                1
            );
            IF OBJECT_ID('Holidays', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Holidays WHERE HolidayDate = '2026-01-01' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Holidays (HolidayDate, HolidayName, date_created, created_by_user_code, is_deleted)
                    VALUES ('2026-01-01', 'New Year''s Day', GETDATE(), @defaultUserCode, 0);
                IF NOT EXISTS (SELECT 1 FROM Holidays WHERE HolidayDate = '2026-12-25' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Holidays (HolidayDate, HolidayName, date_created, created_by_user_code, is_deleted)
                    VALUES ('2026-12-25', 'Christmas Day', GETDATE(), @defaultUserCode, 0);
            END
        ");

        Console.WriteLine("  ✓ Batch 9 system-configuration coverage updates applied.");

        var systemParametersCount = await (await HasTableAsync(dbContext, "system_parameters") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM system_parameters WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var versionCount = await (await HasTableAsync(dbContext, "version") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM version WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var dbVersionCount = await (await HasTableAsync(dbContext, "db_version") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM db_version WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var dbDdlLogCount = await (await HasTableAsync(dbContext, "db_ddl_log") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM db_ddl_log WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var ssisCount = await (await HasTableAsync(dbContext, "SSIS Configurations") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [SSIS Configurations] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var dtPropertiesCount = await (await HasTableAsync(dbContext, "dtproperties") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM dtproperties WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var userCompanyCount = await (await HasTableAsync(dbContext, "UserCompany") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Configuration].[UserCompany] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var parameterCount = await (await HasTableAsync(dbContext, "Parameter") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Configuration].[Parameter] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var parameterValueCount = await (await HasTableAsync(dbContext, "ParameterValue") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Configuration].[ParameterValue] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var holidayCount = await (await HasTableAsync(dbContext, "Holidays") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Holidays WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 9 | System Params: {systemParametersCount} | Version: {versionCount} | DB Version: {dbVersionCount} | DB DDL Log: {dbDdlLogCount} | SSIS Config: {ssisCount}");
        Console.WriteLine($"  📊 Batch 9 | dtproperties: {dtPropertiesCount} | UserCompany: {userCompanyCount} | Parameter: {parameterCount} | ParameterValue: {parameterValueCount} | Holidays: {holidayCount}");
    }

    private static async Task ApplyBatch10TariffSegmentCoverageAsync(FisDbContext dbContext)
    {
        // Batch 10 tables:
        // tariff, LeaseTariff, Last_monthly_Tar, fin.TariffParameter, fin.TariffWeightCalculation,
        // fin.vehicle_tariff, segment_type, segment_group, segment, journal_detail_type_group

        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultClassCode = await HasTableAsync(dbContext, "class") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM class WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 class_code FROM class WHERE ISNULL(is_deleted, 0) = 0 ORDER BY class_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('tariff', 'U') IS NOT NULL
                BEGIN
                    DECLARE @nextTariffCode int = ISNULL((SELECT MAX(tariff_code) FROM tariff), 0) + 1;
                    SET IDENTITY_INSERT tariff ON;
                    IF NOT EXISTS (SELECT 1 FROM tariff WHERE class_code = {defaultClassCode} AND year_manufactured = 2023 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO tariff
                        (
                            tariff_code, class_code, year_manufactured, monthly_fixed_amount, monthly_odo_amount,
                            daily_fixed_amount, hourly_fixed_amount, effective_start_date, effective_end_date,
                            replacement_percent, loss_percent, profit_percent, overhead_percent, accident_percent,
                            fuel_kilo_tariff, tariff_approval_status, approver_code, approval_date,
                            date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (@nextTariffCode, {defaultClassCode}, 2023, 3500, 900, 180, 25, DATEADD(month, -3, GETDATE()), NULL, 8, 2, 5, 10, 3, 2.45, 2, {defaultUserCode}, GETDATE(), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT tariff OFF;
                END
            ",
            $@"
                IF OBJECT_ID('tariff', 'U') IS NOT NULL
                BEGIN
                    DECLARE @nextTariffCode int = ISNULL((SELECT MAX(tariff_code) FROM tariff), 0) + 1;
                    IF NOT EXISTS (SELECT 1 FROM tariff WHERE class_code = {defaultClassCode} AND year_manufactured = 2023 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO tariff
                        (
                            tariff_code, class_code, year_manufactured, monthly_fixed_amount, monthly_odo_amount,
                            daily_fixed_amount, hourly_fixed_amount, effective_start_date, effective_end_date,
                            replacement_percent, loss_percent, profit_percent, overhead_percent, accident_percent,
                            fuel_kilo_tariff, tariff_approval_status, approver_code, approval_date,
                            date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (@nextTariffCode, {defaultClassCode}, 2023, 3500, 900, 180, 25, DATEADD(month, -3, GETDATE()), NULL, 8, 2, 5, 10, 3, 2.45, 2, {defaultUserCode}, GETDATE(), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('LeaseTariff', 'U') IS NOT NULL
                BEGIN
                    DECLARE @nextLeaseTariffCode int = ISNULL((SELECT MAX(lease_tariff_code) FROM LeaseTariff), 0) + 1;
                    SET IDENTITY_INSERT LeaseTariff ON;
                    IF NOT EXISTS (SELECT 1 FROM LeaseTariff WHERE vmf_code = {defaultVehicleCode} AND active = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO LeaseTariff
                        (lease_tariff_code, vmf_code, start_date, end_date, fixed_tariff, active, date_created, created_by_user_code, is_deleted)
                        VALUES
                        (@nextLeaseTariffCode, {defaultVehicleCode}, DATEADD(month, -2, GETDATE()), DATEADD(month, 10, GETDATE()), 4200, 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT LeaseTariff OFF;
                END
            ",
            $@"
                IF OBJECT_ID('LeaseTariff', 'U') IS NOT NULL
                BEGIN
                    DECLARE @nextLeaseTariffCode int = ISNULL((SELECT MAX(lease_tariff_code) FROM LeaseTariff), 0) + 1;
                    IF NOT EXISTS (SELECT 1 FROM LeaseTariff WHERE vmf_code = {defaultVehicleCode} AND active = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO LeaseTariff
                        (lease_tariff_code, vmf_code, start_date, end_date, fixed_tariff, active, date_created, created_by_user_code, is_deleted)
                        VALUES
                        (@nextLeaseTariffCode, {defaultVehicleCode}, DATEADD(month, -2, GETDATE()), DATEADD(month, 10, GETDATE()), 4200, 1, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Last_monthly_Tar', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Last_monthly_Tar WHERE ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Last_monthly_Tar (Last_monthly_Tar, date_created, created_by_user_code, is_deleted)
                    VALUES (CAST(GETDATE() AS date), GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('fin.TariffParameter', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM fin.TariffParameter WHERE TariffParameterYear = YEAR(GETDATE()) AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO fin.TariffParameter
                    (
                        TariffParameterYear, AnnualInterestRatePercentage, AnnualPayments, EffectiveInterestRate,
                        PoolVehicleChargedDaysPerMonth, CostCategoryMultiple, AnnualRecoveredKilos, AverageFuelPrice,
                        EffectiveDate, CaptureDate, user_access_code, user_access_name, Approved, ApprovalDate,
                        Approval_user_access_code, Approval_user_access_name, date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        YEAR(GETDATE()), 11.5, 12, 0.9583, 22, 1, 24000, 24.50,
                        CAST(GETDATE() AS date), GETDATE(), 1, 'seed-user', 1, GETDATE(), 1, 'seed-approver',
                        GETDATE(), {0}, 0
                    );
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('fin.TariffWeightCalculation', 'U') IS NOT NULL
            BEGIN
                DECLARE @tpId int = (
                    SELECT TOP 1 TariffParameterID
                    FROM fin.TariffParameter
                    WHERE ISNULL(is_deleted, 0) = 0
                    ORDER BY TariffParameterID DESC
                );
                IF @tpId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM fin.TariffWeightCalculation WHERE TariffParameterID = @tpId AND category = 1 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO fin.TariffWeightCalculation
                    (TariffParameterID, category, number, WeightFactorPerUnit, calculation_date_time, date_created, created_by_user_code, is_deleted)
                    VALUES
                    (@tpId, 1, 120, 1.15, GETDATE(), GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('fin.vehicle_tariff', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM fin.vehicle_tariff WHERE vmf_code = {0} AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO fin.vehicle_tariff
                    (
                        vmf_code, start_date, end_date, residual_percentage, parameter_year, annual_interest_percentage,
                        purchase_amount, purchase_date, purchase_amount_group, overhead_unit_factor,
                        target_replacement_date, year_manufactured, model_code, class_code, kilometer_life, months_life,
                        residual_amount, capital_payment, overhead_payment, adjustment_amount, vehicle_fixed_tariff,
                        vehicle_fixed_daily_tariff, vehicle_fixed_tariff_pool, class_fixed_tariff, class_fixed_pool_tariff,
                        lease_fixed_tariff, overhead_kilometer_amount, maintenance_kilometer_amount, vehicle_kilometer_tariff,
                        fuel_kilo_tariff, calculation_date, comment, date_created, created_by_user_code, is_deleted
                    )
                    VALUES
                    (
                        {0}, DATEADD(month, -1, GETDATE()), NULL, 30, YEAR(GETDATE()), 11.5,
                        450000, DATEADD(year, -2, GETDATE()), 2, 1.2,
                        DATEADD(year, 3, GETDATE()), 2023, 1, {1}, 240000, 72,
                        135000, 4200, 850, 0, 3500,
                        180, 3000, 3400, 3200,
                        4200, 0.85, 1.10, 2.20,
                        2.45, GETDATE(), 'Seeded tariff row', GETDATE(), {2}, 0
                    );
            END
        ", defaultVehicleCode, defaultClassCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('segment_type', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM segment_type WHERE segment_type_code = 1)
                    INSERT INTO segment_type (segment_type_code, segment_type_name, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'BAS', GETDATE(), {0}, 0);
            END

            IF OBJECT_ID('segment_group', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM segment_group WHERE segment_group_name = 'Cost Centre' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO segment_group
                    (segment_group_name, financial_system_code, segment_type_code, segment_group_isdebit, segment_group_isledger, date_created, created_by_user_code, is_deleted)
                    VALUES ('Cost Centre', 1, 1, 1, 1, GETDATE(), {0}, 0);
            END

            IF OBJECT_ID('segment', 'U') IS NOT NULL
            BEGIN
                DECLARE @segmentGroupCode int = (
                    SELECT TOP 1 segment_group_code
                    FROM segment_group
                    WHERE segment_group_name = 'Cost Centre' AND ISNULL(is_deleted, 0) = 0
                    ORDER BY segment_group_code
                );

                IF @segmentGroupCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM segment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO segment
                    (segment_number, segment_name, segment_group_code, department_code, site_code, date_created, created_by_user_code, is_deleted)
                    VALUES ('4500000', 'Office of the Chief Justice', @segmentGroupCode, 1, 1, GETDATE(), {0}, 0);
            END

            IF OBJECT_ID('journal_detail_type_group', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM journal_detail_type_group WHERE journal_detail_type_group_name = 'Billing' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO journal_detail_type_group
                    (journal_detail_type_group_name, date_created, created_by_user_code, is_deleted)
                    VALUES ('Billing', GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        Console.WriteLine("  ✓ Batch 10 tariff-segment coverage updates applied.");

        var tariffCount = await (await HasTableAsync(dbContext, "tariff") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM tariff WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var leaseTariffCount = await (await HasTableAsync(dbContext, "LeaseTariff") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM LeaseTariff WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var lastMonthlyTarCount = await (await HasTableAsync(dbContext, "Last_monthly_Tar") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Last_monthly_Tar WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tariffParameterCount = await (await HasTableAsync(dbContext, "TariffParameter") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.TariffParameter WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tariffWeightCount = await (await HasTableAsync(dbContext, "TariffWeightCalculation") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.TariffWeightCalculation WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vehicleTariffCount = await (await HasTableAsync(dbContext, "vehicle_tariff") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.vehicle_tariff WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentTypeCount = await (await HasTableAsync(dbContext, "segment_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentGroupCount = await (await HasTableAsync(dbContext, "segment_group") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_group WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentCount = await (await HasTableAsync(dbContext, "segment") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var journalDetailTypeGroupCount = await (await HasTableAsync(dbContext, "journal_detail_type_group") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail_type_group WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 10 | Tariff: {tariffCount} | LeaseTariff: {leaseTariffCount} | LastMonthlyTar: {lastMonthlyTarCount} | fin.TariffParameter: {tariffParameterCount} | fin.TariffWeight: {tariffWeightCount}");
        Console.WriteLine($"  📊 Batch 10 | fin.VehicleTariff: {vehicleTariffCount} | SegmentType: {segmentTypeCount} | SegmentGroup: {segmentGroupCount} | Segment: {segmentCount} | JournalDetailTypeGroup: {journalDetailTypeGroupCount}");
    }

    private static async Task ApplyBatch11ContractFinanceMappingCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('contract_status', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT contract_status ON;
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 1 OR status_description = 'Pending Approval')
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Pending Approval', 'PEND', 0, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 3 OR status_description IN ('Active', 'Approved Active'))
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (3, 'Approved Active', 'ACT', 1, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 7 OR status_description = 'Closed')
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (7, 'Closed', 'CLS', 0, 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT contract_status OFF;
                END
            ",
            $@"
                IF OBJECT_ID('contract_status', 'U') IS NOT NULL
                BEGIN
                    DECLARE @csCode int = ISNULL((SELECT MAX(contract_status_code) FROM contract_status), 0);
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 1 OR status_description = 'Pending Approval')
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (@csCode + 1, 'Pending Approval', 'PEND', 0, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 3 OR status_description IN ('Active', 'Approved Active'))
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (@csCode + 2, 'Approved Active', 'ACT', 1, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (
                        SELECT 1
                        FROM contract_status
                        WHERE ISNULL(is_deleted, 0) = 0
                          AND (contract_status_code = 7 OR status_description = 'Closed')
                    )
                        INSERT INTO contract_status
                        (contract_status_code, status_description, status_abbreviation, is_active, is_final, date_created, created_by_user_code, is_deleted)
                        VALUES (@csCode + 3, 'Closed', 'CLS', 0, 1, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Contract_type', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Contract_type WHERE contract_type = 'H' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Contract_type (contract_type, CT_description, CT_Active, date_created, created_by_user_code, is_deleted)
                    VALUES ('H', 'Hire Contract', 1, GETDATE(), {0}, 0);
                IF NOT EXISTS (SELECT 1 FROM Contract_type WHERE contract_type = 'R' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Contract_type (contract_type, CT_description, CT_Active, date_created, created_by_user_code, is_deleted)
                    VALUES ('R', 'Relief Contract', 1, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('Contract_Type_Grouping', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT Contract_Type_Grouping ON;
                    IF NOT EXISTS (SELECT 1 FROM Contract_Type_Grouping WHERE ctg_description = 'Standard Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Contract_Type_Grouping (ctg_code, ctg_description, Is_Active, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Standard Operations', 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Contract_Type_Grouping OFF;
                END
            ",
            $@"
                IF OBJECT_ID('Contract_Type_Grouping', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM Contract_Type_Grouping WHERE ctg_description = 'Standard Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Contract_Type_Grouping (ctg_description, Is_Active, date_created, created_by_user_code, is_deleted)
                        VALUES ('Standard Operations', 1, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Contract_Type_Map', 'U') IS NOT NULL
            BEGIN
                DECLARE @ctgCode int = (
                    SELECT TOP 1 ctg_code
                    FROM Contract_Type_Grouping
                    WHERE ctg_description = 'Standard Operations' AND ISNULL(is_deleted, 0) = 0
                    ORDER BY ctg_code
                );
                IF @ctgCode IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM Contract_Type_Map WHERE ctg_code = @ctgCode AND contract_type = 'H' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Contract_Type_Map (ctg_code, contract_type, date_created, created_by_user_code, is_deleted)
                        VALUES (@ctgCode, 'H', GETDATE(), {0}, 0);
                    IF NOT EXISTS (SELECT 1 FROM Contract_Type_Map WHERE ctg_code = @ctgCode AND contract_type = 'R' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Contract_Type_Map (ctg_code, contract_type, date_created, created_by_user_code, is_deleted)
                        VALUES (@ctgCode, 'R', GETDATE(), {0}, 0);
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('contract_type_mapping', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM contract_type_mapping WHERE Contract_Type = 'H' AND vs_code = 1 AND type_code = 1 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO contract_type_mapping (Contract_Type, vs_code, type_code, Is_Lease, date_created, created_by_user_code, is_deleted)
                    VALUES ('H', 1, 1, 0, GETDATE(), {0}, 0);

                IF NOT EXISTS (SELECT 1 FROM contract_type_mapping WHERE Contract_Type = 'R' AND vs_code = 1 AND type_code = 2 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO contract_type_mapping (Contract_Type, vs_code, type_code, Is_Lease, date_created, created_by_user_code, is_deleted)
                    VALUES ('R', 1, 2, 0, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Contract_Type_Group_Mapping', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Contract_Type_Group_Mapping WHERE vs_code = 1 AND type_code = 1 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Contract_Type_Group_Mapping (vs_code, type_code, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 1, GETDATE(), {0}, 0);

                IF NOT EXISTS (SELECT 1 FROM Contract_Type_Group_Mapping WHERE vs_code = 1 AND type_code = 2 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Contract_Type_Group_Mapping (vs_code, type_code, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 2, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('journal_detail_type', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT journal_detail_type ON;
                    IF NOT EXISTS (SELECT 1 FROM journal_detail_type WHERE journal_detail_type_name = 'Billing Revenue' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO journal_detail_type
                        (journal_detail_type_code, journal_detail_type_name, journal_detail_type_description, journal_detail_type_isggmt, journal_detail_type_isreversal, journal_detail_type_issuspense, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Billing Revenue', 'Standard billing revenue posting', 0, 0, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM journal_detail_type WHERE journal_detail_type_name = 'Billing Reversal' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO journal_detail_type
                        (journal_detail_type_code, journal_detail_type_name, journal_detail_type_description, journal_detail_type_isggmt, journal_detail_type_isreversal, journal_detail_type_issuspense, date_created, created_by_user_code, is_deleted)
                        VALUES (2, 'Billing Reversal', 'Reversal posting for corrections', 0, 1, 0, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT journal_detail_type OFF;
                END
            ",
            $@"
                IF OBJECT_ID('journal_detail_type', 'U') IS NOT NULL
                BEGIN
                    DECLARE @jdtCode int = ISNULL((SELECT MAX(journal_detail_type_code) FROM journal_detail_type), 0);
                    IF NOT EXISTS (SELECT 1 FROM journal_detail_type WHERE journal_detail_type_name = 'Billing Revenue' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO journal_detail_type
                        (journal_detail_type_code, journal_detail_type_name, journal_detail_type_description, journal_detail_type_isggmt, journal_detail_type_isreversal, journal_detail_type_issuspense, date_created, created_by_user_code, is_deleted)
                        VALUES (@jdtCode + 1, 'Billing Revenue', 'Standard billing revenue posting', 0, 0, 0, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM journal_detail_type WHERE journal_detail_type_name = 'Billing Reversal' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO journal_detail_type
                        (journal_detail_type_code, journal_detail_type_name, journal_detail_type_description, journal_detail_type_isggmt, journal_detail_type_isreversal, journal_detail_type_issuspense, date_created, created_by_user_code, is_deleted)
                        VALUES (@jdtCode + 2, 'Billing Reversal', 'Reversal posting for corrections', 0, 1, 0, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('cost_revenue_map', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT cost_revenue_map ON;
                    IF NOT EXISTS (SELECT 1 FROM cost_revenue_map WHERE journal_detail_type_code = 1 AND journal_detail_revenue_type_code = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO cost_revenue_map
                        (cost_revenue_map_code, journal_detail_type_code, journal_detail_revenue_type_code, journal_detail_revenue_type_description, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 1, 1, 'Revenue', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM cost_revenue_map WHERE journal_detail_type_code = 2 AND journal_detail_revenue_type_code = 2 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO cost_revenue_map
                        (cost_revenue_map_code, journal_detail_type_code, journal_detail_revenue_type_code, journal_detail_revenue_type_description, date_created, created_by_user_code, is_deleted)
                        VALUES (2, 2, 2, 'Reversal', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT cost_revenue_map OFF;
                END
            ",
            $@"
                IF OBJECT_ID('cost_revenue_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @crmCode int = ISNULL((SELECT MAX(cost_revenue_map_code) FROM cost_revenue_map), 0);
                    IF NOT EXISTS (SELECT 1 FROM cost_revenue_map WHERE journal_detail_type_code = 1 AND journal_detail_revenue_type_code = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO cost_revenue_map
                        (cost_revenue_map_code, journal_detail_type_code, journal_detail_revenue_type_code, journal_detail_revenue_type_description, date_created, created_by_user_code, is_deleted)
                        VALUES (@crmCode + 1, 1, 1, 'Revenue', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM cost_revenue_map WHERE journal_detail_type_code = 2 AND journal_detail_revenue_type_code = 2 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO cost_revenue_map
                        (cost_revenue_map_code, journal_detail_type_code, journal_detail_revenue_type_code, journal_detail_revenue_type_description, date_created, created_by_user_code, is_deleted)
                        VALUES (@crmCode + 2, 2, 2, 'Reversal', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('fuel_recovery_configuration', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT fuel_recovery_configuration ON;
                    IF NOT EXISTS (SELECT 1 FROM fuel_recovery_configuration WHERE recovery_percentage = 90.00 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fuel_recovery_configuration
                        (fuel_recovery_configuration_code, start_date, end_date, recovery_percentage, date_created, created_by_user_code, is_deleted)
                        VALUES (1, DATEADD(month, -1, GETDATE()), NULL, 90.00, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT fuel_recovery_configuration OFF;
                END
            ",
            $@"
                IF OBJECT_ID('fuel_recovery_configuration', 'U') IS NOT NULL
                BEGIN
                    DECLARE @frcCode int = ISNULL((SELECT MAX(fuel_recovery_configuration_code) FROM fuel_recovery_configuration), 0);
                    IF NOT EXISTS (SELECT 1 FROM fuel_recovery_configuration WHERE recovery_percentage = 90.00 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fuel_recovery_configuration
                        (fuel_recovery_configuration_code, start_date, end_date, recovery_percentage, date_created, created_by_user_code, is_deleted)
                        VALUES (@frcCode + 1, DATEADD(month, -1, GETDATE()), NULL, 90.00, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('overtime_multiplier', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT overtime_multiplier ON;
                    IF NOT EXISTS (SELECT 1 FROM overtime_multiplier WHERE overtime_multiplier = 1.50 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO overtime_multiplier
                        (overtime_multiplier_code, overtime_multiplier, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 1.50, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM overtime_multiplier WHERE overtime_multiplier = 2.00 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO overtime_multiplier
                        (overtime_multiplier_code, overtime_multiplier, date_created, created_by_user_code, is_deleted)
                        VALUES (2, 2.00, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT overtime_multiplier OFF;
                END
            ",
            $@"
                IF OBJECT_ID('overtime_multiplier', 'U') IS NOT NULL
                BEGIN
                    DECLARE @otCode int = ISNULL((SELECT MAX(overtime_multiplier_code) FROM overtime_multiplier), 0);
                    IF NOT EXISTS (SELECT 1 FROM overtime_multiplier WHERE overtime_multiplier = 1.50 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO overtime_multiplier
                        (overtime_multiplier_code, overtime_multiplier, date_created, created_by_user_code, is_deleted)
                        VALUES (@otCode + 1, 1.50, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM overtime_multiplier WHERE overtime_multiplier = 2.00 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO overtime_multiplier
                        (overtime_multiplier_code, overtime_multiplier, date_created, created_by_user_code, is_deleted)
                        VALUES (@otCode + 2, 2.00, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('journal_detail_type_segment_group_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentGroupCode int = (
                        SELECT TOP 1 segment_group_code FROM segment_group WHERE segment_group_name = 'Cost Centre' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_group_code
                    );
                    IF @segmentGroupCode IS NOT NULL
                    BEGIN
                        SET IDENTITY_INSERT journal_detail_type_segment_group_map ON;
                        IF NOT EXISTS (SELECT 1 FROM journal_detail_type_segment_group_map WHERE journal_detail_type_code = 1 AND segment_group_code = @segmentGroupCode AND ISNULL(is_deleted, 0) = 0)
                            INSERT INTO journal_detail_type_segment_group_map
                            (journal_detail_type_segment_group_map_code, journal_detail_type_code, segment_group_code, date_created, created_by_user_code, is_deleted)
                            VALUES (1, 1, @segmentGroupCode, GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT journal_detail_type_segment_group_map OFF;
                    END
                END
            ",
            $@"
                IF OBJECT_ID('journal_detail_type_segment_group_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentGroupCode int = (
                        SELECT TOP 1 segment_group_code FROM segment_group WHERE segment_group_name = 'Cost Centre' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_group_code
                    );
                    DECLARE @jdtsgmCode int = ISNULL((SELECT MAX(journal_detail_type_segment_group_map_code) FROM journal_detail_type_segment_group_map), 0);
                    IF @segmentGroupCode IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM journal_detail_type_segment_group_map WHERE journal_detail_type_code = 1 AND segment_group_code = @segmentGroupCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO journal_detail_type_segment_group_map
                        (journal_detail_type_segment_group_map_code, journal_detail_type_code, segment_group_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@jdtsgmCode + 1, 1, @segmentGroupCode, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        Console.WriteLine("  ✓ Batch 11 contract-finance mapping coverage updates applied.");

        var contractStatusCount = await (await HasTableAsync(dbContext, "contract_status") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract_status WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractTypeCount = await (await HasTableAsync(dbContext, "Contract_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contract_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractTypeGroupingCount = await (await HasTableAsync(dbContext, "Contract_Type_Grouping") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contract_Type_Grouping WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractTypeMapCount = await (await HasTableAsync(dbContext, "Contract_Type_Map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contract_Type_Map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractTypeMappingCount = await (await HasTableAsync(dbContext, "contract_type_mapping") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract_type_mapping WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractTypeGroupMappingCount = await (await HasTableAsync(dbContext, "Contract_Type_Group_Mapping") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contract_Type_Group_Mapping WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var journalDetailTypeCount = await (await HasTableAsync(dbContext, "journal_detail_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var costRevenueMapCount = await (await HasTableAsync(dbContext, "cost_revenue_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM cost_revenue_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var fuelRecoveryConfigCount = await (await HasTableAsync(dbContext, "fuel_recovery_configuration") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fuel_recovery_configuration WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var overtimeMultiplierCount = await (await HasTableAsync(dbContext, "overtime_multiplier") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM overtime_multiplier WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var detailSegmentMapCount = await (await HasTableAsync(dbContext, "journal_detail_type_segment_group_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail_type_segment_group_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 11 | ContractStatus: {contractStatusCount} | ContractType: {contractTypeCount} | ContractTypeGrouping: {contractTypeGroupingCount} | ContractTypeMap: {contractTypeMapCount} | ContractTypeMapping: {contractTypeMappingCount}");
        Console.WriteLine($"  📊 Batch 11 | ContractTypeGroupMapping: {contractTypeGroupMappingCount} | JournalDetailType: {journalDetailTypeCount} | CostRevenueMap: {costRevenueMapCount} | FuelRecoveryConfig: {fuelRecoveryConfigCount} | OvertimeMultiplier: {overtimeMultiplierCount}");
        Console.WriteLine($"  📊 Batch 11 | JournalDetailTypeSegmentGroupMap: {detailSegmentMapCount}");
    }

    private static async Task ApplyBatch12SegmentAnchorCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultSegmentGroupCode = await HasTableAsync(dbContext, "segment_group") &&
                                      await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_group WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 segment_group_code FROM segment_group WHERE ISNULL(is_deleted, 0) = 0 ORDER BY segment_group_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultProvinceCode = await HasTableAsync(dbContext, "province") &&
                                  await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM province WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 province_code FROM province WHERE ISNULL(is_deleted, 0) = 0 ORDER BY province_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('connection_type', 'U') IS NOT NULL
                BEGIN
                    DECLARE @connCode int = ISNULL((SELECT MAX(connection_id) FROM connection_type), 0);
                    SET IDENTITY_INSERT connection_type ON;
                    IF NOT EXISTS (SELECT 1 FROM connection_type WHERE description = 'Direct' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO connection_type (connection_id, description, date_created, created_by_user_code, is_deleted)
                        VALUES (@connCode + 1, 'Direct', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM connection_type WHERE description = 'Transfer' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO connection_type (connection_id, description, date_created, created_by_user_code, is_deleted)
                        VALUES (@connCode + 2, 'Transfer', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT connection_type OFF;
                END
            ",
            $@"
                IF OBJECT_ID('connection_type', 'U') IS NOT NULL
                BEGIN
                    DECLARE @connCode int = ISNULL((SELECT MAX(connection_id) FROM connection_type), 0);
                    IF NOT EXISTS (SELECT 1 FROM connection_type WHERE description = 'Direct' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO connection_type (connection_id, description, date_created, created_by_user_code, is_deleted)
                        VALUES (@connCode + 1, 'Direct', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM connection_type WHERE description = 'Transfer' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO connection_type (connection_id, description, date_created, created_by_user_code, is_deleted)
                        VALUES (@connCode + 2, 'Transfer', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('anchor_types', 'U') IS NOT NULL
            BEGIN
                DECLARE @anchorTypeCode int = ISNULL((SELECT MAX(anchor_type_code) FROM anchor_types), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('anchor_types'), 'anchor_type_code', 'IsIdentity');

                IF @isIdentity = 1
                BEGIN
                    SET IDENTITY_INSERT anchor_types ON;
                    IF NOT EXISTS (SELECT 1 FROM anchor_types WHERE anchor_type_name = 'Manual ODO' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO anchor_types (anchor_type_code, anchor_type_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@anchorTypeCode + 1, 'Manual ODO', GETDATE(), {0}, 0);
                    IF NOT EXISTS (SELECT 1 FROM anchor_types WHERE anchor_type_name = 'Telematics' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO anchor_types (anchor_type_code, anchor_type_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@anchorTypeCode + 2, 'Telematics', GETDATE(), {0}, 0);
                    SET IDENTITY_INSERT anchor_types OFF;
                END
                ELSE
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM anchor_types WHERE anchor_type_name = 'Manual ODO' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO anchor_types (anchor_type_code, anchor_type_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@anchorTypeCode + 1, 'Manual ODO', GETDATE(), {0}, 0);
                    IF NOT EXISTS (SELECT 1 FROM anchor_types WHERE anchor_type_name = 'Telematics' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO anchor_types (anchor_type_code, anchor_type_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@anchorTypeCode + 2, 'Telematics', GETDATE(), {0}, 0);
                END
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('bassegment', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentCode int = ISNULL((SELECT MAX(segment_code) FROM bassegment), 0);
                    SET IDENTITY_INSERT bassegment ON;
                    IF NOT EXISTS (SELECT 1 FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO bassegment
                        (segment_code, segment_number, segment_name, segment_group_code, department_code, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@segmentCode + 1, '4500000', 'Office Operations', {defaultSegmentGroupCode}, {defaultDepartmentCode}, {defaultSiteCode}, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM bassegment WHERE segment_number = '4500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO bassegment
                        (segment_code, segment_number, segment_name, segment_group_code, department_code, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@segmentCode + 2, '4500001', 'Fleet Operations', {defaultSegmentGroupCode}, {defaultDepartmentCode}, {defaultSiteCode}, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT bassegment OFF;
                END
            ",
            $@"
                IF OBJECT_ID('bassegment', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentCode int = ISNULL((SELECT MAX(segment_code) FROM bassegment), 0);
                    IF NOT EXISTS (SELECT 1 FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO bassegment
                        (segment_code, segment_number, segment_name, segment_group_code, department_code, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@segmentCode + 1, '4500000', 'Office Operations', {defaultSegmentGroupCode}, {defaultDepartmentCode}, {defaultSiteCode}, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM bassegment WHERE segment_number = '4500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO bassegment
                        (segment_code, segment_number, segment_name, segment_group_code, department_code, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@segmentCode + 2, '4500001', 'Fleet Operations', {defaultSegmentGroupCode}, {defaultDepartmentCode}, {defaultSiteCode}, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('province_segment_map', 'U') IS NOT NULL
            BEGIN
                DECLARE @segmentCode int = (
                    SELECT TOP 1 segment_code
                    FROM bassegment
                    WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0
                    ORDER BY segment_code
                );
                IF @segmentCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM province_segment_map WHERE Province_code = {0} AND segment_code = @segmentCode AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO province_segment_map
                    (Province_code, segment_code, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, @segmentCode, GETDATE(), {1}, 0);
            END
        ", defaultProvinceCode, defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('segment_scoa', 'U') IS NOT NULL
                BEGIN
                    DECLARE @scoaCode int = ISNULL((SELECT MAX(segment_scoa_code) FROM segment_scoa), 0);
                    SET IDENTITY_INSERT segment_scoa ON;
                    IF NOT EXISTS (SELECT 1 FROM segment_scoa WHERE segment_name = 'Cost Centre Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO segment_scoa
                        (segment_scoa_code, segment_group_code, segment_name, segment_part, scoa_version, date_created, created_by_user_code, is_deleted)
                        VALUES (@scoaCode + 1, CAST({defaultSegmentGroupCode} AS smallint), 'Cost Centre Operations', 'SCOA-CC-OPS', 6, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT segment_scoa OFF;
                END
            ",
            $@"
                IF OBJECT_ID('segment_scoa', 'U') IS NOT NULL
                BEGIN
                    DECLARE @scoaCode int = ISNULL((SELECT MAX(segment_scoa_code) FROM segment_scoa), 0);
                    IF NOT EXISTS (SELECT 1 FROM segment_scoa WHERE segment_name = 'Cost Centre Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO segment_scoa
                        (segment_scoa_code, segment_group_code, segment_name, segment_part, scoa_version, date_created, created_by_user_code, is_deleted)
                        VALUES (@scoaCode + 1, CAST({defaultSegmentGroupCode} AS smallint), 'Cost Centre Operations', 'SCOA-CC-OPS', 6, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('segment_structure_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @parentSegmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @childSegmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500001' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @ssmCode int = ISNULL((SELECT MAX(segment_structure_map_code) FROM segment_structure_map), 0);
                    IF @parentSegmentCode IS NOT NULL AND @childSegmentCode IS NOT NULL
                    BEGIN
                        SET IDENTITY_INSERT segment_structure_map ON;
                        IF NOT EXISTS (SELECT 1 FROM segment_structure_map WHERE segment_parent_code = @parentSegmentCode AND segment_child_code = @childSegmentCode AND ISNULL(is_deleted, 0) = 0)
                            INSERT INTO segment_structure_map
                            (segment_structure_map_code, segment_parent_code, segment_child_code, date_created, created_by_user_code, is_deleted)
                            VALUES (@ssmCode + 1, @parentSegmentCode, @childSegmentCode, GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT segment_structure_map OFF;
                    END
                END
            ",
            $@"
                IF OBJECT_ID('segment_structure_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @parentSegmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @childSegmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500001' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @ssmCode int = ISNULL((SELECT MAX(segment_structure_map_code) FROM segment_structure_map), 0);
                    IF @parentSegmentCode IS NOT NULL AND @childSegmentCode IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM segment_structure_map WHERE segment_parent_code = @parentSegmentCode AND segment_child_code = @childSegmentCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO segment_structure_map
                        (segment_structure_map_code, segment_parent_code, segment_child_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@ssmCode + 1, @parentSegmentCode, @childSegmentCode, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('segment_journal_detail_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @journalDetailCode uniqueidentifier = (
                        SELECT TOP 1 journal_detail_code FROM journal_detail WHERE ISNULL(is_deleted, 0) = 0 ORDER BY date_created
                    );
                    DECLARE @sjdmCode bigint = ISNULL((SELECT MAX(segment_journal_detail_map_code) FROM segment_journal_detail_map), 0);
                    IF @segmentCode IS NOT NULL AND @journalDetailCode IS NOT NULL
                    BEGIN
                        SET IDENTITY_INSERT segment_journal_detail_map ON;
                        IF NOT EXISTS (SELECT 1 FROM segment_journal_detail_map WHERE segment_code = @segmentCode AND journal_detail_code = @journalDetailCode AND ISNULL(is_deleted, 0) = 0)
                            INSERT INTO segment_journal_detail_map
                            (segment_journal_detail_map_code, segment_code, journal_detail_code, date_created, created_by_user_code, is_deleted)
                            VALUES (@sjdmCode + 1, @segmentCode, @journalDetailCode, GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT segment_journal_detail_map OFF;
                    END
                END
            ",
            $@"
                IF OBJECT_ID('segment_journal_detail_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @segmentCode int = (
                        SELECT TOP 1 segment_code FROM bassegment WHERE segment_number = '4500000' AND ISNULL(is_deleted, 0) = 0 ORDER BY segment_code
                    );
                    DECLARE @journalDetailCode uniqueidentifier = (
                        SELECT TOP 1 journal_detail_code FROM journal_detail WHERE ISNULL(is_deleted, 0) = 0 ORDER BY date_created
                    );
                    DECLARE @sjdmCode bigint = ISNULL((SELECT MAX(segment_journal_detail_map_code) FROM segment_journal_detail_map), 0);
                    IF @segmentCode IS NOT NULL AND @journalDetailCode IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM segment_journal_detail_map WHERE segment_code = @segmentCode AND journal_detail_code = @journalDetailCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO segment_journal_detail_map
                        (segment_journal_detail_map_code, segment_code, journal_detail_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@sjdmCode + 1, @segmentCode, @journalDetailCode, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('mf_code', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mfCode int = ISNULL((SELECT MAX(mf_code_code) FROM mf_code), 0);
                    SET IDENTITY_INSERT mf_code ON;
                    IF NOT EXISTS (SELECT 1 FROM mf_code WHERE mf_code_number = 'CC' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO mf_code
                        (mf_code_code, mf_code_number, mf_code_name, mf_code_value_mask, segment_group_code, mf_code_order, date_created, created_by_user_code, is_deleted)
                        VALUES (@mfCode + 1, 'CC', 'Cost Centre', '#######', {defaultSegmentGroupCode}, 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT mf_code OFF;
                END
            ",
            $@"
                IF OBJECT_ID('mf_code', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mfCode int = ISNULL((SELECT MAX(mf_code_code) FROM mf_code), 0);
                    IF NOT EXISTS (SELECT 1 FROM mf_code WHERE mf_code_number = 'CC' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO mf_code
                        (mf_code_code, mf_code_number, mf_code_name, mf_code_value_mask, segment_group_code, mf_code_order, date_created, created_by_user_code, is_deleted)
                        VALUES (@mfCode + 1, 'CC', 'Cost Centre', '#######', {defaultSegmentGroupCode}, 1, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('mf_code_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mfCodeCode int = (
                        SELECT TOP 1 mf_code_code FROM mf_code WHERE mf_code_number = 'CC' AND ISNULL(is_deleted, 0) = 0 ORDER BY mf_code_code
                    );
                    DECLARE @sjdmCode bigint = (
                        SELECT TOP 1 segment_journal_detail_map_code FROM segment_journal_detail_map WHERE ISNULL(is_deleted, 0) = 0 ORDER BY segment_journal_detail_map_code
                    );
                    DECLARE @mfMapCode int = ISNULL((SELECT MAX(mf_code_map_code) FROM mf_code_map), 0);
                    IF @mfCodeCode IS NOT NULL AND @sjdmCode IS NOT NULL
                    BEGIN
                        SET IDENTITY_INSERT mf_code_map ON;
                        IF NOT EXISTS (SELECT 1 FROM mf_code_map WHERE mf_code_code = @mfCodeCode AND segment_journal_detail_map_code = @sjdmCode AND ISNULL(is_deleted, 0) = 0)
                            INSERT INTO mf_code_map
                            (mf_code_map_code, segment_journal_detail_map_code, mf_code_code, mf_code_value, date_created, created_by_user_code, is_deleted)
                            VALUES (@mfMapCode + 1, @sjdmCode, @mfCodeCode, '4500000', GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT mf_code_map OFF;
                    END
                END
            ",
            $@"
                IF OBJECT_ID('mf_code_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mfCodeCode int = (
                        SELECT TOP 1 mf_code_code FROM mf_code WHERE mf_code_number = 'CC' AND ISNULL(is_deleted, 0) = 0 ORDER BY mf_code_code
                    );
                    DECLARE @sjdmCode bigint = (
                        SELECT TOP 1 segment_journal_detail_map_code FROM segment_journal_detail_map WHERE ISNULL(is_deleted, 0) = 0 ORDER BY segment_journal_detail_map_code
                    );
                    DECLARE @mfMapCode int = ISNULL((SELECT MAX(mf_code_map_code) FROM mf_code_map), 0);
                    IF @mfCodeCode IS NOT NULL AND @sjdmCode IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM mf_code_map WHERE mf_code_code = @mfCodeCode AND segment_journal_detail_map_code = @sjdmCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO mf_code_map
                        (mf_code_map_code, segment_journal_detail_map_code, mf_code_code, mf_code_value, date_created, created_by_user_code, is_deleted)
                        VALUES (@mfMapCode + 1, @sjdmCode, @mfCodeCode, '4500000', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('anchor_points', 'U') IS NOT NULL
                BEGIN
                    DECLARE @anchorTypeCode int = (
                        SELECT TOP 1 anchor_type_code FROM anchor_types WHERE anchor_type_name = 'Manual ODO' AND ISNULL(is_deleted, 0) = 0 ORDER BY anchor_type_code
                    );
                    DECLARE @anchorPointCode int = ISNULL((SELECT MAX(anchor_point_code) FROM anchor_points), 0);
                    IF @anchorTypeCode IS NOT NULL
                    BEGIN
                        SET IDENTITY_INSERT anchor_points ON;
                        IF NOT EXISTS (SELECT 1 FROM anchor_points WHERE vmf_code = {defaultVehicleCode} AND anchor_odo_meter = 12000 AND ISNULL(is_deleted, 0) = 0)
                            INSERT INTO anchor_points
                            (anchor_point_code, vmf_code, anchor_odo_meter, anchor_date, anchor_type_code, bas_journal_record_code, date_created, created_by_user_code, is_deleted)
                            VALUES (@anchorPointCode + 1, {defaultVehicleCode}, 12000, DATEADD(day, -20, GETDATE()), CAST(@anchorTypeCode AS tinyint), NULL, GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT anchor_points OFF;
                    END
                END
            ",
            $@"
                IF OBJECT_ID('anchor_points', 'U') IS NOT NULL
                BEGIN
                    DECLARE @anchorTypeCode int = (
                        SELECT TOP 1 anchor_type_code FROM anchor_types WHERE anchor_type_name = 'Manual ODO' AND ISNULL(is_deleted, 0) = 0 ORDER BY anchor_type_code
                    );
                    DECLARE @anchorPointCode int = ISNULL((SELECT MAX(anchor_point_code) FROM anchor_points), 0);
                    IF @anchorTypeCode IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM anchor_points WHERE vmf_code = {defaultVehicleCode} AND anchor_odo_meter = 12000 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO anchor_points
                        (anchor_point_code, vmf_code, anchor_odo_meter, anchor_date, anchor_type_code, bas_journal_record_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@anchorPointCode + 1, {defaultVehicleCode}, 12000, DATEADD(day, -20, GETDATE()), CAST(@anchorTypeCode AS tinyint), NULL, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        Console.WriteLine("  ✓ Batch 12 segment-anchor mapping coverage updates applied.");

        var connectionTypeCount = await (await HasTableAsync(dbContext, "connection_type") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM connection_type WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var anchorTypeCount = await (await HasTableAsync(dbContext, "anchor_types") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM anchor_types WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var basSegmentCount = await (await HasTableAsync(dbContext, "bassegment") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM bassegment WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var provinceSegmentMapCount = await (await HasTableAsync(dbContext, "province_segment_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM province_segment_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentScoaCount = await (await HasTableAsync(dbContext, "segment_scoa") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_scoa WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentStructureMapCount = await (await HasTableAsync(dbContext, "segment_structure_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_structure_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var segmentJournalDetailMapCount = await (await HasTableAsync(dbContext, "segment_journal_detail_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM segment_journal_detail_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var mfCodeCount = await (await HasTableAsync(dbContext, "mf_code") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM mf_code WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var mfCodeMapCount = await (await HasTableAsync(dbContext, "mf_code_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM mf_code_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var anchorPointsCount = await (await HasTableAsync(dbContext, "anchor_points") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM anchor_points WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 12 | ConnectionType: {connectionTypeCount} | AnchorTypes: {anchorTypeCount} | BasSegment: {basSegmentCount} | ProvinceSegmentMap: {provinceSegmentMapCount} | SegmentScoa: {segmentScoaCount}");
        Console.WriteLine($"  📊 Batch 12 | SegmentStructureMap: {segmentStructureMapCount} | SegmentJournalDetailMap: {segmentJournalDetailMapCount} | MfCode: {mfCodeCount} | MfCodeMap: {mfCodeMapCount} | AnchorPoints: {anchorPointsCount}");
    }

    private static async Task ApplyBatch13AuthOperationsCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        var defaultRankCode = await HasTableAsync(dbContext, "ranks") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM ranks WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 rank_code FROM ranks WHERE ISNULL(is_deleted, 0) = 0 ORDER BY rank_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('AccessLevels', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT AccessLevels ON;
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels WHERE AccessLevelID = 1)
                        INSERT INTO AccessLevels (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Admin', 9223372036854775807, NULL, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels WHERE AccessLevelID = 2)
                        INSERT INTO AccessLevels (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (2, 'User', 1024, NULL, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT AccessLevels OFF;
                END
            ",
            $@"
                IF OBJECT_ID('AccessLevels', 'U') IS NOT NULL
                BEGIN
                    DECLARE @alCode int = ISNULL((SELECT MAX(AccessLevelID) FROM AccessLevels), 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels WHERE AccessLevelName = 'Admin' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO AccessLevels (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (@alCode + 1, 'Admin', 9223372036854775807, NULL, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels WHERE AccessLevelName = 'User' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO AccessLevels (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (@alCode + 2, 'User', 1024, NULL, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('AccessLevels_2', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT AccessLevels_2 ON;
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels_2 WHERE AccessLevelID = 1)
                        INSERT INTO AccessLevels_2 (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'FinanceAdmin', 2048, NULL, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels_2 WHERE AccessLevelID = 2)
                        INSERT INTO AccessLevels_2 (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (2, 'Operations', 4096, NULL, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT AccessLevels_2 OFF;
                END
            ",
            $@"
                IF OBJECT_ID('AccessLevels_2', 'U') IS NOT NULL
                BEGIN
                    DECLARE @al2Code int = ISNULL((SELECT MAX(AccessLevelID) FROM AccessLevels_2), 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels_2 WHERE AccessLevelName = 'FinanceAdmin' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO AccessLevels_2 (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (@al2Code + 1, 'FinanceAdmin', 2048, NULL, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM AccessLevels_2 WHERE AccessLevelName = 'Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO AccessLevels_2 (AccessLevelID, AccessLevelName, AccessLevelValue, AccessLevelCalc, date_created, created_by_user_code, is_deleted)
                        VALUES (@al2Code + 2, 'Operations', 4096, NULL, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('user_access_old1', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT user_access_old1 ON;
                    IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE name = 'legacy.seed.admin' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_access_old1
                        (
                            user_access_code, Site_code, name, password, user_status, last_log_on, AccessLevel, E_Mail, telephone,
                            FirstName, LastName, user_active, Retry, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (9101, CAST({defaultSiteCode} AS smallint), 'legacy.seed.admin', 'seeded-hash', 'Active', GETDATE(), 9223372036854775807, 'legacy.admin@fis.local', '0110000101',
                         'Legacy', 'Admin', 1, 0, GETDATE(), {defaultUserCode}, 0);

                    IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE name = 'legacy.seed.user' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_access_old1
                        (
                            user_access_code, Site_code, name, password, user_status, last_log_on, AccessLevel, E_Mail, telephone,
                            FirstName, LastName, user_active, Retry, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (9102, CAST({defaultSiteCode} AS smallint), 'legacy.seed.user', 'seeded-hash', 'Active', GETDATE(), 1024, 'legacy.user@fis.local', '0110000102',
                         'Legacy', 'User', 1, 0, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT user_access_old1 OFF;
                END
            ",
            $@"
                IF OBJECT_ID('user_access_old1', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE name = 'legacy.seed.admin' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_access_old1
                        (
                            Site_code, name, password, user_status, last_log_on, AccessLevel, E_Mail, telephone,
                            FirstName, LastName, user_active, Retry, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (CAST({defaultSiteCode} AS smallint), 'legacy.seed.admin', 'seeded-hash', 'Active', GETDATE(), 9223372036854775807, 'legacy.admin@fis.local', '0110000101',
                         'Legacy', 'Admin', 1, 0, GETDATE(), {defaultUserCode}, 0);

                    IF NOT EXISTS (SELECT 1 FROM user_access_old1 WHERE name = 'legacy.seed.user' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_access_old1
                        (
                            Site_code, name, password, user_status, last_log_on, AccessLevel, E_Mail, telephone,
                            FirstName, LastName, user_active, Retry, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (CAST({defaultSiteCode} AS smallint), 'legacy.seed.user', 'seeded-hash', 'Active', GETDATE(), 1024, 'legacy.user@fis.local', '0110000102',
                         'Legacy', 'User', 1, 0, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('approvers', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM approvers WHERE Surname = 'Seed' AND Firstname = 'Approver' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO approvers
                    (site_code, department_code, rank_code, Surname, Firstname, date_created, created_by_user_code, is_deleted)
                    VALUES (CAST({0} AS smallint), {1}, {2}, 'Seed', 'Approver', GETDATE(), {3}, 0);
            END
        ", defaultSiteCode, defaultDepartmentCode, defaultRankCode, defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('user_message', 'U') IS NOT NULL
                BEGIN
                    DECLARE @msgCode int = ISNULL((SELECT MAX(user_message_code) FROM user_message), 0);
                    SET IDENTITY_INSERT user_message ON;
                    IF NOT EXISTS (SELECT 1 FROM user_message WHERE user_access_code = {defaultUserCode} AND message = 'System seeded notification' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_message
                        (user_message_code, user_access_code, message, message_read, date_created, created_by_user_code, is_deleted)
                        VALUES (@msgCode + 1, {defaultUserCode}, 'System seeded notification', 'N', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT user_message OFF;
                END
            ",
            $@"
                IF OBJECT_ID('user_message', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM user_message WHERE user_access_code = {defaultUserCode} AND message = 'System seeded notification' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO user_message
                        (user_access_code, message, message_read, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultUserCode}, 'System seeded notification', 'N', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('profile_history', 'U') IS NOT NULL
                BEGIN
                    DECLARE @profileCode int = ISNULL((SELECT MAX(profile_history_code) FROM profile_history), 0);
                    SET IDENTITY_INSERT profile_history ON;
                    IF NOT EXISTS (SELECT 1 FROM profile_history WHERE vmf_code = {defaultVehicleCode} AND profile_code = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO profile_history
                        (profile_history_code, vmf_code, trans_code, profile_code, activity_odo, activity_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@profileCode + 1, {defaultVehicleCode}, NULL, 1, 15000, GETDATE(), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT profile_history OFF;
                END
            ",
            $@"
                IF OBJECT_ID('profile_history', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM profile_history WHERE vmf_code = {defaultVehicleCode} AND profile_code = 1 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO profile_history
                        (vmf_code, trans_code, profile_code, activity_odo, activity_date, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultVehicleCode}, NULL, 1, 15000, GETDATE(), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('TS_Comment', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM TS_Comment WHERE Ref_number = {0} AND Comments = 'Seeded comment for workflow trace' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO TS_Comment
                    (Ref_number, Comments, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, 'Seeded comment for workflow trace', GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('Notify_List', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT Notify_List ON;
                    IF NOT EXISTS (SELECT 1 FROM Notify_List WHERE Notify_list_desc = 'Operations Alerts' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Notify_List (Notify_list_code, Notify_list_desc, Notify_email1, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Operations Alerts', 'ops.alerts@fis.local', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Notify_List OFF;
                END
            ",
            $@"
                IF OBJECT_ID('Notify_List', 'U') IS NOT NULL
                BEGIN
                    DECLARE @notifyCode int = ISNULL((SELECT MAX(Notify_list_code) FROM Notify_List), 0);
                    IF NOT EXISTS (SELECT 1 FROM Notify_List WHERE Notify_list_desc = 'Operations Alerts' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Notify_List (Notify_list_code, Notify_list_desc, Notify_email1, date_created, created_by_user_code, is_deleted)
                        VALUES (@notifyCode + 1, 'Operations Alerts', 'ops.alerts@fis.local', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('vip_site_map', 'U') IS NOT NULL
                BEGIN
                    SET IDENTITY_INSERT vip_site_map ON;
                    IF NOT EXISTS (SELECT 1 FROM vip_site_map WHERE site_code = {defaultSiteCode} AND end_date IS NULL AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO vip_site_map
                        (vip_site_map_code, site_code, start_date, end_date, notes, date_updated, created_by_user_code, is_deleted)
                        VALUES (1, {defaultSiteCode}, DATEADD(day, -30, GETDATE()), NULL, 'Seeded VIP routing map', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT vip_site_map OFF;
                END
            ",
            $@"
                IF OBJECT_ID('vip_site_map', 'U') IS NOT NULL
                BEGIN
                    DECLARE @vipCode int = ISNULL((SELECT MAX(vip_site_map_code) FROM vip_site_map), 0);
                    IF NOT EXISTS (SELECT 1 FROM vip_site_map WHERE site_code = {defaultSiteCode} AND end_date IS NULL AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO vip_site_map
                        (vip_site_map_code, site_code, start_date, end_date, notes, date_updated, created_by_user_code, is_deleted)
                        VALUES (@vipCode + 1, {defaultSiteCode}, DATEADD(day, -30, GETDATE()), NULL, 'Seeded VIP routing map', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('EntraId_User_Mapping', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM EntraId_User_Mapping WHERE user_access_code = {0})
                    INSERT INTO EntraId_User_Mapping
                    (entra_object_id, user_access_code, created_date)
                    VALUES (CONCAT('seed-', CAST({0} AS varchar(20)), '-object-id'), {0}, GETDATE());
            END
        ", defaultUserCode);

        Console.WriteLine("  ✓ Batch 13 auth-ops coverage updates applied.");

        var accessLevelsCount = await (await HasTableAsync(dbContext, "AccessLevels") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM AccessLevels WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var accessLevels2Count = await (await HasTableAsync(dbContext, "AccessLevels_2") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM AccessLevels_2 WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var userAccessOldCount = await (await HasTableAsync(dbContext, "user_access_old1") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM user_access_old1 WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var approversCount = await (await HasTableAsync(dbContext, "approvers") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM approvers WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var userMessageCount = await (await HasTableAsync(dbContext, "user_message") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM user_message WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var profileHistoryCount = await (await HasTableAsync(dbContext, "profile_history") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM profile_history WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tsCommentCount = await (await HasTableAsync(dbContext, "TS_Comment") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Comment WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notifyListCount = await (await HasTableAsync(dbContext, "Notify_List") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Notify_List WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vipSiteMapCount = await (await HasTableAsync(dbContext, "vip_site_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vip_site_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var entraMapCount = await (await HasTableAsync(dbContext, "EntraId_User_Mapping") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM EntraId_User_Mapping") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 13 | AccessLevels: {accessLevelsCount} | AccessLevels_2: {accessLevels2Count} | UserAccessOld: {userAccessOldCount} | Approvers: {approversCount} | UserMessage: {userMessageCount}");
        Console.WriteLine($"  📊 Batch 13 | ProfileHistory: {profileHistoryCount} | TSComment: {tsCommentCount} | NotifyList: {notifyListCount} | VipSiteMap: {vipSiteMapCount} | EntraMappings: {entraMapCount}");
    }

    private static async Task ApplyBatch14MaintenanceWorkshopCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultClassCode = await HasTableAsync(dbContext, "class") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM class WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 class_code FROM class WHERE ISNULL(is_deleted, 0) = 0 ORDER BY class_code")
            : 1;

        var defaultTariffParameterId = await HasTableAsync(dbContext, "TariffParameter") &&
                                       await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.TariffParameter WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 TariffParameterID FROM fin.TariffParameter WHERE ISNULL(is_deleted, 0) = 0 ORDER BY TariffParameterID DESC")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('bill_of_material', 'U') IS NOT NULL
                BEGIN
                    DECLARE @bomCode int = ISNULL((SELECT MAX(bom_code) FROM bill_of_material), 0);
                    SET IDENTITY_INSERT bill_of_material ON;
                    IF NOT EXISTS (SELECT 1 FROM bill_of_material WHERE bom_code = 1)
                        INSERT INTO bill_of_material (bom_code, quantity, date_created, created_by_user_code, is_deleted)
                        VALUES (@bomCode + 1, 1.00, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT bill_of_material OFF;
                END
            ",
            $@"
                IF OBJECT_ID('bill_of_material', 'U') IS NOT NULL
                BEGIN
                    DECLARE @bomCode int = ISNULL((SELECT MAX(bom_code) FROM bill_of_material), 0);
                    IF NOT EXISTS (SELECT 1 FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0)
                        INSERT INTO bill_of_material (bom_code, quantity, date_created, created_by_user_code, is_deleted)
                        VALUES (@bomCode + 1, 1.00, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('part', 'U') IS NOT NULL
                BEGIN
                    DECLARE @partCode int = ISNULL((SELECT MAX(part_code) FROM part), 0);
                    DECLARE @bomCode int = ISNULL((SELECT TOP 1 bom_code FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0 ORDER BY bom_code), @partCode + 1);
                    SET IDENTITY_INSERT part ON;
                    IF NOT EXISTS (SELECT 1 FROM part WHERE part_number = 'PT-0001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO part (part_code, bom_code, part_number, description, qty_on_hand, qty_on_order, date_created, created_by_user_code, is_deleted)
                        VALUES (@partCode + 1, @bomCode, 'PT-0001', 'Oil Filter', 20, 5, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT part OFF;
                END
            ",
            $@"
                IF OBJECT_ID('part', 'U') IS NOT NULL
                BEGIN
                    DECLARE @partCode int = ISNULL((SELECT MAX(part_code) FROM part), 0);
                    DECLARE @bomCode int = ISNULL((SELECT TOP 1 bom_code FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0 ORDER BY bom_code), @partCode + 1);
                    IF NOT EXISTS (SELECT 1 FROM part WHERE part_number = 'PT-0001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO part (part_code, bom_code, part_number, description, qty_on_hand, qty_on_order, date_created, created_by_user_code, is_deleted)
                        VALUES (@partCode + 1, @bomCode, 'PT-0001', 'Oil Filter', 20, 5, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('task', 'U') IS NOT NULL
                BEGIN
                    DECLARE @taskCode int = ISNULL((SELECT MAX(task_code) FROM task), 0);
                    DECLARE @bomCode int = ISNULL((SELECT TOP 1 bom_code FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0 ORDER BY bom_code), @taskCode + 1);
                    SET IDENTITY_INSERT task ON;
                    IF NOT EXISTS (SELECT 1 FROM task WHERE description = 'Standard 15k Service' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO task (task_code, profile_code, bom_code, description, duration_hours, date_created, created_by_user_code, is_deleted)
                        VALUES (@taskCode + 1, 1, @bomCode, 'Standard 15k Service', 2.5, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT task OFF;
                END
            ",
            $@"
                IF OBJECT_ID('task', 'U') IS NOT NULL
                BEGIN
                    DECLARE @taskCode int = ISNULL((SELECT MAX(task_code) FROM task), 0);
                    DECLARE @bomCode int = ISNULL((SELECT TOP 1 bom_code FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0 ORDER BY bom_code), @taskCode + 1);
                    IF NOT EXISTS (SELECT 1 FROM task WHERE description = 'Standard 15k Service' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO task (task_code, profile_code, bom_code, description, duration_hours, date_created, created_by_user_code, is_deleted)
                        VALUES (@taskCode + 1, 1, @bomCode, 'Standard 15k Service', 2.5, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('maint_profile_model', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM maint_profile_model WHERE profile_code = 1 AND model_code = 1 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO maint_profile_model
                    (profile_code, model_code, description, trigger_type, trigger_description, interval, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 1, 'Basic maintenance profile', 'KM', 'Every 15000 KM', 15000, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('wwmerchant', 'U') IS NOT NULL
                BEGIN
                    DECLARE @merchantCode int = ISNULL((SELECT MAX(wwmerch_code) FROM wwmerchant), 0);
                    SET IDENTITY_INSERT wwmerchant ON;
                    IF NOT EXISTS (SELECT 1 FROM wwmerchant WHERE wwmerch_name = 'Fleet Workshop Central' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO wwmerchant
                        (wwmerch_code, wwmerch_name, wwmerch_tel, wwmerch_fax, wwmerch_email, date_created, created_by_user_code, is_deleted)
                        VALUES (@merchantCode + 1, 'Fleet Workshop Central', '0110000200', '0110000201', 'workshop@fis.local', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT wwmerchant OFF;
                END
            ",
            $@"
                IF OBJECT_ID('wwmerchant', 'U') IS NOT NULL
                BEGIN
                    DECLARE @merchantCode int = ISNULL((SELECT MAX(wwmerch_code) FROM wwmerchant), 0);
                    IF NOT EXISTS (SELECT 1 FROM wwmerchant WHERE wwmerch_name = 'Fleet Workshop Central' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO wwmerchant
                        (wwmerch_code, wwmerch_name, wwmerch_tel, wwmerch_fax, wwmerch_email, date_created, created_by_user_code, is_deleted)
                        VALUES (@merchantCode + 1, 'Fleet Workshop Central', '0110000200', '0110000201', 'workshop@fis.local', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('maintenance_trigger', 'U') IS NOT NULL
                BEGIN
                    DECLARE @triggerCode int = ISNULL((SELECT MAX(maint_trigger_code) FROM maintenance_trigger), 0);
                    SET IDENTITY_INSERT maintenance_trigger ON;
                    IF NOT EXISTS (SELECT 1 FROM maintenance_trigger WHERE trigger_id = 'KM_15000' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO maintenance_trigger
                        (maint_trigger_code, description, trigger_id, date_created, created_by_user_code, is_deleted)
                        VALUES (@triggerCode + 1, 'Service every 15000km', 'KM_15000', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT maintenance_trigger OFF;
                END
            ",
            $@"
                IF OBJECT_ID('maintenance_trigger', 'U') IS NOT NULL
                BEGIN
                    DECLARE @triggerCode int = ISNULL((SELECT MAX(maint_trigger_code) FROM maintenance_trigger), 0);
                    IF NOT EXISTS (SELECT 1 FROM maintenance_trigger WHERE trigger_id = 'KM_15000' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO maintenance_trigger
                        (maint_trigger_code, description, trigger_id, date_created, created_by_user_code, is_deleted)
                        VALUES (@triggerCode + 1, 'Service every 15000km', 'KM_15000', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('maintenance_records', 'U') IS NOT NULL
                BEGIN
                    DECLARE @maintId int = ISNULL((SELECT MAX(maintenance_id) FROM maintenance_records), 0);
                    SET IDENTITY_INSERT maintenance_records ON;
                    IF NOT EXISTS (SELECT 1 FROM maintenance_records WHERE vmf_code = {defaultVehicleCode} AND maintenance_type = 'SERVICE' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO maintenance_records
                        (
                            maintenance_id, vmf_code, maintenance_date, maintenance_type, odometer_reading, service_provider, work_order_number,
                            invoice_number, total_cost, labour_cost, parts_cost, description, status, still_current, created_date,
                            date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (
                            @maintId + 1, {defaultVehicleCode}, GETDATE(), 'SERVICE', 15000, 'Fleet Workshop Central', 'WO-15000',
                            'INV-15000', 1800, 900, 900, 'Scheduled service completed', 'COMPLETED', 'Y', GETDATE(),
                            GETDATE(), {defaultUserCode}, 0
                        );
                    SET IDENTITY_INSERT maintenance_records OFF;
                END
            ",
            $@"
                IF OBJECT_ID('maintenance_records', 'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM maintenance_records WHERE vmf_code = {defaultVehicleCode} AND maintenance_type = 'SERVICE' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO maintenance_records
                        (
                            vmf_code, maintenance_date, maintenance_type, odometer_reading, service_provider, work_order_number,
                            invoice_number, total_cost, labour_cost, parts_cost, description, status, still_current, created_date,
                            date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (
                            {defaultVehicleCode}, GETDATE(), 'SERVICE', 15000, 'Fleet Workshop Central', 'WO-15000',
                            'INV-15000', 1800, 900, 900, 'Scheduled service completed', 'COMPLETED', 'Y', GETDATE(),
                            GETDATE(), {defaultUserCode}, 0
                        );
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('Maintenance_Value_History', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mvhId int = ISNULL((SELECT MAX(Maintenance_Value_History_ID) FROM Maintenance_Value_History), 0);
                    SET IDENTITY_INSERT Maintenance_Value_History ON;
                    IF NOT EXISTS (SELECT 1 FROM Maintenance_Value_History WHERE TariffParameterID = {defaultTariffParameterId} AND class_code = {defaultClassCode} AND months_age = 12 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Maintenance_Value_History
                        (Maintenance_Value_History_ID, TariffParameterID, class_code, months_age, amount, date_created, created_by_user_code, is_deleted)
                        VALUES (@mvhId + 1, {defaultTariffParameterId}, {defaultClassCode}, 12, 1200.00, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Maintenance_Value_History OFF;
                END
            ",
            $@"
                IF OBJECT_ID('Maintenance_Value_History', 'U') IS NOT NULL
                BEGIN
                    DECLARE @mvhId int = ISNULL((SELECT MAX(Maintenance_Value_History_ID) FROM Maintenance_Value_History), 0);
                    IF NOT EXISTS (SELECT 1 FROM Maintenance_Value_History WHERE TariffParameterID = {defaultTariffParameterId} AND class_code = {defaultClassCode} AND months_age = 12 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Maintenance_Value_History
                        (Maintenance_Value_History_ID, TariffParameterID, class_code, months_age, amount, date_created, created_by_user_code, is_deleted)
                        VALUES (@mvhId + 1, {defaultTariffParameterId}, {defaultClassCode}, 12, 1200.00, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('fin.OverheadType', 'U') IS NOT NULL
                BEGIN
                    DECLARE @otId int = ISNULL((SELECT MAX(OverheadTypeId) FROM fin.OverheadType), 0);
                    SET IDENTITY_INSERT fin.OverheadType ON;
                    IF NOT EXISTS (SELECT 1 FROM fin.OverheadType WHERE OverheadTypeName = 'Workshop Overhead' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fin.OverheadType (OverheadTypeId, OverheadTypeName, date_created, created_by_user_code, is_deleted)
                        VALUES (@otId + 1, 'Workshop Overhead', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT fin.OverheadType OFF;
                END
            ",
            $@"
                IF OBJECT_ID('fin.OverheadType', 'U') IS NOT NULL
                BEGIN
                    DECLARE @otId int = ISNULL((SELECT MAX(OverheadTypeId) FROM fin.OverheadType), 0);
                    IF NOT EXISTS (SELECT 1 FROM fin.OverheadType WHERE OverheadTypeName = 'Workshop Overhead' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fin.OverheadType (OverheadTypeId, OverheadTypeName, date_created, created_by_user_code, is_deleted)
                        VALUES (@otId + 1, 'Workshop Overhead', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('fin.Overhead', 'U') IS NOT NULL
                BEGIN
                    DECLARE @overheadId int = ISNULL((SELECT MAX(OverheadId) FROM fin.Overhead), 0);
                    DECLARE @overheadTypeId int = ISNULL((SELECT TOP 1 OverheadTypeId FROM fin.OverheadType WHERE ISNULL(is_deleted, 0) = 0 ORDER BY OverheadTypeId), 1);
                    SET IDENTITY_INSERT fin.Overhead ON;
                    IF NOT EXISTS (SELECT 1 FROM fin.Overhead WHERE TariffParameterID = {defaultTariffParameterId} AND OverheadDescription = 'Workshop admin overhead' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fin.Overhead
                        (
                            OverheadId, OverheadDescription, OverheadAmount, OverheadTypeId, OverheadNote, TariffParameterID, CaptureDate,
                            user_access_code, user_access_name, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (
                            @overheadId + 1, 'Workshop admin overhead', 450.00, CAST(@overheadTypeId AS tinyint), 'Seeded overhead row', {defaultTariffParameterId}, GETDATE(),
                            CAST({defaultUserCode} AS smallint), 'seed-user', GETDATE(), {defaultUserCode}, 0
                        );
                    SET IDENTITY_INSERT fin.Overhead OFF;
                END
            ",
            $@"
                IF OBJECT_ID('fin.Overhead', 'U') IS NOT NULL
                BEGIN
                    DECLARE @overheadId int = ISNULL((SELECT MAX(OverheadId) FROM fin.Overhead), 0);
                    DECLARE @overheadTypeId int = ISNULL((SELECT TOP 1 OverheadTypeId FROM fin.OverheadType WHERE ISNULL(is_deleted, 0) = 0 ORDER BY OverheadTypeId), 1);
                    IF NOT EXISTS (SELECT 1 FROM fin.Overhead WHERE TariffParameterID = {defaultTariffParameterId} AND OverheadDescription = 'Workshop admin overhead' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO fin.Overhead
                        (
                            OverheadId, OverheadDescription, OverheadAmount, OverheadTypeId, OverheadNote, TariffParameterID, CaptureDate,
                            user_access_code, user_access_name, date_created, created_by_user_code, is_deleted
                        )
                        VALUES
                        (
                            @overheadId + 1, 'Workshop admin overhead', 450.00, CAST(@overheadTypeId AS tinyint), 'Seeded overhead row', {defaultTariffParameterId}, GETDATE(),
                            CAST({defaultUserCode} AS smallint), 'seed-user', GETDATE(), {defaultUserCode}, 0
                        );
                END
            ");

        Console.WriteLine("  ✓ Batch 14 maintenance-workshop coverage updates applied.");

        var bomCount = await (await HasTableAsync(dbContext, "bill_of_material") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM bill_of_material WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var partCount = await (await HasTableAsync(dbContext, "part") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM part WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taskCount = await (await HasTableAsync(dbContext, "task") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM task WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var maintProfileModelCount = await (await HasTableAsync(dbContext, "maint_profile_model") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM maint_profile_model WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var merchantCount = await (await HasTableAsync(dbContext, "wwmerchant") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM wwmerchant WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var triggerCount = await (await HasTableAsync(dbContext, "maintenance_trigger") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM maintenance_trigger WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var maintenanceRecordCount = await (await HasTableAsync(dbContext, "maintenance_records") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM maintenance_records WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var maintenanceValueHistoryCount = await (await HasTableAsync(dbContext, "Maintenance_Value_History") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Maintenance_Value_History WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var overheadTypeCount = await (await HasTableAsync(dbContext, "OverheadType") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.OverheadType WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var overheadCount = await (await HasTableAsync(dbContext, "Overhead") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM fin.Overhead WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 14 | BOM: {bomCount} | Parts: {partCount} | Tasks: {taskCount} | MaintProfileModel: {maintProfileModelCount} | Merchants: {merchantCount}");
        Console.WriteLine($"  📊 Batch 14 | MaintTriggers: {triggerCount} | MaintRecords: {maintenanceRecordCount} | MaintValueHistory: {maintenanceValueHistoryCount} | OverheadType: {overheadTypeCount} | Overhead: {overheadCount}");
    }

    private static async Task ApplyBatch15FinancialIntegrationCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultContractCode = await HasTableAsync(dbContext, "contract") &&
                                  await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 contract_code FROM contract WHERE ISNULL(is_deleted, 0) = 0 ORDER BY contract_code DESC")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        var defaultFuelCardCode = await HasTableAsync(dbContext, "Fuel_card") &&
                                  await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Fuel_card WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Fuel_card_code FROM Fuel_card WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Fuel_card_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(

            dbContext,
            $@"
                IF OBJECT_ID('absa_transaction_codes', 'U') IS NOT NULL
                BEGIN
                    DECLARE @atcCode int = ISNULL((SELECT MAX(absa_transaction_codes_code) FROM absa_transaction_codes), 0);
                    SET IDENTITY_INSERT absa_transaction_codes ON;
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction_codes WHERE transaction_code = 'PUR' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction_codes (absa_transaction_codes_code, transaction_code, description, cost_category_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@atcCode + 1, 'PUR', 'Fuel Purchase', 2, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction_codes WHERE transaction_code = 'REV' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction_codes (absa_transaction_codes_code, transaction_code, description, cost_category_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@atcCode + 2, 'REV', 'Fuel Reversal', 2, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT absa_transaction_codes OFF;
                END
            ",
            $@"
                IF OBJECT_ID('absa_transaction_codes', 'U') IS NOT NULL
                BEGIN
                    DECLARE @atcCode int = ISNULL((SELECT MAX(absa_transaction_codes_code) FROM absa_transaction_codes), 0);
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction_codes WHERE transaction_code = 'PUR' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction_codes (absa_transaction_codes_code, transaction_code, description, cost_category_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@atcCode + 1, 'PUR', 'Fuel Purchase', 2, GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction_codes WHERE transaction_code = 'REV' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction_codes (absa_transaction_codes_code, transaction_code, description, cost_category_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@atcCode + 2, 'REV', 'Fuel Reversal', 2, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('absa_transaction', 'U') IS NOT NULL
                BEGIN
                    DECLARE @absaTxCode int = ISNULL((SELECT MAX(absa_transaction_code) FROM absa_transaction), 0);
                    DECLARE @journalDetailCode uniqueidentifier = (SELECT TOP 1 journal_detail_code FROM journal_detail WHERE ISNULL(is_deleted, 0) = 0 ORDER BY date_created);
                    SET IDENTITY_INSERT absa_transaction ON;
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction WHERE vmf_code = {defaultVehicleCode} AND contract_code = {defaultContractCode} AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction
                        (absa_transaction_code, vmf_code, contract_code, site_code, fuel_card_code, journal_detail_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@absaTxCode + 1, {defaultVehicleCode}, {defaultContractCode}, {defaultSiteCode}, {defaultFuelCardCode}, @journalDetailCode, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT absa_transaction OFF;
                END
            ",
            $@"
                IF OBJECT_ID('absa_transaction', 'U') IS NOT NULL
                BEGIN
                    DECLARE @absaTxCode int = ISNULL((SELECT MAX(absa_transaction_code) FROM absa_transaction), 0);
                    DECLARE @journalDetailCode uniqueidentifier = (SELECT TOP 1 journal_detail_code FROM journal_detail WHERE ISNULL(is_deleted, 0) = 0 ORDER BY date_created);
                    IF NOT EXISTS (SELECT 1 FROM absa_transaction WHERE vmf_code = {defaultVehicleCode} AND contract_code = {defaultContractCode} AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO absa_transaction
                        (absa_transaction_code, vmf_code, contract_code, site_code, fuel_card_code, journal_detail_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@absaTxCode + 1, {defaultVehicleCode}, {defaultContractCode}, {defaultSiteCode}, {defaultFuelCardCode}, @journalDetailCode, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('wesbank_transaction', 'U') IS NOT NULL
                BEGIN
                    DECLARE @wbTxCode int = ISNULL((SELECT MAX(wesbank_transaction_code) FROM wesbank_transaction), 0);
                    SET IDENTITY_INSERT wesbank_transaction ON;
                    IF NOT EXISTS (SELECT 1 FROM wesbank_transaction WHERE vmf_code = {defaultVehicleCode} AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO wesbank_transaction
                        (wesbank_transaction_code, fuel_card_code, vmf_code, site_code, file_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@wbTxCode + 1, {defaultFuelCardCode}, {defaultVehicleCode}, {defaultSiteCode}, CAST(GETDATE() AS date), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT wesbank_transaction OFF;
                END
            ",
            $@"
                IF OBJECT_ID('wesbank_transaction', 'U') IS NOT NULL
                BEGIN
                    DECLARE @wbTxCode int = ISNULL((SELECT MAX(wesbank_transaction_code) FROM wesbank_transaction), 0);
                    IF NOT EXISTS (SELECT 1 FROM wesbank_transaction WHERE vmf_code = {defaultVehicleCode} AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO wesbank_transaction
                        (wesbank_transaction_code, fuel_card_code, vmf_code, site_code, file_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@wbTxCode + 1, {defaultFuelCardCode}, {defaultVehicleCode}, {defaultSiteCode}, CAST(GETDATE() AS date), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('PastelStatic', 'U') IS NOT NULL
                BEGIN
                    DECLARE @psCode int = ISNULL((SELECT MAX(staticID) FROM PastelStatic), 0);
                    SET IDENTITY_INSERT PastelStatic ON;
                    IF NOT EXISTS (SELECT 1 FROM PastelStatic WHERE staticID = 1)
                        INSERT INTO PastelStatic
                        (staticID, idLinePermanent, iValidateFlag, iAccountCurrencyID, cAccountCurrencySymbol, bTrCodeHasTax, date_created, created_by_user_code, is_deleted)
                        VALUES (@psCode + 1, 100, 1, 1, 'ZAR', 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT PastelStatic OFF;
                END
            ",
            $@"
                IF OBJECT_ID('PastelStatic', 'U') IS NOT NULL
                BEGIN
                    DECLARE @psCode int = ISNULL((SELECT MAX(staticID) FROM PastelStatic), 0);
                    IF NOT EXISTS (SELECT 1 FROM PastelStatic WHERE ISNULL(is_deleted, 0) = 0)
                        INSERT INTO PastelStatic
                        (staticID, idLinePermanent, iValidateFlag, iAccountCurrencyID, cAccountCurrencySymbol, bTrCodeHasTax, date_created, created_by_user_code, is_deleted)
                        VALUES (@psCode + 1, 100, 1, 1, 'ZAR', 1, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('PastelCustomer', 'U') IS NOT NULL
                BEGIN
                    DECLARE @pcCode int = ISNULL((SELECT MAX(pcID) FROM PastelCustomer), 0);
                    SET IDENTITY_INSERT PastelCustomer ON;
                    IF NOT EXISTS (SELECT 1 FROM PastelCustomer WHERE customer = 'CUST-001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO PastelCustomer
                        (pcID, customer, name, customerID, site_code, department_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@pcCode + 1, 'CUST-001', 'Office of the Chief Justice', 1, CAST({defaultSiteCode} AS smallint), CAST({defaultDepartmentCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT PastelCustomer OFF;
                END
            ",
            $@"
                IF OBJECT_ID('PastelCustomer', 'U') IS NOT NULL
                BEGIN
                    DECLARE @pcCode int = ISNULL((SELECT MAX(pcID) FROM PastelCustomer), 0);
                    IF NOT EXISTS (SELECT 1 FROM PastelCustomer WHERE customer = 'CUST-001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO PastelCustomer
                        (pcID, customer, name, customerID, site_code, department_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@pcCode + 1, 'CUST-001', 'Office of the Chief Justice', 1, CAST({defaultSiteCode} AS smallint), CAST({defaultDepartmentCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('PastelGL', 'U') IS NOT NULL
                BEGIN
                    DECLARE @glCode int = ISNULL((SELECT MAX(GLID) FROM PastelGL), 0);
                    SET IDENTITY_INSERT PastelGL ON;
                    IF NOT EXISTS (SELECT 1 FROM PastelGL WHERE account = '4000-REVENUE' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO PastelGL
                        (GLID, account, active, description, accountLink, accountType, date_created, created_by_user_code, is_deleted)
                        VALUES (@glCode + 1, '4000-REVENUE', 1, 'Revenue Account', 1, 'Income', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT PastelGL OFF;
                END
            ",
            $@"
                IF OBJECT_ID('PastelGL', 'U') IS NOT NULL
                BEGIN
                    DECLARE @glCode int = ISNULL((SELECT MAX(GLID) FROM PastelGL), 0);
                    IF NOT EXISTS (SELECT 1 FROM PastelGL WHERE account = '4000-REVENUE' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO PastelGL
                        (GLID, account, active, description, accountLink, accountType, date_created, created_by_user_code, is_deleted)
                        VALUES (@glCode + 1, '4000-REVENUE', 1, 'Revenue Account', 1, 'Income', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('InvalidSegmentNumbersUsed', 'U') IS NOT NULL
            BEGIN
                DECLARE @journalDetailCode uniqueidentifier = (
                    SELECT TOP 1 journal_detail_code
                    FROM journal_detail
                    WHERE ISNULL(is_deleted, 0) = 0
                    ORDER BY date_created
                );
                IF @journalDetailCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM InvalidSegmentNumbersUsed WHERE journal_detail_code = @journalDetailCode AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO InvalidSegmentNumbersUsed
                    (journal_detail_code, segment_number, segment_group_code, segment_type_code, site_code, department_code, date_created, created_by_user_code, is_deleted)
                    VALUES (@journalDetailCode, 9999999, 1, 1, CAST({0} AS smallint), CAST({1} AS smallint), GETDATE(), {2}, 0);
            END
        ", defaultSiteCode, defaultDepartmentCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Journal_WithInvalidBasCodes', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Journal_WithInvalidBasCodes WHERE GGNumber = CONCAT('GGX', {0}) AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Journal_WithInvalidBasCodes
                    (JournalType, GGNumber, BasCode_FinancialYear, Enter_Correct_Responsibility_Number_Only, Enter_Correct_Objective_Number_Only, SiteName, date_created, created_by_user_code, is_deleted)
                    VALUES ('Billing', CONCAT('GGX', {0}), CONCAT(YEAR(GETDATE()), '/', YEAR(GETDATE()) + 1), '4500000', '1000', 'Head Office', GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('daily_import_except', 'U') IS NOT NULL
                BEGIN
                    DECLARE @dieCode int = ISNULL((SELECT MAX(except_ID) FROM daily_import_except), 0);
                    SET IDENTITY_INSERT daily_import_except ON;
                    IF NOT EXISTS (SELECT 1 FROM daily_import_except WHERE PAN = '600000000000001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO daily_import_except
                        (except_ID, PAN, reg_number, voucher, import_date, exception_desc, date_created, created_by_user_code, is_deleted)
                        VALUES (@dieCode + 1, '600000000000001', CONCAT('REG', {defaultVehicleCode}), 'VOUCH-001', GETDATE(), 'Duplicate voucher ignored', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT daily_import_except OFF;
                END
            ",
            $@"
                IF OBJECT_ID('daily_import_except', 'U') IS NOT NULL
                BEGIN
                    DECLARE @dieCode int = ISNULL((SELECT MAX(except_ID) FROM daily_import_except), 0);
                    IF NOT EXISTS (SELECT 1 FROM daily_import_except WHERE PAN = '600000000000001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO daily_import_except
                        (except_ID, PAN, reg_number, voucher, import_date, exception_desc, date_created, created_by_user_code, is_deleted)
                        VALUES (@dieCode + 1, '600000000000001', CONCAT('REG', {defaultVehicleCode}), 'VOUCH-001', GETDATE(), 'Duplicate voucher ignored', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('batch_export', 'U') IS NOT NULL
                BEGIN
                    DECLARE @batchExportCode int = ISNULL((SELECT MAX(batch_export_code) FROM batch_export), 0);
                    DECLARE @batchCode int = ISNULL((SELECT TOP 1 batch_code FROM batch WHERE ISNULL(is_deleted, 0) = 0 ORDER BY batch_code DESC), 1);
                    SET IDENTITY_INSERT batch_export ON;
                    IF NOT EXISTS (SELECT 1 FROM batch_export WHERE batch_code = @batchCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO batch_export
                        (batch_export_code, batch_code, batch_export_date, batch_export_turnover, department_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@batchExportCode + 1, @batchCode, GETDATE(), 10000.00, CAST({defaultDepartmentCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT batch_export OFF;
                END
            ",
            $@"
                IF OBJECT_ID('batch_export', 'U') IS NOT NULL
                BEGIN
                    DECLARE @batchExportCode int = ISNULL((SELECT MAX(batch_export_code) FROM batch_export), 0);
                    DECLARE @batchCode int = ISNULL((SELECT TOP 1 batch_code FROM batch WHERE ISNULL(is_deleted, 0) = 0 ORDER BY batch_code DESC), 1);
                    IF NOT EXISTS (SELECT 1 FROM batch_export WHERE batch_code = @batchCode AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO batch_export
                        (batch_export_code, batch_code, batch_export_date, batch_export_turnover, department_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@batchExportCode + 1, @batchCode, GETDATE(), 10000.00, CAST({defaultDepartmentCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        Console.WriteLine("  ✓ Batch 15 financial-integration coverage updates applied.");

        var absaCodeCount = await (await HasTableAsync(dbContext, "absa_transaction_codes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM absa_transaction_codes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var absaTxCount = await (await HasTableAsync(dbContext, "absa_transaction") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM absa_transaction WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var wesbankTxCount = await (await HasTableAsync(dbContext, "wesbank_transaction") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM wesbank_transaction WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var pastelStaticCount = await (await HasTableAsync(dbContext, "PastelStatic") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM PastelStatic WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var pastelCustomerCount = await (await HasTableAsync(dbContext, "PastelCustomer") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM PastelCustomer WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var pastelGlCount = await (await HasTableAsync(dbContext, "PastelGL") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM PastelGL WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var invalidSegmentCount = await (await HasTableAsync(dbContext, "InvalidSegmentNumbersUsed") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM InvalidSegmentNumbersUsed WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var invalidBasCount = await (await HasTableAsync(dbContext, "Journal_WithInvalidBasCodes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Journal_WithInvalidBasCodes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var dailyImportExceptCount = await (await HasTableAsync(dbContext, "daily_import_except") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM daily_import_except WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var batchExportCount = await (await HasTableAsync(dbContext, "batch_export") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM batch_export WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 15 | AbsaCodes: {absaCodeCount} | AbsaTransactions: {absaTxCount} | WesbankTransactions: {wesbankTxCount} | PastelStatic: {pastelStaticCount} | PastelCustomer: {pastelCustomerCount}");
        Console.WriteLine($"  📊 Batch 15 | PastelGL: {pastelGlCount} | InvalidSegments: {invalidSegmentCount} | InvalidBasCodes: {invalidBasCount} | DailyImportExcept: {dailyImportExceptCount} | BatchExport: {batchExportCount}");
    }

    private static async Task ApplyBatch16LegacyIdentifiersCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultTempVmfCode = await HasTableAsync(dbContext, "pre_vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM pre_vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 temp_vmf_code FROM pre_vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY temp_vmf_code")
            : 1;

        var defaultExtraCode = await HasTableAsync(dbContext, "extra_codes") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM extra_codes WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 extra_code FROM extra_codes WHERE ISNULL(is_deleted, 0) = 0 ORDER BY extra_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('GG_Block', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockId int = ISNULL((SELECT MAX(Block_ID) FROM GG_Block), 0);
                    SET IDENTITY_INSERT GG_Block ON;
                    IF NOT EXISTS (SELECT 1 FROM GG_Block WHERE Vch_Start_Reg = 'GGX500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO GG_Block
                        (Block_ID, Creation_Date, Created_By_User_Code, Vch_Start_Reg, Vch_End_Reg, Modified_User_Code, audit_date_created, audit_created_by_user_code, is_deleted)
                        VALUES (@blockId + 1, GETDATE(), CAST({defaultUserCode} AS smallint), 'GGX500001', 'GGX500100', CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT GG_Block OFF;
                END
            ",
            $@"
                IF OBJECT_ID('GG_Block', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockId int = ISNULL((SELECT MAX(Block_ID) FROM GG_Block), 0);
                    IF NOT EXISTS (SELECT 1 FROM GG_Block WHERE Vch_Start_Reg = 'GGX500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO GG_Block
                        (Block_ID, Creation_Date, Created_By_User_Code, Vch_Start_Reg, Vch_End_Reg, Modified_User_Code, audit_date_created, audit_created_by_user_code, is_deleted)
                        VALUES (@blockId + 1, GETDATE(), CAST({defaultUserCode} AS smallint), 'GGX500001', 'GGX500100', CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('GG_Blocks', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockId2 int = ISNULL((SELECT MAX(Block_ID) FROM GG_Blocks), 0);
                    SET IDENTITY_INSERT GG_Blocks ON;
                    IF NOT EXISTS (SELECT 1 FROM GG_Blocks WHERE Vch_Start_Reg = 'GGX600001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO GG_Blocks
                        (Block_ID, Creation_Date, Created_By_User_Code, Vch_Start_Reg, Vch_End_Reg, Modified_User_Code, date_updated, is_deleted)
                        VALUES (@blockId2 + 1, GETDATE(), {defaultUserCode}, 'GGX600001', 'GGX600100', {defaultUserCode}, GETDATE(), 0);
                    SET IDENTITY_INSERT GG_Blocks OFF;
                END
            ",
            $@"
                IF OBJECT_ID('GG_Blocks', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockId2 int = ISNULL((SELECT MAX(Block_ID) FROM GG_Blocks), 0);
                    IF NOT EXISTS (SELECT 1 FROM GG_Blocks WHERE Vch_Start_Reg = 'GGX600001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO GG_Blocks
                        (Block_ID, Creation_Date, Created_By_User_Code, Vch_Start_Reg, Vch_End_Reg, Modified_User_Code, date_updated, is_deleted)
                        VALUES (@blockId2 + 1, GETDATE(), {defaultUserCode}, 'GGX600001', 'GGX600100', {defaultUserCode}, GETDATE(), 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('block_gg_numbers', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockGenId int = ISNULL((SELECT MAX(Block_GEN_ID) FROM block_gg_numbers), 0);
                    DECLARE @blockId int = ISNULL((SELECT TOP 1 Block_ID FROM GG_Block WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Block_ID), 1);
                    SET IDENTITY_INSERT block_gg_numbers ON;
                    IF NOT EXISTS (SELECT 1 FROM block_gg_numbers WHERE GG_Number = 'GGX500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO block_gg_numbers
                        (Block_GEN_ID, Block_ID, Creation_Date, Created_By_User_Code, GG_Number, audit_date_created, audit_created_by_user_code, is_deleted)
                        VALUES (@blockGenId + 1, CAST(@blockId AS smallint), GETDATE(), CAST({defaultUserCode} AS smallint), 'GGX500001', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT block_gg_numbers OFF;
                END
            ",
            $@"
                IF OBJECT_ID('block_gg_numbers', 'U') IS NOT NULL
                BEGIN
                    DECLARE @blockGenId int = ISNULL((SELECT MAX(Block_GEN_ID) FROM block_gg_numbers), 0);
                    DECLARE @blockId int = ISNULL((SELECT TOP 1 Block_ID FROM GG_Block WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Block_ID), 1);
                    IF NOT EXISTS (SELECT 1 FROM block_gg_numbers WHERE GG_Number = 'GGX500001' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO block_gg_numbers
                        (Block_GEN_ID, Block_ID, Creation_Date, Created_By_User_Code, GG_Number, audit_date_created, audit_created_by_user_code, is_deleted)
                        VALUES (@blockGenId + 1, CAST(@blockId AS smallint), GETDATE(), CAST({defaultUserCode} AS smallint), 'GGX500001', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('gg_numbers', 'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM gg_numbers WHERE gg_number = 'GGX500001')
                    INSERT INTO gg_numbers (gg_number, status, date_created, created_by_user_code, is_deleted)
                    VALUES ('GGX500001', 1, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('Alphabets', 'U') IS NOT NULL
                BEGIN
                    DECLARE @alphaId int = ISNULL((SELECT MAX(alphabet_id) FROM Alphabets), 0);
                    SET IDENTITY_INSERT Alphabets ON;
                    IF NOT EXISTS (SELECT 1 FROM Alphabets WHERE alphabet_name = 'A' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Alphabets (alphabet_id, alphabet_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@alphaId + 1, 'A', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM Alphabets WHERE alphabet_name = 'B' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Alphabets (alphabet_id, alphabet_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@alphaId + 2, 'B', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Alphabets OFF;
                END
            ",
            $@"
                IF OBJECT_ID('Alphabets', 'U') IS NOT NULL
                BEGIN
                    DECLARE @alphaId int = ISNULL((SELECT MAX(alphabet_id) FROM Alphabets), 0);
                    IF NOT EXISTS (SELECT 1 FROM Alphabets WHERE alphabet_name = 'A' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Alphabets (alphabet_id, alphabet_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@alphaId + 1, 'A', GETDATE(), {defaultUserCode}, 0);
                    IF NOT EXISTS (SELECT 1 FROM Alphabets WHERE alphabet_name = 'B' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Alphabets (alphabet_id, alphabet_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@alphaId + 2, 'B', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('tally', 'U') IS NOT NULL
                BEGIN
                    DECLARE @tallyCode int = ISNULL((SELECT MAX(tally_code) FROM tally), 0);
                    SET IDENTITY_INSERT tally ON;
                    IF NOT EXISTS (SELECT 1 FROM tally WHERE tally_value = 100 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO tally (tally_code, tally_value, date_created, created_by_user_code, is_deleted)
                        VALUES (@tallyCode + 1, 100, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT tally OFF;
                END
            ",
            $@"
                IF OBJECT_ID('tally', 'U') IS NOT NULL
                BEGIN
                    DECLARE @tallyCode int = ISNULL((SELECT MAX(tally_code) FROM tally), 0);
                    IF NOT EXISTS (SELECT 1 FROM tally WHERE tally_value = 100 AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO tally (tally_code, tally_value, date_created, created_by_user_code, is_deleted)
                        VALUES (@tallyCode + 1, 100, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('FIS_Survey', 'U') IS NOT NULL
                BEGIN
                    DECLARE @surveyCode int = ISNULL((SELECT MAX(satisfaction_survey_code) FROM FIS_Survey), 0);
                    SET IDENTITY_INSERT FIS_Survey ON;
                    IF NOT EXISTS (SELECT 1 FROM FIS_Survey WHERE used_service = 'Vehicle Hire' AND department_fleet = 'Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO FIS_Survey
                        (satisfaction_survey_code, used_service, department_fleet, customer_service, professionalism, quality_of_vehicles, date_created, created_by_user_code, is_deleted)
                        VALUES (@surveyCode + 1, 'Vehicle Hire', 'Operations', 'Good', 'Excellent', 'Good', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT FIS_Survey OFF;
                END
            ",
            $@"
                IF OBJECT_ID('FIS_Survey', 'U') IS NOT NULL
                BEGIN
                    DECLARE @surveyCode int = ISNULL((SELECT MAX(satisfaction_survey_code) FROM FIS_Survey), 0);
                    IF NOT EXISTS (SELECT 1 FROM FIS_Survey WHERE used_service = 'Vehicle Hire' AND department_fleet = 'Operations' AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO FIS_Survey
                        (satisfaction_survey_code, used_service, department_fleet, customer_service, professionalism, quality_of_vehicles, date_created, created_by_user_code, is_deleted)
                        VALUES (@surveyCode + 1, 'Vehicle Hire', 'Operations', 'Good', 'Excellent', 'Good', GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('Registrations', 'U') IS NOT NULL
                BEGIN
                    DECLARE @regId int = ISNULL((SELECT MAX(RegistrationID) FROM Registrations), 0);
                    SET IDENTITY_INSERT Registrations ON;
                    IF NOT EXISTS (SELECT 1 FROM Registrations WHERE vmf_code = {defaultVehicleCode} AND RegistrationNumber = CONCAT('REG-', {defaultVehicleCode}) AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Registrations
                        (RegistrationID, RegistrationNumber, RegistrationDate, vmf_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@regId + 1, CONCAT('REG-', {defaultVehicleCode}), CAST(GETDATE() AS date), {defaultVehicleCode}, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Registrations OFF;
                END
            ",
            $@"
                IF OBJECT_ID('Registrations', 'U') IS NOT NULL
                BEGIN
                    DECLARE @regId int = ISNULL((SELECT MAX(RegistrationID) FROM Registrations), 0);
                    IF NOT EXISTS (SELECT 1 FROM Registrations WHERE vmf_code = {defaultVehicleCode} AND RegistrationNumber = CONCAT('REG-', {defaultVehicleCode}) AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO Registrations
                        (RegistrationID, RegistrationNumber, RegistrationDate, vmf_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@regId + 1, CONCAT('REG-', {defaultVehicleCode}), CAST(GETDATE() AS date), {defaultVehicleCode}, GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"
                IF OBJECT_ID('EnjinNumbers', 'U') IS NOT NULL
                BEGIN
                    DECLARE @enjinId int = ISNULL((SELECT MAX(EnjinNumberID) FROM EnjinNumbers), 0);
                    SET IDENTITY_INSERT EnjinNumbers ON;
                    IF NOT EXISTS (SELECT 1 FROM EnjinNumbers WHERE vmf_code = {defaultVehicleCode} AND EnjinNumber = CONCAT('ENG-', {defaultVehicleCode}) AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO EnjinNumbers
                        (EnjinNumberID, vmf_code, EnjinNumber, date_created, created_by_user_code, is_deleted)
                        VALUES (@enjinId + 1, {defaultVehicleCode}, CONCAT('ENG-', {defaultVehicleCode}), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT EnjinNumbers OFF;
                END
            ",
            $@"
                IF OBJECT_ID('EnjinNumbers', 'U') IS NOT NULL
                BEGIN
                    DECLARE @enjinId int = ISNULL((SELECT MAX(EnjinNumberID) FROM EnjinNumbers), 0);
                    IF NOT EXISTS (SELECT 1 FROM EnjinNumbers WHERE vmf_code = {defaultVehicleCode} AND EnjinNumber = CONCAT('ENG-', {defaultVehicleCode}) AND ISNULL(is_deleted, 0) = 0)
                        INSERT INTO EnjinNumbers
                        (EnjinNumberID, vmf_code, EnjinNumber, date_created, created_by_user_code, is_deleted)
                        VALUES (@enjinId + 1, {defaultVehicleCode}, CONCAT('ENG-', {defaultVehicleCode}), GETDATE(), {defaultUserCode}, 0);
                END
            ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Temp_Vehicle_extras', 'U') IS NOT NULL
            BEGIN
                DECLARE @defaultVehicleCode int = {0};
                DECLARE @defaultTempVmfCode int = {1};
                DECLARE @defaultExtraCode int = {2};
                DECLARE @defaultUserCode int = {3};
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('Temp_Vehicle_extras'), 'extras_code', 'IsIdentity');
                DECLARE @sql nvarchar(max);

                IF COL_LENGTH('Temp_Vehicle_extras', 'vmf_code') IS NOT NULL
                BEGIN
                    IF @isIdentity = 1
                        SET @sql = N'
                            IF NOT EXISTS (SELECT 1 FROM Temp_Vehicle_extras WHERE vmf_code = @vehicleCode AND extra_code = CAST(@extraCode AS smallint) AND ISNULL(is_deleted, 0) = 0)
                                INSERT INTO Temp_Vehicle_extras
                                (vmf_code, extra_code, quantity, amount, date_created, created_by_user_code, is_deleted)
                                VALUES (@vehicleCode, CAST(@extraCode AS smallint), 1, 350.00, GETDATE(), @userCode, 0);';
                    ELSE
                        SET @sql = N'
                            IF NOT EXISTS (SELECT 1 FROM Temp_Vehicle_extras WHERE vmf_code = @vehicleCode AND extra_code = CAST(@extraCode AS smallint) AND ISNULL(is_deleted, 0) = 0)
                                INSERT INTO Temp_Vehicle_extras
                                (extras_code, vmf_code, extra_code, quantity, amount, date_created, created_by_user_code, is_deleted)
                                VALUES ((SELECT ISNULL(MAX(extras_code), 0) + 1 FROM Temp_Vehicle_extras), @vehicleCode, CAST(@extraCode AS smallint), 1, 350.00, GETDATE(), @userCode, 0);';

                    EXEC sp_executesql
                        @sql,
                        N'@vehicleCode int, @tempVmfCode int, @extraCode int, @userCode int',
                        @vehicleCode = @defaultVehicleCode,
                        @tempVmfCode = @defaultTempVmfCode,
                        @extraCode = @defaultExtraCode,
                        @userCode = @defaultUserCode;
                END
                ELSE IF COL_LENGTH('Temp_Vehicle_extras', 'temp_vmf_code') IS NOT NULL
                BEGIN
                    IF @isIdentity = 1
                        SET @sql = N'
                            IF NOT EXISTS (SELECT 1 FROM Temp_Vehicle_extras WHERE temp_vmf_code = @tempVmfCode AND extra_code = CAST(@extraCode AS smallint) AND ISNULL(is_deleted, 0) = 0)
                                INSERT INTO Temp_Vehicle_extras
                                (temp_vmf_code, extra_code, quantity, amount, date_created, created_by_user_code, is_deleted)
                                VALUES (@tempVmfCode, CAST(@extraCode AS smallint), 1, 350.00, GETDATE(), @userCode, 0);';
                    ELSE
                        SET @sql = N'
                            IF NOT EXISTS (SELECT 1 FROM Temp_Vehicle_extras WHERE temp_vmf_code = @tempVmfCode AND extra_code = CAST(@extraCode AS smallint) AND ISNULL(is_deleted, 0) = 0)
                                INSERT INTO Temp_Vehicle_extras
                                (extras_code, temp_vmf_code, extra_code, quantity, amount, date_created, created_by_user_code, is_deleted)
                                VALUES ((SELECT ISNULL(MAX(extras_code), 0) + 1 FROM Temp_Vehicle_extras), @tempVmfCode, CAST(@extraCode AS smallint), 1, 350.00, GETDATE(), @userCode, 0);';

                    EXEC sp_executesql
                        @sql,
                        N'@vehicleCode int, @tempVmfCode int, @extraCode int, @userCode int',
                        @vehicleCode = @defaultVehicleCode,
                        @tempVmfCode = @defaultTempVmfCode,
                        @extraCode = @defaultExtraCode,
                        @userCode = @defaultUserCode;
                END
            END
        ", defaultVehicleCode, defaultTempVmfCode, defaultExtraCode, defaultUserCode);

        Console.WriteLine("  ✓ Batch 16 legacy-identifiers coverage updates applied.");

        var ggBlockLegacyCount = await (await HasTableAsync(dbContext, "GG_Block") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM GG_Block WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var ggBlocksCount = await (await HasTableAsync(dbContext, "GG_Blocks") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM GG_Blocks WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var blockGgNumbersCount = await (await HasTableAsync(dbContext, "block_gg_numbers") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM block_gg_numbers WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var ggNumbersCount = await (await HasTableAsync(dbContext, "gg_numbers") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM gg_numbers WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var alphabetsCount = await (await HasTableAsync(dbContext, "Alphabets") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Alphabets WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tallyCount = await (await HasTableAsync(dbContext, "tally") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM tally WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var surveyCount = await (await HasTableAsync(dbContext, "FIS_Survey") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM FIS_Survey WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var registrationsCount = await (await HasTableAsync(dbContext, "Registrations") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Registrations WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var enjinNumbersCount = await (await HasTableAsync(dbContext, "EnjinNumbers") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM EnjinNumbers WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tempVehicleExtrasCount = await (await HasTableAsync(dbContext, "Temp_Vehicle_extras") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Temp_Vehicle_extras WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 16 | GG_Block: {ggBlockLegacyCount} | GG_Blocks: {ggBlocksCount} | block_gg_numbers: {blockGgNumbersCount} | gg_numbers: {ggNumbersCount} | Alphabets: {alphabetsCount}");
        Console.WriteLine($"  📊 Batch 16 | Tally: {tallyCount} | FIS_Survey: {surveyCount} | Registrations: {registrationsCount} | EnjinNumbers: {enjinNumbersCount} | TempVehicleExtras: {tempVehicleExtrasCount}");
    }

    private static async Task ApplyBatch17LocationTempCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultContractCode = await HasTableAsync(dbContext, "contract") &&
                                  await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 contract_code FROM contract WHERE ISNULL(is_deleted, 0) = 0 ORDER BY contract_code DESC")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Ambulance','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Ambulance_code) FROM Ambulance),0);
                    SET IDENTITY_INSERT Ambulance ON;
                    IF NOT EXISTS (SELECT 1 FROM Ambulance WHERE Amb_name='ER24 Central' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Ambulance (Ambulance_code, Amb_area, Amb_name, Amb_tel, Amb_fax, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'Johannesburg','ER24 Central','0110000300','0110000301',GETDATE(),{defaultUserCode},0);
                    SET IDENTITY_INSERT Ambulance OFF;
                 END",
            $@"IF OBJECT_ID('Ambulance','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Ambulance_code) FROM Ambulance),0);
                    IF NOT EXISTS (SELECT 1 FROM Ambulance WHERE Amb_name='ER24 Central' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Ambulance (Ambulance_code, Amb_area, Amb_name, Amb_tel, Amb_fax, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'Johannesburg','ER24 Central','0110000300','0110000301',GETDATE(),{defaultUserCode},0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Tow_Truck','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Tow_code) FROM Tow_Truck),0);
                    SET IDENTITY_INSERT Tow_Truck ON;
                    IF NOT EXISTS (SELECT 1 FROM Tow_Truck WHERE Tow_name='City Towing' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Tow_Truck (Tow_code, Tow_area, Tow_name, Tow_tel, Tow_fax, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'Johannesburg','City Towing','0110000310','0110000311',GETDATE(),{defaultUserCode},0);
                    SET IDENTITY_INSERT Tow_Truck OFF;
                 END",
            $@"IF OBJECT_ID('Tow_Truck','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Tow_code) FROM Tow_Truck),0);
                    IF NOT EXISTS (SELECT 1 FROM Tow_Truck WHERE Tow_name='City Towing' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Tow_Truck (Tow_code, Tow_area, Tow_name, Tow_tel, Tow_fax, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'Johannesburg','City Towing','0110000310','0110000311',GETDATE(),{defaultUserCode},0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('univ','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(univ_code) FROM univ),0);
                    SET IDENTITY_INSERT univ ON;
                    IF NOT EXISTS (SELECT 1 FROM univ WHERE univ_name='University of Johannesburg' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO univ (univ_code, univ_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'University of Johannesburg',GETDATE(),{defaultUserCode},0);
                    SET IDENTITY_INSERT univ OFF;
                 END",
            $@"IF OBJECT_ID('univ','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(univ_code) FROM univ),0);
                    IF NOT EXISTS (SELECT 1 FROM univ WHERE univ_name='University of Johannesburg' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO univ (univ_code, univ_name, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'University of Johannesburg',GETDATE(),{defaultUserCode},0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Booking_address','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(location_code) FROM Booking_address),0);
                    SET IDENTITY_INSERT Booking_address ON;
                    IF NOT EXISTS (SELECT 1 FROM Booking_address WHERE net_address='bookings@fis.local' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Booking_address (location_code, net_address, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'bookings@fis.local',GETDATE(),{defaultUserCode},0);
                    SET IDENTITY_INSERT Booking_address OFF;
                 END",
            $@"IF OBJECT_ID('Booking_address','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(location_code) FROM Booking_address),0);
                    IF NOT EXISTS (SELECT 1 FROM Booking_address WHERE net_address='bookings@fis.local' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Booking_address (location_code, net_address, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1,'bookings@fis.local',GETDATE(),{defaultUserCode},0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('tempTA','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(ta) FROM tempTA),0);
                    SET IDENTITY_INSERT tempTA ON;
                    IF NOT EXISTS (SELECT 1 FROM tempTA WHERE ta={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO tempTA (ta, endodo, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultContractCode}, 22000, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT tempTA OFF;
                 END",
            $@"IF OBJECT_ID('tempTA','U') IS NOT NULL BEGIN
                    IF NOT EXISTS (SELECT 1 FROM tempTA WHERE ta={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO tempTA (ta, endodo, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultContractCode}, 22000, GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('temptrips','U') IS NOT NULL BEGIN
                    SET IDENTITY_INSERT temptrips ON;
                    IF NOT EXISTS (SELECT 1 FROM temptrips WHERE trip_authority_code={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO temptrips (trip_authority_code, contract_code, approver_name, approver_rank, approver_tel, end_odo_meter, expiry_date, trip_reason, trip_request_number, issue_date, trip_type_code, trip_incident_type_code, user_access_code, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultContractCode}, {defaultContractCode}, 'Seed Approver', 'Manager', '0110000320', 23000, DATEADD(day,7,GETDATE()), 'Operational duty', CONCAT('TMP-', {defaultContractCode}), GETDATE(), 1, 1, CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT temptrips OFF;
                 END",
            $@"IF OBJECT_ID('temptrips','U') IS NOT NULL BEGIN
                    IF NOT EXISTS (SELECT 1 FROM temptrips WHERE trip_authority_code={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO temptrips (trip_authority_code, contract_code, approver_name, approver_rank, approver_tel, end_odo_meter, expiry_date, trip_reason, trip_request_number, issue_date, trip_type_code, trip_incident_type_code, user_access_code, date_created, created_by_user_code, is_deleted)
                        VALUES ({defaultContractCode}, {defaultContractCode}, 'Seed Approver', 'Manager', '0110000320', 23000, DATEADD(day,7,GETDATE()), 'Operational duty', CONCAT('TMP-', {defaultContractCode}), GETDATE(), 1, 1, CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('route_details','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(route_code) FROM route_details),0);
                    SET IDENTITY_INSERT route_details ON;
                    IF NOT EXISTS (SELECT 1 FROM route_details WHERE trip_authority_code={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO route_details (route_code, trip_authority_code, start_date, end_date, start_odo_meter, end_odo_meter, bas_responsibility_code, bas_object_code, start_route_location_name, end_route_location_name, estimated_distance, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1, {defaultContractCode}, GETDATE(), DATEADD(day,1,GETDATE()), 22000, 22350, '4500000', '1000', 'Head Office', 'Pretoria', 55, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT route_details OFF;
                 END",
            $@"IF OBJECT_ID('route_details','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(route_code) FROM route_details),0);
                    IF NOT EXISTS (SELECT 1 FROM route_details WHERE trip_authority_code={defaultContractCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO route_details (route_code, trip_authority_code, start_date, end_date, start_odo_meter, end_odo_meter, bas_responsibility_code, bas_object_code, start_route_location_name, end_route_location_name, estimated_distance, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1, {defaultContractCode}, GETDATE(), DATEADD(day,1,GETDATE()), 22000, 22350, '4500000', '1000', 'Head Office', 'Pretoria', 55, GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Request_Change','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(request_code) FROM Request_Change),0);
                    SET IDENTITY_INSERT Request_Change ON;
                    IF NOT EXISTS (SELECT 1 FROM Request_Change WHERE request_name='Seeded Change Request' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Request_Change (request_code, request_date, request_name, captured_by_userid, change_description, sub_system_affected, approve_or_not, request_comment, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1, GETDATE(), 'Seeded Change Request', {defaultUserCode}, 'Add legacy temp coverage', 'Trips', 'Y', 'Auto-seeded', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Request_Change OFF;
                 END",
            $@"IF OBJECT_ID('Request_Change','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(request_code) FROM Request_Change),0);
                    IF NOT EXISTS (SELECT 1 FROM Request_Change WHERE request_name='Seeded Change Request' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Request_Change (request_code, request_date, request_name, captured_by_userid, change_description, sub_system_affected, approve_or_not, request_comment, date_created, created_by_user_code, is_deleted)
                        VALUES (@code+1, GETDATE(), 'Seeded Change Request', {defaultUserCode}, 'Add legacy temp coverage', 'Trips', 'Y', 'Auto-seeded', GETDATE(), {defaultUserCode}, 0);
                 END");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('rpt_temp','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM rpt_temp WHERE reg_num = CONCAT('REG-', {0}) AND ISNULL(is_deleted,0)=0)
                    INSERT INTO rpt_temp (date, reg_num, site_code, department, merchant_name, merchant_area, date_created, created_by_user_code, is_deleted)
                    VALUES (CONVERT(varchar(10), GETDATE(), 120), CONCAT('REG-', {0}), {1}, 1, 'Fleet Workshop Central', 'Johannesburg', GETDATE(), {2}, 0);
            END
        ", defaultVehicleCode, defaultSiteCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('temp','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM temp WHERE contract_code = {0} AND vmf_code = {1} AND ISNULL(is_deleted,0)=0)
                    INSERT INTO temp (contract_code, start_date, end_date, tariff, vmf_code, site_code, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, GETDATE(), DATEADD(day, 30, GETDATE()), 3500.00, {1}, CAST({2} AS smallint), GETDATE(), {3}, 0);
            END
        ", defaultContractCode, defaultVehicleCode, defaultSiteCode, defaultUserCode);

        Console.WriteLine("  ✓ Batch 17 location-temp coverage updates applied.");

        var ambulanceCount = await (await HasTableAsync(dbContext, "Ambulance") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Ambulance WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var towTruckCount = await (await HasTableAsync(dbContext, "Tow_Truck") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Tow_Truck WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var univCount = await (await HasTableAsync(dbContext, "univ") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM univ WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var bookingAddressCount = await (await HasTableAsync(dbContext, "Booking_address") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Booking_address WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tempTaCount = await (await HasTableAsync(dbContext, "tempTA") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM tempTA WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tempTripsCount = await (await HasTableAsync(dbContext, "temptrips") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM temptrips WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var routeDetailsCount = await (await HasTableAsync(dbContext, "route_details") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM route_details WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var requestChangeCount = await (await HasTableAsync(dbContext, "Request_Change") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Request_Change WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var rptTempCount = await (await HasTableAsync(dbContext, "rpt_temp") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM rpt_temp WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tempCount = await (await HasTableAsync(dbContext, "temp") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM temp WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 17 | Ambulance: {ambulanceCount} | TowTruck: {towTruckCount} | Univ: {univCount} | BookingAddress: {bookingAddressCount} | TempTA: {tempTaCount}");
        Console.WriteLine($"  📊 Batch 17 | TempTrips: {tempTripsCount} | RouteDetails: {routeDetailsCount} | RequestChange: {requestChangeCount} | RptTemp: {rptTempCount} | Temp: {tempCount}");
    }

    private static async Task ApplyBatch18TaxiThirdPartyCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        var defaultClassCode = await HasTableAsync(dbContext, "class") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM class WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 class_code FROM class WHERE ISNULL(is_deleted, 0) = 0 ORDER BY class_code")
            : 1;

        var defaultModelCode = await HasTableAsync(dbContext, "model") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM model WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 model_code FROM model WHERE ISNULL(is_deleted, 0) = 0 ORDER BY model_code")
            : 1;

        var defaultSupplierId = await HasTableAsync(dbContext, "Suppliers") &&
                                await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Suppliers WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 supplier_id FROM Suppliers WHERE ISNULL(is_deleted, 0) = 0 ORDER BY supplier_id")
            : 1;

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Contractors','U') IS NOT NULL BEGIN
                    DECLARE @contractorId int = ISNULL((SELECT MAX(contractor_id) FROM Contractors), 0);
                    SET IDENTITY_INSERT Contractors ON;
                    IF NOT EXISTS (SELECT 1 FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Contractors
                        (contractor_id, contractor_name, physical_address, postal_address, tel_number, fax_number, date_created, created_by_user_code, is_deleted)
                        VALUES (@contractorId + 1, 'Seed Taxi Contractor', '1 Fleet Lane, Johannesburg', 'PO Box 501, Johannesburg', '0110000400', '0110000401', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Contractors OFF;
                 END",
            $@"IF OBJECT_ID('Contractors','U') IS NOT NULL BEGIN
                    DECLARE @contractorId int = ISNULL((SELECT MAX(contractor_id) FROM Contractors), 0);
                    IF NOT EXISTS (SELECT 1 FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Contractors
                        (contractor_id, contractor_name, physical_address, postal_address, tel_number, fax_number, date_created, created_by_user_code, is_deleted)
                        VALUES (@contractorId + 1, 'Seed Taxi Contractor', '1 Fleet Lane, Johannesburg', 'PO Box 501, Johannesburg', '0110000400', '0110000401', GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Contractor_taxi_class','U') IS NOT NULL BEGIN
                    DECLARE @contractorId smallint = CAST(ISNULL((SELECT TOP 1 contractor_id FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0 ORDER BY contractor_id), 1) AS smallint);
                    DECLARE @taxiClassId int = ISNULL((SELECT MAX(class_id) FROM Contractor_taxi_class), 0);
                    SET IDENTITY_INSERT Contractor_taxi_class ON;
                    IF NOT EXISTS (SELECT 1 FROM Contractor_taxi_class WHERE contractor_id=@contractorId AND description='Standard Sedan' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Contractor_taxi_class
                        (class_id, contractor_id, description, km_tariff, driver_per_hour, daily_tariff, date_created, created_by_user_code, is_deleted)
                        VALUES (@taxiClassId + 1, @contractorId, 'Standard Sedan', 8.75, 95.00, 650.00, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Contractor_taxi_class OFF;
                 END",
            $@"IF OBJECT_ID('Contractor_taxi_class','U') IS NOT NULL BEGIN
                    DECLARE @contractorId smallint = CAST(ISNULL((SELECT TOP 1 contractor_id FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0 ORDER BY contractor_id), 1) AS smallint);
                    DECLARE @taxiClassId int = ISNULL((SELECT MAX(class_id) FROM Contractor_taxi_class), 0);
                    IF NOT EXISTS (SELECT 1 FROM Contractor_taxi_class WHERE contractor_id=@contractorId AND description='Standard Sedan' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Contractor_taxi_class
                        (class_id, contractor_id, description, km_tariff, driver_per_hour, daily_tariff, date_created, created_by_user_code, is_deleted)
                        VALUES (@taxiClassId + 1, @contractorId, 'Standard Sedan', 8.75, 95.00, 650.00, GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Taxis','U') IS NOT NULL BEGIN
                    DECLARE @requestId int = ISNULL((SELECT MAX(request_id) FROM Taxis), 0);
                    DECLARE @contractorId smallint = CAST(ISNULL((SELECT TOP 1 contractor_id FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0 ORDER BY contractor_id), 1) AS smallint);
                    SET IDENTITY_INSERT Taxis ON;
                    IF NOT EXISTS (SELECT 1 FROM Taxis WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxis
                        (request_id, rek_num, contractor_id, vmf_code, department_code, site_code, date_required, time_required, vehicle_type_code, official, rank, address_1, address_2, address_3, flight, date_created, created_by_user_code, is_deleted)
                        VALUES (@requestId + 1, 'TAXI-SEED-001', @contractorId, CAST({defaultVehicleCode} AS varchar(20)), CAST({defaultDepartmentCode} AS smallint), CAST({defaultSiteCode} AS smallint), DATEADD(day,1,GETDATE()), DATEADD(hour,2,GETDATE()), 1, 'Seed Official', 'Manager', '1 Main Rd', 'Braamfontein', 'Johannesburg', NULL, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Taxis OFF;
                 END",
            $@"IF OBJECT_ID('Taxis','U') IS NOT NULL BEGIN
                    DECLARE @contractorId smallint = CAST(ISNULL((SELECT TOP 1 contractor_id FROM Contractors WHERE contractor_name='Seed Taxi Contractor' AND ISNULL(is_deleted,0)=0 ORDER BY contractor_id), 1) AS smallint);
                    IF NOT EXISTS (SELECT 1 FROM Taxis WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxis
                        (rek_num, contractor_id, vmf_code, department_code, site_code, date_required, time_required, vehicle_type_code, official, rank, address_1, address_2, address_3, flight, date_created, created_by_user_code, is_deleted)
                        VALUES ('TAXI-SEED-001', @contractorId, CAST({defaultVehicleCode} AS varchar(20)), CAST({defaultDepartmentCode} AS smallint), CAST({defaultSiteCode} AS smallint), DATEADD(day,1,GETDATE()), DATEADD(hour,2,GETDATE()), 1, 'Seed Official', 'Manager', '1 Main Rd', 'Braamfontein', 'Johannesburg', NULL, GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Taxi_logs','U') IS NOT NULL BEGIN
                    DECLARE @logId int = ISNULL((SELECT MAX(log_id) FROM Taxi_logs), 0);
                    DECLARE @requestId int = ISNULL((SELECT TOP 1 request_id FROM Taxis WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0 ORDER BY request_id DESC), 0);
                    SET IDENTITY_INSERT Taxi_logs ON;
                    IF NOT EXISTS (SELECT 1 FROM Taxi_logs WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_logs
                        (log_id, request_id, rek_num, user_start_odo, user_end_odo, user_start_date, user_end_date, user_start_time, user_end_time, driver_start_odo, driver_end_odo, driver_start_date, driver_end_date, driver_start_time, driver_end_time, userid, enter_date, division, distance, days, date_created, created_by_user_code, is_deleted)
                        VALUES (@logId + 1, @requestId, 'TAXI-SEED-001', 12000, 12040, CAST(GETDATE() AS date), CAST(GETDATE() AS date), GETDATE(), DATEADD(hour,2,GETDATE()), 12000, 12040, CAST(GETDATE() AS date), CAST(GETDATE() AS date), GETDATE(), DATEADD(hour,2,GETDATE()), CAST({defaultUserCode} AS smallint), GETDATE(), 'Fleet Ops', 40, 1, GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Taxi_logs OFF;
                 END",
            $@"IF OBJECT_ID('Taxi_logs','U') IS NOT NULL BEGIN
                    DECLARE @requestId int = ISNULL((SELECT TOP 1 request_id FROM Taxis WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0 ORDER BY request_id DESC), 0);
                    IF NOT EXISTS (SELECT 1 FROM Taxi_logs WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_logs
                        (request_id, rek_num, user_start_odo, user_end_odo, user_start_date, user_end_date, user_start_time, user_end_time, driver_start_odo, driver_end_odo, driver_start_date, driver_end_date, driver_start_time, driver_end_time, userid, enter_date, division, distance, days, date_created, created_by_user_code, is_deleted)
                        VALUES (@requestId, 'TAXI-SEED-001', 12000, 12040, CAST(GETDATE() AS date), CAST(GETDATE() AS date), GETDATE(), DATEADD(hour,2,GETDATE()), 12000, 12040, CAST(GETDATE() AS date), CAST(GETDATE() AS date), GETDATE(), DATEADD(hour,2,GETDATE()), CAST({defaultUserCode} AS smallint), GETDATE(), 'Fleet Ops', 40, 1, GETDATE(), {defaultUserCode}, 0);
                 END");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Taxi_Log_changes','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Taxi_Log_changes WHERE rek_num='TAXI-SEED-001' AND ISNULL(is_deleted,0)=0)
                    INSERT INTO Taxi_Log_changes
                    (ID, rek_num, days, hours, km, date_changed, date_created, created_by_user_code, is_deleted)
                    VALUES (1, 'TAXI-SEED-001', 1, 2.0, 40.00, GETDATE(), GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Taxi_white_log','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Log_id) FROM Taxi_white_log),0);
                    SET IDENTITY_INSERT Taxi_white_log ON;
                    IF NOT EXISTS (SELECT 1 FROM Taxi_white_log WHERE vmf_code={defaultVehicleCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_white_log
                        (Log_id, vmf_code, start_odo, end_odo, start_date, end_date, driver, user_access_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultVehicleCode}, 15000, 15065, CAST(GETDATE() AS date), CAST(GETDATE() AS date), 'Seed Driver', CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Taxi_white_log OFF;
                 END",
            $@"IF OBJECT_ID('Taxi_white_log','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(Log_id) FROM Taxi_white_log),0);
                    IF NOT EXISTS (SELECT 1 FROM Taxi_white_log WHERE vmf_code={defaultVehicleCode} AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_white_log
                        (Log_id, vmf_code, start_odo, end_odo, start_date, end_date, driver, user_access_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultVehicleCode}, 15000, 15065, CAST(GETDATE() AS date), CAST(GETDATE() AS date), 'Seed Driver', CAST({defaultUserCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('Taxi_ScanDocs','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(taxi_scandoc_code) FROM Taxi_ScanDocs),0);
                    SET IDENTITY_INSERT Taxi_ScanDocs ON;
                    IF NOT EXISTS (SELECT 1 FROM Taxi_ScanDocs WHERE vmf_code={defaultVehicleCode} AND image='seeded/taxi_scandoc_001.pdf' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_ScanDocs
                        (taxi_scandoc_code, vmf_code, image, period_begin, period_end, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultVehicleCode}, 'seeded/taxi_scandoc_001.pdf', DATEADD(day,-30,GETDATE()), GETDATE(), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT Taxi_ScanDocs OFF;
                 END",
            $@"IF OBJECT_ID('Taxi_ScanDocs','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(taxi_scandoc_code) FROM Taxi_ScanDocs),0);
                    IF NOT EXISTS (SELECT 1 FROM Taxi_ScanDocs WHERE vmf_code={defaultVehicleCode} AND image='seeded/taxi_scandoc_001.pdf' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO Taxi_ScanDocs
                        (taxi_scandoc_code, vmf_code, image, period_begin, period_end, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultVehicleCode}, 'seeded/taxi_scandoc_001.pdf', DATEADD(day,-30,GETDATE()), GETDATE(), GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('taxi_log_notes','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(taxi_log_note_code) FROM taxi_log_notes),0);
                    SET IDENTITY_INSERT taxi_log_notes ON;
                    IF NOT EXISTS (SELECT 1 FROM taxi_log_notes WHERE taxi_log_note_description='Seeded taxi trip note' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO taxi_log_notes
                        (taxi_log_note_code, taxi_log_note_description, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded taxi trip note', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT taxi_log_notes OFF;
                 END",
            $@"IF OBJECT_ID('taxi_log_notes','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(taxi_log_note_code) FROM taxi_log_notes),0);
                    IF NOT EXISTS (SELECT 1 FROM taxi_log_notes WHERE taxi_log_note_description='Seeded taxi trip note' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO taxi_log_notes
                        (taxi_log_note_code, taxi_log_note_description, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded taxi trip note', GETDATE(), {defaultUserCode}, 0);
                 END");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('third_party_projects','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM third_party_projects WHERE description='Seeded Third Party Project' AND ISNULL(is_deleted,0)=0)
                    INSERT INTO third_party_projects
                    (department_code, site_code, description, start_date, end_date, responsible_person, rp_physical_address, rp_postal_address, rp_tel, rp_fax, rp_email, rp_cell, notes, order_reference, class_configuration, date_created, created_by_user_code, is_deleted)
                    VALUES (CAST({0} AS smallint), CAST({1} AS smallint), 'Seeded Third Party Project', GETDATE(), DATEADD(day, 90, GETDATE()), 'Seed Coordinator', '11 Project Park, Midrand', 'PO Box 77, Midrand', '0110000410', '0110000411', 'thirdparty@fis.local', '0820000412', 'Auto-seeded third-party project', 'ORD-TP-001', 'Class mix', GETDATE(), {2}, 0);
            END
        ", defaultDepartmentCode, defaultSiteCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('ClassRequirements','U') IS NOT NULL
            BEGIN
                DECLARE @projectId int = 0;
                IF OBJECT_ID('third_party_projects','U') IS NOT NULL
                    SELECT TOP 1 @projectId = project_id
                    FROM third_party_projects
                    WHERE description='Seeded Third Party Project' AND ISNULL(is_deleted,0)=0
                    ORDER BY project_id DESC;
                IF @projectId > 0
                   AND NOT EXISTS (SELECT 1 FROM ClassRequirements WHERE project_id=@projectId AND class_id=CAST({0} AS smallint) AND ISNULL(is_deleted,0)=0)
                    INSERT INTO ClassRequirements
                    (project_id, class_id, required_count, start_date, end_date, notes, date_created, created_by_user_code, is_deleted)
                    VALUES (@projectId, CAST({0} AS smallint), 2, GETDATE(), DATEADD(day, 90, GETDATE()), 'Seeded requirement row', GETDATE(), {1}, 0);
            END
        ", defaultClassCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Vehicle_orders','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Vehicle_orders WHERE order_number='VO-SEED-001' AND ISNULL(is_deleted,0)=0)
                    INSERT INTO Vehicle_orders
                    (make_code, model_code, quantity, supplier_id, order_number, date_created, created_by_user_code, is_deleted)
                    VALUES (1, CAST({0} AS smallint), 2, CAST({1} AS smallint), 'VO-SEED-001', GETDATE(), {2}, 0);
            END
        ", defaultModelCode, defaultSupplierId, defaultUserCode);

        Console.WriteLine("  ✓ Batch 18 taxi-thirdparty coverage updates applied.");

        var contractorsCount = await (await HasTableAsync(dbContext, "Contractors") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contractors WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var contractorTaxiClassCount = await (await HasTableAsync(dbContext, "Contractor_taxi_class") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Contractor_taxi_class WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxisCount = await (await HasTableAsync(dbContext, "Taxis") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Taxis WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxiLogsCount = await (await HasTableAsync(dbContext, "Taxi_logs") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Taxi_logs WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxiLogChangesCount = await (await HasTableAsync(dbContext, "Taxi_Log_changes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Taxi_Log_changes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxiWhiteLogCount = await (await HasTableAsync(dbContext, "Taxi_white_log") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Taxi_white_log WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxiScanDocsCount = await (await HasTableAsync(dbContext, "Taxi_ScanDocs") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Taxi_ScanDocs WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var taxiLogNotesCount = await (await HasTableAsync(dbContext, "taxi_log_notes") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM taxi_log_notes WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var thirdPartyProjectsCount = await (await HasTableAsync(dbContext, "third_party_projects") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM third_party_projects WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var classRequirementsCount = await (await HasTableAsync(dbContext, "ClassRequirements") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM ClassRequirements WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vehicleOrdersCount = await (await HasTableAsync(dbContext, "Vehicle_orders") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Vehicle_orders WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 18 | Contractors: {contractorsCount} | ContractorTaxiClass: {contractorTaxiClassCount} | Taxis: {taxisCount} | TaxiLogs: {taxiLogsCount} | TaxiLogChanges: {taxiLogChangesCount}");
        Console.WriteLine($"  📊 Batch 18 | TaxiWhiteLog: {taxiWhiteLogCount} | TaxiScanDocs: {taxiScanDocsCount} | TaxiLogNotes: {taxiLogNotesCount} | ThirdPartyProjects: {thirdPartyProjectsCount} | ClassRequirements: {classRequirementsCount} | VehicleOrders: {vehicleOrdersCount}");
    }

    private static async Task ApplyBatch19ContractsDocsCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultContractCode = await HasTableAsync(dbContext, "contract") &&
                                  await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 contract_code FROM contract WHERE ISNULL(is_deleted, 0) = 0 ORDER BY contract_code DESC")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultPostingMonthCode = await HasTableAsync(dbContext, "posting_month") &&
                                      await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM posting_month WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 posting_month_code FROM posting_month WHERE ISNULL(is_deleted, 0) = 0 ORDER BY posting_month_code DESC")
            : 0;

        var defaultExtraCode = await HasTableAsync(dbContext, "extra_codes") &&
                               await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM extra_codes WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 extra_code FROM extra_codes WHERE ISNULL(is_deleted, 0) = 0 ORDER BY extra_code")
            : 1;

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('LeaseContractTerms','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM LeaseContractTerms WHERE vmf_Code = {0} AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO LeaseContractTerms
                    (vmf_Code, AgreedTerms, AgreedKilos, AppliedInterest, FixedMonthlyAmount, AuthorityStatus, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, StartDate, EndDate, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, 36, 120000, 10.50, 7450.00, 1, {1}, GETDATE(), {1}, GETDATE(), DATEADD(day, -30, GETDATE()), DATEADD(year, 3, GETDATE()), GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('LeaseContractTermsComment','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(comment_code) FROM LeaseContractTermsComment), 0);
                    SET IDENTITY_INSERT LeaseContractTermsComment ON;
                    IF NOT EXISTS (SELECT 1 FROM LeaseContractTermsComment WHERE comment='Auto-seeded lease terms comment' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO LeaseContractTermsComment
                        (comment_code, comment, comment_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Auto-seeded lease terms comment', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT LeaseContractTermsComment OFF;
                 END",
            $@"IF OBJECT_ID('LeaseContractTermsComment','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(comment_code) FROM LeaseContractTermsComment), 0);
                    IF NOT EXISTS (SELECT 1 FROM LeaseContractTermsComment WHERE comment='Auto-seeded lease terms comment' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO LeaseContractTermsComment
                        (comment_code, comment, comment_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Auto-seeded lease terms comment', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('contract_rebillsplit','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(rebillsplit_code) FROM contract_rebillsplit), 0);
                    SET IDENTITY_INSERT contract_rebillsplit ON;
                    IF NOT EXISTS (SELECT 1 FROM contract_rebillsplit WHERE contract_code={defaultContractCode} AND site_code=CAST({defaultSiteCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO contract_rebillsplit
                        (rebillsplit_code, contract_code, rebill_percentage, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultContractCode}, 50.00, CAST({defaultSiteCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT contract_rebillsplit OFF;
                 END",
            $@"IF OBJECT_ID('contract_rebillsplit','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(rebillsplit_code) FROM contract_rebillsplit), 0);
                    IF NOT EXISTS (SELECT 1 FROM contract_rebillsplit WHERE contract_code={defaultContractCode} AND site_code=CAST({defaultSiteCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO contract_rebillsplit
                        (rebillsplit_code, contract_code, rebill_percentage, site_code, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultContractCode}, 50.00, CAST({defaultSiteCode} AS smallint), GETDATE(), {defaultUserCode}, 0);
                 END");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('scan_docs','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM scan_docs WHERE vmf_code = {0} AND image = 'seeded/scan_doc_001.pdf' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO scan_docs
                    (vmf_code, image, period_begin, period_end, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, 'seeded/scan_doc_001.pdf', DATEADD(day, -30, GETDATE()), GETDATE(), GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        if (defaultPostingMonthCode > 0)
        {
            await ExecuteIdentityAwareSqlAsync(
                dbContext,
                $@"IF OBJECT_ID('monthly_odo','U') IS NOT NULL BEGIN
                        DECLARE @code int = ISNULL((SELECT MAX(monthly_odo_code) FROM monthly_odo), 0);
                        SET IDENTITY_INSERT monthly_odo ON;
                        IF NOT EXISTS (SELECT 1 FROM monthly_odo WHERE vmf_code={defaultVehicleCode} AND posting_month_code=CAST({defaultPostingMonthCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                            INSERT INTO monthly_odo
                            (monthly_odo_code, vmf_code, posting_month_code, max_odometer, derived_odo, odometer_date, trans_type, date_created, created_by_user_code, is_deleted)
                            VALUES (@code + 1, {defaultVehicleCode}, CAST({defaultPostingMonthCode} AS smallint), 25500, 'Captured', GETDATE(), 'M', GETDATE(), {defaultUserCode}, 0);
                        SET IDENTITY_INSERT monthly_odo OFF;
                     END",
                $@"IF OBJECT_ID('monthly_odo','U') IS NOT NULL BEGIN
                        DECLARE @code int = ISNULL((SELECT MAX(monthly_odo_code) FROM monthly_odo), 0);
                        IF NOT EXISTS (SELECT 1 FROM monthly_odo WHERE vmf_code={defaultVehicleCode} AND posting_month_code=CAST({defaultPostingMonthCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                            INSERT INTO monthly_odo
                            (monthly_odo_code, vmf_code, posting_month_code, max_odometer, derived_odo, odometer_date, trans_type, date_created, created_by_user_code, is_deleted)
                            VALUES (@code + 1, {defaultVehicleCode}, CAST({defaultPostingMonthCode} AS smallint), 25500, 'Captured', GETDATE(), 'M', GETDATE(), {defaultUserCode}, 0);
                     END");
        }

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('temp_fleet_note','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(temp_fleet_notes_code) FROM temp_fleet_note), 0);
                    SET IDENTITY_INSERT temp_fleet_note ON;
                    IF NOT EXISTS (SELECT 1 FROM temp_fleet_note WHERE temp_vmf_code=3001 AND notes='Auto-seeded temp fleet note' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO temp_fleet_note
                        (temp_fleet_notes_code, temp_vmf_code, notes, update_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 3001, 'Auto-seeded temp fleet note', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT temp_fleet_note OFF;
                 END",
            $@"IF OBJECT_ID('temp_fleet_note','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(temp_fleet_notes_code) FROM temp_fleet_note), 0);
                    IF NOT EXISTS (SELECT 1 FROM temp_fleet_note WHERE temp_vmf_code=3001 AND notes='Auto-seeded temp fleet note' AND ISNULL(is_deleted,0)=0)
                        INSERT INTO temp_fleet_note
                        (temp_fleet_notes_code, temp_vmf_code, notes, update_date, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 3001, 'Auto-seeded temp fleet note', GETDATE(), GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('extras','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(extras_code) FROM extras), 0);
                    DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('extras'), 'extras_code', 'IsIdentity');
                    IF @isIdentity = 1
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM extras WHERE vmf_code={defaultVehicleCode} AND extra_code=CAST({defaultExtraCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                            INSERT INTO extras
                            (vmf_code, extra_code, quantity, amount, serial_number, date_created, created_by_user_code, is_deleted)
                            VALUES ({defaultVehicleCode}, CAST({defaultExtraCode} AS smallint), 1, 500.00, 'SN-SEED-001', GETDATE(), {defaultUserCode}, 0);
                    END
                    ELSE
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM extras WHERE vmf_code={defaultVehicleCode} AND extra_code=CAST({defaultExtraCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                            INSERT INTO extras
                            (extras_code, vmf_code, extra_code, quantity, amount, serial_number, date_created, created_by_user_code, is_deleted)
                            VALUES (@code + 1, {defaultVehicleCode}, CAST({defaultExtraCode} AS smallint), 1, 500.00, 'SN-SEED-001', GETDATE(), {defaultUserCode}, 0);
                    END
                 END",
            $@"IF OBJECT_ID('extras','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(extras_code) FROM extras), 0);
                    IF NOT EXISTS (SELECT 1 FROM extras WHERE vmf_code={defaultVehicleCode} AND extra_code=CAST({defaultExtraCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO extras
                        (extras_code, vmf_code, extra_code, quantity, amount, serial_number, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {defaultVehicleCode}, CAST({defaultExtraCode} AS smallint), 1, 500.00, 'SN-SEED-001', GETDATE(), {defaultUserCode}, 0);
                 END");

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('third_party_vehicle_model_description','U') IS NOT NULL BEGIN
                    SET IDENTITY_INSERT third_party_vehicle_model_description ON;
                    IF NOT EXISTS (SELECT 1 FROM third_party_vehicle_model_description WHERE vmf_code = CAST({defaultVehicleCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO third_party_vehicle_model_description
                        (vmf_code, model_description, date_created, created_by_user_code, is_deleted)
                        VALUES (CAST({defaultVehicleCode} AS smallint), 'Seeded Third-Party Model Description', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT third_party_vehicle_model_description OFF;
                 END",
            $@"IF OBJECT_ID('third_party_vehicle_model_description','U') IS NOT NULL BEGIN
                    IF NOT EXISTS (SELECT 1 FROM third_party_vehicle_model_description WHERE vmf_code = CAST({defaultVehicleCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO third_party_vehicle_model_description
                        (vmf_code, model_description, date_created, created_by_user_code, is_deleted)
                        VALUES (CAST({defaultVehicleCode} AS smallint), 'Seeded Third-Party Model Description', GETDATE(), {defaultUserCode}, 0);
                 END");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('third_party_allocations','U') IS NOT NULL
            BEGIN
                DECLARE @projectId int = 0;
                IF OBJECT_ID('third_party_projects','U') IS NOT NULL
                    SELECT TOP 1 @projectId = project_id FROM third_party_projects WHERE ISNULL(is_deleted,0)=0 ORDER BY project_id DESC;

                IF @projectId > 0
                   AND NOT EXISTS (SELECT 1 FROM third_party_allocations WHERE project_id=@projectId AND vehicle_id={0} AND ISNULL(is_deleted,0)=0)
                    INSERT INTO third_party_allocations
                    (project_id, supplier_id, vehicle_id, class_id, quantity, date_created, created_by_user_code, is_deleted)
                    VALUES (@projectId, 1, {0}, 1, 1, GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('PrivHireFuel_card','U') IS NOT NULL
            BEGIN
                DECLARE @phvCode int = 0;
                IF OBJECT_ID('Private_hire','U') IS NOT NULL
                    SELECT TOP 1 @phvCode = phv_code FROM Private_hire WHERE ISNULL(is_deleted,0)=0 ORDER BY phv_code;
                IF @phvCode > 0
                   AND NOT EXISTS (SELECT 1 FROM PrivHireFuel_card WHERE phv_code=@phvCode AND ISNULL(is_deleted,0)=0)
                    INSERT INTO PrivHireFuel_card
                    (phv_code, Counter, card_number, PAN_number, date_created, created_by_user_code, is_deleted)
                    VALUES (@phvCode, 1, RIGHT(CONCAT('000000000000000', CAST(710000000000000 + @phvCode AS varchar(20))), 15), RIGHT(CONCAT('000000000000000', CAST(810000000000000 + @phvCode AS varchar(20))), 15), GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        Console.WriteLine("  ✓ Batch 19 contracts-docs coverage updates applied.");

        var leaseTermsCount = await (await HasTableAsync(dbContext, "LeaseContractTerms") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM LeaseContractTerms WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var leaseTermsCommentCount = await (await HasTableAsync(dbContext, "LeaseContractTermsComment") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM LeaseContractTermsComment WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var rebillSplitCount = await (await HasTableAsync(dbContext, "contract_rebillsplit") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM contract_rebillsplit WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var scanDocsCount = await (await HasTableAsync(dbContext, "scan_docs") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM scan_docs WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var monthlyOdoCount = await (await HasTableAsync(dbContext, "monthly_odo") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM monthly_odo WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tempFleetNoteCount = await (await HasTableAsync(dbContext, "temp_fleet_note") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM temp_fleet_note WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var extrasCount = await (await HasTableAsync(dbContext, "extras") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM extras WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var thirdPartyModelDescCount = await (await HasTableAsync(dbContext, "third_party_vehicle_model_description") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM third_party_vehicle_model_description WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var thirdPartyAllocCount = await (await HasTableAsync(dbContext, "third_party_allocations") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM third_party_allocations WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var privHireFuelCardCount = await (await HasTableAsync(dbContext, "PrivHireFuel_card") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM PrivHireFuel_card WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 19 | LeaseTerms: {leaseTermsCount} | LeaseTermsComment: {leaseTermsCommentCount} | RebillSplit: {rebillSplitCount} | ScanDocs: {scanDocsCount} | MonthlyOdo: {monthlyOdoCount}");
        Console.WriteLine($"  📊 Batch 19 | TempFleetNote: {tempFleetNoteCount} | Extras: {extrasCount} | ThirdPartyModelDesc: {thirdPartyModelDescCount} | ThirdPartyAllocations: {thirdPartyAllocCount} | PrivHireFuelCards: {privHireFuelCardCount}");
    }

    private static async Task ApplyBatch20LegacyOpsTailCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('TS_Error_Code','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM TS_Error_Code WHERE Error_Code = 'E-SEED-001' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO TS_Error_Code
                    (Error_Code, Description, date_created, created_by_user_code, is_deleted)
                    VALUES ('E-SEED-001', 'Seeded diagnostic error code', GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('TS_Comment','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM TS_Comment WHERE Ref_number = {0} AND Comments = 'Seeded TS comment' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO TS_Comment
                    (Ref_number, Comments, date_created, created_by_user_code, is_deleted)
                    VALUES ({0}, 'Seeded TS comment', GETDATE(), {1}, 0);
            END
        ", defaultVehicleCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Notify_List','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Notify_List WHERE Notify_list_desc = 'Seeded Ops Alerts' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Notify_List
                    (Notify_list_desc, Notify_email1, date_created, created_by_user_code, is_deleted)
                    VALUES ('Seeded Ops Alerts', 'ops.alerts@fis.local', GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Req_num','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Req_num WHERE series = 'SEED-OPS' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Req_num
                    (series, number, date_created, created_by_user_code, is_deleted)
                    VALUES ('SEED-OPS', 1001, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('IL','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM IL WHERE department_code = CAST({0} AS float) AND bas_installation_code = 4500000 AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO IL
                    (department_code, bas_installation_code, date_created, created_by_user_code, is_deleted)
                    VALUES (CAST({0} AS float), 4500000, GETDATE(), {1}, 0);
            END
        ", defaultDepartmentCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('tyda','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM tyda WHERE xreknum = 'SEED-REQ-001' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO tyda
                    (xf_nom, xd_nom, xs_odo, xe_odo, xs_dat, xe_dat, xreknum, xtrdat, xclas, date_created, created_by_user_code, is_deleted)
                    VALUES ('GGX000001', 'Seed Driver', 10000, 10080, CAST(GETDATE() AS date), CAST(GETDATE() AS date), 'SEED-REQ-001', GETDATE(), 1, GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('tyda1','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM tyda1 WHERE xel = 'SEED-REQ-001' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO tyda1
                    (xf_nom, xr_nom, xd_nom, xd_naam, xs_odo, xe_odo, xs_dat, xe_dat, xel, date_created, created_by_user_code, is_deleted)
                    VALUES ('GGX000001', 'REG001GP', 'Seed Driver', 'Seed Driver Fullname', 10000, 10080, CAST(GETDATE() AS date), CAST(GETDATE() AS date), 'SEED-REQ-001', GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('AdHocHolidays','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(AdHocHolidayID) FROM AdHocHolidays), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('AdHocHolidays'), 'AdHocHolidayID', 'IsIdentity');

                IF NOT EXISTS (SELECT 1 FROM AdHocHolidays WHERE HolidayName = 'Seeded AdHoc Holiday' AND ISNULL(is_deleted,0)=0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO AdHocHolidays
                        (HolidayDate, HolidayName, date_created, created_by_user_code, is_deleted)
                        VALUES (DATEADD(day, 21, CAST(GETDATE() AS date)), 'Seeded AdHoc Holiday', GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO AdHocHolidays
                        (AdHocHolidayID, HolidayDate, HolidayName, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, DATEADD(day, 21, CAST(GETDATE() AS date)), 'Seeded AdHoc Holiday', GETDATE(), {0}, 0);
                    END
                END
            END", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Holidays','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Holidays WHERE HolidayDate = DATEADD(day, 28, CAST(GETDATE() AS date)) AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Holidays
                    (HolidayDate, HolidayName, date_created, created_by_user_code, is_deleted)
                    VALUES (DATEADD(day, 28, CAST(GETDATE() AS date)), 'Seeded Public Holiday', GETDATE(), {0}, 0);
            END
        ", defaultUserCode);

        await ExecuteIdentityAwareSqlAsync(
            dbContext,
            $@"IF OBJECT_ID('vip_site_map','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(vip_site_map_code) FROM vip_site_map), 0);
                    SET IDENTITY_INSERT vip_site_map ON;
                    IF NOT EXISTS (SELECT 1 FROM vip_site_map WHERE site_code = CAST({defaultSiteCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO vip_site_map
                        (vip_site_map_code, site_code, start_date, end_date, notes, date_updated, created_by_user_code, is_deleted)
                        VALUES (@code + 1, CAST({defaultSiteCode} AS smallint), CAST(GETDATE() AS date), DATEADD(year, 1, CAST(GETDATE() AS date)), 'Seeded VIP site mapping', GETDATE(), {defaultUserCode}, 0);
                    SET IDENTITY_INSERT vip_site_map OFF;
                 END",
            $@"IF OBJECT_ID('vip_site_map','U') IS NOT NULL BEGIN
                    DECLARE @code int = ISNULL((SELECT MAX(vip_site_map_code) FROM vip_site_map), 0);
                    IF NOT EXISTS (SELECT 1 FROM vip_site_map WHERE site_code = CAST({defaultSiteCode} AS smallint) AND ISNULL(is_deleted,0)=0)
                        INSERT INTO vip_site_map
                        (vip_site_map_code, site_code, start_date, end_date, notes, date_updated, created_by_user_code, is_deleted)
                        VALUES (@code + 1, CAST({defaultSiteCode} AS smallint), CAST(GETDATE() AS date), DATEADD(year, 1, CAST(GETDATE() AS date)), 'Seeded VIP site mapping', GETDATE(), {defaultUserCode}, 0);
                 END");

        Console.WriteLine("  ✓ Batch 20 legacy-ops-tail coverage updates applied.");

        var tsErrorCodeCount = await (await HasTableAsync(dbContext, "TS_Error_Code") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Error_Code WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tsCommentCount = await (await HasTableAsync(dbContext, "TS_Comment") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Comment WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notifyListCount = await (await HasTableAsync(dbContext, "Notify_List") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Notify_List WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var reqNumCount = await (await HasTableAsync(dbContext, "Req_num") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Req_num WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var ilCount = await (await HasTableAsync(dbContext, "IL") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM IL WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tydaCount = await (await HasTableAsync(dbContext, "tyda") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM tyda WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var tyda1Count = await (await HasTableAsync(dbContext, "tyda1") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM tyda1 WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var adHocHolidayCount = await (await HasTableAsync(dbContext, "AdHocHolidays") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM AdHocHolidays WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var holidayCount = await (await HasTableAsync(dbContext, "Holidays") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Holidays WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vipSiteMapCount = await (await HasTableAsync(dbContext, "vip_site_map") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vip_site_map WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 20 | TSErrorCode: {tsErrorCodeCount} | TSComment: {tsCommentCount} | NotifyList: {notifyListCount} | ReqNum: {reqNumCount} | IL: {ilCount}");
        Console.WriteLine($"  📊 Batch 20 | Tyda: {tydaCount} | Tyda1: {tyda1Count} | AdHocHolidays: {adHocHolidayCount} | Holidays: {holidayCount} | VipSiteMap: {vipSiteMapCount}");
    }

    private static async Task ApplyBatch21FinancialLedgerTailCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        var defaultVehicleCode = await HasTableAsync(dbContext, "vehicle_master") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 vmf_code FROM vehicle_master WHERE ISNULL(is_deleted, 0) = 0 ORDER BY vmf_code")
            : 1;

        var defaultDepartmentCode = await HasTableAsync(dbContext, "department") &&
                                    await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM department WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 department_code FROM department WHERE ISNULL(is_deleted, 0) = 0 ORDER BY department_code")
            : 1;

        var defaultSiteCode = await HasTableAsync(dbContext, "site") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM site WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 Site_code FROM site WHERE ISNULL(is_deleted, 0) = 0 ORDER BY Site_code")
            : 1;

        var defaultCostCategoryCode = await HasTableAsync(dbContext, "cost_category") &&
                                      await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM cost_category WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 cost_category_code FROM cost_category WHERE ISNULL(is_deleted, 0) = 0 ORDER BY cost_category_code")
            : 1;

        var defaultPostingYearCode = await HasTableAsync(dbContext, "posting_year") &&
                                     await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM posting_year WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 posting_year_code FROM posting_year WHERE ISNULL(is_deleted, 0) = 0 ORDER BY posting_year_code DESC")
            : DateTime.UtcNow.Year;

        var defaultPostingMonthCode = await HasTableAsync(dbContext, "posting_month") &&
                                      await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM posting_month WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 posting_month_code FROM posting_month WHERE ISNULL(is_deleted, 0) = 0 ORDER BY posting_month_code DESC")
            : 0;

        var defaultJournalCode = await HasTableAsync(dbContext, "journal") &&
                                 await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 journal_code FROM journal WHERE ISNULL(is_deleted, 0) = 0 ORDER BY journal_code DESC")
            : 0;

        var defaultJournalDetailTypeCode = await HasTableAsync(dbContext, "journal_detail_type") &&
                                           await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail_type WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 journal_detail_type_code FROM journal_detail_type WHERE ISNULL(is_deleted, 0) = 0 ORDER BY journal_detail_type_code")
            : 1;

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('transactions','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(trans_code) FROM transactions), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('transactions'), 'trans_code', 'IsIdentity');

                IF NOT EXISTS (SELECT 1 FROM transactions WHERE vmf_code = {0} AND date_service_delivered = CAST(GETDATE() AS date) AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO transactions
                        (vmf_code, cost_category_code, posting_month_code, date_service_delivered, odometer, derived_odo, rejected_odo, date_created, created_by_user_code, is_deleted)
                        VALUES ({0}, CAST({1} AS smallint), CASE WHEN {2} > 0 THEN CAST({2} AS smallint) ELSE NULL END, CAST(GETDATE() AS date), 26500, 'Captured', NULL, GETDATE(), {3}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO transactions
                        (trans_code, vmf_code, cost_category_code, posting_month_code, date_service_delivered, odometer, derived_odo, rejected_odo, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {0}, CAST({1} AS smallint), CASE WHEN {2} > 0 THEN CAST({2} AS smallint) ELSE NULL END, CAST(GETDATE() AS date), 26500, 'Captured', NULL, GETDATE(), {3}, 0);
                    END
                END
            END
        ", defaultVehicleCode, defaultCostCategoryCode, defaultPostingMonthCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('journal_detail','U') IS NOT NULL
            BEGIN
                DECLARE @id int = ISNULL((SELECT MAX(journal_detail_id) FROM journal_detail), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('journal_detail'), 'journal_detail_id', 'IsIdentity');
                DECLARE @detailCode uniqueidentifier = NEWID();

                IF NOT EXISTS (SELECT 1 FROM journal_detail WHERE journal_detail_description = 'Seeded batch21 journal detail' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO journal_detail
                        (journal_detail_code, journal_code, department_code, site_code, vmf_code, journal_detail_type_code, journal_detail_isdebit, journal_detail_quantity, journal_detail_tariff, journal_detail_amount, journal_detail_description, journal_detail_date_created, journal_detail_date_posted, journal_detail_isaccepted, journal_detail_financial_year, journal_detail_date, journal_detail_isreversaldenied, journal_detail_debitamount, date_created, created_by_user_code, is_deleted)
                        VALUES (@detailCode, CASE WHEN {0} > 0 THEN {0} ELSE NULL END, CAST({1} AS smallint), CAST({2} AS smallint), {3}, CAST({4} AS tinyint), 1, 1, 1000.00, 1000.00, 'Seeded batch21 journal detail', GETDATE(), GETDATE(), 1, CAST({5} AS varchar(10)), CAST(GETDATE() AS date), 0, 1000.00, GETDATE(), {6}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO journal_detail
                        (journal_detail_id, journal_detail_code, journal_code, department_code, site_code, vmf_code, journal_detail_type_code, journal_detail_isdebit, journal_detail_quantity, journal_detail_tariff, journal_detail_amount, journal_detail_description, journal_detail_date_created, journal_detail_date_posted, journal_detail_isaccepted, journal_detail_financial_year, journal_detail_date, journal_detail_isreversaldenied, journal_detail_debitamount, date_created, created_by_user_code, is_deleted)
                        VALUES (@id + 1, @detailCode, CASE WHEN {0} > 0 THEN {0} ELSE NULL END, CAST({1} AS smallint), CAST({2} AS smallint), {3}, CAST({4} AS tinyint), 1, 1, 1000.00, 1000.00, 'Seeded batch21 journal detail', GETDATE(), GETDATE(), 1, CAST({5} AS varchar(10)), CAST(GETDATE() AS date), 0, 1000.00, GETDATE(), {6}, 0);
                    END
                END
            END
        ", defaultJournalCode, defaultDepartmentCode, defaultSiteCode, defaultVehicleCode, defaultJournalDetailTypeCode, defaultPostingYearCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('journal_detail_allocation_exception','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(journal_detail_allocation_exception_code) FROM journal_detail_allocation_exception), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('journal_detail_allocation_exception'), 'journal_detail_allocation_exception_code', 'IsIdentity');
                DECLARE @detailCode uniqueidentifier = (SELECT TOP 1 journal_detail_code FROM journal_detail WHERE journal_detail_description = 'Seeded batch21 journal detail' AND ISNULL(is_deleted, 0) = 0 ORDER BY journal_detail_date_created DESC);

                IF @detailCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM journal_detail_allocation_exception WHERE journal_detail_code = @detailCode AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO journal_detail_allocation_exception
                        (journal_detail_code, journal_detail_allocation_exception_date_created, created_by_user_code, is_system_user, new_responsibility_code, date_created, is_deleted)
                        VALUES (@detailCode, GETDATE(), {0}, 0, '4500000', GETDATE(), 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO journal_detail_allocation_exception
                        (journal_detail_allocation_exception_code, journal_detail_code, journal_detail_allocation_exception_date_created, created_by_user_code, is_system_user, new_responsibility_code, date_created, is_deleted)
                        VALUES (@code + 1, @detailCode, GETDATE(), {0}, 0, '4500000', GETDATE(), 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Income_Split_TempTable','U') IS NOT NULL
            BEGIN
                DECLARE @detailId int = (SELECT TOP 1 journal_detail_id FROM journal_detail WHERE journal_detail_description = 'Seeded batch21 journal detail' AND ISNULL(is_deleted, 0) = 0 ORDER BY journal_detail_date_created DESC);
                DECLARE @detailCode uniqueidentifier = (SELECT TOP 1 journal_detail_code FROM journal_detail WHERE journal_detail_description = 'Seeded batch21 journal detail' AND ISNULL(is_deleted, 0) = 0 ORDER BY journal_detail_date_created DESC);

                IF @detailId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM Income_Split_TempTable WHERE journal_detail_id = @detailId AND Type = 'SeededSplit' AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Income_Split_TempTable
                    (Type, source_date, TransactionFinYear, journal_detail_id, journal_detail_code, journal_code, date_created, created_by_user_code, is_deleted)
                    VALUES ('SeededSplit', CAST(GETDATE() AS date), CAST({0} AS varchar(10)), @detailId, @detailCode, CASE WHEN {1} > 0 THEN {1} ELSE NULL END, GETDATE(), {2}, 0);
            END
        ", defaultPostingYearCode, defaultJournalCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('burn_rate','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(burn_rate_code) FROM burn_rate), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('burn_rate'), 'burn_rate_code', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM burn_rate WHERE department_number = CAST({0} AS varchar(50)) AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO burn_rate
                        (company_id, company_name, department_id, department_name, department_number, date_created, created_by_user_code, is_deleted)
                        VALUES (1, 'Gauteng Fleet', CAST({0} AS smallint), 'Seed Department', CAST({0} AS varchar(50)), GETDATE(), {1}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO burn_rate
                        (burn_rate_code, company_id, company_name, department_id, department_name, department_number, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 1, 'Gauteng Fleet', CAST({0} AS smallint), 'Seed Department', CAST({0} AS varchar(50)), GETDATE(), {1}, 0);
                    END
                END
            END
        ", defaultDepartmentCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Monthly_burn_rate','U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM Monthly_burn_rate WHERE [month] = MONTH(GETDATE()) AND [year] = YEAR(GETDATE()) AND department_number = {0} AND ISNULL(is_deleted, 0) = 0)
                    INSERT INTO Monthly_burn_rate
                    ([month], [year], department_number, Fixed_income_total, Fixed_cost_total, percent_Replacement, date_created, created_by_user_code, is_deleted)
                    VALUES (MONTH(GETDATE()), YEAR(GETDATE()), {0}, 125000.00, 97500.00, 78.00, GETDATE(), {1}, 0);
            END
        ", defaultDepartmentCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('vip_billing','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(vip_billing_code) FROM vip_billing), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('vip_billing'), 'vip_billing_code', 'IsIdentity');
                DECLARE @detailCode uniqueidentifier = (SELECT TOP 1 journal_detail_code FROM journal_detail WHERE journal_detail_description = 'Seeded batch21 journal detail' AND ISNULL(is_deleted, 0) = 0 ORDER BY journal_detail_date_created DESC);
                IF @detailCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM vip_billing WHERE journal_detail_code = @detailCode AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO vip_billing
                        (journal_detail_code, vmf_code, site_code, normal_midweek_hours, midweek_overtime_hours, date_created, created_by_user_code, is_deleted)
                        VALUES (@detailCode, {0}, {1}, 8.00, 2.00, GETDATE(), {2}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO vip_billing
                        (vip_billing_code, journal_detail_code, vmf_code, site_code, normal_midweek_hours, midweek_overtime_hours, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, @detailCode, {0}, {1}, 8.00, 2.00, GETDATE(), {2}, 0);
                    END
                END
            END
        ", defaultVehicleCode, defaultSiteCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('budget','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(budget_code) FROM budget), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('budget'), 'budget_code', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM budget WHERE vmf_code = {0} AND posting_year_code = CAST({1} AS smallint) AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO budget
                        (vmf_code, posting_year_code, annual_odo, date_created, created_by_user_code, is_deleted)
                        VALUES ({0}, CAST({1} AS smallint), 36000, GETDATE(), {2}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO budget
                        (budget_code, vmf_code, posting_year_code, annual_odo, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, {0}, CAST({1} AS smallint), 36000, GETDATE(), {2}, 0);
                    END
                END
            END
        ", defaultVehicleCode, defaultPostingYearCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('budget_amount','U') IS NOT NULL
            BEGIN
                DECLARE @budgetCode int = (SELECT TOP 1 budget_code FROM budget WHERE vmf_code = {0} AND posting_year_code = CAST({1} AS smallint) AND ISNULL(is_deleted, 0) = 0 ORDER BY budget_code DESC);
                DECLARE @code int = ISNULL((SELECT MAX(budget_amount_code) FROM budget_amount), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('budget_amount'), 'budget_amount_code', 'IsIdentity');

                IF @budgetCode IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM budget_amount WHERE budget_code = @budgetCode AND cost_category_code = CAST({2} AS smallint) AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO budget_amount
                        (budget_code, cost_category_code, amount, date_created, created_by_user_code, is_deleted)
                        VALUES (@budgetCode, CAST({2} AS smallint), 120000.00, GETDATE(), {3}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO budget_amount
                        (budget_amount_code, budget_code, cost_category_code, amount, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, @budgetCode, CAST({2} AS smallint), 120000.00, GETDATE(), {3}, 0);
                    END
                END
            END
        ", defaultVehicleCode, defaultPostingYearCode, defaultCostCategoryCode, defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('Surcharge','U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(surcharge_code) FROM Surcharge), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('Surcharge'), 'surcharge_code', 'IsIdentity');
                DECLARE @regNo varchar(50) = ISNULL((SELECT TOP 1 registration_number FROM vehicle_master WHERE vmf_code = {0}), CONCAT('REG-', {0}));

                IF NOT EXISTS (SELECT 1 FROM Surcharge WHERE vmf_code = {0} AND AuthorityNo = 91001 AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO Surcharge
                        (Department, Site_code, RegNo1, RegNo2, Same, vmf_code, Merchant, TrxDate, ServiceType, AuthorityNo, date_created, created_by_user_code, is_deleted)
                        VALUES ('Seed Department', CAST({1} AS smallint), @regNo, @regNo, 'Y', {0}, 'Fuel Merchant Seed', GETDATE(), 'Fuel', 91001, GETDATE(), {2}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO Surcharge
                        (surcharge_code, Department, Site_code, RegNo1, RegNo2, Same, vmf_code, Merchant, TrxDate, ServiceType, AuthorityNo, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seed Department', CAST({1} AS smallint), @regNo, @regNo, 'Y', {0}, 'Fuel Merchant Seed', GETDATE(), 'Fuel', 91001, GETDATE(), {2}, 0);
                    END
                END
            END
        ", defaultVehicleCode, defaultSiteCode, defaultUserCode);

        Console.WriteLine("  ✓ Batch 21 financial-ledger tail coverage updates applied.");

        var transactionsCount = await (await HasTableAsync(dbContext, "transactions") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM transactions WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var journalDetailCount = await (await HasTableAsync(dbContext, "journal_detail") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var journalAllocExCount = await (await HasTableAsync(dbContext, "journal_detail_allocation_exception") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM journal_detail_allocation_exception WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var incomeSplitCount = await (await HasTableAsync(dbContext, "Income_Split_TempTable") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Income_Split_TempTable WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var burnRateCount = await (await HasTableAsync(dbContext, "burn_rate") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM burn_rate WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var monthlyBurnRateCount = await (await HasTableAsync(dbContext, "Monthly_burn_rate") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Monthly_burn_rate WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var vipBillingCount = await (await HasTableAsync(dbContext, "vip_billing") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM vip_billing WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var budgetCount = await (await HasTableAsync(dbContext, "budget") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM budget WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var budgetAmountCount = await (await HasTableAsync(dbContext, "budget_amount") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM budget_amount WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var surchargeCount = await (await HasTableAsync(dbContext, "Surcharge") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM Surcharge WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 21 | Transactions: {transactionsCount} | JournalDetail: {journalDetailCount} | JournalAllocEx: {journalAllocExCount} | IncomeSplitTemp: {incomeSplitCount} | BurnRate: {burnRateCount}");
        Console.WriteLine($"  📊 Batch 21 | MonthlyBurnRate: {monthlyBurnRateCount} | VipBilling: {vipBillingCount} | Budget: {budgetCount} | BudgetAmount: {budgetAmountCount} | Surcharge: {surchargeCount}");
    }

    private static async Task ApplyBatch22WorkflowAnalyticsCoverageAsync(FisDbContext dbContext)
    {
        var defaultUserCode = await HasTableAsync(dbContext, "TS_Users") &&
                              await QueryCountAsync(dbContext, "SELECT COUNT(1) FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0") > 0
            ? await QueryCountAsync(dbContext, "SELECT TOP 1 user_access_code FROM TS_Users WHERE ISNULL(is_deleted, 0) = 0 ORDER BY user_access_code")
            : 1;

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[Workflow]', 'U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(WorkflowID) FROM [Workflow].[Workflow]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[Workflow]'), 'WorkflowID', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[Workflow]
                        (WorkflowName, AlwaysExecute, date_created, created_by_user_code, is_deleted)
                        VALUES ('Seeded Batch22 Workflow', 1, GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[Workflow]
                        (WorkflowID, WorkflowName, AlwaysExecute, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded Batch22 Workflow', 1, GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[StepType]', 'U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(StepTypeID) FROM [Workflow].[StepType]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[StepType]'), 'StepTypeID', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM [Workflow].[StepType] WHERE StepTypeName = 'Seeded Processor' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[StepType]
                        (StepTypeName, StepTypeData, date_created, created_by_user_code, is_deleted)
                        VALUES ('Seeded Processor', 'category=seed', GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[StepType]
                        (StepTypeID, StepTypeName, StepTypeData, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded Processor', 'category=seed', GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[Step]', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (SELECT TOP 1 WorkflowID FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0 ORDER BY WorkflowID DESC);
                DECLARE @stepTypeId int = (SELECT TOP 1 StepTypeID FROM [Workflow].[StepType] WHERE StepTypeName = 'Seeded Processor' AND ISNULL(is_deleted, 0) = 0 ORDER BY StepTypeID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(StepID) FROM [Workflow].[Step]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[Step]'), 'StepID', 'IsIdentity');
                IF @workflowId IS NOT NULL AND @stepTypeId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[Step] WHERE StepName = 'Seeded Step 1' AND WorkflowID = @workflowId AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[Step]
                        (StepName, StepOrder, StepTypeID, WorkflowID, ParentStepID, StepParameters, HandlerType, IsConditional, ConditionExpression, TrueStepID, FalseStepID, date_created, created_by_user_code, is_deleted)
                        VALUES ('Seeded Step 1', 1, @stepTypeId, @workflowId, NULL, 'mode=seed', 'SeedHandler', 0, NULL, NULL, NULL, GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[Step]
                        (StepID, StepName, StepOrder, StepTypeID, WorkflowID, ParentStepID, StepParameters, HandlerType, IsConditional, ConditionExpression, TrueStepID, FalseStepID, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded Step 1', 1, @stepTypeId, @workflowId, NULL, 'mode=seed', 'SeedHandler', 0, NULL, NULL, NULL, GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[Status]', 'U') IS NOT NULL
            BEGIN
                DECLARE @stepId int = (SELECT TOP 1 StepID FROM [Workflow].[Step] WHERE StepName = 'Seeded Step 1' AND ISNULL(is_deleted, 0) = 0 ORDER BY StepID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(StatusID) FROM [Workflow].[Status]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[Status]'), 'StatusID', 'IsIdentity');
                IF @stepId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[Status] WHERE StepID = @stepId AND StartedByUserName = 'seed.user' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[Status]
                        (StepID, DateCompleted, DateStarted, IsBusy, StartedByUserName, date_created, created_by_user_code, is_deleted)
                        VALUES (@stepId, GETDATE(), DATEADD(minute, -5, GETDATE()), 0, 'seed.user', GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[Status]
                        (StatusID, StepID, DateCompleted, DateStarted, IsBusy, StartedByUserName, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, @stepId, GETDATE(), DATEADD(minute, -5, GETDATE()), 0, 'seed.user', GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[EventMap]', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (SELECT TOP 1 WorkflowID FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0 ORDER BY WorkflowID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(EventMapID) FROM [Workflow].[EventMap]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[EventMap]'), 'EventMapID', 'IsIdentity');
                IF @workflowId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[EventMap] WHERE EventName = 'SeededEvent.Batch22' AND WorkflowID = @workflowId AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[EventMap]
                        (EventName, WorkflowID, date_created, created_by_user_code, is_deleted)
                        VALUES ('SeededEvent.Batch22', @workflowId, GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[EventMap]
                        (EventMapID, EventName, WorkflowID, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'SeededEvent.Batch22', @workflowId, GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[NotificationTemplate]', 'U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(TemplateID) FROM [Workflow].[NotificationTemplate]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[NotificationTemplate]'), 'TemplateID', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM [Workflow].[NotificationTemplate] WHERE TemplateName = 'Seeded Template Batch22' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[NotificationTemplate]
                        (TemplateName, Description, Subject, Body, TemplateType, Variables, IsActive, date_created, created_by_user_code, is_deleted)
                        VALUES ('Seeded Template Batch22', 'Seeded template', 'Seeded Subject', 'Seeded Body WorkflowName', 'Email', 'WorkflowName', 1, GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[NotificationTemplate]
                        (TemplateID, TemplateName, Description, Subject, Body, TemplateType, Variables, IsActive, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'Seeded Template Batch22', 'Seeded template', 'Seeded Subject', 'Seeded Body WorkflowName', 'Email', 'WorkflowName', 1, GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[WorkflowNotification]', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (SELECT TOP 1 WorkflowID FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0 ORDER BY WorkflowID DESC);
                DECLARE @stepId int = (SELECT TOP 1 StepID FROM [Workflow].[Step] WHERE StepName = 'Seeded Step 1' AND ISNULL(is_deleted, 0) = 0 ORDER BY StepID DESC);
                DECLARE @templateId int = (SELECT TOP 1 TemplateID FROM [Workflow].[NotificationTemplate] WHERE TemplateName = 'Seeded Template Batch22' AND ISNULL(is_deleted, 0) = 0 ORDER BY TemplateID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(NotificationID) FROM [Workflow].[WorkflowNotification]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[WorkflowNotification]'), 'NotificationID', 'IsIdentity');
                IF @workflowId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[WorkflowNotification] WHERE WorkflowID = @workflowId AND EventType = 'WorkflowCompleted' AND RecipientIdentifier = 'ops@fis.local' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[WorkflowNotification]
                        (WorkflowID, StepID, EventType, RecipientType, RecipientIdentifier, NotificationTemplateID, Subject, Body, IsActive, SendDelay, date_created, created_by_user_code, is_deleted)
                        VALUES (@workflowId, @stepId, 'WorkflowCompleted', 'Email', 'ops@fis.local', @templateId, 'Seeded Workflow Completed', 'Workflow completed notification', 1, 0, GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[WorkflowNotification]
                        (NotificationID, WorkflowID, StepID, EventType, RecipientType, RecipientIdentifier, NotificationTemplateID, Subject, Body, IsActive, SendDelay, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, @workflowId, @stepId, 'WorkflowCompleted', 'Email', 'ops@fis.local', @templateId, 'Seeded Workflow Completed', 'Workflow completed notification', 1, 0, GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[NotificationLog]', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (SELECT TOP 1 WorkflowID FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0 ORDER BY WorkflowID DESC);
                DECLARE @stepId int = (SELECT TOP 1 StepID FROM [Workflow].[Step] WHERE StepName = 'Seeded Step 1' AND ISNULL(is_deleted, 0) = 0 ORDER BY StepID DESC);
                DECLARE @statusId int = (SELECT TOP 1 StatusID FROM [Workflow].[Status] WHERE StartedByUserName = 'seed.user' AND ISNULL(is_deleted, 0) = 0 ORDER BY StatusID DESC);
                DECLARE @notificationId int = (SELECT TOP 1 NotificationID FROM [Workflow].[WorkflowNotification] WHERE RecipientIdentifier = 'ops@fis.local' AND ISNULL(is_deleted, 0) = 0 ORDER BY NotificationID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(LogID) FROM [Workflow].[NotificationLog]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[NotificationLog]'), 'LogID', 'IsIdentity');
                IF @workflowId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[NotificationLog] WHERE WorkflowID = @workflowId AND EventType = 'WorkflowCompleted' AND RecipientEmail = 'ops@fis.local' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[NotificationLog]
                        (NotificationID, WorkflowID, StepID, StatusID, EventType, RecipientEmail, Subject, Body, SentAt, DeliveryStatus, ErrorMessage, RetryCount, ExternalMessageId, date_created, is_deleted)
                        VALUES (@notificationId, @workflowId, @stepId, @statusId, 'WorkflowCompleted', 'ops@fis.local', 'Seeded Workflow Completed', 'Workflow completed notification log', GETDATE(), 'Sent', NULL, 0, 'seed-msg-001', GETDATE(), 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[NotificationLog]
                        (LogID, NotificationID, WorkflowID, StepID, StatusID, EventType, RecipientEmail, Subject, Body, SentAt, DeliveryStatus, ErrorMessage, RetryCount, ExternalMessageId, date_created, is_deleted)
                        VALUES (@code + 1, @notificationId, @workflowId, @stepId, @statusId, 'WorkflowCompleted', 'ops@fis.local', 'Seeded Workflow Completed', 'Workflow completed notification log', GETDATE(), 'Sent', NULL, 0, 'seed-msg-001', GETDATE(), 0);
                    END
                END
            END
        ");

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[Audit]', 'U') IS NOT NULL
            BEGIN
                DECLARE @code int = ISNULL((SELECT MAX(AuditID) FROM [Workflow].[Audit]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[Audit]'), 'AuditID', 'IsIdentity');
                IF NOT EXISTS (SELECT 1 FROM [Workflow].[Audit] WHERE Action = 'INSERT' AND TableName = 'Workflow' AND PrimaryKey = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[Audit]
                        (Action, TableName, PrimaryKey, Changes, ActionedBy, date_created, created_by_user_code, is_deleted)
                        VALUES ('INSERT', 'Workflow', 'Seeded Batch22 Workflow', 'Created seeded workflow for batch 22', 'seed.user', GETDATE(), {0}, 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[Audit]
                        (AuditID, Action, TableName, PrimaryKey, Changes, ActionedBy, date_created, created_by_user_code, is_deleted)
                        VALUES (@code + 1, 'INSERT', 'Workflow', 'Seeded Batch22 Workflow', 'Created seeded workflow for batch 22', 'seed.user', GETDATE(), {0}, 0);
                    END
                END
            END
        ", defaultUserCode);

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('[Workflow].[WorkflowExecutionSummary]', 'U') IS NOT NULL
            BEGIN
                DECLARE @workflowId int = (SELECT TOP 1 WorkflowID FROM [Workflow].[Workflow] WHERE WorkflowName = 'Seeded Batch22 Workflow' AND ISNULL(is_deleted, 0) = 0 ORDER BY WorkflowID DESC);
                DECLARE @statusId int = (SELECT TOP 1 StatusID FROM [Workflow].[Status] WHERE StartedByUserName = 'seed.user' AND ISNULL(is_deleted, 0) = 0 ORDER BY StatusID DESC);
                DECLARE @code int = ISNULL((SELECT MAX(SummaryID) FROM [Workflow].[WorkflowExecutionSummary]), 0);
                DECLARE @isIdentity int = COLUMNPROPERTY(OBJECT_ID('[Workflow].[WorkflowExecutionSummary]'), 'SummaryID', 'IsIdentity');
                IF @workflowId IS NOT NULL AND @statusId IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM [Workflow].[WorkflowExecutionSummary] WHERE WorkflowID = @workflowId AND StatusID = @statusId AND ISNULL(is_deleted, 0) = 0)
                BEGIN
                    IF @isIdentity = 1
                    BEGIN
                        INSERT INTO [Workflow].[WorkflowExecutionSummary]
                        (WorkflowID, StatusID, WorkflowName, StartedAt, CompletedAt, DurationSeconds, TotalSteps, CompletedSteps, CurrentStepName, ExecutionStatus, StartedByUserName, NotificationsSent, ErrorCount, date_created, is_deleted)
                        VALUES (@workflowId, @statusId, 'Seeded Batch22 Workflow', DATEADD(minute, -5, GETDATE()), GETDATE(), 300, 1, 1, 'Seeded Step 1', 'Completed', 'seed.user', 1, 0, GETDATE(), 0);
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [Workflow].[WorkflowExecutionSummary]
                        (SummaryID, WorkflowID, StatusID, WorkflowName, StartedAt, CompletedAt, DurationSeconds, TotalSteps, CompletedSteps, CurrentStepName, ExecutionStatus, StartedByUserName, NotificationsSent, ErrorCount, date_created, is_deleted)
                        VALUES (@code + 1, @workflowId, @statusId, 'Seeded Batch22 Workflow', DATEADD(minute, -5, GETDATE()), GETDATE(), 300, 1, 1, 'Seeded Step 1', 'Completed', 'seed.user', 1, 0, GETDATE(), 0);
                    END
                END
            END
        ");

        Console.WriteLine("  ✓ Batch 22 workflow-analytics coverage updates applied.");

        var workflowCount = await (await HasTableAsync(dbContext, "Workflow", "Workflow") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[Workflow] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var stepTypeCount = await (await HasTableAsync(dbContext, "Workflow", "StepType") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[StepType] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var stepCount = await (await HasTableAsync(dbContext, "Workflow", "Step") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[Step] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var statusCount = await (await HasTableAsync(dbContext, "Workflow", "Status") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[Status] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var eventMapCount = await (await HasTableAsync(dbContext, "Workflow", "EventMap") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[EventMap] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notificationTemplateCount = await (await HasTableAsync(dbContext, "Workflow", "NotificationTemplate") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[NotificationTemplate] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var workflowNotificationCount = await (await HasTableAsync(dbContext, "Workflow", "WorkflowNotification") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[WorkflowNotification] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var notificationLogCount = await (await HasTableAsync(dbContext, "Workflow", "NotificationLog") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[NotificationLog] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var auditCount = await (await HasTableAsync(dbContext, "Workflow", "Audit") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[Audit] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));
        var workflowExecutionSummaryCount = await (await HasTableAsync(dbContext, "Workflow", "WorkflowExecutionSummary") ? QueryCountAsync(dbContext, "SELECT COUNT(1) FROM [Workflow].[WorkflowExecutionSummary] WHERE ISNULL(is_deleted, 0) = 0") : Task.FromResult(0));

        Console.WriteLine($"  📊 Batch 22 | Workflow: {workflowCount} | StepType: {stepTypeCount} | Step: {stepCount} | Status: {statusCount} | EventMap: {eventMapCount}");
        Console.WriteLine($"  📊 Batch 22 | NotificationTemplate: {notificationTemplateCount} | WorkflowNotification: {workflowNotificationCount} | NotificationLog: {notificationLogCount} | Audit: {auditCount} | WorkflowExecutionSummary: {workflowExecutionSummaryCount}");
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "Seeder validation uses fixed internal SQL literals only.")]
    private static async Task<int> QueryCountAsync(FisDbContext dbContext, string sql)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
            openedHere = true;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        var count = result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);

        if (openedHere)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return count;
    }

    private static async Task<bool> HasColumnAsync(FisDbContext dbContext, string tableName, string columnName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
            openedHere = true;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id WHERE t.name = @tableName AND c.name = @columnName";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var columnParam = command.CreateParameter();
        columnParam.ParameterName = "@columnName";
        columnParam.Value = columnName;
        command.Parameters.Add(columnParam);

        var result = await command.ExecuteScalarAsync();
        var exists = result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;

        if (openedHere)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return exists;
    }

    private static async Task<bool> HasTableAsync(FisDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
            openedHere = true;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sys.tables WHERE name = @tableName";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var result = await command.ExecuteScalarAsync();
        var exists = result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;

        if (openedHere)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return exists;
    }

    private static async Task<bool> HasTableAsync(FisDbContext dbContext, string schemaName, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync();
            openedHere = true;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = @schemaName AND t.name = @tableName";

        var schemaParam = command.CreateParameter();
        schemaParam.ParameterName = "@schemaName";
        schemaParam.Value = schemaName;
        command.Parameters.Add(schemaParam);

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var result = await command.ExecuteScalarAsync();
        var exists = result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;

        if (openedHere)
        {
            await dbContext.Database.CloseConnectionAsync();
        }

        return exists;
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
                services.AddLogging(builder => builder.AddConsole());
            });
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            
            Console.WriteLine("📑 Seeding reference data (Batch 1)...");
            await SeedReferenceData(dbContext);

            Console.WriteLine("🏢 Seeding organization data...");
            await SeedOrganizationData(dbContext);

            Console.WriteLine("🚗 Seeding vehicle data...");
            await SeedVehicleData(dbContext);

            Console.WriteLine("📜 Seeding contract data...");
            await SeedContractData(dbContext);

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

        // Seed User Profiles (user_access_old1 table) with FirstName/LastName for login
        Console.WriteLine("  🧑 Seeding user profiles (FirstName, LastName, etc.)...");

        var userProfiles = new[]
        {
            new UserAccessOld
            {
                user_access_code = 1,
                FirstName = "Murcus",
                LastName = "Developer",
                E_Mail = "murcus@corptech.co.za",
                telephone = "+27 11 123 4567",
                password = "Password123!", // TODO: Hash in production
                user_status = "Active",
                user_active = true,
                Site_code = 1,
                Position_Code = 1,
                AccessLevel = 1,
                date_created = DateTime.UtcNow,
                is_deleted = false
            },
            new UserAccessOld
            {
                user_access_code = 2,
                FirstName = "Admin",
                LastName = "User",
                E_Mail = "xxodbeats@gmail.com",
                telephone = "+27 82 555 1234",
                password = "Password123!",
                user_status = "Active",
                user_active = true,
                Site_code = 1,
                Position_Code = 1,
                AccessLevel = 1,
                date_created = DateTime.UtcNow,
                is_deleted = false
            },
            new UserAccessOld
            {
                user_access_code = 3,
                FirstName = "IT",
                LastName = "Support",
                E_Mail = "It@kulungwana.co.za",
                telephone = "+27 11 987 6543",
                password = "Password123!",
                user_status = "Active",
                user_active = true,
                Site_code = 1,
                Position_Code = 2,
                AccessLevel = 2,
                date_created = DateTime.UtcNow,
                is_deleted = false
            },
            new UserAccessOld
            {
                user_access_code = 4,
                FirstName = "Backup",
                LastName = "Admin",
                E_Mail = "info.backup@kulungwana.co.za",
                telephone = "+27 11 987 6544",
                password = "Password123!",
                user_status = "Active",
                user_active = true,
                Site_code = 1,
                Position_Code = 1,
                AccessLevel = 1,
                date_created = DateTime.UtcNow,
                is_deleted = false
            }
        };

        dbContext.UserAccessOlds.AddRange(userProfiles);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"  ✓ Added {userProfiles.Length} user profiles with FirstName/LastName");
        Console.WriteLine("  📝 Test login: FirstName='Murcus', Password='Password123!'");
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
        if (!await dbContext.Departments.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT department ON;
                INSERT INTO department (department_code, description, company_code, dept_active, Service_Years, Overhead_Percentage, Service_Kilometres, date_created, is_deleted) VALUES
                (1, 'Transport', 1, 1, 1, 10, 15000, GETDATE(), 0),
                (2, 'Logistics', 1, 1, 1, 10, 15000, GETDATE(), 0),
                (3, 'Admin', 1, 1, 1, 10, 15000, GETDATE(), 0);
                SET IDENTITY_INSERT department OFF;
            ");
            Console.WriteLine("  ✓ Departments seeded.");
        }

        if (!await dbContext.Sites.AnyAsync())
        {
             await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT site ON;
                INSERT INTO site (Site_code, description, Depatrment_code, site_active, date_created, is_deleted) VALUES
                (1, 'Head Office', 1, 1, GETDATE(), 0),
                (2, 'Cape Town Branch', 2, 1, GETDATE(), 0);
                SET IDENTITY_INSERT site OFF;
            ");
            Console.WriteLine("  ✓ Sites seeded.");
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
            Console.WriteLine("  ✓ Contracts seeded (ensured 10 contracts).");
        }
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
        if (!await dbContext.TripDrivers.AnyAsync())
        {
            // Seed Trip Drivers linked to Trips (1, 2)
            await dbContext.Database.ExecuteSqlRawAsync(@"
                SET IDENTITY_INSERT trip_driver ON;
                INSERT INTO trip_driver (
                    trip_driver_code, trip_authority_code, trip_driver_name, trip_driver_id, 
                    trip_driver_primary, driver_licence_type_id, driver_active, driver_hasPDP, date_created, is_deleted
                ) VALUES 
                (1, 1, 'John Doe', '8001015009087', 1, 2, 1, 1, GETDATE(), 0),
                (2, 2, 'Jane Smith', '8505050050080', 1, 1, 1, 1, GETDATE(), 0);
                SET IDENTITY_INSERT trip_driver OFF;
            ");
            Console.WriteLine("  ✓ Drivers seeded.");
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
                INSERT INTO Suppliers (supplier_id, name, address, tel, contact_person, email, supplier_type, active, date_created, is_deleted) VALUES 
                (1, 'Toyota SA', 'Sandton, JHB', '011 809 9111', 'Sales Manager', 'sales@toyota.co.za', 'Manufacturer', 1, GETDATE(), 0),
                (2, 'Ford SA', 'Silverton, Pretoria', '012 800 1234', 'Fleet Sales', 'fleet@ford.co.za', 'Manufacturer', 1, GETDATE(), 0),
                (3, 'Avis Fleet', 'Isando, JHB', '011 923 3900', 'Account Mgr', 'accounts@avisfleet.co.za', 'Leasing', 1, GETDATE(), 0);
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

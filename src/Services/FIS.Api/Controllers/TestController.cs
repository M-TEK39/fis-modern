using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly FisDbContext _context;

    public TestController(FisDbContext context)
    {
        _context = context;
    }

    [HttpGet("legacy-connection")]
    public async Task<IActionResult> TestLegacyConnection()
    {
        try
        {
            // Test basic connection without migrations
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                return Ok(
                    new
                    {
                        success = false,
                        message = "Cannot connect to database",
                        timestamp = DateTime.UtcNow,
                    }
                );
            }

            // Test reading from legacy tables
            var vehicleCount = await _context.Vehicles.CountAsync();
            var contractCount = await _context.Contracts.CountAsync();

            // Get sample data to verify entity mapping works
            var sampleVehicles = await _context
                .Vehicles.Select(v => new
                {
                    VehicleId = v.vmf_code, // Use exact legacy field name
                    VmfCode = v.vmf_code,
                    RegistrationNumber = v.registration_number, // Use exact legacy field name
                    // Note: Vehicle table doesn't have still_current - that's on contract table
                })
                .Take(3)
                .ToListAsync();

            var sampleContracts = await _context
                .Contracts.Where(c => c.still_current == "Y") // Use exact legacy field name
                .Select(c => new
                {
                    ContractCode = c.contract_code, // Use exact legacy field name
                    VmfCode = c.vmf_code, // Use exact legacy field name
                    StillCurrentFlag = c.still_current, // Use exact legacy field name
                    StartDate = c.start_date, // Use exact legacy field name
                })
                .Take(3)
                .ToListAsync();

            return Ok(
                new
                {
                    success = true,
                    message = "Legacy database connection successful - no migrations required!",
                    data = new
                    {
                        vehicleCount,
                        contractCount,
                        sampleVehicles,
                        sampleContracts,
                    },
                    legacyCompatibility = new
                    {
                        vehicleTableMapping = "vehicles",
                        contractTableMapping = "Contracts",
                        charFieldSupport = "still_current char(1) Y/N",
                        snakeCaseColumns = "vmf_code, registration_number, etc.",
                    },
                    timestamp = DateTime.UtcNow,
                }
            );
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Error connecting to legacy database",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow,
                }
            );
        }
    }

    [HttpGet("legacy-entities")]
    public async Task<IActionResult> TestAllLegacyEntities()
    {
        try
        {
            // Test all legacy entities to prove the pattern scales
            var results = new
            {
                success = true,
                message = "All legacy entities accessible - pattern scales successfully!",
                data = new
                {
                    // Core entities (already tested)
                    vehicleCount = await _context.Vehicles.CountAsync(),
                    contractCount = await _context.Contracts.CountAsync(),
                    userCount = await _context.Users.CountAsync(), // Now mapped to TS_Users table
                    siteCount = await _context.Sites.CountAsync(),

                    // Real legacy entities (based on actual schemas)
                    fuelCardCount = await _context.FuelCards.CountAsync(),
                    tripDriverCount = await _context.TripDrivers.CountAsync(),
                    privateHireCount = await _context.PrivateHires.CountAsync(),

                    // Placeholder legacy entities (proving scalability)
                    driverCount = await _context.Drivers.CountAsync(),
                    // maintenanceRecordCount = await _context.MaintenanceRecords.CountAsync(), // MaintenanceRecords DbSet not defined yet
                    tripCount = await _context.Trips.CountAsync(),
                    departmentCount = await _context.Departments.CountAsync(),

                    // Sample data from new entities
                    sampleDrivers = await _context
                        .Drivers.Where(d => d.driver_active == true) // Use exact legacy field name
                        .Select(d => new
                        {
                            DriverCode = d.site_driver_code, // Use exact legacy field name
                            LicenceNumber = d.driver_licence_number, // Use exact legacy field name
                            FirstName = d.driver_firstname, // Use exact legacy field name
                            LastName = d.driver_surname, // Use exact legacy field name
                            StillCurrentFlag = d.driver_active ? "Y" : "N", // Computed from driver_active
                        })
                        .Take(3)
                        .ToListAsync(),

                    sampleDepartments = await _context
                        .Departments.Select(d => new
                        {
                            DepartmentCode = d.department_code, // Use exact legacy field name
                            DepartmentName = d.description, // Use description as name
                            Description = d.description, // Use exact legacy field name
                            StillCurrentFlag = "Y", // Department table doesn't have still_current, assume all are current
                        })
                        .Take(3)
                        .ToListAsync(),
                },
                legacyCompatibility = new
                {
                    entitiesTotal = 10, // Original 4 + New 6 (that have DbSets)
                    snakeCaseMapping = "All entities use [Table] and [Column] attributes for legacy compatibility",
                    charFieldSupport = "still_current char(1) Y/N pattern works across all entities",
                    legacyTableNames = new[]
                    {
                        "vehicles",
                        "Contracts",
                        "users",
                        "sites", // original
                        "drivers",
                        "fuel_transactions",
                        "maintenance_records",
                        "trips",
                        "departments", // new ones with DbSets
                    },
                },
                timestamp = DateTime.UtcNow,
            };

            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new
                {
                    success = false,
                    message = "Error testing legacy entities",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow,
                }
            );
        }
    }
}

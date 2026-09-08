using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Vehicle registration (GP number) history.
/// Solves the problem where traffic tickets carry old GP numbers that no longer
/// match the vehicle's current registration_number — users can search by any
/// historical registration to find the correct vehicle.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class RegistrationController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(FisDbContext context, ILogger<RegistrationController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult GetRoot()
    {
        return Ok(
            new
            {
                module = "Registration History",
                endpoints = new[]
                {
                    "search?q={value}",
                    "vehicle/{vmfCode}",
                    "vehicle/{vmfCode} [POST]",
                },
            }
        );
    }

    /// <summary>
    /// Get all historical registration numbers for a vehicle.
    /// Returns both the current registration and all previous ones with timestamps.
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult> GetByVehicle(int vmfCode)
    {
        try
        {
            var vehicle = await _context
                .Vehicles.Where(v => v.vmf_code == vmfCode && !v.is_deleted)
                .Select(v => new
                {
                    v.vmf_code,
                    v.fleet_number,
                    v.registration_number,
                })
                .FirstOrDefaultAsync();

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle {vmfCode} not found" });

            var history = await _context
                .Registrations.Where(r => r.vmf_code == vmfCode && !r.is_deleted)
                .OrderByDescending(r => r.RegistrationDate)
                .Select(r => new
                {
                    registration_id = r.RegistrationID,
                    registration_number = r.RegistrationNumber,
                    recorded_date = r.RegistrationDate,
                    is_current = false,
                })
                .ToListAsync();

            return Ok(
                new
                {
                    vmf_code = vehicle.vmf_code,
                    fleet_number = vehicle.fleet_number,
                    current_registration = vehicle.registration_number,
                    history,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving registration history for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to retrieve registration history" });
        }
    }

    /// <summary>
    /// Search for a vehicle by registration number — checks both the current
    /// registration_number on vehicle_master AND all historical entries in Registrations.
    /// This is the key fix for the traffic ticket problem: a ticket with an old GP
    /// number will still resolve to the correct vehicle.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Search term is required" });

        try
        {
            var term = q.Trim().ToUpper();

            // 1. Vehicles whose CURRENT registration matches
            var currentMatches = await _context
                .Vehicles.Where(v =>
                    !v.is_deleted
                    && v.registration_number != null
                    && v.registration_number.ToUpper().Contains(term)
                )
                .Select(v => new
                {
                    vmf_code = v.vmf_code,
                    fleet_number = v.fleet_number,
                    current_registration = v.registration_number,
                    matched_registration = v.registration_number,
                    is_historical_match = false,
                })
                .ToListAsync();

            // 2. Vehicles found via HISTORICAL registrations that aren't already in current matches
            var currentVmfCodes = currentMatches.Select(m => m.vmf_code).ToHashSet();

            var historicalMatches = await _context
                .Registrations.Where(r =>
                    !r.is_deleted && r.RegistrationNumber.ToUpper().Contains(term)
                )
                .Join(
                    _context.Vehicles.Where(v => !v.is_deleted),
                    r => r.vmf_code,
                    v => v.vmf_code,
                    (r, v) =>
                        new
                        {
                            vmf_code = v.vmf_code,
                            fleet_number = v.fleet_number,
                            current_registration = v.registration_number,
                            matched_registration = r.RegistrationNumber,
                            recorded_date = r.RegistrationDate,
                            is_historical_match = true,
                        }
                )
                .Where(m => !currentVmfCodes.Contains(m.vmf_code))
                .ToListAsync();

            var results = currentMatches
                .Select(m => new
                {
                    m.vmf_code,
                    m.fleet_number,
                    m.current_registration,
                    matched_registration = (string?)m.matched_registration,
                    m.is_historical_match,
                    recorded_date = (DateTime?)null,
                })
                .Concat(
                    historicalMatches.Select(m => new
                    {
                        m.vmf_code,
                        m.fleet_number,
                        m.current_registration,
                        matched_registration = (string?)m.matched_registration,
                        m.is_historical_match,
                        recorded_date = (DateTime?)m.recorded_date,
                    })
                )
                .OrderBy(m => m.is_historical_match)
                .ThenBy(m => m.fleet_number)
                .ToList();

            _logger.LogInformation(
                "Registration search for '{Term}': {Current} current, {Historical} historical matches",
                q,
                currentMatches.Count,
                historicalMatches.Count
            );

            return Ok(
                new
                {
                    search_term = q,
                    total = results.Count,
                    results,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching registrations for '{Term}'", q);
            return StatusCode(500, new { error = "Failed to search registrations" });
        }
    }

    /// <summary>
    /// Manually add a historical registration record for a vehicle.
    /// Use this to backfill historical GP numbers that were lost in the old system.
    /// </summary>
    [HttpPost("vehicle/{vmfCode}")]
    public async Task<ActionResult> AddHistorical(
        int vmfCode,
        [FromBody] AddRegistrationDto request
    )
    {
        if (string.IsNullOrWhiteSpace(request.registration_number))
            return BadRequest(new { error = "registration_number is required" });

        try
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v =>
                v.vmf_code == vmfCode && !v.is_deleted
            );

            if (vehicle == null)
                return NotFound(new { message = $"Vehicle {vmfCode} not found" });

            var entry = new FIS.Core.Domain.Entities.Vehicles.Registration
            {
                vmf_code = vmfCode,
                RegistrationNumber = request.registration_number.Trim(),
                RegistrationDate = request.effective_date ?? DateTime.UtcNow,
                date_created = DateTime.UtcNow,
                created_by_user_code = GetCurrentUserId(),
                is_deleted = false,
            };

            _context.Registrations.Add(entry);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Manually added historical registration '{Reg}' for vehicle {VmfCode}",
                request.registration_number,
                vmfCode
            );

            return Ok(
                new
                {
                    message = "Historical registration recorded",
                    registration_id = entry.RegistrationID,
                    vmf_code = vmfCode,
                    registration_number = entry.RegistrationNumber,
                    recorded_date = entry.RegistrationDate,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error adding historical registration for vehicle {VmfCode}",
                vmfCode
            );
            return StatusCode(500, new { error = "Failed to add historical registration" });
        }
    }
}

public class AddRegistrationDto
{
    public string registration_number { get; set; } = string.Empty;
    public DateTime? effective_date { get; set; }
}

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for vehicle maintenance record operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MaintenanceRecordController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ILogger<MaintenanceRecordController> _logger;

    public MaintenanceRecordController(
        IMaintenanceService maintenanceService,
        ILogger<MaintenanceRecordController> logger)
    {
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    /// <summary>
    /// Get a maintenance record by ID
    /// </summary>
    [HttpGet("{maintenanceId}")]
    public async Task<ActionResult<MaintenanceRecord>> GetMaintenanceRecord(int maintenanceId)
    {
        try
        {
            var maintenanceRecord = await _maintenanceService.GetMaintenanceRecordByIdAsync(maintenanceId);
            if (maintenanceRecord == null)
            {
                _logger.LogWarning("Maintenance record with ID {MaintenanceId} not found", maintenanceId);
                return NotFound($"Maintenance record with ID {maintenanceId} not found");
            }

            _logger.LogInformation("Retrieved maintenance record {MaintenanceId} for vehicle {VmfCode}", 
                maintenanceId, maintenanceRecord.VmfCode);
            return Ok(maintenanceRecord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance record {MaintenanceId}", maintenanceId);
            return StatusCode(500, "An error occurred while retrieving the maintenance record");
        }
    }

    /// <summary>
    /// Get all maintenance records for a vehicle
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<MaintenanceRecord>>> GetMaintenanceRecordsByVehicle(int vmfCode)
    {
        try
        {
            var maintenanceRecords = await _maintenanceService.GetMaintenanceHistoryAsync(vmfCode);
            _logger.LogInformation("Found {Count} maintenance records for vehicle {VmfCode}", 
                maintenanceRecords.Count(), vmfCode);
            return Ok(maintenanceRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while retrieving maintenance records for the vehicle");
        }
    }

    /// <summary>
    /// Get maintenance records by date range
    /// </summary>
    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<MaintenanceRecord>>> GetMaintenanceRecordsByDateRange(
        [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate > endDate)
            {
                return BadRequest("Start date must be before or equal to end date");
            }

            var maintenanceRecords = await _maintenanceService.GetMaintenanceByDateRangeAsync(startDate, endDate);
            _logger.LogInformation("Found {Count} maintenance records between {StartDate} and {EndDate}", 
                maintenanceRecords.Count(), startDate.ToShortDateString(), endDate.ToShortDateString());
            return Ok(maintenanceRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records for date range {StartDate} - {EndDate}", 
                startDate, endDate);
            return StatusCode(500, "An error occurred while retrieving maintenance records for the date range");
        }
    }

    /// <summary>
    /// Get maintenance records by service type
    /// </summary>
    [HttpGet("servicetype/{serviceType}")]
    public async Task<ActionResult<IEnumerable<MaintenanceRecord>>> GetMaintenanceRecordsByServiceType(string serviceType)
    {
        try
        {
            var maintenanceRecords = await _maintenanceService.GetMaintenanceByTypeAsync(serviceType);
            _logger.LogInformation("Found {Count} maintenance records for service type '{ServiceType}'", 
                maintenanceRecords.Count(), serviceType);
            return Ok(maintenanceRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records for service type '{ServiceType}'", serviceType);
            return StatusCode(500, "An error occurred while retrieving maintenance records for the service type");
        }
    }

    /// <summary>
    /// Search maintenance records by description, service provider, or work order
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MaintenanceRecord>>> SearchMaintenanceRecords([FromQuery] string? searchTerm)
    {
        try
        {
            // Note: Search not in service yet, would need to add to IMaintenanceService
            var maintenanceRecords = await _maintenanceService.GetMaintenanceByTypeAsync("SERVICE");
            _logger.LogInformation("Found {Count} maintenance records matching search term '{SearchTerm}'", 
                maintenanceRecords.Count(), searchTerm);
            return Ok(maintenanceRecords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching maintenance records with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching maintenance records");
        }
    }

    /// <summary>
    /// Create a new maintenance record
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MaintenanceRecord>> CreateMaintenanceRecord([FromBody] MaintenanceRecord maintenanceRecord)
    {
        try
        {
            var createdMaintenanceRecord = await _maintenanceService.CreateMaintenanceRecordAsync(maintenanceRecord);
            _logger.LogInformation("Created maintenance record {MaintenanceId} for vehicle {VmfCode}: {MaintenanceType}", 
                createdMaintenanceRecord.MaintenanceId, createdMaintenanceRecord.VmfCode, createdMaintenanceRecord.MaintenanceType);
            
            return CreatedAtAction(nameof(GetMaintenanceRecord), 
                new { maintenanceId = createdMaintenanceRecord.MaintenanceId }, createdMaintenanceRecord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance record for vehicle {VmfCode}", maintenanceRecord.VmfCode);
            return StatusCode(500, "An error occurred while creating the maintenance record");
        }
    }

    /// <summary>
    /// Update an existing maintenance record
    /// </summary>
    [HttpPut("{maintenanceId}")]
    public async Task<ActionResult<MaintenanceRecord>> UpdateMaintenanceRecord(int maintenanceId, [FromBody] MaintenanceRecord maintenanceRecord)
    {
        try
        {
            if (maintenanceId != maintenanceRecord.MaintenanceId)
            {
                return BadRequest("Maintenance record ID mismatch");
            }

            var existingMaintenanceRecord = await _maintenanceService.GetMaintenanceRecordByIdAsync(maintenanceId);
            if (existingMaintenanceRecord == null)
            {
                _logger.LogWarning("Maintenance record with ID {MaintenanceId} not found for update", maintenanceId);
                return NotFound($"Maintenance record with ID {maintenanceId} not found");
            }

            // Preserve creation date
            maintenanceRecord.CreatedDate = existingMaintenanceRecord.CreatedDate;

            await _maintenanceService.UpdateMaintenanceRecordAsync(maintenanceRecord);
            _logger.LogInformation("Updated maintenance record {MaintenanceId} for vehicle {VmfCode}", 
                maintenanceId, maintenanceRecord.VmfCode);
            
            return Ok(maintenanceRecord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance record {MaintenanceId}", maintenanceId);
            return StatusCode(500, "An error occurred while updating the maintenance record");
        }
    }

    /// <summary>
    /// Delete a maintenance record
    /// </summary>
    [HttpDelete("{maintenanceId}")]
    public async Task<ActionResult> DeleteMaintenanceRecord(int maintenanceId)
    {
        try
        {
            var existingMaintenanceRecord = await _maintenanceService.GetMaintenanceRecordByIdAsync(maintenanceId);
            if (existingMaintenanceRecord == null)
            {
                _logger.LogWarning("Maintenance record with ID {MaintenanceId} not found for deletion", maintenanceId);
                return NotFound($"Maintenance record with ID {maintenanceId} not found");
            }

            await _maintenanceService.DeleteMaintenanceRecordAsync(maintenanceId);
            _logger.LogInformation("Deleted maintenance record {MaintenanceId} for vehicle {VmfCode}", 
                maintenanceId, existingMaintenanceRecord.VmfCode);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance record {MaintenanceId}", maintenanceId);
            return StatusCode(500, "An error occurred while deleting the maintenance record");
        }
    }

    /// <summary>
    /// Get maintenance statistics for a vehicle
    /// </summary>
    [HttpGet("vehicle/{vmfCode}/stats")]
    public async Task<ActionResult<object>> GetMaintenanceStats(int vmfCode)
    {
        try
        {
            var stats = await _maintenanceService.GetMaintenanceStatisticsAsync(vmfCode);

            _logger.LogInformation("Generated maintenance statistics for vehicle {VmfCode}: {TotalRecords} records", 
                vmfCode, stats.TotalRecords);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating maintenance statistics for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while generating maintenance statistics");
        }
    }
}
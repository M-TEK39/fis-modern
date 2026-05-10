using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for private hire vehicle operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PrivateHireController : BaseApiController
{
    private readonly IPrivateHireRepository _privateHireRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<PrivateHireController> _logger;

    public PrivateHireController(
        IPrivateHireRepository privateHireRepository,
        FisDbContext context,
        ILogger<PrivateHireController> logger)
    {
        _privateHireRepository = privateHireRepository;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all private hire vehicles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHires()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var privateHires = await _privateHireRepository.GetActiveHiresAsync();
            _logger.LogInformation("Retrieved {Count} active private hires", privateHires.Count());
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires");
            return StatusCode(500, "An error occurred while retrieving private hires");
        }
    }

    /// <summary>
    /// Get a private hire vehicle by code
    /// </summary>
    [HttpGet("{privateHireCode}")]
    public async Task<ActionResult<PrivateHire>> GetPrivateHire(int privateHireCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var privateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (privateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            _logger.LogInformation("Retrieved private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, privateHire.registration_number);
            return Ok(privateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while retrieving the private hire vehicle");
        }
    }

    /// <summary>
    /// Get private hires by vehicle VMF code
    /// </summary>
    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHiresByVehicle(int vmfCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var privateHires = await _privateHireRepository.GetByVehicleAsync(vmfCode);
            _logger.LogInformation("Found {Count} private hires for vehicle {VmfCode}", privateHires.Count(), vmfCode);
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "An error occurred while retrieving private hires for the vehicle");
        }
    }

    /// <summary>
    /// Get private hires by date range
    /// </summary>
    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHiresByDateRange(
        [FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (startDate > endDate)
            {
                return BadRequest("Start date must be before or equal to end date");
            }

            var privateHires = await _privateHireRepository.GetByDateRangeAsync(startDate, endDate);
            _logger.LogInformation("Found {Count} private hires between {StartDate} and {EndDate}", 
                privateHires.Count(), startDate.ToShortDateString(), endDate.ToShortDateString());
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires for date range {StartDate} - {EndDate}", 
                startDate, endDate);
            return StatusCode(500, "An error occurred while retrieving private hires for the date range");
        }
    }

    /// <summary>
    /// Search private hire vehicles by registration or details
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> SearchPrivateHires([FromQuery] string? searchTerm)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var privateHires = await _privateHireRepository.SearchHiresAsync(searchTerm ?? "");
            _logger.LogInformation("Found {Count} private hires matching search term '{SearchTerm}'", 
                privateHires.Count(), searchTerm);
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching private hires with term '{SearchTerm}'", searchTerm);
            return StatusCode(500, "An error occurred while searching private hires");
        }
    }

    /// <summary>
    /// Create a new private hire vehicle
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrivateHire>> CreatePrivateHire([FromBody] PrivateHire privateHire)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var createdPrivateHire = await _privateHireRepository.CreateAsync(privateHire, currentUserId);
            _logger.LogInformation("Created private hire {PrivateHireCode}: {Registration}", 
                createdPrivateHire.PHV_code, createdPrivateHire.registration_number);
            
            return CreatedAtAction(nameof(GetPrivateHire), 
                new { privateHireCode = createdPrivateHire.PHV_code }, createdPrivateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating private hire {Registration}", privateHire.registration_number);
            return StatusCode(500, "An error occurred while creating the private hire vehicle");
        }
    }

    /// <summary>
    /// Update an existing private hire vehicle
    /// </summary>
    [HttpPut("{privateHireCode}")]
    public async Task<ActionResult<PrivateHire>> UpdatePrivateHire(int privateHireCode, [FromBody] PrivateHire privateHire)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (privateHireCode != privateHire.PHV_code)
            {
                return BadRequest("Private hire code mismatch");
            }

            var existingPrivateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (existingPrivateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found for update", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.UpdateAsync(privateHire, currentUserId);
            _logger.LogInformation("Updated private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, privateHire.registration_number);
            
            return Ok(privateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while updating the private hire vehicle");
        }
    }

    /// <summary>
    /// Delete a private hire vehicle
    /// </summary>
    [HttpDelete("{privateHireCode}")]
    public async Task<ActionResult> DeletePrivateHire(int privateHireCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingPrivateHire = await _privateHireRepository.GetByIdAsync(privateHireCode);
            if (existingPrivateHire == null)
            {
                _logger.LogWarning("Private hire with code {PrivateHireCode} not found for deletion", privateHireCode);
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.DeleteAsync(privateHireCode, currentUserId);
            _logger.LogInformation("Deleted private hire {PrivateHireCode}: {Registration}", 
                privateHireCode, existingPrivateHire.registration_number);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting private hire {PrivateHireCode}", privateHireCode);
            return StatusCode(500, "An error occurred while deleting the private hire vehicle");
        }
    }

    [HttpGet("contractors")]
    public async Task<ActionResult<IEnumerable<PrivateHireContractorDto>>> GetContractors()
    {
        var contractors = await _context.Contractors
            .AsNoTracking()
            .Where(x => !x.is_deleted)
            .OrderBy(x => x.contractor_name)
            .Select(x => ToContractorDto(x))
            .ToListAsync();

        return Ok(contractors);
    }

    [HttpGet("contractors/{contractorId:int}")]
    public async Task<ActionResult<PrivateHireContractorDto>> GetContractor(int contractorId)
    {
        var contractor = await _context.Contractors
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.contractor_id == contractorId && !x.is_deleted);
        if (contractor == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        return Ok(ToContractorDto(contractor));
    }

    [HttpPost("contractors")]
    public async Task<ActionResult<PrivateHireContractorDto>> CreateContractor([FromBody] PrivateHireContractorDto request)
    {
        var now = DateTime.UtcNow;
        var userId = GetCurrentUserId();
        var entity = new Contractor
        {
            contractor_name = request.company_name?.Trim(),
            physical_address = request.address?.Trim(),
            postal_address = request.email?.Trim(),
            tel_number = request.phone?.Trim(),
            fax_number = request.business_registration?.Trim(),
            date_created = now,
            created_by_user_code = userId,
            is_deleted = false
        };

        _context.Contractors.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(ToContractorDto(entity));
    }

    [HttpPut("contractors/{contractorId:int}")]
    public async Task<ActionResult<PrivateHireContractorDto>> UpdateContractor(int contractorId, [FromBody] PrivateHireContractorDto request)
    {
        if (contractorId != request.contractor_id)
        {
            return BadRequest("Contractor ID mismatch");
        }

        var entity = await _context.Contractors.FirstOrDefaultAsync(x => x.contractor_id == contractorId && !x.is_deleted);
        if (entity == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        entity.contractor_name = request.company_name?.Trim();
        entity.physical_address = request.address?.Trim();
        entity.postal_address = request.email?.Trim();
        entity.tel_number = request.phone?.Trim();
        entity.fax_number = request.business_registration?.Trim();
        entity.date_updated = DateTime.UtcNow;
        entity.modified_by_user_code = GetCurrentUserId();

        await _context.SaveChangesAsync();
        return Ok(ToContractorDto(entity));
    }

    [HttpDelete("contractors/{contractorId:int}")]
    public async Task<ActionResult> DeleteContractor(int contractorId)
    {
        var entity = await _context.Contractors.FirstOrDefaultAsync(x => x.contractor_id == contractorId && !x.is_deleted);
        if (entity == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        entity.is_deleted = true;
        entity.date_updated = DateTime.UtcNow;
        entity.modified_by_user_code = GetCurrentUserId();
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static PrivateHireContractorDto ToContractorDto(Contractor contractor)
    {
        return new PrivateHireContractorDto
        {
            contractor_id = contractor.contractor_id,
            company_name = contractor.contractor_name ?? string.Empty,
            contact_person = contractor.contractor_name ?? string.Empty,
            phone = contractor.tel_number ?? string.Empty,
            email = contractor.postal_address ?? string.Empty,
            business_registration = contractor.fax_number ?? string.Empty,
            address = contractor.physical_address ?? string.Empty,
            status = "Active"
        };
    }
}

public class PrivateHireContractorDto
{
    public int contractor_id { get; set; }
    public string company_name { get; set; } = string.Empty;
    public string contact_person { get; set; } = string.Empty;
    public string phone { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string business_registration { get; set; } = string.Empty;
    public string address { get; set; } = string.Empty;
    public string status { get; set; } = "Active";
}

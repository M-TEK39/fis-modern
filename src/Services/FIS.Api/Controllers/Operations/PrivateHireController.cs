using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// API Controller for private hire vehicle operations
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PrivateHireController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly IPrivateHireRepository _privateHireRepository;
    private readonly ILogger<PrivateHireController> _logger;

    public PrivateHireController(
        IPrivateHireRepository privateHireRepository,
        ILogger<PrivateHireController> logger
    )
    {
        _privateHireRepository = privateHireRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get a server-paginated page of private hire vehicles using the existing list/search filters.
    /// </summary>
    [HttpGet("page")]
    public async Task<ActionResult> GetPrivateHirePage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? searchTerm = null
    )
    {
        try
        {
            var result = await _privateHireRepository.GetPageAsync(
                new PrivateHirePageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    searchTerm
                )
            );

            return Ok(
                new
                {
                    items = result.Items,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged private hire vehicles");
            return StatusCode(500, "An error occurred while retrieving private hires");
        }
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
                _logger.LogWarning(
                    "Private hire with code {PrivateHireCode} not found",
                    privateHireCode
                );
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            _logger.LogInformation(
                "Retrieved private hire {PrivateHireCode}: {Registration}",
                privateHireCode,
                privateHire.registration_number
            );
            return Ok(privateHire);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving private hire {PrivateHireCode}",
                privateHireCode
            );
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
            _logger.LogInformation(
                "Found {Count} private hires for vehicle {VmfCode}",
                privateHires.Count(),
                vmfCode
            );
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hires for vehicle {VmfCode}", vmfCode);
            return StatusCode(
                500,
                "An error occurred while retrieving private hires for the vehicle"
            );
        }
    }

    /// <summary>
    /// Get private hires by date range
    /// </summary>
    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> GetPrivateHiresByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            if (startDate > endDate)
            {
                return BadRequest("Start date must be before or equal to end date");
            }

            var privateHires = await _privateHireRepository.GetByDateRangeAsync(startDate, endDate);
            _logger.LogInformation(
                "Found {Count} private hires between {StartDate} and {EndDate}",
                privateHires.Count(),
                startDate.ToShortDateString(),
                endDate.ToShortDateString()
            );
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving private hires for date range {StartDate} - {EndDate}",
                startDate,
                endDate
            );
            return StatusCode(
                500,
                "An error occurred while retrieving private hires for the date range"
            );
        }
    }

    /// <summary>
    /// Search private hire vehicles by registration or details
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<PrivateHire>>> SearchPrivateHires(
        [FromQuery] string? searchTerm
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var privateHires = await _privateHireRepository.SearchHiresAsync(searchTerm ?? "");
            _logger.LogInformation(
                "Found {Count} private hires matching search term '{SearchTerm}'",
                privateHires.Count(),
                searchTerm
            );
            return Ok(privateHires);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching private hires with term '{SearchTerm}'",
                searchTerm
            );
            return StatusCode(500, "An error occurred while searching private hires");
        }
    }

    /// <summary>
    /// Create a new private hire vehicle
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrivateHire>> CreatePrivateHire(
        [FromBody] PrivateHire privateHire
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var createdPrivateHire = await _privateHireRepository.CreateAsync(
                privateHire,
                currentUserId
            );
            _logger.LogInformation(
                "Created private hire {PrivateHireCode}: {Registration}",
                createdPrivateHire.PHV_code,
                createdPrivateHire.registration_number
            );

            return CreatedAtAction(
                nameof(GetPrivateHire),
                new { privateHireCode = createdPrivateHire.PHV_code },
                createdPrivateHire
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating private hire {Registration}",
                privateHire.registration_number
            );
            return StatusCode(500, "An error occurred while creating the private hire vehicle");
        }
    }

    /// <summary>
    /// Update an existing private hire vehicle
    /// </summary>
    [HttpPut("{privateHireCode}")]
    public async Task<ActionResult<PrivateHire>> UpdatePrivateHire(
        int privateHireCode,
        [FromBody] PrivateHire privateHire
    )
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
                _logger.LogWarning(
                    "Private hire with code {PrivateHireCode} not found for update",
                    privateHireCode
                );
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.UpdateAsync(privateHire, currentUserId);
            _logger.LogInformation(
                "Updated private hire {PrivateHireCode}: {Registration}",
                privateHireCode,
                privateHire.registration_number
            );

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
                _logger.LogWarning(
                    "Private hire with code {PrivateHireCode} not found for deletion",
                    privateHireCode
                );
                return NotFound($"Private hire with code {privateHireCode} not found");
            }

            await _privateHireRepository.DeleteAsync(privateHireCode, currentUserId);
            _logger.LogInformation(
                "Deleted private hire {PrivateHireCode}: {Registration}",
                privateHireCode,
                existingPrivateHire.registration_number
            );

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
        var contractors = await _privateHireRepository.GetContractorsAsync();
        return Ok(contractors.Select(ToContractorDto));
    }

    /// <summary>
    /// Get a server-paginated page of private hire contractors.
    /// </summary>
    [HttpGet("contractors/page")]
    public async Task<ActionResult> GetContractorPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var result = await _privateHireRepository.GetContractorPageAsync(
                new PrivateHireContractorPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize)
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(ToContractorDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged private hire contractors");
            return StatusCode(500, "An error occurred while retrieving private hire contractors");
        }
    }

    [HttpGet("contractors/{contractorId:int}")]
    public async Task<ActionResult<PrivateHireContractorDto>> GetContractor(int contractorId)
    {
        var contractor = await _privateHireRepository.GetContractorByIdAsync(contractorId);
        if (contractor == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        return Ok(ToContractorDto(contractor));
    }

    [HttpPost("contractors")]
    public async Task<ActionResult<PrivateHireContractorDto>> CreateContractor(
        [FromBody] PrivateHireContractorDto request
    )
    {
        var contractor = await _privateHireRepository.CreateContractorAsync(
            ToContractorRecord(request),
            GetCurrentUserId()
        );
        return Ok(ToContractorDto(contractor));
    }

    [HttpPut("contractors/{contractorId:int}")]
    public async Task<ActionResult<PrivateHireContractorDto>> UpdateContractor(
        int contractorId,
        [FromBody] PrivateHireContractorDto request
    )
    {
        if (contractorId != request.contractor_id)
        {
            return BadRequest("Contractor ID mismatch");
        }

        var entity = await _privateHireRepository.GetContractorByIdAsync(contractorId);
        if (entity == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        var update = ToContractorRecord(request);
        update.contractor_id = entity.contractor_id;
        await _privateHireRepository.UpdateContractorAsync(update, GetCurrentUserId());
        return Ok(ToContractorDto(update));
    }

    [HttpDelete("contractors/{contractorId:int}")]
    public async Task<ActionResult> DeleteContractor(int contractorId)
    {
        var entity = await _privateHireRepository.GetContractorByIdAsync(contractorId);
        if (entity == null)
        {
            return NotFound(new { message = $"Contractor with ID {contractorId} not found" });
        }

        await _privateHireRepository.DeleteContractorAsync(contractorId, GetCurrentUserId());

        return NoContent();
    }

    private static PrivateHireContractorDto ToContractorDto(PrivateHireContractorRecord contractor)
    {
        return new PrivateHireContractorDto
        {
            contractor_id = contractor.contractor_id,
            company_name = contractor.contractor_name ?? string.Empty,
            contact_person = contractor.contact_person ?? string.Empty,
            phone = contractor.tel_number ?? string.Empty,
            email = contractor.email_address ?? string.Empty,
            business_registration = contractor.fax_number ?? string.Empty,
            address = contractor.physical_address ?? string.Empty,
            status = contractor.active is 0 ? "Inactive" : "Active",
            postal_address = contractor.postal_address ?? string.Empty,
            fax_number = contractor.fax_number ?? string.Empty,
            quotations = contractor.quotations,
            type = contractor.type ?? string.Empty,
            project_name = contractor.project_name ?? string.Empty,
            project_begdat = contractor.project_begdat,
            project_enddat = contractor.project_enddat,
        };
    }

    private static PrivateHireContractorRecord ToContractorRecord(
        PrivateHireContractorDto request
    ) =>
        new()
        {
            contractor_id = checked((short)request.contractor_id),
            contractor_name = request.company_name?.Trim(),
            physical_address = request.address?.Trim(),
            postal_address = request.postal_address?.Trim(),
            tel_number = request.phone?.Trim(),
            fax_number = string.IsNullOrWhiteSpace(request.fax_number)
                ? request.business_registration?.Trim()
                : request.fax_number.Trim(),
            email_address = request.email?.Trim(),
            contact_person = request.contact_person?.Trim(),
            active = string.Equals(request.status, "Inactive", StringComparison.OrdinalIgnoreCase)
                ? (short)0
                : (short)1,
            quotations = request.quotations,
            type = request.type?.Trim(),
            project_name = request.project_name?.Trim(),
            project_begdat = request.project_begdat,
            project_enddat = request.project_enddat,
        };
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
    public string postal_address { get; set; } = string.Empty;
    public string fax_number { get; set; } = string.Empty;
    public bool? quotations { get; set; }
    public string type { get; set; } = string.Empty;
    public string project_name { get; set; } = string.Empty;
    public DateTime? project_begdat { get; set; }
    public DateTime? project_enddat { get; set; }
}

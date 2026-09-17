using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Repositories;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
// Vehicle assessments are captured from the legacy Contracts workflow; do
// not expose this shared table endpoint to an authenticated user without a
// Contracts entitlement.
[ContractAccess]
[Route("api/[controller]")]
public class VehicleAssessmentController : BaseApiController
{
    private readonly IVehicleAssessmentRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<VehicleAssessmentController> _logger;

    public VehicleAssessmentController(
        IVehicleAssessmentRepository repository,
        IVehicleRepository vehicleRepository,
        IContractRepository contractRepository,
        ISiteRepository siteRepository,
        FisDbContext context,
        ILogger<VehicleAssessmentController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _contractRepository = contractRepository;
        _siteRepository = siteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VehicleAssessment>>> GetAll()
    {
        try
        {
            return Ok(await FilterByScopeAsync(await _repository.GetAllAsync()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VehicleAssessment>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item is null)
                return NotFound();
            return await IsAssessmentAllowedAsync(item) ? Ok(item) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<VehicleAssessment>> GetByVehicle(int vmfCode)
    {
        try
        {
            var item = await _repository.GetByVehicleAsync(vmfCode);
            if (item is null)
                return NotFound();
            return await IsAssessmentAllowedAsync(item) ? Ok(item) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpGet("recent/{days}")]
    public async Task<ActionResult<IEnumerable<VehicleAssessment>>> GetRecent(int days)
    {
        try
        {
            return Ok(await FilterByScopeAsync(await _repository.GetRecentAssessmentsAsync(days)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment read procedure is unavailable or incompatible.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<VehicleAssessment>> Create([FromBody] VehicleAssessment item)
    {
        try
        {
            if (item is null || item.vmf_code <= 0)
                return BadRequest(new { error = "A valid vehicle is required." });
            if (!await IsVehicleAllowedAsync(item.vmf_code))
                return Forbid();

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.vehicle_assessment_code },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is ArgumentException)
                return BadRequest(new { error = ex.Message });
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment procedure is unavailable or incompatible. No partial inspection mutation was written.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<VehicleAssessment>> Update(
        int id,
        [FromBody] VehicleAssessment item
    )
    {
        try
        {
            if (item is null || id != item.vehicle_assessment_code)
                return BadRequest();
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsAssessmentAllowedAsync(existing))
                return Forbid();
            // DEV_UPD_Vehicle_Assessment does not accept @vmf_code. Keep the
            // persisted vehicle association and reject attempts to move an
            // inspection to another vehicle through the API.
            if (item.vmf_code != 0 && item.vmf_code != existing.vmf_code)
                return BadRequest(new { error = "The vehicle for an existing assessment cannot be changed." });
            item.vmf_code = existing.vmf_code;
            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is ArgumentException)
                return BadRequest(new { error = ex.Message });
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy vehicle-assessment procedure is unavailable or incompatible. No partial inspection mutation was written.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsAssessmentAllowedAsync(existing))
                return Forbid();
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            if (ex is NotSupportedException or LegacyVehicleAssessmentWorkflowUnavailableException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "Legacy vehicle assessments cannot be deleted; the inspection record is retained for audit history.",
                        source = "legacy-procedure-required",
                    }
                );
            }
            return StatusCode(500);
        }
    }

    private async Task<IReadOnlyList<VehicleAssessment>> FilterByScopeAsync(
        IEnumerable<VehicleAssessment> assessments
    )
    {
        var values = assessments.ToList();
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return values;

        var allowedByVehicle = new Dictionary<int, bool>();
        var filtered = new List<VehicleAssessment>(values.Count);
        var currentUserId = GetCurrentUserId();
        foreach (var assessment in values)
        {
            if (!allowedByVehicle.TryGetValue(assessment.vmf_code, out var vehicleAllowed))
            {
                vehicleAllowed = await IsVehicleAllowedAsync(assessment.vmf_code, allowedSites);
                allowedByVehicle[assessment.vmf_code] = vehicleAllowed;
            }

            var allowed = vehicleAllowed
                || assessment.user_access_code == currentUserId;
            if (allowed)
                filtered.Add(assessment);
        }

        return filtered;
    }

    private async Task<bool> IsAssessmentAllowedAsync(
        VehicleAssessment assessment,
        IReadOnlySet<short>? allowedSites = null
    )
    {
        allowedSites ??= await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return true;

        if (await IsVehicleAllowedAsync(assessment.vmf_code, allowedSites))
            return true;

        // A user may continue to see an assessment attached to a historical
        // contract they captured, even after the vehicle has moved sites.
        var currentUserId = GetCurrentUserId();
        return (await _contractRepository.GetContractsByVehicleAsync(assessment.vmf_code))
            .Any(contract =>
                contract.user_code == currentUserId
                || contract.created_by_user_code == currentUserId
            );
    }

    private async Task<bool> IsVehicleAllowedAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSites = null
    )
    {
        allowedSites ??= await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return true;
        if (allowedSites.Count == 0)
            return false;

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle is null)
            return false;

        if (
            (vehicle.veh_site_code.HasValue && allowedSites.Contains(vehicle.veh_site_code.Value))
            || (vehicle.initial_site_code.HasValue && allowedSites.Contains(vehicle.initial_site_code.Value))
            || (vehicle.default_site.HasValue && allowedSites.Contains(vehicle.default_site.Value))
        )
        {
            return true;
        }

        var activeContract = await _contractRepository.GetActiveContractByVehicleAsync(vmfCode);
        return activeContract is not null && allowedSites.Contains(activeContract.site_code);
    }

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalContractScope())
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<short>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<short>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (HasRole("Vehicle List for All Departments in Province") && profileSite.province_code.HasValue)
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        else if (HasRole("Vehicle List for All Sites in Department") && profileSite.Depatrment_code.HasValue)
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        else
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private bool HasGlobalContractScope() =>
        HasRole("Admin")
        || HasRole("Administrator")
        || HasRole("System Administrator")
        || HasRole("SystemAdministrator");

    private bool HasRole(string expectedRole) => User.Claims
        .Where(claim =>
            claim.Type == ClaimTypes.Role
            || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
        )
        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
}

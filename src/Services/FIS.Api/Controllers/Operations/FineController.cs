using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FineController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const int MaximumSearchQueryLength = 8;

    private readonly IFineRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly ITrafficDeptRepository _trafficDeptRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<FineController> _logger;

    public FineController(
        IFineRepository repository,
        IVehicleRepository vehicleRepository,
        ISiteRepository siteRepository,
        ITrafficDeptRepository trafficDeptRepository,
        FisDbContext context,
        ILogger<FineController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _siteRepository = siteRepository;
        _trafficDeptRepository = trafficDeptRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Fine>>> GetAll()
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            return Ok(FilterByAllowedSites(await _repository.GetAllAsync(), allowedSites).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? searchType = "GP",
        [FromQuery] string? searchQuery = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();

        var normalizedSearchType = searchType?.Trim().ToUpperInvariant() switch
        {
            "GG" or "RADIOGG" => "GG",
            "GP" or "RADIOGP" => "GP",
            _ => string.Empty,
        };
        if (normalizedSearchType.Length == 0)
        {
            return BadRequest(new { error = "Search type must be GG or GP." });
        }

        var normalizedSearchQuery = searchQuery?.Trim() ?? string.Empty;
        if (normalizedSearchQuery.Length > MaximumSearchQueryLength)
        {
            return BadRequest(new { error = "Search query cannot exceed 8 characters." });
        }

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var result = await _repository.GetPageAsync(
                new FinePageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    normalizedSearchType,
                    normalizedSearchQuery,
                    allowedSites
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
            _logger.LogError(ex, "Error retrieving paged fine records");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Fine>> GetById(int id)
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item is null)
                return NotFound();
            if (!await IsSiteAllowedAsync(item.Site_code))
                return Forbid();
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("unpaid")]
    public async Task<ActionResult<IEnumerable<Fine>>> GetUnpaid()
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            return Ok(FilterByAllowedSites(await _repository.GetUnpaidFinesAsync(), allowedSites).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<Fine>> Create([FromBody] Fine item)
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            if (await ValidateMutationAsync(item) is { } validationError)
                return BadRequest(new { error = validationError });

            var created = await _repository.CreateAsync(item, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Fine_code }, created);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("legacy Fines audit-trigger", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy Fines mutation workflow is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy Fines audit workflow is unavailable. The fine was not saved." }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Fine>> Update(int id, [FromBody] Fine item)
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            if (id != item.Fine_code)
                return BadRequest();
            if (await ValidateMutationAsync(item) is { } validationError)
                return BadRequest(new { error = validationError });

            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsSiteAllowedAsync(existing.Site_code))
                return Forbid();

            return Ok(await _repository.UpdateAsync(item, GetCurrentUserId()));
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("legacy Fines audit-trigger", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy Fines mutation workflow is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy Fines audit workflow is unavailable. The fine was not saved." }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasFinesMaintenanceAccess())
            return Forbid();
        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsSiteAllowedAsync(existing.Site_code))
                return Forbid();

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("legacy Fines audit-trigger", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy Fines mutation workflow is unavailable");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy Fines audit workflow is unavailable. The fine was not deleted." }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    // Both legacy Fines menu pages are guarded by the Reports role. The main
    // navigation entry is separately labelled Fines, but the legacy page
    // itself—not the menu visibility—was the server-side authority.
    private bool HasFinesMaintenanceAccess() =>
        HasAnyRole("Reports", "SystemAdministrator", "System Administrator");

    /// <summary>
    /// The legacy capture page only allowed a fine to be submitted after a
    /// vehicle had been resolved and an active site had been selected. Keep
    /// those business references server-side as well; dropdowns are not an
    /// authorization or integrity boundary for direct API callers.
    /// </summary>
    private async Task<string?> ValidateMutationAsync(Fine item)
    {
        if (item is null)
            return "Fine data is required.";

        var lengthError = FirstLengthError(
            ("Offence reference", item.Offence_reference, 20),
            ("Offence issuer", item.Offence_issuer, 20),
            ("Offence name", item.Offence_name, 20),
            ("Department responsible person", item.Dept_person_name, 25),
            ("Department responsible person ID", item.Dept_person_id, 13),
            ("Document type", item.Document_type, 20)
        );
        if (lengthError is not null)
            return lengthError;

        if (item.vmf_code is not > 0)
            return "A valid vehicle is required.";

        if (!item.Offence_date.HasValue)
            return "The offence date is required.";
        if (item.Fine_amount is < 0)
            return "The fine amount cannot be negative.";

        if (item.Site_code is not > 0)
            return "A valid site is required.";

        var vehicle = await _vehicleRepository.GetByIdAsync(item.vmf_code.Value);
        if (vehicle is null)
            return "The selected vehicle does not exist.";

        var site = await _siteRepository.GetByIdAsync(item.Site_code.Value);
        if (site is null)
            return "The selected site does not exist.";
        if (!site.site_active)
            return "The selected site is not active.";
        if (!await IsSiteAllowedAsync(site.Site_code))
            return "You do not have permission to maintain fines for the selected site.";

        if (item.Traffic_dept_code is > 0)
        {
            var trafficDept = await _trafficDeptRepository.GetByIdAsync(
                item.Traffic_dept_code.Value
            );
            if (trafficDept is null)
                return "The selected traffic department does not exist.";
        }

        return null;
    }

    private static string? FirstLengthError(
        params (string Name, string? Value, int Maximum)[] values
    )
    {
        foreach (var (name, value, maximum) in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maximum)
                return $"{name} cannot exceed {maximum} characters.";
        }

        return null;
    }

    private async Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalFinesScope())
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
        if (
            HasAnyRole("Vehicle List for All Departments in Province")
            && profileSite.province_code.HasValue
        )
        {
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        }
        else if (
            HasAnyRole("Vehicle List for All Sites in Department")
            && profileSite.Depatrment_code.HasValue
        )
        {
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        }
        else
        {
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);
        }

        return sites.Select(site => site.Site_code).ToHashSet();
    }

    private async Task<bool> IsSiteAllowedAsync(short? siteCode)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        return allowedSites is null || (siteCode.HasValue && allowedSites.Contains(siteCode.Value));
    }

    private static IEnumerable<Fine> FilterByAllowedSites(
        IEnumerable<Fine> fines,
        IReadOnlySet<short>? allowedSites
    ) => allowedSites is null
        ? fines
        : fines.Where(fine => fine.Site_code.HasValue && allowedSites.Contains(fine.Site_code.Value));

    private bool HasGlobalFinesScope() =>
        HasAnyRole("Admin", "Administrator", "SystemAdministrator", "System Administrator");

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }
}

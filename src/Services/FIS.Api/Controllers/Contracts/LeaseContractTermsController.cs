using System.Security.Claims;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LeaseContractTermsController : BaseApiController
{
    private readonly ILeaseContractTermsRepository _repository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly ILogger<LeaseContractTermsController> _logger;

    public LeaseContractTermsController(
        ILeaseContractTermsRepository repository,
        IVehicleRepository vehicleRepository,
        LegacyVehicleScopeService vehicleScope,
        ILogger<LeaseContractTermsController> logger
    )
    {
        _repository = repository;
        _vehicleRepository = vehicleRepository;
        _vehicleScope = vehicleScope;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaseContractTerms>>> GetAll()
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            return Ok(await FilterByScopeAsync(await _repository.GetAllAsync()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null,
        [FromQuery] string? mode = null,
        [FromQuery] string? status = null
    )
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var result = await _repository.GetPageAsync(
                new LeaseContractTermsPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    search,
                    mode,
                    status,
                    allowedSites,
                    GetCurrentUserId()
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
            _logger.LogError(ex, "Error retrieving paged lease contract terms");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LeaseContractTerms>> GetById(int id)
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            var item = await _repository.GetByIdAsync(id);
            if (item is null)
                return NotFound();
            return await IsTermAllowedAsync(item) ? Ok(item) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<LeaseContractTerms>> GetByVehicle(int vmfCode)
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            var item = await _repository.GetByVehicleAsync(vmfCode);
            if (item is null)
                return NotFound();
            return await IsTermAllowedAsync(item) ? Ok(item) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<LeaseContractTerms>>> GetActive()
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            return Ok(await FilterByScopeAsync(await _repository.GetActiveTermsAsync()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<LeaseContractTerms>> Create([FromBody] LeaseContractTerms item)
    {
        if (!HasLeaseVehicleCapturerRole())
            return Forbid();

        try
        {
            if (!await IsNewTermAllowedAsync(item))
                return Forbid();
            var created = await _repository.CaptureOrResubmitAsync(
                item,
                GetLegacyUsername(),
                GetCurrentUserId()
            );
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.VehicleContractTermID },
                created
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<LeaseContractTerms>> Update(
        int id,
        [FromBody] LeaseContractTerms item
    )
    {
        try
        {
            if (id != item.VehicleContractTermID)
                return BadRequest();

            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsTermAllowedAsync(existing) || !await IsRequestedSiteAllowedAsync(item))
                return Forbid();

            var username = GetLegacyUsername();
            var currentUserId = GetCurrentUserId();
            if (item.AuthorityStatus == 2)
            {
                if (!HasLeaseVehicleAuthorizerRole())
                    return Forbid();
                if (!IsAwaitingAuthorisation(existing))
                    return Conflict(new { error = "Only lease terms awaiting authorisation can be authorised." });

                var capturedBy = await _repository.GetCapturedByUsernameAsync(existing.vmf_Code);
                if (string.IsNullOrWhiteSpace(capturedBy))
                    return Conflict(new { error = "The original lease-term capturer could not be resolved; authorisation is blocked until the legacy user record is available." });
                if (existing.CreatedBy == currentUserId
                    || string.Equals(capturedBy, username, StringComparison.OrdinalIgnoreCase))
                    return Conflict(
                        new { error = "You cannot authorise lease contract terms that you captured yourself." }
                    );

                var comment = item.authority_comment?.Trim();
                if (string.IsNullOrWhiteSpace(comment))
                    return BadRequest(new { error = "An authorisation comment is required." });
                return Ok(
                    await _repository.AuthorizeAsync(
                        existing,
                        comment,
                        username,
                        currentUserId
                    )
                );
            }

            if (item.AuthorityStatus == 4)
            {
                if (!HasLeaseVehicleAuthorizerRole())
                    return Forbid();
                if (!IsAwaitingAuthorisation(existing))
                    return Conflict(new { error = "Only lease terms awaiting authorisation can be rejected." });

                var capturedBy = await _repository.GetCapturedByUsernameAsync(existing.vmf_Code);
                if (string.IsNullOrWhiteSpace(capturedBy))
                    return Conflict(new { error = "The original lease-term capturer could not be resolved; rejection is blocked until the legacy user record is available." });
                if (existing.CreatedBy == currentUserId
                    || string.Equals(capturedBy, username, StringComparison.OrdinalIgnoreCase))
                    return Conflict(
                        new { error = "You cannot reject lease contract terms that you captured yourself." }
                    );

                var comment = item.rejection_reason?.Trim() ?? item.authority_comment?.Trim();
                if (string.IsNullOrWhiteSpace(comment))
                    return BadRequest(new { error = "A rejection comment is required." });
                return Ok(
                    await _repository.RejectAsync(
                        existing,
                        comment,
                        username,
                        currentUserId
                    )
                );
            }

            if (!HasLeaseVehicleCapturerRole())
            {
                return Forbid();
            }
            if (existing.AuthorityStatus == 2 || existing.Rejected == 4)
                return Conflict(new { error = "Authorised lease terms are immutable; use the legacy recall workflow before resubmitting." });

            return Ok(
                await _repository.CaptureOrResubmitAsync(
                    item,
                    username,
                    currentUserId
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error");
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        if (!HasLeaseVehicleCapturerRole())
            return Forbid();

        // The legacy lease-contract-term workflow has capture, authorisation,
        // rejection, and recall actions, but no user-facing delete operation.
        return Conflict(
            new { error = "Deleting lease contract terms is not available in the legacy FML workflow." }
        );
    }

    [HttpPost("{id}/recall")]
    public async Task<ActionResult<LeaseContractTerms>> Recall(int id)
    {
        if (!HasLeaseVehicleCapturerRole() && !HasLeaseVehicleAuthorizerRole())
            return Forbid();

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();
            if (!await IsTermAllowedAsync(existing))
                return NotFound();

            return Ok(
                await _repository.RecallAsync(existing, GetLegacyUsername(), GetCurrentUserId())
            );
        }
        catch (UnauthorizedAccessException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (NotSupportedException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalling lease contract term {TermId}", id);
            return StatusCode(500);
        }
    }

    private bool HasLeaseVehiclePendingRole() => HasRole("Lease Vehicle Pending");

    private bool HasLeaseVehicleCapturerRole() => HasRole("Lease Vehicle Capturer");

    private bool HasLeaseVehicleAuthorizerRole() => HasRole("Lease Vehicle Authorizer");

    private Task<IReadOnlySet<short>?> ResolveAllowedSiteCodesAsync() =>
        _vehicleScope.ResolveAllowedSiteCodesAsync(User, HttpContext.RequestAborted);

    private async Task<bool> IsNewTermAllowedAsync(LeaseContractTerms terms)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return true;
        if (terms.lease_site_code is > 0 && allowedSites.Contains(terms.lease_site_code.Value))
            return true;
        return await _vehicleRepository.GetByIdAsync(
                terms.vmf_Code,
                allowedSites,
                GetCurrentUserId()
            )
            is not null;
    }

    private async Task<bool> IsRequestedSiteAllowedAsync(LeaseContractTerms terms)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        return allowedSites is null
            || terms.lease_site_code is not > 0
            || allowedSites.Contains(terms.lease_site_code.Value);
    }

    private async Task<bool> IsTermAllowedAsync(LeaseContractTerms terms)
    {
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return true;
        if (terms.lease_site_code is > 0 && allowedSites.Contains(terms.lease_site_code.Value))
            return true;
        if (terms.CreatedBy == GetCurrentUserId() || terms.created_by_user_code == GetCurrentUserId())
            return true;
        return await _vehicleRepository.GetByIdAsync(
                terms.vmf_Code,
                allowedSites,
                GetCurrentUserId()
            )
            is not null;
    }

    private async Task<IReadOnlyList<LeaseContractTerms>> FilterByScopeAsync(
        IEnumerable<LeaseContractTerms> terms
    )
    {
        var values = terms.ToList();
        var allowedSites = await ResolveAllowedSiteCodesAsync();
        if (allowedSites is null)
            return values;

        var filtered = new List<LeaseContractTerms>(values.Count);
        foreach (var term in values)
        {
            if (await IsTermAllowedAsync(term))
                filtered.Add(term);
        }
        return filtered;
    }

    private static bool IsAwaitingAuthorisation(LeaseContractTerms terms) =>
        terms.AuthorityStatus == 1 && (terms.Rejected is null or 3);

    private string GetLegacyUsername()
    {
        var username = User.FindFirst("legacy_username")?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException(
                "Authenticated user does not include the legacy username required by the FML workflow."
            );
        return username;
    }

    private bool HasRole(string expectedRole)
    {
        if (User.IsInRole(expectedRole))
            return true;

        return User.Claims
            .Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            )
            .Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase));
    }
}

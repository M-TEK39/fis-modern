using System.Security.Claims;
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
    private readonly ILogger<LeaseContractTermsController> _logger;

    public LeaseContractTermsController(
        ILeaseContractTermsRepository repository,
        ILogger<LeaseContractTermsController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaseContractTerms>>> GetAll()
    {
        if (!HasLeaseVehiclePendingRole())
            return Forbid();

        try
        {
            return Ok(await _repository.GetAllAsync());
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
            var result = await _repository.GetPageAsync(
                new LeaseContractTermsPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    search,
                    mode,
                    status
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
            return item == null ? NotFound() : Ok(item);
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
            return item == null ? NotFound() : Ok(item);
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
            return Ok(await _repository.GetActiveTermsAsync());
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
            var created = await _repository.CaptureOrResubmitAsync(item, GetLegacyUsername());
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

            var username = GetLegacyUsername();
            if (item.AuthorityStatus == 2)
            {
                if (!HasLeaseVehicleAuthorizerRole())
                    return Forbid();

                var capturedBy = await _repository.GetCapturedByUsernameAsync(existing.vmf_Code);
                if (string.Equals(capturedBy, username, StringComparison.OrdinalIgnoreCase))
                    return Conflict(
                        new { error = "You cannot authorise lease contract terms that you captured yourself." }
                    );

                var comment = item.authority_comment?.Trim();
                if (string.IsNullOrWhiteSpace(comment))
                    return BadRequest(new { error = "An authorisation comment is required." });
                return Ok(await _repository.AuthorizeAsync(existing, comment, username));
            }

            if (item.AuthorityStatus == 4)
            {
                if (!HasLeaseVehicleAuthorizerRole())
                    return Forbid();

                var capturedBy = await _repository.GetCapturedByUsernameAsync(existing.vmf_Code);
                if (string.Equals(capturedBy, username, StringComparison.OrdinalIgnoreCase))
                    return Conflict(
                        new { error = "You cannot reject lease contract terms that you captured yourself." }
                    );

                var comment = item.rejection_reason?.Trim() ?? item.authority_comment?.Trim();
                if (string.IsNullOrWhiteSpace(comment))
                    return BadRequest(new { error = "A rejection comment is required." });
                return Ok(await _repository.RejectAsync(existing, comment, username));
            }

            if (!HasLeaseVehicleCapturerRole())
            {
                return Forbid();
            }

            return Ok(await _repository.CaptureOrResubmitAsync(item, username));
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
        if (!HasLeaseVehicleCapturerRole())
            return Forbid();

        try
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound();

            return Ok(await _repository.RecallAsync(existing));
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

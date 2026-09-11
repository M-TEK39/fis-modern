using System.Security.Claims;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.WorkshopEntities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workshop/merchants")]
public sealed class WorkshopMerchantController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private readonly IWorkshopMerchantRepository _repository;
    private readonly ILogger<WorkshopMerchantController> _logger;

    public WorkshopMerchantController(
        IWorkshopMerchantRepository repository,
        ILogger<WorkshopMerchantController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkshopMerchantDto>>> GetAll()
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            return Ok((await _repository.GetAllAsync()).Select(Map));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workshop merchants");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<ActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            var result = await _repository.GetPageAsync(
                new WorkshopMerchantPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, MaximumPageSize),
                    search
                )
            );
            return Ok(
                new
                {
                    items = result.Items.Select(Map),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged workshop merchants");
            return StatusCode(500);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkshopMerchantDto>> GetById(int id)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            var merchant = await _repository.GetByIdAsync(id);
            return merchant is null ? NotFound() : Ok(Map(merchant));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workshop merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<WorkshopMerchantDto>> Create(
        [FromBody] WorkshopMerchantRequest request
    )
    {
        if (!HasWorkshopRole())
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Merchant name is required." });
        try
        {
            var merchant = await _repository.CreateAsync(ToEntity(request), GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = merchant.wwmerch_code },
                Map(merchant)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workshop merchant");
            return StatusCode(500);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<WorkshopMerchantDto>> Update(
        int id,
        [FromBody] WorkshopMerchantRequest request
    )
    {
        if (!HasWorkshopRole())
            return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Merchant name is required." });
        try
        {
            var merchant = ToEntity(request);
            merchant.wwmerch_code = id;
            return Ok(Map(await _repository.UpdateAsync(merchant, GetCurrentUserId())));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workshop merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasWorkshopRole())
            return Forbid();
        try
        {
            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workshop merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    private static WwMerchant ToEntity(WorkshopMerchantRequest request) =>
        new()
        {
            wwmerch_name = request.Name?.Trim(),
            wwmerch_tel = request.Tel?.Trim(),
            wwmerch_fax = request.Fax?.Trim(),
            wwmerch_email = request.Email?.Trim(),
        };

    private static WorkshopMerchantDto Map(WwMerchant merchant) =>
        new(
            merchant.wwmerch_code,
            merchant.wwmerch_name,
            merchant.wwmerch_tel,
            merchant.wwmerch_fax,
            merchant.wwmerch_email
        );

    private bool HasWorkshopRole()
    {
        if (User.IsInRole("Workshop"))
            return true;
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
            string.Equals(role, "Workshop", StringComparison.OrdinalIgnoreCase)
        );
    }
}

public sealed record WorkshopMerchantDto(
    int MerchantCode,
    string? Name,
    string? Tel,
    string? Fax,
    string? Email
);

public sealed class WorkshopMerchantRequest
{
    public string? Name { get; set; }
    public string? Tel { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
}

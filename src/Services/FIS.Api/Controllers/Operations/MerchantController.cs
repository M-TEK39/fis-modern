using System.Security.Claims;
using FIS.Api.Services;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MerchantController : BaseApiController
{
    private readonly IMerchantRepository _repository;
    private readonly MerchantCompatibilityService _compatibilityService;
    private readonly ILogger<MerchantController> _logger;

    public MerchantController(
        IMerchantRepository repository,
        MerchantCompatibilityService compatibilityService,
        ILogger<MerchantController> logger
    )
    {
        _repository = repository;
        _compatibilityService = compatibilityService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MerchantDto>>> GetAll()
    {
        if (!HasMerchantReadRole())
            return Forbid();
        try
        {
            var merchants = await _repository.GetAllAsync();
            return Ok(merchants.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving merchants");
            return StatusCode(500);
        }
    }

    [HttpGet("page")]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!HasMerchantReadRole())
            return Forbid();

        try
        {
            var result = await _compatibilityService.GetPageAsync(
                search,
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100),
                HttpContext.RequestAborted
            );

            return Ok(
                new
                {
                    items = result.Items.Select(item => new MerchantDto
                    {
                        Merchant_code = item.MerchantCode,
                        Merchant_Name = item.MerchantName,
                    }),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged merchants");
            return StatusCode(500);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MerchantDto>> GetById(int id)
    {
        if (!HasMerchantReadRole())
            return Forbid();
        try
        {
            var merchant = await _repository.GetByIdAsync(id);
            return merchant == null ? NotFound() : Ok(MapToDto(merchant));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<MerchantDto>> Create([FromBody] MerchantCreateDto dto)
    {
        if (!HasMerchantWriteRole())
            return Forbid();
        try
        {
            var merchant = new MerchantReference { Merchant_name = dto.Merchant_Name };

            var created = await _repository.CreateAsync(merchant, GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Merchant_code },
                MapToDto(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating merchant");
            return StatusCode(500);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MerchantDto>> Update(int id, [FromBody] MerchantCreateDto dto)
    {
        if (!HasMerchantWriteRole())
            return Forbid();
        try
        {
            var merchant = new MerchantReference
            {
                Merchant_code = id,
                Merchant_name = dto.Merchant_Name,
            };

            var updated = await _repository.UpdateAsync(merchant, GetCurrentUserId());
            return Ok(MapToDto(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        if (!HasMerchantWriteRole())
            return Forbid();
        try
        {
            var merchant = await _repository.GetByIdAsync(id);
            if (merchant == null)
            {
                return NotFound();
            }

            var clearanceCount = await _repository.CountClearancesAsync(id);
            if (clearanceCount > 0)
            {
                return Conflict(
                    new MerchantDeleteCheckDto
                    {
                        MerchantCode = id,
                        MerchantName = merchant.Merchant_name,
                        ClearanceCount = clearanceCount,
                        CanDelete = false,
                    }
                );
            }

            await _repository.DeleteAsync(id, GetCurrentUserId());
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting merchant {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    [HttpGet("{id}/delete-check")]
    public async Task<ActionResult<MerchantDeleteCheckDto>> GetDeleteCheck(int id)
    {
        if (!HasMerchantWriteRole())
            return Forbid();

        try
        {
            var merchant = await _repository.GetByIdAsync(id);
            if (merchant == null)
            {
                return NotFound();
            }

            var clearanceCount = await _repository.CountClearancesAsync(id);
            return Ok(
                new MerchantDeleteCheckDto
                {
                    MerchantCode = id,
                    MerchantName = merchant.Merchant_name,
                    ClearanceCount = clearanceCount,
                    CanDelete = clearanceCount == 0,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking merchant deletion {MerchantCode}", id);
            return StatusCode(500);
        }
    }

    private static MerchantDto MapToDto(MerchantReference merchant)
    {
        return new MerchantDto
        {
            Merchant_code = merchant.Merchant_code,
            Merchant_Name = merchant.Merchant_name,
        };
    }

    private bool HasMerchantReadRole() => HasAnyRole("Clearance", "Workshop", "Reports");

    private bool HasMerchantWriteRole() => HasAnyRole("Clearance", "Workshop");

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

public class MerchantDto
{
    public int Merchant_code { get; set; }
    public string? Merchant_Name { get; set; }
}

public class MerchantCreateDto
{
    public string? Merchant_Name { get; set; }
}

public class MerchantDeleteCheckDto
{
    public int MerchantCode { get; set; }
    public string? MerchantName { get; set; }
    public int ClearanceCount { get; set; }
    public bool CanDelete { get; set; }
}

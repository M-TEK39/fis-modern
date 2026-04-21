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
    private readonly ILogger<MerchantController> _logger;

    public MerchantController(IMerchantRepository repository, ILogger<MerchantController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MerchantDto>>> GetAll()
    {
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

    [HttpGet("{id}")]
    public async Task<ActionResult<MerchantDto>> GetById(int id)
    {
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
        try
        {
            var merchant = new MerchantReference
            {
                Merchant_name = dto.Merchant_Name
            };

            var created = await _repository.CreateAsync(merchant, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.Merchant_code }, MapToDto(created));
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
        try
        {
            var merchant = new MerchantReference
            {
                Merchant_code = id,
                Merchant_name = dto.Merchant_Name
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
        try
        {
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

    private static MerchantDto MapToDto(MerchantReference merchant)
    {
        return new MerchantDto
        {
            Merchant_code = merchant.Merchant_code,
            Merchant_Name = merchant.Merchant_name
        };
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

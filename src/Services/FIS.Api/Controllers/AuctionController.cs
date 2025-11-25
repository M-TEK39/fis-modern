using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionController : ControllerBase
{
    private readonly IAuctionRepository _repository;
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(IAuctionRepository repository, ILogger<AuctionController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Auction>>> GetAll()
    {
        try
        {
            var items = await _repository.GetAllAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auctions");
            return StatusCode(500, "Error retrieving auctions");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Auction>> GetById(short id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auction {Id}", id);
            return StatusCode(500, "Error retrieving auction");
        }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Auction>>> GetByVehicle(int vmfCode)
    {
        try
        {
            var items = await _repository.GetByVehicleAsync(vmfCode);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auctions for vehicle {VmfCode}", vmfCode);
            return StatusCode(500, "Error retrieving auctions");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Auction>> Create([FromBody] Auction item)
    {
        try
        {
            var created = await _repository.CreateAsync(item);
            return CreatedAtAction(nameof(GetById), new { id = created.auction_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auction");
            return StatusCode(500, "Error creating auction");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Auction>> Update(short id, [FromBody] Auction item)
    {
        try
        {
            if (id != item.auction_code)
                return BadRequest("ID mismatch");

            var updated = await _repository.UpdateAsync(item);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction {Id}", id);
            return StatusCode(500, "Error updating auction");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try
        {
            await _repository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting auction {Id}", id);
            return StatusCode(500, "Error deleting auction");
        }
    }
}

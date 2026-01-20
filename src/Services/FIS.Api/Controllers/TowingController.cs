using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TowingController : ControllerBase
{
    private readonly ITowingRepository _repository;
    private readonly ILogger<TowingController> _logger;

    public TowingController(ITowingRepository repository, ILogger<TowingController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Towing>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Towing>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Towing>> Create([FromBody] Towing item)
    {
        try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.Towing_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Towing>> Update(short id, [FromBody] Towing item)
    {
        try { if (id != item.Towing_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }
}

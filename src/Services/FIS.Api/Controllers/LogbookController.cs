using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogbookController : ControllerBase
{
    private readonly ILogbookRepository _repository;
    private readonly ILogger<LogbookController> _logger;

    public LogbookController(ILogbookRepository repository, ILogger<LogbookController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Logbook>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Logbook>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Logbook>> Create([FromBody] Logbook item)
    {
        try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.logbookcode }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Logbook>> Update(short id, [FromBody] Logbook item)
    {
        try { if (id != item.logbookcode) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }
}

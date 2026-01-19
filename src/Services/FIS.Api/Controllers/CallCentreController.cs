using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CallCentreController : ControllerBase
{
    private readonly ICallCentreRepository _repository;
    private readonly ILogger<CallCentreController> _logger;

    public CallCentreController(ICallCentreRepository repository, ILogger<CallCentreController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CallCentre>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CallCentre>> GetById(short id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<CallCentre>>> GetByVehicle(int vmfCode)
    {
        try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpPost]
    public async Task<ActionResult<CallCentre>> Create([FromBody] CallCentre item)
    {
        try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.Call_centre_code }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CallCentre>> Update(short id, [FromBody] CallCentre item)
    {
        try { if (id != item.Call_centre_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id)
    {
        try { await _repository.DeleteAsync(id); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500, "Error"); }
    }
}

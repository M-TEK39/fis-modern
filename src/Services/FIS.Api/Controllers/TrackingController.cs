using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly ITrackingRepository _repository;
    private readonly ILogger<TrackingController> _logger;
    public TrackingController(ITrackingRepository repository, ILogger<TrackingController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tracking>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetByVehicle(int vmfCode) { try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Tracking>>> GetActive() { try { return Ok(await _repository.GetActiveTrackingAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<Tracking>> Create([FromBody] Tracking item) { try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.track_code }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<Tracking>> Update(short id, [FromBody] Tracking item) { try { if (id != item.track_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }
}

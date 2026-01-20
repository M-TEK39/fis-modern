using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TrafficDeptController : ControllerBase
{
    private readonly ITrafficDeptRepository _repository;
    private readonly ILogger<TrafficDeptController> _logger;
    public TrafficDeptController(ITrafficDeptRepository repository, ILogger<TrafficDeptController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrafficDept>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<TrafficDept>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("name/{name}")]
    public async Task<ActionResult<TrafficDept>> GetByName(string name) { try { var item = await _repository.GetByNameAsync(name); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<TrafficDept>> Create([FromBody] TrafficDept item) { try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.Traffic_dept_code }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<TrafficDept>> Update(short id, [FromBody] TrafficDept item) { try { if (id != item.Traffic_dept_code) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }
}

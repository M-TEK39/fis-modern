using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupplierController : ControllerBase
{
    private readonly ISupplierRepository _repository;
    private readonly ILogger<SupplierController> _logger;
    public SupplierController(ISupplierRepository repository, ILogger<SupplierController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<Supplier>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetActive() { try { return Ok(await _repository.GetActiveAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<Supplier>>> GetByType(string type) { try { return Ok(await _repository.GetByTypeAsync(type)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<Supplier>> Create([FromBody] Supplier item) { try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.supplier_id }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<Supplier>> Update(short id, [FromBody] Supplier item) { try { if (id != item.supplier_id) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }
}

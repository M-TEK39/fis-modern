using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LeaseContractTermsController : ControllerBase
{
    private readonly ILeaseContractTermsRepository _repository;
    private readonly ILogger<LeaseContractTermsController> _logger;
    public LeaseContractTermsController(ILeaseContractTermsRepository repository, ILogger<LeaseContractTermsController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaseContractTerms>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<LeaseContractTerms>> GetById(int id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<LeaseContractTerms>> GetByVehicle(int vmfCode) { try { var item = await _repository.GetByVehicleAsync(vmfCode); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<LeaseContractTerms>>> GetActive() { try { return Ok(await _repository.GetActiveTermsAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<LeaseContractTerms>> Create([FromBody] LeaseContractTerms item) { try { var created = await _repository.CreateAsync(item); return CreatedAtAction(nameof(GetById), new { id = created.VehicleContractTermID }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<LeaseContractTerms>> Update(int id, [FromBody] LeaseContractTerms item) { try { if (id != item.VehicleContractTermID) return BadRequest(); return Ok(await _repository.UpdateAsync(item)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id) { try { await _repository.DeleteAsync(id); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }
}

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TaxiController : BaseApiController
{
    private readonly ITaxiRepository _repository;
    private readonly ITaxiWhiteLogRepository _whiteLogRepository;
    private readonly ILogger<TaxiController> _logger;

    public TaxiController(
        ITaxiRepository repository,
        ITaxiWhiteLogRepository whiteLogRepository,
        ILogger<TaxiController> logger)
    {
        _repository = repository;
        _whiteLogRepository = whiteLogRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Taxi>>> GetAll()
    {
        try { return Ok(await _repository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Taxi>> GetById(int id)
    {
        try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPost]
    public async Task<ActionResult<Taxi>> Create([FromBody] Taxi item)
    {
        try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.request_id }, created); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Taxi>> Update(int id, [FromBody] Taxi item)
    {
        try { if (id != item.request_id) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    // --- White Log endpoints ---

    [HttpPost("white-log")]
    public async Task<ActionResult> CreateWhiteLog([FromBody] CreateWhiteLogRequest request)
    {
        try
        {
            if (request.end_odo <= request.start_odo)
                return BadRequest("End odometer must be greater than start odometer.");

            if (request.end_date < request.start_date)
                return BadRequest("End date must be on or after start date.");

            var log = new TaxiWhiteLog
            {
                vmf_code = request.vmf_code,
                start_odo = request.start_odo,
                end_odo = request.end_odo,
                start_date = request.start_date,
                end_date = request.end_date,
                driver = request.driver,
                user_access_code = (short)GetCurrentUserId()
            };

            var created = await _whiteLogRepository.CreateAsync(log, GetCurrentUserId());
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating white log");
            return StatusCode(500);
        }
    }

    [HttpGet("white-log")]
    public async Task<ActionResult<IEnumerable<TaxiWhiteLog>>> GetAllWhiteLogs()
    {
        try { return Ok(await _whiteLogRepository.GetAllAsync()); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }

    [HttpGet("white-log/vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<TaxiWhiteLog>>> GetWhiteLogsByVehicle(int vmfCode)
    {
        try { return Ok(await _whiteLogRepository.GetByVehicleAsync(vmfCode)); }
        catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); }
    }
}

public class CreateWhiteLogRequest
{
    public int vmf_code { get; set; }
    public long start_odo { get; set; }
    public long end_odo { get; set; }
    public DateTime start_date { get; set; }
    public DateTime end_date { get; set; }
    public string? driver { get; set; }
}

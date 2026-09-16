using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Fuelcards")]
public class PrivateHireFuelCardController : BaseApiController
{
    private readonly IPrivateHireFuelCardRepository _repository;
    private readonly ILogger<PrivateHireFuelCardController> _logger;

    public PrivateHireFuelCardController(
        IPrivateHireFuelCardRepository repository,
        ILogger<PrivateHireFuelCardController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrivateHireFuelCard>>> GetAll()
    {
        try
        {
            return Ok(await _repository.GetActiveFuelCardsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hire fuel cards");
            return StatusCode(500, "Error retrieving private hire fuel cards");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PrivateHireFuelCard>> GetById(int id)
    {
        try
        {
            var item = await _repository.GetByIdAsync(id);
            return item == null ? NotFound() : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving private hire fuel card {Id}", id);
            return StatusCode(500, "Error retrieving private hire fuel card");
        }
    }

    [HttpGet("privatehire/{privateHireCode}")]
    public async Task<ActionResult<IEnumerable<PrivateHireFuelCard>>> GetByPrivateHireCode(
        int privateHireCode
    )
    {
        try
        {
            var items = await _repository.GetByPrivateHireCodeAsync(privateHireCode);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving private hire fuel cards for PHV {PrivateHireCode}",
                privateHireCode
            );
            return StatusCode(500, "Error retrieving private hire fuel cards");
        }
    }

    [HttpGet("registration/{registrationNumber}")]
    public async Task<ActionResult<IEnumerable<PrivateHireFuelCard>>> GetByRegistrationNumber(
        string registrationNumber
    )
    {
        try
        {
            var items = await _repository.GetByRegistrationNumberAsync(registrationNumber);
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving private hire fuel cards for registration {RegistrationNumber}",
                registrationNumber
            );
            return StatusCode(500, "Error retrieving private hire fuel cards");
        }
    }

    [HttpPost("delete/search")]
    public async Task<ActionResult<PrivateHireFuelCardDeleteSearchResultDto>> SearchForDelete(
        [FromBody] PrivateHireFuelCardDeleteSearchDto request
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CardNumber))
            {
                return BadRequest("Card number is required.");
            }

            var card = await _repository.GetByCardNumberAsync(request.CardNumber.Trim());
            return Ok(
                new PrivateHireFuelCardDeleteSearchResultDto
                {
                    Found = card != null,
                    Message =
                        card == null
                            ? "Private hire fuel card not found"
                            : "Private hire fuel card found and ready for deletion",
                    FuelCardCode = card?.PHFuel_card_code,
                    CardNumber = card?.card_number,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error searching private hire fuel card for deletion with card number {CardNumber}",
                request.CardNumber
            );
            return StatusCode(500, "Error searching private hire fuel card");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting private hire fuel card {Id}", id);
            return StatusCode(500, "Error deleting private hire fuel card");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PrivateHireFuelCard>> Create(
        [FromBody] CreatePrivateHireFuelCardRequest request
    )
    {
        try
        {
            if (request.Counter is < 0)
            {
                return BadRequest("Counter must be zero or greater.");
            }

            if (string.IsNullOrWhiteSpace(request.RegistrationNumber))
            {
                return BadRequest("Registration number is required.");
            }

            var registration = request.RegistrationNumber.Trim();
            var privateHireCode = await _repository.GetPrivateHireCodeByRegistrationAsync(
                registration
            );
            if (!privateHireCode.HasValue)
            {
                return NotFound(
                    $"Private hire vehicle not found for registration '{registration}'."
                );
            }

            if (!string.IsNullOrWhiteSpace(request.CardNumber))
            {
                var existingCard = await _repository.GetByCardNumberAsync(
                    request.CardNumber.Trim()
                );
                if (existingCard != null)
                {
                    return Conflict($"Card number '{request.CardNumber.Trim()}' already exists.");
                }
            }

            var entity = new PrivateHireFuelCard
            {
                phv_code = privateHireCode.Value,
                Counter = request.Counter,
                card_number = string.IsNullOrWhiteSpace(request.CardNumber)
                    ? null
                    : request.CardNumber.Trim(),
                PAN_number = string.IsNullOrWhiteSpace(request.PanNumber)
                    ? null
                    : request.PanNumber.Trim(),
            };

            var created = await _repository.CreateAsync(entity, GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = created.PHFuel_card_code }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating private hire fuel card for registration {RegistrationNumber}",
                request.RegistrationNumber
            );
            return StatusCode(500, "Error creating private hire fuel card");
        }
    }
}

public sealed class PrivateHireFuelCardDeleteSearchDto
{
    public string CardNumber { get; set; } = string.Empty;
}

public sealed class PrivateHireFuelCardDeleteSearchResultDto
{
    public bool Found { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? FuelCardCode { get; set; }
    public string? CardNumber { get; set; }
}

public sealed class CreatePrivateHireFuelCardRequest
{
    public string RegistrationNumber { get; set; } = string.Empty;
    public short? Counter { get; set; }
    public string? CardNumber { get; set; }
    public string? PanNumber { get; set; }
}

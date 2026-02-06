using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingController : BaseApiController
{
    private readonly IBookingRepository _repository;
    private readonly ILogger<BookingController> _logger;
    public BookingController(IBookingRepository repository, ILogger<BookingController> logger) { _repository = repository; _logger = logger; }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Booking>>> GetAll() { try { return Ok(await _repository.GetAllAsync()); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("{id}")]
    public async Task<ActionResult<Booking>> GetById(short id) { try { var item = await _repository.GetByIdAsync(id); return item == null ? NotFound() : Ok(item); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("daterange")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetByDateRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate) { try { return Ok(await _repository.GetByDateRangeAsync(startDate, endDate)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetByStatus(string status) { try { return Ok(await _repository.GetByStatusAsync(status)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpGet("vehicle/{vmfCode}")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetByVehicle(int vmfCode) { try { return Ok(await _repository.GetByVehicleAsync(vmfCode)); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPost]
    public async Task<ActionResult<Booking>> Create([FromBody] Booking item) { try { var created = await _repository.CreateAsync(item, GetCurrentUserId()); return CreatedAtAction(nameof(GetById), new { id = created.booking_id }, created); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpPut("{id}")]
    public async Task<ActionResult<Booking>> Update(short id, [FromBody] Booking item) { try { if (id != item.booking_id) return BadRequest(); return Ok(await _repository.UpdateAsync(item, GetCurrentUserId())); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(short id) { try { await _repository.DeleteAsync(id, GetCurrentUserId()); return NoContent(); } catch (Exception ex) { _logger.LogError(ex, "Error"); return StatusCode(500); } }

    #region Specialized Operations

    /// <summary>
    /// Get booking notifications
    /// </summary>
    [HttpGet("notifications")]
    public ActionResult<BookingNotificationsDto> GetNotifications()
    {
        // TODO: Implement booking notifications retrieval
        _logger.LogInformation("Getting booking notifications");
        var notifications = new BookingNotificationsDto
        {
            Notifications = new List<BookingNotificationDto>(),
            PendingCount = 0
        };
        return Ok(notifications);
    }

    /// <summary>
    /// Get booking help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<BookingHelpDto> GetHelp()
    {
        var help = new BookingHelpDto
        {
            Title = "Vehicle Booking Help",
            Description = "Manage vehicle reservations and bookings for departments",
            Sections = new List<HelpSectionDto>
            {
                new HelpSectionDto
                {
                    Title = "Creating Bookings",
                    Content = "Submit new vehicle booking requests with date range and department details"
                },
                new HelpSectionDto
                {
                    Title = "Booking Status",
                    Content = "Track booking approval status and vehicle allocation"
                },
                new HelpSectionDto
                {
                    Title = "Notifications",
                    Content = "Receive alerts for booking confirmations and changes"
                }
            }
        };
        return Ok(help);
    }

    #endregion
}

#region Booking DTOs

public class BookingNotificationsDto
{
    public List<BookingNotificationDto> Notifications { get; set; } = new();
    public int PendingCount { get; set; }
}

public class BookingNotificationDto
{
    public int NotificationId { get; set; }
    public string Message { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public string Status { get; set; } = "";
}

public class BookingHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSectionDto> Sections { get; set; } = new();
}

#endregion

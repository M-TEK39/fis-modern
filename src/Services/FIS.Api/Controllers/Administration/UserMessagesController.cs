using FIS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// The signed-in user's personal legacy inbox. This deliberately does not
/// expose workflow delivery logs or any other user's notifications.
/// </summary>
[ApiController]
[Route("api/user-messages")]
[Authorize]
public sealed class UserMessagesController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 50;

    private readonly UserMessageCompatibilityService _userMessageService;
    private readonly ILogger<UserMessagesController> _logger;

    public UserMessagesController(
        UserMessageCompatibilityService userMessageService,
        ILogger<UserMessagesController> logger
    )
    {
        _userMessageService = userMessageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<UserMessageInboxDto>> GetInbox([FromQuery] int? limit = null)
    {
        if (!TryGetCurrentUserAccessCode(out var userAccessCode))
        {
            return Unauthorized();
        }

        var boundedLimit = Math.Clamp(limit ?? DefaultPageSize, 1, MaximumPageSize);

        try
        {
            var inbox = await _userMessageService.GetInboxAsync(
                userAccessCode,
                boundedLimit,
                HttpContext.RequestAborted
            );
            return Ok(
                new UserMessageInboxDto
                {
                    Items = inbox.Items.Select(ToDto).ToArray(),
                    UnreadCount = inbox.UnreadCount,
                }
            );
        }
        catch (UserMessageCompatibilityUnavailableException exception)
        {
            _logger.LogWarning(exception, "Per-user notification inbox is unavailable");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Notification inbox unavailable",
                detail: "The per-user notification inbox is not available in this database."
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error retrieving user notification inbox");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Error retrieving notifications"
            );
        }
    }

    [HttpPut("{id:int}/read")]
    public async Task<ActionResult<UserMessageDto>> MarkRead(int id)
    {
        if (!TryGetCurrentUserAccessCode(out var userAccessCode))
        {
            return Unauthorized();
        }

        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            var message = await _userMessageService.MarkReadAsync(
                id,
                userAccessCode,
                HttpContext.RequestAborted
            );
            return message is null ? NotFound() : Ok(ToDto(message));
        }
        catch (UserMessageCompatibilityUnavailableException exception)
        {
            _logger.LogWarning(exception, "Per-user notification inbox is unavailable");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Notification inbox unavailable",
                detail: "The per-user notification inbox is not available in this database."
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error marking user notification {MessageId} read", id);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                "Error updating notification"
            );
        }
    }

    private bool TryGetCurrentUserAccessCode(out short userAccessCode)
    {
        userAccessCode = default;

        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId <= 0 || currentUserId > short.MaxValue)
            {
                return false;
            }

            userAccessCode = (short)currentUserId;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static UserMessageDto ToDto(UserMessageRecord record) =>
        new()
        {
            Id = record.Id,
            Message = record.Message ?? string.Empty,
            IsRead = record.IsRead,
            CreatedAt = record.CreatedAt,
        };
}

public sealed class UserMessageInboxDto
{
    public IReadOnlyList<UserMessageDto> Items { get; init; } = Array.Empty<UserMessageDto>();
    public int UnreadCount { get; init; }
}

public sealed class UserMessageDto
{
    public int Id { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public DateTime? CreatedAt { get; init; }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace FIS.Web.Services;

public class NotificationApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationApiService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private const string AccessCookieName = "FIS_Access_Token";

    public NotificationApiService(
        HttpClient httpClient,
        ILogger<NotificationApiService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<NotificationConfigStatusDto?> GetConfigStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationConfigStatusDto>("api/Notification/config/status", _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification config status");
            return null;
        }
    }

    public async Task<NotificationFeedDto> GetFeedAsync()
    {
        if (!HasSessionCookie())
        {
            return new NotificationFeedDto(new List<NotificationListItemDto>(), 0, null);
        }

        var workflowTask = GetWorkflowNotificationsAsync();
        var bookingTask = GetBookingNotificationsAsync();
        var callCentreTask = GetCallCentreNotificationsAsync();

        await Task.WhenAll(workflowTask, bookingTask, callCentreTask);

        var sourceErrors = new[]
        {
            workflowTask.Result.ErrorMessage,
            bookingTask.Result.ErrorMessage,
            callCentreTask.Result.ErrorMessage
        }
        .Where(message => !string.IsNullOrWhiteSpace(message))
        .Distinct(StringComparer.Ordinal)
        .ToList();

        var notifications = workflowTask.Result.Notifications
            .Concat(bookingTask.Result.Notifications)
            .Concat(callCentreTask.Result.Notifications)
            .OrderByDescending(item => item.CreatedDate)
            .Take(50)
            .ToList();

        return new NotificationFeedDto(
            notifications,
            notifications.Count(item => item.IsUnread),
            sourceErrors.Count == 0 ? null : string.Join(" ", sourceErrors));
    }

    private async Task<NotificationSourceFeedDto> GetWorkflowNotificationsAsync()
    {
        var pendingTask = GetJsonAsync<List<WorkflowNotificationLogDto>>("api/notification-log/pending");
        var failedTask = GetJsonAsync<List<WorkflowNotificationLogDto>>("api/notification-log/failed");

        await Task.WhenAll(pendingTask, failedTask);

        var notifications = (pendingTask.Result.Value ?? new List<WorkflowNotificationLogDto>())
            .Concat(failedTask.Result.Value ?? new List<WorkflowNotificationLogDto>())
            .OrderByDescending(item => item.SentAt ?? item.DateCreated)
            .Select(item => new NotificationListItemDto(
                item.LogID,
                BuildWorkflowTitle(item),
                $"{item.DeliveryStatus} · {FormatRelativeTime(item.SentAt ?? item.DateCreated)}",
                "Workflow",
                item.SentAt ?? item.DateCreated,
                true,
                BuildWorkflowLink(item)))
            .ToList();

        var errors = new[] { pendingTask.Result.ErrorMessage, failedTask.Result.ErrorMessage }
            .Where(message => !string.IsNullOrWhiteSpace(message));

        return new NotificationSourceFeedDto(notifications, string.Join(" ", errors));
    }

    private async Task<NotificationSourceFeedDto> GetBookingNotificationsAsync()
    {
        var response = await GetJsonAsync<BookingNotificationsDto>("api/booking/notifications");

        var notifications = response.Value?.Notifications?
            .OrderByDescending(item => item.CreatedDate)
            .Select(item => new NotificationListItemDto(
                item.NotificationId,
                item.Message,
                $"{item.Status} · {FormatRelativeTime(item.CreatedDate)}",
                "Booking",
                item.CreatedDate,
                item.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase),
                BuildBookingLink(item)))
            .ToList()
            ?? new List<NotificationListItemDto>();

        return new NotificationSourceFeedDto(notifications, response.ErrorMessage);
    }

    private async Task<NotificationSourceFeedDto> GetCallCentreNotificationsAsync()
    {
        var response = await GetJsonAsync<CallCentreNotificationsDto>("api/callcentre/notifications");

        var notifications = response.Value?.Notifications?
            .OrderByDescending(item => item.CreatedDate)
            .Select(item => new NotificationListItemDto(
                item.NotificationId,
                item.Message,
                $"{(item.IsRead ? "Read" : "Unread")} · {FormatRelativeTime(item.CreatedDate)}",
                "Call centre",
                item.CreatedDate,
                !item.IsRead,
                BuildCallCentreLink(item)))
            .ToList()
            ?? new List<NotificationListItemDto>();

        return new NotificationSourceFeedDto(notifications, response.ErrorMessage);
    }

    private async Task<NotificationApiResult<T>> GetJsonAsync<T>(string path)
    {
        try
        {
            using var response = await _httpClient.GetAsync(path);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
            {
                _logger.LogInformation(
                    "Notification source {Path} is unavailable (status {StatusCode}); returning empty source.",
                    path,
                    (int)response.StatusCode);
                return new NotificationApiResult<T>(default, null);
            }

            response.EnsureSuccessStatusCode();
            return new NotificationApiResult<T>(
                await response.Content.ReadFromJsonAsync<T>(_jsonOptions),
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notifications from {Path}", path);
            return new NotificationApiResult<T>(default, "Unable to refresh all notification sources right now.");
        }
    }

    private bool HasSessionCookie()
        => _httpContextAccessor.HttpContext?.Request.Cookies.ContainsKey(AccessCookieName) == true;

    private static string BuildWorkflowTitle(WorkflowNotificationLogDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.Subject))
        {
            return item.Subject;
        }

        return item.EventType.Replace('_', ' ') switch
        {
            var eventType when string.IsNullOrWhiteSpace(eventType) => "Workflow notification",
            var eventType => $"{eventType} notification"
        };
    }

    private static string BuildBookingLink(BookingNotificationDto item)
        => QueryHelpers.AddQueryString(
            "/call-centre",
            new Dictionary<string, string?>
            {
                ["notificationSource"] = "booking",
                ["notificationId"] = item.NotificationId.ToString()
            }) + "#booking-section";

    private static string BuildCallCentreLink(CallCentreNotificationDto item)
        => QueryHelpers.AddQueryString(
            "/call-centre/incident/edit",
            new Dictionary<string, string?>
            {
                ["referenceNumber"] = item.NotificationId.ToString(),
                ["notificationSource"] = "call-centre"
            });

    private static string BuildWorkflowLink(WorkflowNotificationLogDto item)
    {
        var route = ResolveWorkflowRoute(item);

        return QueryHelpers.AddQueryString(
            route,
            new Dictionary<string, string?>
            {
                ["notificationLogId"] = item.LogID.ToString(),
                ["notificationId"] = item.NotificationID?.ToString(),
                ["workflowId"] = item.WorkflowID?.ToString(),
                ["stepId"] = item.StepID?.ToString(),
                ["eventType"] = item.EventType
            });
    }

    private static string ResolveWorkflowRoute(WorkflowNotificationLogDto item)
    {
        var lookup = $"{item.Subject} {item.EventType}".ToLowerInvariant();

        if (lookup.Contains("backdat"))
        {
            return "/contracts/backdating-approval";
        }

        if (lookup.Contains("tariff") || lookup.Contains("lease"))
        {
            return "/full-maintenance-lease/tariffs";
        }

        if (lookup.Contains("vehicle") && (lookup.Contains("authoriz") || lookup.Contains("approval")))
        {
            return "/vehicles/authorize";
        }

        if (lookup.Contains("contract") || lookup.Contains("expiry"))
        {
            return "/contracts";
        }

        if (lookup.Contains("licence") || lookup.Contains("license") || lookup.Contains("cof") || lookup.Contains("maintenance"))
        {
            return "/vehicles";
        }

        if (lookup.Contains("call") || lookup.Contains("incident"))
        {
            return "/call-centre";
        }

        return "/contracts";
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var delta = DateTime.UtcNow - timestamp.ToUniversalTime();
        if (delta.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalMinutes))}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalHours))}h ago";
        }

        if (delta.TotalDays < 7)
        {
            return $"{Math.Max(1, (int)Math.Floor(delta.TotalDays))}d ago";
        }

        return timestamp.ToLocalTime().ToString("dd MMM yyyy");
    }
}

public record NotificationConfigStatusDto(bool? SmtpConfigured, bool? TemplatesConfigured);
public record NotificationFeedDto(IReadOnlyList<NotificationListItemDto> Notifications, int UnreadCount, string? ErrorMessage);
public record NotificationListItemDto(int Id, string Title, string Meta, string Source, DateTime CreatedDate, bool IsUnread, string? LinkUrl);
internal sealed record NotificationSourceFeedDto(IReadOnlyList<NotificationListItemDto> Notifications, string? ErrorMessage);
internal sealed record NotificationApiResult<T>(T? Value, string? ErrorMessage);

public sealed class WorkflowNotificationLogDto
{
    public int LogID { get; set; }
    public int? NotificationID { get; set; }
    public int? WorkflowID { get; set; }
    public int? StepID { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public DateTime? SentAt { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
}

public sealed class BookingNotificationsDto
{
    public List<BookingNotificationDto> Notifications { get; set; } = new();
    public int PendingCount { get; set; }
}

public sealed class BookingNotificationDto
{
    public int NotificationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CallCentreNotificationsDto
{
    public List<CallCentreNotificationDto> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
}

public sealed class CallCentreNotificationDto
{
    public int NotificationId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsRead { get; set; }
}

using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class NotificationApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationApiService> _logger;

    public NotificationApiService(HttpClient httpClient, ILogger<NotificationApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NotificationConfigStatusDto?> GetConfigStatusAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<NotificationConfigStatusDto>("api/Notification/config/status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification config status");
            return null;
        }
    }
}

public record NotificationConfigStatusDto(bool? SmtpConfigured, bool? TemplatesConfigured);

using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class MaintenanceTriggerApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MaintenanceTriggerApiService> _logger;

    public MaintenanceTriggerApiService(HttpClient httpClient, ILogger<MaintenanceTriggerApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<MaintenanceTriggerDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<MaintenanceTriggerDto>>("api/MaintenanceTrigger");
            return result ?? new List<MaintenanceTriggerDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching maintenance triggers");
            return new List<MaintenanceTriggerDto>();
        }
    }
}

public record MaintenanceTriggerDto(
    int Id,
    string? Description,
    string? TriggerId
);

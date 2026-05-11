using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class MaintenanceTriggerApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<MaintenanceTriggerApiService> _logger;

    public MaintenanceTriggerApiService(HttpClient httpClient, TokenService tokenService, ILogger<MaintenanceTriggerApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token)) return;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<MaintenanceTriggerDto>> GetAllAsync()
    {
        try
        {
            AddAuthHeader();
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

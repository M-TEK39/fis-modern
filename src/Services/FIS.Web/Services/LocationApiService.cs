using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class LocationApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocationApiService> _logger;

    public LocationApiService(HttpClient httpClient, ILogger<LocationApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<LocationDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<LocationDto>>("api/Location");
            return result ?? new List<LocationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations");
            return new List<LocationDto>();
        }
    }
}

public record LocationDto(
    int LocationId,
    string? Name,
    string? Country,
    string? Province
);

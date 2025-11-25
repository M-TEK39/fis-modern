using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class TripDriverApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TripDriverApiService> _logger;

    public TripDriverApiService(HttpClient httpClient, ILogger<TripDriverApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TripDriverDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDriverDto>>("api/TripDriver");
            return result ?? new List<TripDriverDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip drivers");
            return new List<TripDriverDto>();
        }
    }

    public async Task<List<TripDriverDto>> GetBySiteAsync(int siteCode)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDriverDto>>($"api/TripDriver/site/{siteCode}");
            return result ?? new List<TripDriverDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip drivers for site {SiteCode}", siteCode);
            return new List<TripDriverDto>();
        }
    }

    public async Task<List<TripDriverDto>> GetPrimaryAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDriverDto>>("api/TripDriver/primary");
            return result ?? new List<TripDriverDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching primary trip drivers");
            return new List<TripDriverDto>();
        }
    }
}

public record TripDriverDto(
    int TripDriverCode,
    int DriverCode,
    int? SiteCode,
    bool Primary,
    string? Notes
);

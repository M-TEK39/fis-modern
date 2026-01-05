using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json;

namespace FIS.Web.Services;

public class TripApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TripApiService> _logger;

    public TripApiService(HttpClient httpClient, ILogger<TripApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TripDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>("api/Trip");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips");
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetByVehicleAsync(int vmfCode)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>($"api/Trip/vehicle/{vmfCode}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips for vehicle {VmfCode}", vmfCode);
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetByDriverAsync(int driverId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>($"api/Trip/driver/{driverId}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips for driver {DriverId}", driverId);
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetRecentAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>("api/Trip/recent");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent trips");
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetDateRangeAsync(DateTime start, DateTime end)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>(
                $"api/Trip/daterange?startDate={Uri.EscapeDataString(start.ToString("o"))}&endDate={Uri.EscapeDataString(end.ToString("o"))}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips by date range");
            return new List<TripDto>();
        }
    }

    public async Task<TripDto?> GetAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TripDto>($"api/Trip/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip {TripId}", id);
            return null;
        }
    }
}

public record TripDto(
    int TripId,
    int ContractCode,
    int VmfCode,
    int? DriverId,
    DateTime TripDate,
    string? Status,
    string? Notes
);

using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class DriverApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DriverApiService> _logger;

    public DriverApiService(HttpClient httpClient, ILogger<DriverApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<DriverDto>> GetDriversAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<DriverDto>>("api/drivers");
            return result ?? new List<DriverDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching drivers");
            return new List<DriverDto>();
        }
    }

    public async Task<DriverDto?> GetDriverAsync(int driverCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DriverDto>($"api/drivers/{driverCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching driver {DriverCode}", driverCode);
            return null;
        }
    }

    public async Task<bool> CreateDriverAsync(DriverDto driver)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/drivers", driver);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver");
            return false;
        }
    }

    public async Task<bool> UpdateDriverAsync(int driverCode, DriverDto driver)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/drivers/{driverCode}", driver);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver {DriverCode}", driverCode);
            return false;
        }
    }

    public async Task<bool> DeleteDriverAsync(int driverCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/drivers/{driverCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver {DriverCode}", driverCode);
            return false;
        }
    }
}

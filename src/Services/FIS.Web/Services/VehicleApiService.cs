using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json;

namespace FIS.Web.Services;

public class VehicleApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VehicleApiService> _logger;

    public VehicleApiService(HttpClient httpClient, ILogger<VehicleApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PagedResult<VehicleDto>> GetVehiclesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? search = null
    )
    {
        try
        {
            var query = $"api/vehicles?pageNumber={pageNumber}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(search))
            {
                query += $"&search={Uri.EscapeDataString(search)}";
            }

            var response = await _httpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                var paged = JsonSerializer.Deserialize<PagedResult<VehicleDto>>(content, jsonOptions);
                if (paged is not null)
                {
                    return paged;
                }
            }
            catch (JsonException)
            {
                // fallback below
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<VehicleDto>>(content, jsonOptions) ?? new List<VehicleDto>();
                return new PagedResult<VehicleDto>
                {
                    Data = list,
                    TotalCount = list.Count,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing vehicles response: {Content}", content);
                return new PagedResult<VehicleDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicles");
            return new PagedResult<VehicleDto>();
        }
    }

    public async Task<VehicleDto?> GetVehicleAsync(int vmfCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VehicleDto>($"api/vehicles/{vmfCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle {VmfCode}", vmfCode);
            return null;
        }
    }

    public async Task<bool> CreateVehicleAsync(VehicleDto vehicle)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/vehicles", vehicle);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Error creating vehicle. Status: {StatusCode}, Response: {Content}",
                    response.StatusCode,
                    errorContent
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle");
            return false;
        }
    }

    public async Task<bool> UpdateVehicleAsync(int vmfCode, VehicleDto vehicle)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/vehicles/{vmfCode}", vehicle);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Error updating vehicle {VmfCode}. Status: {StatusCode}, Response: {Content}",
                    vmfCode,
                    response.StatusCode,
                    errorContent
                );
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> DeleteVehicleAsync(int vmfCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/vehicles/{vmfCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle {VmfCode}", vmfCode);
            return false;
        }
    }
}

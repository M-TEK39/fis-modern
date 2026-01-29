using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json;
using System.Net.Http.Headers;

namespace FIS.Web.Services;

public class VehicleApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<VehicleApiService> _logger;

    public VehicleApiService(HttpClient httpClient, TokenService tokenService, ILogger<VehicleApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthorizationHeader()
    {
        if (_tokenService.IsTokenValid && !string.IsNullOrEmpty(_tokenService.Token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _tokenService.Token);
        }
    }

    public async Task<PagedResult<VehicleDto>> GetVehiclesAsync(
        int pageNumber = 1,
        int pageSize = 10,
        string? search = null
    )
    {
        try
        {
            AddAuthorizationHeader();
            
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

    public async Task<List<VehicleDto>> SearchVehiclesAsync(string searchTerm)
    {
        try
        {
            AddAuthorizationHeader();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<VehicleDto>();
            }

            var url = $"api/vehicles/search?searchTerm={Uri.EscapeDataString(searchTerm)}";
            var result = await _httpClient.GetFromJsonAsync<List<VehicleDto>>(url);
            return result ?? new List<VehicleDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicles with term {SearchTerm}", searchTerm);
            return new List<VehicleDto>();
        }
    }

    public async Task<bool> CreateVehicleAsync(VehicleDto vehicle)
    {
        try
        {
            var createRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                fleet_number = vehicle.fleet_number,
                registration_number = vehicle.registration_number,
                engine_number_1 = vehicle.engine_number_1,
                chassis_number = vehicle.chassis_number,
                take_on_date = vehicle.take_on_date,
                take_on_odo = vehicle.take_on_odo ?? 0,
                current_odo = vehicle.current_odo ?? 0,
                tare = vehicle.tare,
                gvm = vehicle.gvm,
                year_manufactured = vehicle.year_manufactured,
                colour = vehicle.colour,
                purchase_date = vehicle.purchase_date,
                purchase_amount = vehicle.purchase_amount
            };

            var response = await _httpClient.PostAsJsonAsync("api/vehicles", createRequest);

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
            var updateRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                fleet_number = vehicle.fleet_number,
                registration_number = vehicle.registration_number,
                engine_number_1 = vehicle.engine_number_1,
                chassis_number = vehicle.chassis_number,
                take_on_odo = vehicle.take_on_odo,
                current_odo = vehicle.current_odo,
                tare = vehicle.tare,
                gvm = vehicle.gvm,
                year_manufactured = vehicle.year_manufactured,
                colour = vehicle.colour
            };

            var response = await _httpClient.PutAsJsonAsync($"api/vehicles/{vmfCode}", updateRequest);

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

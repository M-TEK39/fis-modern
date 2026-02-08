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

    public async Task<bool> CreateVehicleAsync(VehicleDto vehicle, bool recalculateTariff = false)
    {
        try
        {
            var createRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                site_code = vehicle.site_code,
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
                purchase_amount = vehicle.purchase_amount,
                purchased_from = vehicle.purchased_from,
                invoice_number = vehicle.invoice_number,
                service_last_done = vehicle.service_last_done,
                service_last_odo = vehicle.service_last_odo,
                cof_last_done = vehicle.cof_last_done,
                cof_required = vehicle.cof_required,
                cof_number = vehicle.cof_number,
                cof_amount = vehicle.Cof_amount,
                extended_service = vehicle.extended_service,
                recalculate_tariff = recalculateTariff
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

    public async Task<bool> UpdateVehicleAsync(int vmfCode, VehicleDto vehicle, bool recalculateTariff = false)
    {
        try
        {
            var updateRequest = new
            {
                model_code = vehicle.model_code,
                type_code = vehicle.type_code,
                vehicle_status_code = vehicle.vehicle_status_code,
                location_code = vehicle.location_code,
                site_code = vehicle.site_code,
                fleet_number = vehicle.fleet_number,
                registration_number = vehicle.registration_number,
                engine_number_1 = vehicle.engine_number_1,
                chassis_number = vehicle.chassis_number,
                take_on_odo = vehicle.take_on_odo,
                current_odo = vehicle.current_odo,
                tare = vehicle.tare,
                gvm = vehicle.gvm,
                year_manufactured = vehicle.year_manufactured,
                colour = vehicle.colour,
                purchase_date = vehicle.purchase_date,
                purchase_amount = vehicle.purchase_amount,
                purchased_from = vehicle.purchased_from,
                invoice_number = vehicle.invoice_number,
                service_last_done = vehicle.service_last_done,
                service_last_odo = vehicle.service_last_odo,
                cof_last_done = vehicle.cof_last_done,
                cof_required = vehicle.cof_required,
                cof_number = vehicle.cof_number,
                cof_amount = vehicle.Cof_amount,
                extended_service = vehicle.extended_service,
                recalculate_tariff = recalculateTariff
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

    public async Task<List<VehicleAuthorizationDto>> GetPendingAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/pending");

    public async Task<List<VehicleAuthorizationDto>> GetAuthorizedAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/authorized");

    public async Task<List<VehicleAuthorizationDto>> GetRejectedAuthorizationsAsync()
        => await GetVehicleAuthorizationsAsync("api/vehicle/authorization/rejected");

    public async Task<VehicleAuthorizationDto?> GetVehicleAuthorizationAsync(int id)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/vehicle/authorization/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<VehicleAuthorizationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle authorization {Id}", id);
            return null;
        }
    }

    public async Task<FinanceApiResult> ApproveVehicleAuthorizationAsync(int id, string? comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/approve",
            new { comment });
    }

    public async Task<FinanceApiResult> RejectVehicleAuthorizationAsync(int id, string rejectionReason, string? comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/reject",
            new { rejectionReason, comment });
    }

    public async Task<FinanceApiResult> AddVehicleAuthorizationCommentAsync(int id, string comment)
    {
        return await PostVehicleAuthorizationActionAsync(
            $"api/vehicle/authorization/{id}/comment",
            new { comment });
    }

    private async Task<List<VehicleAuthorizationDto>> GetVehicleAuthorizationsAsync(string endpoint)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<VehicleAuthorizationDto>>()
                ?? new List<VehicleAuthorizationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle authorization list from {Endpoint}", endpoint);
            return new List<VehicleAuthorizationDto>();
        }
    }

    private async Task<FinanceApiResult> PostVehicleAuthorizationActionAsync(string endpoint, object payload)
    {
        try
        {
            AddAuthorizationHeader();
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vehicle authorization action failed. Endpoint: {Endpoint}. Status: {Status}. Body: {Body}",
                    endpoint,
                    response.StatusCode,
                    body);
            }

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : ExtractApiErrorMessage(body) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting vehicle authorization action to {Endpoint}", endpoint);
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = ex.Message
            };
        }
    }

    private static string? ExtractApiErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorValue))
            {
                var errorMessage = errorValue.GetString();
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    return errorMessage.Trim();
                }
            }

            if (root.TryGetProperty("message", out var messageValue))
            {
                var message = messageValue.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }
        catch
        {
            // Ignore parse failures and keep fallback status message.
        }

        return null;
    }
}

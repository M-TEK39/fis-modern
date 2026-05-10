using System.Net.Http.Json;
using System.Text.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class FleetManagementApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FleetManagementApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public FleetManagementApiService(HttpClient httpClient, ILogger<FleetManagementApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FuelCardIssueResponse?> IssueFuelCardAsync(IssueFuelCardRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/fleetmanagement/fuelcards/issue", request);
            var payload = await response.Content.ReadFromJsonAsync<FuelCardIssueResponse>(_jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            return payload ?? new FuelCardIssueResponse
            {
                Success = false,
                Message = "Failed to issue fuel card."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing fuel card");
            return new FuelCardIssueResponse
            {
                Success = false,
                Message = "Error issuing fuel card.",
                Error = ex.Message
            };
        }
    }

    public async Task<ApiResponse?> ReturnFuelCardAsync(int fuelCardCode, ReturnFuelCardRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/fleetmanagement/fuelcards/{fuelCardCode}/return",
                request);
            var payload = await response.Content.ReadFromJsonAsync<ApiResponse>(_jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            return payload ?? new ApiResponse
            {
                Success = false,
                Message = "Failed to return fuel card."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning fuel card {FuelCardCode}", fuelCardCode);
            return new ApiResponse
            {
                Success = false,
                Message = $"Error returning fuel card {fuelCardCode}.",
                Error = ex.Message
            };
        }
    }

    public async Task<FuelCardReportResponse?> GetFuelCardAllocationAsync(int? siteCode = null)
    {
        try
        {
            var url = "api/fleetmanagement/reports/fuelcard-allocation";
            if (siteCode.HasValue)
            {
                url = $"{url}?siteCode={siteCode.Value}";
            }

            var response = await _httpClient.GetAsync(url);
            var payload = await response.Content.ReadFromJsonAsync<FuelCardReportResponse>(_jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return payload;
            }

            return payload ?? new FuelCardReportResponse
            {
                Success = false,
                Message = "Failed to fetch fuel card allocation report."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuel card allocation report");
            return new FuelCardReportResponse
            {
                Success = false,
                Message = "Error fetching fuel card allocation report.",
                Error = ex.Message
            };
        }
    }

    public async Task<List<FuelCardRecord>> GetFuelCardsByVehicleAsync(int vmfCode)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/FuelCard/vehicle/{vmfCode}");
            if (!response.IsSuccessStatusCode)
            {
                return new List<FuelCardRecord>();
            }

            var payload = await response.Content.ReadFromJsonAsync<List<FuelCardRecord>>(_jsonOptions);
            return payload ?? new List<FuelCardRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuel cards for vehicle {VmfCode}", vmfCode);
            return new List<FuelCardRecord>();
        }
    }

    public async Task<bool> CreateFuelCardAsync(FuelCardCreateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/FuelCard", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fuel card for vehicle {VmfCode}", request.vmf_code);
            return false;
        }
    }

    public async Task<bool> CreatePrivateHireFuelCardAsync(PrivateHireFuelCardCreateRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/privatehirefuelcard", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating private hire fuel card for registration {Registration}", request.RegistrationNumber);
            return false;
        }
    }

    public async Task<bool> DeleteFuelCardAsync(int fuelCardCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/FuelCard/{fuelCardCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting fuel card {FuelCardCode}", fuelCardCode);
            return false;
        }
    }
}

public sealed class FuelCardRecord
{
    public int Fuel_card_code { get; set; }
    public int? vmf_code { get; set; }
    public string? card_number { get; set; }
}

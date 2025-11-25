using System.Net.Http.Json;
using System.Text.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class FleetManagementApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FleetManagementApiService> _logger;

    public FleetManagementApiService(HttpClient httpClient, ILogger<FleetManagementApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IssueFuelCardAsync(FuelCardIssueDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/FleetManagement/fuelcards/issue", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing fuel card");
            return false;
        }
    }

    public async Task<bool> ReturnFuelCardAsync(int fuelCardCode)
    {
        try
        {
            var response = await _httpClient.PutAsync($"api/FleetManagement/fuelcards/{fuelCardCode}/return", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning fuel card {FuelCardCode}", fuelCardCode);
            return false;
        }
    }

    public async Task<List<FuelCardAllocationReportDto>> GetFuelCardAllocationAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/FleetManagement/reports/fuelcard-allocation");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                var list = JsonSerializer.Deserialize<List<FuelCardAllocationReportDto>>(content, options) ?? new List<FuelCardAllocationReportDto>();
                if (list is not null)
                {
                    return list;
                }
            }
            catch (JsonException)
            {
                // fall through to alternate shapes
            }

            try
            {
                var single = JsonSerializer.Deserialize<FuelCardAllocationReportDto>(content, options);
                if (single is not null)
                {
                    return new List<FuelCardAllocationReportDto> { single };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing fuel card allocation response: {Content}", content);
            }

            return new List<FuelCardAllocationReportDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching fuel card allocation report");
            return new List<FuelCardAllocationReportDto>();
        }
    }
}

public record FuelCardIssueDto(string CardNumber, int VehicleCode, DateTime IssueDate, string? Notes);

public record FuelCardAllocationReportDto(string CardNumber, int VehicleCode, string? Vehicle, string? Status, DateTime? IssueDate, DateTime? ReturnDate);

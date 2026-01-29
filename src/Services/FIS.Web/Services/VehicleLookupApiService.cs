using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class VehicleLookupApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VehicleLookupApiService> _logger;

    public VehicleLookupApiService(HttpClient httpClient, ILogger<VehicleLookupApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<string>> GetSearchCriteriaAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<string>>("api/VehicleSearchCriteria");
            return result ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle search criteria");
            return new List<string>();
        }
    }

    public async Task<List<VehicleLookupDto>> SearchAsync(string keyword)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new List<VehicleLookupDto>();
            }

            var url = $"api/VehicleLookup?keyword={Uri.EscapeDataString(keyword)}";
            var result = await _httpClient.GetFromJsonAsync<List<VehicleLookupDto>>(url);
            return result ?? new List<VehicleLookupDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle lookup results");
            return new List<VehicleLookupDto>();
        }
    }
}

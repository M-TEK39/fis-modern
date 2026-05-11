using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class VehicleLookupApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<VehicleLookupApiService> _logger;

    public VehicleLookupApiService(
        HttpClient httpClient,
        TokenService tokenService,
        ILogger<VehicleLookupApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthorizationHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<string>> GetSearchCriteriaAsync()
    {
        try
        {
            AddAuthorizationHeader();
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
            AddAuthorizationHeader();
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

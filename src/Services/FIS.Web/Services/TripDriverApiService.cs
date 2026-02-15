using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json.Serialization;

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

    public async Task<TripDriverDto?> CreateAsync(TripDriverDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/TripDriver", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TripDriverDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trip driver");
            return null;
        }
    }

    public async Task<bool> DeleteAsync(int tripDriverCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/TripDriver/{tripDriverCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting trip driver {TripDriverCode}", tripDriverCode);
            return false;
        }
    }
}

public class TripDriverDto
{
    [JsonPropertyName("trip_driver_code")]
    public int trip_driver_code { get; set; }

    [JsonPropertyName("trip_driver_name")]
    public string? trip_driver_name { get; set; }

    [JsonPropertyName("trip_driver_id")]
    public string? trip_driver_id { get; set; }

    [JsonPropertyName("trip_authority_code")]
    public int trip_authority_code { get; set; }

    [JsonPropertyName("trip_driver_primary")]
    public bool trip_driver_primary { get; set; }

    [JsonPropertyName("site_code")]
    public int? site_code { get; set; }

    [JsonPropertyName("driver_licence_type_id")]
    public int? driver_licence_type_id { get; set; }

    [JsonPropertyName("driver_passportnumber")]
    public string? driver_passportnumber { get; set; }

    [JsonPropertyName("driver_persalnumber")]
    public string? driver_persalnumber { get; set; }

    [JsonPropertyName("driver_contractnumber")]
    public string? driver_contractnumber { get; set; }

    [JsonPropertyName("driver_licence_number")]
    public string? driver_licence_number { get; set; }

    [JsonPropertyName("driver_licence_issuedate")]
    public DateTime? driver_licence_issuedate { get; set; }

    [JsonPropertyName("driver_licence_lastVerifiedDate")]
    public DateTime? driver_licence_lastVerifiedDate { get; set; }

    [JsonPropertyName("driver_hasPDP")]
    public bool driver_hasPDP { get; set; }

    [JsonPropertyName("driver_PDP_ExpiryDate")]
    public DateTime? driver_PDP_ExpiryDate { get; set; }

    [JsonPropertyName("driver_licence_ExpiryDate")]
    public DateTime? driver_licence_ExpiryDate { get; set; }

    [JsonPropertyName("driver_active")]
    public bool driver_active { get; set; } = true;
}

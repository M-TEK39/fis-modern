using System.Net.Http.Json;
using FIS.Web.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FIS.Web.Services;

public class ReportCatalogApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportCatalogApiService> _logger;

    public ReportCatalogApiService(HttpClient httpClient, ILogger<ReportCatalogApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ReportDto>> GetAvailableAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<ReportDto>>("api/Report/available");
            return result ?? new List<ReportDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available reports");
            return new List<ReportDto>();
        }
    }

    public async Task<List<TripReportSummaryDto>> GetTripSummaryAsync()
    {
        try
        {
            var startDate = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-90).ToString("o"));
            var endDate = Uri.EscapeDataString(DateTime.UtcNow.ToString("o"));
            var response = await _httpClient.GetAsync($"api/Report/trip/summary?startDate={startDate}&endDate={endDate}");
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            // Some endpoints may return a single object instead of an array; handle both.
            var content = await response.Content.ReadAsStreamAsync();
            try
            {
                var list = await JsonSerializer.DeserializeAsync<List<TripReportSummaryDto>>(content, options);
                if (list != null)
                {
                    return list;
                }
            }
            catch (JsonException)
            {
                content.Position = 0;
                var single = await JsonSerializer.DeserializeAsync<TripReportSummaryDto>(content, options);
                if (single != null)
                {
                    return new List<TripReportSummaryDto> { single };
                }
            }

            return new List<TripReportSummaryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip summary report");
            return new List<TripReportSummaryDto>();
        }
    }

    public async Task<TripReportDetailDto?> GetTripDetailAsync(int tripId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Report/trip/detail/{tripId}");
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            return await response.Content.ReadFromJsonAsync<TripReportDetailDto>(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip detail for trip {TripId}", tripId);
            return null;
        }
    }
}

public record TripReportSummaryDto
{
    public int TripId { get; set; }
    public int? VmfCode { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? Driver { get; set; }
    public DateTime? TripDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public record TripReportDetailDto : TripReportSummaryDto
{
    public decimal? Distance { get; set; }
    public decimal? FuelUsed { get; set; }
    public decimal? Cost { get; set; }
}

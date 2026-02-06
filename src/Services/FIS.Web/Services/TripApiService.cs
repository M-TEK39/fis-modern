using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class TripApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TripApiService> _logger;

    public TripApiService(HttpClient httpClient, ILogger<TripApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<TripDto>> GetAllAsync()
    {
        try
        {
            var startDate = Uri.EscapeDataString(DateTime.UtcNow.AddDays(-90).ToString("o"));
            var endDate = Uri.EscapeDataString(DateTime.UtcNow.ToString("o"));
            var result = await _httpClient.GetFromJsonAsync<List<TripReportSummaryResponse>>(
                $"api/Report/trip/summary?startDate={startDate}&endDate={endDate}");
            return result?.Select(MapSummary).ToList() ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips");
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetByVehicleAsync(int vmfCode)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>($"api/Trip/vehicle/{vmfCode}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips for vehicle {VmfCode}", vmfCode);
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetByDriverAsync(int driverId)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>($"api/Trip/driver/{driverId}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips for driver {DriverId}", driverId);
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetRecentAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>("api/Trip/recent");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent trips");
            return new List<TripDto>();
        }
    }

    public async Task<List<TripDto>> GetDateRangeAsync(DateTime start, DateTime end)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TripDto>>(
                $"api/Trip/daterange?startDate={Uri.EscapeDataString(start.ToString("o"))}&endDate={Uri.EscapeDataString(end.ToString("o"))}");
            return result ?? new List<TripDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trips by date range");
            return new List<TripDto>();
        }
    }

    public async Task<TripDto?> GetAsync(int id)
    {
        try
        {
            var reportTrip = await _httpClient.GetFromJsonAsync<TripReportSummaryResponse>($"api/Report/trip/detail/{id}");
            if (reportTrip != null)
            {
                return MapSummary(reportTrip);
            }

            var apiTrip = await _httpClient.GetFromJsonAsync<TripControllerResponse>($"api/Trip/{id}");
            if (apiTrip == null)
            {
                return null;
            }

            return new TripDto
            {
                TripId = apiTrip.TripAuthorityCode,
                ContractCode = apiTrip.ContractCode,
                VmfCode = 0,
                DriverId = null,
                TripDate = apiTrip.IssueDate,
                Status = apiTrip.ExpiryDate.HasValue && apiTrip.ExpiryDate.Value < DateTime.UtcNow
                    ? "Expired"
                    : "Open",
                Notes = apiTrip.TripReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trip {TripId}", id);
            return null;
        }
    }

    private static TripDto MapSummary(TripReportSummaryResponse source)
        => new()
        {
            TripId = source.TripId,
            ContractCode = 0,
            VmfCode = source.VmfCode ?? 0,
            DriverId = null,
            TripDate = source.TripDate ?? DateTime.MinValue,
            Status = source.Status,
            Notes = source.Notes
        };
}

public class TripDto
{
    public int TripId { get; set; }
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public int? DriverId { get; set; }
    public DateTime TripDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

internal sealed class TripReportSummaryResponse
{
    public int TripId { get; set; }
    public int? VmfCode { get; set; }
    public DateTime? TripDate { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

internal sealed class TripControllerResponse
{
    public int TripAuthorityCode { get; set; }
    public int ContractCode { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? TripReason { get; set; }
}

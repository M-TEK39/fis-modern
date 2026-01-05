using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class MaintenanceRecordApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MaintenanceRecordApiService> _logger;

    public MaintenanceRecordApiService(HttpClient httpClient, ILogger<MaintenanceRecordApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<MaintenanceRecordDto>> GetByVehicleAsync(int vmfCode)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<MaintenanceRecordDto>>($"api/MaintenanceRecord/vehicle/{vmfCode}");
            return result ?? new List<MaintenanceRecordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching maintenance records for vehicle {VmfCode}", vmfCode);
            return new List<MaintenanceRecordDto>();
        }
    }

    public async Task<List<MaintenanceRecordDto>> SearchAsync(string searchTerm)
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<MaintenanceRecordDto>>($"api/MaintenanceRecord/search?searchTerm={Uri.EscapeDataString(searchTerm)}");
            return result ?? new List<MaintenanceRecordDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching maintenance records");
            return new List<MaintenanceRecordDto>();
        }
    }
}

public record MaintenanceRecordDto(
    int MaintenanceId,
    int VmfCode,
    string? ServiceType,
    DateTime? ServiceDate,
    int? ServiceOdometer,
    decimal? Cost,
    string? Notes
);

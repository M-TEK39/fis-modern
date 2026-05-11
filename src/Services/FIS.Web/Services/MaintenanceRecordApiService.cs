using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class MaintenanceRecordApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<MaintenanceRecordApiService> _logger;

    public MaintenanceRecordApiService(HttpClient httpClient, TokenService tokenService, ILogger<MaintenanceRecordApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token)) return;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<MaintenanceRecordDto>> GetByVehicleAsync(int vmfCode)
    {
        try
        {
            AddAuthHeader();
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
            AddAuthHeader();
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

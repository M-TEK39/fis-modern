using System.Net.Http.Json;
using FIS.Web.Models;

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
}

using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class LicenseApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LicenseApiService> _logger;

    public LicenseApiService(HttpClient httpClient, ILogger<LicenseApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<LicenseDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<LicenseDto>>("api/License");
            return result ?? new List<LicenseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching licenses");
            return new List<LicenseDto>();
        }
    }
}

public record LicenseDto(
    int LicenceCode,
    string? Description,
    DateTime? ExpiryDate,
    string? Status
);

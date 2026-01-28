using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiLicenseResponse
{
    public short licence_code { get; set; }
    public string licence_description { get; set; } = string.Empty;
    public string? licence_category { get; set; }
}

public class LicenseApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LicenseApiService> _logger;

    public LicenseApiService(HttpClient httpClient, ILogger<LicenseApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<LicenseTypeDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiLicenseResponse>>("api/License");
            if (apiResponse == null) return new List<LicenseTypeDto>();

            return apiResponse.Select(license => new LicenseTypeDto
            {
                licence_code = license.licence_code,
                licence_description = license.licence_description,
                licence_category = license.licence_category
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching licenses");
            return new List<LicenseTypeDto>();
        }
    }

    public async Task<bool> CreateAsync(LicenseTypeDto license)
    {
        try
        {
            var createDto = new
            {
                licence_description = license.licence_description,
                licence_category = license.licence_category
            };

            var response = await _httpClient.PostAsJsonAsync("api/License", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int licenceCode, LicenseTypeDto license)
    {
        try
        {
            var updateDto = new
            {
                licence_code = (short)licenceCode,
                licence_description = license.licence_description,
                licence_category = license.licence_category
            };

            var response = await _httpClient.PutAsJsonAsync($"api/License/{licenceCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating license {LicenceCode}", licenceCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int licenceCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/License/{licenceCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting license {LicenceCode}", licenceCode);
            return false;
        }
    }
}

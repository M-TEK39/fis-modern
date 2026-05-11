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
    private readonly TokenService _tokenService;
    private readonly ILogger<LicenseApiService> _logger;

    public LicenseApiService(HttpClient httpClient, TokenService tokenService, ILogger<LicenseApiService> logger)
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

    public async Task<List<LicenseTypeDto>> GetAllAsync()
    {
        try
        {
            AddAuthHeader();
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
            AddAuthHeader();
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
            AddAuthHeader();
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
            AddAuthHeader();
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

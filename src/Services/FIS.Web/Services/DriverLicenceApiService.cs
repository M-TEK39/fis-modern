using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiDriverLicenceTypeResponse
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
}

internal class ApiDriverLicenceResponse
{
    public short LicenceCode { get; set; }
    public string? Description { get; set; }
}

public class DriverLicenceApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<DriverLicenceApiService> _logger;

    public DriverLicenceApiService(HttpClient httpClient, TokenService tokenService, ILogger<DriverLicenceApiService> logger)
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

    /// <summary>
    /// Returns driver licence types from driver_licence_types table.
    /// Used to populate dropdowns where driver_licence_type_id is the FK.
    /// </summary>
    public async Task<List<DriverLicenceDto>> GetAllAsync()
    {
        try
        {
            AddAuthHeader();
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiDriverLicenceTypeResponse>>("api/site-drivers/licence-types");
            if (apiResponse == null) return new List<DriverLicenceDto>();

            return apiResponse.Select(item => new DriverLicenceDto
            {
                licence_code = item.Id,
                code = item.Code,
                description = item.Description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching driver licence types");
            return new List<DriverLicenceDto>();
        }
    }

    public async Task<bool> CreateAsync(DriverLicenceDto licence)
    {
        try
        {
            AddAuthHeader();
            var createDto = new
            {
                code = licence.code,
                description = licence.description
            };

            var response = await _httpClient.PostAsJsonAsync("api/site-drivers/licence-types", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver license");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int licenceCode, DriverLicenceDto licence)
    {
        try
        {
            AddAuthHeader();
            var updateDto = new
            {
                code = licence.code,
                description = licence.description
            };

            var response = await _httpClient.PutAsJsonAsync($"api/site-drivers/licence-types/{licenceCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver license {LicenceCode}", licenceCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int licenceCode)
    {
        try
        {
            AddAuthHeader();
            var response = await _httpClient.DeleteAsync($"api/site-drivers/licence-types/{licenceCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver license {LicenceCode}", licenceCode);
            return false;
        }
    }
}

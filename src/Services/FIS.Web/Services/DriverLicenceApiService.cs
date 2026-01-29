using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiDriverLicenceResponse
{
    public short licence_code { get; set; }
    public string description { get; set; } = string.Empty;
}

public class DriverLicenceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DriverLicenceApiService> _logger;

    public DriverLicenceApiService(HttpClient httpClient, ILogger<DriverLicenceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<DriverLicenceDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiDriverLicenceResponse>>("api/driverlicence");
            if (apiResponse == null) return new List<DriverLicenceDto>();

            return apiResponse.Select(item => new DriverLicenceDto
            {
                licence_code = item.licence_code,
                description = item.description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching driver licenses");
            return new List<DriverLicenceDto>();
        }
    }

    public async Task<bool> CreateAsync(DriverLicenceDto licence)
    {
        try
        {
            var createDto = new
            {
                description = licence.description
            };

            var response = await _httpClient.PostAsJsonAsync("api/driverlicence", createDto);
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
            var updateDto = new
            {
                licence_code = (short)licenceCode,
                description = licence.description
            };

            var response = await _httpClient.PutAsJsonAsync($"api/driverlicence/{licenceCode}", updateDto);
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
            var response = await _httpClient.DeleteAsync($"api/driverlicence/{licenceCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver license {LicenceCode}", licenceCode);
            return false;
        }
    }
}

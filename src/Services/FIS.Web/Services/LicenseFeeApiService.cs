using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiLicenseFeeResponse
{
    public short licence_fee_code { get; set; }
    public string licence_description { get; set; } = string.Empty;
    public decimal? licence_fee { get; set; }
}

public class LicenseFeeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LicenseFeeApiService> _logger;

    public LicenseFeeApiService(HttpClient httpClient, ILogger<LicenseFeeApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<LicenseFeeDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiLicenseFeeResponse>>("api/licensefee");
            if (apiResponse == null) return new List<LicenseFeeDto>();

            return apiResponse.Select(item => new LicenseFeeDto
            {
                licence_fee_code = item.licence_fee_code,
                licence_description = item.licence_description,
                licence_fee = item.licence_fee
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching license fees");
            return new List<LicenseFeeDto>();
        }
    }

    public async Task<bool> CreateAsync(LicenseFeeDto fee)
    {
        try
        {
            var createDto = new
            {
                licence_description = fee.licence_description,
                licence_fee = fee.licence_fee
            };

            var response = await _httpClient.PostAsJsonAsync("api/licensefee", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating license fee");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int feeCode, LicenseFeeDto fee)
    {
        try
        {
            var updateDto = new
            {
                licence_fee_code = (short)feeCode,
                licence_description = fee.licence_description,
                licence_fee = fee.licence_fee
            };

            var response = await _httpClient.PutAsJsonAsync($"api/licensefee/{feeCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating license fee {FeeCode}", feeCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int feeCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/licensefee/{feeCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting license fee {FeeCode}", feeCode);
            return false;
        }
    }
}

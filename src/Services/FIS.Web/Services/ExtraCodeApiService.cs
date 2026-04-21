using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiExtraCodeResponse
{
    public short extra_code { get; set; }
    public string extra_description { get; set; } = string.Empty;
}

public class ExtraCodeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExtraCodeApiService> _logger;

    public ExtraCodeApiService(HttpClient httpClient, ILogger<ExtraCodeApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ExtraCodeDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiExtraCodeResponse>>("api/extracode");
            if (apiResponse == null) return new List<ExtraCodeDto>();

            return apiResponse.Select(item => new ExtraCodeDto
            {
                extra_code = item.extra_code,
                extra_description = item.extra_description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching extras");
            return new List<ExtraCodeDto>();
        }
    }

    public async Task<bool> CreateAsync(ExtraCodeDto extra)
    {
        try
        {
            var createDto = new
            {
                extra_description = extra.extra_description
            };

            var response = await _httpClient.PostAsJsonAsync("api/extracode", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating extra");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int extraCode, ExtraCodeDto extra)
    {
        try
        {
            var updateDto = new
            {
                extra_code = (short)extraCode,
                extra_description = extra.extra_description
            };

            var response = await _httpClient.PutAsJsonAsync($"api/extracode/{extraCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating extra {ExtraCode}", extraCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int extraCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/extracode/{extraCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting extra {ExtraCode}", extraCode);
            return false;
        }
    }
}

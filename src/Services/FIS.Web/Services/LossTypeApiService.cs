using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiLossTypeResponse
{
    public short loss_code { get; set; }
    public string loss_description { get; set; } = string.Empty;
}

public class LossTypeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LossTypeApiService> _logger;

    public LossTypeApiService(HttpClient httpClient, ILogger<LossTypeApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<LossTypeDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiLossTypeResponse>>("api/losstype");
            if (apiResponse == null) return new List<LossTypeDto>();

            return apiResponse.Select(item => new LossTypeDto
            {
                loss_code = item.loss_code,
                loss_description = item.loss_description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching loss types");
            return new List<LossTypeDto>();
        }
    }

    public async Task<bool> CreateAsync(LossTypeDto lossType)
    {
        try
        {
            var createDto = new
            {
                loss_description = lossType.loss_description
            };

            var response = await _httpClient.PostAsJsonAsync("api/losstype", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loss type");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int lossCode, LossTypeDto lossType)
    {
        try
        {
            var updateDto = new
            {
                loss_code = (short)lossCode,
                loss_description = lossType.loss_description
            };

            var response = await _httpClient.PutAsJsonAsync($"api/losstype/{lossCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loss type {LossCode}", lossCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int lossCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/losstype/{lossCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting loss type {LossCode}", lossCode);
            return false;
        }
    }
}

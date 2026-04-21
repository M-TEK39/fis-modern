using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiProvinceResponse
{
    public string? province_code { get; set; }
    public string? province_name { get; set; }
}

public class ProvinceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProvinceApiService> _logger;

    public ProvinceApiService(HttpClient httpClient, ILogger<ProvinceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ProvinceDto>> GetAllAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiProvinceResponse>>("api/province");
            if (apiResponse == null) return new List<ProvinceDto>();

            return apiResponse.Select(item => new ProvinceDto
            {
                province_code = item.province_code,
                province_name = item.province_name
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching provinces");
            return new List<ProvinceDto>();
        }
    }
}

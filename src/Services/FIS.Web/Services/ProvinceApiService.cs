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
    private readonly TokenService _tokenService;
    private readonly ILogger<ProvinceApiService> _logger;

    public ProvinceApiService(HttpClient httpClient, TokenService tokenService, ILogger<ProvinceApiService> logger)
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

    public async Task<List<ProvinceDto>> GetAllAsync()
    {
        try
        {
            AddAuthHeader();
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

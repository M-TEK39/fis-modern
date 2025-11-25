using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class TariffApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TariffApiService> _logger;

    public TariffApiService(HttpClient httpClient, ILogger<TariffApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TariffResultDto?> CalculateAsync(TariffRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Tariff/calculate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TariffResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating tariff");
            return null;
        }
    }
}

public record TariffRequestDto(int ContractCode);
public record TariffResultDto(decimal? Amount, string? Notes);

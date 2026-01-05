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

    public async Task<TariffResultDto?> GetContractTariffAsync(int contractCode, DateTime? checkDate = null, string tariffType = "Fixed")
    {
        try
        {
            var dateParam = checkDate?.ToString("o");
            var url = $"api/Tariff/contract/{contractCode}";
            if (dateParam != null)
            {
                url += $"?checkDate={Uri.EscapeDataString(dateParam)}&tariffType={Uri.EscapeDataString(tariffType)}";
            }

            var result = await _httpClient.GetFromJsonAsync<TariffResultDto>(url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching contract tariff {ContractCode}", contractCode);
            return null;
        }
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
public record TariffResultDto(decimal? Amount, string? Status, string? Notes);

using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class UnitOfMeasureApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UnitOfMeasureApiService> _logger;

    public UnitOfMeasureApiService(HttpClient httpClient, ILogger<UnitOfMeasureApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<UnitOfMeasureDto>> GetAllAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<UnitOfMeasureDto>>("api/UnitOfMeasure");
            return result ?? new List<UnitOfMeasureDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching units of measure");
            return new List<UnitOfMeasureDto>();
        }
    }
}

public record UnitOfMeasureDto(
    int UnitCode,
    string? Description,
    string? Abbreviation,
    string? Category
);

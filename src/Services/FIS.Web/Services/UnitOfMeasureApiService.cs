using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiUnitOfMeasureResponse
{
    public short unit_of_measure_code { get; set; }
    public string unit_description { get; set; } = string.Empty;
    public string? unit_abbreviation { get; set; }
    public string? unit_category { get; set; }
}

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
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiUnitOfMeasureResponse>>("api/UnitOfMeasure");
            if (apiResponse == null) return new List<UnitOfMeasureDto>();

            return apiResponse.Select(unit => new UnitOfMeasureDto
            {
                unit_of_measure_code = unit.unit_of_measure_code,
                unit_description = unit.unit_description,
                unit_abbreviation = unit.unit_abbreviation,
                unit_category = unit.unit_category
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching units of measure");
            return new List<UnitOfMeasureDto>();
        }
    }

    public async Task<bool> CreateAsync(UnitOfMeasureDto unit)
    {
        try
        {
            var createDto = new
            {
                unit_description = unit.unit_description,
                unit_abbreviation = unit.unit_abbreviation,
                unit_category = unit.unit_category
            };

            var response = await _httpClient.PostAsJsonAsync("api/UnitOfMeasure", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating unit of measure");
            return false;
        }
    }

    public async Task<bool> UpdateAsync(int unitCode, UnitOfMeasureDto unit)
    {
        try
        {
            var updateDto = new
            {
                unit_of_measure_code = (short)unitCode,
                unit_description = unit.unit_description,
                unit_abbreviation = unit.unit_abbreviation,
                unit_category = unit.unit_category
            };

            var response = await _httpClient.PutAsJsonAsync($"api/UnitOfMeasure/{unitCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating unit of measure {UnitCode}", unitCode);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int unitCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/UnitOfMeasure/{unitCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting unit of measure {UnitCode}", unitCode);
            return false;
        }
    }
}

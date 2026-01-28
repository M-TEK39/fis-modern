using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

internal class ApiClassResponse
{
    public short class_code { get; set; }
    public string description { get; set; } = string.Empty;
}

public class ClassApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClassApiService> _logger;

    public ClassApiService(HttpClient httpClient, ILogger<ClassApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ClassDto>> GetClassesAsync()
    {
        try
        {
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiClassResponse>>("api/class");
            if (apiResponse == null) return new List<ClassDto>();

            return apiResponse.Select(c => new ClassDto
            {
                class_code = c.class_code,
                class_description = c.description
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching classes");
            return new List<ClassDto>();
        }
    }

    public async Task<bool> CreateClassAsync(ClassDto classDto)
    {
        try
        {
            var createDto = new
            {
                description = classDto.class_description
            };

            var response = await _httpClient.PostAsJsonAsync("api/class", createDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating class");
            return false;
        }
    }

    public async Task<bool> UpdateClassAsync(int classCode, ClassDto classDto)
    {
        try
        {
            var updateDto = new
            {
                class_code = (short)classCode,
                description = classDto.class_description
            };

            var response = await _httpClient.PutAsJsonAsync($"api/class/{classCode}", updateDto);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating class {ClassCode}", classCode);
            return false;
        }
    }

    public async Task<bool> DeleteClassAsync(int classCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/class/{classCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting class {ClassCode}", classCode);
            return false;
        }
    }
}

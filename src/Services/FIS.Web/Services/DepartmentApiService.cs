using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

public class DepartmentApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DepartmentApiService> _logger;

    public DepartmentApiService(HttpClient httpClient, ILogger<DepartmentApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<DepartmentDto>>("api/departments");
            return result ?? new List<DepartmentDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching departments");
            return new List<DepartmentDto>();
        }
    }

    public async Task<DepartmentDto?> GetDepartmentAsync(int departmentCode)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DepartmentDto>(
                $"api/departments/{departmentCode}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching department {DepartmentCode}", departmentCode);
            return null;
        }
    }

    public async Task<bool> CreateDepartmentAsync(DepartmentDto department)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/departments", department);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating department");
            return false;
        }
    }

    public async Task<bool> UpdateDepartmentAsync(int departmentCode, DepartmentDto department)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"api/departments/{departmentCode}",
                department
            );
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating department {DepartmentCode}", departmentCode);
            return false;
        }
    }

    public async Task<bool> DeleteDepartmentAsync(int departmentCode)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/departments/{departmentCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting department {DepartmentCode}", departmentCode);
            return false;
        }
    }
}

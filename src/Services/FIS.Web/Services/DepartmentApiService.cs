using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

// Internal API response model for mapping
internal class ApiDepartmentResponse
{
    public short department_code { get; set; }
    public string? description { get; set; }
    public string? res_person { get; set; }
    public string? address1 { get; set; }
    public string? address2 { get; set; }
    public string? telephone { get; set; }
    public string? fax { get; set; }
    public string? email { get; set; }
}

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
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiDepartmentResponse>>("api/department");
            if (apiResponse == null) return new List<DepartmentDto>();
            
            return apiResponse.Select(dept => new DepartmentDto
            {
                department_code = dept.department_code,
                department_description = dept.description,
                contact_person = dept.res_person,
                telephone = dept.telephone,
                fax = dept.fax,
                email = dept.email,
                physical_address = dept.address1,
                postal_address = dept.address2
            }).ToList();
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
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiDepartmentResponse>(
                $"api/department/{departmentCode}"
            );
            if (apiResponse == null) return null;
            
            return new DepartmentDto
            {
                department_code = apiResponse.department_code,
                department_description = apiResponse.description,
                contact_person = apiResponse.res_person,
                telephone = apiResponse.telephone,
                fax = apiResponse.fax,
                email = apiResponse.email,
                physical_address = apiResponse.address1,
                postal_address = apiResponse.address2
            };
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
            // Map DepartmentDto to API expected format
            var createDepartmentDto = new
            {
                description = department.department_description,
                res_person = department.contact_person,
                address1 = department.physical_address,
                address2 = department.postal_address,
                telephone = department.telephone,
                fax = department.fax,
                email = department.email
            };
            
            var response = await _httpClient.PostAsJsonAsync("api/department", createDepartmentDto);
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
            // Map DepartmentDto to Department entity for API
            var departmentEntity = new
            {
                department_code = (short)departmentCode,
                description = department.department_description,
                res_person = department.contact_person,
                address1 = department.physical_address,
                address2 = department.postal_address,
                telephone = department.telephone,
                fax = department.fax,
                email = department.email
            };
            
            var response = await _httpClient.PutAsJsonAsync(
                $"api/department/{departmentCode}",
                departmentEntity
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
            var response = await _httpClient.DeleteAsync($"api/department/{departmentCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting department {DepartmentCode}", departmentCode);
            return false;
        }
    }
}

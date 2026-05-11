using System.Net.Http.Json;
using FIS.Web.Models;

namespace FIS.Web.Services;

// Internal API response model for mapping
internal class ApiDepartmentResponse
{
    public short DepartmentCode { get; set; }
    public short CompanyCode { get; set; }
    public string? Description { get; set; }
    public string? ResponsiblePerson { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? PostalCode { get; set; }
    public string? Telephone { get; set; }
    public string? Telephone2 { get; set; }
    public string? Fax { get; set; }
    public string? Fax2 { get; set; }
    public string? NetAddress { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? CellNumber { get; set; }
    public string? Notes { get; set; }
    public string? DepartmentAbbr { get; set; }
    public string? BasInstallationCode { get; set; }
    public bool DeptActive { get; set; }
    public string? CloEmail { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime? FinancialSystemActivateDate { get; set; }
    public short? DefaultSite { get; set; }
    public bool? ExportIsActive { get; set; }
    public DateTime? DateLastExported { get; set; }
    public int ServiceKilometres { get; set; }
    public byte ServiceYears { get; set; }
    public decimal OverheadPercentage { get; set; }
    public int? UserAccessCode { get; set; }
    public string? Comments { get; set; }
}

public class DepartmentApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;
    private readonly ILogger<DepartmentApiService> _logger;

    public DepartmentApiService(HttpClient httpClient, TokenService tokenService, ILogger<DepartmentApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        try
        {
            AddAuthHeader();
            var apiResponse = await _httpClient.GetFromJsonAsync<List<ApiDepartmentResponse>>("api/department");
            if (apiResponse == null) return new List<DepartmentDto>();
            
            return apiResponse.Select(dept => new DepartmentDto
            {
                department_code = dept.DepartmentCode,
                department_description = dept.Description,
                contact_person = dept.ResponsiblePerson,
                telephone = dept.Telephone,
                telephone2 = dept.Telephone2,
                fax = dept.Fax,
                fax2 = dept.Fax2,
                email = dept.NetAddress,
                physical_address = dept.Address1,
                physical_address_line2 = dept.Address2,
                physical_address_line3 = dept.Address3,
                postal_address = dept.Address2,
                postal_code = dept.PostalCode,
                net_address = dept.NetAddress,
                department_number = dept.DepartmentNumber,
                cell_number = dept.CellNumber,
                notes = dept.Notes,
                department_abbr = dept.DepartmentAbbr,
                bas_installation_code = dept.BasInstallationCode,
                dept_active = dept.DeptActive,
                clo_email = dept.CloEmail,
                financial_system_code = dept.FinancialSystemCode,
                financial_system_active = dept.FinancialSystemActive,
                financial_system_activate_date = dept.FinancialSystemActivateDate,
                default_site = dept.DefaultSite,
                export_is_active = dept.ExportIsActive,
                date_last_exported = dept.DateLastExported,
                service_kilometres = dept.ServiceKilometres,
                service_years = dept.ServiceYears,
                overhead_percentage = dept.OverheadPercentage,
                user_access_code = dept.UserAccessCode,
                comments = dept.Comments
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
            AddAuthHeader();
            var apiResponse = await _httpClient.GetFromJsonAsync<ApiDepartmentResponse>(
                $"api/department/{departmentCode}"
            );
            if (apiResponse == null) return null;
            
            return new DepartmentDto
            {
                department_code = apiResponse.DepartmentCode,
                department_description = apiResponse.Description,
                contact_person = apiResponse.ResponsiblePerson,
                telephone = apiResponse.Telephone,
                telephone2 = apiResponse.Telephone2,
                fax = apiResponse.Fax,
                fax2 = apiResponse.Fax2,
                email = apiResponse.NetAddress,
                physical_address = apiResponse.Address1,
                physical_address_line2 = apiResponse.Address2,
                physical_address_line3 = apiResponse.Address3,
                postal_address = apiResponse.Address2,
                postal_code = apiResponse.PostalCode,
                net_address = apiResponse.NetAddress,
                department_number = apiResponse.DepartmentNumber,
                cell_number = apiResponse.CellNumber,
                notes = apiResponse.Notes,
                department_abbr = apiResponse.DepartmentAbbr,
                bas_installation_code = apiResponse.BasInstallationCode,
                dept_active = apiResponse.DeptActive,
                clo_email = apiResponse.CloEmail,
                financial_system_code = apiResponse.FinancialSystemCode,
                financial_system_active = apiResponse.FinancialSystemActive,
                financial_system_activate_date = apiResponse.FinancialSystemActivateDate,
                default_site = apiResponse.DefaultSite,
                export_is_active = apiResponse.ExportIsActive,
                date_last_exported = apiResponse.DateLastExported,
                service_kilometres = apiResponse.ServiceKilometres,
                service_years = apiResponse.ServiceYears,
                overhead_percentage = apiResponse.OverheadPercentage,
                user_access_code = apiResponse.UserAccessCode,
                comments = apiResponse.Comments
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
            AddAuthHeader();
            // Map DepartmentDto to API expected format
            var createDepartmentDto = new
            {
                companyCode = (short)0,
                description = department.department_description,
                responsiblePerson = department.contact_person,
                address1 = department.physical_address,
                address2 = department.physical_address_line2,
                address3 = department.physical_address_line3,
                postalCode = department.postal_code,
                telephone = department.telephone,
                telephone2 = department.telephone2,
                fax = department.fax,
                fax2 = department.fax2,
                netAddress = department.net_address ?? department.email,
                departmentNumber = department.department_number,
                cellNumber = department.cell_number,
                notes = department.notes,
                departmentAbbr = department.department_abbr,
                basInstallationCode = department.bas_installation_code,
                deptActive = department.dept_active,
                cloEmail = department.clo_email,
                financialSystemCode = department.financial_system_code,
                financialSystemActive = department.financial_system_active,
                financialSystemActivateDate = department.financial_system_activate_date,
                defaultSite = department.default_site,
                exportIsActive = department.export_is_active,
                serviceKilometres = department.service_kilometres,
                serviceYears = department.service_years,
                overheadPercentage = department.overhead_percentage,
                userAccessCode = department.user_access_code,
                comments = department.comments
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
            AddAuthHeader();
            // Map DepartmentDto to Department entity for API
            var departmentEntity = new
            {
                departmentCode = (short)departmentCode,
                companyCode = (short)0,
                description = department.department_description,
                responsiblePerson = department.contact_person,
                address1 = department.physical_address,
                address2 = department.physical_address_line2,
                address3 = department.physical_address_line3,
                postalCode = department.postal_code,
                telephone = department.telephone,
                telephone2 = department.telephone2,
                fax = department.fax,
                fax2 = department.fax2,
                netAddress = department.net_address ?? department.email,
                departmentNumber = department.department_number,
                cellNumber = department.cell_number,
                notes = department.notes,
                departmentAbbr = department.department_abbr,
                basInstallationCode = department.bas_installation_code,
                deptActive = department.dept_active,
                cloEmail = department.clo_email,
                financialSystemCode = department.financial_system_code,
                financialSystemActive = department.financial_system_active,
                financialSystemActivateDate = department.financial_system_activate_date,
                defaultSite = department.default_site,
                exportIsActive = department.export_is_active,
                serviceKilometres = department.service_kilometres,
                serviceYears = department.service_years,
                overheadPercentage = department.overhead_percentage,
                userAccessCode = department.user_access_code,
                comments = department.comments
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
            AddAuthHeader();
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

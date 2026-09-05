using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

public class CreateDepartmentDto
{
    public short CompanyCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ResponsiblePerson { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Address3 { get; set; }
    public string? PostalCode { get; set; }
    public string? Telephone { get; set; }
    public string? Fax { get; set; }
    public string? NetAddress { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? CellNumber { get; set; }
    public string? Notes { get; set; }
    public string? DepartmentAbbr { get; set; }
    public string? BasInstallationCode { get; set; }
    public bool DeptActive { get; set; } = true;
    public string? CloEmail { get; set; }
    public string? Telephone2 { get; set; }
    public string? Fax2 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime? FinancialSystemActivateDate { get; set; }
    public short? DefaultSite { get; set; }
    public bool? ExportIsActive { get; set; }
    public int ServiceKilometres { get; set; }
    public byte ServiceYears { get; set; }
    public decimal OverheadPercentage { get; set; }
    public short? UserAccessCode { get; set; }
    public string? Comments { get; set; }
}

public class UpdateDepartmentDto : CreateDepartmentDto { }

public class DepartmentDto
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
    public string? Fax { get; set; }
    public string? NetAddress { get; set; }
    public string? DepartmentNumber { get; set; }
    public string? CellNumber { get; set; }
    public string? Notes { get; set; }
    public string? DepartmentAbbr { get; set; }
    public string? BasInstallationCode { get; set; }
    public bool DeptActive { get; set; }
    public string? CloEmail { get; set; }
    public string? Telephone2 { get; set; }
    public string? Fax2 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime? FinancialSystemActivateDate { get; set; }
    public short? DefaultSite { get; set; }
    public bool? ExportIsActive { get; set; }
    public DateTime? DateLastExported { get; set; }
    public int ServiceKilometres { get; set; }
    public byte ServiceYears { get; set; }
    public decimal OverheadPercentage { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
    public int? UserAccessCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public string? Comments { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentController : BaseApiController
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentController> _logger;

    public DepartmentController(IDepartmentRepository departmentRepository, ILogger<DepartmentController> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetDepartments()
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            // The legacy maintenance list includes inactive departments as well.
            // Keep that behavior so older records are not silently omitted.
            var departments = await _departmentRepository.GetAllAsync();
            var departmentDtos = departments.Select(d => new DepartmentDto
            {
                DepartmentCode = d.department_code,
                CompanyCode = d.company_code,
                Description = d.description,
                ResponsiblePerson = d.res_person,
                Address1 = d.address1,
                Address2 = d.address2,
                Address3 = d.address3,
                PostalCode = d.postal_code,
                Telephone = d.telephone,
                Fax = d.fax,
                NetAddress = d.net_address,
                DepartmentNumber = d.Department_number,
                CellNumber = d.cell_number,
                Notes = d.notes,
                DepartmentAbbr = d.department_abbr,
                BasInstallationCode = d.bas_installation_code,
                DeptActive = d.dept_active,
                CloEmail = d.clo_email,
                Telephone2 = d.telephone2,
                Fax2 = d.fax2,
                FinancialSystemCode = d.financial_system_code,
                FinancialSystemActive = d.financial_system_active,
                FinancialSystemActivateDate = d.financial_system_activate_date,
                DefaultSite = d.default_site,
                ExportIsActive = d.export_is_active,
                DateLastExported = d.date_last_exported,
                ServiceKilometres = d.Service_Kilometres,
                ServiceYears = d.Service_Years,
                OverheadPercentage = d.Overhead_Percentage,
                DateCreated = d.date_created,
                DateUpdated = d.date_updated,
                UserAccessCode = d.user_access_code,
                ModifiedByUserCode = d.modified_by_user_code,
                Comments = d.comments
            });
            return Ok(departmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetDepartment(int id)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var department = await _departmentRepository.GetByIdAsync(id);
            if (department == null)
            {
                return NotFound();
            }

            var departmentDto = new DepartmentDto
            {
                DepartmentCode = department.department_code,
                CompanyCode = department.company_code,
                Description = department.description,
                ResponsiblePerson = department.res_person,
                Address1 = department.address1,
                Address2 = department.address2,
                Address3 = department.address3,
                PostalCode = department.postal_code,
                Telephone = department.telephone,
                Fax = department.fax,
                NetAddress = department.net_address,
                DepartmentNumber = department.Department_number,
                CellNumber = department.cell_number,
                Notes = department.notes,
                DepartmentAbbr = department.department_abbr,
                BasInstallationCode = department.bas_installation_code,
                DeptActive = department.dept_active,
                CloEmail = department.clo_email,
                Telephone2 = department.telephone2,
                Fax2 = department.fax2,
                FinancialSystemCode = department.financial_system_code,
                FinancialSystemActive = department.financial_system_active,
                FinancialSystemActivateDate = department.financial_system_activate_date,
                DefaultSite = department.default_site,
                ExportIsActive = department.export_is_active,
                DateLastExported = department.date_last_exported,
                ServiceKilometres = department.Service_Kilometres,
                ServiceYears = department.Service_Years,
                OverheadPercentage = department.Overhead_Percentage,
                DateCreated = department.date_created,
                DateUpdated = department.date_updated,
                UserAccessCode = department.user_access_code,
                ModifiedByUserCode = department.modified_by_user_code,
                Comments = department.comments
            };

            return Ok(departmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving department with id {DepartmentId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetActiveDepartments()
    {
        try
        {
            var departments = await _departmentRepository.GetActiveDepartmentsAsync();
            return Ok(departments.Select(d => new DepartmentDto
            {
                DepartmentCode = d.department_code,
                CompanyCode = d.company_code,
                Description = d.description,
                ResponsiblePerson = d.res_person,
                Address1 = d.address1,
                Address2 = d.address2,
                Address3 = d.address3,
                PostalCode = d.postal_code,
                Telephone = d.telephone,
                Fax = d.fax,
                NetAddress = d.net_address,
                DepartmentNumber = d.Department_number,
                CellNumber = d.cell_number,
                Notes = d.notes,
                DepartmentAbbr = d.department_abbr,
                BasInstallationCode = d.bas_installation_code,
                DeptActive = d.dept_active,
                CloEmail = d.clo_email,
                Telephone2 = d.telephone2,
                Fax2 = d.fax2,
                FinancialSystemCode = d.financial_system_code,
                FinancialSystemActive = d.financial_system_active,
                FinancialSystemActivateDate = d.financial_system_activate_date,
                DefaultSite = d.default_site,
                ExportIsActive = d.export_is_active,
                DateLastExported = d.date_last_exported,
                ServiceKilometres = d.Service_Kilometres,
                ServiceYears = d.Service_Years,
                OverheadPercentage = d.Overhead_Percentage,
                DateCreated = d.date_created,
                DateUpdated = d.date_updated,
                UserAccessCode = d.user_access_code,
                ModifiedByUserCode = d.modified_by_user_code,
                Comments = d.comments
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active departments");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id:int}/delete-check")]
    public async Task<ActionResult<DepartmentDeleteCheck>> GetDeleteCheck(int id)
    {
        try
        {
            var department = await _departmentRepository.GetByIdAsync(id);
            if (department is null)
            {
                return NotFound();
            }

            return Ok(await _departmentRepository.GetDeleteCheckAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking department dependencies for {DepartmentId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("company/{companyCode}")]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetDepartmentsByCompany(int companyCode)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var departments = await _departmentRepository.GetByCompanyAsync(companyCode);
            var departmentDtos = departments.Select(d => new DepartmentDto
            {
                DepartmentCode = d.department_code,
                CompanyCode = d.company_code,
                Description = d.description,
                ResponsiblePerson = d.res_person,
                Address1 = d.address1,
                Address2 = d.address2,
                Address3 = d.address3,
                PostalCode = d.postal_code,
                Telephone = d.telephone,
                Fax = d.fax,
                NetAddress = d.net_address,
                DepartmentNumber = d.Department_number,
                CellNumber = d.cell_number,
                Notes = d.notes,
                DepartmentAbbr = d.department_abbr,
                BasInstallationCode = d.bas_installation_code,
                DeptActive = d.dept_active,
                CloEmail = d.clo_email,
                Telephone2 = d.telephone2,
                Fax2 = d.fax2,
                FinancialSystemCode = d.financial_system_code,
                FinancialSystemActive = d.financial_system_active,
                FinancialSystemActivateDate = d.financial_system_activate_date,
                DefaultSite = d.default_site,
                ExportIsActive = d.export_is_active,
                DateLastExported = d.date_last_exported,
                ServiceKilometres = d.Service_Kilometres,
                ServiceYears = d.Service_Years,
                OverheadPercentage = d.Overhead_Percentage,
                DateCreated = d.date_created,
                DateUpdated = d.date_updated,
                UserAccessCode = d.user_access_code,
                ModifiedByUserCode = d.modified_by_user_code,
                Comments = d.comments
            });
            return Ok(departmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments for company {CompanyCode}", companyCode);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> SearchDepartments([FromQuery] string searchTerm)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var departments = await _departmentRepository.SearchDepartmentsAsync(searchTerm);
            var departmentDtos = departments.Select(d => new DepartmentDto
            {
                DepartmentCode = d.department_code,
                CompanyCode = d.company_code,
                Description = d.description,
                ResponsiblePerson = d.res_person,
                Address1 = d.address1,
                Address2 = d.address2,
                Address3 = d.address3,
                PostalCode = d.postal_code,
                Telephone = d.telephone,
                Fax = d.fax,
                NetAddress = d.net_address,
                DepartmentNumber = d.Department_number,
                CellNumber = d.cell_number,
                Notes = d.notes,
                DepartmentAbbr = d.department_abbr,
                BasInstallationCode = d.bas_installation_code,
                DeptActive = d.dept_active,
                CloEmail = d.clo_email,
                Telephone2 = d.telephone2,
                Fax2 = d.fax2,
                FinancialSystemCode = d.financial_system_code,
                FinancialSystemActive = d.financial_system_active,
                FinancialSystemActivateDate = d.financial_system_activate_date,
                DefaultSite = d.default_site,
                ExportIsActive = d.export_is_active,
                DateLastExported = d.date_last_exported,
                ServiceKilometres = d.Service_Kilometres,
                ServiceYears = d.Service_Years,
                OverheadPercentage = d.Overhead_Percentage,
                DateCreated = d.date_created,
                DateUpdated = d.date_updated,
                UserAccessCode = d.user_access_code,
                ModifiedByUserCode = d.modified_by_user_code,
                Comments = d.comments
            });
            return Ok(departmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching departments with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> CreateDepartment([FromBody] CreateDepartmentDto createDepartmentDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var department = new Department
            {
                // The legacy add screen always writes company_code = 1 when
                // the caller does not provide a company value.
                company_code = createDepartmentDto.CompanyCode == 0 ? (short)1 : createDepartmentDto.CompanyCode,
                description = createDepartmentDto.Description,
                res_person = createDepartmentDto.ResponsiblePerson,
                address1 = createDepartmentDto.Address1,
                address2 = createDepartmentDto.Address2,
                address3 = createDepartmentDto.Address3,
                postal_code = createDepartmentDto.PostalCode,
                telephone = createDepartmentDto.Telephone,
                fax = createDepartmentDto.Fax,
                net_address = createDepartmentDto.NetAddress,
                Department_number = createDepartmentDto.DepartmentNumber,
                cell_number = createDepartmentDto.CellNumber,
                notes = createDepartmentDto.Notes,
                department_abbr = createDepartmentDto.DepartmentAbbr,
                bas_installation_code = createDepartmentDto.BasInstallationCode,
                dept_active = createDepartmentDto.DeptActive,
                clo_email = createDepartmentDto.CloEmail,
                telephone2 = createDepartmentDto.Telephone2,
                fax2 = createDepartmentDto.Fax2,
                financial_system_code = createDepartmentDto.FinancialSystemCode,
                financial_system_active = createDepartmentDto.FinancialSystemActive,
                financial_system_activate_date = createDepartmentDto.FinancialSystemActivateDate,
                default_site = createDepartmentDto.DefaultSite,
                export_is_active = createDepartmentDto.ExportIsActive,
                Service_Kilometres = createDepartmentDto.ServiceKilometres,
                Service_Years = createDepartmentDto.ServiceYears,
                Overhead_Percentage = createDepartmentDto.OverheadPercentage,
                user_access_code = createDepartmentDto.UserAccessCode ??
                    (currentUserId > 0 && currentUserId <= short.MaxValue ? (short)currentUserId : null),
                comments = createDepartmentDto.Comments
            };

            var createdDepartment = await _departmentRepository.CreateAsync(department, currentUserId);

            var departmentDto = new DepartmentDto
            {
                DepartmentCode = createdDepartment.department_code,
                CompanyCode = createdDepartment.company_code,
                Description = createdDepartment.description,
                ResponsiblePerson = createdDepartment.res_person,
                Address1 = createdDepartment.address1,
                Address2 = createdDepartment.address2,
                Address3 = createdDepartment.address3,
                PostalCode = createdDepartment.postal_code,
                Telephone = createdDepartment.telephone,
                Fax = createdDepartment.fax,
                NetAddress = createdDepartment.net_address,
                DepartmentNumber = createdDepartment.Department_number,
                CellNumber = createdDepartment.cell_number,
                Notes = createdDepartment.notes,
                DepartmentAbbr = createdDepartment.department_abbr,
                BasInstallationCode = createdDepartment.bas_installation_code,
                DeptActive = createdDepartment.dept_active,
                CloEmail = createdDepartment.clo_email,
                Telephone2 = createdDepartment.telephone2,
                Fax2 = createdDepartment.fax2,
                FinancialSystemCode = createdDepartment.financial_system_code,
                FinancialSystemActive = createdDepartment.financial_system_active,
                FinancialSystemActivateDate = createdDepartment.financial_system_activate_date,
                DefaultSite = createdDepartment.default_site,
                ExportIsActive = createdDepartment.export_is_active,
                DateLastExported = createdDepartment.date_last_exported,
                ServiceKilometres = createdDepartment.Service_Kilometres,
                ServiceYears = createdDepartment.Service_Years,
                OverheadPercentage = createdDepartment.Overhead_Percentage,
                DateCreated = createdDepartment.date_created,
                DateUpdated = createdDepartment.date_updated,
                UserAccessCode = createdDepartment.user_access_code,
                ModifiedByUserCode = createdDepartment.modified_by_user_code,
                Comments = createdDepartment.comments
            };

            return CreatedAtAction(nameof(GetDepartment), new { id = createdDepartment.department_code }, departmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating department");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DepartmentDto>> UpdateDepartment(int id, [FromBody] UpdateDepartmentDto updateDepartmentDto)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingDepartment = await _departmentRepository.GetByIdAsync(id);
            if (existingDepartment == null)
            {
                return NotFound();
            }

            if (existingDepartment.dept_active && !updateDepartmentDto.DeptActive &&
                await _departmentRepository.HasActiveContractsAsync(id))
            {
                return Conflict(new
                {
                    message = "This department cannot be deactivated while active vehicle contracts are assigned to it."
                });
            }

            existingDepartment.company_code = updateDepartmentDto.CompanyCode == 0
                ? existingDepartment.company_code
                : updateDepartmentDto.CompanyCode;
            existingDepartment.description = updateDepartmentDto.Description;
            existingDepartment.res_person = updateDepartmentDto.ResponsiblePerson;
            existingDepartment.address1 = updateDepartmentDto.Address1;
            existingDepartment.address2 = updateDepartmentDto.Address2;
            existingDepartment.address3 = updateDepartmentDto.Address3;
            existingDepartment.postal_code = updateDepartmentDto.PostalCode;
            existingDepartment.telephone = updateDepartmentDto.Telephone;
            existingDepartment.fax = updateDepartmentDto.Fax;
            existingDepartment.net_address = updateDepartmentDto.NetAddress;
            existingDepartment.Department_number = updateDepartmentDto.DepartmentNumber;
            existingDepartment.cell_number = updateDepartmentDto.CellNumber;
            existingDepartment.notes = updateDepartmentDto.Notes;
            existingDepartment.department_abbr = updateDepartmentDto.DepartmentAbbr;
            existingDepartment.bas_installation_code = updateDepartmentDto.BasInstallationCode;
            existingDepartment.dept_active = updateDepartmentDto.DeptActive;
            existingDepartment.clo_email = updateDepartmentDto.CloEmail;
            existingDepartment.telephone2 = updateDepartmentDto.Telephone2;
            existingDepartment.fax2 = updateDepartmentDto.Fax2;
            existingDepartment.financial_system_code = updateDepartmentDto.FinancialSystemCode;
            existingDepartment.financial_system_active = updateDepartmentDto.FinancialSystemActive;
            existingDepartment.financial_system_activate_date = updateDepartmentDto.FinancialSystemActivateDate;
            existingDepartment.default_site = updateDepartmentDto.DefaultSite;
            existingDepartment.export_is_active = updateDepartmentDto.ExportIsActive;
            existingDepartment.Service_Kilometres = updateDepartmentDto.ServiceKilometres;
            existingDepartment.Service_Years = updateDepartmentDto.ServiceYears;
            existingDepartment.Overhead_Percentage = updateDepartmentDto.OverheadPercentage;
            if (updateDepartmentDto.UserAccessCode.HasValue)
            {
                existingDepartment.user_access_code = updateDepartmentDto.UserAccessCode;
            }
            existingDepartment.comments = updateDepartmentDto.Comments;

            await _departmentRepository.UpdateAsync(existingDepartment, currentUserId);

            var departmentDto = new DepartmentDto
            {
                DepartmentCode = existingDepartment.department_code,
                CompanyCode = existingDepartment.company_code,
                Description = existingDepartment.description,
                ResponsiblePerson = existingDepartment.res_person,
                Address1 = existingDepartment.address1,
                Address2 = existingDepartment.address2,
                Address3 = existingDepartment.address3,
                PostalCode = existingDepartment.postal_code,
                Telephone = existingDepartment.telephone,
                Fax = existingDepartment.fax,
                NetAddress = existingDepartment.net_address,
                DepartmentNumber = existingDepartment.Department_number,
                CellNumber = existingDepartment.cell_number,
                Notes = existingDepartment.notes,
                DepartmentAbbr = existingDepartment.department_abbr,
                BasInstallationCode = existingDepartment.bas_installation_code,
                DeptActive = existingDepartment.dept_active,
                CloEmail = existingDepartment.clo_email,
                Telephone2 = existingDepartment.telephone2,
                Fax2 = existingDepartment.fax2,
                FinancialSystemCode = existingDepartment.financial_system_code,
                FinancialSystemActive = existingDepartment.financial_system_active,
                FinancialSystemActivateDate = existingDepartment.financial_system_activate_date,
                DefaultSite = existingDepartment.default_site,
                ExportIsActive = existingDepartment.export_is_active,
                DateLastExported = existingDepartment.date_last_exported,
                ServiceKilometres = existingDepartment.Service_Kilometres,
                ServiceYears = existingDepartment.Service_Years,
                OverheadPercentage = existingDepartment.Overhead_Percentage,
                DateCreated = existingDepartment.date_created,
                DateUpdated = existingDepartment.date_updated,
                UserAccessCode = existingDepartment.user_access_code,
                ModifiedByUserCode = existingDepartment.modified_by_user_code,
                Comments = existingDepartment.comments
            };

            return Ok(departmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating department with id {DepartmentId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteDepartment(int id)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingDepartment = await _departmentRepository.GetByIdAsync(id);
            if (existingDepartment == null)
            {
                return NotFound();
            }

            var deleteCheck = await _departmentRepository.GetDeleteCheckAsync(id);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(new
                {
                    message = "This department cannot be deleted until its sites are removed and its logsheets are rectified.",
                    siteCount = deleteCheck.SiteCount,
                    logsheetCount = deleteCheck.LogsheetCount
                });
            }

            await _departmentRepository.DeleteAsync(id, currentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting department with id {DepartmentId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}

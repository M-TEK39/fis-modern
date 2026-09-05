using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

public class CreateSiteDto
{
    public short? DepartmentCode { get; set; }
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
    public string? MapReference { get; set; }
    public string? MapDescription { get; set; }
    public string? CellNumber { get; set; }
    public bool SiteActive { get; set; } = true;
    public string? Telephone2 { get; set; }
    public string? Fax1 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime? FinancialSystemActivateDate { get; set; }
    public bool? ExportIsActive { get; set; }
    public DateTime? DateLastExported { get; set; }
    public int ServiceKilometres { get; set; }
    public byte ServiceYears { get; set; }
    public decimal OverheadPercentage { get; set; }
    public string? ProvinceCode { get; set; }
    public string? Notes { get; set; }
    public int? UserAccessCode { get; set; }
}

public class UpdateSiteDto : CreateSiteDto { }

public class SiteDto
{
    public short SiteCode { get; set; }
    public short? DepartmentCode { get; set; }
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
    public string? MapReference { get; set; }
    public string? MapDescription { get; set; }
    public string? CellNumber { get; set; }
    public bool SiteActive { get; set; }
    public string? Telephone2 { get; set; }
    public string? Fax1 { get; set; }
    public byte? FinancialSystemCode { get; set; }
    public bool? FinancialSystemActive { get; set; }
    public DateTime? FinancialSystemActivateDate { get; set; }
    public bool? ExportIsActive { get; set; }
    public DateTime? DateLastExported { get; set; }
    public int ServiceKilometres { get; set; }
    public byte ServiceYears { get; set; }
    public decimal OverheadPercentage { get; set; }
    public string? ProvinceCode { get; set; }
    public string? Notes { get; set; }
    public int? UserAccessCode { get; set; }
    public int? ModifiedByUserCode { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SiteController : BaseApiController
{
    private readonly ISiteRepository _siteRepository;
    private readonly ILogger<SiteController> _logger;

    public SiteController(ISiteRepository siteRepository, ILogger<SiteController> logger)
    {
        _siteRepository = siteRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SiteDto>>> GetSites()
    {
        try
        {
            var sites = await _siteRepository.GetActiveSitesAsync();
            return Ok(sites.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sites");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SiteDto>> GetSite(short id)
    {
        try
        {
            var site = await _siteRepository.GetByIdAsync(id);
            return site is null ? NotFound() : Ok(MapToDto(site));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving site with id {SiteId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<SiteDto>>> GetActiveSites()
        => await GetSites();

    [HttpGet("{id:int}/delete-check")]
    public async Task<ActionResult<SiteDeleteCheck>> GetDeleteCheck(short id)
    {
        try
        {
            if (await _siteRepository.GetByIdAsync(id) is null)
            {
                return NotFound();
            }

            return Ok(await _siteRepository.GetDeleteCheckAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking site dependencies for {SiteId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<SiteDto>> CreateSite([FromBody] CreateSiteDto dto)
    {
        try
        {
            var validationError = ValidateWriteDto(dto);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var currentUserId = GetCurrentUserId();
            var site = ApplyWriteDto(new Site(), dto, currentUserId);
            var createdSite = await _siteRepository.CreateAsync(site, currentUserId);
            var response = MapToDto(createdSite);
            return CreatedAtAction(nameof(GetSite), new { id = createdSite.Site_code }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating site");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SiteDto>> UpdateSite(short id, [FromBody] UpdateSiteDto dto)
    {
        try
        {
            var validationError = ValidateWriteDto(dto);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var existingSite = await _siteRepository.GetByIdAsync(id);
            if (existingSite is null)
            {
                return NotFound();
            }

            if (existingSite.site_active != dto.SiteActive &&
                (string.IsNullOrWhiteSpace(dto.Notes) || string.Equals(existingSite.notes?.Trim(), dto.Notes.Trim(), StringComparison.Ordinal)))
            {
                return BadRequest(new { message = "A new note is required when changing the site active status." });
            }

            if (existingSite.site_active && !dto.SiteActive && await _siteRepository.HasActiveContractsAsync(id))
            {
                return Conflict(new
                {
                    message = "This site cannot be deactivated while active vehicle contracts are assigned to it."
                });
            }

            var currentUserId = GetCurrentUserId();
            ApplyWriteDto(existingSite, dto, currentUserId);
            await _siteRepository.UpdateAsync(existingSite, currentUserId);
            return Ok(MapToDto(existingSite));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating site with id {SiteId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteSite(short id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (await _siteRepository.GetByIdAsync(id) is null)
            {
                return NotFound();
            }

            var deleteCheck = await _siteRepository.GetDeleteCheckAsync(id);
            if (!deleteCheck.CanDelete)
            {
                return Conflict(new
                {
                    message = "Contracts issued to this site must be changed before deleting it.",
                    contractCount = deleteCheck.ContractCount
                });
            }

            await _siteRepository.DeleteAsync(id, currentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting site with id {SiteId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private static Site ApplyWriteDto(Site site, CreateSiteDto dto, int currentUserId)
    {
        site.Depatrment_code = dto.DepartmentCode;
        site.description = dto.Description.Trim();
        site.res_person = Clean(dto.ResponsiblePerson);
        site.address1 = Clean(dto.Address1);
        site.address2 = Clean(dto.Address2);
        site.address3 = Clean(dto.Address3);
        site.postal_code = Clean(dto.PostalCode);
        site.telephone = Clean(dto.Telephone);
        site.fax = Clean(dto.Fax);
        site.net_address = Clean(dto.NetAddress);
        site.Department_number = Clean(dto.DepartmentNumber);
        site.Map_reference = Clean(dto.MapReference);
        site.Map_description = Clean(dto.MapDescription);
        site.cell_number = Clean(dto.CellNumber);
        site.site_active = dto.SiteActive;
        site.telephone2 = Clean(dto.Telephone2);
        site.fax1 = Clean(dto.Fax1);
        site.financial_system_code = dto.FinancialSystemCode;
        site.financial_system_active = dto.FinancialSystemActive;
        site.financial_system_activate_date = dto.FinancialSystemActivateDate;
        site.export_is_active = dto.ExportIsActive;
        site.date_last_exported = dto.DateLastExported;
        site.Service_Kilometres = dto.ServiceKilometres;
        site.Service_Years = dto.ServiceYears;
        site.Overhead_Percentage = dto.OverheadPercentage;
        site.province_code = ParseProvinceCode(dto.ProvinceCode);
        site.notes = Clean(dto.Notes);
        if (dto.UserAccessCode.HasValue)
        {
            site.user_access_code = dto.UserAccessCode;
        }
        else if (site.user_access_code is null && currentUserId > 0 && currentUserId <= short.MaxValue)
        {
            site.user_access_code = currentUserId;
        }

        return site;
    }

    private static string? ValidateWriteDto(CreateSiteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length > 75)
        {
            return "Site description is required and must be 75 characters or fewer.";
        }

        if (dto.DepartmentCode is null or <= 0)
        {
            return "A department is required.";
        }

        if (string.IsNullOrWhiteSpace(dto.DepartmentNumber) || dto.DepartmentNumber.Trim().Length != 7)
        {
            return "Department number must be 7 characters long.";
        }

        if (dto.PostalCode is not null && dto.PostalCode.Any(character => !char.IsDigit(character)))
        {
            return "Postal code must contain numbers only.";
        }

        if (dto.CellNumber?.Length > 15)
        {
            return "Cell number must be 15 characters or fewer.";
        }

        if (dto.UserAccessCode is < 0 or > short.MaxValue)
        {
            return "User access code is outside the supported range.";
        }

        return null;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SiteDto MapToDto(Site site)
        => new()
        {
            SiteCode = site.Site_code,
            DepartmentCode = site.Depatrment_code,
            Description = site.description,
            ResponsiblePerson = site.res_person,
            Address1 = site.address1,
            Address2 = site.address2,
            Address3 = site.address3,
            PostalCode = site.postal_code,
            Telephone = site.telephone,
            Fax = site.fax,
            NetAddress = site.net_address,
            DepartmentNumber = site.Department_number,
            MapReference = site.Map_reference,
            MapDescription = site.Map_description,
            CellNumber = site.cell_number,
            SiteActive = site.site_active,
            Telephone2 = site.telephone2,
            Fax1 = site.fax1,
            FinancialSystemCode = site.financial_system_code,
            FinancialSystemActive = site.financial_system_active,
            FinancialSystemActivateDate = site.financial_system_activate_date,
            ExportIsActive = site.export_is_active,
            DateLastExported = site.date_last_exported,
            ServiceKilometres = site.Service_Kilometres,
            ServiceYears = site.Service_Years,
            OverheadPercentage = site.Overhead_Percentage,
            ProvinceCode = FormatProvinceCode(site.province_code),
            Notes = site.notes,
            UserAccessCode = site.user_access_code,
            ModifiedByUserCode = site.modified_by_user_code,
            DateCreated = site.date_created,
            DateUpdated = site.date_updated
        };

    private static string? FormatProvinceCode(byte? provinceCode)
        => provinceCode?.ToString(CultureInfo.InvariantCulture);

    private static byte? ParseProvinceCode(string? provinceCode)
        => byte.TryParse(provinceCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}

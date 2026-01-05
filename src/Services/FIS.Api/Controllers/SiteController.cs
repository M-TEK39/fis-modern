using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers
{
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
        public DateTime DateCreated { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SiteController : ControllerBase
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
                var siteDtos = sites.Select(s => new SiteDto
                {
                    SiteCode = s.Site_code,
                    DepartmentCode = s.Depatrment_code,
                    Description = s.description,
                    ResponsiblePerson = s.res_person,
                    Address1 = s.address1,
                    Address2 = s.address2,
                    Address3 = s.address3,
                    PostalCode = s.postal_code,
                    Telephone = s.telephone,
                    Fax = s.fax,
                    NetAddress = s.net_address,
                    DepartmentNumber = s.Department_number,
                    MapReference = s.Map_reference,
                    MapDescription = s.Map_description,
                    CellNumber = s.cell_number,
                    SiteActive = s.site_active,
                    Telephone2 = s.telephone2,
                    Fax1 = s.fax1,
                    FinancialSystemCode = s.financial_system_code,
                    FinancialSystemActive = s.financial_system_active,
                    DateCreated = s.date_created,
                });
                return Ok(siteDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sites");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SiteDto>> GetSite(short id)
        {
            try
            {
                var site = await _siteRepository.GetByIdAsync(id);
                if (site == null)
                {
                    return NotFound();
                }

                var siteDto = new SiteDto
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
                    DateCreated = site.date_created,
                };

                return Ok(siteDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving site with id {SiteId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<SiteDto>>> GetActiveSites()
        {
            try
            {
                var sites = await _siteRepository.GetActiveSitesAsync();
                var siteDtos = sites.Select(s => new SiteDto
                {
                    SiteCode = s.Site_code,
                    DepartmentCode = s.Depatrment_code,
                    Description = s.description,
                    ResponsiblePerson = s.res_person,
                    Address1 = s.address1,
                    Address2 = s.address2,
                    Address3 = s.address3,
                    PostalCode = s.postal_code,
                    Telephone = s.telephone,
                    Fax = s.fax,
                    NetAddress = s.net_address,
                    DepartmentNumber = s.Department_number,
                    MapReference = s.Map_reference,
                    MapDescription = s.Map_description,
                    CellNumber = s.cell_number,
                    SiteActive = s.site_active,
                    Telephone2 = s.telephone2,
                    Fax1 = s.fax1,
                    FinancialSystemCode = s.financial_system_code,
                    FinancialSystemActive = s.financial_system_active,
                    DateCreated = s.date_created,
                });
                return Ok(siteDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active sites");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost]
        public async Task<ActionResult<SiteDto>> CreateSite([FromBody] CreateSiteDto createSiteDto)
        {
            try
            {
                var site = new Site
                {
                    Depatrment_code = createSiteDto.DepartmentCode,
                    description = createSiteDto.Description,
                    res_person = createSiteDto.ResponsiblePerson,
                    address1 = createSiteDto.Address1,
                    address2 = createSiteDto.Address2,
                    address3 = createSiteDto.Address3,
                    postal_code = createSiteDto.PostalCode,
                    telephone = createSiteDto.Telephone,
                    fax = createSiteDto.Fax,
                    net_address = createSiteDto.NetAddress,
                    Department_number = createSiteDto.DepartmentNumber,
                    Map_reference = createSiteDto.MapReference,
                    Map_description = createSiteDto.MapDescription,
                    cell_number = createSiteDto.CellNumber,
                    site_active = createSiteDto.SiteActive,
                    telephone2 = createSiteDto.Telephone2,
                    fax1 = createSiteDto.Fax1,
                    financial_system_code = createSiteDto.FinancialSystemCode,
                    financial_system_active = createSiteDto.FinancialSystemActive,
                    date_created = DateTime.Now,
                };

                var createdSite = await _siteRepository.CreateAsync(site);

                var siteDto = new SiteDto
                {
                    SiteCode = createdSite.Site_code,
                    DepartmentCode = createdSite.Depatrment_code,
                    Description = createdSite.description,
                    ResponsiblePerson = createdSite.res_person,
                    Address1 = createdSite.address1,
                    Address2 = createdSite.address2,
                    Address3 = createdSite.address3,
                    PostalCode = createdSite.postal_code,
                    Telephone = createdSite.telephone,
                    Fax = createdSite.fax,
                    NetAddress = createdSite.net_address,
                    DepartmentNumber = createdSite.Department_number,
                    MapReference = createdSite.Map_reference,
                    MapDescription = createdSite.Map_description,
                    CellNumber = createdSite.cell_number,
                    SiteActive = createdSite.site_active,
                    Telephone2 = createdSite.telephone2,
                    Fax1 = createdSite.fax1,
                    FinancialSystemCode = createdSite.financial_system_code,
                    FinancialSystemActive = createdSite.financial_system_active,
                    DateCreated = createdSite.date_created,
                };

                return CreatedAtAction(
                    nameof(GetSite),
                    new { id = createdSite.Site_code },
                    siteDto
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating site");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SiteDto>> UpdateSite(
            short id,
            [FromBody] UpdateSiteDto updateSiteDto
        )
        {
            try
            {
                var existingSite = await _siteRepository.GetByIdAsync(id);
                if (existingSite == null)
                {
                    return NotFound();
                }

                existingSite.Depatrment_code = updateSiteDto.DepartmentCode;
                existingSite.description = updateSiteDto.Description;
                existingSite.res_person = updateSiteDto.ResponsiblePerson;
                existingSite.address1 = updateSiteDto.Address1;
                existingSite.address2 = updateSiteDto.Address2;
                existingSite.address3 = updateSiteDto.Address3;
                existingSite.postal_code = updateSiteDto.PostalCode;
                existingSite.telephone = updateSiteDto.Telephone;
                existingSite.fax = updateSiteDto.Fax;
                existingSite.net_address = updateSiteDto.NetAddress;
                existingSite.Department_number = updateSiteDto.DepartmentNumber;
                existingSite.Map_reference = updateSiteDto.MapReference;
                existingSite.Map_description = updateSiteDto.MapDescription;
                existingSite.cell_number = updateSiteDto.CellNumber;
                existingSite.site_active = updateSiteDto.SiteActive;
                existingSite.telephone2 = updateSiteDto.Telephone2;
                existingSite.fax1 = updateSiteDto.Fax1;
                existingSite.financial_system_code = updateSiteDto.FinancialSystemCode;
                existingSite.financial_system_active = updateSiteDto.FinancialSystemActive;

                await _siteRepository.UpdateAsync(existingSite);

                var siteDto = new SiteDto
                {
                    SiteCode = existingSite.Site_code,
                    DepartmentCode = existingSite.Depatrment_code,
                    Description = existingSite.description,
                    ResponsiblePerson = existingSite.res_person,
                    Address1 = existingSite.address1,
                    Address2 = existingSite.address2,
                    Address3 = existingSite.address3,
                    PostalCode = existingSite.postal_code,
                    Telephone = existingSite.telephone,
                    Fax = existingSite.fax,
                    NetAddress = existingSite.net_address,
                    DepartmentNumber = existingSite.Department_number,
                    MapReference = existingSite.Map_reference,
                    MapDescription = existingSite.Map_description,
                    CellNumber = existingSite.cell_number,
                    SiteActive = existingSite.site_active,
                    Telephone2 = existingSite.telephone2,
                    Fax1 = existingSite.fax1,
                    FinancialSystemCode = existingSite.financial_system_code,
                    FinancialSystemActive = existingSite.financial_system_active,
                    DateCreated = existingSite.date_created,
                };

                return Ok(siteDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating site with id {SiteId}", id);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSite(short id)
        {
            try
            {
                var existingSite = await _siteRepository.GetByIdAsync(id);
                if (existingSite == null)
                {
                    return NotFound();
                }

                await _siteRepository.DeleteAsync(id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting site with id {SiteId}", id);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FIS.Api.Controllers;

public class CreateDriverDto
{
    public int SiteCode { get; set; }
    public int DriverLicenceTypeId { get; set; }
    public string DriverSurname { get; set; } = string.Empty;
    public string DriverFirstname { get; set; } = string.Empty;
    public string? DriverSAId { get; set; }
    public string? DriverPassportNumber { get; set; }
    public string? DriverPersonalNumber { get; set; }
    public string? DriverContractNumber { get; set; }
    public string? DriverLicenceNumber { get; set; }
    public DateTime DriverLicenceIssueDate { get; set; }
    public DateTime DriverLicenceLastVerifiedDate { get; set; }
    public bool DriverHasPDP { get; set; }
    public DateTime? DriverPDPExpiryDate { get; set; }
    public DateTime? DriverLicenceExpiryDate { get; set; }
    public bool DriverActive { get; set; } = true;
}

public class UpdateDriverDto : CreateDriverDto { }

public class DriverDto
{
    public int SiteDriverCode { get; set; }
    public int SiteCode { get; set; }
    public int DriverLicenceTypeId { get; set; }
    public string? DriverSurname { get; set; }
    public string? DriverFirstname { get; set; }
    public string? DriverSAId { get; set; }
    public string? DriverPassportNumber { get; set; }
    public string? DriverPersonalNumber { get; set; }
    public string? DriverContractNumber { get; set; }
    public string? DriverLicenceNumber { get; set; }
    public DateTime DriverLicenceIssueDate { get; set; }
    public DateTime DriverLicenceLastVerifiedDate { get; set; }
    public bool DriverHasPDP { get; set; }
    public DateTime? DriverPDPExpiryDate { get; set; }
    public DateTime? DriverLicenceExpiryDate { get; set; }
    public bool DriverActive { get; set; }
    public string FullName => $"{DriverFirstname} {DriverSurname}".Trim();
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Driver and Authoriser Management,SystemAdministrator,System Administrator")]
public class DriverController : BaseApiController
{
    private readonly IDriverRepository _driverRepository;
    private readonly ISiteRepository _siteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<DriverController> _logger;

    public DriverController(
        IDriverRepository driverRepository,
        ISiteRepository siteRepository,
        FisDbContext context,
        ILogger<DriverController> logger
    )
    {
        _driverRepository = driverRepository;
        _siteRepository = siteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers()
    {
        try
        {
            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var drivers = FilterByAllowedSites(
                await _driverRepository.GetActiveDriversAsync(),
                allowedSites
            );
            var driverDtos = drivers.Select(d => new DriverDto
            {
                SiteDriverCode = d.site_driver_code,
                SiteCode = d.site_code,
                DriverLicenceTypeId = d.driver_licence_type_id,
                DriverSurname = d.driver_surname,
                DriverFirstname = d.driver_firstname,
                DriverSAId = d.driver_SA_id,
                DriverPassportNumber = d.driver_passportnumber,
                DriverPersonalNumber = d.driver_persalnumber,
                DriverContractNumber = d.driver_contractnumber,
                DriverLicenceNumber = d.driver_licence_number,
                DriverLicenceIssueDate = d.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = d.driver_licence_lastVerifiedDate,
                DriverHasPDP = d.driver_hasPDP,
                DriverPDPExpiryDate = d.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = d.driver_licence_ExpiryDate,
                DriverActive = d.driver_active,
            });
            return Ok(driverDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving drivers");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DriverDto>> GetDriver(string id)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var driver = await _driverRepository.GetByIdAsync(id);
            if (driver == null)
            {
                return NotFound();
            }

            if (!await IsSiteAllowedAsync(driver.site_code))
                return Forbid();

            var driverDto = new DriverDto
            {
                SiteDriverCode = driver.site_driver_code,
                SiteCode = driver.site_code,
                DriverLicenceTypeId = driver.driver_licence_type_id,
                DriverSurname = driver.driver_surname,
                DriverFirstname = driver.driver_firstname,
                DriverSAId = driver.driver_SA_id,
                DriverPassportNumber = driver.driver_passportnumber,
                DriverPersonalNumber = driver.driver_persalnumber,
                DriverContractNumber = driver.driver_contractnumber,
                DriverLicenceNumber = driver.driver_licence_number,
                DriverLicenceIssueDate = driver.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = driver.driver_licence_lastVerifiedDate,
                DriverHasPDP = driver.driver_hasPDP,
                DriverPDPExpiryDate = driver.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = driver.driver_licence_ExpiryDate,
                DriverActive = driver.driver_active,
            };

            return Ok(driverDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving driver with id {DriverId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetActiveDrivers()
    {
        return await GetDrivers(); // Same as default behavior
    }

    [HttpGet("licence/{licenceNumber}")]
    public async Task<ActionResult<DriverDto>> GetDriverByLicence(string licenceNumber)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var driver = await _driverRepository.GetByLicenceNumberAsync(licenceNumber);
            if (driver == null)
            {
                return NotFound();
            }

            if (!await IsSiteAllowedAsync(driver.site_code))
                return Forbid();

            var driverDto = new DriverDto
            {
                SiteDriverCode = driver.site_driver_code,
                SiteCode = driver.site_code,
                DriverLicenceTypeId = driver.driver_licence_type_id,
                DriverSurname = driver.driver_surname,
                DriverFirstname = driver.driver_firstname,
                DriverSAId = driver.driver_SA_id,
                DriverPassportNumber = driver.driver_passportnumber,
                DriverPersonalNumber = driver.driver_persalnumber,
                DriverContractNumber = driver.driver_contractnumber,
                DriverLicenceNumber = driver.driver_licence_number,
                DriverLicenceIssueDate = driver.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = driver.driver_licence_lastVerifiedDate,
                DriverHasPDP = driver.driver_hasPDP,
                DriverPDPExpiryDate = driver.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = driver.driver_licence_ExpiryDate,
                DriverActive = driver.driver_active,
            };

            return Ok(driverDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving driver with licence number {LicenceNumber}",
                licenceNumber
            );
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<DriverDto>>> SearchDrivers(
        [FromQuery] string searchTerm
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var allowedSites = await ResolveAllowedSiteCodesAsync();
            var drivers = FilterByAllowedSites(
                await _driverRepository.SearchDriversAsync(searchTerm),
                allowedSites
            );
            var driverDtos = drivers.Select(d => new DriverDto
            {
                SiteDriverCode = d.site_driver_code,
                SiteCode = d.site_code,
                DriverLicenceTypeId = d.driver_licence_type_id,
                DriverSurname = d.driver_surname,
                DriverFirstname = d.driver_firstname,
                DriverSAId = d.driver_SA_id,
                DriverPassportNumber = d.driver_passportnumber,
                DriverPersonalNumber = d.driver_persalnumber,
                DriverContractNumber = d.driver_contractnumber,
                DriverLicenceNumber = d.driver_licence_number,
                DriverLicenceIssueDate = d.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = d.driver_licence_lastVerifiedDate,
                DriverHasPDP = d.driver_hasPDP,
                DriverPDPExpiryDate = d.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = d.driver_licence_ExpiryDate,
                DriverActive = d.driver_active,
            });
            return Ok(driverDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching drivers with term {SearchTerm}", searchTerm);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    [Authorize(Roles = "Driver and Authoriser Management,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<DriverDto>> CreateDriver(
        [FromBody] CreateDriverDto createDriverDto
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();
            if (!await IsSiteAllowedAsync(createDriverDto.SiteCode))
                return Forbid();
            if (await ValidateNoDuplicateIdentityAsync(createDriverDto) is { } duplicateError)
                return Conflict(new { error = duplicateError });

            var driver = new Driver
            {
                site_code = createDriverDto.SiteCode,
                driver_licence_type_id = createDriverDto.DriverLicenceTypeId,
                driver_surname = createDriverDto.DriverSurname,
                driver_firstname = createDriverDto.DriverFirstname,
                driver_SA_id = createDriverDto.DriverSAId,
                driver_passportnumber = createDriverDto.DriverPassportNumber,
                driver_persalnumber = createDriverDto.DriverPersonalNumber,
                driver_contractnumber = createDriverDto.DriverContractNumber,
                driver_licence_number = createDriverDto.DriverLicenceNumber,
                driver_licence_issuedate = createDriverDto.DriverLicenceIssueDate,
                driver_licence_lastVerifiedDate = createDriverDto.DriverLicenceLastVerifiedDate,
                driver_hasPDP = createDriverDto.DriverHasPDP,
                driver_PDP_ExpiryDate = createDriverDto.DriverPDPExpiryDate,
                driver_licence_ExpiryDate = createDriverDto.DriverLicenceExpiryDate,
                driver_active = createDriverDto.DriverActive,
            };

            var createdDriver = await _driverRepository.CreateAsync(driver, currentUserId);

            var driverDto = new DriverDto
            {
                SiteDriverCode = createdDriver.site_driver_code,
                SiteCode = createdDriver.site_code,
                DriverLicenceTypeId = createdDriver.driver_licence_type_id,
                DriverSurname = createdDriver.driver_surname,
                DriverFirstname = createdDriver.driver_firstname,
                DriverSAId = createdDriver.driver_SA_id,
                DriverPassportNumber = createdDriver.driver_passportnumber,
                DriverPersonalNumber = createdDriver.driver_persalnumber,
                DriverContractNumber = createdDriver.driver_contractnumber,
                DriverLicenceNumber = createdDriver.driver_licence_number,
                DriverLicenceIssueDate = createdDriver.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = createdDriver.driver_licence_lastVerifiedDate,
                DriverHasPDP = createdDriver.driver_hasPDP,
                DriverPDPExpiryDate = createdDriver.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = createdDriver.driver_licence_ExpiryDate,
                DriverActive = createdDriver.driver_active,
            };

            return CreatedAtAction(
                nameof(GetDriver),
                new { id = createdDriver.site_driver_code.ToString() },
                driverDto
            );
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("cannot be captured or assigned", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Duplicate driver identity rejected");
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("procedure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy driver procedure contract is unavailable or incompatible");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The deployed legacy driver procedure is unavailable or incompatible. No direct-DML fallback was run.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Driver and Authoriser Management,SystemAdministrator,System Administrator")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(
        string id,
        [FromBody] UpdateDriverDto updateDriverDto
    )
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound();
            }
            if (
                !await IsSiteAllowedAsync(existingDriver.site_code)
                || !await IsSiteAllowedAsync(updateDriverDto.SiteCode)
            )
                return Forbid();
            if (await ValidateNoDuplicateIdentityAsync(updateDriverDto, existingDriver.site_driver_code) is { } duplicateError)
                return Conflict(new { error = duplicateError });

            existingDriver.site_code = updateDriverDto.SiteCode;
            existingDriver.driver_licence_type_id = updateDriverDto.DriverLicenceTypeId;
            existingDriver.driver_surname = updateDriverDto.DriverSurname;
            existingDriver.driver_firstname = updateDriverDto.DriverFirstname;
            existingDriver.driver_SA_id = updateDriverDto.DriverSAId;
            existingDriver.driver_passportnumber = updateDriverDto.DriverPassportNumber;
            existingDriver.driver_persalnumber = updateDriverDto.DriverPersonalNumber;
            existingDriver.driver_contractnumber = updateDriverDto.DriverContractNumber;
            existingDriver.driver_licence_number = updateDriverDto.DriverLicenceNumber;
            existingDriver.driver_licence_issuedate = updateDriverDto.DriverLicenceIssueDate;
            existingDriver.driver_licence_lastVerifiedDate =
                updateDriverDto.DriverLicenceLastVerifiedDate;
            existingDriver.driver_hasPDP = updateDriverDto.DriverHasPDP;
            existingDriver.driver_PDP_ExpiryDate = updateDriverDto.DriverPDPExpiryDate;
            existingDriver.driver_licence_ExpiryDate = updateDriverDto.DriverLicenceExpiryDate;
            existingDriver.driver_active = updateDriverDto.DriverActive;

            await _driverRepository.UpdateAsync(existingDriver, currentUserId);

            var driverDto = new DriverDto
            {
                SiteDriverCode = existingDriver.site_driver_code,
                SiteCode = existingDriver.site_code,
                DriverLicenceTypeId = existingDriver.driver_licence_type_id,
                DriverSurname = existingDriver.driver_surname,
                DriverFirstname = existingDriver.driver_firstname,
                DriverSAId = existingDriver.driver_SA_id,
                DriverPassportNumber = existingDriver.driver_passportnumber,
                DriverPersonalNumber = existingDriver.driver_persalnumber,
                DriverContractNumber = existingDriver.driver_contractnumber,
                DriverLicenceNumber = existingDriver.driver_licence_number,
                DriverLicenceIssueDate = existingDriver.driver_licence_issuedate,
                DriverLicenceLastVerifiedDate = existingDriver.driver_licence_lastVerifiedDate,
                DriverHasPDP = existingDriver.driver_hasPDP,
                DriverPDPExpiryDate = existingDriver.driver_PDP_ExpiryDate,
                DriverLicenceExpiryDate = existingDriver.driver_licence_ExpiryDate,
                DriverActive = existingDriver.driver_active,
            };

            return Ok(driverDto);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("cannot be captured or assigned", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Duplicate driver identity rejected for {DriverId}", id);
            return Conflict(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("procedure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(ex, "Legacy driver procedure contract is unavailable or incompatible for {DriverId}", id);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The deployed legacy driver procedure is unavailable or incompatible. No direct-DML fallback was run.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver with id {DriverId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Driver and Authoriser Management,SystemAdministrator,System Administrator")]
    public async Task<ActionResult> DeleteDriver(string id)
    {
        try
        {
            int currentUserId = GetCurrentUserId();

            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound();
            }
            if (!await IsSiteAllowedAsync(existingDriver.site_code))
                return Forbid();

            await _driverRepository.DeleteAsync(id, currentUserId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver with id {DriverId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    private async Task<string?> ValidateNoDuplicateIdentityAsync(
        CreateDriverDto candidate,
        int? excludedSiteDriverCode = null
    )
    {
        var southAfricanId = NormalizeIdentity(candidate.DriverSAId);
        var passportNumber = NormalizeIdentity(candidate.DriverPassportNumber);
        var licenceNumber = NormalizeIdentity(candidate.DriverLicenceNumber);
        var existingDrivers = await _driverRepository.GetAllDriversAsync();
        var duplicate = existingDrivers.FirstOrDefault(driver =>
            (!excludedSiteDriverCode.HasValue || driver.site_driver_code != excludedSiteDriverCode.Value)
            && (
                MatchesIdentity(driver.driver_SA_id, southAfricanId)
                || MatchesIdentity(driver.driver_passportnumber, passportNumber)
                || MatchesIdentity(driver.driver_licence_number, licenceNumber)
            )
        );
        if (duplicate is null)
            return null;

        return duplicate.site_code == candidate.SiteCode
            ? "This driver already exists at the selected site. Update the existing driver record instead."
            : $"This driver already exists at site {duplicate.site_code}. A driver cannot be captured at more than one site.";
    }

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Replace(" ", string.Empty).ToUpperInvariant();

    private static bool MatchesIdentity(string? existingValue, string? candidateValue) =>
        candidateValue is not null
        && string.Equals(NormalizeIdentity(existingValue), candidateValue, StringComparison.Ordinal);

    private async Task<IReadOnlySet<int>?> ResolveAllowedSiteCodesAsync()
    {
        if (HasGlobalDriverScope())
            return null;

        var userId = GetCurrentUserId();
        var profileSiteCode = await _context.UserAccessOlds.AsNoTracking()
            .Where(user => user.user_access_code == userId)
            .Select(user => user.Site_code)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (profileSiteCode is not > 0)
            return new HashSet<int>();

        var profileSite = await _siteRepository.GetByIdAsync(profileSiteCode.Value);
        if (profileSite is null)
            return new HashSet<int>();

        var sites = await _siteRepository.GetActiveSitesAsync();
        if (HasRole("Vehicle List for All Departments in Province") && profileSite.province_code.HasValue)
            sites = sites.Where(site => site.province_code == profileSite.province_code.Value);
        else if (HasRole("Vehicle List for All Sites in Department") && profileSite.Depatrment_code.HasValue)
            sites = sites.Where(site => site.Depatrment_code == profileSite.Depatrment_code.Value);
        else
            sites = sites.Where(site => site.Site_code == profileSite.Site_code);

        return sites.Select(site => (int)site.Site_code).ToHashSet();
    }

    private async Task<bool> IsSiteAllowedAsync(int siteCode)
    {
        var allowed = await ResolveAllowedSiteCodesAsync();
        return allowed is null || allowed.Contains(siteCode);
    }

    private static IEnumerable<Driver> FilterByAllowedSites(
        IEnumerable<Driver> drivers,
        IReadOnlySet<int>? allowedSites
    ) => allowedSites is null
        ? drivers
        : drivers.Where(driver => allowedSites.Contains(driver.site_code));

    private bool HasGlobalDriverScope() => HasRole("SystemAdministrator") || HasRole("System Administrator");

    private bool HasRole(string expectedRole) => User.Claims
        .Where(claim =>
            claim.Type == ClaimTypes.Role
            || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
        )
        .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
        .Any(role => string.Equals(role.Trim(), expectedRole, StringComparison.OrdinalIgnoreCase));
}

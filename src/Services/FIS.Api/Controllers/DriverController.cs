using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

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
public class DriverController : ControllerBase
{
    private readonly IDriverRepository _driverRepository;
    private readonly ILogger<DriverController> _logger;

    public DriverController(IDriverRepository driverRepository, ILogger<DriverController> logger)
    {
        _driverRepository = driverRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers()
    {
        try
        {
            var drivers = await _driverRepository.GetActiveDriversAsync();
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
                DriverActive = d.driver_active
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
            var driver = await _driverRepository.GetByIdAsync(id);
            if (driver == null)
            {
                return NotFound();
            }

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
                DriverActive = driver.driver_active
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
            var driver = await _driverRepository.GetByLicenceNumberAsync(licenceNumber);
            if (driver == null)
            {
                return NotFound();
            }

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
                DriverActive = driver.driver_active
            };

            return Ok(driverDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving driver with licence number {LicenceNumber}", licenceNumber);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<DriverDto>>> SearchDrivers([FromQuery] string searchTerm)
    {
        try
        {
            var drivers = await _driverRepository.SearchDriversAsync(searchTerm);
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
                DriverActive = d.driver_active
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
    public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverDto createDriverDto)
    {
        try
        {
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
                driver_active = createDriverDto.DriverActive
            };

            var createdDriver = await _driverRepository.CreateAsync(driver);

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
                DriverActive = createdDriver.driver_active
            };

            return CreatedAtAction(nameof(GetDriver), new { id = createdDriver.site_driver_code.ToString() }, driverDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating driver");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<DriverDto>> UpdateDriver(string id, [FromBody] UpdateDriverDto updateDriverDto)
    {
        try
        {
            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound();
            }

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
            existingDriver.driver_licence_lastVerifiedDate = updateDriverDto.DriverLicenceLastVerifiedDate;
            existingDriver.driver_hasPDP = updateDriverDto.DriverHasPDP;
            existingDriver.driver_PDP_ExpiryDate = updateDriverDto.DriverPDPExpiryDate;
            existingDriver.driver_licence_ExpiryDate = updateDriverDto.DriverLicenceExpiryDate;
            existingDriver.driver_active = updateDriverDto.DriverActive;

            await _driverRepository.UpdateAsync(existingDriver);

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
                DriverActive = existingDriver.driver_active
            };

            return Ok(driverDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating driver with id {DriverId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteDriver(string id)
    {
        try
        {
            var existingDriver = await _driverRepository.GetByIdAsync(id);
            if (existingDriver == null)
            {
                return NotFound();
            }

            await _driverRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting driver with id {DriverId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
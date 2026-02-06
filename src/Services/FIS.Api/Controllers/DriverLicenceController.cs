using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DriverLicenceController : BaseApiController
{
    private readonly ILogger<DriverLicenceController> _logger;

    public DriverLicenceController(ILogger<DriverLicenceController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all driver licence types
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<DriverLicenceTypeDto>> GetAll()
    {
        // TODO: Implement repository call to get all driver licence types
        _logger.LogInformation("Getting all driver licence types");
        return Ok(new List<DriverLicenceTypeDto>());
    }

    /// <summary>
    /// Get driver licence type by code
    /// </summary>
    [HttpGet("{code}")]
    public ActionResult<DriverLicenceTypeDto> GetByCode(string code)
    {
        // TODO: Implement repository call to get driver licence type by code
        _logger.LogInformation("Getting driver licence type {Code}", code);
        return NotFound();
    }

    /// <summary>
    /// Create new driver licence type
    /// </summary>
    [HttpPost]
    public ActionResult<DriverLicenceTypeDto> Create([FromBody] CreateDriverLicenceTypeDto request)
    {
        // TODO: Implement repository call to create driver licence type
        _logger.LogInformation("Creating driver licence type: {Code}", request.LicenceCode);
        var created = new DriverLicenceTypeDto
        {
            LicenceCode = request.LicenceCode,
            Description = request.Description
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing driver licence type
    /// </summary>
    [HttpPut("{code}")]
    public ActionResult<DriverLicenceTypeDto> Update(string code, [FromBody] UpdateDriverLicenceTypeDto request)
    {
        // TODO: Implement repository call to update driver licence type
        _logger.LogInformation("Updating driver licence type {Code}", code);
        if (code != request.LicenceCode)
            return BadRequest("Code mismatch");

        var updated = new DriverLicenceTypeDto
        {
            LicenceCode = request.LicenceCode,
            Description = request.Description
        };
        return Ok(updated);
    }

    /// <summary>
    /// Delete driver licence type
    /// </summary>
    [HttpDelete("{code}")]
    public ActionResult Delete(string code)
    {
        // TODO: Implement repository call to delete driver licence type
        _logger.LogInformation("Deleting driver licence type {Code}", code);
        return Ok(new { message = "Driver licence type deleted successfully", code });
    }
}

#region Driver Licence DTOs

public class DriverLicenceTypeDto
{
    public string LicenceCode { get; set; } = "";
    public string? Description { get; set; }
}

public class CreateDriverLicenceTypeDto
{
    public string LicenceCode { get; set; } = "";
    public string? Description { get; set; }
}

public class UpdateDriverLicenceTypeDto
{
    public string LicenceCode { get; set; } = "";
    public string? Description { get; set; }
}

#endregion

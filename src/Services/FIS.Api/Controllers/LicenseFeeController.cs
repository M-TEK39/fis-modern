using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LicenseFeeController : BaseApiController
{
    private readonly ILogger<LicenseFeeController> _logger;

    public LicenseFeeController(ILogger<LicenseFeeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all licence fees
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<LicenseFeeDto>> GetAll()
    {
        // TODO: Implement repository call to get all licence fees
        _logger.LogInformation("Getting all licence fees");
        return Ok(new List<LicenseFeeDto>());
    }

    /// <summary>
    /// Get licence fee by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<LicenseFeeDto> GetById(int id)
    {
        // TODO: Implement repository call to get licence fee by ID
        _logger.LogInformation("Getting licence fee {Id}", id);
        return NotFound();
    }

    /// <summary>
    /// Create new licence fee
    /// </summary>
    [HttpPost]
    public ActionResult<LicenseFeeDto> Create([FromBody] CreateLicenseFeeDto request)
    {
        // TODO: Implement repository call to create licence fee
        _logger.LogInformation("Creating licence fee for vehicle type: {VehicleTypeCode}", request.VehicleTypeCode);
        var created = new LicenseFeeDto
        {
            LicenseFeeId = 0,
            VehicleTypeCode = request.VehicleTypeCode,
            ProvinceCode = request.ProvinceCode,
            FeeAmount = request.FeeAmount,
            EffectiveDate = request.EffectiveDate
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing licence fee
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<LicenseFeeDto> Update(int id, [FromBody] UpdateLicenseFeeDto request)
    {
        // TODO: Implement repository call to update licence fee
        _logger.LogInformation("Updating licence fee {Id}", id);
        if (id != request.LicenseFeeId)
            return BadRequest("ID mismatch");

        var updated = new LicenseFeeDto
        {
            LicenseFeeId = id,
            VehicleTypeCode = request.VehicleTypeCode,
            ProvinceCode = request.ProvinceCode,
            FeeAmount = request.FeeAmount,
            EffectiveDate = request.EffectiveDate
        };
        return Ok(updated);
    }

    /// <summary>
    /// Delete licence fee
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult Delete(int id)
    {
        // TODO: Implement repository call to delete licence fee
        _logger.LogInformation("Deleting licence fee {Id}", id);
        return Ok(new { message = "Licence fee deleted successfully", id });
    }
}

#region Licence Fee DTOs

public class LicenseFeeDto
{
    public int LicenseFeeId { get; set; }
    public string? VehicleTypeCode { get; set; }
    public string? ProvinceCode { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class CreateLicenseFeeDto
{
    public string? VehicleTypeCode { get; set; }
    public string? ProvinceCode { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class UpdateLicenseFeeDto
{
    public int LicenseFeeId { get; set; }
    public string? VehicleTypeCode { get; set; }
    public string? ProvinceCode { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

#endregion

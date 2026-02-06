using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExtraCodeController : BaseApiController
{
    private readonly ILogger<ExtraCodeController> _logger;

    public ExtraCodeController(ILogger<ExtraCodeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all extra codes
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<ExtraCodeDto>> GetAll()
    {
        // TODO: Implement repository call to get all extra codes
        _logger.LogInformation("Getting all extra codes");
        return Ok(new List<ExtraCodeDto>());
    }

    /// <summary>
    /// Get extra code by code
    /// </summary>
    [HttpGet("{code}")]
    public ActionResult<ExtraCodeDto> GetByCode(string code)
    {
        // TODO: Implement repository call to get extra code by code
        _logger.LogInformation("Getting extra code {Code}", code);
        return NotFound();
    }

    /// <summary>
    /// Create new extra code
    /// </summary>
    [HttpPost]
    public ActionResult<ExtraCodeDto> Create([FromBody] CreateExtraCodeDto request)
    {
        // TODO: Implement repository call to create extra code
        _logger.LogInformation("Creating extra code: {Code}", request.ExtraCode);
        var created = new ExtraCodeDto
        {
            ExtraCode = request.ExtraCode,
            Description = request.Description,
            Category = request.Category
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing extra code
    /// </summary>
    [HttpPut("{code}")]
    public ActionResult<ExtraCodeDto> Update(string code, [FromBody] UpdateExtraCodeDto request)
    {
        // TODO: Implement repository call to update extra code
        _logger.LogInformation("Updating extra code {Code}", code);
        if (code != request.ExtraCode)
            return BadRequest("Code mismatch");

        var updated = new ExtraCodeDto
        {
            ExtraCode = request.ExtraCode,
            Description = request.Description,
            Category = request.Category
        };
        return Ok(updated);
    }

    /// <summary>
    /// Delete extra code
    /// </summary>
    [HttpDelete("{code}")]
    public ActionResult Delete(string code)
    {
        // TODO: Implement repository call to delete extra code
        _logger.LogInformation("Deleting extra code {Code}", code);
        return Ok(new { message = "Extra code deleted successfully", code });
    }
}

#region Extra Code DTOs

public class ExtraCodeDto
{
    public string ExtraCode { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class CreateExtraCodeDto
{
    public string ExtraCode { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class UpdateExtraCodeDto
{
    public string ExtraCode { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
}

#endregion

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LossTypeController : BaseApiController
{
    private readonly ILogger<LossTypeController> _logger;

    public LossTypeController(ILogger<LossTypeController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all loss types
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<LossTypeDto>> GetAll()
    {
        // TODO: Implement repository call to get all loss types
        _logger.LogInformation("Getting all loss types");
        return Ok(new List<LossTypeDto>());
    }

    /// <summary>
    /// Get loss type by code
    /// </summary>
    [HttpGet("{code}")]
    public ActionResult<LossTypeDto> GetByCode(short code)
    {
        // TODO: Implement repository call to get loss type by code
        _logger.LogInformation("Getting loss type {Code}", code);
        return NotFound();
    }

    /// <summary>
    /// Create new loss type
    /// </summary>
    [HttpPost]
    public ActionResult<LossTypeDto> Create([FromBody] CreateLossTypeDto request)
    {
        // TODO: Implement repository call to create loss type
        _logger.LogInformation("Creating loss type: {Description}", request.Description);
        var created = new LossTypeDto
        {
            LossTypeCode = 0,
            Description = request.Description,
            Category = request.Category
        };
        return Ok(created);
    }

    /// <summary>
    /// Update existing loss type
    /// </summary>
    [HttpPut("{code}")]
    public ActionResult<LossTypeDto> Update(short code, [FromBody] UpdateLossTypeDto request)
    {
        // TODO: Implement repository call to update loss type
        _logger.LogInformation("Updating loss type {Code}", code);
        if (code != request.LossTypeCode)
            return BadRequest("Code mismatch");

        var updated = new LossTypeDto
        {
            LossTypeCode = code,
            Description = request.Description,
            Category = request.Category
        };
        return Ok(updated);
    }

    /// <summary>
    /// Delete loss type
    /// </summary>
    [HttpDelete("{code}")]
    public ActionResult Delete(short code)
    {
        // TODO: Implement repository call to delete loss type
        _logger.LogInformation("Deleting loss type {Code}", code);
        return Ok(new { message = "Loss type deleted successfully", code });
    }
}

#region Loss Type DTOs

public class LossTypeDto
{
    public short LossTypeCode { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class CreateLossTypeDto
{
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class UpdateLossTypeDto
{
    public short LossTypeCode { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
}

#endregion

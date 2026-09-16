using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicle-inception/gg-blocks")]
public sealed class GgBlocksController : BaseApiController
{
    private static readonly Regex GgNumberPattern = new(
        "^[A-Z]{3}[0-9]{3}G$",
        RegexOptions.CultureInvariant
    );

    private readonly IGgBlockRepository _repository;
    private readonly ILogger<GgBlocksController> _logger;

    public GgBlocksController(IGgBlockRepository repository, ILogger<GgBlocksController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!HasVehicleManagementPermission())
        {
            return Forbid();
        }

        try
        {
            var result = await _repository.GetHistoryAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, 100)
            );
            return Ok(
                new
                {
                    items = result.Items.Select(MapHistoryRecord),
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalRecords = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving GG block history");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The GG block service is unavailable." }
            );
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGgBlockRequest request)
    {
        if (!HasVehicleManagementPermission())
        {
            return Forbid();
        }

        var start = request.StartGgNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        var end = request.EndGgNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!GgNumberPattern.IsMatch(start) || !GgNumberPattern.IsMatch(end))
        {
            return BadRequest(new { message = "GG numbers must use the format ABC123G." });
        }

        var startNumber = int.Parse(start[3..6]);
        var endNumber = int.Parse(end[3..6]);
        if (!string.Equals(start[..3], end[..3], StringComparison.Ordinal) || start[6] != end[6])
        {
            return BadRequest(
                new
                {
                    message = "The start and end GG numbers must use the same prefix and suffix.",
                }
            );
        }

        if (endNumber == startNumber)
        {
            return BadRequest(
                new { message = "The end GG number must be greater than the start GG number." }
            );
        }

        if (endNumber < startNumber)
        {
            return BadRequest(
                new { message = "The end GG number cannot be before the start GG number." }
            );
        }

        int currentUserId;
        try
        {
            currentUserId = GetCurrentUserId();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _repository.CreateAsync(start, end, currentUserId);
            return Created(
                string.Empty,
                new
                {
                    capturedBy = result.CapturedBy,
                    dateCreated = result.DateCreated,
                    startGgNumber = result.StartGgNumber,
                    endGgNumber = result.EndGgNumber,
                }
            );
        }
        catch (GgBlockRangeConflictException)
        {
            return Conflict(
                new
                {
                    message = "The requested GG block range falls within an existing GG block range.",
                }
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating GG block range {StartGgNumber} to {EndGgNumber}",
                start,
                end
            );
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The GG block service is unavailable." }
            );
        }
    }

    private bool HasVehicleManagementPermission()
    {
        return HasRole("Vehicle Master");
    }

    private bool HasRole(string expectedRole) =>
        User.Claims.Any(claim =>
            (claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))
            && claim.Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ).Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase))
        );

    private static object MapHistoryRecord(GgBlockHistoryRecord record) =>
        new
        {
            blockId = record.BlockId,
            capturedBy = record.CapturedBy,
            dateCreated = record.DateCreated,
            startGgNumber = record.StartGgNumber,
            endGgNumber = record.EndGgNumber,
        };
}

public sealed class CreateGgBlockRequest
{
    [Required]
    [StringLength(7)]
    public string? StartGgNumber { get; set; }

    [Required]
    [StringLength(7)]
    public string? EndGgNumber { get; set; }
}

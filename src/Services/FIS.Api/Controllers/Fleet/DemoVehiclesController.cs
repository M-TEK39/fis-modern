using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/demo-vehicles")]
public sealed class DemoVehiclesController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;

    private readonly IDemoVehicleRepository _repository;
    private readonly ILogger<DemoVehiclesController> _logger;

    public DemoVehiclesController(
        IDemoVehicleRepository repository,
        ILogger<DemoVehiclesController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DemoVehicleResponse>>> GetAll()
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        try
        {
            return Ok((await _repository.GetAllAsync()).Select(Map));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving demo vehicles");
            return StatusCode(500, "An error occurred while retrieving demo vehicles.");
        }
    }

    [HttpGet("reports/all")]
    public async Task<ActionResult<IReadOnlyList<DemoVehicleResponse>>> GetReport()
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        try
        {
            return Ok((await _repository.GetAllAsync()).Select(Map));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the demo vehicle report");
            return StatusCode(500, "An error occurred while retrieving the demo vehicle report.");
        }
    }

    [HttpGet("reports/all/page")]
    public async Task<ActionResult> GetReportPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        try
        {
            var result = await _repository.GetPageAsync(
                Math.Max(1, page),
                Math.Clamp(pageSize, 1, MaximumPageSize)
            );

            return Ok(
                new
                {
                    items = result.Items.Select(Map).ToList(),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.Total,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the paged demo vehicle report");
            return StatusCode(500, "An error occurred while retrieving the demo vehicle report.");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<DemoVehicleResponse>>> Search(
        [FromQuery] string? mode,
        [FromQuery] string? search
    )
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        var normalizedMode = (mode ?? "GG").Trim().ToUpperInvariant();
        if (normalizedMode is not ("GG" or "GP"))
        {
            return BadRequest(new { message = "Search mode must be GG or GP." });
        }

        try
        {
            return Ok(
                (
                    await _repository.SearchAsync(search ?? string.Empty, normalizedMode == "GP")
                ).Select(Map)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching demo vehicles");
            return StatusCode(500, "An error occurred while searching demo vehicles.");
        }
    }

    [HttpGet("{demoVehicleCode:int}")]
    public async Task<ActionResult<DemoVehicleResponse>> Get(int demoVehicleCode)
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        try
        {
            var vehicle = await _repository.GetByIdAsync(demoVehicleCode);
            return vehicle is null ? NotFound() : Ok(Map(vehicle));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error retrieving demo vehicle {DemoVehicleCode}",
                demoVehicleCode
            );
            return StatusCode(500, "An error occurred while retrieving the demo vehicle.");
        }
    }

    [HttpPost]
    public async Task<ActionResult<DemoVehicleResponse>> Create(
        [FromBody] DemoVehicleRequest request
    )
    {
        if (!HasDemoVehicleRole())
            return Forbid();
        if (!TryMapInput(request, out var input, out var error))
            return BadRequest(new { message = error });

        try
        {
            var created = await _repository.CreateAsync(input!, GetCurrentUserId());
            return CreatedAtAction(
                nameof(Get),
                new { demoVehicleCode = created.DemoVehicleCode },
                Map(created)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating demo vehicle");
            return StatusCode(500, "An error occurred while creating the demo vehicle.");
        }
    }

    [HttpPut("{demoVehicleCode:int}")]
    public async Task<ActionResult<DemoVehicleResponse>> Update(
        int demoVehicleCode,
        [FromBody] DemoVehicleRequest request
    )
    {
        if (!HasDemoVehicleRole())
            return Forbid();
        if (!TryMapInput(request, out var input, out var error))
            return BadRequest(new { message = error });

        try
        {
            return Ok(
                Map(await _repository.UpdateAsync(demoVehicleCode, input!, GetCurrentUserId()))
            );
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating demo vehicle {DemoVehicleCode}", demoVehicleCode);
            return StatusCode(500, "An error occurred while updating the demo vehicle.");
        }
    }

    [HttpDelete("{demoVehicleCode:int}")]
    public async Task<IActionResult> Delete(int demoVehicleCode)
    {
        if (!HasDemoVehicleRole())
            return Forbid();

        try
        {
            await _repository.DeleteAsync(demoVehicleCode, GetCurrentUserId());
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting demo vehicle {DemoVehicleCode}", demoVehicleCode);
            return StatusCode(500, "An error occurred while deleting the demo vehicle.");
        }
    }

    private bool HasDemoVehicleRole()
    {
        if (User.IsInRole("Demo Vehicles"))
            return true;

        var roleClaims = User
            .Claims.Where(claim =>
                claim.Type == ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
            )
            .SelectMany(claim =>
                claim.Value.Split(
                    ',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                )
            );

        return roleClaims.Any(role =>
            string.Equals(role, "Demo Vehicles", StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool TryMapInput(
        DemoVehicleRequest request,
        out DemoVehicleInput? input,
        out string? error
    )
    {
        input = null;
        error = null;
        var registration = request.RegistrationNumber?.Trim() ?? string.Empty;
        var model = request.ModelDescription?.Trim() ?? string.Empty;
        if (registration.Length is < 7 or > 8)
        {
            error = "A valid registration number between 7 and 8 characters is required.";
            return false;
        }

        if (model.Length == 0)
        {
            error = "A make and model description is required.";
            return false;
        }

        if (
            model.Length > 100
            || (request.GgNumber?.Trim().Length ?? 0) > 7
            || registration.Length > 8
        )
        {
            error = "One or more demo vehicle fields exceed the legacy database length.";
            return false;
        }

        if (
            !TryParseNullableInt(request.YearManufactured, out var year, out error)
            || !TryParseNullableShort(request.Tank, out var tank, out error)
            || !TryParseNullableShort(
                request.SiteCode,
                out var siteCode,
                out error,
                zeroMeansNull: true
            )
        )
        {
            return false;
        }

        input = new DemoVehicleInput(
            Normalize(request.GgNumber),
            registration,
            model,
            siteCode,
            year,
            Normalize(request.BankCode),
            tank,
            Normalize(request.Colour),
            Normalize(request.EngineNumber),
            Normalize(request.ChassisNumber)
        );
        return true;
    }

    private static bool TryParseNullableInt(string? value, out int? parsed, out string? error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            error = null;
            return true;
        }

        if (
            int.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number
            )
        )
        {
            parsed = number;
            error = null;
            return true;
        }

        parsed = null;
        error = "Year Manufactured must be a whole number.";
        return false;
    }

    private static bool TryParseNullableShort(
        string? value,
        out short? parsed,
        out string? error,
        bool zeroMeansNull = false
    )
    {
        if (string.IsNullOrWhiteSpace(value) || zeroMeansNull && value.Trim() == "0")
        {
            parsed = null;
            error = null;
            return true;
        }

        if (
            short.TryParse(
                value.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var number
            )
        )
        {
            parsed = number;
            error = null;
            return true;
        }

        parsed = null;
        error = "Site and tank values must be whole numbers.";
        return false;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DemoVehicleResponse Map(DemoVehicleRecord record) =>
        new()
        {
            DemoVehicleCode = record.DemoVehicleCode,
            GgNumber = record.GgNumber,
            RegistrationNumber = record.RegistrationNumber,
            ModelDescription = record.ModelDescription,
            YearManufactured = record.YearManufactured,
            SiteCode = record.SiteCode,
            SiteDescription = record.SiteDescription,
            BankCode = record.BankCode,
            Tank = record.Tank,
            Colour = record.Colour,
            EngineNumber = record.EngineNumber,
            ChassisNumber = record.ChassisNumber,
            DateCreated = record.DateCreated,
            DateUpdated = record.DateUpdated,
            CreatedByUserCode = record.CreatedByUserCode,
            ModifiedByUserCode = record.ModifiedByUserCode,
        };
}

public sealed class DemoVehicleRequest
{
    [JsonPropertyName("gg_number")]
    public string? GgNumber { get; set; }

    [JsonPropertyName("reg_number")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("model_description")]
    public string? ModelDescription { get; set; }

    [JsonPropertyName("site_code")]
    public string? SiteCode { get; set; }

    [JsonPropertyName("year_mnf")]
    public string? YearManufactured { get; set; }

    [JsonPropertyName("bank_code")]
    public string? BankCode { get; set; }

    [JsonPropertyName("tank")]
    public string? Tank { get; set; }

    [JsonPropertyName("colour")]
    public string? Colour { get; set; }

    [JsonPropertyName("engine_number")]
    public string? EngineNumber { get; set; }

    [JsonPropertyName("chassis_number")]
    public string? ChassisNumber { get; set; }
}

public sealed class DemoVehicleResponse
{
    [JsonPropertyName("demo_vehicle_code")]
    public int DemoVehicleCode { get; set; }

    [JsonPropertyName("gg_number")]
    public string? GgNumber { get; set; }

    [JsonPropertyName("reg_number")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("model_description")]
    public string? ModelDescription { get; set; }

    [JsonPropertyName("year_mnf")]
    public int? YearManufactured { get; set; }

    [JsonPropertyName("site_code")]
    public short? SiteCode { get; set; }

    [JsonPropertyName("site_description")]
    public string? SiteDescription { get; set; }

    [JsonPropertyName("bank_code")]
    public string? BankCode { get; set; }

    [JsonPropertyName("tank")]
    public short? Tank { get; set; }

    [JsonPropertyName("colour")]
    public string? Colour { get; set; }

    [JsonPropertyName("engine_number")]
    public string? EngineNumber { get; set; }

    [JsonPropertyName("chassis_number")]
    public string? ChassisNumber { get; set; }

    [JsonPropertyName("date_created")]
    public DateTime? DateCreated { get; set; }

    [JsonPropertyName("date_updated")]
    public DateTime? DateUpdated { get; set; }

    [JsonPropertyName("created_by_user_code")]
    public int? CreatedByUserCode { get; set; }

    [JsonPropertyName("modified_by_user_code")]
    public int? ModifiedByUserCode { get; set; }
}

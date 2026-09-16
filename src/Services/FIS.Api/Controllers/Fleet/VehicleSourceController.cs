using System.ComponentModel.DataAnnotations;
using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vehicle-source")]
public sealed class VehicleSourceController : BaseApiController
{

    private readonly IVehicleSourceRepository _repository;
    private readonly ILogger<VehicleSourceController> _logger;

    public VehicleSourceController(
        IVehicleSourceRepository repository,
        ILogger<VehicleSourceController> logger
    )
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // Vehicle inception capturers select the legacy hired-from value while
        // capturing a vehicle. Reading this reference data is part of their
        // capture workflow; only source maintenance itself remains restricted
        // to Vehicle Master.
        if (!HasVehicleSourceReadPermission())
        {
            return Forbid();
        }

        try
        {
            var page = await _repository.GetPageAsync();
            return Ok(
                new
                {
                    items = page.Items.Select(Map),
                    capabilities = new
                    {
                        emailAddress = page.Capabilities.HasEmailAddress,
                        contactPerson = page.Capabilities.HasContactPerson,
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle sources");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The vehicle source service is unavailable." }
            );
        }
    }

    [HttpGet("{sourceCode:int}")]
    public async Task<IActionResult> GetByCode(byte sourceCode)
    {
        if (!HasVehicleSourceReadPermission())
        {
            return Forbid();
        }

        try
        {
            var source = await _repository.GetByIdAsync(sourceCode);
            return source is null
                ? NotFound(
                    new { message = $"Vehicle source with code {sourceCode} was not found." }
                )
                : Ok(Map(source));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle source {SourceCode}", sourceCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The vehicle source service is unavailable." }
            );
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] VehicleSourceRequest request)
    {
        if (!HasVehicleManagementPermission())
        {
            return Forbid();
        }

        try
        {
            var capabilities = await _repository.GetCapabilitiesAsync();
            var validationError = Validate(request, capabilities);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var created = await _repository.CreateAsync(ToInput(request), GetCurrentUserId());
            return CreatedAtAction(
                nameof(GetByCode),
                new { sourceCode = created.SourceCode },
                Map(created)
            );
        }
        catch (VehicleSourceFieldUnavailableException ex)
        {
            return Conflict(
                new
                {
                    message = $"This database cannot store the legacy vehicle source {GetFriendlyFieldName(ex.FieldName)} field.",
                }
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle source");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The vehicle source could not be saved." }
            );
        }
    }

    [HttpPut("{sourceCode:int}")]
    public async Task<IActionResult> Update(
        byte sourceCode,
        [FromBody] VehicleSourceRequest request
    )
    {
        if (!HasVehicleManagementPermission())
        {
            return Forbid();
        }

        try
        {
            var capabilities = await _repository.GetCapabilitiesAsync();
            var validationError = Validate(request, capabilities);
            if (validationError is not null)
            {
                return BadRequest(new { message = validationError });
            }

            var updated = await _repository.UpdateAsync(
                sourceCode,
                ToInput(request),
                GetCurrentUserId()
            );
            return Ok(Map(updated));
        }
        catch (VehicleSourceFieldUnavailableException ex)
        {
            return Conflict(
                new
                {
                    message = $"This database cannot store the legacy vehicle source {GetFriendlyFieldName(ex.FieldName)} field.",
                }
            );
        }
        catch (KeyNotFoundException)
        {
            return NotFound(
                new { message = $"Vehicle source with code {sourceCode} was not found." }
            );
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle source {SourceCode}", sourceCode);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "The vehicle source could not be saved." }
            );
        }
    }

    private bool HasVehicleManagementPermission()
    {
        return HasRole("Vehicle Master") || HasSystemAdministratorRole();
    }

    private bool HasVehicleSourceReadPermission()
    {
        return HasSystemAdministratorRole()
            || HasVehicleManagementPermission()
            || HasRole("Vehicle Inception Capturer")
            || HasRole("Vehicle Inception Authorizer");
    }

    private bool HasSystemAdministratorRole() =>
        HasRole("SystemAdministrator")
        || HasRole("System Administrator");

    private bool HasRole(string expectedRole) =>
        User.Claims.Any(claim =>
            (claim.Type == System.Security.Claims.ClaimTypes.Role
                || claim.Type.Equals("role", StringComparison.OrdinalIgnoreCase)
                || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase))
            && claim.Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
            ).Any(role => string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase))
        );

    private static string? Validate(
        VehicleSourceRequest? request,
        VehicleSourceCapabilities capabilities
    )
    {
        if (request is null)
        {
            return "A vehicle source is required.";
        }

        var requiredFields = new (string Name, string? Value)[]
        {
            ("name", request.Name),
            ("physical address", request.PhysicalAddress),
            ("postal address", request.PostalAddress),
            ("telephone number", request.TelephoneNumber),
            ("fax number", request.FaxNumber),
        };
        var missing = requiredFields.FirstOrDefault(field =>
            string.IsNullOrWhiteSpace(field.Value)
        );
        if (missing.Name is not null)
        {
            return $"Enter the vehicle source {missing.Name}.";
        }

        if (capabilities.HasEmailAddress && string.IsNullOrWhiteSpace(request.EmailAddress))
        {
            return "Enter the vehicle source e-mail address. If not available enter NONE.";
        }

        return request.Name!.Trim().Length > 60
                ? "The vehicle source name cannot exceed 60 characters."
            : request.PhysicalAddress!.Trim().Length > 60
                ? "The physical address cannot exceed 60 characters."
            : request.PostalAddress!.Trim().Length > 60
                ? "The postal address cannot exceed 60 characters."
            : request.TelephoneNumber!.Trim().Length > 20
                ? "The telephone number cannot exceed 20 characters."
            : request.FaxNumber!.Trim().Length > 20 ? "The fax number cannot exceed 20 characters."
            : request.EmailAddress?.Trim().Length > 40
                ? "The e-mail address cannot exceed 40 characters."
            : request.ContactPerson?.Trim().Length > 40
                ? "The contact person cannot exceed 40 characters."
            : null;
    }

    private static VehicleSourceInput ToInput(VehicleSourceRequest request) =>
        new(
            request.Name!.Trim().ToUpperInvariant(),
            request.PhysicalAddress!.Trim(),
            request.PostalAddress!.Trim(),
            request.TelephoneNumber!.Trim(),
            request.FaxNumber!.Trim(),
            request.EmailAddress?.Trim() ?? string.Empty,
            request.ContactPerson?.Trim() ?? string.Empty
        );

    private static object Map(VehicleSourceRecord source) =>
        new
        {
            vsCode = source.SourceCode,
            name = source.Name,
            physicalAddress = source.PhysicalAddress,
            postalAddress = source.PostalAddress,
            telephoneNumber = source.TelephoneNumber,
            faxNumber = source.FaxNumber,
            emailAddress = source.EmailAddress,
            contactPerson = source.ContactPerson,
            dateCreated = source.DateCreated,
            dateUpdated = source.DateUpdated,
        };

    private static string GetFriendlyFieldName(string fieldName) =>
        fieldName switch
        {
            "email_address" => "e-mail address",
            "contact_person" => "contact person",
            _ => fieldName,
        };
}

public sealed class VehicleSourceRequest
{
    [Required]
    [StringLength(60)]
    public string? Name { get; set; }

    [Required]
    [StringLength(60)]
    public string? PhysicalAddress { get; set; }

    [Required]
    [StringLength(60)]
    public string? PostalAddress { get; set; }

    [Required]
    [StringLength(20)]
    public string? TelephoneNumber { get; set; }

    [Required]
    [StringLength(20)]
    public string? FaxNumber { get; set; }

    [StringLength(40)]
    public string? EmailAddress { get; set; }

    [StringLength(40)]
    public string? ContactPerson { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Validation")]
public class ValidationController : BaseApiController
{
    private readonly ILogger<ValidationController> _logger;

    public ValidationController(ILogger<ValidationController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<ValidationMenuDto> GetRoot()
    {
        return GetMenu();
    }

    /// <summary>
    /// Get validation/reference data menu options
    /// </summary>
    [HttpGet("menu")]
    public ActionResult<ValidationMenuDto> GetMenu()
    {
        var menu = new ValidationMenuDto
        {
            Options = new List<string>
            {
                "Driver Licence Types",
                "Licence Fees",
                "Extra Codes",
                "Loss Types",
                "Vehicle Classes",
                "Fuel Types",
                "Maintenance Triggers",
            },
        };
        return Ok(menu);
    }

    /// <summary>
    /// Get validation/reference data help information
    /// </summary>
    [HttpGet("help")]
    public ActionResult<ValidationHelpDto> GetHelp()
    {
        var help = new ValidationHelpDto
        {
            Title = "Validation & Reference Data Management",
            Description = "Manage reference data used throughout the fleet management system",
            Sections = new List<HelpSectionDto>
            {
                new HelpSectionDto
                {
                    Title = "Driver Licence Types",
                    Content =
                        "Manage valid driver licence type codes and descriptions (e.g., C1, C, EB, EC)",
                },
                new HelpSectionDto
                {
                    Title = "Licence Fees",
                    Content = "Configure licence renewal fees by vehicle type and province",
                },
                new HelpSectionDto
                {
                    Title = "Extra Codes",
                    Content =
                        "Define additional classification codes for special vehicle categories",
                },
                new HelpSectionDto
                {
                    Title = "Loss Types",
                    Content =
                        "Categorize different types of vehicle losses (theft, accident, write-off, etc.)",
                },
            },
        };
        return Ok(help);
    }
}

#region Validation DTOs

public class ValidationMenuDto
{
    public List<string> Options { get; set; } = new();
}

public class ValidationHelpDto
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSectionDto> Sections { get; set; } = new();
}

public class HelpSectionDto
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

#endregion

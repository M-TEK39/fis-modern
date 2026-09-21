using System.Security.Claims;
using FIS.Api.Services;
using FIS.Api.DTOs;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

/// <summary>
/// Controller for managing job cards (work orders for vehicle maintenance/repairs)
/// Legacy: Replaces stored procedures DEV_SEL_JobcardPriotiyForCapturers, DEV_INS_NewJobCards, etc.
/// </summary>
[ApiController]
[Authorize]
[Route("api/jobcards")]
public class JobCardController : BaseApiController
{
    private const string JobCardCapturerRole = "JobCard Capturer";
    private const string JobCardAuthorizerRole = "JobCard Authorizer";

    private readonly IJobCardRepository _repository;
    private readonly IContractRepository _contractRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly LegacyVehicleScopeService _vehicleScope;
    private readonly ILogger<JobCardController> _logger;

    public JobCardController(
        IJobCardRepository repository,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        LegacyVehicleScopeService vehicleScope,
        ILogger<JobCardController> logger
    )
    {
        _repository = repository;
        _contractRepository = contractRepository;
        _vehicleRepository = vehicleRepository;
        _vehicleScope = vehicleScope;
        _logger = logger;
    }

    /// <summary>
    /// Get all job cards
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetAll()
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            _logger.LogInformation("Getting all job cards");
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var jobCards = (await _repository.GetAllAsync())
                .Where(jobCard => allowedVehicles.Contains(jobCard.vmf_code));
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all job cards");
            return StatusCode(500, new { error = "An error occurred while retrieving job cards" });
        }
    }

    /// <summary>
    /// Archive AuthorizerPerGGNumberView uses DEV_SEL_JobcardsForAuthorizers
    /// @ggnumber. AuthorizerJobcardStatusScreen uses
    /// DEV_SEL_JobcardsStatusReportForAuthorizer @statusCode.
    /// </summary>
    [HttpGet("authorizer/page")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetAuthorizerPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null,
        [FromQuery] string? searchType = "GG",
        [FromQuery] string[]? statusCodes = null,
        [FromQuery] string? mode = null
    )
    {
        if (!HasJobCardAuthorizerRole())
            return Forbid();

        var normalizedSearchType = (mode ?? searchType)?.Trim().ToUpperInvariant() ?? "GG";
        if (normalizedSearchType is not ("GG" or "GP"))
            return BadRequest(new { error = "Search type must be GG or GP." });

        if (!TryParseStatusCodes(statusCodes, out var parsedStatusCodes))
            return BadRequest(new { error = "Status codes must be integers." });

        try
        {
            var result = await _repository.GetAuthorizerPageAsync(
                new JobCardPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    search,
                    normalizedSearchType,
                    parsedStatusCodes.Length > 0 ? parsedStatusCodes : [1, 2],
                    JobCardId: null,
                    await GetAccessibleVmfCodesAsync()
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(MapToDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalRecords = result.TotalRecords,
                    total = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorizer job cards");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving authorizer job cards" }
            );
        }
    }

    /// <summary>
    /// Archive AuthorizerPerGGNumberView / AuthorizerVehicleView first grid uses
    /// DEV_SEL_JobcardsPerGGNumberAuthorizer with no parameters, or @ggnumber.
    /// BoundFields: GGNumber, Jobcards, Pending, AwaitingAuthorisation,
    /// Authorised, Inprogress, Canceled, Failed, Completed.
    /// </summary>
    [HttpGet("authorizer/gg-stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAuthorizerGgStats(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? ggNumber = null,
        [FromQuery] string? search = null
    )
    {
        if (!HasJobCardAuthorizerRole())
            return Forbid();

        var filter = (ggNumber ?? search)?.Trim();
        try
        {
            var overlay = await _repository.GetAuthorizerGgStatsAsync(
                new JobCardAuthorizerGgStatsQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    filter,
                    await GetAccessibleVmfCodesAsync()
                )
            );
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            return Ok(
                new
                {
                    overlay = true,
                    items = overlay.Items.Select(item => new
                    {
                        ggNumber = item.GgNumber,
                        jobcards = item.Jobcards,
                        pending = item.Pending,
                        awaitingAuthorisation = item.AwaitingAuthorisation,
                        authorised = item.Authorised,
                        inProgress = item.InProgress,
                        canceled = item.Canceled,
                        failed = item.Failed,
                        completed = item.Completed,
                    }),
                    page = overlay.Page,
                    pageSize = overlay.PageSize,
                    totalRecords = overlay.TotalRecords,
                    totalPages = overlay.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorizer job-card stats per GG number");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving authorizer job-card stats" }
            );
        }
    }

    /// <summary>
    /// Archive AuthorizerPerGGNumberView DetailsView uses
    /// DEV_SEL_JobcardAuthorizerDetails @ggNumber @extraCode.
    /// </summary>
    [HttpGet("authorizer/details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAuthorizerDetails(
        [FromQuery] int? jobCardId,
        [FromQuery] string? ggNumber,
        [FromQuery] string? extraCode
    )
    {
        if (!HasJobCardAuthorizerRole())
            return Forbid();

        try
        {
            JobCard? leftover = null;
            if (jobCardId is > 0)
            {
                leftover = await _repository.GetByIdAsync(jobCardId.Value);
                if (leftover is null || !await IsVehicleAllowedAsync(leftover.vmf_code))
                    return NotFound(new { error = "Job card not found." });
                ggNumber = leftover.Vehicle?.fleet_number;
                extraCode = leftover.extra_code.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                );
            }

            var trimmedGg = ggNumber?.Trim() ?? string.Empty;
            var trimmedExtra = extraCode?.Trim() ?? string.Empty;
            if (trimmedGg.Length == 0 || trimmedExtra.Length == 0)
                return BadRequest(new { error = "A GG number and extra code are required." });

            var overlay = await _repository.GetAuthorizerDetailsAsync(trimmedGg, trimmedExtra);
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            var item = overlay.FirstOrDefault();
            if (item is not null && leftover is not null)
            {
                item = item with { JobCardId = leftover.job_card_id };
            }

            return Ok(
                new
                {
                    overlay = true,
                    item = item is null
                        ? null
                        : new
                        {
                            jobCardId = item.JobCardId,
                            jcNumber = item.JcNumber,
                            ggNumber = item.GgNumber,
                            extraDescription = item.ExtraDescription,
                            initialCapturedDate = item.InitialCapturedDate,
                            initialCapturer = item.InitialCapturer,
                            barcode = item.Barcode,
                            capturedDate = item.CapturedDate,
                            jobCardsCapturer = item.JobCardsCapturer,
                            handoverName = item.HandoverName,
                            handoverDate = item.HandoverDate,
                            damages = item.Damages,
                            comments = item.Comments,
                            statusDescription = item.StatusDescription,
                            priority = item.Priority,
                            authorizer = item.Authorizer,
                            authorizedDate = item.AuthorizedDate,
                            authorizerComments = item.AuthorizerComments,
                        },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorizer job-card details");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving authorizer job-card details" }
            );
        }
    }

    /// <summary>
    /// Archive AuthorizerActionControl dropdown uses DEV_SEL_JobcardAuthorizerStatus
    /// with DataTextField status_code_description and DataValueField status_code.
    /// </summary>
    [HttpGet("authorizer/statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAuthorizerStatuses()
    {
        if (!HasJobCardAuthorizerRole())
            return Forbid();

        try
        {
            var overlay = await _repository.GetAuthorizerStatusCodesAsync();
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            return Ok(
                new
                {
                    overlay = true,
                    items = overlay.Select(status => new
                    {
                        statusCode = status.StatusCode,
                        description = status.Description,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorizer job-card statuses");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving authorizer job-card statuses" }
            );
        }
    }

    /// <summary>
    /// Archive VehiclesAvailableForJobcards uses DEV_SEL_NewVehiclesWithoutJobcards
    /// with no parameters. Membership and order come from GG Number.
    /// </summary>
    [HttpGet("vehicles-available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVehiclesAvailableForCapture()
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        try
        {
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var overlay = await _repository.GetVehiclesAvailableForCaptureAsync();
            IEnumerable<object> vehicles;
            if (overlay is not null)
            {
                vehicles = overlay
                    .Where(vehicle => allowedVehicles.Contains(vehicle.VmfCode))
                    .Select(vehicle => new
                    {
                        vmf_code = vehicle.VmfCode,
                        fleet_number = vehicle.FleetNumber,
                        registration_number = vehicle.RegistrationNumber,
                        model_code = vehicle.ModelCode,
                    });
            }
            else
            {
                vehicles = (await _vehicleRepository.GetAllAsync(
                    await _vehicleScope.ResolveAllowedSiteCodesAsync(
                        User,
                        HttpContext.RequestAborted
                    ),
                    GetCurrentUserId()
                ))
                    .Where(vehicle => allowedVehicles.Contains(vehicle.vmf_code))
                    .Select(vehicle => new
                    {
                        vmf_code = vehicle.vmf_code,
                        fleet_number = vehicle.fleet_number,
                        registration_number = vehicle.registration_number,
                        model_code = (short?)vehicle.model_code,
                    });
            }

            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicles available for job card capture");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving vehicles available for job cards" }
            );
        }
    }

    /// <summary>
    /// Archive CreateJobcard.aspx loads DEV_SEL_NewVehicleSummary @ggnumber,
    /// DEV_SEL_ExtrasInCategory @ggnumber, DEV_SEL_FittedExtras @ggnumber, and
    /// DEV_SEL_JobcardsOnStatus @ggnumber. Do not invent @CatID for the
    /// two-parameter extras procedure.
    /// </summary>
    [HttpGet("capture-context")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCaptureContext([FromQuery] string? ggNumber)
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        var trimmed = ggNumber?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return BadRequest(new { error = "A GG number is required." });

        try
        {
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var summary = await _repository.GetCaptureVehicleSummaryAsync(trimmed);
            var extras = await _repository.GetCaptureExtrasInCategoryAsync(trimmed);
            var fittedExtras = await _repository.GetCaptureFittedExtraDescriptionsAsync(trimmed);
            var jobcardsOnStatus = await _repository.GetCaptureJobcardsOnStatusDescriptionsAsync(
                trimmed
            );

            object summaryPayload;
            if (summary is null)
            {
                summaryPayload = new { overlay = false };
            }
            else
            {
                var item = summary.FirstOrDefault();
                if (item?.VmfCode is int vmfCode && !allowedVehicles.Contains(vmfCode))
                {
                    item = null;
                }

                summaryPayload = new
                {
                    overlay = true,
                    item = item is null
                        ? null
                        : new
                        {
                            vmf_code = item.VmfCode,
                            ggNumber = item.GgNumber,
                            registrationNumber = item.RegistrationNumber,
                            classDescription = item.ClassDescription,
                            modelDescription = item.ModelDescription,
                            odoReading = item.OdoReading,
                            vinNumber = item.VinNumber,
                            engineNumber = item.EngineNumber,
                            yearModel = item.YearModel,
                            purchasedFrom = item.PurchasedFrom,
                            hireType = item.HireType,
                            hiredFrom = item.HiredFrom,
                            location = item.Location,
                        },
                };
            }

            return Ok(
                new
                {
                    summary = summaryPayload,
                    extras = extras is null
                        ? new { overlay = false, items = Array.Empty<object>() }
                        : (object)
                            new
                            {
                                overlay = true,
                                items = extras.Select(extra => new
                                {
                                    extraCode = extra.ExtraCode,
                                    description = extra.Description,
                                }),
                            },
                    fittedExtras = fittedExtras is null
                        ? new { overlay = false, items = Array.Empty<string>() }
                        : (object)new { overlay = true, items = fittedExtras },
                    jobcardsOnStatus = jobcardsOnStatus is null
                        ? new { overlay = false, items = Array.Empty<string>() }
                        : (object)new { overlay = true, items = jobcardsOnStatus },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job-card capture context for {GGNumber}", trimmed);
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving the job-card capture context" }
            );
        }
    }

    /// <summary>
    /// Archive JobcardEditUpdateAndPrint DetailsView uses
    /// DEV_SEL_SpecificJobPerVehicle @ggNumber @extraCode.
    /// Do not overlay this onto authorizer details or leftover GetById.
    /// </summary>
    [HttpGet("capturer/details")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCapturerDetails(
        [FromQuery] int? jobCardId,
        [FromQuery] string? ggNumber,
        [FromQuery] string? extraCode
    )
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        try
        {
            JobCard? leftover = null;
            if (jobCardId is > 0)
            {
                leftover = await _repository.GetByIdAsync(jobCardId.Value);
                if (leftover is null || !await IsVehicleAllowedAsync(leftover.vmf_code))
                    return NotFound(new { error = "Job card not found." });
                ggNumber = leftover.Vehicle?.fleet_number;
                extraCode = leftover.extra_code.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                );
            }

            var trimmedGg = ggNumber?.Trim() ?? string.Empty;
            var trimmedExtra = extraCode?.Trim() ?? string.Empty;
            if (trimmedGg.Length == 0 || trimmedExtra.Length == 0)
                return BadRequest(new { error = "A GG number and extra code are required." });

            var overlay = await _repository.GetCapturerDetailsAsync(trimmedGg, trimmedExtra);
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            var item = overlay.FirstOrDefault();
            if (item is not null && leftover is not null)
            {
                item = item with { JobCardId = leftover.job_card_id };
            }

            return Ok(
                new
                {
                    overlay = true,
                    item = item is null
                        ? null
                        : new
                        {
                            jobCardId = item.JobCardId,
                            jcNumber = item.JcNumber,
                            ggNumber = item.GgNumber,
                            extraDescription = item.ExtraDescription,
                            barcode = item.Barcode,
                            initialCapturer = item.InitialCapturer,
                            initialCapturedDate = item.InitialCapturedDate,
                            jobCardsCapturer = item.JobCardsCapturer,
                            capturedDate = item.CapturedDate,
                            handoverName = item.HandoverName,
                            handoverDate = item.HandoverDate,
                            damages = item.Damages,
                            comments = item.Comments,
                            statusDescription = item.StatusDescription,
                            jobcardComment = item.JobcardComment,
                            authorizer = item.Authorizer,
                            authorizerDate = item.AuthorizerDate,
                            authorizerComments = item.AuthorizerComments,
                        },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capturer job-card details");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving capturer job-card details" }
            );
        }
    }

    /// <summary>
    /// Archive JobcardEditUpdateAndPrint status dropdown uses
    /// DEV_SEL_JobcardStatus with DataTextField status_code_description
    /// and DataValueField status_code.
    /// </summary>
    [HttpGet("capturer/statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCapturerStatuses()
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        try
        {
            var overlay = await _repository.GetCapturerStatusCodesAsync();
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            return Ok(
                new
                {
                    overlay = true,
                    items = overlay.Select(status => new
                    {
                        statusCode = status.StatusCode,
                        description = status.Description,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capturer job-card statuses");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving capturer job-card statuses" }
            );
        }
    }

    /// <summary>
    /// Archive PrintAssignedJobcards binds DEV_SEL_JobcardsForPrintingSummary
    /// @ggnumber with BoundFields Jobcard Number / GG Number /
    /// Registration Number / Jobcard Description.
    /// </summary>
    [HttpGet("print/summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPrintSummary([FromQuery] string? ggNumber)
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        var trimmed = ggNumber?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return BadRequest(new { error = "A GG number is required." });

        try
        {
            var overlay = await _repository.GetPrintableJobCardsAsync(trimmed);
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            return Ok(
                new
                {
                    overlay = true,
                    items = overlay.Select(item => new
                    {
                        jobcardNumber = item.JobcardNumber,
                        ggNumber = item.GgNumber,
                        registrationNumber = item.RegistrationNumber,
                        jobcardDescription = item.JobcardDescription,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting printable job cards for {GGNumber}", trimmed);
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving printable job cards" }
            );
        }
    }

    /// <summary>
    /// Archive PrintJobcard.aspx / JobcardData.xsd uses
    /// DEV_SEL_JobcardsForPrinting @ggNumber @jcNumber. Print-all binds
    /// @jcNumber as empty. Changes.PrintJobcardsPerGGNumber is the 1-param
    /// @ggnumber caller.
    /// </summary>
    [HttpGet("print/snapshot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPrintSnapshot(
        [FromQuery] string? ggNumber,
        [FromQuery] string? jcNumber
    )
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        var trimmedGg = ggNumber?.Trim() ?? string.Empty;
        if (trimmedGg.Length == 0)
            return BadRequest(new { error = "A GG number is required." });

        try
        {
            var overlay = await _repository.GetPrintJobCardsAsync(trimmedGg, jcNumber);
            if (overlay is null)
            {
                return Ok(new { overlay = false });
            }

            return Ok(
                new
                {
                    overlay = true,
                    items = overlay.Select(item => new
                    {
                        ggNumber = item.GgNumber,
                        registrationNumber = item.RegistrationNumber,
                        dateDelivered = item.DateDelivered,
                        odoReading = item.OdoReading,
                        vinNumber = item.VinNumber,
                        engineNumber = item.EngineNumber,
                        modelDescription = item.ModelDescription,
                        yearModel = item.YearModel,
                        classDescription = item.ClassDescription,
                        hireType = item.HireType,
                        hiredFrom = item.HiredFrom,
                        location = item.Location,
                        capturedDate = item.CapturedDate,
                        receivedBy = item.ReceivedBy,
                        status = item.Status,
                        statusDate = item.StatusDate,
                        purchasedFrom = item.PurchasedFrom,
                        purchasedDate = item.PurchasedDate,
                        jobcardNumber = item.JobcardNumber,
                        jobDescription = item.JobDescription,
                        jobcardStatus = item.JobcardStatus,
                        capturedBy = item.CapturedBy,
                        jcsDate = item.JcsDate,
                        assignedTo = item.AssignedTo,
                        assignedDate = item.AssignedDate,
                    }),
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting print job-card snapshot for {GGNumber}", trimmedGg);
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving the print job-card snapshot" }
            );
        }
    }

    /// <summary>
    /// Get a filtered page of job cards without changing the legacy unpaginated
    /// GET /api/jobcards response used by existing consumers.
    /// </summary>
    [HttpGet("page")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null,
        [FromQuery] string? searchType = "GG",
        [FromQuery] int? jobCardId = null,
        [FromQuery] string[]? statusCodes = null,
        [FromQuery] string? mode = null
    )
    {
        if (!HasJobCardAccess())
            return Forbid();

        var normalizedSearchType = (mode ?? searchType)?.Trim().ToUpperInvariant() ?? "GG";
        if (normalizedSearchType is not ("GG" or "GP"))
            return BadRequest(new { error = "Search type must be GG or GP." });

        if (!TryParseStatusCodes(statusCodes, out var parsedStatusCodes))
            return BadRequest(new { error = "Status codes must be integers." });

        if (jobCardId is <= 0)
            return BadRequest(new { error = "Job card ID must be a positive integer." });

        try
        {
            var result = await _repository.GetPageAsync(
                new JobCardPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    search,
                    normalizedSearchType,
                    parsedStatusCodes,
                    jobCardId,
                    await GetAccessibleVmfCodesAsync()
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(MapToDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    totalRecords = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged job cards");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving job cards" }
            );
        }
    }

    /// <summary>
    /// Get job card by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> GetById(int id)
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            _logger.LogInformation("Getting job card with ID: {JobCardId}", id);
            var jobCard = await _repository.GetByIdAsync(id);

            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }
            if (!await IsVehicleAllowedAsync(jobCard.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });

            return Ok(MapToDto(jobCard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job card {JobCardId}", id);
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving the job card" }
            );
        }
    }

    /// <summary>
    /// Get job cards by GG number (fleet number)
    /// Legacy: Replaces DEV_SEL_JobCards stored procedure
    /// </summary>
    [HttpGet("by-gg/{ggNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetByGGNumber(string ggNumber)
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            _logger.LogInformation("Getting job cards for GG number: {GGNumber}", ggNumber);
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var jobCards = (await _repository.GetByGGNumberAsync(ggNumber))
                .Where(jobCard => allowedVehicles.Contains(jobCard.vmf_code));
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job cards for GG number: {GGNumber}", ggNumber);
            return StatusCode(500, new { error = "An error occurred while retrieving job cards" });
        }
    }

    /// <summary>
    /// Get priority jobcards that are unassigned
    /// Legacy: Replaces DEV_SEL_JobcardPriotiyForCapturers stored procedure
    /// </summary>
    [HttpGet("priority/unassigned")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<JobCardResponseDto>>> GetPriorityUnassigned()
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            _logger.LogInformation("Getting priority unassigned job cards");
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var jobCards = (await _repository.GetPriorityUnassignedAsync())
                .Where(jobCard => allowedVehicles.Contains(jobCard.vmf_code));
            var dtos = jobCards.Select(MapToDto);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting priority unassigned job cards");
            return StatusCode(
                500,
                new { error = "An error occurred while retrieving priority unassigned job cards" }
            );
        }
    }

    /// <summary>
    /// Get the legacy priority-unassigned job cards as a bounded page.
    /// </summary>
    [HttpGet("priority/unassigned/page")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetPriorityUnassignedPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            var result = await _repository.GetPriorityUnassignedPageAsync(
                new PriorityUnassignedJobCardPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    await GetAccessibleVmfCodesAsync()
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(MapToDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged priority unassigned job cards");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving priority unassigned job cards" }
            );
        }
    }

    /// <summary>
    /// Archive capturer default assigned-priority grid uses
    /// DEV_SEL_JobcardsForAssignedPriority with no parameters.
    /// </summary>
    [HttpGet("priority/assigned/page")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetAssignedPriorityPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            var result = await _repository.GetAssignedPriorityPageAsync(
                new PriorityUnassignedJobCardPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    await GetAccessibleVmfCodesAsync()
                )
            );

            return Ok(
                new
                {
                    items = result.Items.Select(MapToDto),
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.TotalRecords,
                    totalPages = result.TotalPages,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged assigned priority job cards");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving assigned priority job cards" }
            );
        }
    }

    /// <summary>
    /// Create a new job card
    /// Legacy: Replaces DEV_INS_NewJobCards stored procedure
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Create(
        [FromBody] CreateJobCardDto createDto
    )
    {
        try
        {
            if (!HasJobCardCapturerRole())
                return Forbid();
            if (createDto is null)
                return BadRequest(new { error = "Job card data is required." });
            if (createDto.vmf_code <= 0 || createDto.extra_code <= 0)
                return BadRequest(new { error = "A valid vehicle and extra code are required." });

            int currentUserId = GetCurrentUserId();
            if (!await IsVehicleAllowedAsync(createDto.vmf_code))
                return Forbid();
            _logger.LogInformation(
                "Creating new job card for vehicle {VmfCode}, extra {ExtraCode} by user {UserId}",
                createDto.vmf_code,
                createDto.extra_code,
                currentUserId
            );

            var jobCard = new JobCard
            {
                vmf_code = createDto.vmf_code,
                extra_code = createDto.extra_code,
                status_code = 1, // Pending
                reviewed = "N",
            };

            var created = await _repository.CreateAsync(jobCard, currentUserId);
            var dto = MapToDto(created);

            _logger.LogInformation("Job card created with ID: {JobCardId}", created.job_card_id);
            return CreatedAtAction(nameof(GetById), new { id = created.job_card_id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job card");
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while creating the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Update an existing job card
    /// Legacy: Replaces DEV_UPD_Jobcards stored procedure
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Update(
        int id,
        [FromBody] UpdateJobCardDto updateDto
    )
    {
        try
        {
            if (!HasJobCardCapturerRole())
                return Forbid();
            if (updateDto is null)
                return BadRequest(new { error = "Job card data is required." });

            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "Updating job card {JobCardId} by user {UserId}",
                id,
                currentUserId
            );

            var existingJobCard = await _repository.GetByIdAsync(id);
            if (existingJobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }
            if (!await IsVehicleAllowedAsync(existingJobCard.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });

            if (
                !string.IsNullOrWhiteSpace(updateDto.damages)
                && !string.Equals(updateDto.damages, "Y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(updateDto.damages, "N", StringComparison.OrdinalIgnoreCase)
            )
            {
                return BadRequest(new { error = "Damages must be Y or N in the legacy job-card workflow." });
            }

            // Update fields
            existingJobCard.jcs_comment = updateDto.jcs_comment;
            existingJobCard.damages = updateDto.damages;
            existingJobCard.comments = updateDto.comments;
            existingJobCard.assigned_to = updateDto.assigned_to;
            existingJobCard.assigned_date = updateDto.assigned_date;
            if (updateDto.priority is not null)
                existingJobCard.priority = updateDto.priority;

            var updated = await _repository.UpdateAsync(existingJobCard, currentUserId);
            var dto = MapToDto(updated);

            _logger.LogInformation("Job card {JobCardId} updated successfully", id);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while updating the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Authorize a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (approve path)
    /// </summary>
    [HttpPost("{id}/authorize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Authorize(
        int id,
        [FromBody] JobCardAuthorizationDto? authDto = null
    )
    {
        try
        {
            if (!HasJobCardAuthorizerRole())
                return Forbid();

            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} authorizing job card {JobCardId}",
                currentUserId,
                id
            );

            // Fetch job card to validate self-approval prevention
            var jobCard = await _repository.GetByIdAsync(id);
            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }
            if (!await IsVehicleAllowedAsync(jobCard.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });

            // Prevent self-approval
            var selfApprovalCheck = ValidateSelfApprovalPrevention(jobCard, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            var authorized = await _repository.AuthorizeAsync(id, currentUserId, authDto?.comment);
            var dto = MapToDto(authorized);

            _logger.LogInformation(
                "Job card {JobCardId} authorized by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while authorizing the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Decline a job card
    /// Legacy: Replaces DEV_UPD_JobcardAuthorizersUpdates stored procedure (decline path)
    /// </summary>
    [HttpPost("{id}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Decline(
        int id,
        [FromBody] JobCardDeclineDto declineDto
    )
    {
        try
        {
            if (!HasJobCardAuthorizerRole())
                return Forbid();

            if (string.IsNullOrWhiteSpace(declineDto?.decline_reason))
            {
                return BadRequest(new { error = "Decline reason is required" });
            }

            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} declining job card {JobCardId} with reason: {Reason}",
                currentUserId,
                id,
                declineDto.decline_reason
            );

            // Fetch job card to validate self-approval prevention
            var jobCard = await _repository.GetByIdAsync(id);
            if (jobCard == null)
            {
                _logger.LogWarning("Job card not found with ID: {JobCardId}", id);
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            }
            if (!await IsVehicleAllowedAsync(jobCard.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });

            // Prevent self-review/decline
            var selfApprovalCheck = ValidateSelfApprovalPrevention(jobCard, currentUserId);
            if (selfApprovalCheck != null)
                return selfApprovalCheck;

            var declined = await _repository.DeclineAsync(
                id,
                currentUserId,
                declineDto.decline_reason
            );
            var dto = MapToDto(declined);

            _logger.LogInformation(
                "Job card {JobCardId} declined by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error declining job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while declining the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Cancel a job card
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Cancel(
        int id,
        [FromBody] JobCardCancelDto? cancelDto = null
    )
    {
        try
        {
            if (!HasJobCardCapturerRole())
                return Forbid();

            int currentUserId = GetCurrentUserId();
            _logger.LogInformation(
                "User {UserId} canceling job card {JobCardId}",
                currentUserId,
                id
            );

            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            if (!await IsVehicleAllowedAsync(existing.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });

            var canceled = await _repository.CancelAsync(
                id,
                currentUserId,
                cancelDto?.cancel_reason
            );
            var dto = MapToDto(canceled);

            _logger.LogInformation(
                "Job card {JobCardId} canceled by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while canceling the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// Close a job card
    /// </summary>
    [HttpPost("{id}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobCardResponseDto>> Close(
        int id,
        [FromBody] JobCardCloseDto? closeDto = null
    )
    {
        try
        {
            if (!HasJobCardCapturerRole())
                return Forbid();

            int currentUserId = GetCurrentUserId();
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            if (!await IsVehicleAllowedAsync(existing.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            _logger.LogInformation("User {UserId} closing job card {JobCardId}", currentUserId, id);

            if (
                !string.IsNullOrWhiteSpace(closeDto?.damages)
                && !string.Equals(closeDto.damages, "Y", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(closeDto.damages, "N", StringComparison.OrdinalIgnoreCase)
            )
            {
                return BadRequest(new { error = "Damages must be Y or N in the legacy job-card workflow." });
            }

            var closed = await _repository.CloseAsync(
                id,
                currentUserId,
                closeDto?.close_notes,
                labourCost: closeDto?.labour_cost,
                partsCost: closeDto?.parts_cost,
                otherCost: closeDto?.other_cost,
                invoiceNumber: closeDto?.invoice_number,
                invoiceDate: closeDto?.invoice_date,
                serviceProvider: closeDto?.service_provider,
                damages: closeDto?.damages,
                damageComment: closeDto?.damage_comment,
                barcode: closeDto?.barcode,
                closeDate: closeDto?.close_date
            );
            var dto = MapToDto(closed);

            _logger.LogInformation(
                "Job card {JobCardId} closed by user {UserId}",
                id,
                currentUserId
            );
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing job card {JobCardId}", id);
            return StatusCode(
                500,
                new { error = "An error occurred while closing the job card", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Delete a job card (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            if (!HasJobCardCapturerRole())
                return Forbid();

            int currentUserId = GetCurrentUserId();
            var existing = await _repository.GetByIdAsync(id);
            if (existing is null)
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            if (!await IsVehicleAllowedAsync(existing.vmf_code))
                return NotFound(new { error = $"Job card not found with ID: {id}" });
            _logger.LogInformation(
                "User {UserId} deleting job card {JobCardId}",
                currentUserId,
                id
            );

            await _repository.DeleteAsync(id, currentUserId);

            _logger.LogInformation(
                "Job card {JobCardId} deleted by user {UserId}",
                id,
                currentUserId
            );
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Job card not found: {JobCardId}", id);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job card {JobCardId}", id);
            return StatusCode(
                500,
                new
                {
                    error = "An error occurred while deleting the job card",
                    message = ex.Message,
                }
            );
        }
    }

    /// <summary>
    /// The original Jobcards workflow has no repair-cost mutation contract.
    /// This endpoint remains fail-closed rather than silently writing expanded-only fields.
    /// </summary>
    [HttpPatch("{id}/costs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<JobCardResponseDto> UpdateCosts(int id, [FromBody] JobCardCostDto costDto)
    {
        if (!HasJobCardCapturerRole())
            return Forbid();

        return Conflict(
            new
            {
                error = "Repair-cost capture is not available in the original Jobcards workflow and will not be saved as a modern-only approximation.",
            }
        );
    }

    /// <summary>
    /// Repair cost report — returns all closed job cards with their captured costs,
    /// filterable by vehicle, site/department, and date range.
    /// Used by client departments to query repair expenditure on their vehicles.
    /// Assumption: site_code filter uses the vehicle's current contract site.
    /// Confirm scope with users (QUESTIONS.md MX-3).
    /// </summary>
    [HttpGet("repair-cost-report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RepairCostReport(
        [FromQuery] int? vmfCode = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null
    )
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            var allowedVehicles = await GetAccessibleVmfCodesAsync();
            var results = (await _repository.GetByStatusAsync(5))
                .Where(jobCard => allowedVehicles.Contains(jobCard.vmf_code))
                .AsEnumerable();

            if (vmfCode.HasValue)
                results = results.Where(j => j.vmf_code == vmfCode.Value);

            if (fromDate.HasValue)
                results = results.Where(j => j.date_updated >= fromDate.Value);

            if (toDate.HasValue)
                results = results.Where(j => j.date_updated <= toDate.Value.AddDays(1));

            // Site filter: use the compatibility contract repository so the
            // original contract table does not go through EF's static model.
            if (siteCode.HasValue)
            {
                var vehiclesAtSite = (await _contractRepository.GetAllAsync())
                    .Where(c => c.site_code == siteCode.Value)
                    .Select(c => c.vmf_code)
                    .Distinct()
                    .ToHashSet();
                results = results.Where(j => vehiclesAtSite.Contains(j.vmf_code));
            }

            var orderedResults = results.OrderByDescending(j => j.date_updated).ToList();

            var lineItems = orderedResults
                .Select(j => new
                {
                    job_card_id = j.job_card_id,
                    vmf_code = j.vmf_code,
                    fleet_number = j.Vehicle?.fleet_number,
                    registration = j.Vehicle?.registration_number,
                    damages = j.damages,
                    service_provider = j.service_provider,
                    invoice_number = j.invoice_number,
                    invoice_date = j.invoice_date?.ToString("yyyy-MM-dd"),
                    labour_cost = j.labour_cost,
                    parts_cost = j.parts_cost,
                    other_cost = j.other_cost,
                    total_cost = j.total_cost,
                    closed_date = j.date_updated?.ToString("yyyy-MM-dd"),
                })
                .ToList();

            return Ok(
                new
                {
                    filters_applied = new
                    {
                        vmfCode,
                        siteCode,
                        fromDate,
                        toDate,
                    },
                    total_records = lineItems.Count,
                    grand_total = lineItems.Sum(i => i.total_cost ?? 0),
                    total_labour = lineItems.Sum(i => i.labour_cost ?? 0),
                    total_parts = lineItems.Sum(i => i.parts_cost ?? 0),
                    total_other = lineItems.Sum(i => i.other_cost ?? 0),
                    line_items = lineItems,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating repair cost report");
            return StatusCode(
                500,
                new { error = "Failed to generate report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Repair cost report with complete filtered totals and a server-side page
    /// of line items. The unpaged report endpoint remains unchanged.
    /// </summary>
    [HttpGet("repair-cost-report/page")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> RepairCostReportPage(
        [FromQuery] int? vmfCode = null,
        [FromQuery] short? siteCode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24
    )
    {
        if (!HasJobCardAccess())
            return Forbid();

        try
        {
            var accessibleVehicles = await GetAccessibleVmfCodesAsync();
            IReadOnlyCollection<int>? vehiclesAtSite = accessibleVehicles;
            if (siteCode.HasValue)
            {
                vehiclesAtSite = (await _contractRepository.GetAllAsync())
                    .Where(c => c.site_code == siteCode.Value)
                    .Select(c => c.vmf_code)
                    .Where(accessibleVehicles.Contains)
                    .Distinct()
                    .ToArray();
            }

            var result = await _repository.GetRepairCostReportPageAsync(
                new RepairCostReportPageQuery(
                    Math.Max(1, page),
                    Math.Clamp(pageSize, 1, 100),
                    vmfCode,
                    vehiclesAtSite,
                    fromDate,
                    toDate
                )
            );
            var lineItems = result
                .Items.Select(j => new
                {
                    job_card_id = j.job_card_id,
                    vmf_code = j.vmf_code,
                    fleet_number = j.Vehicle?.fleet_number,
                    registration = j.Vehicle?.registration_number,
                    damages = j.damages,
                    service_provider = j.service_provider,
                    invoice_number = j.invoice_number,
                    invoice_date = j.invoice_date?.ToString("yyyy-MM-dd"),
                    labour_cost = j.labour_cost,
                    parts_cost = j.parts_cost,
                    other_cost = j.other_cost,
                    total_cost = j.total_cost,
                    closed_date = j.date_updated?.ToString("yyyy-MM-dd"),
                })
                .ToList();

            return Ok(
                new
                {
                    filters_applied = new
                    {
                        vmfCode,
                        siteCode,
                        fromDate,
                        toDate,
                    },
                    total_records = result.TotalRecords,
                    grand_total = result.GrandTotal,
                    total_labour = result.TotalLabour,
                    total_parts = result.TotalParts,
                    total_other = result.TotalOther,
                    page = result.Page,
                    pageSize = result.PageSize,
                    total = result.TotalRecords,
                    totalPages = result.TotalPages,
                    line_items = lineItems,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating paged repair cost report");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Failed to generate report", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Validates that the current user is not the job card capturer (prevents self-approval)
    /// </summary>
    /// <returns>Null if validation passes, or ForbidResult with error message if validation fails</returns>
    private ActionResult? ValidateSelfApprovalPrevention(JobCard jobCard, int currentUserId)
    {
        // Check if current user is the job card capturer
        if (
            jobCard.created_by_user_code.HasValue
            && jobCard.created_by_user_code.Value == currentUserId
        )
        {
            _logger.LogWarning(
                "Self-approval blocked: User {UserId} attempted to authorize their own job card {JobCardId}",
                currentUserId,
                jobCard.job_card_id
            );

            return StatusCode(
                403,
                new
                {
                    error = "You cannot authorize or review your own job card.",
                    jobCardId = jobCard.job_card_id,
                    userId = currentUserId,
                }
            );
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Get friendly status text from status code
    /// </summary>
    private string GetStatusText(int statusCode)
    {
        return statusCode switch
        {
            0 => "Pending Review",
            1 => "Pending",
            2 => "Awaiting Authorization",
            3 => "Authorized",
            4 => "In Progress",
            5 => "Complete",
            6 => "Failed",
            7 => "Canceled",
            _ => "Unknown",
        };
    }

    private bool HasJobCardCapturerRole() => HasAnyRole(JobCardCapturerRole);

    private bool HasJobCardAuthorizerRole() => HasAnyRole(JobCardAuthorizerRole);

    private bool HasJobCardAccess() => HasJobCardCapturerRole() || HasJobCardAuthorizerRole();

    private async Task<bool> IsVehicleAllowedAsync(int vmfCode)
    {
        var allowedSites = await _vehicleScope.ResolveAllowedSiteCodesAsync(
            User,
            HttpContext.RequestAborted
        );
        return await _vehicleRepository.GetByIdAsync(
            vmfCode,
            allowedSites,
            GetCurrentUserId()
        ) is not null;
    }

    private async Task<HashSet<int>> GetAccessibleVmfCodesAsync()
    {
        var allowedSites = await _vehicleScope.ResolveAllowedSiteCodesAsync(
            User,
            HttpContext.RequestAborted
        );
        return (await _vehicleRepository.GetAllAsync(
            allowedSites,
            GetCurrentUserId()
        )).Select(vehicle => vehicle.vmf_code).ToHashSet();
    }

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
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
            expectedRoles.Any(expected =>
                string.Equals(role, expected, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private static bool TryParseStatusCodes(string[]? values, out int[] statusCodes)
    {
        var parsed = new List<int>();
        foreach (var value in values ?? [])
        {
            foreach (
                var token in value.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
            )
            {
                if (
                    !int.TryParse(
                        token,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var statusCode
                    )
                )
                {
                    statusCodes = [];
                    return false;
                }

                parsed.Add(statusCode);
            }
        }

        statusCodes = parsed.Distinct().ToArray();
        return true;
    }

    /// <summary>
    /// Map JobCard entity to JobCardResponseDto
    /// </summary>
    private JobCardResponseDto MapToDto(JobCard jobCard)
    {
        return new JobCardResponseDto
        {
            job_card_id = jobCard.job_card_id,
            vmf_code = jobCard.vmf_code,
            gg_number = jobCard.Vehicle?.fleet_number,
            registration_number = jobCard.Vehicle?.registration_number,
            extra_code = jobCard.extra_code,
            extra_description = jobCard.ExtraCodeRef?.extra_description,
            status_code = jobCard.status_code,
            status_text = GetStatusText(jobCard.status_code),
            priority = jobCard.priority,
            assigned_to = jobCard.assigned_to,
            assigned_to_name =
                jobCard.AssignedToUser?.email
                ?? jobCard.AssignedToUser?.user_access_code.ToString(),
            assigned_date = jobCard.assigned_date,
            jcs_comment = jobCard.jcs_comment,
            damages = jobCard.damages,
            comments = jobCard.comments,
            authorizer = jobCard.authorizer,
            authorizer_name =
                jobCard.AuthorizerUser?.email
                ?? jobCard.AuthorizerUser?.user_access_code.ToString(),
            reviewed = jobCard.reviewed,
            captured_by_user_code = jobCard.created_by_user_code,
            authorized_by_user_code = jobCard.authorizer,
            date_created = jobCard.date_created,
            date_updated = jobCard.date_updated,
            created_by_user_code = jobCard.created_by_user_code,
            modified_by_user_code = jobCard.modified_by_user_code,
            // Repair costs
            labour_cost = jobCard.labour_cost,
            parts_cost = jobCard.parts_cost,
            other_cost = jobCard.other_cost,
            total_cost = jobCard.total_cost,
            invoice_number = jobCard.invoice_number,
            invoice_date = jobCard.invoice_date,
            service_provider = jobCard.service_provider,
        };
    }
}

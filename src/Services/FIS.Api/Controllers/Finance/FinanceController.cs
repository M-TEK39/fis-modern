using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Threading;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FIS.Api.Services.Finance;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Core.Infrastructure.Repositories;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Finance operations API endpoints - Phase 1 implementation
/// Provides batch processing, BAS operations, tariff parameters, and financial integrations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
[ServiceFilter(typeof(LegacyFinanceAuthorizationFilter))]
public class FinanceController : BaseApiController
{
    private const int DefaultPageSize = 24;
    private const int MaximumPageSize = 100;
    private const int MaximumPage = 1_000_000;

    private readonly IJournalDetailService _journalService;
    private readonly LegacyFinanceReportExecutionService _legacyFinanceReportExecutionService;
    private readonly LegacyBasCompatibilityService _legacyBasCompatibilityService;
    private readonly LegacyWesbankCompatibilityService _legacyWesbankCompatibilityService;
    private readonly ITariffParameterRepository _tariffParameters;
    private readonly IOverheadRepository _overheads;
    private readonly IMaintenanceValueRepository _maintenanceValues;
    private readonly BasSegmentLookupOverlay _basSegmentLookup;
    private readonly FisDbContext _context;
    private readonly ILogger<FinanceController> _logger;
    private static readonly ConcurrentDictionary<Guid, ExportTaskState> ExportTasks = new();
    private static int _exportsCreated;
    private static int _exportsRunning;
    private static int _exportsCompleted;
    private static int _exportsCancelled;
    private static volatile bool _exportsCancelAll;

    public FinanceController(
        IJournalDetailService journalService,
        LegacyFinanceReportExecutionService legacyFinanceReportExecutionService,
        LegacyBasCompatibilityService legacyBasCompatibilityService,
        LegacyWesbankCompatibilityService legacyWesbankCompatibilityService,
        ITariffParameterRepository tariffParameters,
        IOverheadRepository overheads,
        IMaintenanceValueRepository maintenanceValues,
        BasSegmentLookupOverlay basSegmentLookup,
        FisDbContext context,
        ILogger<FinanceController> logger
    )
    {
        _journalService = journalService;
        _legacyFinanceReportExecutionService = legacyFinanceReportExecutionService;
        _legacyBasCompatibilityService = legacyBasCompatibilityService;
        _legacyWesbankCompatibilityService = legacyWesbankCompatibilityService;
        _tariffParameters = tariffParameters;
        _overheads = overheads;
        _maintenanceValues = maintenanceValues;
        _basSegmentLookup = basSegmentLookup;
        _context = context;
        _logger = logger;
    }

    #region Batch Operations

    [HttpGet("batch/status")]
    [LegacyFinanceBatchProgressRead]
    public async Task<ActionResult<BatchStatusDto>> GetBatchStatus()
    {
        try
        {
            var parameterProcedureAvailability = await GetLegacyProcedureAvailabilityAsync(
                ParameterValueRead
            );
            if (parameterProcedureAvailability == LegacyProcedureAvailability.Compatible)
            {
                var value = await ReadLegacyParameterAsync("BatchIsRunning");
                var isRunning = IsLegacyTrue(value);
                var dto = new BatchStatusDto
                {
                    BatchCode = 0,
                    Status = isRunning ? "Running" : "Not running",
                    IsActive = isRunning,
                };
                await OverlayArchivedBatchProgressAsync(dto);
                return Ok(dto);
            }

            if (parameterProcedureAvailability == LegacyProcedureAvailability.Incompatible)
            {
                return Conflict(
                    new
                    {
                        error = "The deployed DEV_SEL_ParameterValue procedure does not match the archived parameter contract. No batch-status fallback was used.",
                    }
                );
            }

            // A read-only compatibility fallback is retained only while the legacy
            // parameter procedure is absent. It does not authorize write shortcuts.
            var batch = await _context
                .Batches
                .OrderByDescending(b => b.batch_date)
                .ThenByDescending(b => b.batch_code)
                .FirstOrDefaultAsync();

            if (batch is null)
            {
                return Ok(
                    new BatchStatusDto
                    {
                        BatchCode = 0,
                        Status = "No active batch",
                        IsActive = false,
                    }
                );
            }

            var batchJournalDetails = (await _journalService.GetAllJournalDetailsAsync())
                .Where(jd => !jd.is_deleted && jd.journal_detail_date.Date == batch.batch_date.Date)
                .ToList();
            var totalTransactions = batchJournalDetails.Count;
            var processedTransactions = batchJournalDetails.Count(jd =>
                jd.journal_detail_date_posted.HasValue
            );

            return Ok(
                new BatchStatusDto
                {
                    BatchCode = batch.batch_code,
                    BatchDate = batch.batch_date,
                    Status =
                        processedTransactions < totalTransactions ? "In progress" : "Ready/Posted",
                    IsActive = processedTransactions < totalTransactions,
                    TotalTransactions = totalTransactions,
                    ProcessedTransactions = processedTransactions,
                }
            );
        }
        catch (LegacyBatchCompatibilityException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch status");
            return StatusCode(500, new { error = "Failed to fetch batch status" });
        }
    }

    [HttpPost("batch/start")]
    [LegacyFinanceBatchAccess]
    [ServiceFilter(typeof(LegacyFinanceBatchAuthorizationFilter))]
    public async Task<ActionResult<BatchStartResultDto>> StartBatch(
        [FromBody] StartBatchDto? request
    )
    {
        var requestedDate = request?.BatchDate.Date ?? DateTime.Today;
        if (requestedDate == DateTime.MinValue)
        {
            requestedDate = DateTime.Today;
        }
        if (requestedDate == DateTime.MaxValue)
        {
            return BadRequest(new { error = "Invalid batch date." });
        }

        try
        {
            var unavailable = await GetUnavailableLegacyProceduresAsync(
                ParameterValueRead,
                ParameterValueWrite,
                TriggerBatchJob
            );
            if (unavailable.Count > 0)
                return LegacyBatchProcedureUnavailable(unavailable);

            var batchJob = await ReadLegacyParameterAsync("BatchJob");
            if (string.IsNullOrWhiteSpace(batchJob))
            {
                return Conflict(
                    new { error = "The legacy BatchJob parameter is unavailable; batch processing was not started." }
                );
            }
            var legacyUsername = GetRequiredLegacyUsername();

            // This mirrors StartandEndBatch.aspx: mark the batch as running, then
            // enqueue the legacy job. Do not replace the job chain with modern DML.
            await WriteLegacyParameterAsync("BatchIsRunning", "true");
            await ExecuteLegacyProcedureAsync(
                TriggerBatchJob,
                new LegacyProcedureParameter("@BatchDate", DbType.DateTime, requestedDate),
                new LegacyProcedureParameter(
                    "@TriggerUser",
                    DbType.String,
                    legacyUsername
                ),
                new LegacyProcedureParameter("@JobName", DbType.String, batchJob),
                new LegacyProcedureParameter("@StepId", DbType.Int32, 1)
            );

            return Ok(
                new BatchStartResultDto
                {
                    BatchCode = 0,
                    BatchDate = requestedDate,
                    Success = true,
                    Message = "Legacy batch job was triggered and the site is marked as batch-running.",
                }
            );
        }
        catch (LegacyBatchCompatibilityException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting batch for date {BatchDate}", requestedDate);
            return StatusCode(500, new { error = "Failed to start batch" });
        }
    }

    [HttpPost("batch/check-scoa")]
    [LegacyFinanceBatchAccess]
    [ServiceFilter(typeof(LegacyFinanceBatchAuthorizationFilter))]
    public async Task<ActionResult<ScoaCheckResultDto>> CheckScoa()
    {
        try
        {
            var unavailable = await GetUnavailableLegacyProceduresAsync(
                CheckScoaProcedure
            );
            if (unavailable.Count > 0)
                return LegacyBatchProcedureUnavailable(unavailable);

            await ExecuteLegacyProcedureAsync(CheckScoaProcedure);

            return Ok(
                new ScoaCheckResultDto
                {
                    Message = "The legacy SCOA verification procedure completed. Review its recorded diagnostics before finishing the batch.",
                }
            );
        }
        catch (LegacyBatchCompatibilityException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SCOA compliance");
            return StatusCode(500, new { error = "Failed to check SCOA compliance" });
        }
    }

    [HttpPost("batch/rollback")]
    [LegacyFinanceBatchAccess]
    [ServiceFilter(typeof(LegacyFinanceBatchAuthorizationFilter))]
    public async Task<ActionResult> RollbackBatch()
    {
        try
        {
            var unavailable = await GetUnavailableLegacyProceduresAsync(
                ParameterValueRead,
                TriggerRollbackJob
            );
            if (unavailable.Count > 0)
                return LegacyBatchProcedureUnavailable(unavailable);

            var rollbackLocation = await ReadLegacyParameterAsync("BatchBackLocation");
            var rollbackJob = await ReadLegacyParameterAsync("RollbackJob");
            if (string.IsNullOrWhiteSpace(rollbackLocation) || string.IsNullOrWhiteSpace(rollbackJob))
            {
                return Conflict(
                    new
                    {
                        error = "The legacy BatchBackLocation or RollbackJob parameter is unavailable; batch rollback was not started.",
                    }
                );
            }

            await ExecuteLegacyProcedureAsync(
                TriggerRollbackJob,
                new LegacyProcedureParameter("@Location", DbType.String, rollbackLocation),
                new LegacyProcedureParameter("@User", DbType.String, GetRequiredLegacyUsername()),
                new LegacyProcedureParameter("@JobName", DbType.String, rollbackJob),
                new LegacyProcedureParameter("@StepId", DbType.Int32, 1)
            );

            return Ok(
                new
                {
                    message = "Legacy batch rollback job was triggered.",
                }
            );
        }
        catch (LegacyBatchCompatibilityException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back batch");
            return StatusCode(500, new { error = "Failed to roll back batch" });
        }
    }

    [HttpPost("batch/finish")]
    [LegacyFinanceBatchAccess]
    [ServiceFilter(typeof(LegacyFinanceBatchAuthorizationFilter))]
    public async Task<ActionResult> FinishBatch()
    {
        try
        {
            var unavailable = await GetUnavailableLegacyProceduresAsync(
                ParameterValueWrite
            );
            if (unavailable.Count > 0)
                return LegacyBatchProcedureUnavailable(unavailable);

            // StartandEndBatch.aspx only clears BatchIsRunning here. Posting and
            // journal mutation belong to the legacy batch job, not this finish action.
            await WriteLegacyParameterAsync("BatchIsRunning", "false");

            return Ok(
                new
                {
                    message = "Legacy batch-running flag cleared; the Finance site can be brought online.",
                }
            );
        }
        catch (LegacyBatchCompatibilityException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing batch");
            return StatusCode(500, new { error = "Failed to finalize batch" });
        }
    }

    #endregion

    #region BAS Operations

    [HttpPost("bas/import")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<BasImportResultDto>> ImportBas([FromBody] BasImportDto request)
    {
        if (string.IsNullOrWhiteSpace(request.FileData))
        {
            return BadRequest(
                new BasImportResultDto { Success = false, Message = "FileData is required." }
            );
        }

        try
        {
            var financeAccess = GetFinanceAccess();
            if (
                !financeAccess.CanMaintainAllFinanceData
                && (
                    financeAccess.Profile is null
                    || (
                        request.DepartmentCode.HasValue
                        && request.DepartmentCode.Value != financeAccess.Profile.DepartmentCode
                    )
                )
            )
            {
                return Forbid();
            }

            var fileBytes = DecodeFileBytes(request.FileData);
            var documents = ReadBasImportDocuments(fileBytes, out var documentError);
            if (documentError is not null)
            {
                return BadRequest(new BasImportResultDto { Success = false, Message = documentError });
            }

            if (documents.Count == 0)
            {
                return Ok(
                    new BasImportResultDto
                    {
                        Success = true,
                        RecordsImported = 0,
                        Message = "No BAS rows found in import file.",
                    }
                );
            }

            var errors = new List<string>();
            List<BasImportRow> parsedRows;
            var legacyDocuments = documents.Where(LooksLikeLegacyBasDocument).ToList();
            if (legacyDocuments.Count > 0)
            {
                if (legacyDocuments.Count != documents.Count)
                {
                    return BadRequest(
                        new BasImportResultDto
                        {
                            Success = false,
                            Message = "A BAS archive must contain only legacy BAS report files; mixed CSV and BAS document formats are not supported in one import.",
                        }
                    );
                }

                var parserDepartmentCode = request.DepartmentCode
                    ?? financeAccess.Profile?.DepartmentCode
                    ?? 0;
                parsedRows = new List<BasImportRow>();
                foreach (var document in legacyDocuments)
                {
                    if (
                        !TryParseLegacyBasDocument(
                            document,
                            parserDepartmentCode,
                            request.EndDate,
                            out var documentRows,
                            out var parseError
                        )
                    )
                    {
                        return BadRequest(
                            new BasImportResultDto
                            {
                                Success = false,
                                Message = parseError,
                            }
                        );
                    }

                    parsedRows.AddRange(documentRows);
                }
            }
            else
            {
                parsedRows = new List<BasImportRow>();
                foreach (
                    var rawLine in documents.SelectMany(document =>
                        document.Split(
                            new[] { "\r\n", "\n" },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                )
                {
                    var cols = ParseCsvLine(rawLine);
                    if (cols.Count < 4)
                    {
                        errors.Add($"Skipped invalid row: '{rawLine}'");
                        continue;
                    }

                    var segmentNumber = cols[0].Trim();
                    var segmentName = cols[1].Trim();
                    var groupCodeText = cols[2].Trim();
                    var departmentCodeText = cols[3].Trim();
                    var siteCodeText = cols.Count >= 5 ? cols[4].Trim() : string.Empty;

                    if (
                        string.Equals(segmentNumber, "segmentnumber", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    if (
                        !int.TryParse(
                            segmentNumber,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var segmentNumberValue
                        )
                        || segmentNumberValue < 0
                        || segmentNumber.Length > 8
                        || !int.TryParse(
                            groupCodeText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var segmentGroupCode
                        )
                        || segmentGroupCode <= 0
                        || !short.TryParse(
                            departmentCodeText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var departmentCode
                        )
                    )
                    {
                        errors.Add($"Skipped invalid row: '{rawLine}'");
                        continue;
                    }

                    short? siteCode = null;
                    if (
                        !string.IsNullOrWhiteSpace(siteCodeText)
                        && short.TryParse(siteCodeText, out var parsedSite)
                    )
                    {
                        siteCode = parsedSite;
                    }

                    parsedRows.Add(
                        new BasImportRow(
                            segmentNumber,
                            segmentName,
                            segmentGroupCode,
                            departmentCode,
                            siteCode
                        )
                    );
                }
            }

            if (parsedRows.Count == 0)
            {
                return Ok(
                    new BasImportResultDto
                    {
                        Success = errors.Count == 0,
                        RecordsImported = 0,
                        Errors = errors,
                        Message = errors.Count == 0
                            ? "No BAS rows found in import file."
                            : "No valid BAS rows were found in import file.",
                    }
                );
            }

            var targetDepartmentCode = request.DepartmentCode
                ?? (parsedRows.Select(row => row.DepartmentCode).Distinct().Count() == 1
                    ? parsedRows[0].DepartmentCode
                    : 0);
            if (targetDepartmentCode <= 0 || targetDepartmentCode > short.MaxValue)
            {
                return BadRequest(
                    new BasImportResultDto
                    {
                        Success = false,
                        Errors = errors,
                        Message = "Select one target department before importing BAS codes.",
                    }
                );
            }

            if (
                !financeAccess.CanMaintainAllFinanceData
                && (
                    financeAccess.Profile is null
                    || targetDepartmentCode != financeAccess.Profile.DepartmentCode
                )
            )
            {
                return Forbid();
            }

            var groups = parsedRows
                .GroupBy(row =>
                {
                    var number = row.SegmentNumber;
                    return new
                    {
                        row.SegmentGroupCode,
                        InstallationCode = number.Length >= 3 ? number[^3..] : string.Empty,
                    };
                })
                .Select(group =>
                    new BasImportGroup(
                        group.Key.SegmentGroupCode,
                        group.Key.InstallationCode,
                        group.ToList()
                    )
                )
                .ToList();
            if (groups.Any(group =>
                    group.InstallationCode.Length != 3
                    || !group.InstallationCode.All(char.IsDigit)))
            {
                return BadRequest(
                    new BasImportResultDto
                    {
                        Success = false,
                        Errors = errors,
                        Message = "Each BAS segment number must end with a three-digit installation code.",
                    }
                );
            }

            // Validate every group before issuing any mutation. The legacy
            // pages displayed a confirmation when the BAS installation link
            // did not match; do not silently import into a different department.
            var validatedGroups = new List<BasImportExecutionGroup>();
            var confirmationCodes = new HashSet<int>();
            foreach (var group in groups)
            {
                var validation = await _legacyBasCompatibilityService.TryValidateImportAsync(
                    targetDepartmentCode,
                    group.InstallationCode,
                    HttpContext.RequestAborted
                );
                if (validation is null)
                {
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new BasImportResultDto
                        {
                            Success = false,
                            Errors = errors,
                            Message = "The legacy BAS validation procedure is unavailable for this database; no BAS rows were written.",
                        }
                    );
                }

                if (validation.ActionCode is not (4 or 5 or 6))
                {
                    return Conflict(
                        new BasImportResultDto
                        {
                            Success = false,
                            RequiresConfirmation = false,
                            ActionCode = validation.ActionCode,
                            Errors = errors,
                            Message = DescribeBasValidation(validation),
                        }
                    );
                }

                var executionDepartmentCode = targetDepartmentCode;
                if (validation.ActionCode == 4)
                {
                    if (
                        validation.DocumentDepartmentCode <= 0
                        || validation.DocumentDepartmentCode > short.MaxValue
                    )
                    {
                        return Conflict(
                            new BasImportResultDto
                            {
                                Success = false,
                                ActionCode = validation.ActionCode,
                                Errors = errors,
                                Message = "The legacy BAS validation did not return the document department required for confirmation; no rows were written.",
                            }
                        );
                    }

                    executionDepartmentCode = validation.DocumentDepartmentCode;
                }

                if (validation.ActionCode is 4 or 5)
                {
                    confirmationCodes.Add(validation.ActionCode);
                }

                validatedGroups.Add(
                    new BasImportExecutionGroup(
                        group,
                        validation.ActionCode,
                        executionDepartmentCode
                    )
                );
            }

            if (confirmationCodes.Count > 0)
            {
                if (confirmationCodes.Count != 1 || !request.ConfirmationActionCode.HasValue)
                {
                    return Conflict(
                        new BasImportResultDto
                        {
                            Success = false,
                            RequiresConfirmation = true,
                            ActionCode = confirmationCodes.Count == 1
                                ? confirmationCodes.Single()
                                : null,
                            Errors = errors,
                            Message = "The legacy BAS validation requires an explicit confirmation. Re-submit the same document with its returned confirmation action code; no rows were written.",
                        }
                    );
                }

                if (!confirmationCodes.Contains(request.ConfirmationActionCode.Value))
                {
                    return BadRequest(
                        new BasImportResultDto
                        {
                            Success = false,
                            RequiresConfirmation = true,
                            ActionCode = confirmationCodes.Single(),
                            Errors = errors,
                            Message = "The BAS confirmation action code does not match the current legacy validation result. Re-submit the document and confirm the current action.",
                        }
                    );
                }
            }
            else if (request.ConfirmationActionCode.HasValue)
            {
                return BadRequest(
                    new BasImportResultDto
                    {
                        Success = false,
                        Message = "A BAS confirmation action code is only valid when legacy validation requests confirmation.",
                    }
                );
            }

            var effectiveDepartmentCodes = validatedGroups
                .Select(group => group.DepartmentCode)
                .Distinct()
                .ToList();
            if (effectiveDepartmentCodes.Count != 1)
            {
                return Conflict(
                    new BasImportResultDto
                    {
                        Success = false,
                        Errors = errors,
                        Message = "The BAS document contains installation links for more than one department. Legacy FIS requires one department per import; no rows were written.",
                    }
                );
            }

            var effectiveDepartmentCode = effectiveDepartmentCodes[0];
            if (
                !financeAccess.CanMaintainAllFinanceData
                && (
                    financeAccess.Profile is null
                    || effectiveDepartmentCode != financeAccess.Profile.DepartmentCode
                )
            )
            {
                return Forbid();
            }

            var startDate = request.StartDate?.Date ?? DateTime.Today;
            var endDate = request.EndDate?.Date ?? GetDefaultBasEndDate(startDate);
            if (endDate < startDate)
            {
                return BadRequest(
                    new BasImportResultDto
                    {
                        Success = false,
                        Errors = errors,
                        Message = "The BAS end date cannot be before the start date.",
                    }
                );
            }

            var inserted = 0;
            var updated = 0;
            var deleted = 0;
            foreach (var executionGroup in validatedGroups)
            {
                var group = executionGroup.Group;
                var document = new XElement(
                    "root",
                    group.Rows.Select(row =>
                        new XElement(
                            "segment",
                            new XAttribute("segment_number", row.SegmentNumber),
                            new XAttribute("segment_name", row.SegmentName ?? string.Empty),
                            new XAttribute("segment_group_code", row.SegmentGroupCode),
                            new XAttribute("department_code", executionGroup.DepartmentCode),
                            new XAttribute(
                                "segment_start_date",
                                startDate.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                            ),
                            new XAttribute(
                                "segment_end_date",
                                endDate.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                            ),
                            new XAttribute("segment_active", "1")
                        )
                    )
                ).ToString(SaveOptions.DisableFormatting);
                var result = await _legacyBasCompatibilityService.TryImportAsync(
                    document,
                    executionGroup.DepartmentCode,
                    group.InstallationCode,
                    executionGroup.ActionCode,
                    group.SegmentGroupCode,
                    HttpContext.RequestAborted
                );
                if (result is null)
                {
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        new BasImportResultDto
                        {
                            Success = false,
                            RecordsImported = inserted + updated,
                            RecordsInserted = inserted,
                            RecordsUpdated = updated,
                            RecordsDeleted = deleted,
                            Errors = errors,
                            Message = "The legacy BAS import procedure is unavailable for this database; no direct-DML fallback was used.",
                        }
                    );
                }

                inserted += result.Inserted;
                updated += result.Updated;
                deleted += result.Deleted;
            }

            return Ok(
                new BasImportResultDto
                {
                    Success = true,
                    RecordsImported = inserted + updated,
                    RecordsInserted = inserted,
                    RecordsUpdated = updated,
                    RecordsDeleted = deleted,
                    Errors = errors,
                    Message =
                        errors.Count == 0
                            ? $"Imported/updated {inserted + updated} BAS segment row(s)."
                            : $"Imported/updated {inserted + updated} BAS segment row(s) with {errors.Count} warning(s).",
                }
            );
        }
        catch (LegacyBasProcedureContractException ex)
        {
            _logger.LogError(ex, "Legacy BAS procedure contract mismatch");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new BasImportResultDto
                {
                    Success = false,
                    Message = $"The deployed legacy BAS procedure {ex.ProcedureName} does not match the archived contract; no direct-DML fallback was used.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing BAS segment codes");
            return StatusCode(
                500,
                new BasImportResultDto
                {
                    Success = false,
                    Message = "Failed to import BAS segment codes.",
                    Errors = new List<string> { ex.Message },
                }
            );
        }
    }

    [HttpGet("bas")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<object>> GetBasOverview()
    {
        try
        {
            var activeSegments = await _context.BasSegments.CountAsync();
            var invalidJournals = await _context.JournalWithInvalidBasCodes.CountAsync();
            var uninvoicedJournals = (await _journalService.GetAllJournalDetailsAsync()).Count(x =>
                !x.is_deleted && !x.journal_detail_date_posted.HasValue
            );

            return Ok(
                new
                {
                    activeSegments,
                    invalidJournals,
                    uninvoicedJournals,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching BAS overview");
            return StatusCode(500, new { error = "Failed to fetch BAS overview" });
        }
    }

    [HttpGet("bas/segments")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<BasSegmentDto>>> GetBasSegments(
        [FromQuery] int? departmentCode,
        [FromQuery] string? segmentType
    )
    {
        try
        {
            IReadOnlyList<string>? orderedNumbers = null;
            if (
                departmentCode.HasValue
                && byte.TryParse(segmentType?.Trim(), out var typeCode)
                && typeCode is >= 1 and <= 6
            )
            {
                orderedNumbers = await _basSegmentLookup.GetOrderedSegmentNumbersAsync(
                    departmentCode.Value,
                    typeCode
                );
                if (orderedNumbers is null)
                {
                    _logger.LogWarning(
                        "DEV_SEL_BASSegments unavailable; returning leftover bassegment rows"
                    );
                }
                else if (orderedNumbers.Count == 0)
                {
                    return Ok(Array.Empty<BasSegmentDto>());
                }
            }

            var query =
                from seg in _context.BasSegments.AsNoTracking()
                join grp in _context.SegmentGroups.AsNoTracking()
                    on seg.segment_group_code equals grp.segment_group_code
                    into segGroups
                from grp in segGroups.DefaultIfEmpty()
                join typ in _context.SegmentTypes.AsNoTracking()
                    on grp.segment_type_code equals typ.segment_type_code
                    into segmentTypes
                from typ in segmentTypes.DefaultIfEmpty()
                select new
                {
                    seg.segment_code,
                    seg.segment_number,
                    seg.segment_name,
                    seg.department_code,
                    SegmentTypeCode = typ != null ? typ.segment_type_code : (byte?)null,
                    SegmentTypeName = typ != null ? typ.segment_type_name : null,
                };

            if (orderedNumbers is null)
            {
                if (departmentCode.HasValue)
                {
                    var dept = (short)departmentCode.Value;
                    query = query.Where(x => x.department_code == dept);
                }

                if (!string.IsNullOrWhiteSpace(segmentType))
                {
                    var token = segmentType.Trim();
                    query = query.Where(x =>
                        (
                            x.SegmentTypeName != null
                            && EF.Functions.Like(x.SegmentTypeName, $"%{token}%")
                        ) || (x.SegmentTypeCode.HasValue && x.SegmentTypeCode.Value.ToString() == token)
                    );
                }
            }
            else
            {
                var dept = (short)departmentCode.GetValueOrDefault();
                var keys = orderedNumbers.ToList();
                // The procedure returns RTRIM(segment_number); SQL Server equality ignores trailing spaces.
                query = query
                    .Where(x => x.department_code == dept)
                    .Where(x => x.segment_number != null && keys.Contains(x.segment_number));
            }

            var rowsQuery = query.OrderBy(x => x.segment_number).ThenBy(x => x.segment_code);
            var rows =
                orderedNumbers is null
                    ? await rowsQuery.Take(2000).ToListAsync()
                    : await rowsQuery.ToListAsync();

            var orderedRows =
                orderedNumbers is null
                    ? rows
                    : BasSegmentLookupOverlay
                        .OrderByKeys(rows, orderedNumbers, x => x.segment_number?.Trim())
                        .Take(2000)
                        .ToList();

            var response = orderedRows.Select(x => new BasSegmentDto
                {
                    SegmentCode = x.segment_code,
                    SegmentNumber = x.segment_number ?? string.Empty,
                    SegmentType =
                        x.SegmentTypeName ?? x.SegmentTypeCode?.ToString() ?? string.Empty,
                    SegmentValue =
                        $"{x.segment_number ?? string.Empty} {x.segment_name ?? string.Empty}".Trim(),
                    DepartmentCode = x.department_code,
                    IsActive = true,
                })
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching BAS segments");
            return StatusCode(500, new { error = "Failed to fetch BAS segments" });
        }
    }

    /// <summary>
    /// Supplies the exact segment selectors used by the two legacy BAS
    /// correction pages. Its separate route preserves the stricter department
    /// selection rules on the normal importer and activation screens.
    /// </summary>
    [HttpGet("bas/segments/correction")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public Task<ActionResult<IEnumerable<BasSegmentDto>>> GetBasCorrectionSegments(
        [FromQuery] int? departmentCode,
        [FromQuery] string? segmentType
    ) => GetBasSegments(departmentCode, segmentType);

    /// <summary>
    /// Returns a bounded BAS segment page for the operational allocation grid.
    /// The original collection endpoint remains for legacy consumers.
    /// </summary>
    [HttpGet("bas/segments/page")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> GetBasSegmentsPage(
        [FromQuery] int? departmentCode,
        [FromQuery] string? segmentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasFinanceDataRole())
            return Forbid();

        try
        {
            var query =
                from seg in _context.BasSegments.AsNoTracking()
                join grp in _context.SegmentGroups.AsNoTracking()
                    on seg.segment_group_code equals grp.segment_group_code
                    into segGroups
                from grp in segGroups.DefaultIfEmpty()
                join typ in _context.SegmentTypes.AsNoTracking()
                    on grp.segment_type_code equals typ.segment_type_code
                    into segmentTypes
                from typ in segmentTypes.DefaultIfEmpty()
                select new
                {
                    seg.segment_code,
                    seg.segment_number,
                    seg.segment_name,
                    seg.department_code,
                    SegmentTypeCode = typ != null ? typ.segment_type_code : (byte?)null,
                    SegmentTypeName = typ != null ? typ.segment_type_name : null,
                };

            if (departmentCode.HasValue)
            {
                var department = checked((short)departmentCode.Value);
                query = query.Where(item => item.department_code == department);
            }

            if (!string.IsNullOrWhiteSpace(segmentType))
            {
                var token = segmentType.Trim();
                query = query.Where(item =>
                    (
                        item.SegmentTypeName != null
                        && EF.Functions.Like(item.SegmentTypeName, $"%{token}%")
                    )
                    || (
                        item.SegmentTypeCode.HasValue
                        && item.SegmentTypeCode.Value.ToString() == token
                    )
                );
            }

            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(item => item.segment_number)
                .ThenBy(item => item.segment_code)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(item => new BasSegmentDto
                {
                    SegmentCode = item.segment_code,
                    SegmentNumber = item.segment_number ?? string.Empty,
                    SegmentType =
                        item.SegmentTypeName
                        ?? (
                            item.SegmentTypeCode.HasValue
                                ? item.SegmentTypeCode.Value.ToString()
                                : string.Empty
                        ),
                    SegmentValue =
                        $"{item.segment_number ?? string.Empty} {item.segment_name ?? string.Empty}".Trim(),
                    DepartmentCode = item.department_code,
                    IsActive = true,
                })
                .ToListAsync();

            return Ok(CreatePageResponse(items, normalizedPage, normalizedPageSize, total));
        }
        catch (OverflowException)
        {
            return BadRequest(new { error = "Department code is outside the supported range." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged BAS segments");
            return StatusCode(500, new { error = "Failed to fetch BAS segments" });
        }
    }

    [HttpPost("bas/segments/activate")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> ActivateBasSegments([FromBody] ActivateSegmentsDto request)
    {
        if (request.SegmentCodes is null || request.SegmentCodes.Count == 0)
        {
            return BadRequest(new { error = "SegmentCodes are required." });
        }

        try
        {
            var financeAccess = GetFinanceAccess();
            var departmentCode = request.DepartmentCode
                ?? financeAccess.Profile?.DepartmentCode;
            if (departmentCode is null or <= 0 or > short.MaxValue)
            {
                return BadRequest(new { error = "Select a valid department before updating the BAS list." });
            }

            if (
                !financeAccess.CanMaintainAllFinanceData
                && (
                    financeAccess.Profile is null
                    || departmentCode.Value != financeAccess.Profile.DepartmentCode
                )
            )
            {
                return Forbid();
            }

            var document = new XElement(
                "root",
                request.SegmentCodes
                    .Distinct()
                    .Select(segmentCode =>
                        new XElement(
                            "segment",
                            new XAttribute("segment_code", segmentCode)
                        )
                    )
            ).ToString(SaveOptions.DisableFormatting);
            var result = await _legacyBasCompatibilityService.TryActivateAsync(
                document,
                departmentCode.Value,
                HttpContext.RequestAborted
            );
            if (result is null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "The legacy BAS activation procedure is unavailable for this database; no direct-DML fallback was used." }
                );
            }

            return Ok(
                new
                {
                    message = $"Updated {result.Updated} segment(s) in the legacy BAS list.",
                    requested = request.SegmentCodes.Count,
                    updated = result.Updated,
                    departmentCode = result.DepartmentCode,
                }
            );
        }
        catch (LegacyBasProcedureContractException ex)
        {
            _logger.LogError(ex, "Legacy BAS activation procedure contract mismatch");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = $"The deployed legacy BAS procedure {ex.ProcedureName} does not match the archived contract; no direct-DML fallback was used." }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating BAS segments");
            return StatusCode(500, new { error = "Failed to activate BAS segments" });
        }
    }

    [HttpGet("bas/journals/invalid")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<InvalidJournalDto>>> GetInvalidJournals(
        [FromQuery] int? departmentCode
    )
    {
        try
        {
            var legacyRows = await TryGetLegacyInvalidBasJournalsAsync(departmentCode);
            if (legacyRows is not null)
            {
                return Ok(legacyRows.Take(500).ToList());
            }

            var query = _context
                .JournalWithInvalidBasCodes.AsNoTracking();
            var permittedSiteNames = await GetProfileInvalidJournalSiteNamesAsync(
                departmentCode,
                permitBasCorrectionSelection: true
            );
            if (permittedSiteNames is not null)
            {
                query = query.Where(x =>
                    x.SiteName != null && permittedSiteNames.Contains(x.SiteName)
                );
            }

            var rows = await query
                .OrderByDescending(x => x.Id)
                .Take(500)
                .ToListAsync();

            var response = rows.Select(x => new InvalidJournalDto
                {
                    TransactionId = x.Id,
                    JournalNumber = x.GGNumber ?? string.Empty,
                    Reason = x.JournalType ?? "Invalid BAS code",
                    DepartmentCode = departmentCode,
                    JournalType = x.JournalType ?? string.Empty,
                    ResponsibilityNumber = x.ResponsibilityNumber ?? string.Empty,
                    ObjectiveNumber = x.ObjectiveNumber ?? string.Empty,
                    SiteName = x.SiteName ?? string.Empty,
                })
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching invalid BAS journals");
            return StatusCode(500, new { error = "Failed to fetch invalid BAS journals" });
        }
    }

    [HttpGet("bas/journals/invalid/page")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> GetInvalidJournalsPage(
        [FromQuery] int? departmentCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasFinanceDataRole())
            return Forbid();

        try
        {
            var legacyRows = await TryGetLegacyInvalidBasJournalsAsync(departmentCode);
            if (legacyRows is not null)
            {
                var legacyPage = NormalizePage(page);
                var legacyPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
                var legacyTotal = legacyRows.Count;
                var legacyItems = legacyRows
                    .Skip((legacyPage - 1) * legacyPageSize)
                    .Take(legacyPageSize)
                    .ToList();
                return Ok(CreatePageResponse(legacyItems, legacyPage, legacyPageSize, legacyTotal));
            }

            var query = _context
                .JournalWithInvalidBasCodes.AsNoTracking();
            var permittedSiteNames = await GetProfileInvalidJournalSiteNamesAsync(
                departmentCode,
                permitBasCorrectionSelection: true
            );
            if (permittedSiteNames is not null)
            {
                query = query.Where(item =>
                    item.SiteName != null && permittedSiteNames.Contains(item.SiteName)
                );
            }
            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(item => item.Id)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(item => new InvalidJournalDto
                {
                    TransactionId = item.Id,
                    JournalNumber = item.GGNumber ?? string.Empty,
                    Reason = item.JournalType ?? "Invalid BAS code",
                    DepartmentCode = departmentCode,
                    JournalType = item.JournalType ?? string.Empty,
                    ResponsibilityNumber = item.ResponsibilityNumber ?? string.Empty,
                    ObjectiveNumber = item.ObjectiveNumber ?? string.Empty,
                    SiteName = item.SiteName ?? string.Empty,
                })
                .ToListAsync();

            return Ok(CreatePageResponse(items, normalizedPage, normalizedPageSize, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged invalid BAS journals");
            return StatusCode(500, new { error = "Failed to fetch invalid BAS journals" });
        }
    }

    /// <summary>
    /// Legacy EditJournalBASCodes.aspx saved the responsibility and objective
    /// selections through DEV_UPD_FixJournalWithInvalidBASCodes. The target
    /// row, selected department, and BAS segments are all checked here before
    /// the fixed procedure can run.
    /// </summary>
    [HttpPost("bas/journals/invalid/fix")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> FixInvalidBasJournal([FromBody] FixInvalidBasJournalDto request)
    {
        if (
            request.TransactionId <= 0
            || request.DepartmentCode <= 0
            || string.IsNullOrWhiteSpace(request.Responsibility)
            || string.IsNullOrWhiteSpace(request.Objective)
            || request.Responsibility.Trim().Length > 8
            || request.Objective.Trim().Length > 8
        )
        {
            return BadRequest(
                new { error = "Select a department, responsibility, and objective before saving the BAS correction." }
            );
        }

        try
        {
            var access = GetFinanceAccess();
            var canSelectDepartment = access.CanSelectAllBASCorrectionDepartments;
            if (
                !canSelectDepartment
                && (access.Profile is null || access.Profile.DepartmentCode != request.DepartmentCode)
            )
            {
                return Forbid();
            }

            var legacyRows = await TryGetLegacyInvalidBasJournalsAsync(request.DepartmentCode);
            if (legacyRows is not null)
            {
                if (!legacyRows.Any(item => item.TransactionId == request.TransactionId))
                {
                    return NotFound(new { error = "The invalid BAS journal is no longer available for this department." });
                }
            }
            else
            {
                var permittedSiteNames = await GetProfileInvalidJournalSiteNamesAsync(
                    request.DepartmentCode,
                    permitBasCorrectionSelection: true
                );
                var invalidJournalQuery = _context
                    .JournalWithInvalidBasCodes.AsNoTracking()
                    .Where(item => item.Id == request.TransactionId);
                if (permittedSiteNames is not null)
                {
                    invalidJournalQuery = invalidJournalQuery.Where(item =>
                        item.SiteName != null && permittedSiteNames.Contains(item.SiteName)
                    );
                }

                if (await invalidJournalQuery.SingleOrDefaultAsync(HttpContext.RequestAborted) is null)
                {
                    return NotFound(new { error = "The invalid BAS journal is no longer available for this department." });
                }
            }

            var validSegmentCodes = await _context
                .BasSegments.AsNoTracking()
                .Join(
                    _context.SegmentGroups.AsNoTracking(),
                    segment => segment.segment_group_code,
                    group => group.segment_group_code,
                    (segment, group) => new { segment, group }
                )
                .Where(item =>
                    item.segment.department_code == request.DepartmentCode
                    && (
                        (item.group.segment_type_code == 3 && item.segment.segment_number == request.Responsibility.Trim())
                        || (item.group.segment_type_code == 2 && item.segment.segment_number == request.Objective.Trim())
                    )
                )
                .Select(item => new { item.group.segment_type_code, item.segment.segment_number })
                .ToListAsync(HttpContext.RequestAborted);
            var hasResponsibility = validSegmentCodes.Any(item =>
                item.segment_type_code == 3
                && string.Equals(
                    item.segment_number,
                    request.Responsibility.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            );
            var hasObjective = validSegmentCodes.Any(item =>
                item.segment_type_code == 2
                && string.Equals(
                    item.segment_number,
                    request.Objective.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            );
            if (!hasResponsibility || !hasObjective)
            {
                return BadRequest(
                    new { error = "Select active responsibility and objective BAS codes for the chosen department." }
                );
            }

            var saved = await _legacyFinanceReportExecutionService.TryExecuteMutationAsync(
                "fix-invalid-bas",
                new Dictionary<string, object?>
                {
                    ["JournalDetailID"] = request.TransactionId,
                    ["Responsibility"] = request.Responsibility.Trim(),
                    ["Objective"] = request.Objective.Trim(),
                },
                HttpContext.RequestAborted
            );
            if (!saved)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "The legacy BAS correction procedure is unavailable for this database." }
                );
            }

            return Ok(new { message = $"Saved BAS correction for journal row {request.TransactionId}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing invalid BAS journal {TransactionId}", request.TransactionId);
            return StatusCode(500, new { error = "Failed to save the BAS correction." });
        }
    }

    [HttpGet("bas/journals/uninvoiced")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<UninvoicedJournalDto>>> GetUninvoicedJournals(
        [FromQuery] int? departmentCode
    )
    {
        try
        {
            var legacyRows = await TryGetLegacyFundAllocationJournalsAsync(departmentCode);
            if (legacyRows is not null)
            {
                return Ok(legacyRows.Take(1000).ToList());
            }

            var query = (await _journalService.GetAllJournalDetailsAsync()).Where(jd =>
                !jd.is_deleted && !jd.journal_detail_date_posted.HasValue
            );

            if (departmentCode.HasValue)
                query = query.Where(jd => jd.department_code == (short)departmentCode.Value);

            var response = query
                .OrderByDescending(jd => jd.journal_detail_date)
                .Select(jd => new UninvoicedJournalDto
                {
                    JournalDetailCode = jd.journal_detail_code,
                    JournalNumber = jd.journal_code.HasValue
                        ? jd.journal_code.Value.ToString()
                        : jd.journal_detail_id.ToString(),
                    Amount = jd.journal_detail_amount,
                    TransactionDate = jd.journal_detail_date,
                    VmfCode = jd.vmf_code.ToString(CultureInfo.InvariantCulture),
                    JournalDetailTypeCode = jd.journal_detail_type_code,
                    SiteCode = jd.site_code,
                    DepartmentCode = jd.department_code,
                    JournalMonth = jd.journal_detail_date.ToString("yyyyMM", CultureInfo.InvariantCulture),
                })
                .Take(1000)
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching uninvoiced journals");
            return StatusCode(500, new { error = "Failed to fetch uninvoiced journals" });
        }
    }

    [HttpGet("bas/journals/uninvoiced/page")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> GetUninvoicedJournalsPage(
        [FromQuery] int? departmentCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasFinanceDataRole())
            return Forbid();

        try
        {
            var legacyRows = await TryGetLegacyFundAllocationJournalsAsync(departmentCode);
            if (legacyRows is not null)
            {
                var legacyPage = NormalizePage(page);
                var legacyPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
                var legacyTotal = legacyRows.Count;
                var legacyItems = legacyRows
                    .Skip((legacyPage - 1) * legacyPageSize)
                    .Take(legacyPageSize)
                    .ToList();
                return Ok(CreatePageResponse(legacyItems, legacyPage, legacyPageSize, legacyTotal));
            }

            var result = await _journalService.GetUninvoicedJournalDetailsPageAsync(
                departmentCode,
                NormalizePage(page),
                Math.Clamp(pageSize, 1, MaximumPageSize)
            );
            var items = result
                .Items.Select(item => new UninvoicedJournalDto
                {
                    JournalDetailCode = item.journal_detail_code,
                    JournalNumber =
                        item.journal_code?.ToString() ?? item.journal_detail_id.ToString(),
                    Amount = item.journal_detail_amount,
                    TransactionDate = item.journal_detail_date,
                    VmfCode = item.vmf_code.ToString(CultureInfo.InvariantCulture),
                    JournalDetailTypeCode = item.journal_detail_type_code,
                    SiteCode = item.site_code,
                    DepartmentCode = item.department_code,
                    JournalMonth = item.journal_detail_date.ToString("yyyyMM", CultureInfo.InvariantCulture),
                })
                .ToList();

            return Ok(CreatePageResponse(items, result.Page, result.PageSize, result.Total));
        }
        catch (OverflowException)
        {
            return BadRequest(new { error = "Department code is outside the supported range." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged un-invoiced BAS journals");
            return StatusCode(500, new { error = "Failed to fetch uninvoiced journals" });
        }
    }

    /// <summary>
    /// Legacy VehicleJournalBASCodesMap.aspx assigned a selected department's
    /// FUND code through DEV_INS_VehicleJournalSegmentMap. The request is
    /// revalidated against the original DEV_REP dataset (or the guarded
    /// compatibility projection when the procedure is absent), so hidden form
    /// values cannot target a journal that was not in the selected list.
    /// </summary>
    [HttpPost("bas/journals/uninvoiced/fund")]
    [LegacyFinanceBasMaintenanceAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> AssignFundCode([FromBody] AssignFundCodeDto request)
    {
        if (
            request.DepartmentCode <= 0
            || string.IsNullOrWhiteSpace(request.FundNumber)
            || string.IsNullOrWhiteSpace(request.VmfCode)
            || request.JournalDetailTypeCode <= 0
            || request.SiteCode <= 0
            || string.IsNullOrWhiteSpace(request.JournalMonth)
        )
        {
            return BadRequest(new { error = "Select a FUND code before saving the allocation." });
        }

        var fundNumber = request.FundNumber.Trim();
        if (fundNumber.Length > 8)
        {
            return BadRequest(new { error = "The selected FUND code is too long." });
        }

        try
        {
            var source = await ResolveFundAllocationJournalAsync(request);
            if (source is null)
            {
                return NotFound(new { error = "The un-invoiced journal is no longer available." });
            }

            var access = GetFinanceAccess();
            var canSelectDepartment = access.CanSelectAllBASCorrectionDepartments;
            if (
                !canSelectDepartment
                && (access.Profile is null || source.DepartmentCode != access.Profile.DepartmentCode)
            )
            {
                return Forbid();
            }

            var fundIsAvailable = await _context
                .BasSegments.AsNoTracking()
                .Join(
                    _context.SegmentGroups.AsNoTracking(),
                    segment => segment.segment_group_code,
                    group => group.segment_group_code,
                    (segment, group) => new { segment, group }
                )
                .AnyAsync(
                    item =>
                        item.segment.department_code == source.DepartmentCode
                        && item.group.segment_type_code == 1
                        && item.segment.segment_number == fundNumber,
                    HttpContext.RequestAborted
                );
            if (!fundIsAvailable)
            {
                return BadRequest(
                    new { error = "Select an active FUND code for the journal's department." }
                );
            }

            var saved = await _legacyFinanceReportExecutionService.TryExecuteMutationAsync(
                "assign-fund-bas",
                new Dictionary<string, object?>
                {
                    ["FUNDNumber"] = fundNumber,
                    ["VMFCode"] = source.VmfCode,
                    ["JournalDetailTypeCode"] = source.JournalDetailTypeCode,
                    ["SiteCode"] = source.SiteCode,
                    ["JournalMonth"] = source.JournalMonth,
                },
                HttpContext.RequestAborted
            );
            if (!saved)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new { error = "The legacy FUND allocation procedure is unavailable for this database." }
                );
            }

            return Ok(new { message = $"Assigned FUND code {fundNumber} to the selected journal." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning FUND code to journal {JournalDetailCode}", request.JournalDetailCode);
            return StatusCode(500, new { error = "Failed to assign the FUND code." });
        }
    }

    [HttpGet("bas/departments-without-bas")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult<IEnumerable<FinanceDepartmentDto>>> GetDepartmentsWithoutBas()
    {
        try
        {
            var departmentsWithBas = _context
                .BasSegments.Select(s => s.department_code)
                .Distinct();

            var response = await _context
                .Departments.AsNoTracking()
                .Where(d => !departmentsWithBas.Contains(d.department_code))
                .OrderBy(d => d.description)
                .Select(d => new FinanceDepartmentDto
                {
                    DepartmentCode = d.department_code,
                    DepartmentName = d.description ?? $"Department {d.department_code}",
                })
                .ToListAsync();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching departments without BAS");
            return StatusCode(500, new { error = "Failed to fetch departments without BAS" });
        }
    }

    [HttpGet("bas/departments-without-bas/page")]
    [LegacyFinanceDataAccess]
    [ServiceFilter(typeof(LegacyFinanceDataAuthorizationFilter))]
    public async Task<ActionResult> GetDepartmentsWithoutBasPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        if (!HasFinanceDataRole())
            return Forbid();

        try
        {
            var departmentsWithBas = _context
                .BasSegments.Select(segment => segment.department_code)
                .Distinct();
            var query = _context
                .Departments.AsNoTracking()
                .Where(department =>
                    !departmentsWithBas.Contains(department.department_code)
                );
            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(department => department.description)
                .ThenBy(department => department.department_code)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(department => new FinanceDepartmentDto
                {
                    DepartmentCode = department.department_code,
                    DepartmentName =
                        department.description ?? $"Department {department.department_code}",
                })
                .ToListAsync();

            return Ok(CreatePageResponse(items, normalizedPage, normalizedPageSize, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged departments without BAS");
            return StatusCode(500, new { error = "Failed to fetch departments without BAS" });
        }
    }

    [HttpGet("bas/departments-missing-financial-system")]
    [ServiceFilter(typeof(LegacyFinanceDepartment147AuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceOwnDepartmentDataAuthorizationFilter))]
    public async Task<
        ActionResult<IEnumerable<FinanceDepartmentDto>>
    > GetDepartmentsMissingFinancialSystem()
    {
        try
        {
            var response = await _context
                .Departments.AsNoTracking()
                .Where(d =>
                    !d.financial_system_code.HasValue || d.financial_system_code.Value == 0
                )
                .OrderBy(d => d.description)
                .Select(d => new FinanceDepartmentDto
                {
                    DepartmentCode = d.department_code,
                    DepartmentName = d.description ?? $"Department {d.department_code}",
                })
                .ToListAsync();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching departments missing financial system");
            return StatusCode(
                500,
                new { error = "Failed to fetch departments missing financial system" }
            );
        }
    }

    [HttpGet("bas/departments-missing-financial-system/page")]
    [ServiceFilter(typeof(LegacyFinanceDepartment147AuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceOwnDepartmentDataAuthorizationFilter))]
    public async Task<ActionResult> GetDepartmentsMissingFinancialSystemPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize
    )
    {
        try
        {
            var query = _context
                .Departments.AsNoTracking()
                .Where(department =>
                    !department.financial_system_code.HasValue
                    || department.financial_system_code.Value == 0
                );
            var normalizedPage = NormalizePage(page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(department => department.description)
                .ThenBy(department => department.department_code)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .Select(department => new FinanceDepartmentDto
                {
                    DepartmentCode = department.department_code,
                    DepartmentName =
                        department.description ?? $"Department {department.department_code}",
                })
                .ToListAsync();

            return Ok(CreatePageResponse(items, normalizedPage, normalizedPageSize, total));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching paged departments missing a financial system");
            return StatusCode(
                500,
                new { error = "Failed to fetch departments missing financial system" }
            );
        }
    }

    #endregion

    #region Reference Data

    [HttpGet("reference/financial-years")]
    public async Task<ActionResult<IEnumerable<FinancialYearDto>>> GetFinancialYears()
    {
        var years = await _legacyFinanceReportExecutionService.GetFinancialYearsAsync(
            HttpContext.RequestAborted
        );
        return Ok(
            years.Select(year => new FinancialYearDto
            {
                Code = year.Code,
                Name = year.Name,
                StartDate = year.StartDate ?? DateTime.MinValue,
                EndDate = year.EndDate ?? DateTime.MinValue,
            })
        );
    }

    [HttpGet("reference/batch-dates")]
    [LegacyFinanceHeadOfficeAccess]
    public async Task<ActionResult<IEnumerable<DateTime>>> GetBatchDates()
    {
        try
        {
            var dates = await _context
                .Batches.AsNoTracking()
                .Select(b => b.batch_date.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            return Ok(dates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching batch date list");
            return StatusCode(500, new { error = "Failed to fetch batch dates" });
        }
    }

    [HttpGet("reference/segment-types")]
    public ActionResult<IEnumerable<SegmentTypeDto>> GetSegmentTypes()
    {
        var types = new List<SegmentTypeDto>
        {
            new() { Code = "OBJ", Name = "Objective" },
            new() { Code = "RESP", Name = "Responsibility" },
        };
        return Ok(types);
    }

    #endregion

    #region Integration / Interface

    [HttpPost("interface/pastel-csv-all")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public async Task<ActionResult> ExportPastelCsvAll([FromBody] PastelExportDto request)
    {
        if (request.FinancialSystemCode != (byte)ExportFinancialSystem.All)
        {
            return BadRequest(new { error = "FinancialSystemCode must be ALL for this endpoint." });
        }

        var zipBytes = await BuildMultiSystemExportZipAsync(request, includeCustomerColumn: false);
        return File(
            zipBytes,
            "application/zip",
            $"pastel_export_all_{DateTime.UtcNow:yyyyMMddHHmmss}.zip"
        );
    }

    [HttpPost("interface/pastel-csv-customer-all")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public async Task<ActionResult> ExportPastelCsvCustomerAll(
        [FromBody] PastelCustomerExportDto request
    )
    {
        if (request.FinancialSystemCode != (byte)ExportFinancialSystem.All)
        {
            return BadRequest(new { error = "FinancialSystemCode must be ALL for this endpoint." });
        }

        var translatedAll = new PastelExportDto
        {
            BatchDate = request.BatchDate,
            DepartmentCode = request.DepartmentCode,
            FinancialSystemCode = request.FinancialSystemCode,
            BatchMode = request.BatchMode,
            SkipPosting = request.SkipPosting,
            ReverseBatch = request.ReverseBatch,
        };

        var zipBytes = await BuildMultiSystemExportZipAsync(
            translatedAll,
            includeCustomerColumn: true
        );
        return File(
            zipBytes,
            "application/zip",
            $"pastel_customers_all_{DateTime.UtcNow:yyyyMMddHHmmss}.zip"
        );
    }

    [HttpPost("interface/pastel-csv")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public async Task<ActionResult> ExportPastelCsv([FromBody] PastelExportDto request)
    {
        if (request.FinancialSystemCode == (byte)ExportFinancialSystem.All)
        {
            return BadRequest(
                new
                {
                    error = "FinancialSystem ALL is not valid for single export execution. Submit BAS, SAP, or MPI separately.",
                }
            );
        }

        var prepare = await PrepareExportContextAsync(request);
        if (!prepare.Success)
        {
            return BadRequest(new { error = prepare.ErrorMessage ?? "Invalid export request." });
        }

        if (request.FinancialSystemCode == (byte)ExportFinancialSystem.Mpi)
        {
            if (!request.SkipPosting)
            {
                await MarkExportRowsPostedAsync(
                    prepare.StartDate,
                    request.FinancialSystemCode,
                    request.DepartmentCode
                );
            }

            return Ok(
                new
                {
                    message = "MPI export has no file output. Posting workflow completed.",
                    batchCode = prepare.BatchCode,
                    batchDate = prepare.StartDate,
                }
            );
        }

        List<string> lines;
        try
        {
            lines = await BuildExportLinesAsync(
                prepare.StartDate,
                prepare.EndDate,
                request.DepartmentCode,
                includeCustomerColumn: false,
                reverseBatch: request.ReverseBatch
            );
        }
        catch (LegacyFinanceProcedureContractException ex)
        {
            _logger.LogError(ex, "Legacy Pastel CSV procedure contract is incompatible.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The legacy Pastel CSV procedure has an incompatible parameter contract.", source = "legacy-procedure-required" });
        }
        catch (LegacyFinanceProcedureUnavailableException ex)
        {
            _logger.LogError(ex, "Legacy Pastel CSV procedure is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The legacy Pastel CSV procedure is unavailable on this database.", source = "legacy-procedure-required" });
        }

        if (!request.SkipPosting)
        {
            await MarkExportRowsPostedAsync(
                prepare.StartDate,
                request.FinancialSystemCode,
                request.DepartmentCode
            );
        }

        var csvContent = string.Join('\n', lines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        var deptSuffix = request.DepartmentCode.HasValue
            ? $"-{request.DepartmentCode.Value}"
            : string.Empty;
        return File(
            bytes,
            "text/csv",
            $"pastel_export_{prepare.StartDate:yyyyMMdd}_{prepare.EndDate:yyyyMMdd}_batch{prepare.BatchCode}{deptSuffix}.csv"
        );
    }

    [HttpPost("interface/pastel-csv-customer")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public async Task<ActionResult> ExportPastelCsvCustomer(
        [FromBody] PastelCustomerExportDto request
    )
    {
        if (request.FinancialSystemCode == (byte)ExportFinancialSystem.All)
        {
            return BadRequest(
                new
                {
                    error = "FinancialSystem ALL is not valid for single export execution. Submit BAS, SAP, or MPI separately.",
                }
            );
        }

        var translated = new PastelExportDto
        {
            BatchDate = request.BatchDate,
            DepartmentCode = request.DepartmentCode,
            FinancialSystemCode = request.FinancialSystemCode,
            BatchMode = request.BatchMode,
            SkipPosting = request.SkipPosting,
            ReverseBatch = request.ReverseBatch,
        };

        var prepare = await PrepareExportContextAsync(translated);
        if (!prepare.Success)
        {
            return BadRequest(new { error = prepare.ErrorMessage ?? "Invalid export request." });
        }

        if (request.FinancialSystemCode == (byte)ExportFinancialSystem.Mpi)
        {
            if (!request.SkipPosting)
            {
                await MarkExportRowsPostedAsync(
                    prepare.StartDate,
                    request.FinancialSystemCode,
                    request.DepartmentCode
                );
            }

            return Ok(
                new
                {
                    message = "MPI export has no file output. Posting workflow completed.",
                    batchCode = prepare.BatchCode,
                    batchDate = prepare.StartDate,
                }
            );
        }

        List<string> lines;
        try
        {
            lines = await BuildExportLinesAsync(
                prepare.StartDate,
                prepare.EndDate,
                request.DepartmentCode,
                includeCustomerColumn: true,
                reverseBatch: request.ReverseBatch
            );
        }
        catch (LegacyFinanceProcedureContractException ex)
        {
            _logger.LogError(ex, "Legacy Pastel customer CSV procedure contract is incompatible.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The legacy Pastel customer CSV procedure has an incompatible parameter contract.", source = "legacy-procedure-required" });
        }
        catch (LegacyFinanceProcedureUnavailableException ex)
        {
            _logger.LogError(ex, "Legacy Pastel customer CSV procedure is unavailable.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The legacy Pastel customer CSV procedure is unavailable on this database.", source = "legacy-procedure-required" });
        }

        if (!request.SkipPosting)
        {
            await MarkExportRowsPostedAsync(
                prepare.StartDate,
                request.FinancialSystemCode,
                request.DepartmentCode
            );
        }

        var csvContent = string.Join('\n', lines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        var deptSuffix = request.DepartmentCode.HasValue
            ? $"-{request.DepartmentCode.Value}"
            : string.Empty;
        return File(
            bytes,
            "text/csv",
            $"pastel_customers_{prepare.StartDate:yyyyMMdd}_{prepare.EndDate:yyyyMMdd}_batch{prepare.BatchCode}{deptSuffix}.csv"
        );
    }

    [HttpPost("interface/export-async")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult StartAsyncExport([FromBody] PastelExportDto request)
    {
        if (request.FinancialSystemCode == (byte)ExportFinancialSystem.All)
        {
            return BadRequest(
                new
                {
                    error = "FinancialSystem ALL is not valid for single export execution. Submit BAS, SAP, or MPI separately.",
                }
            );
        }

        var taskResult = QueueAsyncExportTask(request);
        return Ok(new { taskId = taskResult.TaskId, status = taskResult.Status });
    }

    [HttpPost("interface/export-async-all")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult StartAsyncExportAll([FromBody] PastelExportDto request)
    {
        if (request.FinancialSystemCode != (byte)ExportFinancialSystem.All)
        {
            return BadRequest(new { error = "FinancialSystemCode must be ALL for this endpoint." });
        }

        var systems = new[]
        {
            ExportFinancialSystem.Bas,
            ExportFinancialSystem.Sap,
            ExportFinancialSystem.Mpi,
        };

        var queued = new List<object>();
        foreach (var system in systems)
        {
            var perSystem = new PastelExportDto
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                DepartmentCode = request.DepartmentCode,
                BatchDate = request.BatchDate,
                FinancialSystemCode = (byte)system,
                BatchMode = request.BatchMode,
                SkipPosting = request.SkipPosting,
                ReverseBatch = request.ReverseBatch,
            };

            var taskResult = QueueAsyncExportTask(perSystem);
            queued.Add(
                new
                {
                    system = system.ToString().ToUpperInvariant(),
                    taskId = taskResult.TaskId,
                    status = taskResult.Status,
                }
            );
        }

        return Ok(
            new { message = "Queued asynchronous exports for BAS, SAP and MPI.", tasks = queued }
        );
    }

    private static object CreatePageResponse<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int total
    ) =>
        new
        {
            items,
            page,
            pageSize,
            total,
            totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)),
        };

    private static int NormalizePage(int page) => Math.Clamp(page, 1, MaximumPage);

    private bool HasFinanceDataRole() =>
        HasAnyRole(
            "financial reports",
            "financial data (own department)",
            "financial data (all departments)",
            "administrator",
            "admin"
        );

    private bool HasAnyRole(params string[] expectedRoles)
    {
        if (expectedRoles.Any(User.IsInRole))
        {
            return true;
        }

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

    private LegacyFinanceAccessService.LegacyFinanceAccessContext GetFinanceAccess()
    {
        if (
            HttpContext.Items.TryGetValue(LegacyFinanceAccessService.AccessContextItemKey, out var value)
            && value is LegacyFinanceAccessService.LegacyFinanceAccessContext access
        )
        {
            return access;
        }

        throw new InvalidOperationException("The Finance access context was not resolved.");
    }

    private Task<bool> IsSiteInProfileDepartmentAsync(
        short siteCode,
        short departmentCode,
        CancellationToken cancellationToken
    ) =>
        _context.Sites.AsNoTracking().AnyAsync(
            site => site.Site_code == siteCode && site.Depatrment_code == departmentCode,
            cancellationToken
        );

    /// <summary>
    /// Uses the same DEV_REP_JournalsWithInvalidBASCodes dataset as
    /// EditJournalBASCodes.aspx whenever it is present. The EF projection is
    /// retained only for databases that do not contain that legacy procedure.
    /// </summary>
    private async Task<List<InvalidJournalDto>?> TryGetLegacyInvalidBasJournalsAsync(
        int? departmentCode
    )
    {
        if (
            !departmentCode.HasValue
            || departmentCode.Value <= 0
            || departmentCode.Value > short.MaxValue
        )
        {
            return null;
        }

        var scopedDepartmentCode = checked((short)departmentCode.Value);

        var report = await _legacyFinanceReportExecutionService.TryExecuteAsync(
            new LegacyFinanceProcedureRequest(
                "invalid-bas-journals",
                "Un-Invoiced Journals with Invalid BAS Codes",
                new Dictionary<string, object?> { ["DepartmentCode"] = scopedDepartmentCode }
            ),
            HttpContext.RequestAborted
        );
        if (report is null)
        {
            return null;
        }

        return report.DataRows
            .Select(row => new InvalidJournalDto
            {
                TransactionId = ReadLegacyReportLong(row, "TransactionID", "TransactionId", "Id"),
                JournalNumber = ReadLegacyReportText(row, "GGNumber", "JournalNumber"),
                Reason = ReadLegacyReportText(row, "JournalType", "Reason"),
                DepartmentCode = departmentCode,
                DocumentNumber = ReadLegacyReportText(row, "Document_Number", "DocumentNumber"),
                JournalType = ReadLegacyReportText(row, "JournalType"),
                ResponsibilityNumber = ReadLegacyReportText(
                    row,
                    "Enter_Correct_Responsibility_Number_Only",
                    "ResponsibilityNumber"
                ),
                ObjectiveNumber = ReadLegacyReportText(
                    row,
                    "Enter_Correct_Objective_Number_Only",
                    "ObjectiveNumber"
                ),
                JournalStart = ReadLegacyReportText(row, "Journal_Start", "JournalStart"),
                JournalEnd = ReadLegacyReportText(row, "Journal_End", "JournalEnd"),
                SiteName = ReadLegacyReportText(row, "SiteName", "Site_Name"),
            })
            .Where(item => item.TransactionId > 0)
            .ToList();
    }

    private static string ReadLegacyReportText(
        IReadOnlyDictionary<string, object> row,
        params string[] columnNames
    )
    {
        foreach (var columnName in columnNames)
        {
            var match = row.FirstOrDefault(item =>
                string.Equals(
                    NormalizeLegacyReportColumn(item.Key),
                    NormalizeLegacyReportColumn(columnName),
                    StringComparison.Ordinal
                )
            );
            if (!string.IsNullOrWhiteSpace(match.Key) && match.Value is not null)
            {
                return Convert.ToString(match.Value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static long ReadLegacyReportLong(
        IReadOnlyDictionary<string, object> row,
        params string[] columnNames
    ) => long.TryParse(
        ReadLegacyReportText(row, columnNames),
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var value
    )
        ? value
        : 0;

    private static string NormalizeLegacyReportColumn(string value) => new(
        value.Where(char.IsLetterOrDigit).ToArray()
    );

    private async Task<List<UninvoicedJournalDto>?> TryGetLegacyFundAllocationJournalsAsync(
        int? departmentCode
    )
    {
        if (
            !departmentCode.HasValue
            || departmentCode.Value <= 0
            || departmentCode.Value > short.MaxValue
        )
        {
            return null;
        }

        var scopedDepartmentCode = checked((short)departmentCode.Value);
        var report = await _legacyFinanceReportExecutionService.TryExecuteAsync(
            new LegacyFinanceProcedureRequest(
                "fund-code-allocation",
                "Allocate FUND Codes to Vehicle Journals",
                new Dictionary<string, object?> { ["DepartmentCode"] = scopedDepartmentCode }
            ),
            HttpContext.RequestAborted
        );
        if (report is null)
        {
            return null;
        }

        return report.DataRows
            .Select(row => new UninvoicedJournalDto
            {
                JournalNumber = ReadLegacyReportText(row, "GGNumber", "JournalNumber"),
                VmfCode = ReadLegacyReportText(row, "vmf_Code", "VMFCode"),
                JournalDetailTypeCode = ReadLegacyReportInt(
                    row,
                    "journal_detail_type_code",
                    "JournalDetailTypeCode"
                ),
                SiteCode = ReadLegacyReportInt(row, "Site_Code", "SiteCode"),
                DepartmentCode = scopedDepartmentCode,
                JournalMonth = ReadLegacyReportText(row, "Journal_Month", "JournalMonth"),
                JournalType = ReadLegacyReportText(row, "JournalType"),
                SiteName = ReadLegacyReportText(row, "SiteName", "Site_Name"),
            })
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.VmfCode)
                && item.JournalDetailTypeCode > 0
                && item.SiteCode > 0
                && !string.IsNullOrWhiteSpace(item.JournalMonth)
            )
            .ToList();
    }

    private async Task<UninvoicedJournalDto?> ResolveFundAllocationJournalAsync(
        AssignFundCodeDto request
    )
    {
        var legacyRows = await TryGetLegacyFundAllocationJournalsAsync(request.DepartmentCode);
        if (legacyRows is not null)
        {
            return legacyRows.SingleOrDefault(item =>
                string.Equals(item.VmfCode, request.VmfCode.Trim(), StringComparison.OrdinalIgnoreCase)
                && item.JournalDetailTypeCode == request.JournalDetailTypeCode
                && item.SiteCode == request.SiteCode
                && string.Equals(
                    item.JournalMonth,
                    request.JournalMonth.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        if (!Guid.TryParse(request.JournalDetailCode, out var journalDetailCode))
        {
            return null;
        }

        var journal = await _journalService.GetJournalDetailByCodeAsync(journalDetailCode);
        if (
            journal is null
            || journal.is_deleted
            || journal.journal_detail_date_posted.HasValue
            || journal.department_code != request.DepartmentCode
        )
        {
            return null;
        }

        var source = new UninvoicedJournalDto
        {
            JournalDetailCode = journal.journal_detail_code,
            JournalNumber = journal.journal_code?.ToString(CultureInfo.InvariantCulture)
                ?? journal.journal_detail_id.ToString(CultureInfo.InvariantCulture),
            VmfCode = journal.vmf_code.ToString(CultureInfo.InvariantCulture),
            JournalDetailTypeCode = journal.journal_detail_type_code,
            SiteCode = journal.site_code,
            DepartmentCode = journal.department_code,
            JournalMonth = journal.journal_detail_date.ToString("yyyyMM", CultureInfo.InvariantCulture),
        };
        return string.Equals(source.VmfCode, request.VmfCode.Trim(), StringComparison.OrdinalIgnoreCase)
            && source.JournalDetailTypeCode == request.JournalDetailTypeCode
            && source.SiteCode == request.SiteCode
            && string.Equals(
                source.JournalMonth,
                request.JournalMonth.Trim(),
                StringComparison.OrdinalIgnoreCase
            )
            ? source
            : null;
    }

    private static int ReadLegacyReportInt(
        IReadOnlyDictionary<string, object> row,
        params string[] columnNames
    ) => int.TryParse(
        ReadLegacyReportText(row, columnNames),
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var value
    )
        ? value
        : 0;

    /// <summary>
    /// The legacy invalid-BAS view exposes its department through the site name
    /// rather than a department column. Retain that relationship for the
    /// compatibility query so an own-department user cannot read another
    /// department's exception rows.
    /// </summary>
    private async Task<List<string>?> GetProfileInvalidJournalSiteNamesAsync(
        int? requestedDepartmentCode = null,
        bool permitBasCorrectionSelection = false
    )
    {
        var access = GetFinanceAccess();
        if (access.CanMaintainAllFinanceData && !permitBasCorrectionSelection)
        {
            return null;
        }

        if (access.Profile is null)
        {
            return [];
        }

        var departmentCode = access.Profile.DepartmentCode;
        if (
            permitBasCorrectionSelection
            && access.CanSelectAllBASCorrectionDepartments
            && requestedDepartmentCode is int selectedDepartmentCode
            && selectedDepartmentCode is > 0 and <= short.MaxValue
        )
        {
            departmentCode = checked((short)selectedDepartmentCode);
        }

        return await _context
            .Sites.AsNoTracking()
            .Where(site =>
                site.Depatrment_code == departmentCode
                && !string.IsNullOrWhiteSpace(site.description)
            )
            .Select(site => site.description!)
            .ToListAsync(HttpContext.RequestAborted);
    }

    private (Guid TaskId, string Status) QueueAsyncExportTask(PastelExportDto request)
    {
        if (_exportsCancelAll)
        {
            _exportsCancelAll = false;
        }

        var taskId = Guid.NewGuid();
        var state = new ExportTaskState
        {
            TaskId = taskId,
            Status = "Queued",
            CreatedAtUtc = DateTime.UtcNow,
            Request = request,
        };
        ExportTasks[taskId] = state;
        Interlocked.Increment(ref _exportsCreated);

        _ = Task.Run(async () =>
        {
            state.Status = "Running";
            state.ProgressPercent = 5;
            Interlocked.Increment(ref _exportsRunning);
            try
            {
                if (state.CancellationTokenSource.IsCancellationRequested || _exportsCancelAll)
                {
                    state.Status = "Cancelled";
                    state.ProgressPercent = 100;
                    Interlocked.Increment(ref _exportsCancelled);
                    return;
                }

                byte[] content;
                string contentType;
                string filename;

                var prepare = await PrepareExportContextAsync(request);
                if (!prepare.Success)
                {
                    state.Status = "Failed";
                    state.ErrorMessage = prepare.ErrorMessage ?? "Invalid export request.";
                    state.ProgressPercent = 100;
                    return;
                }

                if (request.FinancialSystemCode == (byte)ExportFinancialSystem.Mpi)
                {
                    if (!request.SkipPosting)
                    {
                        await MarkExportRowsPostedAsync(
                            prepare.StartDate,
                            request.FinancialSystemCode,
                            request.DepartmentCode
                        );
                    }

                    state.FileContent = System.Text.Encoding.UTF8.GetBytes(
                        "MPI export has no file output. Posting workflow completed."
                    );
                    state.FileContentType = "text/plain";
                    var mpiDeptSuffix = request.DepartmentCode.HasValue
                        ? $"-{request.DepartmentCode.Value}"
                        : string.Empty;
                    state.FileName =
                        $"mpi-posting-{prepare.StartDate:yyyyMMdd}-batch{prepare.BatchCode}{mpiDeptSuffix}.txt";
                    state.Status = "Completed";
                    state.ProgressPercent = 100;
                    state.CompletedAtUtc = DateTime.UtcNow;
                    Interlocked.Increment(ref _exportsCompleted);
                    return;
                }

                state.ProgressPercent = 30;
                var lines = await BuildExportLinesAsync(
                    prepare.StartDate,
                    prepare.EndDate,
                    request.DepartmentCode,
                    includeCustomerColumn: false,
                    reverseBatch: request.ReverseBatch
                );

                state.ProgressPercent = 70;
                if (!request.SkipPosting)
                {
                    await MarkExportRowsPostedAsync(
                        prepare.StartDate,
                        request.FinancialSystemCode,
                        request.DepartmentCode
                    );
                }

                content = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', lines));
                contentType = "text/csv";
                var deptSuffix = request.DepartmentCode.HasValue
                    ? $"-{request.DepartmentCode.Value}"
                    : string.Empty;
                filename =
                    $"pastel_export_{prepare.StartDate:yyyyMMdd}_{prepare.EndDate:yyyyMMdd}_batch{prepare.BatchCode}{deptSuffix}.csv";

                if (state.CancellationTokenSource.IsCancellationRequested || _exportsCancelAll)
                {
                    state.Status = "Cancelled";
                    state.ProgressPercent = 100;
                    Interlocked.Increment(ref _exportsCancelled);
                    return;
                }

                state.FileContent = content;
                state.FileContentType = contentType;
                state.FileName = filename;
                state.Status = "Completed";
                state.ProgressPercent = 100;
                state.CompletedAtUtc = DateTime.UtcNow;
                Interlocked.Increment(ref _exportsCompleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Async finance export failed for task {TaskId}", taskId);
                state.Status = "Failed";
                state.ErrorMessage = ex.Message;
                state.ProgressPercent = 100;
            }
            finally
            {
                Interlocked.Decrement(ref _exportsRunning);
            }
        });

        return (taskId, state.Status);
    }

    [HttpGet("interface/export-status/{taskId:guid}")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult GetAsyncExportStatus(Guid taskId)
    {
        if (!ExportTasks.TryGetValue(taskId, out var state))
        {
            return NotFound(new { error = "Export task not found." });
        }

        return Ok(
            new
            {
                taskId = state.TaskId,
                status = state.Status,
                progressPercent = state.ProgressPercent,
                createdAtUtc = state.CreatedAtUtc,
                completedAtUtc = state.CompletedAtUtc,
                errorMessage = state.ErrorMessage,
            }
        );
    }

    [HttpPost("interface/export-cancel/{taskId:guid}")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult CancelAsyncExport(Guid taskId)
    {
        if (!ExportTasks.TryGetValue(taskId, out var state))
        {
            return NotFound(new { error = "Export task not found." });
        }

        if (state.Status is "Completed" or "Failed" or "Cancelled")
        {
            return Ok(
                new
                {
                    taskId,
                    status = state.Status,
                    message = "Task already finalized.",
                }
            );
        }

        state.CancellationTokenSource.Cancel();
        state.Status = "Cancelled";
        state.ProgressPercent = 100;
        Interlocked.Increment(ref _exportsCancelled);

        return Ok(
            new
            {
                taskId,
                status = state.Status,
                message = "Cancellation requested.",
            }
        );
    }

    [HttpPost("interface/export-cancel-all")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult CancelAllAsyncExports()
    {
        _exportsCancelAll = true;

        var cancelled = 0;
        foreach (var item in ExportTasks.Values)
        {
            if (item.Status is "Completed" or "Failed" or "Cancelled")
            {
                continue;
            }

            item.CancellationTokenSource.Cancel();
            item.Status = "Cancelled";
            item.ProgressPercent = 100;
            cancelled++;
        }

        if (cancelled > 0)
        {
            Interlocked.Add(ref _exportsCancelled, cancelled);
        }

        return Ok(
            new { message = "Cancellation requested for all running/queued exports.", cancelled }
        );
    }

    [HttpGet("interface/export-runtime")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult GetExportRuntime()
    {
        var running = Volatile.Read(ref _exportsRunning);
        var created = Volatile.Read(ref _exportsCreated);
        var completed = Volatile.Read(ref _exportsCompleted);
        var cancelled = Volatile.Read(ref _exportsCancelled);

        return Ok(
            new
            {
                exportsCreated = created,
                exportsRunning = running,
                exportsCompleted = completed,
                exportsCancelled = cancelled,
                isRunning = running > 0,
                isComplete = created > 0 && created == completed + cancelled,
                cancelAllRequested = _exportsCancelAll,
            }
        );
    }

    [HttpGet("interface/export-download/{taskId:guid}")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    public ActionResult DownloadAsyncExport(Guid taskId)
    {
        if (!ExportTasks.TryGetValue(taskId, out var state))
        {
            return NotFound(new { error = "Export task not found." });
        }

        if (
            state.Status != "Completed"
            || state.FileContent is null
            || state.FileContent.Length == 0
        )
        {
            return BadRequest(new { error = "Export output not available for download." });
        }

        return File(
            state.FileContent,
            state.FileContentType ?? "application/octet-stream",
            state.FileName ?? $"finance-export-{taskId}.dat"
        );
    }

    /// <summary>
    /// Recreates the legacy Kilo Gaps data view. This remains separate from
    /// generic Finance reporting because legacy derives the gaps from adjacent
    /// vehicle-kilometre rows before filtering the resulting dataset.
    /// </summary>
    [HttpGet("missing-kilometres/kilo-gaps")]
    [ServiceFilter(typeof(LegacyFinanceFinancialReportsAuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceDepartment147AuthorizationFilter))]
    [ProducesResponseType(typeof(UniversalReport), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetKiloGapsReport([FromQuery] string financialYear)
    {
        if (
            !short.TryParse(
                financialYear,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var selectedFinancialYear
            )
        )
        {
            return BadRequest(new { error = "A valid financial year is required." });
        }

        try
        {
            var dataSet = await BuildKiloGapsReportDataSetAsync();
            var table = dataSet.Tables["KiloGapsTable"];
            if (table is null)
            {
                return StatusCode(500, new { error = "The kilometre gaps report could not be prepared." });
            }

            // Finance/OpenReport.aspx applies this DataView filter and sort to
            // both the PDF and XLS variants of the KiloGaps report.
            var rows = table.Rows.Cast<DataRow>()
                .Where(row =>
                    string.Equals(
                        SafeString(row, "Prev_Site"),
                        SafeString(row, "Next_Site"),
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !string.Equals(
                        SafeString(row, "Prev_contract_type"),
                        "VIP",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && !string.Equals(
                        SafeString(row, "Next_contract_type"),
                        "VIP",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && SafeShort(row, "Prev_FinancialYear") >= selectedFinancialYear - 1
                    && SafeShort(row, "Next_FinancialYear") == selectedFinancialYear
                )
                .OrderBy(row => SafeString(row, "Prev_Dept_Code"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => SafeString(row, "Prev_Site"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => SafeString(row, "Prev_fleet_number"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => SafeDouble(row, "Prev_start_odo"))
                .Select(row => table.Columns.Cast<DataColumn>().ToDictionary(
                    column => column.ColumnName,
                    column => row.IsNull(column) ? (object)string.Empty : row[column]
                ))
                .ToList();

            return Ok(
                new UniversalReport
                {
                    ReportType = "financial",
                    Title = "Missing Kilometres Report — Kilo Gaps in the Same Department and Site (VIP Excluded)",
                    GeneratedDate = DateTime.UtcNow,
                    DataRows = rows,
                    ReportData = new Dictionary<string, object>
                    {
                        ["financialYear"] = selectedFinancialYear,
                        ["sameSiteOnly"] = true,
                        ["excludeContractType"] = "VIP",
                    },
                    Summary = new Dictionary<string, object>
                    {
                        ["Total_Rows"] = rows.Count,
                    },
                    SupportsDateFilter = false,
                }
            );
        }
        catch (SqlException ex) when (ex.Number == 2812)
        {
            _logger.LogWarning(ex, "Legacy Kilo Gaps procedure is unavailable.");
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "The legacy vehicle-kilometres reporting procedure is unavailable." }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating legacy Kilo Gaps report");
            return StatusCode(500, new { error = "Failed to generate the kilometre gaps report." });
        }
    }

    [HttpPost("missing-kilometres/close-gaps")]
    [ServiceFilter(typeof(LegacyFinanceFinancialReportsAuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceCoisAuthorizationFilter))]
    public async Task<ActionResult> CloseKilometerGaps([FromBody] CloseKiloGapsRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.FinancialYear))
        {
            return BadRequest(new { error = "Financial year is required." });
        }

        if (
            !short.TryParse(
                request.FinancialYear,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var financialYear
            )
        )
        {
            return BadRequest(new { error = "Financial year is invalid." });
        }

        var currentUserId = GetCurrentUserId();
        var gapsDataSet = await BuildCloseKiloGapsDataSetAsync(financialYear, currentUserId);
        if (gapsDataSet.Tables.Count == 0 || gapsDataSet.Tables[0].Rows.Count == 0)
        {
            return Ok(
                new
                {
                    message = $"No Kilo gaps were found for Financial Year=[{financialYear}/{financialYear + 1}]. Therefore, no kilo gaps were closed.",
                    recordsProcessed = 0,
                }
            );
        }

        var xmlDoc = gapsDataSet.GetXml();
        var affected = await ExecuteCloseKiloGapsProcAsync(xmlDoc);

        return Ok(
            new
            {
                message = $"A total of {affected} kilo gaps (excluding VIP) were successfully closed for Financial Year=[{financialYear}/{financialYear + 1}].",
                recordsProcessed = affected,
            }
        );
    }

    [HttpGet("reports/reversals-tree/{journalNumber}")]
    public async Task<ActionResult> GetReversalTree(string journalNumber)
    {
        if (string.IsNullOrWhiteSpace(journalNumber))
        {
            return BadRequest(new { error = "Journal number is required." });
        }

        var trimmed = journalNumber.Trim();
        var hasJournalCode = long.TryParse(trimmed, out var journalCode);
        if (!hasJournalCode || journalCode is < 1 or > int.MaxValue)
        {
            return BadRequest(new { error = "A valid reversal journal number is required." });
        }

        var legacyReport = await _legacyFinanceReportExecutionService.TryExecuteAsync(
            new LegacyFinanceProcedureRequest(
                "reversals-tree",
                "Reversals Tree Report",
                new Dictionary<string, object?> { ["@ReversalJournalCode"] = (int)journalCode }
            ),
            HttpContext.RequestAborted
        );
        if (legacyReport is not null)
        {
            return Ok(legacyReport);
        }

        var hasJournalDetailId = int.TryParse(trimmed, out var journalDetailId);

        var journalDetails = (await _journalService.GetAllJournalDetailsAsync())
            .Where(jd => !jd.is_deleted)
            .ToList();
        var roots = journalDetails
            .Where(jd =>
                (hasJournalCode && jd.journal_code == journalCode)
                || (hasJournalDetailId && jd.journal_detail_id == journalDetailId)
            )
            .Select(jd => jd.journal_detail_code)
            .ToList();

        if (roots.Count == 0)
        {
            return NotFound(new { error = "Journal not found." });
        }

        var visited = new HashSet<Guid>(roots);
        var frontier = roots.ToList();
        var reversalRows = new List<ReversalNodeDto>();

        while (frontier.Count > 0)
        {
            var matches = journalDetails
                .Where(jd =>
                    jd.journal_detail_reversalof.HasValue
                    && frontier.Contains(jd.journal_detail_reversalof.Value)
                )
                .Select(jd => new
                {
                    jd.journal_detail_code,
                    jd.journal_code,
                    jd.journal_detail_id,
                    jd.journal_detail_date,
                    jd.journal_detail_amount,
                })
                .ToList();

            frontier = new List<Guid>();
            foreach (var row in matches)
            {
                if (!visited.Add(row.journal_detail_code))
                {
                    continue;
                }

                frontier.Add(row.journal_detail_code);
                reversalRows.Add(
                    new ReversalNodeDto
                    {
                        JournalNumber =
                            row.journal_code?.ToString(CultureInfo.InvariantCulture)
                            ?? row.journal_detail_id.ToString(CultureInfo.InvariantCulture),
                        ReversalDate = row.journal_detail_date,
                        Amount = row.journal_detail_amount,
                    }
                );
            }
        }

        var rows = reversalRows
            .OrderBy(item => item.ReversalDate)
            .ThenBy(item => item.JournalNumber)
            .Select(item => new Dictionary<string, object>
            {
                ["Journal Number"] = item.JournalNumber,
                ["Reversal Date"] = item.ReversalDate,
                ["Amount"] = item.Amount,
            })
            .ToList();
        return Ok(
            new UniversalReport
            {
                ReportType = "financial",
                Title = "Reversals Tree Report",
                GeneratedDate = DateTime.UtcNow,
                DataRows = rows,
                Summary = new Dictionary<string, object>
                {
                    ["Source"] = "compatibility-query",
                    ["Journal_Number"] = trimmed,
                    ["Total_Rows"] = rows.Count,
                },
                SupportsDateFilter = false,
            }
        );
    }

    [HttpPost("standard-bank/import")]
    [LegacyFinanceHeadOfficeAccess]
    [ServiceFilter(typeof(LegacyFinanceHeadOfficeAuthorizationFilter))]
    [RequestFormLimits(MultipartBodyLengthLimit = 20 * 1024 * 1024)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ImportResultDto>> ImportStandardBankData(
        [FromForm] IFormFile? file
    )
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded." });
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only CSV files can be uploaded." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _legacyWesbankCompatibilityService.TryImportAsync(
                stream,
                HttpContext.RequestAborted
            );
            if (result is null)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        error = "The legacy Standard Bank import procedure is not deployed. No direct-DML fallback was run.",
                    }
                );
            }

            var countMismatch = result.RecordsAdded != result.RecordsSubmitted;
            return Ok(
                new ImportResultDto
                {
                    Success = !countMismatch,
                    RecordsImported = result.RecordsAdded,
                    RecordsSubmitted = result.RecordsSubmitted,
                    RecordsFailed = countMismatch
                        ? Math.Max(0, result.RecordsSubmitted - result.RecordsAdded)
                        : 0,
                    Message = countMismatch
                        ? $"The legacy procedure completed but reported {result.RecordsAdded} newly stored row(s) for {result.RecordsSubmitted} source row(s)."
                        : $"Completed all records import. A total of {result.RecordsAdded} transactions were imported through the legacy procedure.",
                }
            );
        }
        catch (LegacyWesbankImportFormatException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (LegacyWesbankBatchRunningException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (LegacyWesbankDependencyException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (LegacyWesbankProcedureContractException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (SqlException ex) when (ex.Number is 207 or 208 or 2812)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    error = "The legacy Standard Bank import dependencies are unavailable or incompatible. No direct-DML fallback was run.",
                }
            );
        }
        catch (SqlException ex) when (
            ex.Number == 50000
            && ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
        )
        {
            return Conflict(new { error = "The legacy Standard Bank import was rejected because the transaction file has already been imported." });
        }
    }

    #endregion

    #region Tariff Parameters

    [HttpGet("tariff-parameters/years")]
    [LegacyFinanceTariffAccess]
    [ServiceFilter(typeof(LegacyFinanceTariffParametersAuthorizationFilter))]
    public async Task<IActionResult> GetTariffParameterYears()
    {
        try
        {
            var overlay = await _tariffParameters.GetLookupAsync();
            if (overlay is not null)
            {
                return Ok(
                    new
                    {
                        overlay = true,
                        items = overlay.Select(item => new
                        {
                            tariffParameterId = item.TariffParameterId,
                            dropdownText = item.DropdownText,
                        }),
                    }
                );
            }

            var years = (await _tariffParameters.GetAllAsync())
                .Select(tp => tp.TariffParameterYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            return Ok(new { overlay = false, years });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff parameter years");
            return StatusCode(500, new { error = "Failed to fetch tariff parameter years" });
        }
    }

    [HttpGet("tariff-parameters/id/{tariffParameterId:int}")]
    [LegacyFinanceTariffAccess]
    [ServiceFilter(typeof(LegacyFinanceTariffParametersAuthorizationFilter))]
    public async Task<ActionResult<TariffParametersDto>> GetTariffParametersById(
        int tariffParameterId
    )
    {
        try
        {
            var overlay = await _tariffParameters.GetBySelectorIdAsync(tariffParameterId);
            TariffParameter? param;
            if (overlay is null)
            {
                param = await _tariffParameters.GetByIdAsync(tariffParameterId);
            }
            else
            {
                param = overlay.FirstOrDefault();
            }

            return Ok(await MapTariffParametersAsync(param, param?.TariffParameterYear ?? 0, tariffParameterId));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error fetching tariff parameters for id {TariffParameterId}",
                tariffParameterId
            );
            return StatusCode(500, new { error = "Failed to fetch tariff parameters" });
        }
    }

    [HttpGet("tariff-parameters/{year}")]
    [LegacyFinanceTariffAccess]
    [ServiceFilter(typeof(LegacyFinanceTariffParametersAuthorizationFilter))]
    public async Task<ActionResult<TariffParametersDto>> GetTariffParameters(int year)
    {
        try
        {
            var param = await _tariffParameters.GetByYearAsync(year);
            return Ok(await MapTariffParametersAsync(param, year, param?.TariffParameterID ?? 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff parameters for year {Year}", year);
            return StatusCode(500, new { error = "Failed to fetch tariff parameters" });
        }
    }

    private async Task<TariffParametersDto> MapTariffParametersAsync(
        TariffParameter? param,
        int year,
        int tariffParameterId
    )
    {
        var globalParams = new List<TariffParameterItemDto>();
        bool isApproved = false;
        string? approvedBy = null;
        DateTime? effectiveDate = null;

        if (param != null)
        {
            isApproved = param.Approved;
            approvedBy = param.Approval_user_access_name;
            effectiveDate = param.EffectiveDate;

            globalParams.Add(
                new TariffParameterItemDto
                {
                    ParameterName = "Annual Interest Rate",
                    Value = param.AnnualInterestRatePercentage,
                    Unit = "%",
                }
            );
            globalParams.Add(
                new TariffParameterItemDto
                {
                    ParameterName = "Annual Payments",
                    Value = param.AnnualPayments,
                    Unit = "payments/year",
                }
            );
            if (param.EffectiveInterestRate.HasValue)
                globalParams.Add(
                    new TariffParameterItemDto
                    {
                        ParameterName = "Effective Interest Rate",
                        Value = param.EffectiveInterestRate.Value,
                        Unit = "%",
                    }
                );
            globalParams.Add(
                new TariffParameterItemDto
                {
                    ParameterName = "Pool Vehicle Charged Days/Month",
                    Value = param.PoolVehicleChargedDaysPerMonth,
                    Unit = "days",
                }
            );
            if (param.AverageFuelPrice.HasValue)
                globalParams.Add(
                    new TariffParameterItemDto
                    {
                        ParameterName = "Average Fuel Price",
                        Value = param.AverageFuelPrice.Value,
                        Unit = "R/litre",
                    }
                );
            if (param.AnnualRecoveredKilos.HasValue)
                globalParams.Add(
                    new TariffParameterItemDto
                    {
                        ParameterName = "Annual Recovered Kilometres",
                        Value = param.AnnualRecoveredKilos.Value,
                        Unit = "km",
                    }
                );
        }

        var overheads =
            tariffParameterId > 0
                ? await _overheads.GetSelectorAsync(tariffParameterId)
                : [];
        if (overheads is null && tariffParameterId > 0)
        {
            overheads = (await _overheads.GetByTariffParameterAsync(tariffParameterId))
                .Select(item => new TariffOverheadSnapshot(
                    item.OverheadId,
                    item.OverheadTypeId,
                    item.OverheadDescription,
                    null,
                    item.OverheadAmount,
                    item.OverheadNote
                ))
                .ToList();
        }

        static TariffOverheadRowDto MapOverhead(TariffOverheadSnapshot item) =>
            new()
            {
                OverheadId = item.OverheadId,
                OverheadDescription = item.Description ?? "",
                PreviousAmount = item.PreviousAmount,
                Amount = item.Amount,
                Note = item.Note,
            };

        var fixedTariffs = (overheads ?? [])
            .Where(item => item.OverheadTypeId == 1)
            .Select(MapOverhead)
            .ToList();
        var kiloTariffs = (overheads ?? [])
            .Where(item => item.OverheadTypeId == 2)
            .Select(MapOverhead)
            .ToList();

        List<MaintenanceValueRowDto> maintValues;
        var maintenanceOverlay =
            tariffParameterId > 0
                ? await _maintenanceValues.GetSelectorAsync(tariffParameterId)
                : [];
        if (maintenanceOverlay is null && tariffParameterId > 0)
        {
            var leftoverClasses = await _context.Classes.AsNoTracking().ToListAsync();
            maintValues = (await _maintenanceValues.GetByTariffParameterAsync(tariffParameterId))
                .Select(mv =>
                {
                    var classDescription = leftoverClasses
                        .FirstOrDefault(item => item.class_code == mv.class_code)
                        ?.description;
                    return new MaintenanceValueRowDto
                    {
                        ClassCode = mv.class_code,
                        ClassDescription = classDescription ?? $"Class {mv.class_code}",
                        ClassNumber = mv.class_number,
                        AssignedCount = null,
                        PreviousMonthsAge = null,
                        PreviousKilometerAge = null,
                        PreviousRandPerKilometer = null,
                        MonthsAge = mv.months_age,
                        KilometerAge = mv.kilometer_age,
                        Amount = mv.amount,
                        RandPerKilometer = mv.RandPerKilometer,
                    };
                })
                .OrderBy(mv => mv.ClassCode)
                .ThenBy(mv => mv.MonthsAge)
                .ToList();
        }
        else
        {
            maintValues = (maintenanceOverlay ?? [])
                .Select(item => new MaintenanceValueRowDto
                {
                    ClassCode = item.ClassCode,
                    ClassDescription = item.ClassDescription ?? "",
                    ClassNumber = item.ClassNumber,
                    AssignedCount = item.AssignedCount,
                    PreviousMonthsAge = item.PreviousMonthsAge,
                    PreviousKilometerAge = item.PreviousKilometerAge,
                    PreviousRandPerKilometer = item.PreviousRandPerKilometer,
                    MonthsAge = item.MonthsAge,
                    KilometerAge = item.KilometerAge,
                    Amount = item.Amount,
                    RandPerKilometer = item.RandPerKilometer,
                })
                .ToList();
        }

        return new TariffParametersDto
        {
            Year = year,
            IsApproved = isApproved,
            ApprovedBy = approvedBy,
            EffectiveDate = effectiveDate,
            Parameters = globalParams,
            FixedTariffs = fixedTariffs,
            KiloTariffs = kiloTariffs,
            MaintenanceValues = maintValues,
        };
    }

    [HttpPost("tariff-parameters/{year}/approve")]
    [LegacyFinanceTariffAccess]
    [ServiceFilter(typeof(LegacyFinanceTariffParametersAuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceTariffApproverAuthorizationFilter))]
    public async Task<ActionResult> ApproveTariffParameters(
        int year,
        [FromBody] ApproveTariffDto? request = null
    )
    {
        var currentUserId = GetCurrentUserId();

        var lookup = await _tariffParameters.GetLookupAsync();
        var tariff =
            lookup is not null
                ? await _tariffParameters.GetByIdAsync(year)
                : await _tariffParameters.GetByYearAsync(year);

        if (tariff is null)
        {
            return NotFound(new { error = $"No tariff parameters found for year {year}" });
        }

        var effectiveDate = request?.EffectiveDate?.Date ?? tariff.EffectiveDate?.Date;
        if (!effectiveDate.HasValue)
        {
            return BadRequest(
                new { error = "An effective date is required before tariff parameters can be approved." }
            );
        }

        var unavailable = await GetUnavailableLegacyProceduresAsync(UpdateTariffParameterProcedure);
        if (unavailable.Count > 0)
            return LegacyBatchProcedureUnavailable(unavailable);

        await ExecuteLegacyProcedureAsync(
            UpdateTariffParameterProcedure,
            new LegacyProcedureParameter("@TariffParameterID", DbType.Int32, tariff.TariffParameterID),
            new LegacyProcedureParameter("@TariffParameterYear", DbType.Int32, tariff.TariffParameterYear),
            new LegacyProcedureParameter(
                "@AnnualInterestRatePercentage",
                DbType.Decimal,
                tariff.AnnualInterestRatePercentage
            ),
            new LegacyProcedureParameter("@AnnualPayments", DbType.Byte, tariff.AnnualPayments),
            new LegacyProcedureParameter(
                "@PoolVehicleChargedDaysPerMonth",
                DbType.Byte,
                tariff.PoolVehicleChargedDaysPerMonth
            ),
            new LegacyProcedureParameter(
                "@CostCategoryMultiple",
                DbType.Int32,
                tariff.CostCategoryMultiple
            ),
            new LegacyProcedureParameter(
                "@AnnualRecoveredKilos",
                DbType.Int32,
                tariff.AnnualRecoveredKilos
            ),
            new LegacyProcedureParameter("@AverageFuelPrice", DbType.Decimal, tariff.AverageFuelPrice),
            new LegacyProcedureParameter("@EffectiveDate", DbType.Date, effectiveDate.Value),
            new LegacyProcedureParameter("@user_access_code", DbType.Int16, currentUserId),
            new LegacyProcedureParameter("@Approved", DbType.Boolean, true)
        );

        return Ok(
            new { message = $"Tariff parameters for {year} approved", approvedBy = currentUserId }
        );
    }

    [HttpPost("tariff-parameters/{year}/reject")]
    [LegacyFinanceTariffAccess]
    [ServiceFilter(typeof(LegacyFinanceTariffParametersAuthorizationFilter))]
    [ServiceFilter(typeof(LegacyFinanceTariffApproverAuthorizationFilter))]
    public ActionResult RejectTariffParameters(
        int year,
        [FromBody] RejectTariffDto? request = null
    )
    {
        return Conflict(
            new
            {
                error = "Rejecting an approved tariff year is not an original Fiscal Tariff Parameter Management action and is unavailable until a legacy workflow is evidenced.",
            }
        );
    }

    private async Task<ExportPreparationResult> PrepareExportContextAsync(PastelExportDto request)
    {
        var finSystemCode = request.FinancialSystemCode > 0 ? request.FinancialSystemCode : (byte)1;
        var batchMode = request.BatchMode ?? ExportBatchMode.BatchMustExist;

        if (finSystemCode == (byte)ExportFinancialSystem.All)
        {
            return ExportPreparationResult.Failed(
                "FinancialSystem ALL is not valid for single export call."
            );
        }

        var (startDate, endDate) = await ResolveExportDateWindowAsync(request);

        var existingBatch = await _context
            .Batches.AsNoTracking()
            .Where(b =>
                b.batch_date.Date == startDate.Date
                && b.financial_system_code == finSystemCode
            )
            .OrderByDescending(b => b.batch_code)
            .FirstOrDefaultAsync();
        var hasBatch = existingBatch is not null;
        var resolvedBatchCode = existingBatch?.batch_code ?? 0;

        if (!hasBatch)
        {
            if (batchMode == ExportBatchMode.BatchMustExist)
            {
                return ExportPreparationResult.Failed(
                    "Batch does not exist for requested date and financial system."
                );
            }

            if (
                batchMode == ExportBatchMode.BatchAppendOrCreate
                || batchMode == ExportBatchMode.BatchMustCreateNew
                || batchMode == ExportBatchMode.ExportNewSerialNumber
            )
            {
                var newBatchCode = await CreateBatchViaLegacyProcAsync(
                    startDate.Date,
                    finSystemCode
                );
                var newBatch = await _context
                    .Batches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.batch_code == newBatchCode);
                if (newBatch is null)
                {
                    return ExportPreparationResult.Failed(
                        $"Batch {newBatchCode} was created but could not be loaded."
                    );
                }

                await PrepareBatchJournalsAndMappingsAsync(
                    newBatch.batch_code,
                    newBatch.batch_date.Date,
                    newBatch.financial_system_code
                );
                resolvedBatchCode = newBatch.batch_code;
            }
            else
            {
                return ExportPreparationResult.Failed(
                    "Batch does not exist for requested date and financial system."
                );
            }
        }
        else if (batchMode == ExportBatchMode.ExportNewSerialNumber)
        {
            var newBatchCode = await CreateBatchViaLegacyProcAsync(startDate.Date, finSystemCode);
            var newBatch = await _context
                .Batches.AsNoTracking()
                .FirstOrDefaultAsync(b => b.batch_code == newBatchCode);
            if (newBatch is null)
            {
                return ExportPreparationResult.Failed(
                    $"Batch {newBatchCode} was created but could not be loaded."
                );
            }

            await PrepareBatchJournalsAndMappingsAsync(
                newBatch.batch_code,
                newBatch.batch_date.Date,
                newBatch.financial_system_code
            );
            resolvedBatchCode = newBatch.batch_code;
        }
        else if (batchMode == ExportBatchMode.BatchMustCreateNew)
        {
            return ExportPreparationResult.Failed(
                "Batch already exists, but mode requires creating a new batch."
            );
        }

        return ExportPreparationResult.Ok(startDate, endDate, resolvedBatchCode);
    }

    private static byte[] DecodeFileBytes(string input)
    {
        try
        {
            return Convert.FromBase64String(input);
        }
        catch
        {
            return System.Text.Encoding.UTF8.GetBytes(input);
        }
    }

    private static IReadOnlyList<string> ReadBasImportDocuments(
        byte[] bytes,
        out string? error
    )
    {
        error = null;
        if (bytes.Length < 2 || bytes[0] != 0x50 || bytes[1] != 0x4B)
        {
            return new[] { System.Text.Encoding.UTF8.GetString(bytes) };
        }

        const int maximumEntries = 64;
        const long maximumExpandedBytes = 100L * 1024 * 1024;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToList();
            if (entries.Count == 0)
            {
                error = "The BAS ZIP archive does not contain any report files.";
                return Array.Empty<string>();
            }

            if (entries.Count > maximumEntries)
            {
                error = $"The BAS ZIP archive contains more than {maximumEntries} report files.";
                return Array.Empty<string>();
            }

            var documents = new List<string>(entries.Count);
            long expandedBytes = 0;
            foreach (var entry in entries)
            {
                if (entry.Length > maximumExpandedBytes - expandedBytes)
                {
                    error = "The BAS ZIP archive expands beyond the supported size limit.";
                    return Array.Empty<string>();
                }

                using var entryStream = entry.Open();
                using var reader = new StreamReader(
                    entryStream,
                    System.Text.Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true
                );
                var document = reader.ReadToEnd();
                expandedBytes += System.Text.Encoding.UTF8.GetByteCount(document);
                if (expandedBytes > maximumExpandedBytes)
                {
                    error = "The BAS ZIP archive expands beyond the supported size limit.";
                    return Array.Empty<string>();
                }

                documents.Add(document);
            }

            return documents;
        }
        catch (InvalidDataException)
        {
            error = "The BAS upload is not a valid ZIP archive.";
            return Array.Empty<string>();
        }
    }

    private async Task<(DateTime startDate, DateTime endDate)> ResolveExportDateWindowAsync(
        PastelExportDto request
    )
    {
        if (request.StartDate != default && request.EndDate != default)
        {
            return (request.StartDate.Date, request.EndDate.Date);
        }

        if (!string.IsNullOrWhiteSpace(request.BatchDate))
        {
            if (DateTime.TryParse(request.BatchDate, out var parsed))
            {
                return (parsed.Date, parsed.Date);
            }

            if (int.TryParse(request.BatchDate, out var batchCode))
            {
                var batchByCode = await _context
                    .Batches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.batch_code == batchCode);
                if (batchByCode is not null)
                {
                    return (batchByCode.batch_date.Date, batchByCode.batch_date.Date);
                }
            }
        }

        var latestBatch = await _context
            .Batches.AsNoTracking()
            .OrderByDescending(b => b.batch_date)
            .ThenByDescending(b => b.batch_code)
            .FirstOrDefaultAsync();

        if (latestBatch is not null)
        {
            return (latestBatch.batch_date.Date, latestBatch.batch_date.Date);
        }

        var today = DateTime.Today;
        return (today, today);
    }

    private async Task<List<string>> BuildExportLinesAsync(
        DateTime startDate,
        DateTime endDate,
        int? departmentCode,
        bool includeCustomerColumn,
        bool reverseBatch
    )
    {
        // FinanceMain.aspx -> DownloadReport.aspx executes one of these
        // database-owned procedures. Their joins apply PastelCustomer and
        // PastelGL mappings, exclude suspense/GGMT rows, aggregate reversals,
        // and may create missing customer mappings as part of the legacy
        // transaction. A journal_detail projection cannot reproduce those
        // accounting semantics, so it is deliberately not used here.
        var procedureKey = includeCustomerColumn ? "pastel-csv-customer" : "pastel-csv";
        var batchDate = startDate.Date;
        var report = await _legacyFinanceReportExecutionService.TryExecuteAsync(
            new LegacyFinanceProcedureRequest(
                procedureKey,
                includeCustomerColumn
                    ? "Pastel CSV Interface with Customer"
                    : "Pastel CSV Interface",
                new Dictionary<string, object?> { ["@BatchDate"] = batchDate }
            ),
            HttpContext.RequestAborted
        );

        if (report is null)
        {
            throw new LegacyFinanceProcedureUnavailableException(
                includeCustomerColumn
                    ? "DEV_REP_ExportPastelCSVWithClientName"
                    : "DEV_REP_ExportPastelCSV"
            );
        }

        var expectedColumns = includeCustomerColumn
            ? new[] { "Trans Date", "Account", "AccountName", "Trans Code", "GL Contra Code", "Reference", "Description", "Amount Excl" }
            : new[] { "Trans Date", "Account", "Trans Code", "GL Contra Code", "Reference", "Description", "Amount Excl" };
        var rows = report.DataRows;
        var columns = rows.Count == 0
            ? expectedColumns
            : expectedColumns.Where(column => rows[0].ContainsKey(column))
                .Concat(rows[0].Keys.Where(column => !expectedColumns.Contains(column, StringComparer.OrdinalIgnoreCase)))
                .ToArray();

        var lines = new List<string> { string.Join(",", columns.Select(EscapeCsv)) };
        foreach (var row in rows)
        {
            var values = columns.Select(column =>
            {
                row.TryGetValue(column, out var value);
                if (reverseBatch && string.Equals(column, "Amount Excl", StringComparison.OrdinalIgnoreCase)
                    && value is not null && decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    value = -amount;
                }
                return EscapeCsv(value);
            });
            lines.Add(string.Join(",", values));
        }

        return lines;
    }

    private static string EscapeCsv(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private async Task<byte[]> BuildMultiSystemExportZipAsync(
        PastelExportDto request,
        bool includeCustomerColumn
    )
    {
        var systems = new[]
        {
            ExportFinancialSystem.Bas,
            ExportFinancialSystem.Sap,
            ExportFinancialSystem.Mpi,
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var system in systems)
            {
                var perSystem = new PastelExportDto
                {
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    DepartmentCode = request.DepartmentCode,
                    BatchDate = request.BatchDate,
                    FinancialSystemCode = (byte)system,
                    BatchMode = request.BatchMode,
                    SkipPosting = request.SkipPosting,
                    ReverseBatch = request.ReverseBatch,
                };

                var prepare = await PrepareExportContextAsync(perSystem);
                if (!prepare.Success)
                {
                    var errorName = $"ERROR_{system.ToString().ToUpperInvariant()}.txt";
                    var errorEntry = archive.CreateEntry(errorName);
                    await using var errorStream = new StreamWriter(errorEntry.Open());
                    await errorStream.WriteAsync(prepare.ErrorMessage ?? "Unknown error");
                    continue;
                }

                var lines = await BuildExportLinesAsync(
                    prepare.StartDate,
                    prepare.EndDate,
                    perSystem.DepartmentCode,
                    includeCustomerColumn,
                    reverseBatch: perSystem.ReverseBatch
                );

                if (!perSystem.SkipPosting)
                {
                    await MarkExportRowsPostedAsync(
                        prepare.StartDate,
                        perSystem.FinancialSystemCode,
                        perSystem.DepartmentCode
                    );
                }

                if (system == ExportFinancialSystem.Mpi)
                {
                    var mpiEntry = archive.CreateEntry(
                        $"MPI_INFO_batch{prepare.BatchCode}_{prepare.StartDate:yyyyMMdd}.txt"
                    );
                    await using var mpiWriter = new StreamWriter(mpiEntry.Open());
                    await mpiWriter.WriteAsync(
                        "MPI export has no file output. Posting workflow completed."
                    );
                    continue;
                }

                var deptSuffix = perSystem.DepartmentCode.HasValue
                    ? $"-{perSystem.DepartmentCode.Value}"
                    : string.Empty;
                var entryName =
                    $"{system.ToString().ToUpperInvariant()}_{prepare.StartDate:yyyyMMdd}_{prepare.EndDate:yyyyMMdd}_batch{prepare.BatchCode}{deptSuffix}.csv";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var entryWriter = new StreamWriter(entry.Open());
                foreach (var line in lines)
                {
                    await entryWriter.WriteLineAsync(line);
                }
            }
        }

        return memoryStream.ToArray();
    }

    private async Task MarkExportRowsPostedAsync(
        DateTime batchDate,
        byte financialSystemCode,
        int? departmentCode
    )
    {
        var sqlBatchDate = batchDate.Date;
        if (departmentCode.HasValue)
        {
            var dept = (short)departmentCode.Value;
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_UPD_DepartmentBatchToPosted @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}, @DepartmentCode={dept}"
            );
            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC DEV_UPD_BatchToPosted @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
        );
    }

    private async Task PrepareBatchJournalsAndMappingsAsync(
        int batchCode,
        DateTime batchDate,
        byte financialSystemCode
    )
    {
        var sqlBatchDate = batchDate.Date;
        try
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_INS_SegmentJournalDetailMapsForExport @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_UPD_VerifySegmentJournalDetailMapsForExport @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );

            var unpostedCount = await GetUnpostedJournalDetailCountAsync(
                batchDate,
                financialSystemCode
            );

            if (unpostedCount == 0)
            {
                return;
            }

            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_UPD_UnpostedJournalToJournalDetail @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_INS_JournalSummary @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_INS_RevenueJournals @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_INS_SegmentJournalDetailSummaryMapsForExport @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_UPD_VerifySegmentJournalDetailMapsForExport @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC DEV_INS_MFCodeMap @BatchDate={sqlBatchDate}, @FinancialSystem={financialSystemCode}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Legacy finance procedure sequence failed for batch {BatchCode} ({BatchDate}, FS {FinancialSystemCode}).",
                batchCode,
                batchDate,
                financialSystemCode
            );
            throw new InvalidOperationException(
                $"Legacy finance procedure sequence failed for batch {batchCode} on {batchDate:yyyy-MM-dd} (financial system {financialSystemCode}).",
                ex
            );
        }
    }

    private async Task<int> GetUnpostedJournalDetailCountAsync(
        DateTime batchDate,
        byte financialSystemCode
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DEV_SEL_UnpostedJournalDetailCount";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            var financialSystemParameter = command.CreateParameter();
            financialSystemParameter.ParameterName = "@FinancialSystem";
            financialSystemParameter.DbType = DbType.Byte;
            financialSystemParameter.Value = financialSystemCode;
            command.Parameters.Add(financialSystemParameter);

            var batchDateParameter = command.CreateParameter();
            batchDateParameter.ParameterName = "@BatchDate";
            batchDateParameter.DbType = DbType.DateTime;
            batchDateParameter.Value = batchDate.Date;
            command.Parameters.Add(batchDateParameter);

            var scalar = await command.ExecuteScalarAsync();
            if (scalar is null || scalar is DBNull)
            {
                return 0;
            }

            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int> CreateBatchViaLegacyProcAsync(
        DateTime batchDate,
        byte financialSystemCode
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DEV_INS_Batch";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 120;

            var batchDateParameter = command.CreateParameter();
            batchDateParameter.ParameterName = "@BatchDate";
            batchDateParameter.DbType = DbType.DateTime;
            batchDateParameter.Value = batchDate.Date;
            command.Parameters.Add(batchDateParameter);

            var financialSystemParameter = command.CreateParameter();
            financialSystemParameter.ParameterName = "@FinancialSystem";
            financialSystemParameter.DbType = DbType.Byte;
            financialSystemParameter.Value = financialSystemCode;
            command.Parameters.Add(financialSystemParameter);

            var scalar = await command.ExecuteScalarAsync();
            if (scalar is null || scalar is DBNull)
            {
                throw new InvalidOperationException("DEV_INS_Batch returned null batch code.");
            }

            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static readonly LegacyProcedureContract ParameterValueRead = new(
        "DEV_SEL_ParameterValue",
        ["@receivedParameterName"]
    );

    private static readonly LegacyProcedureContract ParameterValueWrite = new(
        "DEV_UPD_ParameterValue",
        ["@parameterName", "@parameterValue"]
    );

    private static readonly LegacyProcedureContract TriggerBatchJob = new(
        "ADM_TriggerBatchJob",
        ["@BatchDate", "@TriggerUser", "@JobName", "@StepId"]
    );

    private static readonly LegacyProcedureContract TriggerRollbackJob = new(
        "ADM_TriggerRollbackJob",
        ["@Location", "@User", "@JobName", "@StepId"]
    );

    private static readonly LegacyProcedureContract CheckScoaProcedure = new(
        "ADM_CheckSCOA_Version5",
        []
    );

    private static readonly LegacyProcedureContract CheckJobStatusProcedure = new(
        "ADM_CheckJobStatus",
        ["@JobName"]
    );

    private static readonly LegacyProcedureContract CheckRecordedLogsProcedure = new(
        "ADM_CheckRecordedLogs",
        ["@LogJob"]
    );

    private static readonly LegacyProcedureContract UpdateTariffParameterProcedure = new(
        "DEV_UPD_TariffParameter",
        [
            "@TariffParameterID",
            "@TariffParameterYear",
            "@AnnualInterestRatePercentage",
            "@AnnualPayments",
            "@PoolVehicleChargedDaysPerMonth",
            "@CostCategoryMultiple",
            "@AnnualRecoveredKilos",
            "@AverageFuelPrice",
            "@EffectiveDate",
            "@user_access_code",
            "@Approved",
        ],
        "fin"
    );

    private async Task<List<string>> GetUnavailableLegacyProceduresAsync(
        params LegacyProcedureContract[] procedures
    )
    {
        var unavailable = new List<string>();
        foreach (var procedure in procedures)
        {
            var availability = await GetLegacyProcedureAvailabilityAsync(procedure);
            if (availability != LegacyProcedureAvailability.Compatible)
                unavailable.Add(
                    availability == LegacyProcedureAvailability.Missing
                        ? $"{procedure.Name} is missing"
                        : $"{procedure.Name} has an incompatible parameter contract"
                );
        }

        return unavailable;
    }

    private async Task<LegacyProcedureAvailability> GetLegacyProcedureAvailabilityAsync(
        LegacyProcedureContract expected
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [parameterObject].[parameter_id], [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                WHERE [schemaObject].[name] = @schemaName
                  AND [procedureObject].[name] = @procedureName
                ORDER BY [parameterObject].[parameter_id]
                """;
            AddDbParameter(command, "@schemaName", DbType.String, expected.SchemaName);
            AddDbParameter(command, "@procedureName", DbType.String, expected.Name);

            var actual = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            var found = false;
            while (await reader.ReadAsync())
            {
                found = true;
                if (!reader.IsDBNull(0))
                    actual.Add(reader.GetString(1));
            }

            if (!found)
                return LegacyProcedureAvailability.Missing;

            return actual.SequenceEqual(expected.ParameterNames, StringComparer.OrdinalIgnoreCase)
                ? LegacyProcedureAvailability.Compatible
                : LegacyProcedureAvailability.Incompatible;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> ReadLegacyParameterAsync(string parameterName)
    {
        await EnsureLegacyProcedureCompatibleAsync(ParameterValueRead);
        var result = await ExecuteLegacyProcedureScalarAsync(
            ParameterValueRead,
            new LegacyProcedureParameter("@receivedParameterName", DbType.String, parameterName)
        );
        return result is null || result is DBNull
            ? null
            : Convert.ToString(result, CultureInfo.InvariantCulture)?.Trim();
    }

    private async Task WriteLegacyParameterAsync(string parameterName, string parameterValue)
    {
        await EnsureLegacyProcedureCompatibleAsync(ParameterValueWrite);
        await ExecuteLegacyProcedureAsync(
            ParameterValueWrite,
            new LegacyProcedureParameter("@parameterName", DbType.String, parameterName),
            new LegacyProcedureParameter("@parameterValue", DbType.String, parameterValue)
        );
    }

    private async Task EnsureLegacyProcedureCompatibleAsync(LegacyProcedureContract expected)
    {
        var availability = await GetLegacyProcedureAvailabilityAsync(expected);
        if (availability == LegacyProcedureAvailability.Compatible)
            return;

        throw new LegacyBatchCompatibilityException(
            availability == LegacyProcedureAvailability.Missing
                ? $"The required legacy procedure {expected.Name} is not deployed. No direct-DML fallback was run."
                : $"The deployed legacy procedure {expected.Name} does not match the archived parameter contract. No direct-DML fallback was run."
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review if the query string passed to 'string DbCommand.CommandText' accepts any user input",
        Justification = "Procedure names come only from fixed archived legacy procedure contracts."
    )]
    private async Task ExecuteLegacyProcedureAsync(
        LegacyProcedureContract procedure,
        params LegacyProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = $"{procedure.SchemaName}.{procedure.Name}";
            command.CommandTimeout = 0;
            AddDbParameters(command, parameters);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review if the query string passed to 'string DbCommand.CommandText' accepts any user input",
        Justification = "Procedure names come only from fixed archived legacy procedure contracts."
    )]
    private async Task<object?> ExecuteLegacyProcedureScalarAsync(
        LegacyProcedureContract procedure,
        params LegacyProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = $"{procedure.SchemaName}.{procedure.Name}";
            command.CommandTimeout = 0;
            AddDbParameters(command, parameters);
            return await command.ExecuteScalarAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    /// <summary>
    /// BatchInProgress.aspx.vb overlays ADM_CheckRecordedLogs(@LogJob="batch")
    /// and ADM_CheckJobStatus(@JobName=BatchJob/RollbackJob) onto the live
    /// BatchIsRunning flag. Missing procedures stay omitted; incompatible
    /// contracts are not replaced with leftover EF job history.
    /// </summary>
    private async Task OverlayArchivedBatchProgressAsync(BatchStatusDto dto)
    {
        try
        {
            var recordedLogsAvailability = await GetLegacyProcedureAvailabilityAsync(
                CheckRecordedLogsProcedure
            );
            if (recordedLogsAvailability == LegacyProcedureAvailability.Compatible)
            {
                var latestLog = await ExecuteLegacyProcedureScalarAsync(
                    CheckRecordedLogsProcedure,
                    new LegacyProcedureParameter("@LogJob", DbType.String, "batch")
                );
                dto.LatestLog = latestLog is null or DBNull
                    ? null
                    : Convert.ToString(latestLog, CultureInfo.InvariantCulture)?.Trim();
            }

            var jobStatusAvailability = await GetLegacyProcedureAvailabilityAsync(
                CheckJobStatusProcedure
            );
            if (jobStatusAvailability != LegacyProcedureAvailability.Compatible)
                return;

            var processStartDate = ToLegacyDateTime(await ReadLegacyParameterAsync("BatchStartDate"));
            var processEndDate = ToLegacyDateTime(await ReadLegacyParameterAsync("BatchEndDate"));
            var batchJob = await ReadLegacyParameterAsync("BatchJob") ?? "";
            var rollbackJob = await ReadLegacyParameterAsync("RollbackJob") ?? "";

            var jobRow = await ExecuteLegacyProcedureFirstRowAsync(
                CheckJobStatusProcedure,
                new LegacyProcedureParameter("@JobName", DbType.String, batchJob)
            );
            DateTime startDate;
            DateTime endDate;
            var hours = 0;
            if (jobRow is not null)
            {
                startDate = ToLegacyDateTime(ReadRowValue(jobRow, "startDate"));
                endDate = ToLegacyDateTime(ReadRowValue(jobRow, "endDate"));
                var timeTook = ToLegacyInteger(ReadRowValue(jobRow, "timetook"));
                if (timeTook != 0)
                    hours = timeTook;
            }
            else
            {
                startDate = processStartDate;
                endDate = processEndDate;
                hours = (int)(processStartDate - processEndDate).TotalHours;
            }

            if (hours < 0)
                hours = 4;

            dto.JobStartDate = ToOptionalDateTime(startDate);
            dto.JobEndDate = ToOptionalDateTime(endDate);
            dto.TypicalHours = hours;
            dto.DatabaseOperationsActive =
                processStartDate > processEndDate || endDate == DateTime.MinValue;

            var rollbackRow = await ExecuteLegacyProcedureFirstRowAsync(
                CheckJobStatusProcedure,
                new LegacyProcedureParameter("@JobName", DbType.String, rollbackJob)
            );
            if (rollbackRow is null)
                return;

            var rollbackStartDate = ToLegacyDateTime(ReadRowValue(rollbackRow, "startDate"));
            var rollbackEndDate = ToLegacyDateTime(ReadRowValue(rollbackRow, "endDate"));
            dto.RollbackStartDate = ToOptionalDateTime(rollbackStartDate);
            dto.RollbackEndDate = ToOptionalDateTime(rollbackEndDate);
            dto.RollbackOperationsActive =
                rollbackEndDate != DateTime.MinValue && rollbackStartDate > rollbackEndDate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Archived batch job-status overlay failed");
            dto.JobStatusError = ex.InnerException?.Message ?? ex.Message;
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review if the query string passed to 'string DbCommand.CommandText' accepts any user input",
        Justification = "Procedure names come only from fixed archived legacy procedure contracts."
    )]
    private async Task<Dictionary<string, object?>?> ExecuteLegacyProcedureFirstRowAsync(
        LegacyProcedureContract procedure,
        params LegacyProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = $"{procedure.SchemaName}.{procedure.Name}";
            command.CommandTimeout = 0;
            AddDbParameters(command, parameters);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[reader.GetName(index)] = reader.IsDBNull(index)
                    ? null
                    : reader.GetValue(index);
            }

            return values;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static object? ReadRowValue(
        IReadOnlyDictionary<string, object?> row,
        string column
    ) => row.TryGetValue(column, out var value) ? value : null;

    private static DateTime ToLegacyDateTime(object? value)
    {
        if (value is null or DBNull)
            return DateTime.MinValue;
        if (value is DateTime dateTime)
            return dateTime;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return DateTime.MinValue;
        if (
            DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var invariant
            )
        )
            return invariant;
        if (
            DateTime.TryParse(
                text,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AssumeLocal,
                out var current
            )
        )
            return current;
        return DateTime.MinValue;
    }

    private static DateTime? ToOptionalDateTime(DateTime value) =>
        value == DateTime.MinValue ? null : value;

    private static int ToLegacyInteger(object? value)
    {
        if (value is null or DBNull)
            return 0;
        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void AddDbParameters(
        DbCommand command,
        IEnumerable<LegacyProcedureParameter> parameters
    )
    {
        foreach (var parameter in parameters)
            AddDbParameter(command, parameter.Name, parameter.Type, parameter.Value);
    }

    private static void AddDbParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private ActionResult LegacyBatchProcedureUnavailable(IReadOnlyCollection<string> unavailable) =>
        Conflict(
            new
            {
                error = $"Legacy batch workflow cannot run: {string.Join("; ", unavailable)}. No direct-DML fallback was run.",
            }
        );

    private string GetRequiredLegacyUsername()
    {
        var username = User.FindFirst("legacy_username")?.Value?.Trim();
        if (!string.IsNullOrWhiteSpace(username))
            return username;

        throw new LegacyBatchCompatibilityException(
            "The authenticated session does not include the legacy username required by the batch procedure."
        );
    }

    private static bool IsLegacyTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private sealed record LegacyProcedureContract(
        string Name,
        string[] ParameterNames,
        string SchemaName = "dbo"
    );

    private sealed record LegacyProcedureParameter(string Name, DbType Type, object? Value);

    private sealed class LegacyBatchCompatibilityException(string message) : InvalidOperationException(message);

    private enum LegacyProcedureAvailability
    {
        Missing,
        Incompatible,
        Compatible,
    }

    private sealed record BasImportRow(
        string SegmentNumber,
        string SegmentName,
        int SegmentGroupCode,
        short DepartmentCode,
        short? SiteCode
    );

    private sealed record BasImportGroup(
        int SegmentGroupCode,
        string InstallationCode,
        IReadOnlyList<BasImportRow> Rows
    );

    private sealed record BasImportExecutionGroup(
        BasImportGroup Group,
        int ActionCode,
        int DepartmentCode
    );

    private static bool LooksLikeLegacyBasDocument(string value)
    {
        var firstLine = value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).FirstOrDefault();
        return firstLine is not null
            && Regex.IsMatch(firstLine, @"^\s*BAS\b", RegexOptions.IgnoreCase);
    }

    private static bool TryParseLegacyBasDocument(
        string value,
        int targetDepartmentCode,
        DateTime? requestedEndDate,
        out List<BasImportRow> rows,
        out string error
    )
    {
        rows = new List<BasImportRow>();
        error = "The BAS document could not be parsed.";
        if (targetDepartmentCode <= 0 || targetDepartmentCode > short.MaxValue)
        {
            error = "Select a valid target department before importing BAS codes.";
            return false;
        }

        using var reader = new StringReader(value);
        var firstLine = reader.ReadLine();
        if (firstLine is null || !Regex.IsMatch(firstLine, @"^\s*BAS\b", RegexOptions.IgnoreCase))
        {
            error = "Invalid BAS document supplied.";
            return false;
        }

        for (var index = 0; index < 13; index++)
        {
            if (reader.ReadLine() is null)
            {
                error = "The BAS document ended before its effective date.";
                return false;
            }
        }

        var effectiveLine = reader.ReadLine();
        var effectiveDateMatch = effectiveLine is null
            ? null
            : Regex.Match(effectiveLine, @"(?<date>\d{1,4}[/\\]\d{1,2}[/\\]\d{2,4})\s*$");
        if (
            effectiveDateMatch is null
            || !TryParseLegacyDate(effectiveDateMatch.Groups["date"].Value, out _)
        )
        {
            error = "The BAS document has an invalid effective date.";
            return false;
        }

        for (var index = 0; index < 16; index++)
        {
            if (reader.ReadLine() is null)
            {
                error = "The BAS document ended before its segment type.";
                return false;
            }
        }

        var segmentTypeLine = reader.ReadLine();
        var segmentTypeTokens = segmentTypeLine is null
            ? Array.Empty<string>()
            : Regex.Split(segmentTypeLine.Trim(), @"\s+");
        if (segmentTypeTokens.Length < 3)
        {
            error = "The BAS document does not identify a segment type.";
            return false;
        }

        var segmentGroupCode = segmentTypeTokens[2].Trim().ToUpperInvariant() switch
        {
            "FUND" => 1,
            "OBJECTIVE" => 2,
            "RESPONSIBILITY" => 3,
            "PROJECT" => 7,
            "ASSETS" => 56,
            "REGIONAL" => 62,
            "INFRASTRUCTURE" => 88,
            "ITEM" => 0,
            _ => -1,
        };
        if (segmentGroupCode == 0)
        {
            error = "ITEM BAS files are configured through the BAS interface and cannot be imported here.";
            return false;
        }

        if (segmentGroupCode < 0)
        {
            error = $"The BAS document segment type '{segmentTypeTokens[2]}' is not supported.";
            return false;
        }

        for (var index = 0; index < 5; index++)
        {
            if (reader.ReadLine() is null)
            {
                error = "The BAS document ended before its segment rows.";
                return false;
            }
        }

        var endDate = requestedEndDate?.Date ?? GetDefaultBasEndDate(DateTime.Today);
        var rowPattern = new Regex(
            @"^\s*\d+\s+(?<number>\d{4,8})\s+(?<name>.*?)\s+(?<date>\d{1,4}[/\\]\d{1,2}[/\\]\d{2,4})\s+(?:IN)?ACTIVE\s+(?<posted>[YN])\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
        while (reader.ReadLine() is { } line)
        {
            var match = rowPattern.Match(line);
            if (!match.Success || !string.Equals(match.Groups["posted"].Value, "Y", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (
                !TryParseLegacyDate(match.Groups["date"].Value, out var segmentDate)
                || segmentDate.Date > endDate
            )
            {
                error = "A BAS segment effective date is later than the selected end date.";
                return false;
            }

            rows.Add(
                new BasImportRow(
                    match.Groups["number"].Value,
                    match.Groups["name"].Value.Trim(),
                    segmentGroupCode,
                    checked((short)targetDepartmentCode),
                    null
                )
            );
        }

        if (rows.Count == 0)
        {
            error = "No active BAS segment rows were found in the document.";
            return false;
        }

        return true;
    }

    private static bool TryParseLegacyDate(string value, out DateTime date)
    {
        var normalized = value.Replace('\\', '/');
        return DateTime.TryParseExact(
            normalized,
            new[] { "d/M/yyyy", "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yy", "dd/MM/yy" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date
        );
    }

    private static DateTime GetDefaultBasEndDate(DateTime startDate)
    {
        var fiscalEndYear = startDate.Month <= 3 ? startDate.Year : startDate.Year + 1;
        return new DateTime(fiscalEndYear, 3, 31);
    }

    private static string DescribeBasValidation(LegacyBasImportValidation validation) =>
        validation.ActionCode switch
        {
            3 => "The BAS installation code is not configured for the selected department. Ask Finance or Helpdesk to configure it before importing.",
            4 => $"The BAS document belongs to {validation.DocumentDepartmentName ?? "another department"}. Legacy FIS requires explicit confirmation before importing it.",
            5 => "The BAS document's department is not installed in FIS. Legacy FIS requires explicit confirmation before configuring the installation link.",
            _ => $"The legacy BAS validation returned action code {validation.ActionCode}; no rows were written.",
        };

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private async Task<DataSet> BuildCloseKiloGapsDataSetAsync(
        short financialYear,
        int currentUserId
    )
    {
        var allKilos = await LoadAllVehicleKilosAsync();
        var table = CreateKiloGapsTableSchema();
        var maxGapNumber = await GetMaxGapRecordNumberAsync();
        var closeGapNumber = maxGapNumber + 1;

        DataRow? prev = null;
        foreach (DataRow current in allKilos.Rows)
        {
            if (
                prev is not null
                && SafeInt(prev, "vmf_code") == SafeInt(current, "vmf_code")
                && !string.Equals(
                    SafeString(prev, "TA_REK"),
                    SafeString(current, "TA_REK"),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var prevEnd = SafeInt(prev, "end_odo");
                var currStart = SafeInt(current, "start_odo");
                var gapSize = currStart - prevEnd;
                if (gapSize > 0)
                {
                    var prevSite = SafeString(prev, "Site");
                    var currSite = SafeString(current, "Site");
                    var prevContractType = SafeString(prev, "contract_type");
                    var currContractType = SafeString(current, "contract_type");
                    var prevFinancialYear = SafeShort(prev, "FinancialYear");
                    var currFinancialYear = SafeShort(current, "FinancialYear");

                    if (
                        string.Equals(prevSite, currSite, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(
                            prevContractType,
                            "VIP",
                            StringComparison.OrdinalIgnoreCase
                        )
                        && !string.Equals(
                            currContractType,
                            "VIP",
                            StringComparison.OrdinalIgnoreCase
                        )
                        && prevFinancialYear >= financialYear - 1
                        && currFinancialYear == financialYear
                    )
                    {
                        var row = table.NewRow();
                        FillGapRow(row, prev, current, gapSize, closeGapNumber, currentUserId);
                        table.Rows.Add(row);
                        closeGapNumber++;
                    }
                }
            }

            prev = current;
        }

        var result = new DataSet("NewDataSet");
        result.Tables.Add(table);
        return result;
    }

    private async Task<DataSet> BuildKiloGapsReportDataSetAsync()
    {
        var allKilos = await LoadAllVehicleKilosAsync();
        var table = CreateKiloGapsTableSchema();
        var gapNumber = await GetMaxGapRecordNumberAsync() + 1;
        DataRow? previous = null;

        foreach (DataRow current in allKilos.Rows)
        {
            var gapSize = 0;
            if (
                previous is not null
                && SafeInt(previous, "vmf_code") == SafeInt(current, "vmf_code")
                && !string.Equals(
                    SafeString(previous, "TA_REK"),
                    SafeString(current, "TA_REK"),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                gapSize = SafeInt(current, "start_odo") - SafeInt(previous, "end_odo");
                if (gapSize > 0)
                {
                    var gap = table.NewRow();
                    // The view/report path in KilosDataController has no user
                    // context. Keep its generated values distinct from the
                    // closing workflow, which supplies the capturing user.
                    FillGapRow(gap, previous, current, gapSize, gapNumber, 0);
                    table.Rows.Add(gap);
                    gapNumber++;
                }
            }

            // This is the legacy controller's overlapping-reading rule. It
            // makes subsequent gaps compare against the furthest valid end
            // odometer instead of blindly replacing the prior record.
            if (previous is null)
            {
                previous = current;
            }
            else if (gapSize < 0)
            {
                if (SafeInt(previous, "end_odo") < SafeInt(current, "end_odo"))
                {
                    previous = current;
                }
            }
            else
            {
                previous = current;
            }
        }

        var result = new DataSet("NewDataSet");
        result.Tables.Add(table);
        return result;
    }

    private static void FillGapRow(
        DataRow row,
        DataRow prev,
        DataRow next,
        int gapSize,
        int gapNumber,
        int userId
    )
    {
        row["Prev_vmf_code"] = SafeInt(prev, "vmf_code");
        row["Prev_registration_number"] = SafeString(prev, "registration_number");
        row["Prev_fleet_number"] = SafeString(prev, "fleet_number");
        row["Prev_start_odo"] = SafeDouble(prev, "start_odo");
        row["Prev_end_odo"] = SafeDouble(prev, "end_odo");
        row["Prev_contract_code"] = SafeDouble(prev, "contract_code");
        row["Prev_contract_type"] = SafeString(prev, "contract_type");
        row["Prev_contract_start_odo"] = SafeInt(prev, "contract_start_odo");
        row["Prev_Dept_ID"] = SafeDouble(prev, "Dept_ID");
        row["Prev_Dept_Code"] = SafeString(prev, "Dept_Code");
        row["Prev_site_code"] = SafeDouble(prev, "site_code");
        row["Prev_Site"] = SafeString(prev, "Site");
        row["Prev_TA_REK"] = SafeString(prev, "TA_REK");
        row["Prev_TransactionDate"] = SafeDate(prev, "TransactionDate");
        row["Prev_billing_month"] = SafeString(prev, "billing_month");
        row["Prev_trx_month"] = SafeString(prev, "trx_month");
        row["Prev_Post_Date"] = SafeDate(prev, "Post_Date");
        row["Prev_Bill_Date"] = SafeDate(prev, "Bill_Date");
        row["Prev_Source_Date"] = SafeDate(prev, "Source_Date");
        row["Prev_FinancialYear"] = SafeString(prev, "FinancialYear");
        row["Prev_Tariff"] = SafeDecimal(prev, "Tariff");
        row["Prev_Type"] = SafeString(prev, "Type");

        row["Gap_size"] = Convert.ToDouble(gapSize, CultureInfo.InvariantCulture);
        row["GapSize_Amount"] = Math.Round(gapSize * SafeDecimal(next, "Tariff"), 2);
        row["Gap_RekNumber"] =
            $"GAP{gapNumber.ToString(CultureInfo.InvariantCulture).PadLeft(7, '0')}";
        row["UserID_ToCloseGap"] = Convert.ToDecimal(userId, CultureInfo.InvariantCulture);

        row["Next_vmf_code"] = SafeInt(next, "vmf_code");
        row["Next_registration_number"] = SafeString(next, "registration_number");
        row["Next_fleet_number"] = SafeString(next, "fleet_number");
        row["Next_start_odo"] = SafeDouble(next, "start_odo");
        row["Next_end_odo"] = SafeDouble(next, "end_odo");
        row["Next_contract_code"] = SafeDouble(next, "contract_code");
        row["Next_contract_type"] = SafeString(next, "contract_type");
        row["Next_contract_start_odo"] = SafeInt(next, "contract_start_odo");
        row["Next_Dept_ID"] = SafeDouble(next, "Dept_ID");
        row["Next_Dept_Code"] = SafeString(next, "Dept_Code");
        row["Next_site_code"] = SafeDouble(next, "site_code");
        row["Next_Site"] = SafeString(next, "Site");
        row["Next_TA_REK"] = SafeString(next, "TA_REK");
        row["Next_TransactionDate"] = SafeDate(next, "TransactionDate");
        row["Next_billing_month"] = SafeString(next, "billing_month");
        row["Next_trx_month"] = SafeString(next, "trx_month");
        row["Next_Post_Date"] = SafeDate(next, "Post_Date");
        row["Next_Bill_Date"] = SafeDate(next, "Bill_Date");
        row["Next_Source_Date"] = SafeDate(next, "Source_Date");
        row["Next_FinancialYear"] = SafeString(next, "FinancialYear");
        row["Next_Tariff"] = SafeDecimal(next, "Tariff");
        row["Next_Type"] = SafeString(next, "Type");
    }

    private async Task<DataTable> LoadAllVehicleKilosAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DEV_REP_AllVehicleKilos";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;
            var startDateParameter = command.CreateParameter();
            startDateParameter.ParameterName = "@StartDate";
            startDateParameter.DbType = DbType.DateTime;
            startDateParameter.Value = new DateTime(1900, 1, 1);
            command.Parameters.Add(startDateParameter);

            await using var reader = await command.ExecuteReaderAsync();
            var table = new DataTable("AllKilosTable");
            table.Load(reader);
            return table;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int> ExecuteCloseKiloGapsProcAsync(string xmlDoc)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DEV_INS_CloseKiloGapsFromXML";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 180;
            var xmlParameter = command.CreateParameter();
            xmlParameter.ParameterName = "@XMLDoc";
            xmlParameter.DbType = DbType.String;
            xmlParameter.Value = xmlDoc;
            command.Parameters.Add(xmlParameter);

            return await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int> GetMaxGapRecordNumberAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "DEV_SEL_LogsheetMaxGapRekNumber";
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 60;
            var scalar = await command.ExecuteScalarAsync();
            if (scalar is null || scalar is DBNull)
            {
                return 0;
            }

            return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static DataTable CreateKiloGapsTableSchema()
    {
        var table = new DataTable("KiloGapsTable");
        table.Columns.Add("Prev_vmf_code", typeof(int));
        table.Columns.Add("Prev_registration_number", typeof(string));
        table.Columns.Add("Prev_fleet_number", typeof(string));
        table.Columns.Add("Prev_start_odo", typeof(double));
        table.Columns.Add("Prev_end_odo", typeof(double));
        table.Columns.Add("Prev_contract_code", typeof(double));
        table.Columns.Add("Prev_contract_type", typeof(string));
        table.Columns.Add("Prev_contract_start_odo", typeof(int));
        table.Columns.Add("Prev_Dept_ID", typeof(double));
        table.Columns.Add("Prev_Dept_Code", typeof(string));
        table.Columns.Add("Prev_site_code", typeof(double));
        table.Columns.Add("Prev_Site", typeof(string));
        table.Columns.Add("Prev_TA_REK", typeof(string));
        table.Columns.Add("Prev_TransactionDate", typeof(DateTime));
        table.Columns.Add("Prev_billing_month", typeof(string));
        table.Columns.Add("Prev_trx_month", typeof(string));
        table.Columns.Add("Prev_Post_Date", typeof(DateTime));
        table.Columns.Add("Prev_Bill_Date", typeof(DateTime));
        table.Columns.Add("Prev_Source_Date", typeof(DateTime));
        table.Columns.Add("Prev_FinancialYear", typeof(string));
        table.Columns.Add("Prev_Tariff", typeof(decimal));
        table.Columns.Add("Prev_Type", typeof(string));
        table.Columns.Add("Gap_size", typeof(double));
        table.Columns.Add("GapSize_Amount", typeof(decimal));
        table.Columns.Add("Gap_RekNumber", typeof(string));
        table.Columns.Add("UserID_ToCloseGap", typeof(decimal));
        table.Columns.Add("Next_vmf_code", typeof(int));
        table.Columns.Add("Next_registration_number", typeof(string));
        table.Columns.Add("Next_fleet_number", typeof(string));
        table.Columns.Add("Next_start_odo", typeof(double));
        table.Columns.Add("Next_end_odo", typeof(double));
        table.Columns.Add("Next_contract_code", typeof(double));
        table.Columns.Add("Next_contract_type", typeof(string));
        table.Columns.Add("Next_contract_start_odo", typeof(int));
        table.Columns.Add("Next_Dept_ID", typeof(double));
        table.Columns.Add("Next_Dept_Code", typeof(string));
        table.Columns.Add("Next_site_code", typeof(double));
        table.Columns.Add("Next_Site", typeof(string));
        table.Columns.Add("Next_TA_REK", typeof(string));
        table.Columns.Add("Next_TransactionDate", typeof(DateTime));
        table.Columns.Add("Next_billing_month", typeof(string));
        table.Columns.Add("Next_trx_month", typeof(string));
        table.Columns.Add("Next_Post_Date", typeof(DateTime));
        table.Columns.Add("Next_Bill_Date", typeof(DateTime));
        table.Columns.Add("Next_Source_Date", typeof(DateTime));
        table.Columns.Add("Next_FinancialYear", typeof(string));
        table.Columns.Add("Next_Tariff", typeof(decimal));
        table.Columns.Add("Next_Type", typeof(string));
        return table;
    }

    private static string SafeString(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToString(row[column], CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    private static int SafeInt(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToInt32(row[column], CultureInfo.InvariantCulture)
            : 0;

    private static short SafeShort(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToInt16(row[column], CultureInfo.InvariantCulture)
            : (short)0;

    private static double SafeDouble(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToDouble(row[column], CultureInfo.InvariantCulture)
            : 0d;

    private static decimal SafeDecimal(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToDecimal(row[column], CultureInfo.InvariantCulture)
            : 0m;

    private static DateTime SafeDate(DataRow row, string column) =>
        row.Table.Columns.Contains(column) && row[column] is not DBNull
            ? Convert.ToDateTime(row[column], CultureInfo.InvariantCulture)
            : DateTime.MinValue;

    #endregion
}

#region DTOs
public class BatchStatusDto
{
    public int BatchCode { get; set; }
    public string Status { get; set; } = "";
    public DateTime? BatchDate { get; set; }
    public bool IsActive { get; set; }
    public int TotalTransactions { get; set; }
    public int ProcessedTransactions { get; set; }
    public string? LatestLog { get; set; }
    public DateTime? JobStartDate { get; set; }
    public DateTime? JobEndDate { get; set; }
    public int? TypicalHours { get; set; }
    public DateTime? RollbackStartDate { get; set; }
    public DateTime? RollbackEndDate { get; set; }
    public bool DatabaseOperationsActive { get; set; }
    public bool RollbackOperationsActive { get; set; }
    public string? JobStatusError { get; set; }
}

public class StartBatchDto
{
    public DateTime BatchDate { get; set; }
    public byte FinancialSystemCode { get; set; } = 1;
    public ExportBatchMode? BatchMode { get; set; }
}

public class BatchStartResultDto
{
    public int BatchCode { get; set; }
    public DateTime BatchDate { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class ScoaCheckResultDto
{
    public bool IsCompliant { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int TotalChecked { get; set; }
    public string? Message { get; set; }
}

public class BasImportDto
{
    [Required]
    public string FileData { get; set; } = "";
    public int? DepartmentCode { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    /// <summary>
    /// The action code returned by DEV_SEL_ValidateBasImport when the legacy
    /// workflow requires an explicit Continue confirmation (4 or 5). The
    /// server re-runs validation and never trusts this value on its own.
    /// </summary>
    public int? ConfirmationActionCode { get; set; }
}

public class BasImportResultDto
{
    public bool Success { get; set; }
    public int RecordsImported { get; set; }
    public int RecordsInserted { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsDeleted { get; set; }
    public bool RequiresConfirmation { get; set; }
    public int? ActionCode { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Message { get; set; } = "";
}

public class BasSegmentDto
{
    public int SegmentCode { get; set; }
    public string SegmentNumber { get; set; } = "";
    public string SegmentType { get; set; } = "";
    public string SegmentValue { get; set; } = "";
    public int? DepartmentCode { get; set; }
    public bool IsActive { get; set; }
}

public class ActivateSegmentsDto
{
    [Required]
    public List<int> SegmentCodes { get; set; } = new();
    public int? DepartmentCode { get; set; }
}

public class InvalidJournalDto
{
    public long TransactionId { get; set; }
    public Guid JournalDetailCode { get; set; }
    public string JournalNumber { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? DepartmentCode { get; set; }
    public string DocumentNumber { get; set; } = "";
    public string JournalType { get; set; } = "";
    public string ResponsibilityNumber { get; set; } = "";
    public string ObjectiveNumber { get; set; } = "";
    public string JournalStart { get; set; } = "";
    public string JournalEnd { get; set; } = "";
    public string SiteName { get; set; } = "";
}

public class UninvoicedJournalDto
{
    public Guid JournalDetailCode { get; set; }
    public string JournalNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string VmfCode { get; set; } = "";
    public int JournalDetailTypeCode { get; set; }
    public int SiteCode { get; set; }
    public int DepartmentCode { get; set; }
    public string JournalMonth { get; set; } = "";
    public string JournalType { get; set; } = "";
    public string SiteName { get; set; } = "";
}

public class FixInvalidBasJournalDto
{
    [Range(1, long.MaxValue)]
    public long TransactionId { get; set; }

    [Range(1, short.MaxValue)]
    public short DepartmentCode { get; set; }

    [Required]
    [StringLength(8)]
    public string Responsibility { get; set; } = "";

    [Required]
    [StringLength(8)]
    public string Objective { get; set; } = "";
}

public class AssignFundCodeDto
{
    [Required]
    [StringLength(8)]
    public string FundNumber { get; set; } = "";

    [Range(1, short.MaxValue)]
    public short DepartmentCode { get; set; }

    [Required]
    [StringLength(50)]
    public string VmfCode { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int JournalDetailTypeCode { get; set; }

    [Range(1, short.MaxValue)]
    public short SiteCode { get; set; }

    [Required]
    [StringLength(6)]
    public string JournalMonth { get; set; } = "";

    public string? JournalDetailCode { get; set; }
}

public class FinanceDepartmentDto
{
    public int DepartmentCode { get; set; }
    public string DepartmentName { get; set; } = "";
}

public class FinancialYearDto
{
    public short Code { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class SegmentTypeDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class PastelExportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? DepartmentCode { get; set; }
    public string? BatchDate { get; set; }
    public byte FinancialSystemCode { get; set; } = 1;
    public ExportBatchMode? BatchMode { get; set; }
    public bool SkipPosting { get; set; }
    public bool ReverseBatch { get; set; }
}

public class PastelCustomerExportDto
{
    public int? DepartmentCode { get; set; }
    public string? BatchDate { get; set; }
    public byte FinancialSystemCode { get; set; } = 1;
    public ExportBatchMode? BatchMode { get; set; }
    public bool SkipPosting { get; set; }
    public bool ReverseBatch { get; set; }
}

public class ReversalTreeDto
{
    public string JournalNumber { get; set; } = "";
    public List<ReversalNodeDto> Reversals { get; set; } = new();
}

public class ReversalNodeDto
{
    public string JournalNumber { get; set; } = "";
    public DateTime ReversalDate { get; set; }
    public decimal Amount { get; set; }
}

public class CloseKiloGapsRequest
{
    public string FinancialYear { get; set; } = "";
}

public class StandardBankImportDto
{
    [Required]
    public string FileContent { get; set; } = "";
}

public class ImportResultDto
{
    public bool Success { get; set; }
    public int RecordsSubmitted { get; set; }
    public int RecordsImported { get; set; }
    public int RecordsFailed { get; set; }
    public string Message { get; set; } = "";
    public List<string> Errors { get; set; } = new();
}

public class TariffParametersDto
{
    public int Year { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<TariffParameterItemDto> Parameters { get; set; } = new();
    public List<TariffOverheadRowDto> FixedTariffs { get; set; } = new();
    public List<TariffOverheadRowDto> KiloTariffs { get; set; } = new();
    public List<MaintenanceValueRowDto> MaintenanceValues { get; set; } = new();
}

public class TariffParameterItemDto
{
    public string ParameterName { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
}

public class TariffOverheadRowDto
{
    public int OverheadId { get; set; }
    public string OverheadDescription { get; set; } = "";
    public decimal? PreviousAmount { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class MaintenanceValueRowDto
{
    public short ClassCode { get; set; }
    public string ClassDescription { get; set; } = "";
    public string? ClassNumber { get; set; }
    public int? AssignedCount { get; set; }
    public short? PreviousMonthsAge { get; set; }
    public int? PreviousKilometerAge { get; set; }
    public decimal? PreviousRandPerKilometer { get; set; }
    public short MonthsAge { get; set; }
    public int KilometerAge { get; set; }
    public decimal Amount { get; set; }
    public decimal RandPerKilometer { get; set; }
}

public class ApproveTariffDto
{
    public string? ApprovalNotes { get; set; }
    public DateTime? EffectiveDate { get; set; }
}

public class RejectTariffDto
{
    public string? RejectionNotes { get; set; }
}

public enum ExportFinancialSystem : byte
{
    All = 0,
    Bas = 1,
    Sap = 2,
    Mpi = 3,
}

public enum ExportBatchMode
{
    BatchAppendOrCreate = 0,
    BatchMustCreateNew = 1,
    BatchMustExist = 2,
    ExportNewSerialNumber = 3,
}

public sealed class ExportPreparationResult
{
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }
    public DateTime StartDate { get; private init; }
    public DateTime EndDate { get; private init; }
    public int BatchCode { get; private init; }

    public static ExportPreparationResult Ok(DateTime start, DateTime end, int batchCode) =>
        new()
        {
            Success = true,
            StartDate = start.Date,
            EndDate = end.Date,
            BatchCode = batchCode,
        };

    public static ExportPreparationResult Failed(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public sealed class ExportTaskState
{
    public Guid TaskId { get; set; }
    public string Status { get; set; } = "Queued";
    public int ProgressPercent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public byte[]? FileContent { get; set; }
    public string? FileContentType { get; set; }
    public string? FileName { get; set; }
    public PastelExportDto? Request { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; } = new();
}
#endregion

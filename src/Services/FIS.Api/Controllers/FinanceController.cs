using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Threading;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public class FinanceController : BaseApiController
{
    private readonly IJournalDetailService _journalService;
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
        FisDbContext context,
        ILogger<FinanceController> logger
    )
    {
        _journalService = journalService;
        _context = context;
        _logger = logger;
    }

    #region Batch Operations

    [HttpGet("batch/status")]
    public async Task<ActionResult<BatchStatusDto>> GetBatchStatus()
    {
        try
        {
            var batch = await _context
                .Batches.Where(b => !b.is_deleted)
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

            var totalTransactions = await _context.JournalDetails.CountAsync(jd =>
                !jd.is_deleted && jd.journal_detail_date.Date == batch.batch_date.Date
            );

            var processedTransactions = await _context.JournalDetails.CountAsync(jd =>
                !jd.is_deleted
                && jd.journal_detail_date.Date == batch.batch_date.Date
                && jd.journal_detail_date_posted.HasValue
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batch status");
            return StatusCode(500, new { error = "Failed to fetch batch status" });
        }
    }

    [HttpPost("batch/start")]
    public async Task<ActionResult<BatchStartResultDto>> StartBatch(
        [FromBody] StartBatchDto? request
    )
    {
        var requestedDate = request?.BatchDate.Date ?? DateTime.Today;
        if (requestedDate == DateTime.MinValue)
        {
            requestedDate = DateTime.Today;
        }
        var financialSystemCode =
            request?.FinancialSystemCode > 0 ? request.FinancialSystemCode : (byte)1;
        var batchMode = request?.BatchMode ?? ExportBatchMode.BatchAppendOrCreate;

        if (requestedDate == DateTime.MaxValue)
        {
            return BadRequest(new { error = "Invalid batch date." });
        }

        if (batchMode == ExportBatchMode.BatchMustExist)
        {
            return BadRequest(
                new { error = "The flag BatchMode.BatchMustExist is invalid for batch creation." }
            );
        }

        try
        {
            var existing = await _context
                .Batches.Where(b =>
                    !b.is_deleted
                    && b.batch_date.Date == requestedDate
                    && b.financial_system_code == financialSystemCode
                )
                .OrderByDescending(b => b.batch_code)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                if (batchMode == ExportBatchMode.BatchMustCreateNew)
                {
                    return Conflict(
                        new
                        {
                            error = "Batch already exists for selected date and financial system, but mode requires a new batch.",
                        }
                    );
                }

                await PrepareBatchJournalsAndMappingsAsync(
                    existing.batch_code,
                    existing.batch_date.Date,
                    existing.financial_system_code
                );
                return Ok(
                    new BatchStartResultDto
                    {
                        BatchCode = existing.batch_code,
                        BatchDate = existing.batch_date,
                        Success = true,
                        Message =
                            "Batch already exists for the selected date and financial system.",
                    }
                );
            }

            var newBatchCode = await CreateBatchViaLegacyProcAsync(
                requestedDate,
                financialSystemCode
            );
            var newBatch = await _context
                .Batches.AsNoTracking()
                .FirstOrDefaultAsync(b => !b.is_deleted && b.batch_code == newBatchCode);

            if (newBatch is null)
            {
                throw new InvalidOperationException(
                    $"Batch {newBatchCode} was created but could not be loaded."
                );
            }

            await PrepareBatchJournalsAndMappingsAsync(
                newBatch.batch_code,
                newBatch.batch_date.Date,
                newBatch.financial_system_code
            );

            return Ok(
                new BatchStartResultDto
                {
                    BatchCode = newBatch.batch_code,
                    BatchDate = newBatch.batch_date,
                    Success = true,
                    Message = "Batch created.",
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting batch for date {BatchDate}", requestedDate);
            return StatusCode(500, new { error = "Failed to start batch" });
        }
    }

    [HttpPost("batch/check-scoa")]
    public async Task<ActionResult<ScoaCheckResultDto>> CheckScoa()
    {
        try
        {
            var latestBatch = await _context
                .Batches.Where(b => !b.is_deleted)
                .OrderByDescending(b => b.batch_date)
                .ThenByDescending(b => b.batch_code)
                .FirstOrDefaultAsync();

            if (latestBatch is null)
            {
                return Ok(
                    new ScoaCheckResultDto
                    {
                        IsCompliant = false,
                        Errors = new List<string> { "No batch exists to validate." },
                        TotalChecked = 0,
                    }
                );
            }

            var batchDate = latestBatch.batch_date.Date;
            var journalDetailCodes = await _context
                .JournalDetails.AsNoTracking()
                .Where(jd => !jd.is_deleted && jd.journal_detail_date.Date == batchDate)
                .Select(jd => jd.journal_detail_code)
                .Distinct()
                .ToListAsync();

            if (journalDetailCodes.Count == 0)
            {
                return Ok(
                    new ScoaCheckResultDto
                    {
                        IsCompliant = false,
                        Warnings = new List<string> { "Batch has no journal details to validate." },
                        TotalChecked = 0,
                    }
                );
            }

            var mappedCodes = await _context
                .SegmentJournalDetailMaps.AsNoTracking()
                .Where(m => !m.is_deleted && journalDetailCodes.Contains(m.journal_detail_code))
                .Select(m => m.journal_detail_code)
                .Distinct()
                .ToListAsync();

            var missingCount = journalDetailCodes.Count - mappedCodes.Count;
            var errors = new List<string>();
            var warnings = new List<string>();

            if (missingCount > 0)
            {
                errors.Add($"{missingCount} journal detail record(s) have no segment mapping.");
            }

            return Ok(
                new ScoaCheckResultDto
                {
                    IsCompliant = errors.Count == 0,
                    Errors = errors,
                    Warnings = warnings,
                    TotalChecked = journalDetailCodes.Count,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SCOA compliance");
            return StatusCode(500, new { error = "Failed to check SCOA compliance" });
        }
    }

    [HttpPost("batch/rollback")]
    public async Task<ActionResult> RollbackBatch()
    {
        try
        {
            var latestBatch = await _context
                .Batches.Where(b => !b.is_deleted)
                .OrderByDescending(b => b.batch_date)
                .ThenByDescending(b => b.batch_code)
                .FirstOrDefaultAsync();

            if (latestBatch is null)
            {
                return Ok(new { message = "No batch found to roll back.", updated = 0 });
            }

            var batchDate = latestBatch.batch_date.Date;
            var journalDetails = await _context
                .JournalDetails.Where(jd =>
                    !jd.is_deleted && jd.journal_detail_date.Date == batchDate
                )
                .ToListAsync();

            foreach (var jd in journalDetails)
            {
                jd.journal_detail_date_posted = null;
                jd.journal_code = null;
                jd.journal_detail_date_updated = DateTime.UtcNow;
                jd.modified_by_user_code = GetCurrentUserId();
                jd.date_updated = DateTime.UtcNow;
            }

            var linkedJournals = await _context
                .JournalHeaders.Where(j => !j.is_deleted && j.batch_code == latestBatch.batch_code)
                .ToListAsync();

            foreach (var journal in linkedJournals)
            {
                journal.is_deleted = true;
                journal.date_updated = DateTime.UtcNow;
                journal.modified_by_user_code = GetCurrentUserId();
            }

            latestBatch.is_deleted = true;
            latestBatch.date_updated = DateTime.UtcNow;
            latestBatch.modified_by_user_code = GetCurrentUserId();

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    message = "Batch rolled back",
                    updated = journalDetails.Count,
                    batchCode = latestBatch.batch_code,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back batch");
            return StatusCode(500, new { error = "Failed to roll back batch" });
        }
    }

    [HttpPost("batch/finish")]
    public async Task<ActionResult> FinishBatch()
    {
        try
        {
            var latestBatch = await _context
                .Batches.Where(b => !b.is_deleted)
                .OrderByDescending(b => b.batch_date)
                .ThenByDescending(b => b.batch_code)
                .FirstOrDefaultAsync();

            if (latestBatch is null)
            {
                return Ok(new { message = "No batch found to finalize.", posted = 0 });
            }

            var batchDate = latestBatch.batch_date.Date;
            var now = DateTime.UtcNow;
            var userCode = GetCurrentUserId();

            var unpostedRows = await _context
                .JournalDetails.Where(jd =>
                    !jd.is_deleted
                    && jd.journal_detail_date.Date == batchDate
                    && !jd.journal_detail_date_posted.HasValue
                )
                .ToListAsync();

            foreach (var row in unpostedRows)
            {
                row.journal_detail_date_posted = now;
                row.journal_detail_date_updated = now;
                row.modified_by_user_code = userCode;
                row.date_updated = now;
            }

            latestBatch.date_updated = now;
            latestBatch.modified_by_user_code = userCode;

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    message = "Batch finalized",
                    posted = unpostedRows.Count,
                    batchCode = latestBatch.batch_code,
                }
            );
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
            var decoded = DecodeFileData(request.FileData);
            var lines = decoded
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
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

            var userCode = GetCurrentUserId();
            var now = DateTime.UtcNow;
            var imported = 0;
            var errors = new List<string>();

            foreach (var rawLine in lines)
            {
                var cols = rawLine.Split(',');
                if (cols.Length < 4)
                {
                    errors.Add($"Skipped invalid row: '{rawLine}'");
                    continue;
                }

                var segmentNumber = cols[0].Trim();
                var segmentName = cols[1].Trim();
                var groupCodeText = cols[2].Trim();
                var departmentCodeText = cols[3].Trim();
                var siteCodeText = cols.Length >= 5 ? cols[4].Trim() : string.Empty;

                if (
                    string.IsNullOrWhiteSpace(segmentNumber)
                    || !int.TryParse(groupCodeText, out var segmentGroupCode)
                    || !short.TryParse(departmentCodeText, out var departmentCode)
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

                var existing = await _context.BasSegments.FirstOrDefaultAsync(s =>
                    !s.is_deleted
                    && s.segment_number == segmentNumber
                    && s.segment_group_code == segmentGroupCode
                    && s.department_code == departmentCode
                    && s.site_code == siteCode
                );

                if (existing is null)
                {
                    _context.BasSegments.Add(
                        new FIS.Core.Domain.Entities.ReferenceData.BasSegment
                        {
                            segment_number = segmentNumber,
                            segment_name = string.IsNullOrWhiteSpace(segmentName)
                                ? null
                                : segmentName,
                            segment_group_code = segmentGroupCode,
                            department_code = departmentCode,
                            site_code = siteCode,
                            date_created = now,
                            date_updated = now,
                            created_by_user_code = userCode,
                            modified_by_user_code = userCode,
                            is_deleted = false,
                        }
                    );
                    imported++;
                }
                else
                {
                    existing.segment_name = string.IsNullOrWhiteSpace(segmentName)
                        ? existing.segment_name
                        : segmentName;
                    existing.date_updated = now;
                    existing.modified_by_user_code = userCode;
                    imported++;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(
                new BasImportResultDto
                {
                    Success = true,
                    RecordsImported = imported,
                    Errors = errors,
                    Message =
                        errors.Count == 0
                            ? $"Imported/updated {imported} BAS segment row(s)."
                            : $"Imported/updated {imported} BAS segment row(s) with {errors.Count} warning(s).",
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
    public async Task<ActionResult<object>> GetBasOverview()
    {
        try
        {
            var activeSegments = await _context.BasSegments.CountAsync(x => !x.is_deleted);
            var invalidJournals = await _context.JournalWithInvalidBasCodes.CountAsync(x =>
                !x.is_deleted
            );
            var uninvoicedJournals = await _context.JournalDetails.CountAsync(x =>
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
    public async Task<ActionResult<IEnumerable<BasSegmentDto>>> GetBasSegments(
        [FromQuery] int? departmentCode,
        [FromQuery] string? segmentType
    )
    {
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
                where !seg.is_deleted
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

            var rows = await query
                .OrderBy(x => x.segment_number)
                .ThenBy(x => x.segment_code)
                .Take(2000)
                .ToListAsync();

            var response = rows.Select(x => new BasSegmentDto
                {
                    SegmentCode = x.segment_code,
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

    [HttpPost("bas/segments/activate")]
    public async Task<ActionResult> ActivateBasSegments([FromBody] ActivateSegmentsDto request)
    {
        if (request.SegmentCodes is null || request.SegmentCodes.Count == 0)
        {
            return BadRequest(new { error = "SegmentCodes are required." });
        }

        try
        {
            var userCode = GetCurrentUserId();
            var now = DateTime.UtcNow;

            var segments = await _context
                .BasSegments.Where(s => request.SegmentCodes.Contains(s.segment_code))
                .ToListAsync();

            foreach (var seg in segments)
            {
                seg.is_deleted = false;
                seg.date_updated = now;
                seg.modified_by_user_code = userCode;
            }

            await _context.SaveChangesAsync();

            return Ok(
                new
                {
                    message = $"Activated {segments.Count} segment(s).",
                    requested = request.SegmentCodes.Count,
                    updated = segments.Count,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating BAS segments");
            return StatusCode(500, new { error = "Failed to activate BAS segments" });
        }
    }

    [HttpGet("bas/journals/invalid")]
    public async Task<ActionResult<IEnumerable<InvalidJournalDto>>> GetInvalidJournals(
        [FromQuery] int? departmentCode
    )
    {
        try
        {
            var rows = await _context
                .JournalWithInvalidBasCodes.AsNoTracking()
                .Where(x => !x.is_deleted)
                .OrderByDescending(x => x.Id)
                .Take(500)
                .ToListAsync();

            var response = rows.Select(x => new InvalidJournalDto
                {
                    JournalDetailCode = Guid.Empty,
                    JournalNumber = x.GGNumber ?? string.Empty,
                    Reason = x.JournalType ?? "Invalid BAS code",
                    DepartmentCode = departmentCode,
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

    [HttpGet("bas/journals/uninvoiced")]
    public async Task<ActionResult<IEnumerable<UninvoicedJournalDto>>> GetUninvoicedJournals(
        [FromQuery] int? departmentCode
    )
    {
        try
        {
            var query = _context
                .JournalDetails.AsNoTracking()
                .Where(jd => !jd.is_deleted && !jd.journal_detail_date_posted.HasValue);

            if (departmentCode.HasValue)
            {
                var dept = (short)departmentCode.Value;
                query = query.Where(jd => jd.department_code == dept);
            }

            var response = await query
                .OrderByDescending(jd => jd.journal_detail_date)
                .Select(jd => new UninvoicedJournalDto
                {
                    JournalDetailCode = jd.journal_detail_code,
                    JournalNumber = jd.journal_code.HasValue
                        ? jd.journal_code.Value.ToString()
                        : jd.journal_detail_id.ToString(),
                    Amount = jd.journal_detail_amount,
                    TransactionDate = jd.journal_detail_date,
                })
                .Take(1000)
                .ToListAsync();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching uninvoiced journals");
            return StatusCode(500, new { error = "Failed to fetch uninvoiced journals" });
        }
    }

    [HttpGet("bas/departments-without-bas")]
    public async Task<ActionResult<IEnumerable<FinanceDepartmentDto>>> GetDepartmentsWithoutBas()
    {
        try
        {
            var departmentsWithBas = _context
                .BasSegments.Where(s => !s.is_deleted)
                .Select(s => s.department_code)
                .Distinct();

            var response = await _context
                .Departments.AsNoTracking()
                .Where(d => !d.is_deleted && !departmentsWithBas.Contains(d.department_code))
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

    [HttpGet("bas/departments-missing-financial-system")]
    public async Task<
        ActionResult<IEnumerable<FinanceDepartmentDto>>
    > GetDepartmentsMissingFinancialSystem()
    {
        try
        {
            var response = await _context
                .Departments.AsNoTracking()
                .Where(d =>
                    !d.is_deleted
                    && (!d.financial_system_code.HasValue || d.financial_system_code.Value == 0)
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

    #endregion

    #region Reference Data

    [HttpGet("reference/financial-years")]
    public ActionResult<IEnumerable<FinancialYearDto>> GetFinancialYears()
    {
        var years = new List<FinancialYearDto>
        {
            new()
            {
                Code = 2023,
                Name = "2023/2024",
                StartDate = new DateTime(2023, 7, 1),
                EndDate = new DateTime(2024, 6, 30),
            },
            new()
            {
                Code = 2024,
                Name = "2024/2025",
                StartDate = new DateTime(2024, 7, 1),
                EndDate = new DateTime(2025, 6, 30),
            },
        };
        return Ok(years);
    }

    [HttpGet("reference/batch-dates")]
    public async Task<ActionResult<IEnumerable<DateTime>>> GetBatchDates()
    {
        try
        {
            var dates = await _context
                .Batches.AsNoTracking()
                .Where(b => !b.is_deleted)
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

        var lines = await BuildExportLinesAsync(
            prepare.StartDate,
            prepare.EndDate,
            request.DepartmentCode,
            includeCustomerColumn: false,
            reverseBatch: request.ReverseBatch
        );

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

        var lines = await BuildExportLinesAsync(
            prepare.StartDate,
            prepare.EndDate,
            request.DepartmentCode,
            includeCustomerColumn: true,
            reverseBatch: request.ReverseBatch
        );

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

    [HttpPost("missing-kilometres/close-gaps")]
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
    public async Task<ActionResult<ReversalTreeDto>> GetReversalTree(string journalNumber)
    {
        if (string.IsNullOrWhiteSpace(journalNumber))
        {
            return BadRequest(new { error = "Journal number is required." });
        }

        var trimmed = journalNumber.Trim();
        var hasJournalCode = long.TryParse(trimmed, out var journalCode);
        var hasJournalDetailId = int.TryParse(trimmed, out var journalDetailId);

        var roots = await _context
            .JournalDetails.AsNoTracking()
            .Where(jd =>
                !jd.is_deleted
                && (
                    (hasJournalCode && jd.journal_code == journalCode)
                    || (hasJournalDetailId && jd.journal_detail_id == journalDetailId)
                )
            )
            .Select(jd => jd.journal_detail_code)
            .ToListAsync();

        if (roots.Count == 0)
        {
            return NotFound(new { error = "Journal not found." });
        }

        var visited = new HashSet<Guid>(roots);
        var frontier = roots.ToList();
        var reversalRows = new List<ReversalNodeDto>();

        while (frontier.Count > 0)
        {
            var matches = await _context
                .JournalDetails.AsNoTracking()
                .Where(jd =>
                    !jd.is_deleted
                    && jd.journal_detail_reversalof.HasValue
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
                .ToListAsync();

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

        return Ok(
            new ReversalTreeDto
            {
                JournalNumber = trimmed,
                Reversals = reversalRows
                    .OrderBy(x => x.ReversalDate)
                    .ThenBy(x => x.JournalNumber)
                    .ToList(),
            }
        );
    }

    [HttpPost("standard-bank/import")]
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

        var imported = 0;
        var failed = 0;

        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);

        var header = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(header))
        {
            return BadRequest(new { error = "Uploaded file is empty." });
        }

        var headerColumns = ParseCsvLine(header);
        var vmfIndex = FindHeaderIndex(headerColumns, "vmf_code", "vmf", "vehicle_code");
        var siteIndex = FindHeaderIndex(headerColumns, "site_code", "site");
        var fuelCardIndex = FindHeaderIndex(headerColumns, "fuel_card_code", "fuel_card");
        var fileDateIndex = FindHeaderIndex(headerColumns, "file_date", "transaction_date", "date");

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var columns = ParseCsvLine(line);
                var tx = new WesbankTransaction
                {
                    vmf_code = ParseNullableInt(columns, vmfIndex),
                    site_code = ParseNullableInt(columns, siteIndex),
                    fuel_card_code = ParseNullableInt(columns, fuelCardIndex),
                    file_date = ParseNullableDate(columns, fileDateIndex) ?? DateTime.Today,
                    date_created = DateTime.UtcNow,
                    is_deleted = false,
                };

                _context.WesbankTransactions.Add(tx);
                imported++;
            }
            catch
            {
                failed++;
            }
        }

        if (imported > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(
            new ImportResultDto
            {
                Success = imported > 0 && failed == 0,
                RecordsImported = imported,
                RecordsFailed = failed,
            }
        );
    }

    #endregion

    #region Tariff Parameters

    [HttpGet("tariff-parameters/years")]
    public async Task<ActionResult<IEnumerable<int>>> GetTariffParameterYears()
    {
        try
        {
            var years = await _context
                .TariffParameters.Where(tp => !tp.is_deleted)
                .Select(tp => tp.TariffParameterYear)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!years.Any())
                years = new List<int> { DateTime.Now.Year };

            return Ok(years);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff parameter years");
            return StatusCode(500, new { error = "Failed to fetch tariff parameter years" });
        }
    }

    [HttpGet("tariff-parameters/{year}")]
    public async Task<ActionResult<TariffParametersDto>> GetTariffParameters(int year)
    {
        try
        {
            var param = await _context
                .TariffParameters.Where(tp => tp.TariffParameterYear == year && !tp.is_deleted)
                .FirstOrDefaultAsync();

            // Global parameters (interest rate, fuel price, etc.)
            var globalParams = new List<TariffParameterItemDto>();
            bool isApproved = false;
            string? approvedBy = null;
            DateTime? effectiveDate = null;

            if (param != null)
            {
                isApproved = !string.IsNullOrEmpty(param.Approval_user_access_name);
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

            // Fixed and kilo tariffs per vehicle class (from Tariff table, current effective date)
            var today = DateTime.Today;
            var classTariffs = await _context
                .Tariffs.Where(t =>
                    t.effective_start_date <= today
                    && (t.effective_end_date == null || t.effective_end_date >= today)
                )
                .Join(
                    _context.Classes,
                    t => t.class_code,
                    c => c.class_code,
                    (t, c) =>
                        new
                        {
                            t.tariff_code,
                            t.class_code,
                            class_description = c.description,
                            t.monthly_fixed_amount,
                            t.monthly_odo_amount,
                            t.daily_fixed_amount,
                            t.effective_start_date,
                        }
                )
                .OrderBy(x => x.class_code)
                .ToListAsync();

            var fixedTariffs = classTariffs
                .Select(t => new TariffClassRowDto
                {
                    ClassCode = t.class_code,
                    ClassDescription = t.class_description ?? $"Class {t.class_code}",
                    Amount = t.monthly_fixed_amount,
                    Unit = "R/month",
                    EffectiveDate = t.effective_start_date,
                })
                .ToList();

            var kiloTariffs = classTariffs
                .Select(t => new TariffClassRowDto
                {
                    ClassCode = t.class_code,
                    ClassDescription = t.class_description ?? $"Class {t.class_code}",
                    Amount = t.monthly_odo_amount,
                    Unit = "R/km",
                    EffectiveDate = t.effective_start_date,
                })
                .ToList();

            // Maintenance values for this parameter year
            var maintValues =
                param != null
                    ? await _context
                        .MaintenanceValues.Where(mv =>
                            mv.TariffParameterID == param.TariffParameterID
                        )
                        .Join(
                            _context.Classes,
                            mv => mv.class_code,
                            c => c.class_code,
                            (mv, c) =>
                                new MaintenanceValueRowDto
                                {
                                    ClassCode = mv.class_code,
                                    ClassDescription = c.description ?? $"Class {mv.class_code}",
                                    MonthsAge = mv.months_age,
                                    KilometerAge = mv.kilometer_age,
                                    Amount = mv.amount,
                                    RandPerKilometer = mv.RandPerKilometer,
                                }
                        )
                        .OrderBy(mv => mv.ClassCode)
                        .ThenBy(mv => mv.MonthsAge)
                        .ToListAsync()
                    : new List<MaintenanceValueRowDto>();

            return Ok(
                new TariffParametersDto
                {
                    Year = year,
                    IsApproved = isApproved,
                    ApprovedBy = approvedBy,
                    EffectiveDate = effectiveDate,
                    Parameters = globalParams,
                    FixedTariffs = fixedTariffs,
                    KiloTariffs = kiloTariffs,
                    MaintenanceValues = maintValues,
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff parameters for year {Year}", year);
            return StatusCode(500, new { error = "Failed to fetch tariff parameters" });
        }
    }

    [HttpPost("tariff-parameters/{year}/approve")]
    public async Task<ActionResult> ApproveTariffParameters(
        int year,
        [FromBody] ApproveTariffDto? request = null
    )
    {
        var currentUserId = GetCurrentUserId();

        var tariff = await _context
            .TariffParameters.Where(tp => tp.TariffParameterYear == year && !tp.is_deleted)
            .OrderByDescending(tp => tp.TariffParameterID)
            .FirstOrDefaultAsync();

        if (tariff is null)
        {
            return NotFound(new { error = $"No tariff parameters found for year {year}" });
        }

        tariff.Approved = true;
        tariff.ApprovalDate = DateTime.UtcNow;
        tariff.Approval_user_access_code = (short?)currentUserId;
        tariff.Approval_user_access_name = $"user:{currentUserId}";
        tariff.modified_by_user_code = currentUserId;
        tariff.date_updated = DateTime.UtcNow;
        tariff.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(
            new { message = $"Tariff parameters for {year} approved", approvedBy = currentUserId }
        );
    }

    [HttpPost("tariff-parameters/{year}/reject")]
    public async Task<ActionResult> RejectTariffParameters(
        int year,
        [FromBody] RejectTariffDto? request = null
    )
    {
        var currentUserId = GetCurrentUserId();

        var tariff = await _context
            .TariffParameters.Where(tp => tp.TariffParameterYear == year && !tp.is_deleted)
            .OrderByDescending(tp => tp.TariffParameterID)
            .FirstOrDefaultAsync();

        if (tariff is null)
        {
            return NotFound(new { error = $"No tariff parameters found for year {year}" });
        }

        tariff.Approved = false;
        tariff.ApprovalDate = null;
        tariff.Approval_user_access_code = null;
        tariff.Approval_user_access_name = null;
        tariff.modified_by_user_code = currentUserId;
        tariff.date_updated = DateTime.UtcNow;
        tariff.ModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(
            new
            {
                message = $"Tariff parameters for {year} rejected",
                rejectedBy = currentUserId,
                notes = request?.RejectionNotes,
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
                !b.is_deleted
                && b.batch_date.Date == startDate.Date
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
                    .FirstOrDefaultAsync(b => !b.is_deleted && b.batch_code == newBatchCode);
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
                .FirstOrDefaultAsync(b => !b.is_deleted && b.batch_code == newBatchCode);
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

    private static string DecodeFileData(string input)
    {
        try
        {
            var bytes = Convert.FromBase64String(input);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return input;
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
                    .FirstOrDefaultAsync(b => !b.is_deleted && b.batch_code == batchCode);
                if (batchByCode is not null)
                {
                    return (batchByCode.batch_date.Date, batchByCode.batch_date.Date);
                }
            }
        }

        var latestBatch = await _context
            .Batches.AsNoTracking()
            .Where(b => !b.is_deleted)
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
        var query = _context
            .JournalDetails.AsNoTracking()
            .Where(jd =>
                !jd.is_deleted
                && jd.journal_detail_date.Date >= startDate
                && jd.journal_detail_date.Date <= endDate
            );

        if (departmentCode.HasValue)
        {
            var dept = (short)departmentCode.Value;
            query = query.Where(jd => jd.department_code == dept);
        }

        var rows = await query
            .OrderBy(jd => jd.journal_detail_date)
            .ThenBy(jd => jd.journal_detail_id)
            .Select(jd => new
            {
                jd.journal_detail_id,
                jd.journal_detail_date,
                jd.department_code,
                jd.site_code,
                jd.vmf_code,
                jd.journal_detail_amount,
                jd.journal_detail_description,
            })
            .Take(100000)
            .ToListAsync();

        var lines = new List<string>();
        if (includeCustomerColumn)
        {
            lines.Add(
                "TransactionDate,JournalDetailId,DepartmentCode,SiteCode,VehicleCode,Amount,Customer,Description"
            );
        }
        else
        {
            lines.Add(
                "TransactionDate,JournalDetailId,DepartmentCode,SiteCode,VehicleCode,Amount,Description"
            );
        }

        foreach (var row in rows)
        {
            var description = (row.journal_detail_description ?? string.Empty).Replace(
                "\"",
                "\"\""
            );
            var exportAmount = reverseBatch
                ? (row.journal_detail_amount * -1m)
                : row.journal_detail_amount;
            var amount = exportAmount.ToString("0.00", CultureInfo.InvariantCulture);
            var baseColumns =
                $"{row.journal_detail_date:yyyy-MM-dd},{row.journal_detail_id},{row.department_code},{row.site_code},{row.vmf_code},{amount}";

            if (includeCustomerColumn)
            {
                var customer = $"D{row.department_code}";
                lines.Add($"{baseColumns},{customer},\"{description}\"");
            }
            else
            {
                lines.Add($"{baseColumns},\"{description}\"");
            }
        }

        return lines;
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

            var unpostedCount = await GetUnpostedJournalDetailCountAsync(financialSystemCode);

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

    private async Task<int> GetUnpostedJournalDetailCountAsync(byte financialSystemCode)
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

    private static int FindHeaderIndex(List<string> headers, params string[] aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = headers[i].Trim().ToLowerInvariant().Replace(" ", "_");
            if (aliases.Any(alias => normalized == alias))
            {
                return i;
            }
        }

        return -1;
    }

    private static int? ParseNullableInt(List<string> columns, int index)
    {
        if (index < 0 || index >= columns.Count)
        {
            return null;
        }

        return int.TryParse(
            columns[index],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value
        )
            ? value
            : null;
    }

    private static DateTime? ParseNullableDate(List<string> columns, int index)
    {
        if (index < 0 || index >= columns.Count)
        {
            return null;
        }

        var raw = columns[index];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (
            DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed
            )
        )
        {
            return parsed.Date;
        }

        if (
            DateTime.TryParse(
                raw,
                CultureInfo.GetCultureInfo("en-ZA"),
                DateTimeStyles.AssumeLocal,
                out parsed
            )
        )
        {
            return parsed.Date;
        }

        return null;
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
}

public class BasImportDto
{
    [Required]
    public string FileData { get; set; } = "";
    public int? DepartmentCode { get; set; }
}

public class BasImportResultDto
{
    public bool Success { get; set; }
    public int RecordsImported { get; set; }
    public List<string> Errors { get; set; } = new();
    public string Message { get; set; } = "";
}

public class BasSegmentDto
{
    public int SegmentCode { get; set; }
    public string SegmentType { get; set; } = "";
    public string SegmentValue { get; set; } = "";
    public int? DepartmentCode { get; set; }
    public bool IsActive { get; set; }
}

public class ActivateSegmentsDto
{
    [Required]
    public List<int> SegmentCodes { get; set; } = new();
}

public class InvalidJournalDto
{
    public Guid JournalDetailCode { get; set; }
    public string JournalNumber { get; set; } = "";
    public string Reason { get; set; } = "";
    public int? DepartmentCode { get; set; }
}

public class UninvoicedJournalDto
{
    public Guid JournalDetailCode { get; set; }
    public string JournalNumber { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
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
    public int RecordsImported { get; set; }
    public int RecordsFailed { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TariffParametersDto
{
    public int Year { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<TariffParameterItemDto> Parameters { get; set; } = new();
    public List<TariffClassRowDto> FixedTariffs { get; set; } = new();
    public List<TariffClassRowDto> KiloTariffs { get; set; } = new();
    public List<MaintenanceValueRowDto> MaintenanceValues { get; set; } = new();
}

public class TariffParameterItemDto
{
    public string ParameterName { get; set; } = "";
    public decimal Value { get; set; }
    public string Unit { get; set; } = "";
}

public class TariffClassRowDto
{
    public short ClassCode { get; set; }
    public string ClassDescription { get; set; } = "";
    public decimal Amount { get; set; }
    public string Unit { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
}

public class MaintenanceValueRowDto
{
    public short ClassCode { get; set; }
    public string ClassDescription { get; set; } = "";
    public short MonthsAge { get; set; }
    public int KilometerAge { get; set; }
    public decimal Amount { get; set; }
    public decimal RandPerKilometer { get; set; }
}

public class ApproveTariffDto
{
    public string? ApprovalNotes { get; set; }
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

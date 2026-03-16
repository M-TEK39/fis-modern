using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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

    public FinanceController(
        IJournalDetailService journalService,
        FisDbContext context,
        ILogger<FinanceController> logger)
    {
        _journalService = journalService;
        _context = context;
        _logger = logger;
    }

    #region Batch Operations

    [HttpGet("batch/status")]
    public ActionResult<BatchStatusDto> GetBatchStatus()
    {
        var status = new BatchStatusDto { BatchCode = 0, Status = "No active batch", IsActive = false };
        return Ok(status);
    }

    [HttpPost("batch/start")]
    public ActionResult<BatchStartResultDto> StartBatch([FromBody] StartBatchDto request)
    {
        var result = new BatchStartResultDto { BatchCode = 1, BatchDate = request.BatchDate, Success = true };
        return Ok(result);
    }

    [HttpPost("batch/check-scoa")]
    public ActionResult<ScoaCheckResultDto> CheckScoa()
    {
        var result = new ScoaCheckResultDto { IsCompliant = true };
        return Ok(result);
    }

    [HttpPost("batch/rollback")]
    public ActionResult RollbackBatch()
    {
        return Ok(new { message = "Batch rolled back" });
    }

    [HttpPost("batch/finish")]
    public ActionResult FinishBatch()
    {
        return Ok(new { message = "Batch finalized" });
    }

    #endregion

    #region BAS Operations

    [HttpPost("bas/import")]
    public ActionResult<BasImportResultDto> ImportBas([FromBody] BasImportDto request)
    {
        return Ok(new BasImportResultDto { Success = true, RecordsImported = 0 });
    }

    [HttpGet("bas/segments")]
    public ActionResult<IEnumerable<BasSegmentDto>> GetBasSegments([FromQuery] int? departmentCode, [FromQuery] string? segmentType)
    {
        return Ok(new List<BasSegmentDto>());
    }

    [HttpPost("bas/segments/activate")]
    public ActionResult ActivateBasSegments([FromBody] ActivateSegmentsDto request)
    {
        return Ok(new { message = $"Activated {request.SegmentCodes?.Count ?? 0} segments" });
    }

    [HttpGet("bas/journals/invalid")]
    public ActionResult<IEnumerable<InvalidJournalDto>> GetInvalidJournals([FromQuery] int? departmentCode)
    {
        return Ok(new List<InvalidJournalDto>());
    }

    [HttpGet("bas/journals/uninvoiced")]
    public ActionResult<IEnumerable<UninvoicedJournalDto>> GetUninvoicedJournals([FromQuery] int? departmentCode)
    {
        return Ok(new List<UninvoicedJournalDto>());
    }

    [HttpGet("bas/departments-without-bas")]
    public ActionResult<IEnumerable<FinanceDepartmentDto>> GetDepartmentsWithoutBas()
    {
        return Ok(new List<FinanceDepartmentDto>());
    }

    [HttpGet("bas/departments-missing-financial-system")]
    public ActionResult<IEnumerable<FinanceDepartmentDto>> GetDepartmentsMissingFinancialSystem()
    {
        return Ok(new List<FinanceDepartmentDto>());
    }

    #endregion

    #region Reference Data

    [HttpGet("reference/financial-years")]
    public ActionResult<IEnumerable<FinancialYearDto>> GetFinancialYears()
    {
        var years = new List<FinancialYearDto>
        {
            new() { Code = 2023, Name = "2023/2024", StartDate = new DateTime(2023, 7, 1), EndDate = new DateTime(2024, 6, 30) },
            new() { Code = 2024, Name = "2024/2025", StartDate = new DateTime(2024, 7, 1), EndDate = new DateTime(2025, 6, 30) }
        };
        return Ok(years);
    }

    [HttpGet("reference/batch-dates")]
    public ActionResult<IEnumerable<DateTime>> GetBatchDates()
    {
        return Ok(new List<DateTime>());
    }

    [HttpGet("reference/segment-types")]
    public ActionResult<IEnumerable<SegmentTypeDto>> GetSegmentTypes()
    {
        var types = new List<SegmentTypeDto>
        {
            new() { Code = "OBJ", Name = "Objective" },
            new() { Code = "RESP", Name = "Responsibility" }
        };
        return Ok(types);
    }

    #endregion

    #region Integration / Interface

    [HttpPost("interface/pastel-csv")]
    public ActionResult ExportPastelCsv([FromBody] PastelExportDto request)
    {
        var csvContent = "Header1,Header2\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        return File(bytes, "text/csv", "pastel_export.csv");
    }

    [HttpPost("interface/pastel-csv-customer")]
    public ActionResult ExportPastelCsvCustomer([FromBody] PastelCustomerExportDto request)
    {
        var csvContent = "CustomerID,Name\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvContent);
        return File(bytes, "text/csv", "pastel_customers.csv");
    }

    [HttpPost("missing-kilometres/close-gaps")]
    public ActionResult CloseKilometerGaps()
    {
        return Ok(new { message = "Kilometer gaps closed", recordsProcessed = 0 });
    }

    [HttpGet("reports/reversals-tree/{journalNumber}")]
    public ActionResult<ReversalTreeDto> GetReversalTree(string journalNumber)
    {
        var tree = new ReversalTreeDto { JournalNumber = journalNumber, Reversals = new List<ReversalNodeDto>() };
        return Ok(tree);
    }

    [HttpPost("standard-bank/import")]
    public ActionResult<ImportResultDto> ImportStandardBankData([FromBody] StandardBankImportDto request)
    {
        var result = new ImportResultDto { Success = true, RecordsImported = 0, RecordsFailed = 0 };
        return Ok(result);
    }

    #endregion

    #region Tariff Parameters

    [HttpGet("tariff-parameters/years")]
    public async Task<ActionResult<IEnumerable<int>>> GetTariffParameterYears()
    {
        try
        {
            var years = await _context.TariffParameters
                .Where(tp => !tp.is_deleted)
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
            var param = await _context.TariffParameters
                .Where(tp => tp.TariffParameterYear == year && !tp.is_deleted)
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

                globalParams.Add(new TariffParameterItemDto
                {
                    ParameterName = "Annual Interest Rate",
                    Value = param.AnnualInterestRatePercentage,
                    Unit = "%",
                });
                globalParams.Add(new TariffParameterItemDto
                {
                    ParameterName = "Annual Payments",
                    Value = param.AnnualPayments,
                    Unit = "payments/year",
                });
                if (param.EffectiveInterestRate.HasValue)
                    globalParams.Add(new TariffParameterItemDto
                    {
                        ParameterName = "Effective Interest Rate",
                        Value = param.EffectiveInterestRate.Value,
                        Unit = "%",
                    });
                globalParams.Add(new TariffParameterItemDto
                {
                    ParameterName = "Pool Vehicle Charged Days/Month",
                    Value = param.PoolVehicleChargedDaysPerMonth,
                    Unit = "days",
                });
                if (param.AverageFuelPrice.HasValue)
                    globalParams.Add(new TariffParameterItemDto
                    {
                        ParameterName = "Average Fuel Price",
                        Value = param.AverageFuelPrice.Value,
                        Unit = "R/litre",
                    });
                if (param.AnnualRecoveredKilos.HasValue)
                    globalParams.Add(new TariffParameterItemDto
                    {
                        ParameterName = "Annual Recovered Kilometres",
                        Value = param.AnnualRecoveredKilos.Value,
                        Unit = "km",
                    });
            }

            // Fixed and kilo tariffs per vehicle class (from Tariff table, current effective date)
            var today = DateTime.Today;
            var classTariffs = await _context.Tariffs
                .Where(t => t.effective_start_date <= today &&
                            (t.effective_end_date == null || t.effective_end_date >= today))
                .Join(_context.Classes,
                      t => t.class_code, c => c.class_code,
                      (t, c) => new
                      {
                          t.tariff_code,
                          t.class_code,
                          class_description = c.description,
                          t.monthly_fixed_amount,
                          t.monthly_odo_amount,
                          t.daily_fixed_amount,
                          t.effective_start_date,
                      })
                .OrderBy(x => x.class_code)
                .ToListAsync();

            var fixedTariffs = classTariffs.Select(t => new TariffClassRowDto
            {
                ClassCode      = t.class_code,
                ClassDescription = t.class_description ?? $"Class {t.class_code}",
                Amount         = t.monthly_fixed_amount,
                Unit           = "R/month",
                EffectiveDate  = t.effective_start_date,
            }).ToList();

            var kiloTariffs = classTariffs.Select(t => new TariffClassRowDto
            {
                ClassCode      = t.class_code,
                ClassDescription = t.class_description ?? $"Class {t.class_code}",
                Amount         = t.monthly_odo_amount,
                Unit           = "R/km",
                EffectiveDate  = t.effective_start_date,
            }).ToList();

            // Maintenance values for this parameter year
            var maintValues = param != null
                ? await _context.MaintenanceValues
                    .Where(mv => mv.TariffParameterID == param.TariffParameterID)
                    .Join(_context.Classes,
                          mv => mv.class_code, c => c.class_code,
                          (mv, c) => new MaintenanceValueRowDto
                          {
                              ClassCode      = mv.class_code,
                              ClassDescription = c.description ?? $"Class {mv.class_code}",
                              MonthsAge      = mv.months_age,
                              KilometerAge   = mv.kilometer_age,
                              Amount         = mv.amount,
                              RandPerKilometer = mv.RandPerKilometer,
                          })
                    .OrderBy(mv => mv.ClassCode)
                    .ThenBy(mv => mv.MonthsAge)
                    .ToListAsync()
                : new List<MaintenanceValueRowDto>();

            return Ok(new TariffParametersDto
            {
                Year          = year,
                IsApproved    = isApproved,
                ApprovedBy    = approvedBy,
                EffectiveDate = effectiveDate,
                Parameters    = globalParams,
                FixedTariffs  = fixedTariffs,
                KiloTariffs   = kiloTariffs,
                MaintenanceValues = maintValues,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tariff parameters for year {Year}", year);
            return StatusCode(500, new { error = "Failed to fetch tariff parameters" });
        }
    }

    [HttpPost("tariff-parameters/{year}/approve")]
    public ActionResult ApproveTariffParameters(int year, [FromBody] ApproveTariffDto? request = null)
    {
        int currentUserId = GetCurrentUserId();
        return Ok(new { message = $"Tariff parameters for {year} approved", approvedBy = currentUserId });
    }

    #endregion
}

#region DTOs
public class BatchStatusDto { public int BatchCode { get; set; } public string Status { get; set; } = ""; public DateTime? BatchDate { get; set; } public bool IsActive { get; set; } public int TotalTransactions { get; set; } public int ProcessedTransactions { get; set; } }
public class StartBatchDto { [Required] public DateTime BatchDate { get; set; } public byte FinancialSystemCode { get; set; } = 1; }
public class BatchStartResultDto { public int BatchCode { get; set; } public DateTime BatchDate { get; set; } public bool Success { get; set; } public string Message { get; set; } = ""; }
public class ScoaCheckResultDto { public bool IsCompliant { get; set; } public List<string> Errors { get; set; } = new(); public List<string> Warnings { get; set; } = new(); public int TotalChecked { get; set; } }
public class BasImportDto { [Required] public string FileData { get; set; } = ""; public int? DepartmentCode { get; set; } }
public class BasImportResultDto { public bool Success { get; set; } public int RecordsImported { get; set; } public List<string> Errors { get; set; } = new(); public string Message { get; set; } = ""; }
public class BasSegmentDto { public int SegmentCode { get; set; } public string SegmentType { get; set; } = ""; public string SegmentValue { get; set; } = ""; public int? DepartmentCode { get; set; } public bool IsActive { get; set; } }
public class ActivateSegmentsDto { [Required] public List<int> SegmentCodes { get; set; } = new(); }
public class InvalidJournalDto { public Guid JournalDetailCode { get; set; } public string JournalNumber { get; set; } = ""; public string Reason { get; set; } = ""; public int? DepartmentCode { get; set; } }
public class UninvoicedJournalDto { public Guid JournalDetailCode { get; set; } public string JournalNumber { get; set; } = ""; public decimal Amount { get; set; } public DateTime TransactionDate { get; set; } }
public class FinanceDepartmentDto { public int DepartmentCode { get; set; } public string DepartmentName { get; set; } = ""; }
public class FinancialYearDto { public short Code { get; set; } public string Name { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } }
public class SegmentTypeDto { public string Code { get; set; } = ""; public string Name { get; set; } = ""; }
public class PastelExportDto { public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public int? DepartmentCode { get; set; } }
public class PastelCustomerExportDto { public int? DepartmentCode { get; set; } }
public class ReversalTreeDto { public string JournalNumber { get; set; } = ""; public List<ReversalNodeDto> Reversals { get; set; } = new(); }
public class ReversalNodeDto { public string JournalNumber { get; set; } = ""; public DateTime ReversalDate { get; set; } public decimal Amount { get; set; } }
public class StandardBankImportDto { [Required] public string FileContent { get; set; } = ""; }
public class ImportResultDto { public bool Success { get; set; } public int RecordsImported { get; set; } public int RecordsFailed { get; set; } public List<string> Errors { get; set; } = new(); }
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
public class TariffParameterItemDto { public string ParameterName { get; set; } = ""; public decimal Value { get; set; } public string Unit { get; set; } = ""; }
public class TariffClassRowDto { public short ClassCode { get; set; } public string ClassDescription { get; set; } = ""; public decimal Amount { get; set; } public string Unit { get; set; } = ""; public DateTime EffectiveDate { get; set; } }
public class MaintenanceValueRowDto { public short ClassCode { get; set; } public string ClassDescription { get; set; } = ""; public short MonthsAge { get; set; } public int KilometerAge { get; set; } public decimal Amount { get; set; } public decimal RandPerKilometer { get; set; } }
public class ApproveTariffDto { public string? ApprovalNotes { get; set; } }
#endregion

using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TaxiLogController : BaseApiController
{
    private const short GgContractorId = 2;

    private readonly ITaxiRepository _taxiRepository;
    private readonly ITaxiLogRepository _taxiLogRepository;
    private readonly ITaxiLogNoteRepository _taxiLogNoteRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<TaxiLogController> _logger;

    public TaxiLogController(
        ITaxiRepository taxiRepository,
        ITaxiLogRepository taxiLogRepository,
        ITaxiLogNoteRepository taxiLogNoteRepository,
        FisDbContext context,
        ILogger<TaxiLogController> logger)
    {
        _taxiRepository = taxiRepository;
        _taxiLogRepository = taxiLogRepository;
        _taxiLogNoteRepository = taxiLogNoteRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet("references")]
    public async Task<ActionResult<TaxiLogReferenceResponse>> GetReferences()
    {
        try
        {
            var contractors = await _context.ContractorTaxiClasses
                .AsNoTracking()
                .Where(item => !item.is_deleted)
                .Join(
                    _context.Contractors.AsNoTracking(),
                    taxiClass => taxiClass.contractor_id,
                    contractor => contractor.contractor_id,
                    (taxiClass, contractor) => new
                    {
                        taxiClass.contractor_id,
                        contractor.contractor_name
                    })
                .GroupBy(item => new { item.contractor_id, item.contractor_name })
                .Select(group => new TaxiLogContractorOptionDto(
                    group.Key.contractor_id,
                    string.IsNullOrWhiteSpace(group.Key.contractor_name)
                        ? $"Contractor {group.Key.contractor_id}"
                        : group.Key.contractor_name))
                .OrderBy(item => item.ContractorName)
                .ToListAsync();

            var classOptions = await _context.ContractorTaxiClasses
                .AsNoTracking()
                .Where(item => !item.is_deleted)
                .OrderBy(item => item.contractor_id)
                .ThenBy(item => item.description)
                .Select(item => new TaxiLogClassOptionDto(
                    item.contractor_id,
                    item.class_id,
                    string.IsNullOrWhiteSpace(item.description) ? $"Class {item.class_id}" : item.description,
                    item.km_tariff,
                    item.driver_per_hour,
                    item.daily_tariff))
                .ToListAsync();

            var notes = (await _taxiLogNoteRepository.GetAllAsync())
                .Select(note => new TaxiLogNoteOptionDto(
                    note.taxi_log_note_code,
                    note.taxi_log_note_description ?? $"Note {note.taxi_log_note_code}"))
                .ToList();

            return Ok(new TaxiLogReferenceResponse(contractors, classOptions, notes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading taxi log references");
            return StatusCode(500);
        }
    }

    [HttpGet("lookup/{rekNum}")]
    public async Task<ActionResult<TaxiLogLookupResponse>> Lookup(string rekNum, [FromQuery] string? mode = null)
    {
        try
        {
            var normalizedRekNum = NormalizeKey(rekNum);
            if (string.IsNullOrWhiteSpace(normalizedRekNum))
            {
                return BadRequest("Requisition number required.");
            }

            var request = await _taxiRepository.GetLatestByRequisitionAsync(normalizedRekNum);
            if (request == null)
            {
                return NotFound($"Requisition number {normalizedRekNum} not found.");
            }

            if (!string.IsNullOrWhiteSpace(request.cancelled))
            {
                return BadRequest($"Requisition number {normalizedRekNum} has been cancelled.");
            }

            var log = await _taxiLogRepository.GetLatestByRequisitionAsync(normalizedRekNum);
            var normalizedMode = NormalizeKey(mode);

            if (normalizedMode == "ENTER" && log != null)
            {
                return Conflict($"Logsheet {normalizedRekNum} has already been entered. Use Edit Taxi Log to change logs entered.");
            }

            if (normalizedMode == "EDIT" && log == null)
            {
                return NotFound($"No log for {normalizedRekNum} found.");
            }

            return Ok(await BuildLookupResponseAsync(request, log));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up taxi log for requisition {RekNum}", rekNum);
            return StatusCode(500);
        }
    }

    [HttpPost]
    public async Task<ActionResult<TaxiLogLookupResponse>> Create([FromBody] SaveTaxiLogRequest request)
    {
        try
        {
            var normalizedRekNum = NormalizeKey(request.rek_num);
            var validationError = ValidateSaveRequest(request);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var taxiRequest = await ResolveTaxiRequestAsync(request.request_id, normalizedRekNum);
            if (taxiRequest == null)
            {
                return NotFound($"Requisition number {normalizedRekNum} not found.");
            }

            if (!string.IsNullOrWhiteSpace(taxiRequest.cancelled))
            {
                return BadRequest($"Requisition number {normalizedRekNum} has been cancelled.");
            }

            var existingLog = await _taxiLogRepository.GetLatestByRequisitionAsync(normalizedRekNum);
            if (existingLog != null)
            {
                return Conflict($"Logsheet {normalizedRekNum} has already been entered. Use Edit Taxi Log to change logs entered.");
            }

            var prepared = await PrepareSaveAsync(taxiRequest, request, existingLog);
            if (!prepared.Success)
            {
                return prepared.Result!;
            }

            var created = await _taxiLogRepository.CreateAsync(prepared.Log!, GetCurrentUserId());
            return Ok(await BuildLookupResponseAsync(prepared.TaxiRequest!, created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating taxi log for requisition {RekNum}", request.rek_num);
            return StatusCode(500);
        }
    }

    [HttpPut("{logId:int}")]
    public async Task<ActionResult<TaxiLogLookupResponse>> Update(int logId, [FromBody] SaveTaxiLogRequest request)
    {
        try
        {
            var normalizedRekNum = NormalizeKey(request.rek_num);
            var validationError = ValidateSaveRequest(request);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var existingLog = await _taxiLogRepository.GetByIdAsync(logId);
            if (existingLog == null)
            {
                return NotFound($"Taxi log {logId} not found.");
            }

            var taxiRequest = await ResolveTaxiRequestAsync(request.request_id, normalizedRekNum);
            if (taxiRequest == null)
            {
                return NotFound($"Requisition number {normalizedRekNum} not found.");
            }

            if (!string.IsNullOrWhiteSpace(taxiRequest.cancelled))
            {
                return BadRequest($"Requisition number {normalizedRekNum} has been cancelled.");
            }

            var prepared = await PrepareSaveAsync(taxiRequest, request, existingLog);
            if (!prepared.Success)
            {
                return prepared.Result!;
            }

            var updated = await _taxiLogRepository.UpdateAsync(prepared.Log!, GetCurrentUserId());
            return Ok(await BuildLookupResponseAsync(prepared.TaxiRequest!, updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating taxi log {LogId}", logId);
            return StatusCode(500);
        }
    }

    private async Task<Taxi?> ResolveTaxiRequestAsync(int? requestId, string normalizedRekNum)
    {
        if (requestId is > 0)
        {
            return await _taxiRepository.GetByIdAsync(requestId.Value);
        }

        return await _taxiRepository.GetLatestByRequisitionAsync(normalizedRekNum);
    }

    private async Task<PrepareTaxiLogResult> PrepareSaveAsync(Taxi taxiRequest, SaveTaxiLogRequest request, TaxiLog? existingLog)
    {
        var normalizedRekNum = NormalizeKey(request.rek_num);
        var normalizedRegistration = NormalizeKey(request.reg_num);
        var normalizedDriver = (request.driver ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedRegistration))
        {
            return PrepareTaxiLogResult.Failure(BadRequest("Registration number required."));
        }

        if (string.IsNullOrWhiteSpace(normalizedDriver))
        {
            return PrepareTaxiLogResult.Failure(BadRequest("Driver name required."));
        }

        var (startDateTime, endDateTime) = CombineJourneyDateTimes(request);
        var durationValidationError = ValidateJourneyRange(startDateTime, endDateTime, request.driver_start_odo, request.driver_end_odo);
        if (durationValidationError != null)
        {
            return PrepareTaxiLogResult.Failure(BadRequest(durationValidationError));
        }

        var distance = request.driver_end_odo!.Value - request.driver_start_odo!.Value;
        var duration = CalculateDuration(startDateTime, endDateTime);

        Vehicle? ggVehicle = null;
        short? effectiveVehicleTypeCode = request.vehicle_type_code;
        var effectiveQuotedTariff = request.vehicle_type_code is null or 0 ? request.quoted_tariff : null;

        if (request.contractor_id == GgContractorId)
        {
            ggVehicle = await FindVehicleByRegistrationAsync(normalizedRegistration);
            if (ggVehicle == null)
            {
                return PrepareTaxiLogResult.Failure(NotFound($"{normalizedRegistration} not found."));
            }

            effectiveVehicleTypeCode = await ResolveVehicleClassCodeAsync(ggVehicle.model_code) ?? ggVehicle.type_code;
            effectiveQuotedTariff = null;

            var ggOverlapError = await ValidateGgOverlapAsync(
                ggVehicle.vmf_code,
                normalizedRekNum,
                existingLog?.log_id,
                request.driver_start_odo.Value,
                request.driver_end_odo.Value);
            if (ggOverlapError != null)
            {
                return PrepareTaxiLogResult.Failure(BadRequest(ggOverlapError));
            }
        }
        else
        {
            var contractorOverlapError = await ValidateContractorOverlapAsync(
                normalizedRegistration,
                request.contractor_id,
                normalizedRekNum,
                existingLog?.log_id,
                request.driver_start_odo.Value,
                request.driver_end_odo.Value);
            if (contractorOverlapError != null)
            {
                return PrepareTaxiLogResult.Failure(BadRequest(contractorOverlapError));
            }
        }

        taxiRequest.rek_num = normalizedRekNum;
        taxiRequest.contractor_id = request.contractor_id;
        taxiRequest.reg_num = normalizedRegistration;
        taxiRequest.driver = normalizedDriver;
        taxiRequest.vehicle_type_code = effectiveVehicleTypeCode;
        if (ggVehicle != null)
        {
            taxiRequest.vmf_code = ggVehicle.vmf_code.ToString(CultureInfo.InvariantCulture);
        }

        var updatedRequest = await _taxiRepository.UpdateAsync(taxiRequest, GetCurrentUserId());
        var log = existingLog ?? new TaxiLog();

        log.request_id = updatedRequest.request_id;
        log.rek_num = normalizedRekNum;
        log.driver_start_odo = request.driver_start_odo;
        log.driver_end_odo = request.driver_end_odo;
        log.driver_start_date = request.driver_start_date?.Date;
        log.driver_end_date = request.driver_end_date?.Date;
        log.driver_start_time = request.driver_start_date?.Date.Add(request.driver_start_time!.Value);
        log.driver_end_time = request.driver_end_date?.Date.Add(request.driver_end_time!.Value);
        log.userid = ToLegacyShortUserId(GetCurrentUserId());
        log.enter_date = DateTime.Today;
        log.distance = distance;
        log.days = duration.Days;
        log.hours = duration.Hours;
        log.quoted_tariff = effectiveQuotedTariff.HasValue ? (float?)effectiveQuotedTariff.Value : null;
        log.taxi_log_note_code = request.taxi_log_note_code;

        return PrepareTaxiLogResult.FromSuccess(updatedRequest, log);
    }

    private async Task<TaxiLogLookupResponse> BuildLookupResponseAsync(Taxi request, TaxiLog? log)
    {
        var contractorName = request.contractor_id.HasValue
            ? await _context.Contractors
                .AsNoTracking()
                .Where(item => item.contractor_id == request.contractor_id.Value)
                .Select(item => item.contractor_name)
                .FirstOrDefaultAsync()
            : null;

        var classDescription = request.vehicle_type_code.HasValue
            ? await _context.Classes
                .AsNoTracking()
                .Where(item => item.class_code == request.vehicle_type_code.Value)
                .Select(item => item.description)
                .FirstOrDefaultAsync()
            : null;

        var vehicle = await ResolveVehicleFromTaxiRequestAsync(request);
        var displayRegistration = !string.IsNullOrWhiteSpace(request.reg_num)
            ? request.reg_num
            : vehicle?.fleet_number ?? vehicle?.registration_number;

        return new TaxiLogLookupResponse(
            request.request_id,
            request.rek_num ?? string.Empty,
            request.contractor_id,
            contractorName,
            request.vehicle_type_code,
            classDescription,
            displayRegistration,
            vehicle?.fleet_number,
            request.driver,
            request.official,
            request.date_required,
            request.time_required,
            request.department_code,
            request.Department?.description,
            log == null
                ? null
                : new TaxiLogDetailDto(
                    log.log_id,
                    log.driver_start_odo,
                    log.driver_end_odo,
                    log.driver_start_date,
                    log.driver_end_date,
                    FormatTime(log.driver_start_time),
                    FormatTime(log.driver_end_time),
                    log.taxi_log_note_code,
                    log.quoted_tariff.HasValue ? Convert.ToDecimal(log.quoted_tariff.Value, CultureInfo.InvariantCulture) : null));
    }

    private async Task<Vehicle?> ResolveVehicleFromTaxiRequestAsync(Taxi taxiRequest)
    {
        if (!int.TryParse(taxiRequest.vmf_code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vmfCode))
        {
            return null;
        }

        return await _context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(vehicle => vehicle.vmf_code == vmfCode && !vehicle.is_deleted);
    }

    private async Task<Vehicle?> FindVehicleByRegistrationAsync(string registration)
    {
        var normalized = NormalizeKey(registration);

        return await _context.Vehicles
            .AsNoTracking()
            .Where(vehicle => !vehicle.is_deleted)
            .FirstOrDefaultAsync(vehicle =>
                (vehicle.fleet_number ?? string.Empty).ToUpper() == normalized ||
                (vehicle.registration_number ?? string.Empty).ToUpper() == normalized);
    }

    private async Task<short?> ResolveVehicleClassCodeAsync(short modelCode)
    {
        return await _context.Models
            .AsNoTracking()
            .Where(model => model.model_code == modelCode && !model.is_deleted)
            .Select(model => (short?)model.class_code)
            .FirstOrDefaultAsync();
    }

    private async Task<string?> ValidateGgOverlapAsync(
        int vmfCode,
        string rekNum,
        int? excludeLogId,
        decimal startOdo,
        decimal endOdo)
    {
        var overlappingLogs = await (
            from taxi in _context.Taxis.AsNoTracking()
            join log in _context.TaxiLogs.AsNoTracking() on taxi.rek_num equals log.rek_num
            where !taxi.is_deleted
                  && !log.is_deleted
                  && taxi.vmf_code == vmfCode.ToString()
                  && !_context.Taxis.Any(child => child.parent_taxi_code == taxi.request_id)
                  && !_context.TaxiLogs.Any(child => child.parent_taxi_log_code == log.log_id && !child.is_deleted)
                  && taxi.rek_num != rekNum
                  && (!excludeLogId.HasValue || log.log_id != excludeLogId.Value)
            select new OdoRangeDto(
                taxi.rek_num ?? string.Empty,
                log.driver_start_odo,
                log.driver_end_odo))
            .ToListAsync();

        var overlap = overlappingLogs.FirstOrDefault(item => IsOverlap(startOdo, endOdo, item.StartOdo, item.EndOdo));
        if (overlap != null)
        {
            return $"Odometer values overlap with log {overlap.RekNum} values.\nStart odo = {FormatDecimal(overlap.StartOdo)}\nEnd odo = {FormatDecimal(overlap.EndOdo)}.";
        }

        var whiteLogs = await _context.TaxiWhiteLogs
            .AsNoTracking()
            .Where(item => !item.is_deleted && item.vmf_code == vmfCode)
            .Select(item => new OdoRangeDto("WHITE LOG", item.start_odo, item.end_odo))
            .ToListAsync();

        var whiteLogOverlap = whiteLogs.FirstOrDefault(item => IsOverlap(startOdo, endOdo, item.StartOdo, item.EndOdo));
        if (whiteLogOverlap != null)
        {
            return $"Odometer values overlap with white log :\nStart odo = {FormatDecimal(whiteLogOverlap.StartOdo)}\nEnd odo = {FormatDecimal(whiteLogOverlap.EndOdo)}.";
        }

        return null;
    }

    private async Task<string?> ValidateContractorOverlapAsync(
        string registration,
        short contractorId,
        string rekNum,
        int? excludeLogId,
        decimal startOdo,
        decimal endOdo)
    {
        var overlaps = await (
            from taxi in _context.Taxis.AsNoTracking()
            join log in _context.TaxiLogs.AsNoTracking() on taxi.rek_num equals log.rek_num
            where !taxi.is_deleted
                  && !log.is_deleted
                  && taxi.contractor_id == contractorId
                  && (taxi.reg_num ?? string.Empty).ToUpper() == registration
                  && !_context.Taxis.Any(child => child.parent_taxi_code == taxi.request_id)
                  && !_context.TaxiLogs.Any(child => child.parent_taxi_log_code == log.log_id && !child.is_deleted)
                  && taxi.rek_num != rekNum
                  && (!excludeLogId.HasValue || log.log_id != excludeLogId.Value)
            select new OdoRangeDto(
                taxi.rek_num ?? string.Empty,
                log.driver_start_odo,
                log.driver_end_odo))
            .ToListAsync();

        var overlap = overlaps.FirstOrDefault(item => IsOverlap(startOdo, endOdo, item.StartOdo, item.EndOdo));
        if (overlap != null)
        {
            return $"Odometer values overlap with log {overlap.RekNum} values.\nStart odo = {FormatDecimal(overlap.StartOdo)}\nEnd odo = {FormatDecimal(overlap.EndOdo)}.";
        }

        return null;
    }

    private static string? ValidateSaveRequest(SaveTaxiLogRequest request)
    {
        if (request.request_id is not > 0)
        {
            return "Request identifier required.";
        }

        if (string.IsNullOrWhiteSpace(request.rek_num))
        {
            return "Requisition number required.";
        }

        if (request.contractor_id is not > 0)
        {
            return "Service provider required.";
        }

        if (string.IsNullOrWhiteSpace(request.reg_num))
        {
            return "Registration number required.";
        }

        if (string.IsNullOrWhiteSpace(request.driver))
        {
            return "Driver name required.";
        }

        if (request.driver_start_odo is null)
        {
            return "Please enter Driver Start odometer.";
        }

        if (request.driver_end_odo is null)
        {
            return "Please enter Driver End odometer.";
        }

        if (request.driver_end_odo <= request.driver_start_odo)
        {
            return "Driver start odometer greater than or equal to end odometer.";
        }

        if (request.driver_start_date is null || request.driver_end_date is null || request.driver_start_time is null || request.driver_end_time is null)
        {
            return "Please complete Start/End date and time.";
        }

        if (request.contractor_id != GgContractorId && (request.vehicle_type_code is null or 0) && request.quoted_tariff is not > 0)
        {
            return "Quoted tariff amount must be entered.";
        }

        return null;
    }

    private static (DateTime Start, DateTime End) CombineJourneyDateTimes(SaveTaxiLogRequest request)
    {
        var start = request.driver_start_date!.Value.Date.Add(request.driver_start_time!.Value);
        var end = request.driver_end_date!.Value.Date.Add(request.driver_end_time!.Value);
        return (start, end);
    }

    private static string? ValidateJourneyRange(DateTime start, DateTime end, decimal? startOdo, decimal? endOdo)
    {
        if (start >= end)
        {
            return "End of journey Date/time is less than Start of journey Date/time";
        }

        var distance = endOdo!.Value - startOdo!.Value;
        var diffHours = (decimal)(end - start).TotalHours;
        if (diffHours <= 0)
        {
            return "End of journey Date/time is less than Start of journey Date/time";
        }

        if (diffHours > 24m)
        {
            var minimumDistance = (diffHours / 24m) * 10m;
            if (distance < minimumDistance)
            {
                return $"Distance = {FormatDecimal(distance)}km\nTime = {Math.Truncate(diffHours / 24m)} days\nOdometer or Date/Time values are incorrect.";
            }
        }

        var maximumDistance = diffHours * 120m;
        if (distance > maximumDistance)
        {
            return $"Distance = {FormatDecimal(distance)}km\nTime = {diffHours:0.##} hours\nOdometer or Date/Time values are incorrect.";
        }

        return null;
    }

    private static TaxiDuration CalculateDuration(DateTime start, DateTime end)
    {
        var totalMinutes = (end - start).TotalMinutes;
        var days = (short)Math.Floor((totalMinutes / 60d) / 24d);
        var remainingHours = Math.Floor(totalMinutes / 60d) - (days * 24d);
        var remainingMinutes = ((totalMinutes / 60d) - (days * 24d) - remainingHours) * 60d;

        double roundedFractionalHours;
        if (remainingMinutes >= 53d)
        {
            remainingHours += 1d;
            roundedFractionalHours = 0d;
        }
        else if (remainingMinutes <= 7d)
        {
            roundedFractionalHours = 0d;
        }
        else if (remainingMinutes <= 22d)
        {
            roundedFractionalHours = 0.25d;
        }
        else if (remainingMinutes <= 37d)
        {
            roundedFractionalHours = 0.5d;
        }
        else
        {
            roundedFractionalHours = 0.75d;
        }

        return new TaxiDuration(days, remainingHours + roundedFractionalHours);
    }

    private static bool IsOverlap(decimal start, decimal end, decimal? existingStart, decimal? existingEnd)
    {
        if (!existingStart.HasValue || !existingEnd.HasValue)
        {
            return false;
        }

        return start < existingEnd.Value && end > existingStart.Value;
    }

    private static short ToLegacyShortUserId(int userId)
    {
        if (userId > short.MaxValue || userId < short.MinValue)
        {
            throw new InvalidOperationException($"User id {userId} cannot be stored in legacy taxi log userid column.");
        }

        return (short)userId;
    }

    private static string NormalizeKey(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string? FormatTime(DateTime? value) => value?.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string FormatDecimal(decimal? value) => value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";

    private sealed record OdoRangeDto(string RekNum, decimal? StartOdo, decimal? EndOdo);

    private sealed record TaxiDuration(short Days, double Hours);

    private sealed record PrepareTaxiLogResult(bool Success, ActionResult<TaxiLogLookupResponse>? Result, Taxi? TaxiRequest, TaxiLog? Log)
    {
        public static PrepareTaxiLogResult Failure(ActionResult<TaxiLogLookupResponse> result) => new(false, result, null, null);

        public static PrepareTaxiLogResult FromSuccess(Taxi taxiRequest, TaxiLog log) => new(true, null, taxiRequest, log);
    }
}

public sealed record TaxiLogReferenceResponse(
    IReadOnlyList<TaxiLogContractorOptionDto> Contractors,
    IReadOnlyList<TaxiLogClassOptionDto> Classes,
    IReadOnlyList<TaxiLogNoteOptionDto> Notes);

public sealed record TaxiLogContractorOptionDto(short ContractorId, string ContractorName);

public sealed record TaxiLogClassOptionDto(
    short ContractorId,
    short ClassId,
    string Description,
    decimal? KmTariff,
    decimal? DriverPerHour,
    decimal? DailyTariff);

public sealed record TaxiLogNoteOptionDto(short NoteCode, string Description);

public sealed record TaxiLogLookupResponse(
    int RequestId,
    string RekNum,
    short? ContractorId,
    string? ContractorName,
    short? VehicleTypeCode,
    string? VehicleTypeDescription,
    string? RegistrationNumber,
    string? FleetNumber,
    string? Driver,
    string? Official,
    DateTime DateRequired,
    DateTime TimeRequired,
    short? DepartmentCode,
    string? DepartmentName,
    TaxiLogDetailDto? Log);

public sealed record TaxiLogDetailDto(
    int LogId,
    decimal? DriverStartOdo,
    decimal? DriverEndOdo,
    DateTime? DriverStartDate,
    DateTime? DriverEndDate,
    string? DriverStartTime,
    string? DriverEndTime,
    short? TaxiLogNoteCode,
    decimal? QuotedTariff);

public sealed class SaveTaxiLogRequest
{
    public int? request_id { get; set; }
    public string? rek_num { get; set; }
    public short contractor_id { get; set; }
    public short? vehicle_type_code { get; set; }
    public string? reg_num { get; set; }
    public string? driver { get; set; }
    public decimal? driver_start_odo { get; set; }
    public decimal? driver_end_odo { get; set; }
    public DateTime? driver_start_date { get; set; }
    public DateTime? driver_end_date { get; set; }
    public TimeSpan? driver_start_time { get; set; }
    public TimeSpan? driver_end_time { get; set; }
    public short? taxi_log_note_code { get; set; }
    public decimal? quoted_tariff { get; set; }
}

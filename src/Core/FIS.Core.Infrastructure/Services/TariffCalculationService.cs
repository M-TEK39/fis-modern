using FIS.Core.Application.Interfaces;
using FIS.Core.Application.Services.Billing;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services;

public class TariffCalculationService : ITariffCalculationService
{
    private readonly ILogger<TariffCalculationService> _logger;
    private readonly FisDbContext _context;
    private readonly IContractRepository _contractRepository;
    private readonly ITariffRepository _tariffRepository;
    private readonly IVehicleTariffRepository _vehicleTariffRepository;
    private readonly ILeaseTariffRepository _leaseTariffRepository;
    private readonly IFuelTariffRepository _fuelTariffRepository;

    public TariffCalculationService(
        ILogger<TariffCalculationService> logger,
        FisDbContext context,
        IContractRepository contractRepository,
        ITariffRepository tariffRepository,
        IVehicleTariffRepository vehicleTariffRepository,
        ILeaseTariffRepository leaseTariffRepository,
        IFuelTariffRepository fuelTariffRepository)
    {
        _logger = logger;
        _context = context;
        _contractRepository = contractRepository;
        _tariffRepository = tariffRepository;
        _vehicleTariffRepository = vehicleTariffRepository;
        _leaseTariffRepository = leaseTariffRepository;
        _fuelTariffRepository = fuelTariffRepository;
    }

    public async Task<TariffResult> GetVehicleTariffAsync(int contractCode, DateTime checkDate, TariffType tariffType)
    {
        var contract = await _contractRepository.GetByIdAsync(contractCode);
        if (contract is null || contract.Vehicle is null || contract.is_deleted)
        {
            return TariffResult.Error(TariffStatus.NoMatch, $"Contract {contractCode} was not found.");
        }

        return await GetVehicleTariffAsync(
            contract.start_date,
            contract.end_date ?? checkDate,
            contract.start_odometer,
            contract.end_odometer ?? contract.start_odometer,
            contract.vmf_code,
            contract.site_code,
            contract.Site?.Depatrment_code ?? 0,
            contract.contract_type ?? "A",
            checkDate,
            tariffType);
    }

    public async Task<TariffResult> GetVehicleTariffAsync(
        DateTime startDate,
        DateTime endDate,
        int startOdometer,
        int endOdometer,
        int vmfCode,
        short siteCode,
        int departmentCode,
        string contractType,
        DateTime checkDate,
        TariffType tariffType)
    {
        var vehicle = await _context.Vehicles
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.vmf_code == vmfCode && !v.is_deleted);

        if (vehicle is null)
        {
            return TariffResult.Error(TariffStatus.NoMatch, $"Vehicle {vmfCode} was not found.");
        }

        if (IsGGMTInternal(siteCode, departmentCode) || IsVehicleMissing(vehicle.vehicle_status_code))
        {
            return TariffResult.Success(0m, TariffSource.SpecialRule);
        }

        if (string.Equals(contractType, "F", StringComparison.OrdinalIgnoreCase))
        {
            return TariffResult.Success(0m, TariffSource.SpecialRule);
        }

        if (string.Equals(contractType, "L", StringComparison.OrdinalIgnoreCase))
        {
            var leaseTariff = await GetLeaseTariffAsync(vmfCode, checkDate);
            if (leaseTariff is null)
            {
                return TariffResult.Error(TariffStatus.NoMatch, $"No lease tariff configured for vehicle {vmfCode}.");
            }

            if (tariffType == TariffType.Kilos)
            {
                return TariffResult.Success(0m, TariffSource.Lease);
            }

            var leaseAmount = IsLeaseProRated(startDate) ? 0m : leaseTariff.fixed_tariff;
            return TariffResult.Success(leaseAmount, TariffSource.Lease);
        }

        var yearManufactured = vehicle.year_manufactured;
        if (!yearManufactured.HasValue)
        {
            return TariffResult.Error(TariffStatus.YearNotFound, $"Vehicle {vmfCode} has no year_manufactured value.");
        }

        var tariffSystem = DetermineTariffSystem(yearManufactured.Value, startDate);
        if (tariffSystem == TariffSystem.Modern)
        {
            var parameterYear = (short)(checkDate.Month >= 4 ? checkDate.Year + 1 : checkDate.Year);
            var vehicleTariff = await GetConfiguredVehicleTariffAsync(vmfCode, parameterYear, checkDate);
            if (vehicleTariff is null)
            {
                return TariffResult.Error(TariffStatus.NoMatch, $"No modern tariff found for vehicle {vmfCode}.");
            }

            var validation = ValidateTariff(vehicleTariff);
            if (!validation.IsValid)
            {
                return TariffResult.Error(validation.Status, string.Join("; ", validation.Messages));
            }

            var amount = tariffType == TariffType.Fixed
                ? GetModernFixedTariff(vehicleTariff, contractType)
                : GetModernKilometerTariff(vehicleTariff, contractType);

            return TariffResult.Success(amount, TariffSource.Modern);
        }

        if (vehicle.Model is null)
        {
            return TariffResult.Error(TariffStatus.NoMatch, $"Vehicle {vmfCode} has no class code linked from model.");
        }

        var classCode = vehicle.Model.class_code;
        var legacyAmount = tariffType == TariffType.Fixed
            ? await GetLegacyFixedTariffAsync(classCode, yearManufactured.Value, checkDate, contractType)
            : await GetLegacyKilometerTariffAsync(classCode, yearManufactured.Value, checkDate);

        if (legacyAmount < 0)
        {
            return TariffResult.Error(TariffStatus.NoMatch, $"No legacy tariff found for class {classCode}, year {yearManufactured}.");
        }

        return TariffResult.Success(legacyAmount, TariffSource.Legacy);
    }

    public async Task<decimal> GetLegacyFixedTariffAsync(int vehicleClassCode, int yearManufactured, DateTime effectiveDate, string contractType)
    {
        var tariff = await _tariffRepository.GetTariffAsync((short)vehicleClassCode, (short)yearManufactured, effectiveDate);
        if (tariff is null)
        {
            return -2m;
        }

        return contractType.ToUpperInvariant() switch
        {
            "A" => Math.Round((tariff.monthly_fixed_amount * 12m) / 365m, 6),
            "B" => tariff.daily_fixed_amount ?? 0m,
            "C" => tariff.hourly_fixed_amount ?? 0m,
            "F" => 0m,
            _ => Math.Round((tariff.monthly_fixed_amount * 12m) / 365m, 6)
        };
    }

    public async Task<decimal> GetLegacyKilometerTariffAsync(int vehicleClassCode, int yearManufactured, DateTime effectiveDate)
    {
        var tariff = await _tariffRepository.GetTariffAsync((short)vehicleClassCode, (short)yearManufactured, effectiveDate);
        return tariff?.monthly_odo_amount ?? -2m;
    }

    public async Task<VehicleTariff?> GetConfiguredVehicleTariffAsync(int vmfCode, int parameterYear, DateTime effectiveDate)
    {
        var activeTariff = await _context.VehicleTariffs
            .Where(t => t.vmf_code == vmfCode && !t.is_deleted)
            .Where(t => t.parameter_year == parameterYear)
            .Where(t => t.start_date <= effectiveDate)
            .Where(t => t.end_date == null || t.end_date >= effectiveDate)
            .OrderByDescending(t => t.start_date)
            .FirstOrDefaultAsync();

        if (activeTariff is not null)
        {
            return activeTariff;
        }

        return await _vehicleTariffRepository.GetCurrentTariffForVehicleAsync(vmfCode);
    }

    public decimal GetModernFixedTariff(VehicleTariff vehicleTariff, string contractType)
    {
        return contractType.ToUpperInvariant() switch
        {
            "A" => vehicleTariff.vehicle_fixed_daily_tariff
                ?? Math.Round(((vehicleTariff.vehicle_fixed_tariff ?? 0m) * 12m) / 365m, 6),
            "B" => vehicleTariff.vehicle_fixed_tariff_pool ?? vehicleTariff.vehicle_fixed_tariff ?? 0m,
            "C" => (vehicleTariff.vehicle_fixed_tariff_pool ?? vehicleTariff.vehicle_fixed_tariff ?? 0m) / 8m,
            _ => vehicleTariff.vehicle_fixed_tariff ?? 0m
        };
    }

    public decimal GetModernKilometerTariff(VehicleTariff vehicleTariff, string contractType)
    {
        if (string.Equals(contractType, "L", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return vehicleTariff.vehicle_kilometer_tariff ?? 0m;
    }

    public async Task<LeaseTariff?> GetLeaseTariffAsync(int vmfCode, DateTime effectiveDate)
    {
        return await _leaseTariffRepository.GetByVehicleAndDateAsync(vmfCode, effectiveDate);
    }

    public bool IsLeaseProRated(DateTime contractStartDate)
    {
        return contractStartDate.Day >= 16;
    }

    public decimal CalculateLeaseExcessCharge(LeaseTariff leaseTariff, int actualKilometers, int contractedKilometers)
    {
        var excess = Math.Max(0, actualKilometers - contractedKilometers);
        if (excess <= 0)
        {
            return 0m;
        }

        var effectiveDays = Math.Max(1d, (leaseTariff.end_date - leaseTariff.start_date).TotalDays);
        var perKmRate = leaseTariff.fixed_tariff / (decimal)Math.Max(1d, effectiveDays * 120d);
        return Math.Round(excess * perKmRate, 2);
    }

    public async Task<decimal> GetFuelTariffAsync(int fuelTypeCode, DateTime effectiveDate)
    {
        var currentFuelTariff = await _fuelTariffRepository.GetCurrentTariffAsync((short)fuelTypeCode);
        if (currentFuelTariff is not null)
        {
            return currentFuelTariff.fuel_tariff;
        }

        var tariffAtDate = await _context.FuelTariffs
            .Where(t => !t.is_deleted && t.fuel_type_code == fuelTypeCode)
            .Where(t => t.start_date <= effectiveDate)
            .Where(t => t.end_date == null || t.end_date >= effectiveDate)
            .OrderByDescending(t => t.start_date)
            .FirstOrDefaultAsync();

        return tariffAtDate?.fuel_tariff ?? 0m;
    }

    public bool IsGGMTInternal(short siteCode, int departmentCode)
    {
        return siteCode is 1621 or 1622 || departmentCode == 147;
    }

    public bool IsVehicleMissing(int vehicleStatusCode)
    {
        return vehicleStatusCode == 11;
    }

    public TariffSystem DetermineTariffSystem(int yearManufactured, DateTime contractStartDate)
    {
        if (yearManufactured >= 2008 && contractStartDate.Date >= new DateTime(2009, 4, 1))
        {
            return TariffSystem.Modern;
        }

        return TariffSystem.Legacy;
    }

    public async Task<List<BatchTariffResult>> CalculateBatchTariffsAsync(List<BatchTariffRequest> requests)
    {
        var results = new List<BatchTariffResult>(requests.Count);
        foreach (var request in requests)
        {
            var requestTariffType = request.TariffType == FIS.Core.Domain.Enums.TariffType.Kilos
                ? TariffType.Kilos
                : TariffType.Fixed;

            var result = await GetVehicleTariffAsync(
                request.ContractCode,
                request.CheckDate,
                requestTariffType);

            results.Add(new BatchTariffResult
            {
                ContractCode = request.ContractCode,
                CheckDate = request.CheckDate,
                TariffType = request.TariffType,
                Amount = result.Amount,
                Status = MapToDomainTariffStatus(result.Status),
                Source = MapToDomainTariffSource(result.Source),
                Message = result.Message,
                IsSuccess = result.Status == TariffStatus.Valid,
                Reference = request.Reference,
                VmfCode = request.VmfCode
            });
        }

        return results;
    }

    public async Task<MonthlyBillingResult> CalculateMonthlyBillingAsync(int siteCode, int departmentCode, DateTime billingPeriodStart, DateTime billingPeriodEnd)
    {
        var result = new MonthlyBillingResult
        {
            SiteCode = siteCode,
            DepartmentCode = departmentCode,
            BillingPeriodStart = billingPeriodStart,
            BillingPeriodEnd = billingPeriodEnd,
            GrandTotal = 0m,
            ContractBillings = new List<ContractBilling>()
        };

        var contracts = await _context.Contracts
            .Where(c => !c.is_deleted && c.site_code == siteCode && c.still_current == "Y")
            .Include(c => c.Vehicle)
            .ToListAsync();

        foreach (var contract in contracts.Where(c => (c.Site?.Depatrment_code ?? 0) == departmentCode))
        {
            var fixedResult = await GetVehicleTariffAsync(contract.contract_code, billingPeriodEnd, TariffType.Fixed);
            var kiloResult = await GetVehicleTariffAsync(contract.contract_code, billingPeriodEnd, TariffType.Kilos);
            var quantityDays = Math.Max(0, (billingPeriodEnd.Date - billingPeriodStart.Date).Days);

            var fixedAmount = Math.Round(quantityDays * fixedResult.Amount, 2);
            var variableAmount = 0m;
            if (contract.end_odometer.HasValue && contract.end_odometer.Value > contract.start_odometer)
            {
                variableAmount = Math.Round((contract.end_odometer.Value - contract.start_odometer) * kiloResult.Amount, 2);
            }

            var total = fixedAmount + variableAmount;
            result.ContractBillings.Add(new ContractBilling
            {
                ContractCode = contract.contract_code,
                VmfCode = contract.vmf_code,
                ContractType = contract.contract_type ?? string.Empty,
                TotalFixedCharges = fixedAmount,
                TotalKilometerCharges = variableAmount,
                TotalExcessCharges = 0m,
                TotalAmount = total,
                BillingItems = new List<FIS.Core.Domain.Entities.Financial.BillingItem>
                {
                    new()
                    {
                        Date = billingPeriodEnd.Date,
                        Description = "Fixed tariff charge",
                        Amount = fixedAmount,
                        TariffType = FIS.Core.Domain.Enums.TariffType.Fixed,
                        Source = MapToDomainTariffSource(fixedResult.Source),
                        Quantity = quantityDays,
                        Rate = fixedResult.Amount
                    },
                    new()
                    {
                        Date = billingPeriodEnd.Date,
                        Description = "Kilometer tariff charge",
                        Amount = variableAmount,
                        TariffType = FIS.Core.Domain.Enums.TariffType.Kilos,
                        Source = MapToDomainTariffSource(kiloResult.Source),
                        Quantity = contract.end_odometer.HasValue ? Math.Max(0, contract.end_odometer.Value - contract.start_odometer) : 0,
                        Rate = kiloResult.Amount
                    }
                }
            });

            result.TotalFixedCharges += fixedAmount;
            result.TotalKilometerCharges += variableAmount;
            result.GrandTotal += total;
        }

        return result;
    }

    public async Task<FinancialReconciliation> ReconcileTariffsAsync(DateTime periodStart, DateTime periodEnd, int? siteCode = null, int? departmentCode = null)
    {
        await Task.CompletedTask;
        return new FinancialReconciliation
        {
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            SiteCode = siteCode,
            DepartmentCode = departmentCode,
            ReconciliationDate = DateTime.UtcNow,
            Discrepancies = new List<TariffDiscrepancy>(),
            TotalDiscrepancies = 0,
            TotalDifference = 0m,
            RequiresAction = false
        };
    }

    public TariffValidationResult ValidateTariff(VehicleTariff vehicleTariff)
    {
        var errors = new List<string>();

        if (!vehicleTariff.overhead_kilometer_amount.HasValue)
        {
            errors.Add("Missing overhead_kilometer_amount.");
        }

        if (!vehicleTariff.maintenance_kilometer_amount.HasValue)
        {
            errors.Add("Missing maintenance_kilometer_amount.");
        }

        if (errors.Count > 0)
        {
            return TariffValidationResult.Invalid(TariffStatus.Incomplete, errors.ToArray());
        }

        return TariffValidationResult.Valid();
    }

    private static FIS.Core.Domain.Enums.TariffStatus MapToDomainTariffStatus(TariffStatus status)
    {
        return status switch
        {
            TariffStatus.Valid => FIS.Core.Domain.Enums.TariffStatus.Valid,
            TariffStatus.YearNotFound => FIS.Core.Domain.Enums.TariffStatus.YearNotFound,
            TariffStatus.NoMatch => FIS.Core.Domain.Enums.TariffStatus.NoMatch,
            TariffStatus.Incomplete => FIS.Core.Domain.Enums.TariffStatus.Incomplete,
            _ => FIS.Core.Domain.Enums.TariffStatus.NoMatch
        };
    }

    private static FIS.Core.Domain.Enums.TariffSource MapToDomainTariffSource(TariffSource source)
    {
        return source switch
        {
            TariffSource.None => FIS.Core.Domain.Enums.TariffSource.None,
            TariffSource.Legacy => FIS.Core.Domain.Enums.TariffSource.Legacy,
            TariffSource.Modern => FIS.Core.Domain.Enums.TariffSource.Modern,
            TariffSource.Lease => FIS.Core.Domain.Enums.TariffSource.Lease,
            TariffSource.SpecialRule => FIS.Core.Domain.Enums.TariffSource.SpecialRule,
            _ => FIS.Core.Domain.Enums.TariffSource.None
        };
    }
}

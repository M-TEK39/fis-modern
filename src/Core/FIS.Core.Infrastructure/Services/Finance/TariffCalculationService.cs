using System.Data;
using System.Data.Common;
using System.Globalization;
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
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ITariffRepository _tariffRepository;
    private readonly IVehicleTariffRepository _vehicleTariffRepository;
    private readonly ILeaseTariffRepository _leaseTariffRepository;
    private readonly IFuelTariffRepository _fuelTariffRepository;

    public TariffCalculationService(
        ILogger<TariffCalculationService> logger,
        FisDbContext context,
        IContractRepository contractRepository,
        IVehicleRepository vehicleRepository,
        ITariffRepository tariffRepository,
        IVehicleTariffRepository vehicleTariffRepository,
        ILeaseTariffRepository leaseTariffRepository,
        IFuelTariffRepository fuelTariffRepository
    )
    {
        _logger = logger;
        _context = context;
        _contractRepository = contractRepository;
        _vehicleRepository = vehicleRepository;
        _tariffRepository = tariffRepository;
        _vehicleTariffRepository = vehicleTariffRepository;
        _leaseTariffRepository = leaseTariffRepository;
        _fuelTariffRepository = fuelTariffRepository;
    }

    public async Task<TariffResult> GetVehicleTariffAsync(
        int contractCode,
        DateTime checkDate,
        TariffType tariffType
    )
    {
        // The legacy billing function is the source of truth for this
        // contract-code overload. It contains the special internal-site,
        // cancelled-contract, lease, fiscal-year, and incomplete-tariff rules
        // that cannot be safely reconstructed from a partial EF projection.
        // Only use the existing compatibility calculation when the function
        // is genuinely absent from the connected database.
        var legacyResult = await TryGetLegacyVehicleTariffAsync(
            contractCode,
            checkDate,
            tariffType
        );
        if (legacyResult is not null)
        {
            return legacyResult;
        }

        var contract = await _contractRepository.GetByIdAsync(contractCode);
        if (contract is null || contract.Vehicle is null || contract.is_deleted)
        {
            return TariffResult.Error(
                TariffStatus.NoMatch,
                $"Contract {contractCode} was not found."
            );
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
            tariffType
        );
    }

    public async Task<string?> GetConfiguredContractTypeAsync(int vmfCode, DateTime checkDate)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var hasConfiguredTariffFunction = false;
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.CommandText = """
                    SELECT CASE WHEN EXISTS
                    (
                        SELECT 1
                        FROM [sys].[objects] AS [o]
                        INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [o].[schema_id]
                        WHERE [s].[name] = N'fin'
                          AND [o].[name] = N'GetVehicleConfiguredTariff'
                          AND [o].[type] IN (N'IF', N'TF')
                    ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
                    """;
                hasConfiguredTariffFunction = Convert.ToBoolean(
                    await existsCommand.ExecuteScalarAsync()
                );
            }

            if (hasConfiguredTariffFunction)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                SELECT TOP (1) [configured].[contract_type]
                FROM [fin].[GetVehicleConfiguredTariff](@vmfCode, @checkDate) AS [configured]
                WHERE [configured].[contract_type] IS NOT NULL
                ORDER BY [configured].[have_valid_tariff] DESC, [configured].[contract_type]
                """;
                AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                AddParameter(command, "@checkDate", DbType.DateTime2, checkDate.Date);
                var value = await command.ExecuteScalarAsync();
                var configured = value is null or DBNull
                    ? null
                    : Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured;
                }
            }

            // Very old restored databases may have the mapping tables but not
            // the table-valued helper. Resolve the same vehicle-source/type
            // mapping directly from those legacy tables rather than inventing
            // a modern-only H contract type.
            await using (var mappingExistsCommand = connection.CreateCommand())
            {
                mappingExistsCommand.CommandText = """
                    SELECT CASE WHEN OBJECT_ID(N'dbo.vehicle_master', N'U') IS NOT NULL
                                      AND OBJECT_ID(N'dbo.Contract_Type_Group_Mapping', N'U') IS NOT NULL
                                      AND OBJECT_ID(N'dbo.Contract_Type_Map', N'U') IS NOT NULL
                                 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
                    """;
                if (!Convert.ToBoolean(await mappingExistsCommand.ExecuteScalarAsync()))
                {
                    return null;
                }
            }

            await using var mappingCommand = connection.CreateCommand();
            mappingCommand.CommandText = """
                SELECT TOP (1) [ctm].[contract_type]
                FROM [dbo].[vehicle_master] AS [vm]
                INNER JOIN [dbo].[Contract_Type_Group_Mapping] AS [ctgm]
                    ON [ctgm].[type_code] = [vm].[type_code]
                   AND ([vm].[vs_code] IS NULL OR [ctgm].[vs_code] = [vm].[vs_code])
                INNER JOIN [dbo].[Contract_Type_Map] AS [ctm]
                    ON [ctm].[ctg_code] = [ctgm].[ctg_code]
                WHERE [vm].[vmf_code] = @vmfCode
                  AND [ctm].[contract_type] IN (N'A', N'B', N'C', N'L')
                ORDER BY [ctgm].[ctg_code], [ctm].[contract_type]
                """;
            AddParameter(mappingCommand, "@vmfCode", DbType.Int32, vmfCode);
            var mapped = await mappingCommand.ExecuteScalarAsync();
            return mapped is null or DBNull
                ? null
                : Convert.ToString(mapped, CultureInfo.InvariantCulture)?.Trim().ToUpperInvariant();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<TariffResult?> TryGetLegacyVehicleTariffAsync(
        int contractCode,
        DateTime checkDate,
        TariffType tariffType
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
            await using (var existsCommand = connection.CreateCommand())
            {
                existsCommand.CommandText = "SELECT OBJECT_ID(N'dbo.GetVehicleTariff', N'FN')";
                var objectId = await existsCommand.ExecuteScalarAsync();
                if (objectId is null or DBNull)
                {
                    return null;
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT dbo.GetVehicleTariff(@contractCode, @checkDate, @tariffType)";
            AddParameter(command, "@contractCode", DbType.Int32, contractCode);
            AddParameter(command, "@checkDate", DbType.DateTime2, checkDate);
            AddParameter(
                command,
                "@tariffType",
                DbType.String,
                tariffType == TariffType.Kilos ? "kilos" : "fixed"
            );

            var raw = await command.ExecuteScalarAsync();
            if (raw is null or DBNull)
            {
                return TariffResult.Error(
                    TariffStatus.NoMatch,
                    "The legacy vehicle-tariff function returned no value."
                );
            }

            var amount = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return amount switch
            {
                -1m => TariffResult.Error(
                    TariffStatus.YearNotFound,
                    "The legacy tariff function could not resolve the vehicle year."
                ),
                -2m => TariffResult.Error(
                    TariffStatus.NoMatch,
                    "The legacy tariff function found no effective tariff."
                ),
                -3m => TariffResult.Error(
                    TariffStatus.Incomplete,
                    "The legacy tariff function found an incomplete vehicle tariff."
                ),
                0m => TariffResult.Success(0m, TariffSource.SpecialRule),
                _ => TariffResult.Success(amount, TariffSource.Legacy),
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
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
        TariffType tariffType
    )
    {
        // The legacy vehicle_master table does not contain the modern audit
        // columns mapped by EF. Resolve the vehicle through the guarded
        // compatibility repository so billing works against both schemas and
        // the model class code needed by legacy tariffs is preserved.
        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);

        if (vehicle is null)
        {
            return TariffResult.Error(TariffStatus.NoMatch, $"Vehicle {vmfCode} was not found.");
        }

        if (
            IsGGMTInternal(siteCode, departmentCode)
            || IsVehicleMissing(vehicle.vehicle_status_code)
        )
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
                return TariffResult.Error(
                    TariffStatus.NoMatch,
                    $"No lease tariff configured for vehicle {vmfCode}."
                );
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
            return TariffResult.Error(
                TariffStatus.YearNotFound,
                $"Vehicle {vmfCode} has no year_manufactured value."
            );
        }

        var tariffSystem = DetermineTariffSystem(yearManufactured.Value, startDate);
        if (tariffSystem == TariffSystem.Modern)
        {
            var parameterYear = (short)(checkDate.Month >= 4 ? checkDate.Year + 1 : checkDate.Year);
            var vehicleTariff = await GetConfiguredVehicleTariffAsync(
                vmfCode,
                parameterYear,
                checkDate
            );
            if (vehicleTariff is null)
            {
                return TariffResult.Error(
                    TariffStatus.NoMatch,
                    $"No modern tariff found for vehicle {vmfCode}."
                );
            }

            var validation = ValidateTariff(vehicleTariff);
            if (!validation.IsValid)
            {
                return TariffResult.Error(
                    validation.Status,
                    string.Join("; ", validation.Messages)
                );
            }

            var amount =
                tariffType == TariffType.Fixed
                    ? GetModernFixedTariff(vehicleTariff, contractType)
                    : GetModernKilometerTariff(vehicleTariff, contractType);

            return TariffResult.Success(amount, TariffSource.Modern);
        }

        if (vehicle.Model is null)
        {
            return TariffResult.Error(
                TariffStatus.NoMatch,
                $"Vehicle {vmfCode} has no class code linked from model."
            );
        }

        var classCode = vehicle.Model.class_code;
        var legacyAmount =
            tariffType == TariffType.Fixed
                ? await GetLegacyFixedTariffAsync(
                    classCode,
                    yearManufactured.Value,
                    checkDate,
                    contractType
                )
                : await GetLegacyKilometerTariffAsync(classCode, yearManufactured.Value, checkDate);

        if (legacyAmount < 0)
        {
            return TariffResult.Error(
                TariffStatus.NoMatch,
                $"No legacy tariff found for class {classCode}, year {yearManufactured}."
            );
        }

        return TariffResult.Success(legacyAmount, TariffSource.Legacy);
    }

    public async Task<decimal> GetLegacyFixedTariffAsync(
        int vehicleClassCode,
        int yearManufactured,
        DateTime effectiveDate,
        string contractType
    )
    {
        var tariff = await _tariffRepository.GetTariffAsync(
            (short)vehicleClassCode,
            (short)yearManufactured,
            effectiveDate
        );
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
            _ => Math.Round((tariff.monthly_fixed_amount * 12m) / 365m, 6),
        };
    }

    public async Task<decimal> GetLegacyKilometerTariffAsync(
        int vehicleClassCode,
        int yearManufactured,
        DateTime effectiveDate
    )
    {
        var tariff = await _tariffRepository.GetTariffAsync(
            (short)vehicleClassCode,
            (short)yearManufactured,
            effectiveDate
        );
        return tariff?.monthly_odo_amount ?? -2m;
    }

    public async Task<VehicleTariff?> GetConfiguredVehicleTariffAsync(
        int vmfCode,
        int parameterYear,
        DateTime effectiveDate
    )
    {
        var activeTariff = await _vehicleTariffRepository.GetTariffForVehicleAsync(
            vmfCode,
            parameterYear,
            effectiveDate
        );

        // A tariff is valid for one capture year. Falling back to whichever
        // row has no end_date silently reuses stale rates (for example a 2017
        // tariff for a 2026 contract) and was the source of the reported
        // revenue leakage. A missing or stale row must remain a visible tariff
        // error so Finance can capture/release the new year's tariff, even if
        // the user deliberately recaptures the same amount.
        return activeTariff is not null
            && activeTariff.start_date.Date >= effectiveDate.Date.AddYears(-1)
            ? activeTariff
            : null;
    }

    public decimal GetModernFixedTariff(VehicleTariff vehicleTariff, string contractType)
    {
        return contractType.ToUpperInvariant() switch
        {
            "A" => vehicleTariff.vehicle_fixed_daily_tariff
                ?? Math.Round(((vehicleTariff.vehicle_fixed_tariff ?? 0m) * 12m) / 365m, 6),
            "B" => vehicleTariff.vehicle_fixed_tariff_pool
                ?? vehicleTariff.vehicle_fixed_tariff
                ?? 0m,
            "C" => (
                vehicleTariff.vehicle_fixed_tariff_pool ?? vehicleTariff.vehicle_fixed_tariff ?? 0m
            ) / 8m,
            _ => vehicleTariff.vehicle_fixed_tariff ?? 0m,
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

    public decimal CalculateLeaseExcessCharge(
        LeaseTariff leaseTariff,
        int actualKilometers,
        int contractedKilometers
    )
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
        var currentFuelTariff = await _fuelTariffRepository.GetCurrentTariffAsync(
            (short)fuelTypeCode
        );
        if (currentFuelTariff is not null)
        {
            return currentFuelTariff.fuel_tariff;
        }

        var tariffAtDate = (await _fuelTariffRepository.GetTariffHistoryAsync((short)fuelTypeCode))
            .Where(t => t.start_date <= effectiveDate)
            .Where(t => t.end_date == null || t.end_date >= effectiveDate)
            .OrderByDescending(t => t.start_date)
            .FirstOrDefault();

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

    public async Task<List<BatchTariffResult>> CalculateBatchTariffsAsync(
        List<BatchTariffRequest> requests
    )
    {
        var results = new List<BatchTariffResult>(requests.Count);
        foreach (var request in requests)
        {
            var requestTariffType =
                request.TariffType == FIS.Core.Domain.Enums.TariffType.Kilos
                    ? TariffType.Kilos
                    : TariffType.Fixed;

            var result = await GetVehicleTariffAsync(
                request.ContractCode,
                request.CheckDate,
                requestTariffType
            );

            results.Add(
                new BatchTariffResult
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
                    VmfCode = request.VmfCode,
                }
            );
        }

        return results;
    }

    public async Task<MonthlyBillingResult> CalculateMonthlyBillingAsync(
        int siteCode,
        int departmentCode,
        DateTime billingPeriodStart,
        DateTime billingPeriodEnd
    )
    {
        var result = new MonthlyBillingResult
        {
            SiteCode = siteCode,
            DepartmentCode = departmentCode,
            BillingPeriodStart = billingPeriodStart,
            BillingPeriodEnd = billingPeriodEnd,
            GrandTotal = 0m,
            ContractBillings = new List<ContractBilling>(),
        };

        // Do not use the EF contract set here: its static model includes
        // expanded audit columns that are absent on the original client
        // schema. The compatibility repository selects only columns proven to
        // exist and still supplies the vehicle/site projections needed by the
        // tariff calculation.
        var contracts = (await _contractRepository.GetActiveContractsAsync())
            .Where(contract => contract.site_code == siteCode)
            .Where(contract => (contract.Site?.Depatrment_code ?? 0) == departmentCode)
            .Where(contract =>
                contract.start_date.Date <= billingPeriodEnd.Date
                && (!contract.end_date.HasValue || contract.end_date.Value.Date >= billingPeriodStart.Date)
            )
            .ToList();

        foreach (var contract in contracts)
        {
            var fixedResult = await GetVehicleTariffAsync(
                contract.contract_code,
                billingPeriodEnd,
                TariffType.Fixed
            );
            var kiloResult = await GetVehicleTariffAsync(
                contract.contract_code,
                billingPeriodEnd,
                TariffType.Kilos
            );
            // Legacy journal calculations treat both period boundaries as
            // billable dates (DATEDIFF + 1). Excluding the final day causes a
            // one-day revenue gap in otherwise complete monthly reports.
            var quantityDays = Math.Max(
                0,
                (billingPeriodEnd.Date - billingPeriodStart.Date).Days + 1
            );

            var fixedAmount = Math.Round(quantityDays * fixedResult.Amount, 2);
            var variableAmount = 0m;
            if (
                contract.end_odometer.HasValue
                && contract.end_odometer.Value > contract.start_odometer
            )
            {
                variableAmount = Math.Round(
                    (contract.end_odometer.Value - contract.start_odometer) * kiloResult.Amount,
                    2
                );
            }

            var total = fixedAmount + variableAmount;
            result.ContractBillings.Add(
                new ContractBilling
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
                            Rate = fixedResult.Amount,
                        },
                        new()
                        {
                            Date = billingPeriodEnd.Date,
                            Description = "Kilometer tariff charge",
                            Amount = variableAmount,
                            TariffType = FIS.Core.Domain.Enums.TariffType.Kilos,
                            Source = MapToDomainTariffSource(kiloResult.Source),
                            Quantity = contract.end_odometer.HasValue
                                ? Math.Max(0, contract.end_odometer.Value - contract.start_odometer)
                                : 0,
                            Rate = kiloResult.Amount,
                        },
                    },
                }
            );

            result.TotalFixedCharges += fixedAmount;
            result.TotalKilometerCharges += variableAmount;
            result.GrandTotal += total;
        }

        return result;
    }

    public async Task<FinancialReconciliation> ReconcileTariffsAsync(
        DateTime periodStart,
        DateTime periodEnd,
        int? siteCode = null,
        int? departmentCode = null
    )
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
            RequiresAction = false,
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

        if (!vehicleTariff.vehicle_fixed_tariff.HasValue)
        {
            errors.Add("Missing vehicle_fixed_tariff.");
        }

        if (!vehicleTariff.vehicle_kilometer_tariff.HasValue)
        {
            errors.Add("Missing vehicle_kilometer_tariff.");
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
            _ => FIS.Core.Domain.Enums.TariffStatus.NoMatch,
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
            _ => FIS.Core.Domain.Enums.TariffSource.None,
        };
    }
}

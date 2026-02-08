using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

public class VehicleTariffRepository : IVehicleTariffRepository
{
    private readonly FisDbContext _context;
    private readonly ILogger<VehicleTariffRepository> _logger;

    public VehicleTariffRepository(FisDbContext context, ILogger<VehicleTariffRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VehicleTariff?> GetByIdAsync(int vehicleTariffCode)
    {
        return await _context.VehicleTariffs
            .Where(t => t.vehicle_tariff_code == vehicleTariffCode)
            .FirstOrDefaultAsync();
    }

    public async Task<VehicleTariff?> GetCurrentTariffForVehicleAsync(int vmfCode)
    {
        return await _context.VehicleTariffs
            .Where(t => t.vmf_code == vmfCode && t.end_date == null)
            .OrderByDescending(t => t.start_date)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<VehicleTariff>> GetTariffHistoryForVehicleAsync(int vmfCode)
    {
        return await _context.VehicleTariffs
            .Where(t => t.vmf_code == vmfCode)
            .OrderByDescending(t => t.start_date)
            .ToListAsync();
    }

    public async Task<VehicleTariff> CreateAsync(VehicleTariff tariff)
    {
        tariff.date_created = DateTime.UtcNow;
        tariff.calculation_date = DateTime.UtcNow;

        _context.VehicleTariffs.Add(tariff);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created vehicle tariff {TariffCode} for vehicle {VmfCode}",
            tariff.vehicle_tariff_code, tariff.vmf_code);

        return tariff;
    }

    public async Task UpdateAsync(VehicleTariff tariff)
    {
        var existing = await _context.VehicleTariffs
            .FirstOrDefaultAsync(t => t.vehicle_tariff_code == tariff.vehicle_tariff_code);

        if (existing == null)
            throw new KeyNotFoundException($"Vehicle tariff {tariff.vehicle_tariff_code} not found");

        existing.start_date = tariff.start_date;
        existing.end_date = tariff.end_date;
        existing.residual_percentage = tariff.residual_percentage;
        existing.parameter_year = tariff.parameter_year;
        existing.annual_interest_percentage = tariff.annual_interest_percentage;
        existing.purchase_amount = tariff.purchase_amount;
        existing.purchase_date = tariff.purchase_date;
        existing.purchase_amount_group = tariff.purchase_amount_group;
        existing.overhead_unit_factor = tariff.overhead_unit_factor;
        existing.target_replacement_date = tariff.target_replacement_date;
        existing.year_manufactured = tariff.year_manufactured;
        existing.model_code = tariff.model_code;
        existing.class_code = tariff.class_code;
        existing.kilometer_life = tariff.kilometer_life;
        existing.months_life = tariff.months_life;
        existing.residual_amount = tariff.residual_amount;
        existing.capital_payment = tariff.capital_payment;
        existing.overhead_payment = tariff.overhead_payment;
        existing.adjustment_amount = tariff.adjustment_amount;
        existing.vehicle_fixed_tariff = tariff.vehicle_fixed_tariff;
        existing.vehicle_fixed_tariff_pool = tariff.vehicle_fixed_tariff_pool;
        existing.class_fixed_tariff = tariff.class_fixed_tariff;
        existing.class_fixed_pool_tariff = tariff.class_fixed_pool_tariff;
        existing.lease_fixed_tariff = tariff.lease_fixed_tariff;
        existing.calculation_date = DateTime.UtcNow;
        existing.overhead_kilometer_amount = tariff.overhead_kilometer_amount;
        existing.maintenance_kilometer_amount = tariff.maintenance_kilometer_amount;
        existing.vehicle_kilometer_tariff = tariff.vehicle_kilometer_tariff;
        existing.comment = tariff.comment;
        existing.TariffWeightCalculation_Code = tariff.TariffWeightCalculation_Code;
        existing.fuel_kilo_tariff = tariff.fuel_kilo_tariff;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated vehicle tariff {TariffCode} for vehicle {VmfCode}",
            tariff.vehicle_tariff_code, tariff.vmf_code);
    }

    public async Task RecalculateTariffAsync(int vmfCode)
    {
        _logger.LogInformation("Recalculating tariff for vehicle {VmfCode}", vmfCode);

        var vehicle = await _context.Vehicles
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.vmf_code == vmfCode);

        if (vehicle == null)
        {
            _logger.LogWarning("Vehicle {VmfCode} not found for tariff recalculation", vmfCode);
            return;
        }

        var currentTariff = await GetCurrentTariffForVehicleAsync(vmfCode);

        if (currentTariff == null)
        {
            _logger.LogInformation("No existing tariff found for vehicle {VmfCode}, creating new tariff", vmfCode);

            currentTariff = new VehicleTariff
            {
                vmf_code = vmfCode,
                start_date = DateTime.UtcNow,
                parameter_year = (short)DateTime.UtcNow.Year,
                residual_percentage = 30.00m,
                annual_interest_percentage = 10.50m,
                purchase_amount = vehicle.purchase_amount ?? 0,
                purchase_date = vehicle.purchase_date,
                year_manufactured = vehicle.year_manufactured,
                model_code = vehicle.model_code,
                kilometer_life = 150000,
                months_life = 48,
                calculation_date = DateTime.UtcNow,
                date_created = DateTime.UtcNow
            };

            await CreateAsync(currentTariff);
        }

        await CalculateTariffComponentsAsync(currentTariff, vehicle);
        await UpdateAsync(currentTariff);

        _logger.LogInformation("Successfully recalculated tariff for vehicle {VmfCode}", vmfCode);
    }

    private async Task CalculateTariffComponentsAsync(VehicleTariff tariff, Vehicle vehicle)
    {
        var purchaseAmount = tariff.purchase_amount ?? vehicle.purchase_amount ?? 250000m;
        var residualPercentage = tariff.residual_percentage / 100m;
        var annualInterestRate = (tariff.annual_interest_percentage ?? 10.50m) / 100m;
        var monthsLife = tariff.months_life ?? 48;
        var kilometerLife = tariff.kilometer_life ?? 150000;

        tariff.residual_amount = purchaseAmount * residualPercentage;
        var depreciableAmount = purchaseAmount - tariff.residual_amount.Value;
        var monthlyDepreciation = depreciableAmount / monthsLife;
        var averageCapital = (purchaseAmount + tariff.residual_amount.Value) / 2;
        var monthlyInterestRate = annualInterestRate / 12m;
        var monthlyInterest = averageCapital * monthlyInterestRate;

        tariff.capital_payment = monthlyDepreciation + monthlyInterest;

        var overheadUnitFactor = tariff.overhead_unit_factor ?? 0.00013517775875275989;
        tariff.overhead_payment = purchaseAmount * (decimal)overheadUnitFactor;

        var adjustmentAmount = tariff.adjustment_amount ?? 0m;
        tariff.vehicle_fixed_tariff = tariff.capital_payment + tariff.overhead_payment + adjustmentAmount;

        tariff.overhead_kilometer_amount = (tariff.overhead_payment * 12) / kilometerLife;

        decimal maintenancePerKm = purchaseAmount switch
        {
            <= 100000 => 0.40m,
            <= 200000 => 0.55m,
            <= 300000 => 0.70m,
            <= 500000 => 0.90m,
            _ => 1.20m
        };
        tariff.maintenance_kilometer_amount = maintenancePerKm;

        decimal fuelPerKm = 2.50m;
        tariff.fuel_kilo_tariff = fuelPerKm;

        tariff.vehicle_kilometer_tariff =
            tariff.overhead_kilometer_amount +
            tariff.maintenance_kilometer_amount +
            tariff.fuel_kilo_tariff;

        tariff.vehicle_fixed_tariff_pool = tariff.vehicle_fixed_tariff;
        tariff.class_fixed_tariff = tariff.vehicle_fixed_tariff;
        tariff.class_fixed_pool_tariff = tariff.vehicle_fixed_tariff;

        if (vehicle.monthly_overhead.HasValue && vehicle.monthly_overhead > 0)
        {
            tariff.lease_fixed_tariff = vehicle.monthly_overhead;
            tariff.vehicle_fixed_tariff = vehicle.monthly_overhead;
        }

        tariff.calculation_date = DateTime.UtcNow;
        tariff.comment = $"Recalculated on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}. " +
                         $"Purchase: R{purchaseAmount:N2}, Residual: {tariff.residual_percentage}%, " +
                         $"Interest: {tariff.annual_interest_percentage}%, Life: {monthsLife}m/{kilometerLife}km";

        _logger.LogInformation(
            "Calculated tariff for vehicle {VmfCode}: Fixed=R{Fixed:N2}/month, Variable=R{Variable:N2}/km",
            tariff.vmf_code,
            tariff.vehicle_fixed_tariff,
            tariff.vehicle_kilometer_tariff);

        await Task.CompletedTask;
    }
}

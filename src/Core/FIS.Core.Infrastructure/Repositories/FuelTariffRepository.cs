using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories
{
    public class FuelTariffRepository : IFuelTariffRepository
    {
        private readonly FisDbContext _context;
        private readonly ILogger<FuelTariffRepository> _logger;

        public FuelTariffRepository(FisDbContext context, ILogger<FuelTariffRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<FuelTariff?> GetByIdAsync(short fuelTariffCode)
        {
            return await _context.FuelTariffs
                .Where(ft => !ft.is_deleted && ft.fuel_tariff_code == fuelTariffCode)
                .FirstOrDefaultAsync();
        }

        public async Task<FuelTariff?> GetCurrentTariffAsync(short fuelTypeCode)
        {
            return await _context.FuelTariffs
                .Where(ft => !ft.is_deleted
                    && ft.fuel_type_code == fuelTypeCode
                    && ft.end_date == null)
                .OrderByDescending(ft => ft.start_date)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<FuelTariff>> GetTariffHistoryAsync(short fuelTypeCode)
        {
            return await _context.FuelTariffs
                .Where(ft => !ft.is_deleted && ft.fuel_type_code == fuelTypeCode)
                .OrderByDescending(ft => ft.start_date)
                .ToListAsync();
        }

        public async Task<IEnumerable<FuelTariff>> GetAllCurrentTariffsAsync()
        {
            return await _context.FuelTariffs
                .Where(ft => !ft.is_deleted && ft.end_date == null)
                .ToListAsync();
        }

        public async Task<FuelTariff> CreateAsync(FuelTariff fuelTariff, int currentUserId)
        {
            fuelTariff.date_created = DateTime.UtcNow;
            fuelTariff.created_by_user_code = currentUserId;
            fuelTariff.is_deleted = false;

            _context.FuelTariffs.Add(fuelTariff);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created fuel tariff {TariffCode} for fuel type {FuelTypeCode} with rate {Rate}",
                fuelTariff.fuel_tariff_code, fuelTariff.fuel_type_code, fuelTariff.fuel_tariff);

            return fuelTariff;
        }

        public async Task<FuelTariff> UpdateAsync(FuelTariff fuelTariff, int currentUserId)
        {
            var existing = await _context.FuelTariffs
                .FirstOrDefaultAsync(ft => ft.fuel_tariff_code == fuelTariff.fuel_tariff_code && !ft.is_deleted);

            if (existing == null)
                throw new KeyNotFoundException($"Fuel tariff {fuelTariff.fuel_tariff_code} not found");

            existing.fuel_tariff = fuelTariff.fuel_tariff;
            existing.fuel_tariff_notes = fuelTariff.fuel_tariff_notes;
            existing.start_date = fuelTariff.start_date;
            existing.end_date = fuelTariff.end_date;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated fuel tariff {TariffCode} for fuel type {FuelTypeCode}",
                fuelTariff.fuel_tariff_code, fuelTariff.fuel_type_code);

            return existing;
        }

        public async Task<FuelTariff> CreateNewRateAsync(short fuelTypeCode, decimal newRate, string? notes, int currentUserId)
        {
            // Close the current tariff by setting end_date
            var currentTariff = await GetCurrentTariffAsync(fuelTypeCode);
            if (currentTariff != null)
            {
                currentTariff.end_date = DateTime.UtcNow;
                currentTariff.date_updated = DateTime.UtcNow;
                currentTariff.modified_by_user_code = currentUserId;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Closed fuel tariff {TariffCode} for fuel type {FuelTypeCode}",
                    currentTariff.fuel_tariff_code, fuelTypeCode);
            }

            // Create new tariff
            var newTariff = new FuelTariff
            {
                fuel_type_code = fuelTypeCode,
                fuel_tariff = newRate,
                fuel_tariff_notes = notes,
                start_date = DateTime.UtcNow,
                end_date = null, // Current/active tariff
                date_created = DateTime.UtcNow,
                created_by_user_code = currentUserId,
                is_deleted = false
            };

            return await CreateAsync(newTariff, currentUserId);
        }

        public async Task DeleteAsync(short fuelTariffCode, int currentUserId)
        {
            var fuelTariff = await _context.FuelTariffs
                .FirstOrDefaultAsync(ft => ft.fuel_tariff_code == fuelTariffCode);

            if (fuelTariff == null)
                throw new KeyNotFoundException($"Fuel tariff {fuelTariffCode} not found");

            fuelTariff.is_deleted = true;
            fuelTariff.date_updated = DateTime.UtcNow;
            fuelTariff.modified_by_user_code = currentUserId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Soft deleted fuel tariff {TariffCode}", fuelTariffCode);
        }
    }
}

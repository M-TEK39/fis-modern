using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class FuelCardRepository : IFuelCardRepository
{
    private readonly FisDbContext _context;

    public FuelCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<FuelCard?> GetByIdAsync(int fuelCardCode)
    {
        return await _context.Set<FuelCard>()
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .FirstOrDefaultAsync(f => f.Fuel_card_code == fuelCardCode && !f.is_deleted);
    }

    public async Task<IEnumerable<FuelCard>> GetFuelCardsByVehicleAsync(int vmfCode)
    {
        return await _context.Set<FuelCard>()
            .Where(f => f.vmf_code == vmfCode && !f.is_deleted)
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<FuelCard?> GetByCardNumberAsync(string cardNumber)
    {
        return await _context.Set<FuelCard>()
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .FirstOrDefaultAsync(f => f.card_number == cardNumber && !f.is_deleted);
    }

    public async Task<IEnumerable<FuelCard>> GetActiveFuelCardsAsync()
    {
        return await _context.Set<FuelCard>()
            .Where(f => !f.is_deleted && (f.Status_date == null || f.Status_date > DateTime.Now))
            .Include(f => f.Vehicle)
            .Include(f => f.Site)
            .ToListAsync();
    }

    public async Task<FuelCard> CreateAsync(FuelCard fuelCard, int currentUserId)
    {
        fuelCard.date_created = DateTime.Now;
        fuelCard.created_by_user_code = currentUserId;
        fuelCard.is_deleted = false;

        await _context.Set<FuelCard>().AddAsync(fuelCard);
        await _context.SaveChangesAsync();
        return fuelCard;
    }

    public async Task UpdateAsync(FuelCard fuelCard, int currentUserId)
    {
        if (fuelCard == null)
            throw new ArgumentNullException(nameof(fuelCard));

        var existing = await _context.Set<FuelCard>().FindAsync(fuelCard.Fuel_card_code);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"FuelCard with Fuel_card_code {fuelCard.Fuel_card_code} not found");

        fuelCard.date_updated = DateTime.Now;
        fuelCard.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(fuelCard);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int fuelCardCode, int currentUserId)
    {
        var fuelCard = await _context.Set<FuelCard>().FindAsync(fuelCardCode);
        if (fuelCard != null && !fuelCard.is_deleted)
        {
            fuelCard.is_deleted = true;
            fuelCard.date_updated = DateTime.Now;
            fuelCard.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}

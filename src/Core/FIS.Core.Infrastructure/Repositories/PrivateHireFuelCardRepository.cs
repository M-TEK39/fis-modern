using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class PrivateHireFuelCardRepository : IPrivateHireFuelCardRepository
{
    private readonly FisDbContext _context;

    public PrivateHireFuelCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PrivateHireFuelCard?> GetByIdAsync(int privateHireFuelCardId)
    {
        return await _context.PrivateHireFuelCards
            .FirstOrDefaultAsync(x => x.PHFuel_card_code == privateHireFuelCardId && !x.is_deleted);
    }

    public async Task<PrivateHireFuelCard?> GetByCardNumberAsync(string cardNumber)
    {
        return await _context.PrivateHireFuelCards
            .FirstOrDefaultAsync(x => x.card_number == cardNumber && !x.is_deleted);
    }

    public async Task<IEnumerable<PrivateHireFuelCard>> GetByPrivateHireCodeAsync(int privateHireCode)
    {
        return await _context.PrivateHireFuelCards
            .Where(x => x.phv_code == privateHireCode && !x.is_deleted)
            .OrderByDescending(x => x.Counter)
            .ThenByDescending(x => x.PHFuel_card_code)
            .ToListAsync();
    }

    public async Task<IEnumerable<PrivateHireFuelCard>> GetByRegistrationNumberAsync(string registrationNumber)
    {
        var normalized = registrationNumber.Trim();

        return await (
            from fuelCard in _context.PrivateHireFuelCards
            join privateHire in _context.PrivateHires on fuelCard.phv_code equals privateHire.PHV_code
            where !fuelCard.is_deleted
                && !privateHire.is_deleted
                && privateHire.registration_number != null
                && privateHire.registration_number == normalized
            orderby fuelCard.Counter descending, fuelCard.PHFuel_card_code descending
            select fuelCard
        ).ToListAsync();
    }

    public async Task<IEnumerable<PrivateHireFuelCard>> GetActiveFuelCardsAsync()
    {
        return await _context.PrivateHireFuelCards
            .Where(x => !x.is_deleted)
            .OrderByDescending(x => x.date_created)
            .ToListAsync();
    }

    public async Task<PrivateHireFuelCard> CreateAsync(PrivateHireFuelCard fuelCard, int currentUserId)
    {
        fuelCard.date_created = DateTime.UtcNow;
        fuelCard.created_by_user_code = currentUserId;
        fuelCard.is_deleted = false;

        await _context.PrivateHireFuelCards.AddAsync(fuelCard);
        await _context.SaveChangesAsync();
        return fuelCard;
    }

    public async Task UpdateAsync(PrivateHireFuelCard fuelCard, int currentUserId)
    {
        var existing = await _context.PrivateHireFuelCards.FindAsync(fuelCard.PHFuel_card_code);
        if (existing == null || existing.is_deleted)
        {
            throw new InvalidOperationException($"PrivateHireFuelCard {fuelCard.PHFuel_card_code} not found");
        }

        fuelCard.date_updated = DateTime.UtcNow;
        fuelCard.modified_by_user_code = currentUserId;
        _context.Entry(existing).CurrentValues.SetValues(fuelCard);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int privateHireFuelCardId, int currentUserId)
    {
        var existing = await _context.PrivateHireFuelCards.FindAsync(privateHireFuelCardId);
        if (existing != null && !existing.is_deleted)
        {
            existing.is_deleted = true;
            existing.date_updated = DateTime.UtcNow;
            existing.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}

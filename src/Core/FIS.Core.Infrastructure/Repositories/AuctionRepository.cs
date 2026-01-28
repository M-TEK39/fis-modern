using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly FisDbContext _context;

    public AuctionRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Auction?> GetByIdAsync(short auctionCode)
    {
        return await _context.Set<Auction>()
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.auction_code == auctionCode);
    }

    public async Task<IEnumerable<Auction>> GetAllAsync()
    {
        return await _context.Set<Auction>()
            .Include(a => a.Vehicle)
            .ToListAsync();
    }

    public async Task<IEnumerable<Auction>> GetByVehicleAsync(int vmfCode)
    {
        return await _context.Set<Auction>()
            .Where(a => a.vmf_code == vmfCode)
            .Include(a => a.Vehicle)
            .ToListAsync();
    }

    public async Task<Auction> CreateAsync(Auction auction, int currentUserId)
    {
        await _context.Set<Auction>().AddAsync(auction);
        await _context.SaveChangesAsync();
        return auction;
    }

    public async Task<Auction> UpdateAsync(Auction auction, int currentUserId)
    {
        if (auction == null)
            throw new ArgumentNullException(nameof(auction));

        var existing = await _context.Set<Auction>().FindAsync(auction.auction_code);
        if (existing == null)
            throw new InvalidOperationException($"Auction with auction_code {auction.auction_code} not found");

        _context.Entry(existing).CurrentValues.SetValues(auction);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short auctionCode, int currentUserId)
    {
        var auction = await GetByIdAsync(auctionCode);
        if (auction != null)
        {
            _context.Set<Auction>().Remove(auction);
            await _context.SaveChangesAsync();
        }
    }
}

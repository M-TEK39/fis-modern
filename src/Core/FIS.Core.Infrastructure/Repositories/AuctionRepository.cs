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

    public async Task<Auction> CreateAsync(Auction auction)
    {
        await _context.Set<Auction>().AddAsync(auction);
        await _context.SaveChangesAsync();
        return auction;
    }

    public async Task<Auction> UpdateAsync(Auction auction)
    {
        _context.Set<Auction>().Update(auction);
        await _context.SaveChangesAsync();
        return auction;
    }

    public async Task DeleteAsync(short auctionCode)
    {
        var auction = await GetByIdAsync(auctionCode);
        if (auction != null)
        {
            _context.Set<Auction>().Remove(auction);
            await _context.SaveChangesAsync();
        }
    }
}

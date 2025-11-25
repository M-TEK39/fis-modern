using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;

public class TripAuthorityRepository : ITripAuthorityRepository
{
    private readonly FisDbContext _context;
    public TripAuthorityRepository(FisDbContext context) { _context = context; }

    public async Task<TripAuthority?> GetByIdAsync(int code) => await _context.TripAuthorities.FirstOrDefaultAsync(t => t.trip_authority_code == code);
    public async Task<IEnumerable<TripAuthority>> GetAllAsync() => await _context.TripAuthorities.ToListAsync();
    public async Task<IEnumerable<TripAuthority>> GetByContractAsync(int contractCode) => await _context.TripAuthorities.Where(t => t.contract_code == contractCode).ToListAsync();
    public async Task<TripAuthority> CreateAsync(TripAuthority item) { await _context.TripAuthorities.AddAsync(item); await _context.SaveChangesAsync(); return item; }
    public async Task<TripAuthority> UpdateAsync(TripAuthority item) { _context.TripAuthorities.Update(item); await _context.SaveChangesAsync(); return item; }
    public async Task DeleteAsync(int code) { var item = await GetByIdAsync(code); if (item != null) { _context.TripAuthorities.Remove(item); await _context.SaveChangesAsync(); } }
}

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
namespace FIS.Core.Infrastructure.Repositories;
public class AssetVerificationRepository : IAssetVerificationRepository
{
    private readonly FisDbContext _context;
    public AssetVerificationRepository(FisDbContext context) { _context = context; }
    public async Task<AssetVerification?> GetByIdAsync(int verificationCode) { return await _context.Set<AssetVerification>().Include(a => a.Vehicle).Include(a => a.Site).Include(a => a.VerifiedByUser).FirstOrDefaultAsync(a => a.asset_verification_code == verificationCode); }
    public async Task<IEnumerable<AssetVerification>> GetAllAsync() { return await _context.Set<AssetVerification>().Include(a => a.Vehicle).Include(a => a.Site).ToListAsync(); }
    public async Task<IEnumerable<AssetVerification>> GetByVehicleAsync(int vmfCode) { return await _context.Set<AssetVerification>().Where(a => a.vmf_code == vmfCode).Include(a => a.Vehicle).ToListAsync(); }
    public async Task<IEnumerable<AssetVerification>> GetBySiteAsync(int siteCode) { return await _context.Set<AssetVerification>().Where(a => a.site_code == siteCode).Include(a => a.Site).ToListAsync(); }
    public async Task<IEnumerable<AssetVerification>> GetByStatusAsync(string status) { return await _context.Set<AssetVerification>().Where(a => a.verification_status == status).Include(a => a.Vehicle).ToListAsync(); }
    public async Task<AssetVerification> CreateAsync(AssetVerification verification) { _context.Set<AssetVerification>().Add(verification); await _context.SaveChangesAsync(); return verification; }
    public async Task<AssetVerification> UpdateAsync(AssetVerification verification) { _context.Set<AssetVerification>().Update(verification); await _context.SaveChangesAsync(); return verification; }
    public async Task DeleteAsync(int verificationCode) { var verification = await GetByIdAsync(verificationCode); if (verification != null) { _context.Set<AssetVerification>().Remove(verification); await _context.SaveChangesAsync(); } }
}

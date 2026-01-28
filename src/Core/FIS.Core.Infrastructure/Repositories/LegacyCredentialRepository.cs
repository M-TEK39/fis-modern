using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class LegacyCredentialRepository : ILegacyCredentialRepository
{
    private readonly FisDbContext _context;

    public LegacyCredentialRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LegacyUserCredential?> GetByUserAccessCodeAsync(int userAccessCode)
    {
        return await _context.LegacyUserCredentials
                
                .FirstOrDefaultAsync(c => c.user_access_code == userAccessCode);
    }

    public async Task<List<LegacyUserCredential>> GetAllAsync()
    {
        return await _context.LegacyUserCredentials.ToListAsync();
    }

    public async Task<LegacyUserCredential> CreateAsync(LegacyUserCredential credential, int currentUserId)
    {
            
            _context.LegacyUserCredentials.Add(credential);
        await _context.SaveChangesAsync();
        return credential;
    }

    public async Task<LegacyUserCredential> UpdateAsync(LegacyUserCredential credential, int currentUserId)
    {
        if (credential == null)
            throw new ArgumentNullException(nameof(credential));

        var existing = await _context.LegacyUserCredentials.FindAsync(credential.credential_id);
        if (existing == null)
            throw new InvalidOperationException($"LegacyUserCredential with credential_id {credential.credential_id} not found");

        _context.Entry(existing).CurrentValues.SetValues(credential);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int userAccessCode, int currentUserId)
    {
        var credential = await GetByUserAccessCodeAsync(userAccessCode);
        if (credential != null)
        {
            _context.LegacyUserCredentials.Remove(credential);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int userAccessCode)
    {
        return await _context.LegacyUserCredentials
            .AnyAsync(c => c.user_access_code == userAccessCode);
    }
}

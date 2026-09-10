using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Auth;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for EntraId_User_Mapping table.
/// Manages the bridge between Azure Entra ID users and legacy TS_Users.
/// </summary>
public class EntraIdUserMappingRepository : IEntraIdUserMappingRepository
{
    private readonly FisDbContext _context;

    public EntraIdUserMappingRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<EntraIdUserMapping?> GetByEntraObjectIdAsync(string entraObjectId)
    {
        return await _context
            .EntraIdUserMappings.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.entra_object_id == entraObjectId);
    }

    public async Task<EntraIdUserMapping?> GetByUserAccessCodeAsync(int userAccessCode)
    {
        return await _context
            .EntraIdUserMappings.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.user_access_code == userAccessCode);
    }

    public async Task<EntraIdUserMapping> CreateAsync(EntraIdUserMapping mapping, int currentUserId)
    {
        _context.EntraIdUserMappings.Add(mapping);
        await _context.SaveChangesAsync();
        return mapping;
    }

    public async Task UpdateAsync(EntraIdUserMapping mapping, int currentUserId)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));

        var existing = await _context.EntraIdUserMappings.FindAsync(mapping.mapping_id);
        if (existing == null)
            throw new InvalidOperationException(
                $"EntraIdUserMapping with mapping_id {mapping.mapping_id} not found"
            );

        _context.Entry(existing).CurrentValues.SetValues(mapping);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int mappingId, int currentUserId)
    {
        var mapping = await _context.EntraIdUserMappings.FindAsync(mappingId);
        if (mapping != null)
        {
            _context.EntraIdUserMappings.Remove(mapping);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string entraObjectId)
    {
        return await _context.EntraIdUserMappings.AnyAsync(m => m.entra_object_id == entraObjectId);
    }

    public async Task<IEnumerable<EntraIdUserMapping>> GetAllAsync()
    {
        return await _context.EntraIdUserMappings.Include(m => m.User).ToListAsync();
    }
}

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

public class ThirdPartyAllocationRepository : IThirdPartyAllocationRepository
{
    private readonly FisDbContext _context;

    public ThirdPartyAllocationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ThirdPartyAllocation?> GetByIdAsync(int allocationId)
    {
        return await _context.Set<ThirdPartyAllocation>()
            .Include(a => a.Project)
            .Include(a => a.Supplier)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.allocation_id == allocationId && !a.is_deleted);
    }

    public async Task<IEnumerable<ThirdPartyAllocation>> GetByProjectAsync(int projectId)
    {
        return await _context.Set<ThirdPartyAllocation>()
            .Include(a => a.Supplier)
            .Include(a => a.Vehicle)
            .Where(a => a.project_id == projectId && !a.is_deleted)
            .OrderBy(a => a.date_created)
            .ToListAsync();
    }

    public async Task<ThirdPartyAllocation> CreateAsync(ThirdPartyAllocation allocation, int currentUserId)
    {
        allocation.date_created = DateTime.UtcNow;
        allocation.created_by_user_code = currentUserId;
        allocation.is_deleted = false;

        _context.Set<ThirdPartyAllocation>().Add(allocation);
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task DeleteAsync(int allocationId, int currentUserId)
    {
        var allocation = await _context.Set<ThirdPartyAllocation>()
            .FirstOrDefaultAsync(a => a.allocation_id == allocationId)
            ?? throw new KeyNotFoundException($"Allocation {allocationId} not found");

        // Soft delete
        allocation.is_deleted = true;
        allocation.date_updated = DateTime.UtcNow;
        allocation.modified_by_user_code = currentUserId;

        await _context.SaveChangesAsync();
    }
}

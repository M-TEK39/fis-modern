using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// third_party_allocations is not in the 2012 archive. Archive allocations
/// live on Third_Party_Project_Supplier / Third_Party_Vehicle_Allocations
/// through ThirdPartyRentalRepository.
/// </summary>
public class ThirdPartyAllocationRepository : IThirdPartyAllocationRepository
{
    private const string TableName = "third_party_allocations";

    private readonly FisDbContext _context;

    public ThirdPartyAllocationRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ThirdPartyAllocation?> GetByIdAsync(int allocationId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return null;
        }

        return await _context
            .Set<ThirdPartyAllocation>()
            .FirstOrDefaultAsync(a => a.allocation_id == allocationId);
    }

    public async Task<IEnumerable<ThirdPartyAllocation>> GetByProjectAsync(int projectId)
    {
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            return [];
        }

        return await _context
            .Set<ThirdPartyAllocation>()
            .Where(a => a.project_id == projectId)
            .ToListAsync();
    }

    public async Task<ThirdPartyAllocation> CreateAsync(
        ThirdPartyAllocation allocation,
        int currentUserId
    )
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        _context.Set<ThirdPartyAllocation>().Add(allocation);
        await _context.SaveChangesAsync();
        return allocation;
    }

    public async Task DeleteAsync(int allocationId, int currentUserId)
    {
        _ = currentUserId;
        if (!await WorkflowOptionalTable.ExistsInSchemaAsync(_context, "dbo", TableName))
        {
            throw new InvalidOperationException(
                WorkflowOptionalTable.MissingMessage("dbo", TableName)
            );
        }

        var allocation =
            await _context
                .Set<ThirdPartyAllocation>()
                .FirstOrDefaultAsync(a => a.allocation_id == allocationId)
            ?? throw new KeyNotFoundException($"Allocation {allocationId} not found");

        _context.Set<ThirdPartyAllocation>().Remove(allocation);
        await _context.SaveChangesAsync();
    }
}

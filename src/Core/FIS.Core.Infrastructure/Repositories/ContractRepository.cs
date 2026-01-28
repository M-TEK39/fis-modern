using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for contract operations against legacy contract table
/// Handles vehicle hire contracts with business rules for active/current contracts
/// </summary>
public class ContractRepository : IContractRepository
{
    private readonly FisDbContext _context;

    public ContractRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get contract by contract code (primary key)
    /// </summary>
    public async Task<Contract?> GetByIdAsync(int contractCode)
    {
        return await _context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.Site)
            .FirstOrDefaultAsync(c => c.contract_code == contractCode);
    }

    /// <summary>
    /// Get all active contracts (still_current = 'Y')
    /// </summary>
    public async Task<IEnumerable<Contract>> GetActiveContractsAsync()
    {
        return await _context.Contracts
            .Where(c => c.still_current == "Y")
            .Include(c => c.Vehicle)
            .Include(c => c.Site)
            .OrderBy(c => c.start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get all contracts including inactive ones
    /// </summary>
    public async Task<IEnumerable<Contract>> GetAllAsync()
    {
        return await _context.Contracts
            .Include(c => c.Vehicle)
            .Include(c => c.Site)
            .OrderBy(c => c.start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get all contracts for a specific vehicle
    /// </summary>
    public async Task<IEnumerable<Contract>> GetContractsByVehicleAsync(int vmfCode)
    {
        return await _context.Contracts
            .Where(c => c.vmf_code == vmfCode)
            .Include(c => c.Vehicle)
            .Include(c => c.Site)
            .OrderByDescending(c => c.start_date)
            .ToListAsync();
    }

    /// <summary>
    /// Get the currently active contract for a vehicle
    /// CRITICAL: Only one contract per vehicle should have still_current = 'Y'
    /// </summary>
    public async Task<Contract?> GetActiveContractByVehicleAsync(int vmfCode)
    {
        return await _context.Contracts
            .Where(c => c.vmf_code == vmfCode && c.still_current == "Y")
            .Include(c => c.Vehicle)
            .Include(c => c.Site)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Check if vehicle has an active contract
    /// Business rule: Vehicle should not have multiple active contracts
    /// </summary>
    public async Task<bool> HasActiveContractAsync(int vmfCode)
    {
        return await _context.Contracts
            .AnyAsync(c => c.vmf_code == vmfCode && c.still_current == "Y");
    }

    /// <summary>
    /// Create a new contract
    /// IMPORTANT: Business rule enforcement for unique active contracts
    /// </summary>
    public async Task<Contract> CreateAsync(Contract contract, int currentUserId)
    {
        if (contract == null)
            throw new ArgumentNullException(nameof(contract));

        // Business rule: Check if vehicle already has active contract
        if (contract.still_current == "Y")
        {
            var hasActiveContract = await HasActiveContractAsync(contract.vmf_code);
            if (hasActiveContract)
            {
                throw new InvalidOperationException(
                    $"Vehicle {contract.vmf_code} already has an active contract. " +
                    "Only one active contract per vehicle is allowed.");
            }
        }

        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();
        return contract;
    }

    /// <summary>
    /// Update an existing contract
    /// IMPORTANT: Enforce business rules for active contract changes
    /// </summary>
    public async Task UpdateAsync(Contract contract, int currentUserId)
    {
        if (contract == null)
            throw new ArgumentNullException(nameof(contract));

        // Business rule: If making this contract active, check for conflicts
        if (contract.still_current == "Y")
        {
            var existingActiveContract = await _context.Contracts
                .Where(c => c.vmf_code == contract.vmf_code && 
                           c.still_current == "Y" && 
                           c.contract_code != contract.contract_code)
                .FirstOrDefaultAsync();

            if (existingActiveContract != null)
            {
                throw new InvalidOperationException(
                    $"Vehicle {contract.vmf_code} already has an active contract " +
                    $"(Contract Code: {existingActiveContract.contract_code}). " +
                    "Only one active contract per vehicle is allowed.");
            }
        }

        var existing = await _context.Contracts.FindAsync(contract.contract_code);
        if (existing == null)
            throw new InvalidOperationException($"Contract with contract_code {contract.contract_code} not found");

        // Preserve creation audit fields
                    contract.date_created = existing.date_created;
                    contract.created_by_user_code = existing.created_by_user_code;
        // Set update audit fields
                    contract.date_updated = DateTime.UtcNow;
                    contract.modified_by_user_code = currentUserId;
        
        _context.Entry(existing).CurrentValues.SetValues(contract);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete a contract
    /// IMPORTANT: Consider business implications of deleting contracts
    /// </summary>
    public async Task DeleteAsync(int contractCode, int currentUserId)
    {
        var contract = await GetByIdAsync(contractCode);
        if (contract != null)
        {
            // Soft delete instead of hard delete
            contract.is_deleted = true;
            contract.modified_by_user_code = currentUserId;
            contract.date_updated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// End/close an active contract
    /// Sets still_current = 'N' and records end details
    /// </summary>
    public async Task EndContractAsync(int contractCode, DateTime endDate, int? endOdometer = null, string? notes = null)
    {
        var contract = await GetByIdAsync(contractCode);
        if (contract == null)
            throw new ArgumentException($"Contract {contractCode} not found");

        if (contract.still_current != "Y")
            throw new InvalidOperationException($"Contract {contractCode} is not currently active");

        contract.still_current = "N";
        contract.end_date = endDate;
        contract.end_time = DateTime.Now;
        
        if (endOdometer.HasValue)
            contract.end_odometer = endOdometer.Value;
            
        if (!string.IsNullOrEmpty(notes))
            contract.Notes = notes;

        await UpdateAsync(contract, 1);
    }
}
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Append-only repository for the contract audit log.
/// Records every state transition and field-level edit on a contract.
/// </summary>
public class ContractAuditLogRepository : IContractAuditLogRepository
{
    private readonly FisDbContext _context;

    public ContractAuditLogRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ContractAuditLog>> GetByContractAsync(int contractCode)
    {
        return await _context.ContractAuditLogs
            .Where(l => l.contract_code == contractCode)
            .OrderBy(l => l.performed_at)
            .ToListAsync();
    }

    public async Task LogAsync(
        int contractCode,
        string action,
        int performedByUserId,
        short? oldStatus = null,
        short? newStatus = null,
        string? notes = null,
        string? fieldChanged = null,
        string? oldValue = null,
        string? newValue = null)
    {
        var entry = new ContractAuditLog
        {
            contract_code = contractCode,
            action = action,
            performed_by_user_code = performedByUserId,
            performed_at = DateTime.Now,
            old_status_code = oldStatus,
            new_status_code = newStatus,
            notes = notes,
            field_changed = fieldChanged,
            old_value = oldValue,
            new_value = newValue
        };

        _context.ContractAuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }
}

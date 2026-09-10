using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository for class-based tariff CRUD and approval workflow.
/// Approval threshold: monthly_fixed_amount > R100,000 requires explicit approval.
/// Self-approval is blocked — see TariffManagementController.
/// </summary>
public class TariffManagementRepository : ITariffManagementRepository
{
    private readonly FisDbContext _context;

    // Tariffs above this monthly amount require approval before becoming effective.
    // ⚠️ ASSUMPTION: R100,000/month threshold. Confirm with business — see QUESTIONS.md.
    public const decimal ApprovalThreshold = 100_000m;

    public TariffManagementRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<Tariff?> GetByIdAsync(int tariffCode)
    {
        return await _context
            .Tariffs.Where(t => !t.is_deleted)
            .FirstOrDefaultAsync(t => t.tariff_code == tariffCode);
    }

    public async Task<IEnumerable<Tariff>> GetAllAsync()
    {
        return await _context
            .Tariffs.Where(t => !t.is_deleted)
            .OrderBy(t => t.class_code)
            .ThenByDescending(t => t.effective_start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tariff>> GetApprovedAsync()
    {
        return await _context
            .Tariffs.Where(t => !t.is_deleted && t.tariff_approval_status == 2)
            .OrderBy(t => t.class_code)
            .ThenByDescending(t => t.effective_start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tariff>> GetPendingApprovalAsync()
    {
        return await _context
            .Tariffs.Where(t => !t.is_deleted && t.tariff_approval_status == 1)
            .OrderBy(t => t.date_created)
            .ToListAsync();
    }

    public async Task<Tariff> CreateAsync(Tariff tariff, int currentUserId)
    {
        tariff.date_created = DateTime.Now;
        tariff.created_by_user_code = currentUserId;
        tariff.is_deleted = false;

        // Auto-approve if below threshold — no approval needed for small tariffs
        tariff.tariff_approval_status =
            tariff.monthly_fixed_amount > ApprovalThreshold
                ? (short)0 // Draft — must be submitted and approved
                : (short)2; // Auto-approved — below threshold

        _context.Tariffs.Add(tariff);
        await _context.SaveChangesAsync();
        return tariff;
    }

    public async Task UpdateAsync(Tariff tariff, int currentUserId)
    {
        var existing =
            await _context.Tariffs.FindAsync(tariff.tariff_code)
            ?? throw new InvalidOperationException($"Tariff {tariff.tariff_code} not found");

        // Only allow editing Draft (0) or Rejected (3) tariffs
        if (existing.tariff_approval_status != 0 && existing.tariff_approval_status != 3)
            throw new InvalidOperationException(
                "Only Draft or Rejected tariffs can be edited. "
                    + "Approved or Pending tariffs must be rejected first."
            );

        tariff.date_updated = DateTime.Now;
        tariff.modified_by_user_code = currentUserId;
        // Re-evaluate threshold after edit
        tariff.tariff_approval_status =
            tariff.monthly_fixed_amount > ApprovalThreshold ? (short)0 : (short)2;
        // Clear any previous rejection reason on edit
        tariff.rejection_reason = null;

        _context.Entry(existing).CurrentValues.SetValues(tariff);
        await _context.SaveChangesAsync();
    }

    public async Task<Tariff> SubmitForApprovalAsync(int tariffCode, int currentUserId)
    {
        var tariff =
            await _context.Tariffs.FindAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 0 && tariff.tariff_approval_status != 3)
            throw new InvalidOperationException(
                "Only Draft (0) or Rejected (3) tariffs can be submitted for approval."
            );

        if (
            tariff.created_by_user_code.HasValue
            && tariff.created_by_user_code.Value != currentUserId
        )
            throw new UnauthorizedAccessException(
                "Only the original capturer can submit this tariff for approval."
            );

        tariff.tariff_approval_status = 1; // Pending Approval
        tariff.date_updated = DateTime.Now;
        tariff.modified_by_user_code = currentUserId;
        tariff.rejection_reason = null;

        await _context.SaveChangesAsync();
        return tariff;
    }

    public async Task<Tariff> ApproveAsync(int tariffCode, int approverUserId)
    {
        var tariff =
            await _context.Tariffs.FindAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 1)
            throw new InvalidOperationException(
                "Only Pending Approval (1) tariffs can be approved."
            );

        tariff.tariff_approval_status = 2; // Approved
        tariff.approver_code = approverUserId;
        tariff.approval_date = DateTime.Now;
        tariff.date_updated = DateTime.Now;
        tariff.modified_by_user_code = approverUserId;

        await _context.SaveChangesAsync();
        return tariff;
    }

    public async Task<Tariff> RejectAsync(
        int tariffCode,
        int approverUserId,
        string rejectionReason
    )
    {
        var tariff =
            await _context.Tariffs.FindAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 1)
            throw new InvalidOperationException(
                "Only Pending Approval (1) tariffs can be rejected."
            );

        tariff.tariff_approval_status = 3; // Rejected
        tariff.approver_code = approverUserId;
        tariff.approval_date = DateTime.Now;
        tariff.rejection_reason = rejectionReason;
        tariff.date_updated = DateTime.Now;
        tariff.modified_by_user_code = approverUserId;

        await _context.SaveChangesAsync();
        return tariff;
    }
}

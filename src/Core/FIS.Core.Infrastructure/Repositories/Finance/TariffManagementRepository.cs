using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Class-tariff CRUD and approval workflow over dbo.tariff. Persistence goes
/// through the compatibility tariff repository so missing approval/audit
/// columns cannot break the original table. Approval mutations require the
/// expanded status columns; they are not approximated.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Schema and table identifiers are fixed constants; no caller input is interpolated."
)]
public class TariffManagementRepository : ITariffManagementRepository
{
    public const decimal ApprovalThreshold = 100_000m;
    private const string ApprovalStatusColumn = "tariff_approval_status";

    private readonly ITariffRepository _tariffs;
    private readonly FisDbContext _context;

    public TariffManagementRepository(ITariffRepository tariffs, FisDbContext context)
    {
        _tariffs = tariffs;
        _context = context;
    }

    public Task<Tariff?> GetByIdAsync(int tariffCode) => _tariffs.GetByIdAsync(tariffCode);

    public async Task<IEnumerable<Tariff>> GetAllAsync() => await _tariffs.GetAllAsync();

    public async Task<IEnumerable<Tariff>> GetApprovedAsync()
    {
        var tariffs = await _tariffs.GetAllAsync();
        return tariffs
            .Where(tariff => tariff.tariff_approval_status == 2)
            .OrderBy(tariff => tariff.class_code)
            .ThenByDescending(tariff => tariff.effective_start_date)
            .ToList();
    }

    public async Task<IEnumerable<Tariff>> GetPendingApprovalAsync()
    {
        if (!await HasApprovalWorkflowAsync())
            return [];

        var tariffs = await _tariffs.GetAllAsync();
        return tariffs
            .Where(tariff => tariff.tariff_approval_status == 1)
            .OrderBy(tariff => tariff.date_created)
            .ToList();
    }

    public async Task<Tariff> CreateAsync(Tariff tariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        tariff.is_deleted = false;
        if (await HasApprovalWorkflowAsync())
        {
            tariff.tariff_approval_status =
                tariff.monthly_fixed_amount > ApprovalThreshold ? (short)0 : (short)2;
        }
        else
        {
            tariff.tariff_approval_status = 2;
        }

        return await _tariffs.CreateAsync(tariff, currentUserId);
    }

    public async Task UpdateAsync(Tariff tariff, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        var existing =
            await _tariffs.GetByIdAsync(tariff.tariff_code)
            ?? throw new InvalidOperationException($"Tariff {tariff.tariff_code} not found");

        if (await HasApprovalWorkflowAsync())
        {
            if (existing.tariff_approval_status != 0 && existing.tariff_approval_status != 3)
            {
                throw new InvalidOperationException(
                    "Only Draft or Rejected tariffs can be edited. "
                        + "Approved or Pending tariffs must be rejected first."
                );
            }

            tariff.tariff_approval_status =
                tariff.monthly_fixed_amount > ApprovalThreshold ? (short)0 : (short)2;
            tariff.rejection_reason = null;
        }

        tariff.created_by_user_code = existing.created_by_user_code;
        tariff.date_created = existing.date_created;
        await _tariffs.UpdateAsync(tariff, currentUserId);
    }

    public async Task<Tariff> SubmitForApprovalAsync(int tariffCode, int currentUserId)
    {
        await EnsureApprovalWorkflowAsync("submit");
        var tariff =
            await _tariffs.GetByIdAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 0 && tariff.tariff_approval_status != 3)
        {
            throw new InvalidOperationException(
                "Only Draft (0) or Rejected (3) tariffs can be submitted for approval."
            );
        }

        if (
            tariff.created_by_user_code.HasValue
            && tariff.created_by_user_code.Value != currentUserId
        )
        {
            throw new UnauthorizedAccessException(
                "Only the original capturer can submit this tariff for approval."
            );
        }

        tariff.tariff_approval_status = 1;
        tariff.rejection_reason = null;
        return await _tariffs.UpdateAsync(tariff, currentUserId);
    }

    public async Task<Tariff> ApproveAsync(int tariffCode, int approverUserId)
    {
        await EnsureApprovalWorkflowAsync("approve");
        var tariff =
            await _tariffs.GetByIdAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 1)
        {
            throw new InvalidOperationException(
                "Only Pending Approval (1) tariffs can be approved."
            );
        }

        tariff.tariff_approval_status = 2;
        tariff.approver_code = approverUserId;
        tariff.approval_date = DateTime.Now;
        return await _tariffs.UpdateAsync(tariff, approverUserId);
    }

    public async Task<Tariff> RejectAsync(
        int tariffCode,
        int approverUserId,
        string rejectionReason
    )
    {
        await EnsureApprovalWorkflowAsync("reject");
        var tariff =
            await _tariffs.GetByIdAsync(tariffCode)
            ?? throw new InvalidOperationException($"Tariff {tariffCode} not found");

        if (tariff.tariff_approval_status != 1)
        {
            throw new InvalidOperationException(
                "Only Pending Approval (1) tariffs can be rejected."
            );
        }

        tariff.tariff_approval_status = 3;
        tariff.approver_code = approverUserId;
        tariff.approval_date = DateTime.Now;
        tariff.rejection_reason = rejectionReason;
        return await _tariffs.UpdateAsync(tariff, approverUserId);
    }

    private async Task EnsureApprovalWorkflowAsync(string action)
    {
        if (!await HasApprovalWorkflowAsync())
        {
            throw new InvalidOperationException(
                $"The legacy dbo.tariff table has no {ApprovalStatusColumn} column; tariff {action} cannot be approximated."
            );
        }
    }

    private async Task<bool> HasApprovalWorkflowAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(1)
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                  AND [COLUMN_NAME] = @column
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, "tariff");
            AddParameter(command, "@column", DbType.String, ApprovalStatusColumn);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

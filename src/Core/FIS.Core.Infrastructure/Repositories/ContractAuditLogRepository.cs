using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Contracts;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Append-only repository for the contract audit log.
/// Records every state transition and field-level edit on a contract.
/// </summary>
[SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The SQL statement uses fixed metadata identifiers and parameterized values.")]
public class ContractAuditLogRepository : IContractAuditLogRepository
{
    private readonly FisDbContext _context;

    public ContractAuditLogRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ContractAuditLog>> GetByContractAsync(int contractCode)
    {
        if (!await AuditTableExistsAsync())
        {
            return [];
        }

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
        if (!await AuditTableExistsAsync())
        {
            return;
        }

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

    private async Task<bool> AuditTableExistsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [INFORMATION_SCHEMA].[TABLES]
                    WHERE [TABLE_SCHEMA] = @schemaName
                      AND [TABLE_NAME] = @tableName
                ) THEN 1 ELSE 0 END
                """;

            AddParameter(command, "@schemaName", DbType.String, "dbo");
            AddParameter(command, "@tableName", DbType.String, "contract_audit_log");
            var result = await command.ExecuteScalarAsync();
            return result is not null && Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
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

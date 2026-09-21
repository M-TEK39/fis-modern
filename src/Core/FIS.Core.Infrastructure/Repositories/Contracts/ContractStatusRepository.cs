using System.Data;
using FIS.Core.Domain.Entities;
using FIS.Core.Infrastructure.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Contract status lookups against dbo.contract_status.
/// The GGMT v2.1.05 table has no is_deleted/is_active/abbreviation columns.
/// The 2012 GGFIS database may not have the table at all.
/// </summary>
public class ContractStatusRepository : IContractStatusRepository
{
    private const string MissingTableMessage =
        "dbo.contract_status is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public ContractStatusRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<ContractStatus?> GetByIdAsync(short statusCode)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.ContractStatuses.FirstOrDefaultAsync(cs =>
            cs.contract_status_code == statusCode
        );
    }

    public async Task<ContractStatus?> GetByDescriptionAsync(string description)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.ContractStatuses.FirstOrDefaultAsync(cs =>
            cs.status_description == description
        );
    }

    public async Task<ContractStatus?> GetByAbbreviationAsync(string abbreviation)
    {
        // status_abbreviation is expanded-only. The archived table has no
        // abbreviation column, so this lookup cannot be satisfied there.
        if (!await TableExistsAsync() || string.IsNullOrWhiteSpace(abbreviation))
        {
            return null;
        }

        return null;
    }

    public async Task<IEnumerable<ContractStatus>> GetAllStatusesAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context.ContractStatuses.OrderBy(cs => cs.status_description).ToListAsync();
    }

    public Task<IEnumerable<ContractStatus>> GetActiveStatusesAsync() =>
        GetAllStatusesAsync();

    public Task<IEnumerable<ContractStatus>> GetFinalStatusesAsync() =>
        GetAllStatusesAsync();

    public async Task<IEnumerable<ContractStatus>> SearchStatusesAsync(string searchTerm)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .ContractStatuses.Where(cs => cs.status_description.Contains(searchTerm))
            .OrderBy(cs => cs.status_description)
            .ToListAsync();
    }

    public async Task<ContractStatus> CreateAsync(ContractStatus status, int currentUserId)
    {
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        _context.ContractStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task<ContractStatus> UpdateAsync(ContractStatus status, int currentUserId)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context.ContractStatuses.FindAsync(status.contract_status_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"ContractStatus with contract_status_code {status.contract_status_code} not found"
            );

        existing.status_description = status.status_description;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(short statusCode, int currentUserId)
    {
        if (!await TableExistsAsync())
        {
            return false;
        }

        var status = await GetByIdAsync(statusCode);
        if (status == null)
            return false;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('dbo.contract_status', 'is_deleted') IS NOT NULL
                    UPDATE [dbo].[contract_status]
                    SET [is_deleted] = 1
                    WHERE [contract_status_code] = @statusCode;
                ELSE
                    DELETE FROM [dbo].[contract_status]
                    WHERE [contract_status_code] = @statusCode;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@statusCode";
            parameter.DbType = DbType.Int16;
            parameter.Value = statusCode;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(status).State = EntityState.Detached;
            return true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<bool> TableExistsAsync()
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

            var schema = command.CreateParameter();
            schema.ParameterName = "@schemaName";
            schema.DbType = DbType.String;
            schema.Value = "dbo";
            command.Parameters.Add(schema);

            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.DbType = DbType.String;
            table.Value = "contract_status";
            command.Parameters.Add(table);

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
}

using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.Status has no is_deleted. Identifier is required on the archive table.
/// The Workflow schema is absent from 2012 GGFIS.
/// </summary>
public class StatusRepository : IStatusRepository
{
    private const string MissingTableMessage =
        "Workflow.Status is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public StatusRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Status?> GetByIdAsync(int statusId)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.Statuses.FirstOrDefaultAsync(s => s.StatusID == statusId);
    }

    public async Task<IEnumerable<Status>> GetAllAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context.Statuses.OrderByDescending(s => s.DateStarted).ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetByStepIdAsync(int stepId)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Statuses.Where(s => s.StepID == stepId)
            .OrderByDescending(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetActiveStatusesAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Statuses.Where(s => s.IsBusy && s.DateCompleted == null)
            .OrderBy(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Status>> GetByUserAsync(string userName)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Statuses.Where(s => s.StartedByUserName == userName)
            .OrderByDescending(s => s.DateStarted)
            .ToListAsync();
    }

    public async Task<Status> CreateAsync(Status status, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        await _context.Statuses.AddAsync(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task UpdateAsync(Status status, int currentUserId)
    {
        _ = currentUserId;
        if (status == null)
            throw new ArgumentNullException(nameof(status));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context.Statuses.FindAsync(status.StatusID);
        if (existing == null)
            throw new InvalidOperationException($"Status with ID {status.StatusID} not found");

        existing.StepID = status.StepID;
        existing.DateCompleted = status.DateCompleted;
        existing.DateStarted = status.DateStarted;
        existing.IsBusy = status.IsBusy;
        existing.StartedByUserName = status.StartedByUserName;
        existing.Identifier = status.Identifier;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int statusId, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                IF COL_LENGTH('Workflow.Status', 'is_deleted') IS NOT NULL
                    UPDATE [Workflow].[Status]
                    SET [is_deleted] = 1
                    WHERE [StatusID] = @statusId;
                ELSE
                    DELETE FROM [Workflow].[Status]
                    WHERE [StatusID] = @statusId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@statusId";
            parameter.DbType = DbType.Int32;
            parameter.Value = statusId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
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
            schema.Value = "Workflow";
            command.Parameters.Add(schema);
            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.DbType = DbType.String;
            table.Value = "Status";
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

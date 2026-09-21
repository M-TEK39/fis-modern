using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.Step has no is_deleted. The Workflow schema is absent from 2012 GGFIS.
/// </summary>
public class StepRepository : IStepRepository
{
    private const string MissingTableMessage =
        "Workflow.Step is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public StepRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Step?> GetByIdAsync(int stepId)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.Steps.FirstOrDefaultAsync(s => s.StepID == stepId);
    }

    public async Task<IEnumerable<Step>> GetAllAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context.Steps.OrderBy(s => s.StepOrder).ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetByWorkflowIdAsync(int workflowId)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Steps.Where(s => s.WorkflowID == workflowId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetByStepTypeIdAsync(int stepTypeId)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Steps.Where(s => s.StepTypeID == stepTypeId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<Step>> GetChildStepsAsync(int parentStepId)
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Steps.Where(s => s.ParentStepID == parentStepId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    public async Task<Step> CreateAsync(Step step, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        await _context.Steps.AddAsync(step);
        await _context.SaveChangesAsync();
        return step;
    }

    public async Task UpdateAsync(Step step, int currentUserId)
    {
        _ = currentUserId;
        if (step == null)
            throw new ArgumentNullException(nameof(step));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context.Steps.FindAsync(step.StepID);
        if (existing == null)
            throw new InvalidOperationException($"Step with ID {step.StepID} not found");

        existing.StepName = step.StepName;
        existing.StepOrder = step.StepOrder;
        existing.StepTypeID = step.StepTypeID;
        existing.WorkflowID = step.WorkflowID;
        existing.ParentStepID = step.ParentStepID;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int stepId, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            return;
        }

        await DeleteRowAsync(stepId);
    }

    private async Task DeleteRowAsync(int key)
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
                IF COL_LENGTH('Workflow.Step', 'is_deleted') IS NOT NULL
                    UPDATE [Workflow].[Step]
                    SET [is_deleted] = 1
                    WHERE [StepID] = @key;
                ELSE
                    DELETE FROM [Workflow].[Step]
                    WHERE [StepID] = @key;
                """;
            AddParameter(command, "@key", DbType.Int32, key);
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
            AddParameter(command, "@schemaName", DbType.String, "Workflow");
            AddParameter(command, "@tableName", DbType.String, "Step");
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

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        DbType type,
        object value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

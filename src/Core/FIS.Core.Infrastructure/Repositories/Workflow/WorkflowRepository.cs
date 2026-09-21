using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.Workflow is WorkflowID + WorkflowName + AlwaysExecute.
/// The Workflow schema is absent from 2012 GGFIS.
/// </summary>
public class WorkflowRepository : IWorkflowRepository
{
    private const string MissingTableMessage =
        "Workflow.Workflow is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public WorkflowRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Workflow?> GetByIdAsync(int workflowId)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.Workflows.FirstOrDefaultAsync(w => w.WorkflowID == workflowId);
    }

    public async Task<Workflow?> GetByNameAsync(string workflowName)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.Workflows.FirstOrDefaultAsync(w => w.WorkflowName == workflowName);
    }

    public async Task<IEnumerable<Workflow>> GetAllAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context.Workflows.OrderBy(w => w.WorkflowName).ToListAsync();
    }

    public async Task<IEnumerable<Workflow>> GetActiveWorkflowsAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Workflows.Where(w => w.AlwaysExecute)
            .OrderBy(w => w.WorkflowName)
            .ToListAsync();
    }

    public async Task<Workflow> CreateAsync(Workflow workflow, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        await _context.Workflows.AddAsync(workflow);
        await _context.SaveChangesAsync();
        return workflow;
    }

    public async Task UpdateAsync(Workflow workflow, int currentUserId)
    {
        _ = currentUserId;
        if (workflow == null)
            throw new ArgumentNullException(nameof(workflow));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context.Workflows.FindAsync(workflow.WorkflowID);
        if (existing == null)
            throw new InvalidOperationException($"Workflow with ID {workflow.WorkflowID} not found");

        existing.WorkflowName = workflow.WorkflowName;
        existing.AlwaysExecute = workflow.AlwaysExecute;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int workflowId, int currentUserId)
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
                IF COL_LENGTH('Workflow.Workflow', 'is_deleted') IS NOT NULL
                    UPDATE [Workflow].[Workflow]
                    SET [is_deleted] = 1
                    WHERE [WorkflowID] = @workflowId;
                ELSE
                    DELETE FROM [Workflow].[Workflow]
                    WHERE [WorkflowID] = @workflowId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@workflowId";
            parameter.DbType = DbType.Int32;
            parameter.Value = workflowId;
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
            table.Value = "Workflow";
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

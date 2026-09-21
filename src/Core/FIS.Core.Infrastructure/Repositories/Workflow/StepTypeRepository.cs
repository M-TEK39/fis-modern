using System.Data;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Workflow.StepType is StepTypeID + StepTypeName + StepTypeData.
/// The Workflow schema is absent from the 2012 GGFIS database.
/// </summary>
public class StepTypeRepository : IStepTypeRepository
{
    private const string MissingTableMessage =
        "Workflow.StepType is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public StepTypeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<StepType?> GetByIdAsync(int stepTypeId)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.StepTypes.FirstOrDefaultAsync(st => st.StepTypeID == stepTypeId);
    }

    public async Task<StepType?> GetByNameAsync(string stepTypeName)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context.StepTypes.FirstOrDefaultAsync(st => st.StepTypeName == stepTypeName);
    }

    public async Task<IEnumerable<StepType>> GetAllAsync()
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context.StepTypes.OrderBy(st => st.StepTypeName).ToListAsync();
    }

    public async Task<StepType> CreateAsync(StepType stepType, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        await _context.StepTypes.AddAsync(stepType);
        await _context.SaveChangesAsync();
        return stepType;
    }

    public async Task UpdateAsync(StepType stepType, int currentUserId)
    {
        _ = currentUserId;
        if (stepType == null)
            throw new ArgumentNullException(nameof(stepType));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context.StepTypes.FindAsync(stepType.StepTypeID);
        if (existing == null)
            throw new InvalidOperationException($"StepType with ID {stepType.StepTypeID} not found");

        existing.StepTypeName = stepType.StepTypeName;
        existing.StepTypeData = stepType.StepTypeData;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int stepTypeId, int currentUserId)
    {
        _ = currentUserId;
        if (!await TableExistsAsync())
        {
            return;
        }

        var stepType = await _context.StepTypes.FindAsync(stepTypeId);
        if (stepType == null)
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
                IF COL_LENGTH('Workflow.StepType', 'is_deleted') IS NOT NULL
                    UPDATE [Workflow].[StepType]
                    SET [is_deleted] = 1
                    WHERE [StepTypeID] = @stepTypeId;
                ELSE
                    DELETE FROM [Workflow].[StepType]
                    WHERE [StepTypeID] = @stepTypeId;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@stepTypeId";
            parameter.DbType = DbType.Int32;
            parameter.Value = stepTypeId;
            command.Parameters.Add(parameter);
            await command.ExecuteNonQueryAsync();
            _context.Entry(stepType).State = EntityState.Detached;
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
            table.Value = "StepType";
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

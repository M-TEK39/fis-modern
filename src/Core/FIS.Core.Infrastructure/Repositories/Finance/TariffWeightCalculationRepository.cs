using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for TariffWeightCalculation table.
/// Handles weight calculations for overhead distribution across vehicle categories.
/// fin.TariffWeightCalculation is a GGMT fiscal object and may be absent.
/// </summary>
public class TariffWeightCalculationRepository : ITariffWeightCalculationRepository
{
    private const string MissingTableMessage =
        "fin.TariffWeightCalculation is unavailable on this database. No compatibility fallback was used.";

    private readonly FisDbContext _context;

    public TariffWeightCalculationRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<TariffWeightCalculation>> GetByTariffParameterAsync(
        int tariffParameterId
    )
    {
        if (!await TableExistsAsync())
        {
            return [];
        }

        return await _context
            .Set<TariffWeightCalculation>()
            .Where(twc => twc.TariffParameterID == tariffParameterId)
            .OrderBy(twc => twc.category)
            .ToListAsync();
    }

    public async Task<TariffWeightCalculation?> GetByCategoryAsync(
        int tariffParameterId,
        string category
    )
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        if (decimal.TryParse(category, out decimal categoryValue))
        {
            return await _context
                .Set<TariffWeightCalculation>()
                .FirstOrDefaultAsync(twc =>
                    twc.TariffParameterID == tariffParameterId && twc.category == categoryValue
                );
        }
        return null;
    }

    public async Task<TariffWeightCalculation?> GetByIdAsync(int tariffWeightCalculationId)
    {
        if (!await TableExistsAsync())
        {
            return null;
        }

        return await _context
            .Set<TariffWeightCalculation>()
            .FirstOrDefaultAsync(twc =>
                twc.TariffWeightCalculation_Code == tariffWeightCalculationId
            );
    }

    public async Task<decimal> GetTotalWeightAsync(int tariffParameterId)
    {
        var calculations = await GetByTariffParameterAsync(tariffParameterId);

        return calculations
            .Where(twc => twc.number.HasValue && twc.WeightFactorPerUnit.HasValue)
            .Sum(twc => (decimal)(twc.number!.Value * twc.WeightFactorPerUnit!.Value));
    }

    public async Task<TariffWeightCalculation> CreateAsync(
        TariffWeightCalculation weightCalculation,
        int currentUserId
    )
    {
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        weightCalculation.calculation_date_time = DateTime.Now;
        _context.Set<TariffWeightCalculation>().Add(weightCalculation);
        await _context.SaveChangesAsync();
        return weightCalculation;
    }

    public async Task<TariffWeightCalculation> UpdateAsync(
        TariffWeightCalculation weightCalculation,
        int currentUserId
    )
    {
        if (weightCalculation == null)
            throw new ArgumentNullException(nameof(weightCalculation));
        if (!await TableExistsAsync())
        {
            throw new InvalidOperationException(MissingTableMessage);
        }

        var existing = await _context
            .Set<TariffWeightCalculation>()
            .FindAsync(weightCalculation.TariffWeightCalculation_Code);
        if (existing == null)
            throw new InvalidOperationException(
                $"TariffWeightCalculation with TariffWeightCalculation_Code {weightCalculation.TariffWeightCalculation_Code} not found"
            );

        weightCalculation.calculation_date_time = DateTime.Now;
        _context.Entry(existing).CurrentValues.SetValues(weightCalculation);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int tariffWeightCalculationId, int currentUserId)
    {
        var weightCalculation = await GetByIdAsync(tariffWeightCalculationId);
        if (weightCalculation != null)
        {
            _context.Set<TariffWeightCalculation>().Remove(weightCalculation);
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var calculations = await GetByTariffParameterAsync(tariffParameterId);
        if (calculations.Count == 0)
        {
            return;
        }

        _context.Set<TariffWeightCalculation>().RemoveRange(calculations);
        await _context.SaveChangesAsync();
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
            schema.Value = "fin";
            command.Parameters.Add(schema);

            var table = command.CreateParameter();
            table.ParameterName = "@tableName";
            table.DbType = DbType.String;
            table.Value = "TariffWeightCalculation";
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

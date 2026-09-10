using System.Data;
using System.Data.Common;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository for vehicle licence history snapshots.
/// </summary>
public class VehicleLicenceHistoryRepository : IVehicleLicenceHistoryRepository
{
    private readonly FisDbContext _context;

    public VehicleLicenceHistoryRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsAvailableAsync()
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
                SELECT COUNT(1)
                FROM [INFORMATION_SCHEMA].[TABLES]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, "vehicle_licence_history");
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<VehicleLicenceHistory>> GetByVehicleAsync(int vmfCode)
    {
        if (!await IsAvailableAsync())
        {
            return [];
        }

        return await _context
            .VehicleLicenceHistories.Include(h => h.CapturedByUser)
            .Where(h => h.vmf_code == vmfCode)
            .OrderByDescending(h => h.captured_at)
            .ToListAsync();
    }

    public async Task<VehicleLicenceHistory?> GetLatestByVehicleAsync(int vmfCode)
    {
        if (!await IsAvailableAsync())
        {
            return null;
        }

        return await _context
            .VehicleLicenceHistories.Include(h => h.CapturedByUser)
            .Where(h => h.vmf_code == vmfCode)
            .OrderByDescending(h => h.captured_at)
            .FirstOrDefaultAsync();
    }

    public async Task<VehicleLicenceHistory> CreateAsync(VehicleLicenceHistory history)
    {
        history.captured_at = DateTime.UtcNow;
        _context.VehicleLicenceHistories.Add(history);
        await _context.SaveChangesAsync();
        return history;
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

using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

public class LogbookRepository : ILogbookRepository
{
    private static readonly string[] ModernRequiredColumns =
    ["logbookcode", "vmf_code", "begin_num", "end_num", "handout_date", "site_code", "lb_receiver_name", "lb_tel_num", "lb_comment", "date_created", "date_updated", "created_by_user_code", "modified_by_user_code", "is_deleted"];

    private readonly FisDbContext _context;
    private readonly LegacyLogbookRepository _legacyRepository;
    private bool? _modernSchemaAvailable;

    public LogbookRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _legacyRepository = new LegacyLogbookRepository(_context);
    }

    public async Task<Logbook?> GetByIdAsync(short logbookCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByIdAsync(logbookCode);

        return await _context.Set<Logbook>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .FirstOrDefaultAsync(l => l.logbookcode == logbookCode && !l.is_deleted);
    }

    public async Task<IEnumerable<Logbook>> GetAllAsync()
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetAllAsync();

        return await _context.Set<Logbook>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .Where(l => !l.is_deleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByVehicleAsync(vmfCode);

        return await _context.Set<Logbook>()
            .Where(l => l.vmf_code == vmfCode && !l.is_deleted)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetBySiteAsync(siteCode);

        return await _context.Set<Logbook>()
            .Where(l => l.site_code == siteCode && !l.is_deleted)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<Logbook> CreateAsync(Logbook logbook, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.CreateAsync(logbook, currentUserId);

        logbook.date_created = DateTime.UtcNow;
        logbook.date_updated = logbook.date_created;
        logbook.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        logbook.modified_by_user_code = logbook.created_by_user_code;
        logbook.is_deleted = false;
        await _context.Set<Logbook>().AddAsync(logbook);
        await _context.SaveChangesAsync();
        return logbook;
    }

    public async Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.UpdateAsync(logbook, currentUserId);

        if (logbook == null)
            throw new ArgumentNullException(nameof(logbook));

        var existing = await _context.Set<Logbook>().FindAsync(logbook.logbookcode);
        if (existing == null)
            throw new InvalidOperationException($"Logbook with logbookcode {logbook.logbookcode} not found");

        _context.Entry(existing).CurrentValues.SetValues(logbook);
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(short logbookCode, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
        {
            await _legacyRepository.DeleteAsync(logbookCode, currentUserId);
            return;
        }

        var logbook = await GetByIdAsync(logbookCode);
        if (logbook != null)
        {
            logbook.is_deleted = true;
            logbook.date_updated = DateTime.UtcNow;
            logbook.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
            await _context.SaveChangesAsync();
        }
    }

    private async Task<bool> IsModernSchemaAvailableAsync()
    {
        if (_modernSchemaAvailable.HasValue)
            return _modernSchemaAvailable.Value;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
            AddParameter(command, "@schema", "dbo");
            AddParameter(command, "@table", "logbook");

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            _modernSchemaAvailable = ModernRequiredColumns.All(columns.Contains);
            return _modernSchemaAvailable.Value;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = System.Data.DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

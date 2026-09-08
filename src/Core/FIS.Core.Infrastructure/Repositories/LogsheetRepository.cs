using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

public class LogsheetRepository : ILogsheetRepository
{
    private static readonly string[] ModernRequiredColumns =
    [
        "log_code",
        "vmf_code",
        "start_odo",
        "end_odo",
        "month",
        "site_code",
        "rek_num",
        "days_used",
        "bund_num",
        "trans_date",
        "driver_time",
        "FBS_comp",
        "user_access_code",
        "trans_time",
        "department_code",
        "contract_code",
        "journal_detail_code",
        "parent_log_code",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;
    private readonly LegacyLogsheetRepository _legacyRepository;
    private bool? _modernSchemaAvailable;

    public LogsheetRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _legacyRepository = new LegacyLogsheetRepository(_context);
    }

    public async Task<Logsheet?> GetByIdAsync(int logCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByIdAsync(logCode);

        return await _context
            .Set<Logsheet>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .FirstOrDefaultAsync(l => l.log_code == logCode && !l.is_deleted);
    }

    public async Task<IEnumerable<Logsheet>> GetAllAsync()
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetAllAsync();

        return await _context
            .Set<Logsheet>()
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .Where(l => !l.is_deleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByVehicleAsync(vmfCode);

        return await _context
            .Set<Logsheet>()
            .Where(l => l.vmf_code == vmfCode && !l.is_deleted)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.GetByMonthAsync(month);

        var startOfMonth = new DateTime(month.Year, month.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1);

        return await _context
            .Set<Logsheet>()
            .Where(l => l.month >= startOfMonth && l.month < endOfMonth && !l.is_deleted)
            .Include(l => l.Vehicle)
            .Include(l => l.Site)
            .ToListAsync();
    }

    public async Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.CreateAsync(logsheet, currentUserId);

        ArgumentNullException.ThrowIfNull(logsheet);
        var now = DateTime.UtcNow;
        logsheet.date_created = now;
        logsheet.date_updated = now;
        logsheet.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        logsheet.modified_by_user_code = logsheet.created_by_user_code;
        logsheet.is_deleted = false;
        if (logsheet.trans_date == default)
            logsheet.trans_date = now;
        if (logsheet.trans_time == default)
            logsheet.trans_time = now.TimeOfDay;
        if (logsheet.department_code <= 0)
            logsheet.department_code = logsheet.site_code;
        await _context.Set<Logsheet>().AddAsync(logsheet);
        await _context.SaveChangesAsync();
        return logsheet;
    }

    public async Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
            return await _legacyRepository.UpdateAsync(logsheet, currentUserId);

        if (logsheet == null)
            throw new ArgumentNullException(nameof(logsheet));

        var existing = await _context.Set<Logsheet>().FindAsync(logsheet.log_code);
        if (existing == null)
            throw new InvalidOperationException(
                $"Logsheet with log_code {logsheet.log_code} not found"
            );

        logsheet.date_created = existing.date_created;
        logsheet.created_by_user_code = existing.created_by_user_code;
        logsheet.is_deleted = existing.is_deleted;
        if (logsheet.trans_date == default)
            logsheet.trans_date = existing.trans_date;
        if (logsheet.trans_time == default)
            logsheet.trans_time = existing.trans_time;
        if (logsheet.department_code <= 0)
            logsheet.department_code = existing.department_code;
        _context.Entry(existing).CurrentValues.SetValues(logsheet);
        existing.date_updated = DateTime.UtcNow;
        existing.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int logCode, int currentUserId)
    {
        if (!await IsModernSchemaAvailableAsync())
        {
            await _legacyRepository.DeleteAsync(logCode, currentUserId);
            return;
        }

        var logsheet = await GetByIdAsync(logCode);
        if (logsheet != null)
        {
            logsheet.is_deleted = true;
            logsheet.date_updated = DateTime.UtcNow;
            logsheet.modified_by_user_code =
                currentUserId > 0 ? currentUserId : logsheet.modified_by_user_code;
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
            command.CommandText =
                "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
            AddParameter(command, "@schema", "dbo");
            AddParameter(command, "@table", "Logsheets");
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

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        string value
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = System.Data.DbType.String;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

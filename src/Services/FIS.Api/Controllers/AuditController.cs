using FIS.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Controllers;

/// <summary>
/// Read-only access to the audit trail and user status history.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AuditController : BaseApiController
{
    private readonly FisDbContext _context;
    private readonly ILogger<AuditController> _logger;

    private const int MaxPageSize = 200;

    public AuditController(FisDbContext context, ILogger<AuditController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Query the audit trail with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] string?   tableName  = null,
        [FromQuery] string?   action     = null,
        [FromQuery] DateTime? fromDate   = null,
        [FromQuery] DateTime? toDate     = null,
        [FromQuery] int?      userId     = null,
        [FromQuery] string?   primaryKey = null,
        [FromQuery] int       pageNumber = 1,
        [FromQuery] int       pageSize   = 50)
    {
        try
        {
            pageSize   = Math.Min(pageSize, MaxPageSize);
            pageNumber = Math.Max(pageNumber, 1);

            var query = _context.Audits
                .AsNoTracking()
                .Where(a => !a.is_deleted);

            if (!string.IsNullOrWhiteSpace(tableName))
                query = query.Where(a => a.TableName != null &&
                    a.TableName.Contains(tableName));

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action.ToUpper());

            if (fromDate.HasValue)
                query = query.Where(a => a.date_created >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.date_created <= toDate.Value.AddDays(1));

            if (userId.HasValue)
                query = query.Where(a => a.created_by_user_code == userId.Value);

            if (!string.IsNullOrWhiteSpace(primaryKey))
                query = query.Where(a => a.PrimaryKey == primaryKey);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.date_created)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.AuditID,
                    a.Action,
                    a.TableName,
                    a.PrimaryKey,
                    a.Changes,
                    a.ActionedBy,
                    a.created_by_user_code,
                    ChangedAt = a.date_created
                })
                .ToListAsync();

            return Ok(new
            {
                TotalCount  = total,
                PageNumber  = pageNumber,
                PageSize    = pageSize,
                TotalPages  = (int)Math.Ceiling(total / (double)pageSize),
                Items       = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying audit trail");
            return StatusCode(500, "An error occurred while querying the audit trail");
        }
    }

    /// <summary>
    /// Get the full audit history for a specific record (table + primary key).
    /// </summary>
    [HttpGet("{tableName}/{primaryKey}")]
    public async Task<IActionResult> GetRecordHistory(string tableName, string primaryKey)
    {
        try
        {
            var items = await _context.Audits
                .AsNoTracking()
                .Where(a => a.TableName == tableName && a.PrimaryKey == primaryKey && !a.is_deleted)
                .OrderByDescending(a => a.date_created)
                .Select(a => new
                {
                    a.AuditID,
                    a.Action,
                    a.Changes,
                    a.ActionedBy,
                    a.created_by_user_code,
                    ChangedAt = a.date_created
                })
                .ToListAsync();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying record history for {TableName}/{PrimaryKey}", tableName, primaryKey);
            return StatusCode(500, "An error occurred while querying record history");
        }
    }

    /// <summary>
    /// Get user status change history (enable/disable/lock events).
    /// </summary>
    [HttpGet("user-status-history")]
    public async Task<IActionResult> GetUserStatusHistory(
        [FromQuery] int?      userAccessCode = null,
        [FromQuery] DateTime? fromDate       = null,
        [FromQuery] DateTime? toDate         = null,
        [FromQuery] int       pageNumber     = 1,
        [FromQuery] int       pageSize       = 50)
    {
        try
        {
            pageSize   = Math.Min(pageSize, MaxPageSize);
            pageNumber = Math.Max(pageNumber, 1);

            var query = _context.UserStatusHistories.AsNoTracking();

            if (userAccessCode.HasValue)
                query = query.Where(h => h.user_access_code == userAccessCode.Value);

            if (fromDate.HasValue)
                query = query.Where(h => h.changed_at >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(h => h.changed_at <= toDate.Value.AddDays(1));

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(h => h.changed_at)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(h => new
                {
                    h.status_history_id,
                    h.user_access_code,
                    h.new_status,
                    h.previous_status,
                    h.changed_by_user_code,
                    h.changed_at,
                    h.reason
                })
                .ToListAsync();

            return Ok(new
            {
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize   = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Items      = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying user status history");
            return StatusCode(500, "An error occurred while querying user status history");
        }
    }

    /// <summary>
    /// Get password change history from LegacyUserCredentials (who changed it, when, expiry).
    /// Password hashes are never returned.
    /// </summary>
    [HttpGet("password-history")]
    public async Task<IActionResult> GetPasswordHistory(
        [FromQuery] int? userAccessCode = null,
        [FromQuery] int  pageNumber     = 1,
        [FromQuery] int  pageSize       = 50)
    {
        try
        {
            pageSize   = Math.Min(pageSize, MaxPageSize);
            pageNumber = Math.Max(pageNumber, 1);

            var query = _context.LegacyUserCredentials
                .AsNoTracking()
                .Where(c => c.is_active);

            if (userAccessCode.HasValue)
                query = query.Where(c => c.user_access_code == userAccessCode.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.last_password_change)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.user_access_code,
                    c.last_password_change,
                    c.password_expiry_date,
                    c.changed_by_user_code,
                    c.failed_login_attempts,
                    c.account_locked_until,
                    IsExpired = c.password_expiry_date.HasValue
                        ? DateTime.UtcNow > c.password_expiry_date.Value
                        : false
                })
                .ToListAsync();

            return Ok(new
            {
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize   = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Items      = items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying password history");
            return StatusCode(500, "An error occurred while querying password history");
        }
    }
}

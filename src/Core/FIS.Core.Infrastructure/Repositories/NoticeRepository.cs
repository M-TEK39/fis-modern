using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Notice entity operations
/// Provides CRUD operations for system notices and announcements
/// </summary>
public class NoticeRepository : INoticeRepository
{
    private readonly FisDbContext _context;

    public NoticeRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<Notice?> GetByIdAsync(int noticeId)
    {
        return await _context.Notices
            .Include(n => n.NoticeSchedules)
            .Where(n => !n.is_deleted)
            .FirstOrDefaultAsync(n => n.notice_id == noticeId);
    }

    public async Task<IEnumerable<Notice>> GetAllAsync()
    {
        return await _context.Notices
            .Include(n => n.NoticeSchedules)
            .Where(n => !n.is_deleted)
            .OrderByDescending(n => n.notice_date ?? n.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notice>> GetActiveNoticesAsync()
    {
        var today = DateTime.UtcNow.Date;

        return await _context.Notices
            .Include(n => n.NoticeSchedules)
            .Where(n => !n.is_deleted)
            .Where(n => n.NoticeSchedules.Any(s =>
                !s.is_deleted &&
                s.start_date <= today &&
                (s.end_date == null || s.end_date >= today)))
            .OrderBy(n => n.NoticeSchedules.Min(s => s.sort_order))
            .ThenByDescending(n => n.notice_date ?? n.date_created)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notice>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.Notices
            .Include(n => n.NoticeSchedules)
            .Where(n => !n.is_deleted)
            .Where(n => n.notice_date >= startDate && n.notice_date <= endDate)
            .OrderByDescending(n => n.notice_date ?? n.date_created)
            .ToListAsync();
    }

    public async Task<Notice> CreateAsync(Notice notice, int currentUserId)
    {
        notice.date_created = DateTime.UtcNow;
        notice.created_by_user_code = currentUserId;
        notice.is_deleted = false;

        _context.Notices.Add(notice);
        await _context.SaveChangesAsync();
        return notice;
    }

    public async Task<Notice> UpdateAsync(Notice notice, int currentUserId)
    {
        if (notice == null)
            throw new ArgumentNullException(nameof(notice));

        var existing = await _context.Notices.FindAsync(notice.notice_id);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"Notice with notice_id {notice.notice_id} not found");

        notice.date_updated = DateTime.UtcNow;
        notice.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(notice);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int noticeId, int currentUserId)
    {
        var notice = await _context.Notices.FindAsync(noticeId);
        if (notice != null)
        {
            notice.is_deleted = true;
            notice.date_updated = DateTime.UtcNow;
            notice.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}

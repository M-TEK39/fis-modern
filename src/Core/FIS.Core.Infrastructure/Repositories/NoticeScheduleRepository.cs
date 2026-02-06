using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for NoticeSchedule entity operations
/// Provides CRUD operations for notice scheduling and display management
/// </summary>
public class NoticeScheduleRepository : INoticeScheduleRepository
{
    private readonly FisDbContext _context;

    public NoticeScheduleRepository(FisDbContext context)
    {
        _context = context;
    }

    public async Task<NoticeSchedule?> GetByIdAsync(int noticeScheduleId)
    {
        return await _context.NoticeSchedules
            .Include(ns => ns.Notice)
            .Where(ns => !ns.is_deleted)
            .FirstOrDefaultAsync(ns => ns.notice_schedule_id == noticeScheduleId);
    }

    public async Task<IEnumerable<NoticeSchedule>> GetAllAsync()
    {
        return await _context.NoticeSchedules
            .Include(ns => ns.Notice)
            .Where(ns => !ns.is_deleted)
            .OrderBy(ns => ns.sort_order)
            .ThenByDescending(ns => ns.start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<NoticeSchedule>> GetByNoticeIdAsync(int noticeId)
    {
        return await _context.NoticeSchedules
            .Include(ns => ns.Notice)
            .Where(ns => !ns.is_deleted)
            .Where(ns => ns.notice_id == noticeId)
            .OrderBy(ns => ns.sort_order)
            .ToListAsync();
    }

    public async Task<IEnumerable<NoticeSchedule>> GetActiveSchedulesAsync()
    {
        var today = DateTime.UtcNow.Date;

        return await _context.NoticeSchedules
            .Include(ns => ns.Notice)
            .Where(ns => !ns.is_deleted)
            .Where(ns => ns.start_date <= today && (ns.end_date == null || ns.end_date >= today))
            .OrderBy(ns => ns.sort_order)
            .ThenByDescending(ns => ns.start_date)
            .ToListAsync();
    }

    public async Task<IEnumerable<NoticeSchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.NoticeSchedules
            .Include(ns => ns.Notice)
            .Where(ns => !ns.is_deleted)
            .Where(ns =>
                (ns.start_date <= endDate) &&
                (ns.end_date == null || ns.end_date >= startDate))
            .OrderBy(ns => ns.sort_order)
            .ThenBy(ns => ns.start_date)
            .ToListAsync();
    }

    public async Task<NoticeSchedule> CreateAsync(NoticeSchedule noticeSchedule, int currentUserId)
    {
        noticeSchedule.date_created = DateTime.UtcNow;
        noticeSchedule.created_by_user_code = currentUserId;
        noticeSchedule.is_deleted = false;

        _context.NoticeSchedules.Add(noticeSchedule);
        await _context.SaveChangesAsync();
        return noticeSchedule;
    }

    public async Task<NoticeSchedule> UpdateAsync(NoticeSchedule noticeSchedule, int currentUserId)
    {
        if (noticeSchedule == null)
            throw new ArgumentNullException(nameof(noticeSchedule));

        var existing = await _context.NoticeSchedules.FindAsync(noticeSchedule.notice_schedule_id);
        if (existing == null || existing.is_deleted)
            throw new InvalidOperationException($"NoticeSchedule with notice_schedule_id {noticeSchedule.notice_schedule_id} not found");

        noticeSchedule.date_updated = DateTime.UtcNow;
        noticeSchedule.modified_by_user_code = currentUserId;

        _context.Entry(existing).CurrentValues.SetValues(noticeSchedule);
        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int noticeScheduleId, int currentUserId)
    {
        var noticeSchedule = await _context.NoticeSchedules.FindAsync(noticeScheduleId);
        if (noticeSchedule != null)
        {
            noticeSchedule.is_deleted = true;
            noticeSchedule.date_updated = DateTime.UtcNow;
            noticeSchedule.modified_by_user_code = currentUserId;
            await _context.SaveChangesAsync();
        }
    }
}

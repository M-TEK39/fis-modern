using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for NoticeSchedule entity operations
/// </summary>
public interface INoticeScheduleRepository
{
    Task<NoticeSchedule?> GetByIdAsync(int noticeScheduleId);
    Task<IEnumerable<NoticeSchedule>> GetAllAsync();
    Task<IEnumerable<NoticeSchedule>> GetByNoticeIdAsync(int noticeId);
    Task<IEnumerable<NoticeSchedule>> GetActiveSchedulesAsync();
    Task<IEnumerable<NoticeSchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<NoticeSchedule> CreateAsync(NoticeSchedule noticeSchedule, int currentUserId);
    Task<NoticeSchedule> UpdateAsync(NoticeSchedule noticeSchedule, int currentUserId);
    Task DeleteAsync(int noticeScheduleId, int currentUserId);
}

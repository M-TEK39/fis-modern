using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for NoticeSchedule entity operations
/// </summary>
public interface INoticeScheduleRepository
{
    Task<NoticeSchedule?> GetByIdAsync(int noticeScheduleId);
    Task<IEnumerable<NoticeSchedule>> GetAllAsync();
    Task<NoticeSchedulePage> GetPageAsync(NoticeSchedulePageQuery query);
    Task<IEnumerable<NoticeSchedule>> GetByNoticeIdAsync(int noticeId);
    Task<IEnumerable<NoticeSchedule>> GetActiveSchedulesAsync();
    Task<IEnumerable<NoticeSchedule>> GetSchedulesByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    );
    Task<NoticeSchedule> CreateAsync(NoticeSchedule noticeSchedule, int currentUserId);
    Task<NoticeSchedule> UpdateAsync(NoticeSchedule noticeSchedule, int currentUserId);
    Task DeleteAsync(int noticeScheduleId, int currentUserId);
}

public sealed record NoticeSchedulePageQuery(
    int Page = 1,
    int PageSize = 24,
    string? Search = null,
    string? Status = null,
    DateTime CurrentDate = default
);

public sealed record NoticeSchedulePage(
    IReadOnlyList<NoticeSchedule> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

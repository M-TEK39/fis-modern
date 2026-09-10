using FIS.Core.Domain.Entities.System;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Repository interface for Notice entity operations
/// </summary>
public interface INoticeRepository
{
    Task<Notice?> GetByIdAsync(int noticeId);
    Task<IEnumerable<Notice>> GetAllAsync();
    Task<IEnumerable<Notice>> GetActiveNoticesAsync();
    Task<IEnumerable<Notice>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<Notice> CreateAsync(Notice notice, int currentUserId);
    Task<Notice> UpdateAsync(Notice notice, int currentUserId);
    Task DeleteAsync(int noticeId, int currentUserId);
}

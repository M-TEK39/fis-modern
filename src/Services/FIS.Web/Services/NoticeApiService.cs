using FIS.Web.Models;
using Microsoft.Extensions.Logging;

namespace FIS.Web.Services;

public class NoticeApiService(HttpClient httpClient, TokenService tokenService, ILogger<NoticeApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    public Task<List<NoticeScheduleDto>> GetSchedulesAsync() =>
        GetListAsync<NoticeScheduleDto>("api/notice-schedules");

    public Task DeleteScheduleAsync(int scheduleId) =>
        DeleteAsync($"api/notice-schedules/{scheduleId}");

    public Task<NoticeDetailDto?> GetNoticeAsync(int noticeId) =>
        GetAsync<NoticeDetailDto>($"api/notices/{noticeId}");

    public async Task<NoticeDetailDto?> SaveNoticeAsync(NoticeSaveRequest request)
    {
        if (request.NoticeId > 0)
        {
            return await PutAsync<NoticeSaveRequest, NoticeDetailDto>($"api/notices/{request.NoticeId}", request);
        }

        return await PostAsync<NoticeSaveRequest, NoticeDetailDto>("api/notices", request);
    }

    public async Task<NoticeScheduleDto?> SaveScheduleAsync(NoticeScheduleSaveRequest request)
    {
        if (request.NoticeScheduleId > 0)
        {
            return await PutAsync<NoticeScheduleSaveRequest, NoticeScheduleDto>($"api/notice-schedules/{request.NoticeScheduleId}", request);
        }

        return await PostAsync<NoticeScheduleSaveRequest, NoticeScheduleDto>("api/notice-schedules", request);
    }
}

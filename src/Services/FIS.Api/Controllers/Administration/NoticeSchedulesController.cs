using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/notice-schedules")]
[Authorize]
public class NoticeSchedulesController : BaseApiController
{
    private readonly INoticeScheduleRepository _noticeScheduleRepository;

    public NoticeSchedulesController(INoticeScheduleRepository noticeScheduleRepository)
    {
        _noticeScheduleRepository = noticeScheduleRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoticeScheduleDto>>> GetAll()
    {
        var schedules = await _noticeScheduleRepository.GetAllAsync();
        return Ok(schedules.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoticeScheduleDto>> GetById(int id)
    {
        var schedule = await _noticeScheduleRepository.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound(new { message = $"Notice schedule with ID {id} not found" });
        }

        return Ok(ToDto(schedule));
    }

    [HttpPost]
    public async Task<ActionResult<NoticeScheduleDto>> Create(
        [FromBody] CreateNoticeScheduleDto request
    )
    {
        var noticeSchedule = new NoticeSchedule
        {
            notice_id = request.NoticeId,
            title_field = request.TitleField,
            start_date = request.StartDate,
            end_date = request.EndDate,
            sort_order = request.SortOrder,
        };

        var created = await _noticeScheduleRepository.CreateAsync(
            noticeSchedule,
            GetCurrentUserId()
        );
        return Ok(ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoticeScheduleDto>> Update(
        int id,
        [FromBody] UpdateNoticeScheduleDto request
    )
    {
        if (id != request.NoticeScheduleId)
            return BadRequest("ID mismatch");

        var existing = await _noticeScheduleRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { message = $"Notice schedule with ID {id} not found" });

        existing.notice_id = request.NoticeId;
        existing.title_field = request.TitleField;
        existing.start_date = request.StartDate;
        existing.end_date = request.EndDate;
        existing.sort_order = request.SortOrder;

        var updated = await _noticeScheduleRepository.UpdateAsync(existing, GetCurrentUserId());
        return Ok(ToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var existing = await _noticeScheduleRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { message = $"Notice schedule with ID {id} not found" });

        await _noticeScheduleRepository.DeleteAsync(id, GetCurrentUserId());
        return Ok(new { message = "Notice schedule deleted successfully", id });
    }

    private static NoticeScheduleDto ToDto(NoticeSchedule schedule)
    {
        return new NoticeScheduleDto
        {
            NoticeScheduleId = schedule.notice_schedule_id,
            NoticeId = schedule.notice_id,
            TitleField = schedule.title_field ?? string.Empty,
            StartDate = schedule.start_date,
            EndDate = schedule.end_date,
            SortOrder = schedule.sort_order,
        };
    }
}

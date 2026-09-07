using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FIS.Api.Controllers;

[ApiController]
[Route("api/notices")]
[Authorize]
public class NoticesController : BaseApiController
{
    private readonly INoticeRepository _noticeRepository;

    public NoticesController(INoticeRepository noticeRepository)
    {
        _noticeRepository = noticeRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoticeDto>>> GetAll()
    {
        var notices = await _noticeRepository.GetAllAsync();
        return Ok(notices.Select(ToDto));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NoticeDto>> GetById(int id)
    {
        var notice = await _noticeRepository.GetByIdAsync(id);
        if (notice == null)
            return NotFound(new { message = $"Notice with ID {id} not found" });

        return Ok(ToDto(notice));
    }

    [HttpPost]
    public async Task<ActionResult<NoticeDto>> Create([FromBody] CreateNoticeDto request)
    {
        var notice = new Notice
        {
            notice_date = request.NoticeDate ?? DateTime.UtcNow,
            notice_from = request.NoticeFrom,
            notice_title = request.NoticeTitle,
            notice_body = request.NoticeBody,
            notice_person = request.NoticePerson,
            notice_person_title = request.NoticePersonTitle
        };

        var created = await _noticeRepository.CreateAsync(notice, GetCurrentUserId());
        return Ok(ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<NoticeDto>> Update(int id, [FromBody] UpdateNoticeDto request)
    {
        if (id != request.NoticeId)
            return BadRequest("ID mismatch");

        var existing = await _noticeRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { message = $"Notice with ID {id} not found" });

        existing.notice_date = request.NoticeDate;
        existing.notice_from = request.NoticeFrom;
        existing.notice_title = request.NoticeTitle;
        existing.notice_body = request.NoticeBody;
        existing.notice_person = request.NoticePerson;
        existing.notice_person_title = request.NoticePersonTitle;

        var updated = await _noticeRepository.UpdateAsync(existing, GetCurrentUserId());
        return Ok(ToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var existing = await _noticeRepository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { message = $"Notice with ID {id} not found" });

        await _noticeRepository.DeleteAsync(id, GetCurrentUserId());
        return Ok(new { message = "Notice deleted successfully", id });
    }

    private static NoticeDto ToDto(Notice notice)
    {
        return new NoticeDto
        {
            NoticeId = notice.notice_id,
            NoticeDate = notice.notice_date,
            NoticeFrom = notice.notice_from,
            NoticeTitle = notice.notice_title,
            NoticeBody = notice.notice_body,
            NoticePerson = notice.notice_person,
            NoticePersonTitle = notice.notice_person_title
        };
    }
}

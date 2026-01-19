using FIS.Core.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FIS.Api.Controllers;

/// <summary>
/// Email Notification API endpoints
/// Provides fleet-specific notifications and bulk email capabilities
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NotificationController : ControllerBase
{
    private readonly IEmailNotificationService _emailService;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(
        IEmailNotificationService emailService,
        ILogger<NotificationController> logger)
    {
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Basic Email Operations

    /// <summary>
    /// Send basic email
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendEmail([FromBody] EmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendEmailAsync(request.To, request.Subject, request.Body);

            if (success)
                return Ok(new { message = "Email sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send email" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {EmailAddress}", request.To);
            return StatusCode(500, new { error = "Failed to send email", message = ex.Message });
        }
    }

    /// <summary>
    /// Send HTML email
    /// </summary>
    [HttpPost("send/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendHtmlEmail([FromBody] EmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendHtmlEmailAsync(request.To, request.Subject, request.Body);

            if (success)
                return Ok(new { message = "HTML email sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send email" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending HTML email to {EmailAddress}", request.To);
            return StatusCode(500, new { error = "Failed to send email", message = ex.Message });
        }
    }

    /// <summary>
    /// Send email with attachments
    /// </summary>
    [HttpPost("send/attachments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendEmailWithAttachments([FromBody] EmailWithAttachmentsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendEmailWithAttachmentsAsync(
                request.To,
                request.Subject,
                request.Body,
                request.Attachments);

            if (success)
                return Ok(new { message = "Email with attachments sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send email" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email with attachments to {EmailAddress}", request.To);
            return StatusCode(500, new { error = "Failed to send email", message = ex.Message });
        }
    }

    #endregion

    #region Fleet-Specific Notifications

    /// <summary>
    /// Send maintenance reminder for a vehicle
    /// </summary>
    [HttpPost("maintenance/reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendMaintenanceReminder([FromBody] VehicleNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendMaintenanceReminderAsync(
                request.VmfCode,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Maintenance reminder sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send maintenance reminder" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance reminder for VMF {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Failed to send maintenance reminder", message = ex.Message });
        }
    }

    /// <summary>
    /// Send licence renewal reminder for a vehicle
    /// </summary>
    [HttpPost("licence/reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendLicenceReminder([FromBody] VehicleNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendLicenceReminderAsync(
                request.VmfCode,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Licence reminder sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send licence reminder" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending licence reminder for VMF {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Failed to send licence reminder", message = ex.Message });
        }
    }

    /// <summary>
    /// Send Certificate of Fitness (COF) reminder for a vehicle
    /// </summary>
    [HttpPost("cof/reminder")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendCofReminder([FromBody] VehicleNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendCofReminderAsync(
                request.VmfCode,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "COF reminder sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send COF reminder" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending COF reminder for VMF {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Failed to send COF reminder", message = ex.Message });
        }
    }

    /// <summary>
    /// Send contract expiry notification
    /// </summary>
    [HttpPost("contract/expiry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendContractExpiryNotification([FromBody] ContractNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendContractExpiryNotificationAsync(
                request.ContractId,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Contract expiry notification sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send contract expiry notification" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending contract expiry notification for contract {ContractId}", request.ContractId);
            return StatusCode(500, new { error = "Failed to send contract expiry notification", message = ex.Message });
        }
    }

    #endregion

    #region Report Distribution

    /// <summary>
    /// Send vehicle report via email
    /// </summary>
    [HttpPost("report/vehicle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendVehicleReport([FromBody] VehicleNotificationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendVehicleReportAsync(
                request.VmfCode,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Vehicle report sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send vehicle report" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending vehicle report for VMF {VmfCode}", request.VmfCode);
            return StatusCode(500, new { error = "Failed to send vehicle report", message = ex.Message });
        }
    }

    /// <summary>
    /// Send trip summary report via email
    /// </summary>
    [HttpPost("report/trip-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendTripSummaryReport([FromBody] DateRangeReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendTripSummaryReportAsync(
                request.VmfCode,
                request.StartDate,
                request.EndDate,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Trip summary report sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send trip summary report" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending trip summary report");
            return StatusCode(500, new { error = "Failed to send trip summary report", message = ex.Message });
        }
    }

    /// <summary>
    /// Send maintenance cost report via email
    /// </summary>
    [HttpPost("report/maintenance-cost")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendMaintenanceCostReport([FromBody] DateRangeReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _emailService.SendMaintenanceCostReportAsync(
                request.VmfCode,
                request.StartDate,
                request.EndDate,
                request.EmailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Maintenance cost report sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send maintenance cost report" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance cost report");
            return StatusCode(500, new { error = "Failed to send maintenance cost report", message = ex.Message });
        }
    }

    /// <summary>
    /// Send financial report via email
    /// </summary>
    [HttpPost("report/financial")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendFinancialReport([FromBody] FinancialReportRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Send to first email address (interface supports single recipient)
            var emailAddress = request.EmailAddresses.FirstOrDefault() ?? string.Empty;
            var success = await _emailService.SendFinancialReportAsync(
                request.ReportType,
                request.FinancialYear,
                emailAddress,
                request.RecipientName);

            if (success)
                return Ok(new { message = "Financial report sent successfully" });
            else
                return StatusCode(500, new { error = "Failed to send financial report" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending financial report");
            return StatusCode(500, new { error = "Failed to send financial report", message = ex.Message });
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Send bulk maintenance reminders
    /// </summary>
    [HttpPost("bulk/maintenance-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendBulkMaintenanceReminders()
    {
        try
        {
            var result = await _emailService.SendBulkMaintenanceRemindersAsync();
            return Ok(new { message = "Bulk maintenance reminders processed", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk maintenance reminders");
            return StatusCode(500, new { error = "Failed to send bulk maintenance reminders", message = ex.Message });
        }
    }

    /// <summary>
    /// Send bulk licence reminders
    /// </summary>
    [HttpPost("bulk/licence-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendBulkLicenceReminders()
    {
        try
        {
            var result = await _emailService.SendBulkLicenceRemindersAsync();
            return Ok(new { message = "Bulk licence reminders processed", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk licence reminders");
            return StatusCode(500, new { error = "Failed to send bulk licence reminders", message = ex.Message });
        }
    }

    /// <summary>
    /// Send bulk COF reminders
    /// </summary>
    [HttpPost("bulk/cof-reminders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendBulkCofReminders()
    {
        try
        {
            var result = await _emailService.SendBulkCofRemindersAsync();
            return Ok(new { message = "Bulk COF reminders processed", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk COF reminders");
            return StatusCode(500, new { error = "Failed to send bulk COF reminders", message = ex.Message });
        }
    }

    /// <summary>
    /// Send bulk contract expiry notifications
    /// </summary>
    [HttpPost("bulk/contract-expiry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendBulkContractExpiryNotifications()
    {
        try
        {
            var result = await _emailService.SendBulkContractExpiryNotificationsAsync();
            return Ok(new { message = "Bulk contract expiry notifications processed", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk contract expiry notifications");
            return StatusCode(500, new { error = "Failed to send bulk contract expiry notifications", message = ex.Message });
        }
    }

    #endregion

    #region Email Configuration

    /// <summary>
    /// Get email service configuration status
    /// </summary>
    [HttpGet("config/status")]
    [ProducesResponseType(typeof(EmailServiceStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetEmailServiceStatus()
    {
        try
        {
            var status = await _emailService.GetEmailServiceStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email service status");
            return StatusCode(500, new { error = "Failed to retrieve email service status", message = ex.Message });
        }
    }

    #endregion
}

#region Request DTOs

/// <summary>
/// Basic email request
/// </summary>
public class EmailRequest
{
    [Required]
    [EmailAddress]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Email request with attachments
/// </summary>
public class EmailWithAttachmentsRequest : EmailRequest
{
    public List<EmailAttachment> Attachments { get; set; } = new();
}

/// <summary>
/// Vehicle notification request
/// </summary>
public class VehicleNotificationRequest
{
    [Required]
    public int VmfCode { get; set; }

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    [Required]
    public string RecipientName { get; set; } = string.Empty;
}

/// <summary>
/// Contract notification request
/// </summary>
public class ContractNotificationRequest
{
    [Required]
    public int ContractId { get; set; }

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    [Required]
    public string RecipientName { get; set; } = string.Empty;
}

/// <summary>
/// Date range report request
/// </summary>
public class DateRangeReportRequest
{
    public int? VmfCode { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [EmailAddress]
    public string EmailAddress { get; set; } = string.Empty;

    [Required]
    public string RecipientName { get; set; } = string.Empty;
}

/// <summary>
/// Financial report request
/// </summary>
public class FinancialReportRequest
{
    [Required]
    public int FinancialYear { get; set; }

    [Required]
    public string ReportType { get; set; } = string.Empty;

    [Required]
    public List<string> EmailAddresses { get; set; } = new();

    [Required]
    public string RecipientName { get; set; } = string.Empty;
}

#endregion

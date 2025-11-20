namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Email notification service interface for Fleet Information System
/// Provides modern replacement for legacy email functionality that was broken in original system
/// Supports both simple notifications and rich HTML emails with attachments
/// </summary>
public interface IEmailNotificationService
{
    #region Simple Email Methods
    
    /// <summary>
    /// Send a simple text email to a single recipient
    /// </summary>
    Task<bool> SendEmailAsync(string to, string subject, string body);
    
    /// <summary>
    /// Send a simple text email to multiple recipients
    /// </summary>
    Task<bool> SendEmailAsync(List<string> to, string subject, string body);
    
    /// <summary>
    /// Send HTML email to a single recipient
    /// </summary>
    Task<bool> SendHtmlEmailAsync(string to, string subject, string htmlBody);
    
    /// <summary>
    /// Send HTML email to multiple recipients
    /// </summary>
    Task<bool> SendHtmlEmailAsync(List<string> to, string subject, string htmlBody);
    
    #endregion

    #region Email with Attachments
    
    /// <summary>
    /// Send email with file attachments (e.g., PDF reports)
    /// </summary>
    Task<bool> SendEmailWithAttachmentsAsync(
        string to, 
        string subject, 
        string body, 
        List<EmailAttachment> attachments);
    
    /// <summary>
    /// Send email with file attachments to multiple recipients
    /// </summary>
    Task<bool> SendEmailWithAttachmentsAsync(
        List<string> to, 
        string subject, 
        string body, 
        List<EmailAttachment> attachments);
    
    #endregion

    #region Fleet-Specific Notifications
    
    /// <summary>
    /// Send vehicle maintenance reminder notification
    /// </summary>
    Task<bool> SendMaintenanceReminderAsync(int vmfCode, string emailAddress, string recipientName);
    
    /// <summary>
    /// Send vehicle licence renewal reminder
    /// </summary>
    Task<bool> SendLicenceReminderAsync(int vmfCode, string emailAddress, string recipientName);
    
    /// <summary>
    /// Send Certificate of Fitness (COF) renewal reminder
    /// </summary>
    Task<bool> SendCofReminderAsync(int vmfCode, string emailAddress, string recipientName);
    
    /// <summary>
    /// Send vehicle report via email (PDF attachment)
    /// </summary>
    Task<bool> SendVehicleReportAsync(int vmfCode, string emailAddress, string recipientName);
    
    /// <summary>
    /// Send contract expiry notification
    /// </summary>
    Task<bool> SendContractExpiryNotificationAsync(int contractId, string emailAddress, string recipientName);
    
    /// <summary>
    /// Send trip summary report via email
    /// </summary>
    Task<bool> SendTripSummaryReportAsync(
        int? vmfCode, 
        DateTime startDate, 
        DateTime endDate, 
        string emailAddress, 
        string recipientName);
    
    /// <summary>
    /// Send maintenance cost report via email
    /// </summary>
    Task<bool> SendMaintenanceCostReportAsync(
        int? vmfCode, 
        DateTime startDate, 
        DateTime endDate, 
        string emailAddress, 
        string recipientName);
    
    /// <summary>
    /// Send financial report via email (income/billing reports)
    /// </summary>
    Task<bool> SendFinancialReportAsync(
        string reportType,
        int financialYear,
        string emailAddress, 
        string recipientName);
    
    #endregion

    #region Bulk Notifications
    
    /// <summary>
    /// Send bulk maintenance reminders for all vehicles due for service
    /// </summary>
    Task<BulkEmailResult> SendBulkMaintenanceRemindersAsync();
    
    /// <summary>
    /// Send bulk licence renewal reminders for all vehicles with licences expiring soon
    /// </summary>
    Task<BulkEmailResult> SendBulkLicenceRemindersAsync();
    
    /// <summary>
    /// Send bulk COF renewal reminders for all vehicles with COF expiring soon
    /// </summary>
    Task<BulkEmailResult> SendBulkCofRemindersAsync();
    
    /// <summary>
    /// Send bulk contract expiry notifications
    /// </summary>
    Task<BulkEmailResult> SendBulkContractExpiryNotificationsAsync();
    
    #endregion

    #region Email Templates and Configuration
    
    /// <summary>
    /// Get available email templates
    /// </summary>
    Task<List<EmailTemplate>> GetEmailTemplatesAsync();
    
    /// <summary>
    /// Get specific email template by name
    /// </summary>
    Task<EmailTemplate> GetEmailTemplateAsync(string templateName);
    
    /// <summary>
    /// Create or update email template
    /// </summary>
    Task<bool> SaveEmailTemplateAsync(EmailTemplate template);
    
    /// <summary>
    /// Test email configuration by sending test email
    /// </summary>
    Task<bool> TestEmailConfigurationAsync(string testEmailAddress);
    
    /// <summary>
    /// Get email service status and configuration
    /// </summary>
    Task<EmailServiceStatus> GetEmailServiceStatusAsync();
    
    #endregion
}

#region Supporting Models

/// <summary>
/// Email attachment model
/// </summary>
public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string? Description { get; set; }
}

/// <summary>
/// Email template model
/// </summary>
public class EmailTemplate
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public List<string> RequiredPlaceholders { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Bulk email operation result
/// </summary>
public class BulkEmailResult
{
    public int TotalEmails { get; set; }
    public int SuccessfulEmails { get; set; }
    public int FailedEmails { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
    public DateTime CompletedAt { get; set; }
}

/// <summary>
/// Email service status and configuration
/// </summary>
public class EmailServiceStatus
{
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool UseSSL { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public DateTime LastTestDate { get; set; }
    public bool LastTestSuccessful { get; set; }
    public string? LastErrorMessage { get; set; }
    public int DailyEmailsSent { get; set; }
    public int MonthlyEmailsSent { get; set; }
}

#endregion
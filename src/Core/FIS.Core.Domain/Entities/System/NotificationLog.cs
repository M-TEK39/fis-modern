using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Log of sent notifications with delivery status
/// </summary>
[Table("NotificationLog", Schema = "Workflow")]
public class NotificationLog
{
    [Key]
    [Column("LogID")]
    public int LogID { get; set; }

    [Column("NotificationID")]
    public int? NotificationID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("StepID")]
    public int? StepID { get; set; }

    [Column("StatusID")]
    public int? StatusID { get; set; }

    [Column("EventType")]
    [StringLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Column("RecipientEmail")]
    [StringLength(500)]
    public string RecipientEmail { get; set; } = string.Empty;

    [Column("Subject")]
    [StringLength(500)]
    public string? Subject { get; set; }

    [Column("Body")]
    public string? Body { get; set; }

    [Column("SentAt")]
    public DateTime? SentAt { get; set; }

    [Column("DeliveryStatus")]
    [StringLength(50)]
    public string DeliveryStatus { get; set; } = "Pending"; // Pending, Sent, Failed, Delivered

    [Column("ErrorMessage")]
    public string? ErrorMessage { get; set; }

    [Column("RetryCount")]
    public int RetryCount { get; set; } = 0;

    [Column("ExternalMessageId")]
    [StringLength(255)]
    public string? ExternalMessageId { get; set; } // SendGrid message ID

    // Global audit fields
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("NotificationID")]
    public virtual WorkflowNotification? Notification { get; set; }

    [ForeignKey("WorkflowID")]
    public virtual Workflow? Workflow { get; set; }

    [ForeignKey("StepID")]
    public virtual Step? Step { get; set; }

    [ForeignKey("StatusID")]
    public virtual Status? Status { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Represents a notification rule for workflow events
/// </summary>
[Table("WorkflowNotification", Schema = "Workflow")]
public class WorkflowNotification
{
    [Key]
    [Column("NotificationID")]
    public int NotificationID { get; set; }

    [Column("WorkflowID")]
    public int WorkflowID { get; set; }

    [Column("StepID")]
    public int? StepID { get; set; } // Null = workflow-level notification

    [Column("EventType")]
    [StringLength(50)]
    public string EventType { get; set; } = string.Empty; // WorkflowStarted, StepCompleted, WorkflowCompleted, Error

    [Column("RecipientType")]
    [StringLength(50)]
    public string RecipientType { get; set; } = string.Empty; // User, Role, Email

    [Column("RecipientIdentifier")]
    [StringLength(500)]
    public string RecipientIdentifier { get; set; } = string.Empty; // UserID, RoleName, or Email address

    [Column("NotificationTemplateID")]
    public int? NotificationTemplateID { get; set; }

    [Column("Subject")]
    [StringLength(500)]
    public string? Subject { get; set; }

    [Column("Body")]
    public string? Body { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("SendDelay")]
    public int? SendDelay { get; set; } // Delay in minutes before sending

    // Global audit fields
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("modified_by_user_code")]
    public int? modified_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("WorkflowID")]
    public virtual Workflow? Workflow { get; set; }

    [ForeignKey("StepID")]
    public virtual Step? Step { get; set; }

    [ForeignKey("NotificationTemplateID")]
    public virtual NotificationTemplate? NotificationTemplate { get; set; }

    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

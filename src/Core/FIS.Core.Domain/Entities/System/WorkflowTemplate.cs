using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Workflow template for creating reusable workflow definitions
/// </summary>
[Table("WorkflowTemplate", Schema = "Workflow")]
public class WorkflowTemplate
{
    [Key]
    [Column("TemplateID")]
    public int TemplateID { get; set; }

    [Column("TemplateName")]
    [StringLength(255)]
    [Required]
    public string TemplateName { get; set; } = string.Empty;

    [Column("Category")]
    [StringLength(100)]
    public string? Category { get; set; }

    [Column("Description")]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [Column("TemplateData")]
    public string? TemplateData { get; set; } // JSON structure of workflow definition

    [Column("Version")]
    public int Version { get; set; } = 1;

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
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

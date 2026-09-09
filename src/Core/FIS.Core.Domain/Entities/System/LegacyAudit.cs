using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.System;

/// <summary>
/// Compatibility mapping for the legacy dbo.Audit table. Expanded databases
/// use Workflow.Audit, while some client databases retain the dbo table.
/// </summary>
[Table("Audit", Schema = "dbo")]
public class LegacyAudit
{
    [Key]
    [Column("AuditID")]
    public int AuditID { get; set; }

    [Column("Action")]
    [StringLength(100)]
    public string? Action { get; set; }

    [Column("TableName")]
    [StringLength(200)]
    public string? TableName { get; set; }

    [Column("PrimaryKey")]
    [StringLength(200)]
    public string? PrimaryKey { get; set; }

    [Column("Changes")]
    public string? Changes { get; set; }

    [Column("ActionedBy")]
    [StringLength(200)]
    public string? ActionedBy { get; set; }

    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("modified_by_user_code")]
    public int? modified_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; }
}

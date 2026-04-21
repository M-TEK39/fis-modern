using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("EduCodes")]
public class EduCode
{
    // Legacy schema doesn't have a PK for EduCode in Database.cs?
    // Let's re-examine Database.cs for EduCode
    // [TableName("dbo.EduCode")]
    // [ExplicitColumns]
    // public partial class EduCode : GG_DB.Record<EduCode>

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Adding a modern PK as per MANDATE: "Add Missing Primary Keys"

    [Column("vmf_Code")]
    public int? vmf_Code { get; set; }

    [Column("Registration")]
    [StringLength(20)]
    public string? Registration { get; set; }

    [Column("RespNumber")]
    [StringLength(20)]
    public string? RespNumber { get; set; }

    [Column("RespName")]
    [StringLength(255)]
    public string? RespName { get; set; }

    [Column("ObjNumber")]
    [StringLength(20)]
    public string? ObjNumber { get; set; }

    [Column("ObjName")]
    [StringLength(255)]
    public string? ObjName { get; set; }

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

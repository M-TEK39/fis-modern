using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.WorkshopEntities;

[Table("maint_profile_model")]
public class MaintenanceProfileModel
{
    // Legacy schema doesn't have a PK for maint_profile_model in Database.cs?
    // It seems to be a mapping table.
    // [TableName("dbo.maint_profile_model")]
    // [ExplicitColumns]
    // public partial class maint_profile_model : GG_DB.Record<maint_profile_model>

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Adding PK for modern app

    [Column("profile_code")]
    public short profile_code { get; set; }

    [Column("model_code")]
    public short model_code { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string? description { get; set; }

    [Column("trigger_type")]
    [StringLength(50)]
    public string? trigger_type { get; set; }

    [Column("trigger_description")]
    [StringLength(255)]
    public string? trigger_description { get; set; }

    [Column("interval")]
    public int? interval { get; set; }

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

    [ForeignKey("model_code")]
    public virtual Model? Model { get; set; }
}

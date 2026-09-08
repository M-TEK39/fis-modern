using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("VehiclePhotoInfo")]
public class VehiclePhoto
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("VehiclePhotoInfoCode")]
    public int VehiclePhotoInfoCode { get; set; }

    [Column("VehicleMasterCode")]
    public int VehicleMasterCode { get; set; }

    [StringLength(500)]
    [Column("FileUrl")]
    public string? FileUrl { get; set; }

    [Column("Orientation")]
    public int? Orientation { get; set; }

    [StringLength(200)]
    [Column("Description")]
    public string? Description { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

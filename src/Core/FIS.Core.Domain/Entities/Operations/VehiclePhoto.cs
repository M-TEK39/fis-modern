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
}

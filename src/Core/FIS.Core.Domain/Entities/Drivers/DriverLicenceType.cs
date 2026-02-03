using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Drivers;

[Table("driver_licence_types")]
public class DriverLicenceType
{
    [Key]
    [Column("driver_licence_type_id")]
    public int driver_licence_type_id { get; set; }

    [Column("driver_licence_type_code")]
    [StringLength(50)]
    public string? driver_licence_type_code { get; set; }

    [Column("driver_licence_type_description")]
    [StringLength(255)]
    public string? driver_licence_type_description { get; set; }

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

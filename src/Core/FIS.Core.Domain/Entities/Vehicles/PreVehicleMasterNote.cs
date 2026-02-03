using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Vehicles;

[Table("PreVehicle_Master_notes")]
public class PreVehicleMasterNote
{
    [Key]
    [Column("Comment_ID")]
    public short Comment_ID { get; set; }

    [Column("Temp_vmf_code")]
    public int Temp_vmf_code { get; set; }

    [Column("Comment_Date")]
    public DateTime date_created { get; set; } // Mapping to modern audit property

    [Column("Commented_By_User_Code")]
    public int? created_by_user_code { get; set; } // Mapping to modern audit property, fixing type

    [Column("Comment")]
    public string? Comment { get; set; }

    // Other standard audit fields
    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

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
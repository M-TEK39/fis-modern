using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.ReferenceData;

[Table("GG_Blocks")]
public class GgBlock
{
    [Key]
    [Column("Block_ID")]
    public short Block_ID { get; set; }

    [Column("Creation_Date")]
    public DateTime date_created { get; set; } // Mapping legacy to modern audit property name

    [Column("Created_By_User_Code")]
    public int? created_by_user_code { get; set; } // Mapping legacy to modern audit property name, fixing type

    [Column("Vch_Start_Reg")]
    [StringLength(50)]
    public string? Vch_Start_Reg { get; set; }

    [Column("Vch_End_Reg")]
    [StringLength(50)]
    public string? Vch_End_Reg { get; set; }

    [Column("Modified_User_Code")]
    public int? modified_by_user_code { get; set; } // Mapping legacy to modern audit property name, fixing type

    // Standard audit fields that DON'T exist in legacy
    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}
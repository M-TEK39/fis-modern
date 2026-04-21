using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Integration;

[Table("GG_Block")]
public class GGBlock
{
    [Key]
    [Column("Block_ID")]
    public short Block_ID { get; set; }

    [Column("Creation_Date")]
    public DateTime? Creation_Date { get; set; }

    [Column("Created_By_User_Code")]
    public short Created_By_User_Code { get; set; }

    [Column("Vch_Start_Reg")]
    [StringLength(50)]
    public string? Vch_Start_Reg { get; set; }

    [Column("Vch_End_Reg")]
    [StringLength(50)]
    public string? Vch_End_Reg { get; set; }

    [Column("Modified_User_Code")]
    public short Modified_User_Code { get; set; }

    // Global audit fields
    [Column("audit_date_created")]
    public DateTime date_created { get; set; }

    [Column("audit_date_updated")]
    public DateTime? date_updated { get; set; }

    [Column("audit_created_by_user_code")]
    public int? created_by_user_code { get; set; }

    [Column("audit_modified_by_user_code")]
    public int? modified_by_user_code { get; set; }

    [Column("is_deleted")]
    public bool is_deleted { get; set; } = false;

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

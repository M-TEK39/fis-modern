using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Integration;

[Table("block_gg_numbers")]
public class BlockGGNumber
{
    [Key]
    [Column("Block_GEN_ID")]
    public short Block_GEN_ID { get; set; }

    [Column("Block_ID")]
    public short Block_ID { get; set; }

    [Column("Creation_Date")]
    public DateTime? Creation_Date { get; set; }

    [Column("Created_By_User_Code")]
    public short Created_By_User_Code { get; set; }

    [Column("GG_Number")]
    [StringLength(50)]
    public string? GG_Number { get; set; }

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

    [ForeignKey("Block_ID")]
    public virtual GGBlock? Block { get; set; }
}

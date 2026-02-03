using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;
using FIS.Core.Domain.Entities.Vehicles;

namespace FIS.Core.Domain.Entities.Financial;

[Table("Surcharge")]
public class Surcharge
{
    [Key]
    [Column("surcharge_code")]
    public int surcharge_code { get; set; }

    [Column("Department")]
    [StringLength(255)]
    public string? Department { get; set; }

    [Column("Site_code")]
    public short? Site_code { get; set; }

    [Column("RegNo1")]
    [StringLength(50)]
    public string? RegNo1 { get; set; }

    [Column("RegNo2")]
    [StringLength(50)]
    public string? RegNo2 { get; set; }

    [Column("Same")]
    [StringLength(10)]
    public string? Same { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("Merchant")]
    [StringLength(255)]
    public string? Merchant { get; set; }

    [Column("TrxDate")]
    public DateTime TrxDate { get; set; }

    [Column("ServiceType")]
    [StringLength(50)]
    public string? ServiceType { get; set; }

    [Column("AuthorityNo")]
    public int AuthorityNo { get; set; }

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

    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }
}

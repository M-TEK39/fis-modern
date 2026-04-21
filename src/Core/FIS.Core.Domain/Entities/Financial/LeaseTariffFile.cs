using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("LeaseTariff_File")]
public class LeaseTariffFile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("No")]
    public short? No { get; set; }

    [Column("VMF_Code")]
    public int? VMF_Code { get; set; }

    [Column("GGNumber")]
    [StringLength(50)]
    public string? GGNumber { get; set; }

    [Column("GPNumber")]
    [StringLength(50)]
    public string? GPNumber { get; set; }

    [Column("Start_Date")]
    public DateTime? Start_Date { get; set; }

    [Column("End_Date")]
    public DateTime? End_Date { get; set; }

    [Column("Fixed_Tariff")]
    public decimal? Fixed_Tariff { get; set; }

    [Column("Excess_Kilo_Tariff")]
    public decimal? Excess_Kilo_Tariff { get; set; }

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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// PrivateHire Entity - Legacy compatibility for Private_hire table
/// Maps to the actual legacy Private_hire schema from Database.cs
/// </summary>
[Table("Private_hire")]
public class PrivateHire
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("PHV_code")]
    public short PHV_code { get; set; }

    [Column("registration_number")]
    public string? registration_number { get; set; }

    [Column("model_code")]
    public short model_code { get; set; }

    [Column("site_code")]
    public int site_code { get; set; }

    [Column("contracted_to")]
    public int? contracted_to { get; set; }

    [Column("engine_number")]
    public string? engine_number { get; set; }

    [Column("chassis_number")]
    public string? chassis_number { get; set; }

    [Column("year_manufactured")]
    public string? year_manufactured { get; set; }

    [Column("bank_code")]
    public string? bank_code { get; set; }

    [Column("colour")]
    public string? colour { get; set; }

    [Column("tank_capacity")]
    public int? tank_capacity { get; set; }

    [Column("contractor_id")]
    public short contractor_id { get; set; }

    [Column("fuel_card")]
    public string? fuel_card { get; set; }

    [Column("fuel_card_receiver")]
    public string? fuel_card_receiver { get; set; }

    [Column("take_on_date")]
    public DateTime take_on_date { get; set; }

    [Column("take_on_odo")]
    public int take_on_odo { get; set; }

    [Column("return_date")]
    public DateTime? return_date { get; set; }

    [Column("return_odo")]
    public int return_odo { get; set; }

    [Column("km_tariff")]
    public decimal? km_tariff { get; set; }

    [Column("daily_tariff")]
    public decimal? daily_tariff { get; set; }

    [Column("hourly_tariff")]
    public decimal? hourly_tariff { get; set; }

    [Column("model_desc")]
    public string? model_desc { get; set; }

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

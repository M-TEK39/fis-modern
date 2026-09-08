using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Maintenance cost matrix for modern tariff system (fin schema).
/// Defines maintenance costs by vehicle class and age.
/// Maps to legacy fin.MaintenanceValue table.
/// </summary>
[Table("MaintenanceValue", Schema = "fin")]
public class MaintenanceValue
{
    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("class_code")]
    public short class_code { get; set; }

    [Column("months_age")]
    public short months_age { get; set; }

    [Column("class_number")]
    public string class_number { get; set; } = string.Empty;

    [Column("kilometer_age")]
    public int kilometer_age { get; set; }

    [Column("amount")]
    public decimal amount { get; set; }

    [Column("RandPerKilometer")]
    public decimal RandPerKilometer { get; set; }

    [Column("CaptureDate")]
    public DateTime CaptureDate { get; set; }

    [Column("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("user_access_name")]
    public string? user_access_name { get; set; }

    // Navigation properties
    public virtual TariffParameter? TariffParameter { get; set; }

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

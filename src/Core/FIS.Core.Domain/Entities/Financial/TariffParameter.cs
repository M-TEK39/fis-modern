using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Tariff parameters for modern tariff system (post-2009, fin schema).
/// Contains annual tariff configuration parameters.
/// Maps to legacy fin.TariffParameter table.
/// </summary>
[Table("TariffParameter", Schema = "fin")]
public class TariffParameter
{
    [Key]
    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("TariffParameterYear")]
    public int TariffParameterYear { get; set; }

    [Column("AnnualInterestRatePercentage")]
    public decimal AnnualInterestRatePercentage { get; set; }

    [Column("AnnualPayments")]
    public byte AnnualPayments { get; set; }

    [Column("EffectiveInterestRate")]
    public decimal? EffectiveInterestRate { get; set; }

    [Column("PoolVehicleChargedDaysPerMonth")]
    public byte PoolVehicleChargedDaysPerMonth { get; set; }

    [Column("CostCategoryMultiple")]
    public int CostCategoryMultiple { get; set; }

    [Column("AnnualRecoveredKilos")]
    public int? AnnualRecoveredKilos { get; set; }

    [Column("AverageFuelPrice")]
    public decimal? AverageFuelPrice { get; set; }

    [Column("EffectiveDate")]
    public DateTime? EffectiveDate { get; set; }

    [Column("CaptureDate")]
    public DateTime CaptureDate { get; set; }

    [Column("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("user_access_name")]
    public string? user_access_name { get; set; }

    [Column("Approved")]
    public bool Approved { get; set; }

    [Column("ApprovalDate")]
    public DateTime? ApprovalDate { get; set; }

    [Column("Approval_user_access_code")]
    public short? Approval_user_access_code { get; set; }

    [Column("Approval_user_access_name")]
    public string? Approval_user_access_name { get; set; }

    // Navigation properties
    public virtual ICollection<VehicleTariff> VehicleTariffs { get; set; } =
        new List<VehicleTariff>();
    public virtual ICollection<MaintenanceValue> MaintenanceValues { get; set; } =
        new List<MaintenanceValue>();
    public virtual ICollection<Overhead> Overheads { get; set; } = new List<Overhead>();
    public virtual ICollection<TariffWeightCalculation> WeightCalculations { get; set; } =
        new List<TariffWeightCalculation>();

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

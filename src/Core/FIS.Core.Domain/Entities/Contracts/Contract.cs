using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Contract Entity - EXACT legacy schema match for contract table
/// Uses exact field names from Database.cs - NO modernization
/// CRITICAL: This table needs unique constraint on (vmf_code, still_current) where still_current = 'Y'
/// </summary>
[Table("contract")]
public class Contract
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("contract_code")]
    public int contract_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("site_code")]
    public short site_code { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("start_time")]
    public DateTime start_time { get; set; }

    [Column("end_date")]
    public DateTime? end_date { get; set; }

    [Column("end_time")]
    public DateTime? end_time { get; set; }

    [Column("start_odometer")]
    public int start_odometer { get; set; }

    [Column("end_odometer")]
    public int? end_odometer { get; set; }

    [Column("still_current")]
    public string? still_current { get; set; }

    [Column("contract_type")]
    public string? contract_type { get; set; }

    [Column("Driver_id")]
    public string? Driver_id { get; set; }

    [Column("Authorisation")]
    public string? Authorisation { get; set; }

    [Column("Driver_name")]
    public string? Driver_name { get; set; }

    [Column("Notes")]
    public string? Notes { get; set; }

    [Column("target_return_date")]
    public DateTime? target_return_date { get; set; }

    [Column("user_code")]
    public short? user_code { get; set; }

    [Column("Charged_Until")]
    public DateTime? Charged_Until { get; set; }

    [Column("bas_objective_code")]
    public string? bas_objective_code { get; set; }

    [Column("bas_responsibility_code")]
    public string? bas_responsibility_code { get; set; }

    [Column("relief_for_contract")]
    public int? relief_for_contract { get; set; }

    [Column("locked_for_transfer")]
    public bool locked_for_transfer { get; set; }

    [Column("hours_used")]
    public short? hours_used { get; set; }

    [Column("bas_project_number")]
    public string? bas_project_number { get; set; }

    [Column("journal_detail_code")]
    public Guid? journal_detail_code { get; set; }

    [Column("parent_contract_code")]
    public int? parent_contract_code { get; set; }

    [Column("contract_group_code")]
    public int? contract_group_code { get; set; }

    [Column("bas_fund_code")]
    public string? bas_fund_code { get; set; }

    [Column("monthly_km")]
    public int? monthly_km { get; set; }

    [Column("contract_status_code")]
    public short? contract_status_code { get; set; }

    [Column("contract_status_date")]
    public DateTime? contract_status_date { get; set; }

    [Column("vehicle_assessment_code")]
    public int? vehicle_assessment_code { get; set; }

    [Column("approver_code")]
    public int? approver_code { get; set; }

    [Column("site_driver_code")]
    public int? site_driver_code { get; set; }

    [Column("collector_firstname")]
    public string? collector_firstname { get; set; }

    // Navigation properties (using legacy foreign key names)
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("site_code")]
    public virtual Site? Site { get; set; }

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

    // Legacy field compatibility - add computed properties for backward compatibility
    public string? StillCurrentFlag => still_current; // Computed property for backward compatibility
    public int ContractCode => contract_code; // Computed property for backward compatibility
    public DateTime StartDate => start_date; // Computed property for backward compatibility
}

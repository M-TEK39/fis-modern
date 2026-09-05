using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Site Entity - EXACT legacy schema match for site table
/// Uses exact field names from Database.cs - NO modernization
/// Note: Contains legacy typo "Depatrment_code" - kept for compatibility
/// </summary>
[Table("site")]
public class Site
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("Site_code")]
    public short Site_code { get; set; }

    [Column("Depatrment_code")] // Note: Legacy typo kept intentionally
    public short? Depatrment_code { get; set; }

    [Column("description")]
    public string? description { get; set; }

    [Column("res_person")]
    public string? res_person { get; set; }

    [Column("address1")]
    public string? address1 { get; set; }

    [Column("address2")]
    public string? address2 { get; set; }

    [Column("address3")]
    public string? address3 { get; set; }

    [Column("postal_code")]
    public string? postal_code { get; set; }

    [Column("telephone")]
    public string? telephone { get; set; }

    [Column("fax")]
    public string? fax { get; set; }

    [Column("net_address")]
    public string? net_address { get; set; }

    [Column("Department_number")]
    public string? Department_number { get; set; }

    [Column("Map_reference")]
    public string? Map_reference { get; set; }

    [Column("Map_description")]
    public string? Map_description { get; set; }

    [Column("cell_number")]
    public string? cell_number { get; set; }

    [Column("site_active")]
    public bool site_active { get; set; }

    [Column("telephone2")]
    public string? telephone2 { get; set; }

    [Column("fax1")]
    public string? fax1 { get; set; }

    [Column("financial_system_code")]
    public byte? financial_system_code { get; set; }

    [Column("financial_system_active")]
    public bool? financial_system_active { get; set; }

    [Column("financial_system_activate_date")]
    public DateTime? financial_system_activate_date { get; set; }

    [Column("export_is_active")]
    public bool? export_is_active { get; set; }

    [Column("date_last_exported")]
    public DateTime? date_last_exported { get; set; }

    [Column("Service_Kilometres")]
    public int Service_Kilometres { get; set; }

    [Column("Service_Years")]
    public byte Service_Years { get; set; }

    [Column("Overhead_Percentage")]
    public decimal Overhead_Percentage { get; set; }

    [Column("province_code")]
    public byte? province_code { get; set; }

    [Column("notes")]
    public string? notes { get; set; }

    [Column("user_access_code")]
    public int? user_access_code { get; set; }

    [Column("date_created")]
    public DateTime date_created { get; set; }

    // Navigation properties
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
    

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

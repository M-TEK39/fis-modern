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

    [Column("date_created")]
    public DateTime date_created { get; set; }

    // Navigation properties
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public virtual ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Trip Entity - Legacy compatibility for trip_authorities table
/// Maps to the actual legacy trip_authorities schema from Database.cs
/// </summary>
[Table("trip_authorities")]
public class Trip
{
    [Key]
    [Column("trip_authority_code")]
    public int trip_authority_code { get; set; }

    [Column("contract_code")]
    public int contract_code { get; set; }

    [Column("approver_name")]
    public string? approver_name { get; set; }

    [Column("approver_rank")]
    public string? approver_rank { get; set; }

    [Column("approver_tel")]
    public string? approver_tel { get; set; }

    [Column("end_odo_meter")]
    public int? end_odo_meter { get; set; }

    [Column("expiry_date")]
    public DateTime? expiry_date { get; set; }

    [Column("trip_reason")]
    public string? trip_reason { get; set; }

    [Column("trip_request_number")]
    public string? trip_request_number { get; set; }

    [Column("issue_date")]
    public DateTime issue_date { get; set; }

    [Column("trip_type_code")]
    public short trip_type_code { get; set; }

    [Column("trip_incident_type_code")]
    public short trip_incident_type_code { get; set; }

    [Column("user_access_code")]
    public short? user_access_code { get; set; }

    [Column("locked_for_transfer")]
    public bool locked_for_transfer { get; set; }

    [Column("Trip_Is_Monthly")]
    public bool Trip_Is_Monthly { get; set; }

    // Navigation properties
    public virtual Contract? Contract { get; set; }
}
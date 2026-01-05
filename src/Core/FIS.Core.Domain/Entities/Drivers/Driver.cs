using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// Driver Entity - Legacy compatibility for site_drivers table
/// Maps to the actual legacy site_drivers schema from Database.cs
/// </summary>
[Table("site_drivers")]
public class Driver
{
    [Key]
    [Column("site_driver_code")]
    public int site_driver_code { get; set; }

    [Column("site_code")]
    public int site_code { get; set; }

    [Column("driver_licence_type_id")]
    public int driver_licence_type_id { get; set; }

    [Column("driver_surname")]
    public string? driver_surname { get; set; }

    [Column("driver_firstname")]
    public string? driver_firstname { get; set; }

    [Column("driver_SA_id")]
    public string? driver_SA_id { get; set; }

    [Column("driver_passportnumber")]
    public string? driver_passportnumber { get; set; }

    [Column("driver_persalnumber")]
    public string? driver_persalnumber { get; set; }

    [Column("driver_contractnumber")]
    public string? driver_contractnumber { get; set; }

    [Column("driver_licence_number")]
    public string? driver_licence_number { get; set; }

    [Column("driver_licence_issuedate")]
    public DateTime driver_licence_issuedate { get; set; }

    [Column("driver_licence_lastVerifiedDate")]
    public DateTime driver_licence_lastVerifiedDate { get; set; }

    [Column("driver_hasPDP")]
    public bool driver_hasPDP { get; set; }

    [Column("driver_PDP_ExpiryDate")]
    public DateTime? driver_PDP_ExpiryDate { get; set; }

    [Column("driver_licence_ExpiryDate")]
    public DateTime? driver_licence_ExpiryDate { get; set; }

    [Column("driver_active")]
    public bool driver_active { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("bookings")]
public class Booking
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("booking_id")]
    public short booking_id { get; set; }

    [Column("site_code")]
    public string? site_code { get; set; }

    [Column("name")]
    public string? name { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime? end_date { get; set; }

    [Column("class_code")]
    public short class_code { get; set; }

    [Column("user_id")]
    public short user_id { get; set; }

    [Column("booking_date")]
    public DateTime booking_date { get; set; }

    [Column("telephone")]
    public string? telephone { get; set; }

    [Column("collected")]
    public short? collected { get; set; }

    [Column("location_code")]
    public int location_code { get; set; }

    [Column("vmf_code")]
    public int? vmf_code { get; set; }

    [Column("booking_status")]
    public string? booking_status { get; set; }

    [Column("notes")]
    public string? notes { get; set; }

    // Navigation properties
    [ForeignKey("vmf_code")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("location_code")]
    public virtual Location? Location { get; set; }
}

namespace FIS.Web.Models;

public class BookingDto
{
    public short booking_id { get; set; }
    public string? site_code { get; set; }
    public string? name { get; set; }
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public short class_code { get; set; }
    public short user_id { get; set; }
    public DateTime booking_date { get; set; }
    public string? telephone { get; set; }
    public short? collected { get; set; }
    public int location_code { get; set; }
    public int? vmf_code { get; set; }
    public string? booking_status { get; set; }
    public string? notes { get; set; }
}

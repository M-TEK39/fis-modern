using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Logistics;

[Table("route_details")]
public class RouteDetail
{
    [Key]
    [Column("route_code")]
    public int route_code { get; set; }

    [Column("trip_authority_code")]
    public int trip_authority_code { get; set; }

    [Column("start_date")]
    public DateTime start_date { get; set; }

    [Column("end_date")]
    public DateTime end_date { get; set; }

    [Column("start_odo_meter")]
    public int? start_odo_meter { get; set; }

    [Column("end_odo_meter")]
    public int? end_odo_meter { get; set; }

    [Column("bas_responsibility_code")]
    [StringLength(50)]
    public string? bas_responsibility_code { get; set; }

    [Column("bas_object_code")]
    [StringLength(50)]
    public string? bas_object_code { get; set; }

    [Column("start_route_location_name")]
    [StringLength(255)]
    public string? start_route_location_name { get; set; }

    [Column("end_route_location_name")]
    [StringLength(255)]
    public string? end_route_location_name { get; set; }

    [Column("estimated_distance")]
    public int? estimated_distance { get; set; }

    [Column("bas_journal_record_code")]
    public long? bas_journal_record_code { get; set; }

    // Global audit fields
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

    // Navigation properties
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

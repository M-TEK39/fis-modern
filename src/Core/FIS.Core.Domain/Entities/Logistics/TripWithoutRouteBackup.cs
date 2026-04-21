using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Logistics;

[Table("TripsWithoutRoutes_Backup")]
public class TripWithoutRouteBackup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; } // Surrogate PK

    [Column("trip_authority_code")]
    public int trip_authority_code { get; set; }

    [Column("contract_code")]
    public int contract_code { get; set; }

    [Column("approver_name")]
    [StringLength(255)]
    public string? approver_name { get; set; }

    [Column("approver_rank")]
    [StringLength(255)]
    public string? approver_rank { get; set; }

    [Column("approver_tel")]
    [StringLength(50)]
    public string? approver_tel { get; set; }

    [Column("end_odo_meter")]
    public int? end_odo_meter { get; set; }

    [Column("expiry_date")]
    public DateTime? expiry_date { get; set; }

    [Column("trip_reason")]
    [StringLength(255)]
    public string? trip_reason { get; set; }

    [Column("trip_request_number")]
    [StringLength(50)]
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

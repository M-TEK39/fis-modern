using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Maintenance;

[Table("Maintenance_Value_History")]
public class MaintenanceValueHistory
{
    [Key]
    [Column("Maintenance_Value_History_ID")]
    public int Maintenance_Value_History_ID { get; set; }

    [Column("TariffParameterID")]
    public int TariffParameterID { get; set; }

    [Column("class_code")]
    public short class_code { get; set; }

    [Column("months_age")]
    public short months_age { get; set; }

    [Column("amount")]
    public decimal amount { get; set; }

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

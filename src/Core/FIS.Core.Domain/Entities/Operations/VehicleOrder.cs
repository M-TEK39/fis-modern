using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace FIS.Core.Domain.Entities;

[Table("Vehicle_orders")]
public class VehicleOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("order_id")]
    public int order_id { get; set; }

    [Column("make_code")]
    public short? make_code { get; set; }

    [Column("model_code")]
    public short model_code { get; set; }

    [Column("quantity")]
    public short quantity { get; set; }

    [Column("supplier_id")]
    public short supplier_id { get; set; }

    [StringLength(100)]
    [Column("order_number")]
    public string? order_number { get; set; }
}

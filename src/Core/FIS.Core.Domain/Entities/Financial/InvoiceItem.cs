using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Invoice line item - detailed charges for each vehicle/contract.
/// Maps to legacy dbo.invoice_item table.
/// </summary>
[Table("invoice_item")]
public class InvoiceItem
{
    [Key]
    [Column("item_code")]
    public int item_code { get; set; }

    [Column("invoice_code")]
    public int invoice_code { get; set; }

    [Column("vmf_code")]
    public int vmf_code { get; set; }

    [Column("site_code")]
    public short site_code { get; set; }

    [Column("fixed_tariff_amount")]
    public decimal fixed_tariff_amount { get; set; }

    [Column("start_odometer")]
    public int start_odometer { get; set; }

    [Column("start_odo_derived")]
    public string start_odo_derived { get; set; } = string.Empty;

    [Column("start_odo_date")]
    public DateTime? start_odo_date { get; set; }

    [Column("end_odometer")]
    public int end_odometer { get; set; }

    [Column("end_odo_derived")]
    public string end_odo_derived { get; set; } = string.Empty;

    [Column("end_odo_date")]
    public DateTime? end_odo_date { get; set; }

    [Column("odo_tariff_amount")]
    public decimal odo_tariff_amount { get; set; }

    [Column("driver_rate")]
    public decimal driver_rate { get; set; }

    [Column("contract_start_date")]
    public DateTime contract_start_date { get; set; }

    [Column("contract_end_date")]
    public DateTime? contract_end_date { get; set; }

    [Column("contract_type")]
    public string contract_type { get; set; } = string.Empty;

    [Column("contract_start_time")]
    public DateTime? contract_start_time { get; set; }

    [Column("contract_end_time")]
    public DateTime? contract_end_time { get; set; }

    // Cost component fields
    [Column("cost_replacement")]
    public decimal? cost_replacement { get; set; }

    [Column("provision_overhead")]
    public decimal? provision_overhead { get; set; }

    [Column("cost_overhead")]
    public decimal? cost_overhead { get; set; }

    [Column("provision_loss")]
    public decimal? provision_loss { get; set; }

    [Column("provision_accident")]
    public decimal? provision_accident { get; set; }

    [Column("provision_profit")]
    public decimal? provision_profit { get; set; }

    [Column("provision_replacement")]
    public decimal? provision_replacement { get; set; }

    [Column("variable_cost")]
    public decimal? variable_cost { get; set; }

    // Navigation properties
    public virtual Invoice? Invoice { get; set; }
}

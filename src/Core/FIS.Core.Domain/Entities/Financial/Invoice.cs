using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Invoice header - monthly invoice per department.
/// Maps to legacy dbo.invoice table.
/// </summary>
[Table("invoice")]
public class Invoice
{
    [Key]
    [Column("invoice_code")]
    public int invoice_code { get; set; }

    [Column("posting_month_code")]
    public short posting_month_code { get; set; }

    [Column("department_code")]
    public short department_code { get; set; }

    // Navigation properties
    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}

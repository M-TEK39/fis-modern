using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

[Table("Suppliers")]
public class Supplier
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("supplier_id")]
    public short supplier_id { get; set; }

    [Column("name")]
    public string? name { get; set; }

    [Column("address")]
    public string? address { get; set; }

    [Column("tel")]
    public string? tel { get; set; }

    [Column("fax")]
    public string? fax { get; set; }

    [Column("contact_person")]
    public string? contact_person { get; set; }

    [Column("email")]
    public string? email { get; set; }

    [Column("supplier_type")]
    public string? supplier_type { get; set; }

    [Column("active")]
    public bool? active { get; set; }
}

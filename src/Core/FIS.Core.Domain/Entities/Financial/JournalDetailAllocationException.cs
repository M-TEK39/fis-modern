using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FIS.Core.Domain.Entities.Auth;

namespace FIS.Core.Domain.Entities.Financial;

[Table("journal_detail_allocation_exception")]
public class JournalDetailAllocationException
{
    [Key]
    [Column("journal_detail_allocation_exception_code")]
    public int journal_detail_allocation_exception_code { get; set; }

    [Column("journal_detail_code")]
    public Guid journal_detail_code { get; set; }

    [Column("journal_detail_allocation_exception_date_created")]
    public DateTime journal_detail_allocation_exception_date_created { get; set; }

    [Column("created_by_user_code")]
    public int created_by_user_code { get; set; }

    [Column("is_system_user")]
    public bool is_system_user { get; set; }

    [Column("new_responsibility_code")]
    [StringLength(50)]
    public string? new_responsibility_code { get; set; }

    // Global audit fields (mapped to match existing columns or new ones)
    // Note: This table already has date_created and created_by columns with specific names
    [Column("date_created")]
    public DateTime date_created { get; set; }

    [Column("date_updated")]
    public DateTime? date_updated { get; set; }

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

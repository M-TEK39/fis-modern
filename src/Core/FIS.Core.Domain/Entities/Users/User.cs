using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities;

/// <summary>
/// User Entity - EXACT legacy schema match for TS_Users table
/// Uses exact field names from Database.cs - NO modernization
/// Maps to the actual legacy TS_Users table with only 3 fields
/// </summary>
[Table("TS_Users")]
public class User
{
    [Key]
    [Column("user_access_code")]
    public int user_access_code { get; set; }

    [Column("tel_no")]
    public string? tel_no { get; set; }

    [Column("email")]
    public string? email { get; set; }

    // Global audit fields (AI_CODING_RULES.md - Section 4.5)
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

    // Navigation properties for audit trail
    [ForeignKey("created_by_user_code")]
    public virtual User? CreatedByUser { get; set; }

    [ForeignKey("modified_by_user_code")]
    public virtual User? ModifiedByUser { get; set; }
}

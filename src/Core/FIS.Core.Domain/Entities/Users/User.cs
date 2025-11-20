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
}

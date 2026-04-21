using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FIS.Core.Domain.Entities.Auth;

[Table("user_access_old1")]
public class UserAccessOld
{
    [Key]
    [Column("user_access_code")]
    public short user_access_code { get; set; }

    [Column("Site_code")]
    public short? Site_code { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string? name { get; set; }

    [Column("password")]
    [StringLength(255)]
    public string? password { get; set; }

    [Column("user_status")]
    [StringLength(50)]
    public string? user_status { get; set; }

    [Column("last_log_on")]
    public DateTime? last_log_on { get; set; }

    [Column("vmf")]
    [StringLength(50)]
    public string? vmf { get; set; }

    [Column("data_import")]
    [StringLength(50)]
    public string? data_import { get; set; }

    [Column("virtual_odo")]
    [StringLength(50)]
    public string? virtual_odo { get; set; }

    [Column("month_end")]
    [StringLength(50)]
    public string? month_end { get; set; }

    [Column("user_access")]
    [StringLength(50)]
    public string? user_access { get; set; }

    [Column("AccessLevel")]
    public long AccessLevel { get; set; }

    [Column("E_Mail")]
    [StringLength(255)]
    public string? E_Mail { get; set; }

    [Column("telephone")]
    [StringLength(50)]
    public string? telephone { get; set; }

    [Column("FirstName")]
    [StringLength(255)]
    public string? FirstName { get; set; }

    [Column("LastName")]
    [StringLength(255)]
    public string? LastName { get; set; }

    [Column("Connection_type")]
    public int? Connection_type { get; set; }

    [Column("OS_Browser")]
    [StringLength(255)]
    public string? OS_Browser { get; set; }

    [Column("IPAddress")]
    [StringLength(50)]
    public string? IPAddress { get; set; }

    [Column("Position_Code")]
    public byte? Position_Code { get; set; }

    [Column("user_active")]
    public bool user_active { get; set; }

    [Column("Access_str")]
    [StringLength(255)]
    public string? Access_str { get; set; }

    [Column("Retry")]
    public short? Retry { get; set; }

    [Column("PWD_Expires")]
    public DateTime? PWD_Expires { get; set; }

    [Column("firtsname")] // Preserving typo
    public int? firtsname { get; set; }

    [Column("Persal_Number")]
    public int? Persal_Number { get; set; }

    [Column("Contract_Number")]
    public int? Contract_Number { get; set; }

    [Column("sa_id_number")]
    public int? sa_id_number { get; set; }

    [Column("passport_number")]
    public int? passport_number { get; set; }

    [Column("Fax_Number")]
    public int? Fax_Number { get; set; }

    [Column("Cellphone_Number")]
    public int? Cellphone_Number { get; set; }

    // Global audit fields (some overlap with legacy columns)
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

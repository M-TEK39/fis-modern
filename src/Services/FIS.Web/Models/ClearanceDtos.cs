using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

public sealed record ClearanceDto
{
    public int clearance_code { get; set; }
    public int vmf_code { get; set; }
    public int? clearance_number { get; set; }
    public DateTime? Clearance_date { get; set; }
    public int? Merchant_code { get; set; }
    public decimal? Clearance_amount { get; set; }
    public string? clearance_comment { get; set; }
    public int? clearance_kilo { get; set; }
}

public sealed record ClearanceCreateDto
{
    [Required]
    public int vmf_code { get; set; }
    [Required]
    public int? clearance_number { get; set; }
    public DateTime? Clearance_date { get; set; }
    public int? Merchant_code { get; set; }
    public decimal? Clearance_amount { get; set; }
    [Required]
    [MaxLength(80)]
    public string? clearance_comment { get; set; }
    public int? clearance_kilo { get; set; }
}

public sealed record MerchantDto
{
    public int Merchant_code { get; set; }
    public string? Merchant_Name { get; set; }
}

public sealed record MerchantCreateDto
{
    [Required]
    [MaxLength(30)]
    public string? Merchant_Name { get; set; }
}

public sealed record ClearanceLookupResult
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? registration_number { get; set; }
}

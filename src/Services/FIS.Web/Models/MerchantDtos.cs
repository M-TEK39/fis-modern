using System.ComponentModel.DataAnnotations;

namespace FIS.Web.Models;

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

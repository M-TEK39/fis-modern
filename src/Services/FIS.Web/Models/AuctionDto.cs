namespace FIS.Web.Models;

public sealed record AuctionDto
{
    public short auction_code { get; set; }
    public int vmf_code { get; set; }
    public string? auction_number { get; set; }
    public string? camp { get; set; }
    public decimal? lot { get; set; }
    public short? auction_garage { get; set; }
    public string? auth_number { get; set; }
    public DateTime? auth_date { get; set; }
    public decimal? auction_km { get; set; }
    public string? garage_owner { get; set; }
    public string? reason_sold { get; set; }
    public decimal? estimate_amount { get; set; }
    public decimal? reserve_amount { get; set; }
    public string? sold_id { get; set; }
    public string? remark { get; set; }
}

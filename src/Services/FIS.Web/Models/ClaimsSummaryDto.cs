namespace FIS.Web.Models;

public class ClaimsSummaryDto
{
    public int TotalClaims { get; set; }
    public decimal TotalRepairCosts { get; set; }
    public decimal TotalThirdPartyClaims { get; set; }
    public int PendingClaims { get; set; }
    public Dictionary<string, decimal> CostByCategory { get; set; } = new();
    public Dictionary<string, decimal> MonthlyTrend { get; set; } = new();
}
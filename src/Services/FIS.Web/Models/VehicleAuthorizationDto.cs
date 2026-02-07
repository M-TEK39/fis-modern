namespace FIS.Web.Models;

public record VehicleAuthorizationDto
{
    public int TempVmfCode { get; set; }
    public string ChassisNumber { get; set; } = string.Empty;
    public string? EngineNumber { get; set; }
    public short? ModelCode { get; set; }
    public string? ModelDescription { get; set; }
    public string? Colour { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseFrom { get; set; }
    public DateTime? TakeOnDate { get; set; }
    public int? TakeOnOdo { get; set; }
    public string? ReplacedGGNumber { get; set; }
    public string? FleetNotes { get; set; }
    public string? DamageStatus { get; set; }
    public string? DamagesComment { get; set; }
    public string AuthorityStatus { get; set; } = string.Empty;
    public int? AuthorizedByUserCode { get; set; }
    public string? AuthorizedByUserName { get; set; }
    public DateTime? AuthorizationDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? AuthorizationComment { get; set; }
    public int? VmfCode { get; set; }
    public DateTime DateCreated { get; set; }
    public int? CreatedByUserCode { get; set; }
}

namespace FIS.Core.Application.DTOs;

/// <summary>
/// Vehicle data transfer object
/// </summary>
public class VehicleDto
{
    public int VmfCode { get; set; }
    public string FleetNumber { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string VinNumber { get; set; } = string.Empty;
    public string EngineNumber { get; set; } = string.Empty;
    public decimal? PurchasePrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsAvailable { get; set; }
    public string? CurrentClient { get; set; }
    public string? CurrentSite { get; set; }
}

/// <summary>
/// Contract data transfer object
/// </summary>
public class ContractDto
{
    public int ContractCode { get; set; }
    public int VmfCode { get; set; }
    public string VehicleFleetNumber { get; set; } = string.Empty;
    public string VehicleRegistration { get; set; } = string.Empty;
    public int ClientCode { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public int SiteCode { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string ContractStatusCode { get; set; } = string.Empty;
    public string ContractStatusDescription { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool StillCurrent { get; set; }
    public decimal? MonthlyRate { get; set; }
    public decimal? KilometerRate { get; set; }
    public DateTime? ChargedUntil { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Create vehicle DTO
/// </summary>
public class CreateVehicleDto
{
    public string FleetNumber { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string VinNumber { get; set; } = string.Empty;
    public string EngineNumber { get; set; } = string.Empty;
    public decimal? PurchasePrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? RegistrationDate { get; set; }
}

/// <summary>
/// Update vehicle DTO
/// </summary>
public class UpdateVehicleDto
{
    public string FleetNumber { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string VinNumber { get; set; } = string.Empty;
    public string EngineNumber { get; set; } = string.Empty;
    public decimal? PurchasePrice { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Create contract DTO
/// </summary>
public class CreateContractDto
{
    public int VmfCode { get; set; }
    public int ClientCode { get; set; }
    public int SiteCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MonthlyRate { get; set; }
    public decimal? KilometerRate { get; set; }
    public string? Remarks { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Update contract DTO
/// </summary>
public class UpdateContractDto
{
    public int SiteCode { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MonthlyRate { get; set; }
    public decimal? KilometerRate { get; set; }
    public string? Remarks { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

namespace FIS.Web.Models;

public sealed class TaxiLogReferenceModel
{
    public List<TaxiLogContractorOptionModel> Contractors { get; set; } = [];
    public List<TaxiLogClassOptionModel> Classes { get; set; } = [];
    public List<TaxiLogNoteOptionModel> Notes { get; set; } = [];
}

public sealed class TaxiLogContractorOptionModel
{
    public short ContractorId { get; set; }
    public string ContractorName { get; set; } = string.Empty;
}

public sealed class TaxiLogClassOptionModel
{
    public short ContractorId { get; set; }
    public short ClassId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? KmTariff { get; set; }
    public decimal? DriverPerHour { get; set; }
    public decimal? DailyTariff { get; set; }
}

public sealed class TaxiLogNoteOptionModel
{
    public short NoteCode { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class TaxiLogLookupModel
{
    public int RequestId { get; set; }
    public string RekNum { get; set; } = string.Empty;
    public short? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public short? VehicleTypeCode { get; set; }
    public string? VehicleTypeDescription { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? FleetNumber { get; set; }
    public string? Driver { get; set; }
    public string? Official { get; set; }
    public DateTime DateRequired { get; set; }
    public DateTime TimeRequired { get; set; }
    public short? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public TaxiLogDetailModel? Log { get; set; }
}

public sealed class TaxiLogDetailModel
{
    public int LogId { get; set; }
    public decimal? DriverStartOdo { get; set; }
    public decimal? DriverEndOdo { get; set; }
    public DateTime? DriverStartDate { get; set; }
    public DateTime? DriverEndDate { get; set; }
    public string? DriverStartTime { get; set; }
    public string? DriverEndTime { get; set; }
    public short? TaxiLogNoteCode { get; set; }
    public decimal? QuotedTariff { get; set; }
}

public sealed class TaxiLogSaveModel
{
    public int? RequestId { get; set; }
    public string RekNum { get; set; } = string.Empty;
    public short ContractorId { get; set; }
    public short? VehicleTypeCode { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string Driver { get; set; } = string.Empty;
    public decimal? DriverStartOdo { get; set; }
    public decimal? DriverEndOdo { get; set; }
    public DateTime? DriverStartDate { get; set; }
    public DateTime? DriverEndDate { get; set; }
    public string DriverStartTime { get; set; } = "08:00";
    public string DriverEndTime { get; set; } = "09:00";
    public short? TaxiLogNoteCode { get; set; }
    public decimal? QuotedTariff { get; set; }
}

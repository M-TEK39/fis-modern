namespace FIS.Web.Models;

using System.Text.Json.Serialization;

public class FinanceOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class FinanceApiResult
{
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ResponseBody { get; set; }
}

public class FinanceBatchStatusDto
{
    public bool BatchIsRunning { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}

public class FinanceGridRowDto
{
    public Dictionary<string, object?> Data { get; set; } = new();
}

public class FinanceTariffParametersDto
{
    public int Year { get; set; }

    [JsonPropertyName("is_approved")]
    public bool IsApproved { get; set; }

    [JsonPropertyName("approved_by")]
    public string? ApprovedBy { get; set; }

    [JsonPropertyName("effective_date")]
    public string? EffectiveDate { get; set; }

    public List<FinanceTariffParameterValueDto> Parameters { get; set; } = new();
    public List<FinanceTariffRateDto> FixedTariffs { get; set; } = new();
    public List<FinanceTariffRateDto> KiloTariffs { get; set; } = new();
    public List<FinanceMaintenanceValueDto> MaintenanceValues { get; set; } = new();
}

public class FinanceTariffParameterValueDto
{
    public string ParameterName { get; set; } = string.Empty;
    public decimal? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class FinanceTariffRateDto
{
    public short? ClassCode { get; set; }
    public string ClassDescription { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? EffectiveDate { get; set; }
}

public class FinanceMaintenanceValueDto
{
    public short? ClassCode { get; set; }
    public string ClassDescription { get; set; } = string.Empty;
    public int? MonthsAge { get; set; }
    public int? KilometerAge { get; set; }
    public decimal? Amount { get; set; }
    public decimal? RandPerKilometer { get; set; }
}

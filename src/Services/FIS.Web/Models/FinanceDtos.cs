namespace FIS.Web.Models;

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

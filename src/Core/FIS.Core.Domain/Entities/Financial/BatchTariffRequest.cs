using FIS.Core.Domain.Enums;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Request model for batch tariff calculations.
/// Used when calculating multiple contract tariffs in a single operation.
/// </summary>
public class BatchTariffRequest
{
    /// <summary>
    /// Contract code to calculate tariff for
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Date to calculate tariff as of
    /// </summary>
    public DateTime CheckDate { get; set; }

    /// <summary>
    /// Type of tariff to calculate (Fixed, Kilos, etc.)
    /// </summary>
    public TariffType TariffType { get; set; }

    /// <summary>
    /// Optional vehicle code (if different from contract default)
    /// </summary>
    public int? VmfCode { get; set; }

    /// <summary>
    /// Optional reference for tracking this request
    /// </summary>
    public string? Reference { get; set; }
}
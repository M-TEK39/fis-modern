using FIS.Core.Domain.Enums;

namespace FIS.Core.Domain.Entities.Financial;

/// <summary>
/// Result model for batch tariff calculations.
/// Contains the calculated tariff amount and any error information.
/// </summary>
public class BatchTariffResult
{
    /// <summary>
    /// Contract code that was calculated
    /// </summary>
    public int ContractCode { get; set; }

    /// <summary>
    /// Date the tariff was calculated for
    /// </summary>
    public DateTime CheckDate { get; set; }

    /// <summary>
    /// Type of tariff that was calculated
    /// </summary>
    public TariffType TariffType { get; set; }

    /// <summary>
    /// Calculated tariff amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Status of the calculation
    /// </summary>
    public TariffStatus Status { get; set; }

    /// <summary>
    /// Message (error or info) from the calculation
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Source of the tariff calculation
    /// </summary>
    public TariffSource Source { get; set; }

    /// <summary>
    /// Whether the calculation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Optional reference from the original request
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Optional vehicle code used in calculation
    /// </summary>
    public int? VmfCode { get; set; }
}
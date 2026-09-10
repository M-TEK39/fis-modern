using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;

namespace FIS.Core.Application.Interfaces;

/// <summary>
/// Interface for PDF generation service.
/// Provides consistent PDF generation for all report types with legacy-compatible formatting.
/// </summary>
public interface IPdfGenerationService
{
    #region Vehicle Report PDFs

    /// <summary>
    /// Generate PDF for individual vehicle report.
    /// </summary>
    /// <param name="vehicleReport">Vehicle report data</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateVehicleReportPdfAsync(VehicleReport vehicleReport);

    #endregion

    #region Invoice Report PDFs

    /// <summary>
    /// Generate PDF for invoice document.
    /// </summary>
    /// <param name="invoice">Invoice header</param>
    /// <param name="invoiceItems">Invoice line items</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, List<InvoiceItem> invoiceItems);

    #endregion

    #region Financial Report PDFs

    /// <summary>
    /// Generate PDF for financial summary reports.
    /// </summary>
    /// <param name="reportTitle">Title of the report</param>
    /// <param name="summaryData">Summary data as key-value pairs</param>
    /// <param name="reportDate">Report date</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateFinancialSummaryPdfAsync(
        string reportTitle,
        Dictionary<string, decimal> summaryData,
        DateTime reportDate
    );

    #endregion

    #region Maintenance Report PDFs

    /// <summary>
    /// Generate PDF for maintenance reports.
    /// </summary>
    /// <param name="reportTitle">Title of the report</param>
    /// <param name="maintenanceRecords">Maintenance records to include</param>
    /// <param name="reportDate">Report date</param>
    /// <returns>PDF document as byte array</returns>
    Task<byte[]> GenerateMaintenanceReportPdfAsync(
        string reportTitle,
        List<MaintenanceRecord> maintenanceRecords,
        DateTime reportDate
    );

    #endregion
}

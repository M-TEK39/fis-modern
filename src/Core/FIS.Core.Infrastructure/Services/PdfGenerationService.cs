using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services;

/// <summary>
/// Simple PDF generation service stub implementation
/// Returns empty byte arrays for all PDF generation methods
/// No external dependencies - fully functional for DI registration
/// </summary>
public class PdfGenerationService : IPdfGenerationService
{
    private readonly ILogger<PdfGenerationService> _logger;

    public PdfGenerationService(ILogger<PdfGenerationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<byte[]> GenerateVehicleReportPdfAsync(VehicleReport vehicleReport)
    {
        _logger.LogInformation("PDF generation requested for vehicle report: VMF {VmfCode}", vehicleReport.VmfCode);
        
        // TODO: Implement actual PDF generation
        // For now, return empty byte array as stub
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, List<InvoiceItem> invoiceItems)
    {
        _logger.LogInformation("PDF generation requested for invoice: {InvoiceCode}", invoice.invoice_code);
        
        // TODO: Implement actual PDF generation
        // For now, return empty byte array as stub
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<byte[]> GenerateFinancialSummaryPdfAsync(string reportTitle, Dictionary<string, decimal> summaryData, DateTime reportDate)
    {
        _logger.LogInformation("PDF generation requested for financial summary: {ReportTitle}", reportTitle);
        
        // TODO: Implement actual PDF generation
        // For now, return empty byte array as stub
        return Task.FromResult(Array.Empty<byte>());
    }

    public Task<byte[]> GenerateMaintenanceReportPdfAsync(string reportTitle, List<MaintenanceRecord> maintenanceRecords, DateTime reportDate)
    {
        _logger.LogInformation("PDF generation requested for maintenance report: {ReportTitle}", reportTitle);
        
        // TODO: Implement actual PDF generation
        // For now, return empty byte array as stub
        return Task.FromResult(Array.Empty<byte>());
    }
}

using System.Text;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Financial;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services;

/// <summary>
/// Lightweight PDF generator implementation with no external dependencies.
/// Produces a valid single-page PDF with plain-text content.
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
        _logger.LogInformation(
            "PDF generation requested for vehicle report: VMF {VmfCode}",
            vehicleReport.VmfCode
        );

        var lines = new List<string>
        {
            "Vehicle Report",
            $"VMF Code: {vehicleReport.VmfCode}",
            $"GG Number: {vehicleReport.FleetNumber}",
            $"Registration: {vehicleReport.RegistrationNumber}",
            $"Make/Model: {vehicleReport.Make} {vehicleReport.Model}",
            $"Year: {vehicleReport.YearManufactured}",
            $"Current Odometer: {vehicleReport.CurrentOdometer:N0}",
            $"Status: {vehicleReport.Status}",
            $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
        };

        return Task.FromResult(BuildSimplePdf(lines));
    }

    public Task<byte[]> GenerateInvoicePdfAsync(Invoice invoice, List<InvoiceItem> invoiceItems)
    {
        _logger.LogInformation(
            "PDF generation requested for invoice: {InvoiceCode}",
            invoice.invoice_code
        );

        var lines = new List<string>
        {
            "Invoice",
            $"Invoice Code: {invoice.invoice_code}",
            $"Posting Month Code: {invoice.posting_month_code}",
            $"Department Code: {invoice.department_code}",
            $"Created: {invoice.date_created:yyyy-MM-dd}",
            "Items:",
        };

        lines.AddRange(
            invoiceItems
                .Take(40)
                .Select(item =>
                    $"Item {item.item_code} | VMF {item.vmf_code} | Site {item.site_code} | Fixed {item.fixed_tariff_amount:N2} | Odo {item.odo_tariff_amount:N2}"
                )
        );

        return Task.FromResult(BuildSimplePdf(lines));
    }

    public Task<byte[]> GenerateFinancialSummaryPdfAsync(
        string reportTitle,
        Dictionary<string, decimal> summaryData,
        DateTime reportDate
    )
    {
        _logger.LogInformation(
            "PDF generation requested for financial summary: {ReportTitle}",
            reportTitle
        );

        var lines = new List<string>
        {
            reportTitle,
            $"Report Date: {reportDate:yyyy-MM-dd}",
            "Summary:",
        };

        lines.AddRange(summaryData.Take(60).Select(kv => $"{kv.Key}: {kv.Value:N2}"));

        return Task.FromResult(BuildSimplePdf(lines));
    }

    public Task<byte[]> GenerateMaintenanceReportPdfAsync(
        string reportTitle,
        List<MaintenanceRecord> maintenanceRecords,
        DateTime reportDate
    )
    {
        _logger.LogInformation(
            "PDF generation requested for maintenance report: {ReportTitle}",
            reportTitle
        );

        var lines = new List<string>
        {
            reportTitle,
            $"Report Date: {reportDate:yyyy-MM-dd}",
            $"Total Records: {maintenanceRecords.Count}",
            "Records:",
        };

        lines.AddRange(
            maintenanceRecords
                .Take(50)
                .Select(record =>
                    $"ID {record.MaintenanceId} | VMF {record.VmfCode} | Type {record.MaintenanceType} | Cost {record.TotalCost:N2} | Date {record.MaintenanceDate:yyyy-MM-dd}"
                )
        );

        return Task.FromResult(BuildSimplePdf(lines));
    }

    private static byte[] BuildSimplePdf(IEnumerable<string> sourceLines)
    {
        var lines = sourceLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Length > 120 ? line[..120] : line)
            .Take(60)
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add("Report output is empty.");
        }

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("50 780 Td");
        foreach (var line in lines)
        {
            contentBuilder.AppendLine($"({EscapePdfText(line)}) Tj");
            contentBuilder.AppendLine("0 -14 Td");
        }
        contentBuilder.AppendLine("ET");

        var contentStream = contentBuilder.ToString();
        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Count 1 /Kids [3 0 R] >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n",
            "4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n",
            $"5 0 obj\n<< /Length {Encoding.ASCII.GetByteCount(contentStream)} >>\nstream\n{contentStream}endstream\nendobj\n",
        };

        var pdfBuilder = new StringBuilder();
        pdfBuilder.Append("%PDF-1.4\n");
        pdfBuilder.Append("%FIS\n");

        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdfBuilder.ToString()));
            pdfBuilder.Append(obj);
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdfBuilder.ToString());
        pdfBuilder.Append("xref\n");
        pdfBuilder.Append($"0 {objects.Count + 1}\n");
        pdfBuilder.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdfBuilder.Append($"{offset:D10} 00000 n \n");
        }
        pdfBuilder.Append("trailer\n");
        pdfBuilder.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        pdfBuilder.Append("startxref\n");
        pdfBuilder.Append($"{xrefOffset}\n");
        pdfBuilder.Append("%%EOF");

        return Encoding.ASCII.GetBytes(pdfBuilder.ToString());
    }

    private static string EscapePdfText(string value) =>
        value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}

using System.Net;
using System.Net.Mail;
using System.Text;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services;

/// <summary>
/// Email notification service implementation for Fleet Information System
/// Provides modern replacement for legacy email functionality
/// Uses SMTP client with modern configuration and error handling
/// </summary>
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly IReportingService _reportingService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IMaintenanceRecordRepository _maintenanceRepository;

    // Email configuration settings
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly bool _useSSL;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly bool _isConfigured;

    // Email templates cache
    private static readonly Dictionary<string, EmailTemplate> _defaultTemplates = new()
    {
        ["MaintenanceReminder"] = new EmailTemplate
        {
            TemplateName = "MaintenanceReminder",
            Subject = "Vehicle Maintenance Reminder - {VehicleRegistration}",
            HtmlBody =
                @"
                <h2>Vehicle Maintenance Reminder</h2>
                <p>Dear {RecipientName},</p>
                <p>This is a reminder that vehicle <strong>{VehicleRegistration}</strong> (Fleet #{FleetNumber}) is due for maintenance.</p>
                <ul>
                    <li><strong>VMF Code:</strong> {VmfCode}</li>
                    <li><strong>Current Odometer:</strong> {CurrentOdometer} km</li>
                    <li><strong>Service Due:</strong> {ServiceDueDate}</li>
                    <li><strong>Service Due At:</strong> {ServiceDueOdometer} km</li>
                </ul>
                <p>Please schedule the required maintenance as soon as possible.</p>
                <p>Thank you,<br/>Fleet Management System</p>",
            TextBody =
                @"
                Vehicle Maintenance Reminder
                
                Dear {RecipientName},
                
                This is a reminder that vehicle {VehicleRegistration} (Fleet #{FleetNumber}) is due for maintenance.
                
                VMF Code: {VmfCode}
                Current Odometer: {CurrentOdometer} km
                Service Due: {ServiceDueDate}
                Service Due At: {ServiceDueOdometer} km
                
                Please schedule the required maintenance as soon as possible.
                
                Thank you,
                Fleet Management System",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName",
                "VehicleRegistration",
                "FleetNumber",
                "VmfCode",
                "CurrentOdometer",
                "ServiceDueDate",
                "ServiceDueOdometer",
            },
            IsActive = true,
        },

        ["LicenceReminder"] = new EmailTemplate
        {
            TemplateName = "LicenceReminder",
            Subject = "Vehicle Licence Renewal Reminder - {VehicleRegistration}",
            HtmlBody =
                @"
                <h2>Vehicle Licence Renewal Reminder</h2>
                <p>Dear {RecipientName},</p>
                <p>This is a reminder that the licence for vehicle <strong>{VehicleRegistration}</strong> (Fleet #{FleetNumber}) is expiring soon.</p>
                <ul>
                    <li><strong>VMF Code:</strong> {VmfCode}</li>
                    <li><strong>Licence Expiry Date:</strong> {LicenceExpiryDate}</li>
                    <li><strong>Days Until Expiry:</strong> {DaysUntilExpiry}</li>
                </ul>
                <p>Please renew the vehicle licence before the expiry date to avoid penalties.</p>
                <p>Thank you,<br/>Fleet Management System</p>",
            TextBody =
                @"
                Vehicle Licence Renewal Reminder
                
                Dear {RecipientName},
                
                This is a reminder that the licence for vehicle {VehicleRegistration} (Fleet #{FleetNumber}) is expiring soon.
                
                VMF Code: {VmfCode}
                Licence Expiry Date: {LicenceExpiryDate}
                Days Until Expiry: {DaysUntilExpiry}
                
                Please renew the vehicle licence before the expiry date to avoid penalties.
                
                Thank you,
                Fleet Management System",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName",
                "VehicleRegistration",
                "FleetNumber",
                "VmfCode",
                "LicenceExpiryDate",
                "DaysUntilExpiry",
            },
            IsActive = true,
        },

        ["CofReminder"] = new EmailTemplate
        {
            TemplateName = "CofReminder",
            Subject = "Certificate of Fitness Renewal Reminder - {VehicleRegistration}",
            HtmlBody =
                @"
                <h2>Certificate of Fitness Renewal Reminder</h2>
                <p>Dear {RecipientName},</p>
                <p>This is a reminder that the Certificate of Fitness (COF) for vehicle <strong>{VehicleRegistration}</strong> (Fleet #{FleetNumber}) is expiring soon.</p>
                <ul>
                    <li><strong>VMF Code:</strong> {VmfCode}</li>
                    <li><strong>COF Expiry Date:</strong> {CofExpiryDate}</li>
                    <li><strong>Days Until Expiry:</strong> {DaysUntilExpiry}</li>
                </ul>
                <p>Please arrange for COF renewal before the expiry date to ensure continued vehicle operation.</p>
                <p>Thank you,<br/>Fleet Management System</p>",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName",
                "VehicleRegistration",
                "FleetNumber",
                "VmfCode",
                "CofExpiryDate",
                "DaysUntilExpiry",
            },
            IsActive = true,
        },

        ["ContractExpiry"] = new EmailTemplate
        {
            TemplateName = "ContractExpiry",
            Subject = "Contract Expiry Notification - {ContractNumber}",
            HtmlBody =
                @"
                <h2>Contract Expiry Notification</h2>
                <p>Dear {RecipientName},</p>
                <p>This is a notification that contract <strong>{ContractNumber}</strong> is expiring soon.</p>
                <ul>
                    <li><strong>Contract ID:</strong> {ContractId}</li>
                    <li><strong>Contract Number:</strong> {ContractNumber}</li>
                    <li><strong>Expiry Date:</strong> {ExpiryDate}</li>
                    <li><strong>Days Until Expiry:</strong> {DaysUntilExpiry}</li>
                    <li><strong>Contract Value:</strong> ${ContractValue:N2}</li>
                </ul>
                <p>Please review and renew the contract if necessary.</p>
                <p>Thank you,<br/>Fleet Management System</p>",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName",
                "ContractId",
                "ContractNumber",
                "ExpiryDate",
                "DaysUntilExpiry",
                "ContractValue",
            },
            IsActive = true,
        },
    };

    public EmailNotificationService(
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger,
        IReportingService reportingService,
        IVehicleRepository vehicleRepository,
        IContractRepository contractRepository,
        IMaintenanceRecordRepository maintenanceRepository
    )
    {
        _configuration = configuration;
        _logger = logger;
        _reportingService = reportingService;
        _vehicleRepository = vehicleRepository;
        _contractRepository = contractRepository;
        _maintenanceRepository = maintenanceRepository;

        // Load email configuration
        var emailConfig = _configuration.GetSection("EmailSettings");
        _smtpServer = emailConfig["SmtpServer"] ?? string.Empty;
        _smtpPort = int.Parse(emailConfig["SmtpPort"] ?? "587");
        _useSSL = bool.Parse(emailConfig["UseSSL"] ?? "true");
        _username = emailConfig["Username"] ?? string.Empty;
        _password = emailConfig["Password"] ?? string.Empty;
        _fromAddress = emailConfig["FromAddress"] ?? string.Empty;
        _fromName = emailConfig["FromName"] ?? "Fleet Management System";

        _isConfigured =
            !string.IsNullOrEmpty(_smtpServer)
            && !string.IsNullOrEmpty(_username)
            && !string.IsNullOrEmpty(_fromAddress);

        if (!_isConfigured)
        {
            _logger.LogWarning(
                "Email service is not properly configured. Check EmailSettings in configuration."
            );
        }
    }

    #region Simple Email Methods

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        return await SendEmailAsync(new List<string> { to }, subject, body);
    }

    public async Task<bool> SendEmailAsync(List<string> to, string subject, string body)
    {
        return await SendEmailInternalAsync(to, subject, body, false, new List<EmailAttachment>());
    }

    public async Task<bool> SendHtmlEmailAsync(string to, string subject, string htmlBody)
    {
        return await SendHtmlEmailAsync(new List<string> { to }, subject, htmlBody);
    }

    public async Task<bool> SendHtmlEmailAsync(List<string> to, string subject, string htmlBody)
    {
        return await SendEmailInternalAsync(
            to,
            subject,
            htmlBody,
            true,
            new List<EmailAttachment>()
        );
    }

    #endregion

    #region Email with Attachments

    public async Task<bool> SendEmailWithAttachmentsAsync(
        string to,
        string subject,
        string body,
        List<EmailAttachment> attachments
    )
    {
        return await SendEmailWithAttachmentsAsync(
            new List<string> { to },
            subject,
            body,
            attachments
        );
    }

    public async Task<bool> SendEmailWithAttachmentsAsync(
        List<string> to,
        string subject,
        string body,
        List<EmailAttachment> attachments
    )
    {
        return await SendEmailInternalAsync(to, subject, body, true, attachments);
    }

    #endregion

    #region Fleet-Specific Notifications

    public async Task<bool> SendMaintenanceReminderAsync(
        int vmfCode,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending maintenance reminder for VMF {VmfCode} to {EmailAddress}",
                vmfCode,
                emailAddress
            );

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            var template = _defaultTemplates["MaintenanceReminder"];
            var placeholders = new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["VehicleRegistration"] = vehicle.registration_number ?? string.Empty,
                ["FleetNumber"] = vehicle.fleet_number ?? string.Empty,
                ["VmfCode"] = vmfCode.ToString(),
                ["CurrentOdometer"] = vehicle.current_odo.ToString("N0"),
                ["ServiceDueDate"] = "TBD", // TODO: Calculate from maintenance schedule
                ["ServiceDueOdometer"] = "TBD", // TODO: Calculate from maintenance schedule
            };

            var subject = ReplacePlaceholders(template.Subject, placeholders);
            var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);

            return await SendHtmlEmailAsync(emailAddress, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance reminder for VMF {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> SendLicenceReminderAsync(
        int vmfCode,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending licence reminder for VMF {VmfCode} to {EmailAddress}",
                vmfCode,
                emailAddress
            );

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            if (!vehicle.licence_due_date.HasValue)
            {
                _logger.LogWarning("No licence due date set for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            var daysUntilExpiry = (vehicle.licence_due_date.Value - DateTime.Now).Days;

            var template = _defaultTemplates["LicenceReminder"];
            var placeholders = new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["VehicleRegistration"] = vehicle.registration_number ?? string.Empty,
                ["FleetNumber"] = vehicle.fleet_number ?? string.Empty,
                ["VmfCode"] = vmfCode.ToString(),
                ["LicenceExpiryDate"] = vehicle.licence_due_date.Value.ToString("yyyy-MM-dd"),
                ["DaysUntilExpiry"] = daysUntilExpiry.ToString(),
            };

            var subject = ReplacePlaceholders(template.Subject, placeholders);
            var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);

            return await SendHtmlEmailAsync(emailAddress, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending licence reminder for VMF {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> SendCofReminderAsync(
        int vmfCode,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending COF reminder for VMF {VmfCode} to {EmailAddress}",
                vmfCode,
                emailAddress
            );

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            // TODO: Calculate COF due date from COF last done date + validity period
            var cofDueDate = vehicle.cof_last_done?.AddDays(365); // Placeholder: 1 year validity
            if (!cofDueDate.HasValue)
            {
                _logger.LogWarning("No COF due date available for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            var daysUntilExpiry = (cofDueDate.Value - DateTime.Now).Days;

            var template = _defaultTemplates["CofReminder"];
            var placeholders = new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["VehicleRegistration"] = vehicle.registration_number ?? string.Empty,
                ["FleetNumber"] = vehicle.fleet_number ?? string.Empty,
                ["VmfCode"] = vmfCode.ToString(),
                ["CofExpiryDate"] = cofDueDate.Value.ToString("yyyy-MM-dd"),
                ["DaysUntilExpiry"] = daysUntilExpiry.ToString(),
            };

            var subject = ReplacePlaceholders(template.Subject, placeholders);
            var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);

            return await SendHtmlEmailAsync(emailAddress, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending COF reminder for VMF {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> SendVehicleReportAsync(
        int vmfCode,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending vehicle report for VMF {VmfCode} to {EmailAddress}",
                vmfCode,
                emailAddress
            );

            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found for VMF Code: {VmfCode}", vmfCode);
                return false;
            }

            // Generate PDF report
            var pdfContent = await _reportingService.GenerateVehicleReportPdfAsync(vmfCode);

            var attachment = new EmailAttachment
            {
                FileName =
                    $"Vehicle_Report_{vehicle.registration_number}_{DateTime.Now:yyyyMMdd}.pdf",
                Content = pdfContent,
                ContentType = "application/pdf",
                Description = $"Vehicle report for {vehicle.registration_number}",
            };

            var subject = $"Vehicle Report - {vehicle.registration_number}";
            var body =
                $@"
                <h2>Vehicle Report</h2>
                <p>Dear {recipientName},</p>
                <p>Please find attached the vehicle report for <strong>{vehicle.registration_number}</strong> (VMF Code: {vmfCode}).</p>
                <p>Report generated on: {DateTime.Now:yyyy-MM-dd HH:mm}</p>
                <p>Thank you,<br/>Fleet Management System</p>";

            return await SendEmailWithAttachmentsAsync(
                emailAddress,
                subject,
                body,
                new List<EmailAttachment> { attachment }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending vehicle report for VMF {VmfCode}", vmfCode);
            return false;
        }
    }

    public async Task<bool> SendContractExpiryNotificationAsync(
        int contractId,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending contract expiry notification for Contract {ContractId} to {EmailAddress}",
                contractId,
                emailAddress
            );

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
            {
                _logger.LogWarning("Contract not found for ID: {ContractId}", contractId);
                return false;
            }

            if (!contract.end_date.HasValue)
            {
                _logger.LogWarning("No end date set for Contract ID: {ContractId}", contractId);
                return false;
            }

            var daysUntilExpiry = (contract.end_date.Value - DateTime.Now).Days;

            var template = _defaultTemplates["ContractExpiry"];
            var placeholders = new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["ContractId"] = contractId.ToString(),
                ["ContractNumber"] = contract.contract_code.ToString(),
                ["ExpiryDate"] = contract.end_date.Value.ToString("yyyy-MM-dd"),
                ["DaysUntilExpiry"] = daysUntilExpiry.ToString(),
                ["ContractValue"] = "0.00", // allocated_amount field doesn't exist in legacy schema
            };

            var subject = ReplacePlaceholders(template.Subject, placeholders);
            var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);

            return await SendHtmlEmailAsync(emailAddress, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending contract expiry notification for Contract {ContractId}",
                contractId
            );
            return false;
        }
    }

    public async Task<bool> SendTripSummaryReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation("Sending trip summary report to {EmailAddress}", emailAddress);

            var report = await _reportingService.GenerateTripSummaryReportAsync(
                vmfCode,
                startDate,
                endDate
            );
            var csvContent = await _reportingService.ExportToCsvAsync(
                report.TripSummaries,
                "trip_summary.csv"
            );

            var attachment = new EmailAttachment
            {
                FileName = $"Trip_Summary_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv",
                Content = csvContent,
                ContentType = "text/csv",
                Description = "Trip summary report",
            };

            var subject = $"Trip Summary Report ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})";
            var body =
                $@"
                <h2>Trip Summary Report</h2>
                <p>Dear {recipientName},</p>
                <p>Please find attached the trip summary report for the period {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.</p>
                <ul>
                    <li><strong>Total Trips:</strong> {report.TotalTrips:N0}</li>
                    <li><strong>Total Kilometers:</strong> {report.TotalKilometers:N0} km</li>
                    <li><strong>Total Revenue:</strong> ${report.TotalRevenue:N2}</li>
                </ul>
                <p>Report generated on: {DateTime.Now:yyyy-MM-dd HH:mm}</p>
                <p>Thank you,<br/>Fleet Management System</p>";

            return await SendEmailWithAttachmentsAsync(
                emailAddress,
                subject,
                body,
                new List<EmailAttachment> { attachment }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending trip summary report");
            return false;
        }
    }

    public async Task<bool> SendMaintenanceCostReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending maintenance cost report to {EmailAddress}",
                emailAddress
            );

            var report = await _reportingService.GenerateMaintenanceCostReportAsync(
                vmfCode,
                startDate,
                endDate
            );
            var csvContent = await _reportingService.ExportToCsvAsync(
                report.CostLines,
                "maintenance_costs.csv"
            );

            var attachment = new EmailAttachment
            {
                FileName = $"Maintenance_Costs_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv",
                Content = csvContent,
                ContentType = "text/csv",
                Description = "Maintenance cost report",
            };

            var subject =
                $"Maintenance Cost Report ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})";
            var body =
                $@"
                <h2>Maintenance Cost Report</h2>
                <p>Dear {recipientName},</p>
                <p>Please find attached the maintenance cost report for the period {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.</p>
                <ul>
                    <li><strong>Total Cost:</strong> ${report.TotalCost:N2}</li>
                    <li><strong>Number of Services:</strong> {report.CostLines.Count:N0}</li>
                </ul>
                <p>Report generated on: {DateTime.Now:yyyy-MM-dd HH:mm}</p>
                <p>Thank you,<br/>Fleet Management System</p>";

            return await SendEmailWithAttachmentsAsync(
                emailAddress,
                subject,
                body,
                new List<EmailAttachment> { attachment }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance cost report");
            return false;
        }
    }

    public async Task<bool> SendFinancialReportAsync(
        string reportType,
        int financialYear,
        string emailAddress,
        string recipientName
    )
    {
        try
        {
            _logger.LogInformation(
                "Sending financial report ({ReportType}) for FY {FinancialYear} to {EmailAddress}",
                reportType,
                financialYear,
                emailAddress
            );

            // TODO: Generate financial report based on type
            // This would be implemented after financial reporting is complete

            var subject = $"Financial Report - {reportType} (FY {financialYear})";
            var body =
                $@"
                <h2>Financial Report</h2>
                <p>Dear {recipientName},</p>
                <p>Please find attached the {reportType} financial report for Financial Year {financialYear}.</p>
                <p>Report generated on: {DateTime.Now:yyyy-MM-dd HH:mm}</p>
                <p>Thank you,<br/>Fleet Management System</p>";

            // TODO: Generate and attach actual financial report
            return await SendHtmlEmailAsync(emailAddress, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending financial report");
            return false;
        }
    }

    #endregion

    #region Bulk Notifications

    public Task<BulkEmailResult> SendBulkMaintenanceRemindersAsync()
    {
        var result = new BulkEmailResult { CompletedAt = DateTime.Now };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting bulk maintenance reminders");

            // TODO: Get vehicles due for maintenance
            // This would require implementing maintenance scheduling logic

            var vehicles = new List<Vehicle>(); // Placeholder
            result.TotalEmails = vehicles.Count;

            // TODO: Implement bulk email sending

            result.SuccessfulEmails = 0;
            result.FailedEmails = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk maintenance reminders");
            result.ErrorMessages.Add($"Bulk operation failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return Task.FromResult(result);
    }

    public Task<BulkEmailResult> SendBulkLicenceRemindersAsync()
    {
        // TODO: Implement bulk licence reminder logic
        return Task.FromResult(new BulkEmailResult { CompletedAt = DateTime.Now });
    }

    public Task<BulkEmailResult> SendBulkCofRemindersAsync()
    {
        // TODO: Implement bulk COF reminder logic
        return Task.FromResult(new BulkEmailResult { CompletedAt = DateTime.Now });
    }

    public Task<BulkEmailResult> SendBulkContractExpiryNotificationsAsync()
    {
        // TODO: Implement bulk contract expiry notification logic
        return Task.FromResult(new BulkEmailResult { CompletedAt = DateTime.Now });
    }

    #endregion

    #region Email Templates and Configuration

    public Task<List<EmailTemplate>> GetEmailTemplatesAsync()
    {
        return Task.FromResult(_defaultTemplates.Values.ToList());
    }

    public Task<EmailTemplate> GetEmailTemplateAsync(string templateName)
    {
        if (_defaultTemplates.TryGetValue(templateName, out var template))
        {
            return Task.FromResult(template);
        }

        throw new ArgumentException($"Email template not found: {templateName}");
    }

    public Task<bool> SaveEmailTemplateAsync(EmailTemplate template)
    {
        // TODO: Implement template persistence to database
        _logger.LogInformation("Saving email template: {TemplateName}", template.TemplateName);
        return Task.FromResult(true);
    }

    public async Task<bool> TestEmailConfigurationAsync(string testEmailAddress)
    {
        try
        {
            _logger.LogInformation(
                "Testing email configuration by sending to {TestEmailAddress}",
                testEmailAddress
            );

            var subject = "Email Configuration Test";
            var body =
                $@"
                <h2>Email Configuration Test</h2>
                <p>This is a test email to verify the email configuration for the Fleet Management System.</p>
                <p>Test sent on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
                <p>If you received this email, the email configuration is working correctly.</p>";

            return await SendHtmlEmailAsync(testEmailAddress, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email configuration test failed");
            return false;
        }
    }

    public Task<EmailServiceStatus> GetEmailServiceStatusAsync()
    {
        return Task.FromResult(
            new EmailServiceStatus
            {
                IsConfigured = _isConfigured,
                IsConnected = _isConfigured, // TODO: Test SMTP connection
                SmtpServer = _smtpServer,
                SmtpPort = _smtpPort,
                UseSSL = _useSSL,
                FromAddress = _fromAddress,
                FromName = _fromName,
                LastTestDate = DateTime.MinValue, // TODO: Track last test
                LastTestSuccessful = false,
                DailyEmailsSent = 0, // TODO: Track email statistics
                MonthlyEmailsSent = 0,
            }
        );
    }

    #endregion

    #region Private Helper Methods

    private async Task<bool> SendEmailInternalAsync(
        List<string> toAddresses,
        string subject,
        string body,
        bool isHtml,
        List<EmailAttachment> attachments
    )
    {
        if (!_isConfigured)
        {
            _logger.LogWarning("Email service not configured. Cannot send email.");
            return false;
        }

        try
        {
            using var smtpClient = new SmtpClient(_smtpServer, _smtpPort)
            {
                EnableSsl = _useSSL,
                Credentials = new NetworkCredential(_username, _password),
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromAddress, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml,
            };

            // Add recipients
            foreach (var toAddress in toAddresses)
            {
                mailMessage.To.Add(toAddress);
            }

            // Add attachments
            foreach (var attachment in attachments)
            {
                var memoryStream = new MemoryStream(attachment.Content);
                var mailAttachment = new Attachment(
                    memoryStream,
                    attachment.FileName,
                    attachment.ContentType
                );
                mailMessage.Attachments.Add(mailAttachment);
            }

            await smtpClient.SendMailAsync(mailMessage);

            _logger.LogInformation(
                "Email sent successfully to {Recipients}",
                string.Join(", ", toAddresses)
            );
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {Recipients}",
                string.Join(", ", toAddresses)
            );
            return false;
        }
    }

    private string ReplacePlaceholders(string template, Dictionary<string, string> placeholders)
    {
        var result = template;

        foreach (var placeholder in placeholders)
        {
            result = result.Replace($"{{{placeholder.Key}}}", placeholder.Value);
        }

        return result;
    }

    #endregion
}

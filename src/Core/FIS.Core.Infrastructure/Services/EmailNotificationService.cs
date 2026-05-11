using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
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
    private readonly IUserRepository _userRepository;
    private readonly ISiteRepository _siteRepository;

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

        ["ContractExpiryReminder"] = new EmailTemplate
        {
            TemplateName = "ContractExpiryReminder",
            Subject = "REMINDER: Vehicle Contract Expiring in {DaysRemaining} Days — {FleetNumber} ({Registration})",
            HtmlBody =
                @"<h2>Vehicle Hire Contract — Expiry Reminder</h2>
                <p>Dear {RecipientName},</p>
                <p>Please be advised that the vehicle hire contract detailed below will expire in <strong>{DaysRemaining} day(s)</strong> on <strong>{ExpiryDate}</strong>.</p>
                <table cellpadding='4' cellspacing='0' border='1' style='border-collapse:collapse;'>
                    <tr><td><strong>Contract #</strong></td><td>{ContractNumber}</td></tr>
                    <tr><td><strong>Vehicle Fleet #</strong></td><td>{FleetNumber}</td></tr>
                    <tr><td><strong>Registration</strong></td><td>{Registration}</td></tr>
                    <tr><td><strong>Driver</strong></td><td>{DriverId}</td></tr>
                    <tr><td><strong>Site</strong></td><td>{SiteName}</td></tr>
                    <tr><td><strong>Contract Start</strong></td><td>{StartDate}</td></tr>
                    <tr><td><strong>Expiry Date</strong></td><td>{ExpiryDate}</td></tr>
                </table>
                <br/>
                <p style='background:#fff3cd;padding:10px;border-left:4px solid #ffc107;'>
                    <strong>Action Required:</strong> If you intend to retain the vehicle beyond the expiry date,
                    you must submit a <strong>formal letter of extension</strong> to Fleet Management
                    <em>before</em> the contract expires. Failure to return the vehicle or obtain an approved
                    extension may result in the contract being classified as overdue.
                </p>
                <p>If you have any questions, please contact your Fleet Management Officer.</p>
                <p>Thank you,<br/>Fleet Management System</p>",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName", "DaysRemaining", "ContractNumber", "FleetNumber",
                "Registration", "DriverId", "SiteName", "StartDate", "ExpiryDate",
            },
            IsActive = true,
        },

        ["ContractOpened"] = new EmailTemplate
        {
            TemplateName = "ContractOpened",
            Subject = "Contract Activated - {ContractNumber} ({FleetNumber})",
            HtmlBody =
                @"<h2>Contract Activated</h2>
                <p>Dear {RecipientName},</p>
                <p>The following contract has been <strong>activated</strong>:</p>
                <table cellpadding='4' cellspacing='0' border='1' style='border-collapse:collapse;'>
                    <tr><td><strong>Contract #</strong></td><td>{ContractNumber}</td></tr>
                    <tr><td><strong>Vehicle Fleet #</strong></td><td>{FleetNumber}</td></tr>
                    <tr><td><strong>Registration</strong></td><td>{Registration}</td></tr>
                    <tr><td><strong>Driver</strong></td><td>{DriverId}</td></tr>
                    <tr><td><strong>Site</strong></td><td>{SiteCode}</td></tr>
                    <tr><td><strong>Start Date</strong></td><td>{StartDate}</td></tr>
                    <tr><td><strong>Target Return</strong></td><td>{TargetReturnDate}</td></tr>
                </table>
                <p>Thank you,<br/>Fleet Management System</p>",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName", "ContractNumber", "FleetNumber", "Registration",
                "DriverId", "SiteCode", "StartDate", "TargetReturnDate",
            },
            IsActive = true,
        },

        ["ContractClosed"] = new EmailTemplate
        {
            TemplateName = "ContractClosed",
            Subject = "Contract Closed - {ContractNumber} ({FleetNumber})",
            HtmlBody =
                @"<h2>Contract Closed</h2>
                <p>Dear {RecipientName},</p>
                <p>The following contract has been <strong>closed</strong> ({ClosureReason}):</p>
                <table cellpadding='4' cellspacing='0' border='1' style='border-collapse:collapse;'>
                    <tr><td><strong>Contract #</strong></td><td>{ContractNumber}</td></tr>
                    <tr><td><strong>Vehicle Fleet #</strong></td><td>{FleetNumber}</td></tr>
                    <tr><td><strong>Registration</strong></td><td>{Registration}</td></tr>
                    <tr><td><strong>Start Date</strong></td><td>{StartDate}</td></tr>
                    <tr><td><strong>End Date</strong></td><td>{EndDate}</td></tr>
                    <tr><td><strong>Performed By</strong></td><td>{PerformedBy}</td></tr>
                </table>
                <p>Thank you,<br/>Fleet Management System</p>",
            RequiredPlaceholders = new List<string>
            {
                "RecipientName", "ContractNumber", "FleetNumber", "Registration",
                "StartDate", "EndDate", "PerformedBy", "ClosureReason",
            },
            IsActive = true,
        },
    };
    private static readonly object TemplateLock = new();

    private DateTime _lastTestDate = DateTime.MinValue;
    private bool _lastTestSuccessful;
    private string? _lastErrorMessage;
    private int _dailyEmailsSent;
    private int _monthlyEmailsSent;
    private DateTime _dailyCounterDate = DateTime.UtcNow.Date;
    private DateTime _monthlyCounterDate = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

    public EmailNotificationService(
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger,
        IReportingService reportingService,
        IVehicleRepository vehicleRepository,
        IContractRepository contractRepository,
        IMaintenanceRecordRepository maintenanceRepository,
        IUserRepository userRepository,
        ISiteRepository siteRepository
    )
    {
        _configuration = configuration;
        _logger = logger;
        _reportingService = reportingService;
        _vehicleRepository = vehicleRepository;
        _contractRepository = contractRepository;
        _maintenanceRepository = maintenanceRepository;
        _userRepository = userRepository;
        _siteRepository = siteRepository;

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
            var serviceDueDate = vehicle.service_last_done?.Date.AddDays(90);
            var serviceDueOdometer = vehicle.service_last_odo.HasValue
                ? vehicle.service_last_odo.Value + 10000
                : (int?)null;
            var placeholders = new Dictionary<string, string>
            {
                ["RecipientName"] = recipientName,
                ["VehicleRegistration"] = vehicle.registration_number ?? string.Empty,
                ["FleetNumber"] = vehicle.fleet_number ?? string.Empty,
                ["VmfCode"] = vmfCode.ToString(),
                ["CurrentOdometer"] = vehicle.current_odo.ToString("N0"),
                ["ServiceDueDate"] = serviceDueDate?.ToString("yyyy-MM-dd") ?? "Immediate",
                ["ServiceDueOdometer"] = serviceDueOdometer?.ToString("N0") ?? "Immediate",
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

            if (string.Equals(vehicle.cof_required, "N", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("COF not required for VMF Code: {VmfCode}", vmfCode);
                return true;
            }

            var cofDueDate = vehicle.cof_last_done?.Date.AddYears(1);
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

    public async Task<bool> SendContractOpenedNotificationAsync(
        int contractId,
        int capturerUserId,
        int approverUserId)
    {
        try
        {
            _logger.LogInformation(
                "Sending contract-opened notification for Contract {ContractId}", contractId);

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
            {
                _logger.LogWarning("Contract {ContractId} not found for opened notification", contractId);
                return false;
            }

            var vehicle = contract.vmf_code > 0
                ? await _vehicleRepository.GetByIdAsync(contract.vmf_code)
                : null;

            var template = _defaultTemplates["ContractOpened"];
            var site = await _siteRepository.GetByIdAsync(contract.site_code);

            // Collect email addresses: capturer, approver, and the site contact (client)
            var recipients = new List<(string email, string name)>();
            foreach (var userId in new[] { capturerUserId, approverUserId }.Distinct())
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user?.email != null)
                    recipients.Add((user.email, user.email));
            }
            // Site contact (the "client" — responsible person at the hiring department)
            if (!string.IsNullOrWhiteSpace(site?.net_address))
                recipients.Add((site.net_address, site.res_person ?? site.net_address));

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "No email addresses found for contract-opened notification (Contract {ContractId})", contractId);
                return false;
            }

            var placeholders = new Dictionary<string, string>
            {
                ["ContractNumber"] = contract.contract_code.ToString(),
                ["FleetNumber"] = vehicle?.fleet_number ?? "N/A",
                ["Registration"] = vehicle?.registration_number ?? "N/A",
                ["DriverId"] = contract.Driver_id ?? "N/A",
                ["SiteCode"] = site?.description ?? contract.site_code.ToString(),
                ["StartDate"] = contract.start_date.ToString("yyyy-MM-dd"),
                ["TargetReturnDate"] = contract.target_return_date?.ToString("yyyy-MM-dd") ?? "Open-ended",
            };

            bool allOk = true;
            foreach (var (email, name) in recipients)
            {
                placeholders["RecipientName"] = name;
                var subject = ReplacePlaceholders(template.Subject, placeholders);
                var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);
                var ok = await SendHtmlEmailAsync(email, subject, htmlBody);
                if (!ok) allOk = false;
            }
            return allOk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending contract-opened notification for Contract {ContractId}", contractId);
            return false;
        }
    }

    public async Task<bool> SendContractClosedNotificationAsync(
        int contractId,
        int performedByUserId,
        string closureReason)
    {
        try
        {
            _logger.LogInformation(
                "Sending contract-closed notification for Contract {ContractId}", contractId);

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
            {
                _logger.LogWarning("Contract {ContractId} not found for closed notification", contractId);
                return false;
            }

            var vehicle = contract.vmf_code > 0
                ? await _vehicleRepository.GetByIdAsync(contract.vmf_code)
                : null;

            var template = _defaultTemplates["ContractClosed"];
            var site = await _siteRepository.GetByIdAsync(contract.site_code);

            // Notify capturer, approver, the person who closed, and the site contact (client)
            var userIds = new List<int> { performedByUserId };
            if (contract.created_by_user_code.HasValue)
                userIds.Add(contract.created_by_user_code.Value);
            if (contract.approver_code.HasValue)
                userIds.Add(contract.approver_code.Value);

            var recipients = new List<(string email, string name)>();
            foreach (var userId in userIds.Distinct())
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user?.email != null)
                    recipients.Add((user.email, user.email));
            }
            // Site contact (the "client")
            if (!string.IsNullOrWhiteSpace(site?.net_address))
                recipients.Add((site.net_address, site.res_person ?? site.net_address));

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "No email addresses found for contract-closed notification (Contract {ContractId})", contractId);
                return false;
            }

            var performedByUser = await _userRepository.GetByIdAsync(performedByUserId);

            var placeholders = new Dictionary<string, string>
            {
                ["ContractNumber"] = contract.contract_code.ToString(),
                ["FleetNumber"] = vehicle?.fleet_number ?? "N/A",
                ["Registration"] = vehicle?.registration_number ?? "N/A",
                ["StartDate"] = contract.start_date.ToString("yyyy-MM-dd"),
                ["EndDate"] = contract.end_date?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                ["PerformedBy"] = performedByUser?.email ?? performedByUserId.ToString(),
                ["ClosureReason"] = closureReason,
            };

            bool allOk = true;
            foreach (var (email, name) in recipients)
            {
                placeholders["RecipientName"] = name;
                var subject = ReplacePlaceholders(template.Subject, placeholders);
                var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);
                var ok = await SendHtmlEmailAsync(email, subject, htmlBody);
                if (!ok) allOk = false;
            }
            return allOk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending contract-closed notification for Contract {ContractId}", contractId);
            return false;
        }
    }

    public async Task<bool> SendContractExpiryReminderAsync(int contractId, int daysRemaining)
    {
        try
        {
            _logger.LogInformation(
                "Sending expiry reminder for Contract {ContractId} ({DaysRemaining} days remaining)",
                contractId, daysRemaining);

            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
            {
                _logger.LogWarning("Contract {ContractId} not found for expiry reminder", contractId);
                return false;
            }

            var vehicle = contract.vmf_code > 0
                ? await _vehicleRepository.GetByIdAsync(contract.vmf_code)
                : null;

            var site = await _siteRepository.GetByIdAsync(contract.site_code);
            var template = _defaultTemplates["ContractExpiryReminder"];

            // Recipients: site contact (primary — this is the "client"), plus the capturer
            var recipients = new List<(string email, string name)>();

            // Site contact (client — responsible person at the hiring department)
            if (!string.IsNullOrWhiteSpace(site?.net_address))
                recipients.Add((site.net_address, site.res_person ?? site.net_address));

            // Internal capturer so fleet management is also aware
            if (contract.created_by_user_code.HasValue)
            {
                var capturer = await _userRepository.GetByIdAsync(contract.created_by_user_code.Value);
                if (capturer?.email != null && !recipients.Any(r => r.email == capturer.email))
                    recipients.Add((capturer.email, capturer.email));
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning(
                    "No email addresses for expiry reminder (Contract {ContractId})", contractId);
                return false;
            }

            var placeholders = new Dictionary<string, string>
            {
                ["DaysRemaining"] = daysRemaining.ToString(),
                ["ContractNumber"] = contract.contract_code.ToString(),
                ["FleetNumber"] = vehicle?.fleet_number ?? "N/A",
                ["Registration"] = vehicle?.registration_number ?? "N/A",
                ["DriverId"] = contract.Driver_id ?? "N/A",
                ["SiteName"] = site?.description ?? contract.site_code.ToString(),
                ["StartDate"] = contract.start_date.ToString("yyyy-MM-dd"),
                ["ExpiryDate"] = contract.target_return_date?.ToString("yyyy-MM-dd") ?? "N/A",
            };

            bool allOk = true;
            foreach (var (email, name) in recipients)
            {
                placeholders["RecipientName"] = name;
                var subject = ReplacePlaceholders(template.Subject, placeholders);
                var htmlBody = ReplacePlaceholders(template.HtmlBody, placeholders);
                var ok = await SendHtmlEmailAsync(email, subject, htmlBody);
                if (!ok) allOk = false;
            }
            return allOk;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending expiry reminder for Contract {ContractId}", contractId);
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

            var normalizedReportType = string.IsNullOrWhiteSpace(reportType)
                ? "SummaryIncome"
                : reportType.Trim();
            var pdfContent = await _reportingService.GenerateCustomReportPdfAsync(
                normalizedReportType,
                new Dictionary<string, object>
                {
                    ["financial_year"] = financialYear,
                }
            );

            var safeReportName = string.Concat(
                normalizedReportType.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            );
            var attachment = new EmailAttachment
            {
                FileName = $"Financial_Report_{safeReportName}_{financialYear}_{DateTime.Now:yyyyMMdd}.pdf",
                Content = pdfContent,
                ContentType = "application/pdf",
                Description = $"{normalizedReportType} financial report for FY {financialYear}",
            };

            var subject = $"Financial Report - {normalizedReportType} (FY {financialYear})";
            var body =
                $@"
                <h2>Financial Report</h2>
                <p>Dear {recipientName},</p>
                <p>Please find attached the {normalizedReportType} financial report for Financial Year {financialYear}.</p>
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
            _logger.LogError(ex, "Error sending financial report");
            return false;
        }
    }

    #endregion

    #region Bulk Notifications

    public async Task<BulkEmailResult> SendBulkMaintenanceRemindersAsync()
    {
        var result = new BulkEmailResult { CompletedAt = DateTime.Now };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting bulk maintenance reminders");

            // Legacy-friendly baseline due logic:
            // - service date older than 90 days OR
            // - service odometer + 10,000km <= current odometer OR
            // - no service history captured
            var today = DateTime.Today;
            var vehicles = (await _vehicleRepository.GetAllAsync())
                .Where(v =>
                    !v.is_deleted &&
                    (
                        !v.service_last_done.HasValue ||
                        v.service_last_done.Value.Date <= today.AddDays(-90) ||
                        !v.service_last_odo.HasValue ||
                        v.current_odo >= (v.service_last_odo.Value + 10000)
                    ))
                .ToList();

            result.TotalEmails = vehicles.Count;
            if (vehicles.Count == 0)
            {
                return result;
            }

            var activeContracts = (await _contractRepository.GetActiveContractsAsync()).ToList();
            var users = (await _userRepository.GetAllUsersAsync())
                .Where(u => !string.IsNullOrWhiteSpace(u.email))
                .ToList();
            var fallbackEmail = users.FirstOrDefault()?.email;

            foreach (var vehicle in vehicles)
            {
                var recipient = await ResolveVehicleRecipientAsync(
                    vehicle.vmf_code,
                    activeContracts,
                    fallbackEmail);

                if (recipient is null)
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"No recipient found for VMF {vehicle.vmf_code}");
                    continue;
                }

                var sent = await SendMaintenanceReminderAsync(
                    vehicle.vmf_code,
                    recipient.Value.Email,
                    recipient.Value.Name);

                if (sent) result.SuccessfulEmails++;
                else
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"Failed sending maintenance reminder for VMF {vehicle.vmf_code}");
                }
            }
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

        return result;
    }

    public async Task<BulkEmailResult> SendBulkLicenceRemindersAsync()
    {
        var result = new BulkEmailResult { CompletedAt = DateTime.Now };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var today = DateTime.Today;
            var reminderWindowEnd = today.AddDays(30);

            var vehicles = (await _vehicleRepository.GetAllAsync())
                .Where(v => !v.is_deleted && v.licence_due_date.HasValue)
                .Where(v => v.licence_due_date!.Value.Date >= today && v.licence_due_date.Value.Date <= reminderWindowEnd)
                .ToList();

            result.TotalEmails = vehicles.Count;
            if (vehicles.Count == 0)
            {
                return result;
            }

            var activeContracts = (await _contractRepository.GetActiveContractsAsync()).ToList();
            var users = (await _userRepository.GetAllUsersAsync())
                .Where(u => !string.IsNullOrWhiteSpace(u.email))
                .ToList();
            var fallbackEmail = users.FirstOrDefault()?.email;

            foreach (var vehicle in vehicles)
            {
                var recipient = await ResolveVehicleRecipientAsync(
                    vehicle.vmf_code,
                    activeContracts,
                    fallbackEmail);

                if (recipient is null)
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"No recipient found for VMF {vehicle.vmf_code}");
                    continue;
                }

                var sent = await SendLicenceReminderAsync(
                    vehicle.vmf_code,
                    recipient.Value.Email,
                    recipient.Value.Name);

                if (sent) result.SuccessfulEmails++;
                else
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"Failed sending licence reminder for VMF {vehicle.vmf_code}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk licence reminders");
            result.ErrorMessages.Add($"Bulk operation failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<BulkEmailResult> SendBulkCofRemindersAsync()
    {
        var result = new BulkEmailResult { CompletedAt = DateTime.Now };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var today = DateTime.Today;
            var reminderWindowEnd = today.AddDays(30);

            var vehicles = (await _vehicleRepository.GetAllAsync())
                .Where(v => !v.is_deleted && v.cof_last_done.HasValue)
                .Where(v =>
                {
                    var dueDate = v.cof_last_done!.Value.Date.AddDays(365);
                    return dueDate >= today && dueDate <= reminderWindowEnd;
                })
                .ToList();

            result.TotalEmails = vehicles.Count;
            if (vehicles.Count == 0)
            {
                return result;
            }

            var activeContracts = (await _contractRepository.GetActiveContractsAsync()).ToList();
            var users = (await _userRepository.GetAllUsersAsync())
                .Where(u => !string.IsNullOrWhiteSpace(u.email))
                .ToList();
            var fallbackEmail = users.FirstOrDefault()?.email;

            foreach (var vehicle in vehicles)
            {
                var recipient = await ResolveVehicleRecipientAsync(
                    vehicle.vmf_code,
                    activeContracts,
                    fallbackEmail);

                if (recipient is null)
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"No recipient found for VMF {vehicle.vmf_code}");
                    continue;
                }

                var sent = await SendCofReminderAsync(
                    vehicle.vmf_code,
                    recipient.Value.Email,
                    recipient.Value.Name);

                if (sent) result.SuccessfulEmails++;
                else
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"Failed sending COF reminder for VMF {vehicle.vmf_code}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk COF reminders");
            result.ErrorMessages.Add($"Bulk operation failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<BulkEmailResult> SendBulkContractExpiryNotificationsAsync()
    {
        var result = new BulkEmailResult { CompletedAt = DateTime.Now };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var today = DateTime.Today;
            var reminderWindowEnd = today.AddDays(30);

            var contracts = (await _contractRepository.GetActiveContractsAsync())
                .Where(c => !c.is_deleted && c.target_return_date.HasValue)
                .Where(c => c.target_return_date!.Value.Date >= today && c.target_return_date.Value.Date <= reminderWindowEnd)
                .ToList();

            result.TotalEmails = contracts.Count;
            if (contracts.Count == 0)
            {
                return result;
            }

            foreach (var contract in contracts)
            {
                var daysRemaining = Math.Max(0, (contract.target_return_date!.Value.Date - today).Days);
                var sent = await SendContractExpiryReminderAsync(contract.contract_code, daysRemaining);

                if (sent) result.SuccessfulEmails++;
                else
                {
                    result.FailedEmails++;
                    result.ErrorMessages.Add($"Failed sending contract expiry reminder for Contract {contract.contract_code}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk contract expiry reminders");
            result.ErrorMessages.Add($"Bulk operation failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return result;
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
        if (string.IsNullOrWhiteSpace(template.TemplateName))
        {
            _logger.LogWarning("Cannot save email template with empty template name");
            return Task.FromResult(false);
        }

        lock (TemplateLock)
        {
            template.ModifiedDate = DateTime.UtcNow;
            if (template.CreatedDate == default)
            {
                template.CreatedDate = template.ModifiedDate;
            }

            _defaultTemplates[template.TemplateName] = template;
        }

        _logger.LogInformation("Saved email template: {TemplateName}", template.TemplateName);
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

            var sent = await SendHtmlEmailAsync(testEmailAddress, subject, body);
            _lastTestDate = DateTime.UtcNow;
            _lastTestSuccessful = sent;
            if (sent)
            {
                _lastErrorMessage = null;
            }

            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email configuration test failed");
            _lastTestDate = DateTime.UtcNow;
            _lastTestSuccessful = false;
            _lastErrorMessage = ex.Message;
            return false;
        }
    }

    public Task<EmailServiceStatus> GetEmailServiceStatusAsync()
    {
        return Task.FromResult(
            new EmailServiceStatus
            {
                IsConfigured = _isConfigured,
                IsConnected = _isConfigured && string.IsNullOrWhiteSpace(_lastErrorMessage),
                SmtpServer = _smtpServer,
                SmtpPort = _smtpPort,
                UseSSL = _useSSL,
                FromAddress = _fromAddress,
                FromName = _fromName,
                LastTestDate = _lastTestDate,
                LastTestSuccessful = _lastTestSuccessful,
                LastErrorMessage = _lastErrorMessage,
                DailyEmailsSent = _dailyEmailsSent,
                MonthlyEmailsSent = _monthlyEmailsSent,
            }
        );
    }

    #endregion

    private async Task<(string Email, string Name)?> ResolveVehicleRecipientAsync(
        int vmfCode,
        List<Contract> activeContracts,
        string? fallbackEmail)
    {
        var contract = activeContracts.FirstOrDefault(c => c.vmf_code == vmfCode);
        if (contract is not null)
        {
            var site = await _siteRepository.GetByIdAsync(contract.site_code);
            if (!string.IsNullOrWhiteSpace(site?.net_address))
            {
                return (site.net_address!, site.res_person ?? site.net_address!);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackEmail))
        {
            return (fallbackEmail!, fallbackEmail!);
        }

        return null;
    }

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
            RecordSuccessfulSend();
            _lastErrorMessage = null;

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
            _lastErrorMessage = ex.Message;
            return false;
        }
    }

    private void RecordSuccessfulSend()
    {
        var now = DateTime.UtcNow;
        if (_dailyCounterDate != now.Date)
        {
            _dailyCounterDate = now.Date;
            Interlocked.Exchange(ref _dailyEmailsSent, 0);
        }

        var monthStart = new DateTime(now.Year, now.Month, 1);
        if (_monthlyCounterDate != monthStart)
        {
            _monthlyCounterDate = monthStart;
            Interlocked.Exchange(ref _monthlyEmailsSent, 0);
        }

        Interlocked.Increment(ref _dailyEmailsSent);
        Interlocked.Increment(ref _monthlyEmailsSent);
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

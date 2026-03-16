using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Services;

/// <summary>
/// Reporting service implementation with PDF generation and export capabilities
/// Provides modern implementation of legacy GGFIS_v2.0 reporting functionality
/// Maps legacy report patterns to modern .NET 8 architecture
/// </summary>
public class ReportingService : IReportingService
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ITripRepository _tripRepository;
    private readonly IMaintenanceRecordRepository _maintenanceRepository;
    private readonly FisDbContext _context;
    private readonly ILogger<ReportingService> _logger;

    // Legacy report definitions mapping
    private static readonly List<ReportDefinition> _availableReports = new()
    {
        new ReportDefinition
        {
            ReportName = "VehicleReport",
            DisplayName = "Vehicle Report",
            Description = "Individual vehicle detailed report",
            Category = "Vehicle Reports",
            RequiresVehicleSelection = true,
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "vmf_code",
                    DisplayName = "Vehicle Code",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "MasterFileReport",
            DisplayName = "Master File Report - All Vehicles",
            Description = "Complete fleet listing with status",
            Category = "Vehicle Reports",
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            SupportsExcelExport = true,
        },
        new ReportDefinition
        {
            ReportName = "UniversalReport",
            DisplayName = "Universal Vehicle Report",
            Description = "Flexible vehicle reporting with filters",
            Category = "Vehicle Reports",
            RequiresDateRange = true,
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "report_type",
                    DisplayName = "Report Type",
                    DataType = "string",
                    IsRequired = true,
                    ValidValues = new List<string>
                    {
                        "Summary",
                        "Detailed",
                        "Financial",
                        "Maintenance",
                    },
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "SummaryIncomeReport",
            DisplayName = "Summary Income Split",
            Description = "Financial year income summary by department",
            Category = "Financial Reports",
            RequiresDateRange = false,
            SupportsPdfExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "financial_year",
                    DisplayName = "Financial Year",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "DetailedIncomeReport",
            DisplayName = "Detailed Income Split",
            Description = "Detailed financial transactions for financial year",
            Category = "Financial Reports",
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "financial_year",
                    DisplayName = "Financial Year",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "TariffListReport",
            DisplayName = "Published Tariffs",
            Description = "List of published tariffs for financial year",
            Category = "Financial Reports",
            SupportsPdfExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "financial_year",
                    DisplayName = "Financial Year",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "VehicleBillingHistoryReport",
            DisplayName = "Vehicle Billing History",
            Description = "Billing history for specific vehicle",
            Category = "Financial Reports",
            RequiresVehicleSelection = true,
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "vmf_code",
                    DisplayName = "Vehicle Code",
                    DataType = "int",
                    IsRequired = true,
                },
                new ReportParameter
                {
                    ParameterName = "financial_year",
                    DisplayName = "Financial Year",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "KiloGapsReport",
            DisplayName = "Kilometer Reading Gaps",
            Description = "Missing odometer readings report",
            Category = "Financial Reports",
            SupportsPdfExport = true,
            SupportsCsvExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "financial_year",
                    DisplayName = "Financial Year",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "ServiceHistoryReport",
            DisplayName = "Service History Report",
            Description = "Maintenance service history for vehicle",
            Category = "Maintenance Reports",
            RequiresVehicleSelection = true,
            SupportsPdfExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "vmf_code",
                    DisplayName = "Vehicle Code",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "MaintenanceScheduleReport",
            DisplayName = "Maintenance Schedule",
            Description = "Scheduled and overdue maintenance",
            Category = "Maintenance Reports",
            RequiresDateRange = true,
            SupportsPdfExport = true,
            SupportsCsvExport = true,
        },
        new ReportDefinition
        {
            ReportName = "TripSummaryReport",
            DisplayName = "Trip Summary Report",
            Description = "Trip summary by vehicle or date range",
            Category = "Trip Reports",
            RequiresDateRange = true,
            RequiresVehicleSelection = false,
            SupportsPdfExport = true,
            SupportsCsvExport = true,
        },
        new ReportDefinition
        {
            ReportName = "AuthorityReport",
            DisplayName = "Trip Authority Report",
            Description = "Trip authorization details and usage",
            Category = "Trip Reports",
            SupportsPdfExport = true,
            Parameters = new List<ReportParameter>
            {
                new ReportParameter
                {
                    ParameterName = "contract_id",
                    DisplayName = "Contract ID",
                    DataType = "int",
                    IsRequired = true,
                },
            },
        },
        new ReportDefinition
        {
            ReportName = "ContractSummaryReport",
            DisplayName = "Contract Summary",
            Description = "Contract overview and status",
            Category = "Contract Reports",
            SupportsPdfExport = true,
            SupportsCsvExport = true,
        },
    };

    public ReportingService(
        IVehicleRepository vehicleRepository,
        IContractRepository contractRepository,
        ITripRepository tripRepository,
        IMaintenanceRecordRepository maintenanceRepository,
        FisDbContext context,
        ILogger<ReportingService> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _contractRepository = contractRepository;
        _tripRepository = tripRepository;
        _maintenanceRepository = maintenanceRepository;
        _context = context;
        _logger = logger;
    }

    // ─── Lookup helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all reference lookup dictionaries in a single batch.
    /// Used by report methods that handle multiple vehicles/contracts/trips.
    /// </summary>
    private async Task<ReportLookups> LoadLookupsAsync()
    {
        var sites = await _context.Sites
            .Where(s => !s.is_deleted)
            .ToDictionaryAsync(s => s.Site_code, s => s.description ?? string.Empty);

        var statuses = await _context.VehicleStatuses
            .ToDictionaryAsync(s => s.vehicle_status_code, s => s.status_description ?? string.Empty);

        var types = await _context.VehicleTypes
            .Where(t => !t.is_deleted)
            .ToDictionaryAsync(t => t.type_code, t => t.type_description);

        // Model code → "{make_description} {model_description}"
        var models = await _context.Models
            .Include(m => m.Make)
            .Where(m => !m.is_deleted)
            .ToDictionaryAsync(
                m => m.model_code,
                m => new ModelInfo(
                    m.Make?.make_description ?? string.Empty,
                    m.model_description));

        // vmf_code → registration_number
        var vehicleRegs = await _context.Vehicles
            .Where(v => !v.is_deleted)
            .ToDictionaryAsync(v => v.vmf_code, v => v.registration_number ?? string.Empty);

        return new ReportLookups(sites, statuses, types, models, vehicleRegs);
    }

    private record ModelInfo(string Make, string Model);
    private record ReportLookups(
        Dictionary<short, string> Sites,
        Dictionary<short, string> Statuses,
        Dictionary<short, string> Types,
        Dictionary<short, ModelInfo> Models,
        Dictionary<int, string> VehicleRegistrations);

    #region Vehicle Reports

    public async Task<VehicleReport> GenerateVehicleReportAsync(int vmfCode)
    {
        _logger.LogInformation("Generating vehicle report for VMF Code: {VmfCode}", vmfCode);

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle == null)
        {
            throw new ArgumentException($"Vehicle with VMF Code {vmfCode} not found");
        }

        // Get related data
        var trips = await _tripRepository.GetTripsByVehicleAsync(vmfCode);
        var maintenance = await _maintenanceRepository.GetByVehicleAsync(vmfCode);

        var totalCosts = maintenance.Sum(m => m.TotalCost);
        var alerts = new List<string>();

        // Check for alerts
        if (
            vehicle.licence_due_date.HasValue
            && vehicle.licence_due_date < DateTime.Now.AddDays(30)
        )
            alerts.Add("Licence expiring within 30 days");

        // TODO: Add maintenance due checking
        // TODO: Add COF due checking

        // Resolve reference lookups for this single vehicle
        var lookups = await LoadLookupsAsync();
        lookups.Models.TryGetValue(vehicle.model_code, out var modelInfo);
        lookups.Statuses.TryGetValue(vehicle.vehicle_status_code, out var statusDesc);
        lookups.Types.TryGetValue(vehicle.type_code, out var typeDesc);
        lookups.Sites.TryGetValue(vehicle.location_code, out var locationName);

        return new VehicleReport
        {
            VmfCode = vehicle.vmf_code,
            RegistrationNumber = vehicle.registration_number ?? string.Empty,
            FleetNumber = vehicle.fleet_number ?? string.Empty,
            Make = modelInfo?.Make ?? string.Empty,
            Model = modelInfo?.Model ?? string.Empty,
            Type = typeDesc ?? string.Empty,
            YearManufactured = vehicle.year_manufactured ?? 0,
            Status = statusDesc ?? string.Empty,
            Department = locationName ?? string.Empty,
            Location = locationName ?? string.Empty,
            TakeOnDate = vehicle.take_on_date,
            CurrentOdometer = vehicle.current_odo,
            TotalCosts = totalCosts,
            MonthlyOverhead = vehicle.monthly_overhead ?? 0,
            NextServiceDate = null, // TODO: Calculate from service interval + last service date
            LicenceDueDate = vehicle.licence_due_date,
            CofDueDate = vehicle.cof_last_done,
            Alerts = alerts,
        };
    }

    public async Task<List<VehicleReport>> GenerateVehicleReportsAsync(List<int> vmfCodes)
    {
        var reports = new List<VehicleReport>();

        foreach (var vmfCode in vmfCodes)
        {
            try
            {
                var report = await GenerateVehicleReportAsync(vmfCode);
                reports.Add(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report for VMF Code: {VmfCode}", vmfCode);
            }
        }

        return reports;
    }

    public async Task<MasterFileReport> GenerateMasterFileReportAsync(int? vmfCode = null)
    {
        _logger.LogInformation("Generating master file report");

        List<Vehicle> vehicles;
        if (vmfCode.HasValue)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode.Value);
            vehicles = vehicle != null ? new List<Vehicle> { vehicle } : new List<Vehicle>();
        }
        else
        {
            vehicles = (await _vehicleRepository.GetAllAsync()).ToList();
        }

        // Load lookups once for the whole batch (not per-vehicle)
        var lookups = await LoadLookupsAsync();

        var vehicleReports = new List<VehicleReport>();
        var vehiclesByStatus = new Dictionary<string, int>();
        var vehiclesByDepartment = new Dictionary<string, int>();

        foreach (var vehicle in vehicles.Where(v => v != null))
        {
            var maintenance = await _maintenanceRepository.GetByVehicleAsync(vehicle.vmf_code);
            var totalCosts = maintenance.Sum(m => m.TotalCost);
            var alerts = new List<string>();
            if (vehicle.licence_due_date.HasValue && vehicle.licence_due_date < DateTime.Now.AddDays(30))
                alerts.Add("Licence expiring within 30 days");

            lookups.Models.TryGetValue(vehicle.model_code, out var modelInfo);
            lookups.Statuses.TryGetValue(vehicle.vehicle_status_code, out var statusDesc);
            lookups.Types.TryGetValue(vehicle.type_code, out var typeDesc);
            lookups.Sites.TryGetValue(vehicle.location_code, out var locationName);

            var report = new VehicleReport
            {
                VmfCode = vehicle.vmf_code,
                RegistrationNumber = vehicle.registration_number ?? string.Empty,
                FleetNumber = vehicle.fleet_number ?? string.Empty,
                Make = modelInfo?.Make ?? string.Empty,
                Model = modelInfo?.Model ?? string.Empty,
                Type = typeDesc ?? string.Empty,
                YearManufactured = vehicle.year_manufactured ?? 0,
                Status = statusDesc ?? string.Empty,
                Department = locationName ?? string.Empty,
                Location = locationName ?? string.Empty,
                TakeOnDate = vehicle.take_on_date,
                CurrentOdometer = vehicle.current_odo,
                TotalCosts = totalCosts,
                MonthlyOverhead = vehicle.monthly_overhead ?? 0,
                NextServiceDate = null,
                LicenceDueDate = vehicle.licence_due_date,
                CofDueDate = vehicle.cof_last_done,
                Alerts = alerts,
            };
            vehicleReports.Add(report);

            // Count by status
            if (!string.IsNullOrEmpty(report.Status))
            {
                vehiclesByStatus[report.Status] =
                    vehiclesByStatus.GetValueOrDefault(report.Status) + 1;
            }

            // Count by department
            if (!string.IsNullOrEmpty(report.Department))
            {
                vehiclesByDepartment[report.Department] =
                    vehiclesByDepartment.GetValueOrDefault(report.Department) + 1;
            }
        }

        return new MasterFileReport
        {
            GeneratedDate = DateTime.Now,
            Vehicles = vehicleReports,
            TotalVehicles = vehicleReports.Count,
            TotalFleetValue = vehicleReports.Sum(v => v.TotalCosts),
            VehiclesDueService = vehicleReports.Count(v =>
                v.NextServiceDate.HasValue && v.NextServiceDate < DateTime.Now.AddDays(30)
            ),
            VehiclesDueLicence = vehicleReports.Count(v =>
                v.LicenceDueDate.HasValue && v.LicenceDueDate < DateTime.Now.AddDays(30)
            ),
            VehiclesByStatus = vehiclesByStatus,
            VehiclesByDepartment = vehiclesByDepartment,
        };
    }

    public async Task<UniversalReport> GenerateUniversalReportAsync(UniversalReportRequest request)
    {
        _logger.LogInformation("Generating universal report: {ReportType}", request.ReportType);

        var dataRows = new List<Dictionary<string, object>>();
        var summary = new Dictionary<string, object>();

        // Implementation depends on report type
        switch (request.ReportType.ToLower())
        {
            case "summary":
                var masterReport = await GenerateMasterFileReportAsync(request.VmfCode);
                dataRows = masterReport
                    .Vehicles.Select(v => new Dictionary<string, object>
                    {
                        ["VMF_Code"] = v.VmfCode,
                        ["Registration"] = v.RegistrationNumber,
                        ["Fleet_Number"] = v.FleetNumber,
                        ["Make"] = v.Make,
                        ["Model"] = v.Model,
                        ["Status"] = v.Status,
                        ["Department"] = v.Department,
                        ["Current_Odometer"] = v.CurrentOdometer,
                    })
                    .ToList();
                summary["Total_Vehicles"] = masterReport.TotalVehicles;
                summary["Total_Fleet_Value"] = masterReport.TotalFleetValue;
                break;

            case "detailed":
                var detailedVehicles = await _context.Vehicles
                    .Where(v => !v.is_deleted && (request.VmfCode == null || v.vmf_code == request.VmfCode))
                    .Select(v => new
                    {
                        v.vmf_code, v.registration_number, v.fleet_number,
                        v.current_odo, v.year_manufactured, v.location_code,
                        v.model_code, v.monthly_overhead, v.purchase_amount
                    })
                    .ToListAsync();

                var detailModelIds = detailedVehicles.Select(v => v.model_code).Distinct().ToList();
                var detailSiteIds  = detailedVehicles.Select(v => v.location_code).Distinct().ToList();

                var detailModels = await _context.Models
                    .Where(m => detailModelIds.Contains(m.model_code))
                    .Join(_context.Makes, m => m.make_code, mk => mk.make_code,
                          (m, mk) => new { m.model_code, m.model_description, mk.make_description })
                    .ToDictionaryAsync(x => x.model_code);

                var detailSites = await _context.Sites
                    .Where(s => detailSiteIds.Contains(s.Site_code) && !s.is_deleted)
                    .Join(_context.Departments.Where(d => !d.is_deleted),
                          s => s.Depatrment_code, d => d.department_code,
                          (s, d) => new { s.Site_code, site_desc = s.description, dept_desc = d.description })
                    .ToDictionaryAsync(x => x.Site_code);

                var detailContracts = await _context.Contracts
                    .Where(c => !c.is_deleted && c.still_current == "Y")
                    .Select(c => new { c.vmf_code, c.contract_type, c.monthly_km })
                    .ToDictionaryAsync(c => c.vmf_code);

                var detailTariffs = await _context.LeaseTariffs
                    .Where(t => t.active && !t.is_deleted)
                    .Select(t => new { t.vmf_code, t.fixed_tariff })
                    .ToDictionaryAsync(t => t.vmf_code);

                dataRows = detailedVehicles.Select(v =>
                {
                    detailModels.TryGetValue(v.model_code, out var model);
                    detailSites.TryGetValue(v.location_code, out var site);
                    detailContracts.TryGetValue(v.vmf_code, out var contract);
                    detailTariffs.TryGetValue(v.vmf_code, out var tariff);
                    return new Dictionary<string, object>
                    {
                        ["VMF_Code"]         = v.vmf_code,
                        ["Fleet_Number"]     = (object)(v.fleet_number ?? ""),
                        ["Registration"]     = (object)(v.registration_number ?? ""),
                        ["Make"]             = (object)(model?.make_description ?? ""),
                        ["Model"]            = (object)(model?.model_description ?? ""),
                        ["Year"]             = (object)(v.year_manufactured ?? 0),
                        ["Current_Odo"]      = v.current_odo,
                        ["Site"]             = (object)(site?.site_desc ?? ""),
                        ["Department"]       = (object)(site?.dept_desc ?? ""),
                        ["Contract_Type"]    = (object)(contract?.contract_type ?? "None"),
                        ["Monthly_KM"]       = contract?.monthly_km ?? 0,
                        ["Monthly_Tariff"]   = tariff?.fixed_tariff ?? 0m,
                        ["Monthly_Overhead"] = v.monthly_overhead ?? 0m,
                    };
                }).ToList();

                summary["Total_Vehicles"]       = detailedVehicles.Count;
                summary["Active_Contracts"]     = detailContracts.Count;
                summary["Vehicles_With_Tariff"] = detailTariffs.Count;
                break;

            case "financial":
                var financialItems = await _context.InvoiceItems
                    .Where(ii => !ii.is_deleted
                        && (request.VmfCode == null || ii.vmf_code == request.VmfCode))
                    .Join(_context.Invoices.Where(i => !i.is_deleted),
                          ii => ii.invoice_code, i => i.invoice_code,
                          (ii, i) => new { ii, i })
                    .Join(_context.PostingMonths.Where(pm => !pm.is_deleted),
                          x => x.i.posting_month_code, pm => pm.posting_month_code,
                          (x, pm) => new { x.ii, x.i, pm })
                    .Join(_context.PostingYears.Where(py => !py.is_deleted),
                          x => x.pm.posting_year_code, py => py.posting_year_code,
                          (x, py) => new { x.ii, x.i, x.pm, py })
                    .Where(x =>
                        (request.StartDate == null || x.py.year_start_date >= request.StartDate) &&
                        (request.EndDate   == null || x.py.year_end_date   <= request.EndDate))
                    .Select(x => new
                    {
                        x.ii.vmf_code,
                        x.ii.fixed_tariff_amount,
                        x.ii.odo_tariff_amount,
                        x.i.department_code,
                        month_name   = x.pm.month_name,
                        month_number = (int)x.pm.month_number,
                        year         = x.py.year_start_date.Year,
                    })
                    .ToListAsync();

                dataRows = financialItems.Select(f => new Dictionary<string, object>
                {
                    ["VMF_Code"]       = f.vmf_code,
                    ["Department_Code"]= f.department_code,
                    ["Year"]           = f.year,
                    ["Month"]          = (object)(f.month_name ?? f.month_number.ToString()),
                    ["Fixed_Tariff"]   = f.fixed_tariff_amount,
                    ["Odo_Tariff"]     = f.odo_tariff_amount,
                    ["Total_Billed"]   = f.fixed_tariff_amount + f.odo_tariff_amount,
                }).ToList();

                summary["Total_Billed"]     = financialItems.Sum(f => f.fixed_tariff_amount + f.odo_tariff_amount);
                summary["Vehicles_Billed"]  = financialItems.Select(f => f.vmf_code).Distinct().Count();
                summary["Months_Covered"]   = financialItems.Select(f => new { f.year, f.month_number }).Distinct().Count();
                break;

            case "maintenance":
                var maintVmfFilter = request.VmfCode;
                var maintVehicles = await _context.Vehicles
                    .Where(v => !v.is_deleted && (maintVmfFilter == null || v.vmf_code == maintVmfFilter))
                    .Select(v => new { v.vmf_code, v.registration_number, v.fleet_number })
                    .ToListAsync();

                var maintVmfCodes = maintVehicles.Select(v => v.vmf_code).ToList();
                var maintRecords  = await _context.MaintenanceRecords
                    .Where(m => maintVmfCodes.Contains(m.VmfCode))
                    .Select(m => new { m.VmfCode, m.MaintenanceType, m.MaintenanceDate, m.TotalCost, m.OdometerReading })
                    .ToListAsync();

                var maintByVehicle = maintRecords.GroupBy(m => m.VmfCode)
                    .ToDictionary(g => g.Key, g => g.ToList());

                dataRows = maintVehicles.Select(v =>
                {
                    var records = maintByVehicle.GetValueOrDefault(v.vmf_code);
                    return new Dictionary<string, object>
                    {
                        ["VMF_Code"]             = v.vmf_code,
                        ["Registration"]         = (object)(v.registration_number ?? ""),
                        ["Fleet_Number"]         = (object)(v.fleet_number ?? ""),
                        ["Service_Count"]        = records?.Count ?? 0,
                        ["Total_Maintenance_Cost"]= records?.Sum(r => r.TotalCost) ?? 0m,
                        ["Last_Service_Date"]    = (object)(records?.Max(r => (DateTime?)r.MaintenanceDate)?.ToString("yyyy-MM-dd") ?? ""),
                        ["Last_Service_Odo"]     = records?.OrderByDescending(r => r.MaintenanceDate).FirstOrDefault()?.OdometerReading ?? 0,
                    };
                }).ToList();

                summary["Total_Maintenance_Cost"]  = maintRecords.Sum(r => r.TotalCost);
                summary["Total_Services"]          = maintRecords.Count;
                summary["Vehicles_With_Service"]   = maintByVehicle.Count;
                break;

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }

        // Only "financial" rows carry per-row Month/Year fields; all other types are
        // vehicle-master snapshots with no posting-date dimension.
        var supportsDateFilter = request.ReportType.ToLower() == "financial";

        return new UniversalReport
        {
            ReportType = request.ReportType,
            Title = $"Universal {request.ReportType} Report",
            GeneratedDate = DateTime.Now,
            ReportData = request.Parameters,
            DataRows = dataRows,
            Summary = summary,
            SupportsDateFilter = supportsDateFilter,
        };
    }

    #endregion

    #region Financial Reports

    public async Task<SummaryIncomeReport> GenerateSummaryIncomeReportAsync(int financialYear)
    {
        _logger.LogInformation("Generating summary income report for FY: {FinancialYear}", financialYear);

        // Pull all invoice_items for the requested financial year, joining through invoice → posting_month → posting_year
        var yearItems = await _context.InvoiceItems
            .Where(ii => !ii.is_deleted)
            .Join(_context.Invoices.Where(i => !i.is_deleted),
                  ii => ii.invoice_code, i => i.invoice_code,
                  (ii, i) => new { ii, i })
            .Join(_context.PostingMonths.Where(pm => !pm.is_deleted),
                  x => x.i.posting_month_code, pm => pm.posting_month_code,
                  (x, pm) => new { x.ii, x.i, pm })
            .Join(_context.PostingYears.Where(py => !py.is_deleted),
                  x => x.pm.posting_year_code, py => py.posting_year_code,
                  (x, py) => new { x.ii, x.i, x.pm, py })
            .Where(x => x.py.year_start_date.Year == financialYear)
            .Select(x => new
            {
                x.ii.vmf_code,
                x.i.department_code,
                x.ii.site_code,
                x.ii.fixed_tariff_amount,
                x.ii.odo_tariff_amount,
                month_name   = x.pm.month_name,
                month_number = (int)x.pm.month_number,
            })
            .ToListAsync();

        var deptCodes = yearItems.Select(x => x.department_code).Distinct().ToList();
        var deptNames = await _context.Departments
            .Where(d => deptCodes.Contains(d.department_code) && !d.is_deleted)
            .Select(d => new { d.department_code, d.description })
            .ToDictionaryAsync(d => d.department_code, d => d.description ?? $"Dept {d.department_code}");

        var vmfCodes = yearItems.Select(x => x.vmf_code).Distinct().ToList();
        var vehicleTypes = await _context.Vehicles
            .Where(v => vmfCodes.Contains(v.vmf_code) && !v.is_deleted)
            .Join(_context.VehicleTypes, v => v.type_code, t => t.type_code,
                  (v, t) => new { v.vmf_code, type = t.type_description })
            .ToDictionaryAsync(x => x.vmf_code, x => x.type ?? "Unknown");

        var totalIncome = yearItems.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount);

        var incomeByDept = yearItems
            .GroupBy(x => x.department_code)
            .ToDictionary(
                g => deptNames.GetValueOrDefault(g.Key, $"Dept {g.Key}"),
                g => g.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount));

        var incomeByMonth = yearItems
            .GroupBy(x => new { x.month_number, x.month_name })
            .OrderBy(g => g.Key.month_number)
            .ToDictionary(
                g => g.Key.month_name ?? $"Month {g.Key.month_number}",
                g => g.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount));

        var incomeByType = yearItems
            .GroupBy(x => vehicleTypes.GetValueOrDefault(x.vmf_code, "Unknown"))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount));

        return new SummaryIncomeReport
        {
            FinancialYear       = financialYear,
            GeneratedDate       = DateTime.Now,
            TotalIncome         = totalIncome,
            IncomeByDepartment  = incomeByDept,
            IncomeByMonth       = incomeByMonth,
            IncomeByVehicleType = incomeByType,
            BudgetedIncome      = 0,
            VarianceAmount      = 0,
            VariancePercentage  = 0,
            Summary = $"Financial year {financialYear}: {yearItems.Select(x => x.vmf_code).Distinct().Count()} vehicles billed, {yearItems.Count} invoice lines, total income R{totalIncome:N2}",
        };
    }

    public async Task<DetailedIncomeReport> GenerateDetailedIncomeReportAsync(int financialYear)
    {
        _logger.LogInformation("Generating detailed income report for FY: {FinancialYear}", financialYear);

        var detailItems = await _context.InvoiceItems
            .Where(ii => !ii.is_deleted)
            .Join(_context.Invoices.Where(i => !i.is_deleted),
                  ii => ii.invoice_code, i => i.invoice_code,
                  (ii, i) => new { ii, i })
            .Join(_context.PostingMonths.Where(pm => !pm.is_deleted),
                  x => x.i.posting_month_code, pm => pm.posting_month_code,
                  (x, pm) => new { x.ii, x.i, pm })
            .Join(_context.PostingYears.Where(py => !py.is_deleted),
                  x => x.pm.posting_year_code, py => py.posting_year_code,
                  (x, py) => new { x.ii, x.i, x.pm, py })
            .Where(x => x.py.year_start_date.Year == financialYear)
            .Select(x => new
            {
                x.ii.vmf_code,
                x.i.department_code,
                x.ii.fixed_tariff_amount,
                x.ii.odo_tariff_amount,
                x.ii.date_created,
                month_name = x.pm.month_name,
            })
            .ToListAsync();

        var vmfList = detailItems.Select(x => x.vmf_code).Distinct().ToList();
        var regNumbers = await _context.Vehicles
            .Where(v => vmfList.Contains(v.vmf_code) && !v.is_deleted)
            .Select(v => new { v.vmf_code, v.registration_number })
            .ToDictionaryAsync(v => v.vmf_code, v => v.registration_number ?? "");

        var deptList = detailItems.Select(x => x.department_code).Distinct().ToList();
        var deptNameMap = await _context.Departments
            .Where(d => deptList.Contains(d.department_code) && !d.is_deleted)
            .Select(d => new { d.department_code, d.description })
            .ToDictionaryAsync(d => d.department_code, d => d.description ?? $"Dept {d.department_code}");

        var incomeDetails = detailItems.Select(x => new IncomeDetailLine
        {
            VmfCode            = x.vmf_code.ToString(),
            RegistrationNumber = regNumbers.GetValueOrDefault(x.vmf_code, ""),
            Department         = deptNameMap.GetValueOrDefault(x.department_code, $"Dept {x.department_code}"),
            Amount             = x.fixed_tariff_amount + x.odo_tariff_amount,
            Description        = $"Monthly tariff — {x.month_name}",
            Date               = x.date_created,
        }).ToList();

        var totals = new Dictionary<string, decimal>
        {
            ["Total_Fixed_Tariff"] = detailItems.Sum(x => x.fixed_tariff_amount),
            ["Total_Odo_Tariff"]   = detailItems.Sum(x => x.odo_tariff_amount),
            ["Grand_Total"]        = detailItems.Sum(x => x.fixed_tariff_amount + x.odo_tariff_amount),
        };

        return new DetailedIncomeReport
        {
            FinancialYear  = financialYear,
            GeneratedDate  = DateTime.Now,
            IncomeDetails  = incomeDetails,
            Totals         = totals,
            Summary        = $"{incomeDetails.Count} line items, grand total R{totals["Grand_Total"]:N2}",
        };
    }

    public async Task<TariffListReport> GenerateTariffListReportAsync(int financialYear)
    {
        _logger.LogInformation("Generating tariff list report for FY: {FinancialYear}", financialYear);

        var tariffs = await _context.LeaseTariffs
            .Where(t => !t.is_deleted && t.active)
            .Join(_context.Vehicles.Where(v => !v.is_deleted),
                  t => t.vmf_code, v => v.vmf_code,
                  (t, v) => new { t, v })
            .Select(x => new
            {
                x.t.lease_tariff_code,
                x.t.fixed_tariff,
                x.t.start_date,
                x.t.end_date,
                x.v.vmf_code,
                x.v.registration_number,
                x.v.fleet_number,
            })
            .OrderBy(x => x.vmf_code)
            .ToListAsync();

        var tariffItems = tariffs.Select(t => new TariffItem
        {
            TariffCode  = t.lease_tariff_code.ToString(),
            Description = $"{t.fleet_number ?? t.vmf_code.ToString()} — {t.registration_number} (valid {t.start_date:yyyy-MM-dd} to {t.end_date:yyyy-MM-dd})",
            Rate        = t.fixed_tariff,
            Unit        = "Monthly",
        }).ToList();

        return new TariffListReport
        {
            FinancialYear = financialYear,
            GeneratedDate = DateTime.Now,
            Tariffs       = tariffItems,
            Summary       = $"{tariffItems.Count} active lease tariffs. Average monthly rate: R{(tariffItems.Count > 0 ? tariffItems.Average(t => t.Rate) : 0):N2}",
        };
    }

    public async Task<VehicleBillingHistoryReport> GenerateVehicleBillingHistoryAsync(int vmfCode, int financialYear)
    {
        _logger.LogInformation("Generating vehicle billing history for VMF: {VmfCode}, FY: {FinancialYear}", vmfCode, financialYear);

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle == null)
            throw new ArgumentException($"Vehicle with VMF Code {vmfCode} not found");

        var billingLines = await _context.InvoiceItems
            .Where(ii => !ii.is_deleted && ii.vmf_code == vmfCode)
            .Join(_context.Invoices.Where(i => !i.is_deleted),
                  ii => ii.invoice_code, i => i.invoice_code,
                  (ii, i) => new { ii, i })
            .Join(_context.PostingMonths.Where(pm => !pm.is_deleted),
                  x => x.i.posting_month_code, pm => pm.posting_month_code,
                  (x, pm) => new { x.ii, x.i, pm })
            .Join(_context.PostingYears.Where(py => !py.is_deleted),
                  x => x.pm.posting_year_code, py => py.posting_year_code,
                  (x, py) => new { x.ii, x.i, x.pm, py })
            .Where(x => x.py.year_start_date.Year == financialYear)
            .Select(x => new
            {
                x.ii.fixed_tariff_amount,
                x.ii.odo_tariff_amount,
                x.ii.start_odometer,
                x.ii.end_odometer,
                month_name   = x.pm.month_name,
                month_number = (int)x.pm.month_number,
                x.ii.date_created,
            })
            .OrderBy(x => x.month_number)
            .ToListAsync();

        var history = billingLines.Select(b => new BillingHistoryLine
        {
            Date        = b.date_created,
            Description = $"{b.month_name} — fixed R{b.fixed_tariff_amount:N2} + odo R{b.odo_tariff_amount:N2} ({b.start_odometer} → {b.end_odometer} km)",
            Amount      = b.fixed_tariff_amount + b.odo_tariff_amount,
            Reference   = $"FY{financialYear}/{b.month_number:D2}",
            Type        = "Tariff",
        }).ToList();

        var totalBilled   = history.Sum(h => h.Amount);
        var monthlyAvg    = history.Count > 0 ? totalBilled / history.Count : 0m;

        return new VehicleBillingHistoryReport
        {
            VmfCode                = vmfCode,
            RegistrationNumber     = vehicle.registration_number ?? string.Empty,
            FinancialYear          = financialYear,
            GeneratedDate          = DateTime.Now,
            BillingHistory         = history,
            TotalBilled            = totalBilled,
            AverageMonthlyBilling  = monthlyAvg,
            Summary                = $"{history.Count} billing periods in FY{financialYear}, total R{totalBilled:N2}, average R{monthlyAvg:N2}/month",
        };
    }

    public async Task<KiloGapsReport> GenerateKiloGapsReportAsync(int financialYear)
    {
        _logger.LogInformation("Generating kilo gaps report for FY: {FinancialYear}", financialYear);

        // Find odometer discontinuities: for each vehicle, compare consecutive invoice_item
        // end_odometer vs the next month's start_odometer across the financial year.
        var odoData = await _context.InvoiceItems
            .Where(ii => !ii.is_deleted)
            .Join(_context.Invoices.Where(i => !i.is_deleted),
                  ii => ii.invoice_code, i => i.invoice_code,
                  (ii, i) => new { ii, i })
            .Join(_context.PostingMonths.Where(pm => !pm.is_deleted),
                  x => x.i.posting_month_code, pm => pm.posting_month_code,
                  (x, pm) => new { x.ii, x.i, pm })
            .Join(_context.PostingYears.Where(py => !py.is_deleted),
                  x => x.pm.posting_year_code, py => py.posting_year_code,
                  (x, py) => new { x.ii, x.i, x.pm, py })
            .Where(x => x.py.year_start_date.Year == financialYear)
            .Select(x => new
            {
                x.ii.vmf_code,
                x.ii.start_odometer,
                x.ii.end_odometer,
                x.ii.start_odo_date,
                x.ii.end_odo_date,
                month_number = (int)x.pm.month_number,
            })
            .OrderBy(x => x.vmf_code).ThenBy(x => x.month_number)
            .ToListAsync();

        var gaps = new List<KiloGap>();
        foreach (var vehicleGroup in odoData.GroupBy(x => x.vmf_code))
        {
            var months = vehicleGroup.OrderBy(m => m.month_number).ToList();
            for (int i = 0; i < months.Count - 1; i++)
            {
                var current = months[i];
                var next    = months[i + 1];
                // A gap exists when next month's start_odo doesn't align with this month's end_odo
                if (next.start_odometer > 0 && current.end_odometer > 0
                    && next.start_odometer != current.end_odometer)
                {
                    gaps.Add(new KiloGap
                    {
                        VmfCode     = vehicleGroup.Key,
                        FromDate    = current.end_odo_date ?? DateTime.MinValue,
                        ToDate      = next.start_odo_date ?? DateTime.MinValue,
                        MissingDays = (next.start_odo_date - current.end_odo_date) is TimeSpan ts && ts.TotalDays > 0
                            ? (int)ts.TotalDays : 0,
                    });
                }
            }
        }

        return new KiloGapsReport
        {
            FinancialYear    = financialYear,
            GeneratedDate    = DateTime.Now,
            Gaps             = gaps,
            TotalGaps        = gaps.Count,
            VehiclesAffected = gaps.Select(g => g.VmfCode).Distinct().Count(),
            Summary          = $"{gaps.Count} odometer discontinuities found across {gaps.Select(g => g.VmfCode).Distinct().Count()} vehicles in FY{financialYear}",
        };
    }

    #endregion

    #region Maintenance Reports

    public async Task<ServiceHistoryReport> GenerateServiceHistoryReportAsync(int vmfCode)
    {
        _logger.LogInformation("Generating service history report for VMF: {VmfCode}", vmfCode);

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle == null)
        {
            throw new ArgumentException($"Vehicle with VMF Code {vmfCode} not found");
        }

        var maintenanceRecords = await _maintenanceRepository.GetByVehicleAsync(vmfCode);
        var totalCost = maintenanceRecords.Sum((MaintenanceRecord m) => m.TotalCost);
        var serviceCount = maintenanceRecords.Count();

        return new ServiceHistoryReport
        {
            VehicleCode = vmfCode,
            RegistrationNumber = vehicle.registration_number ?? string.Empty,
            MaintenanceRecords = maintenanceRecords.ToList(),
            TotalCost = totalCost,
            AverageCostPerService = serviceCount > 0 ? totalCost / serviceCount : 0,
            TotalServices = serviceCount,
            FirstServiceDate = maintenanceRecords.Any()
                ? maintenanceRecords.Min(m => m.MaintenanceDate)
                : (DateTime?)null,
            LastServiceDate = maintenanceRecords.Any()
                ? maintenanceRecords.Max(m => m.MaintenanceDate)
                : (DateTime?)null,
            ServiceIntervalDays = 90, // TODO: Calculate from service history
            ServiceIntervalKm = 10000, // TODO: Calculate from service history
            PreferredServiceProvider =
                maintenanceRecords
                    .GroupBy(m => m.ServiceProvider)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()
                    ?.Key
                ?? string.Empty,
        };
    }

    public async Task<MaintenanceScheduleReport> GenerateMaintenanceScheduleReportAsync(
        DateTime startDate,
        DateTime endDate
    )
    {
        _logger.LogInformation(
            "Generating maintenance schedule report from {StartDate} to {EndDate}",
            startDate,
            endDate
        );

        // TODO: Implement maintenance scheduling logic
        // This would calculate due dates based on service intervals and odometer readings

        await Task.CompletedTask; // Make method properly async

        return new MaintenanceScheduleReport
        {
            StartDate = startDate,
            EndDate = endDate,
            ScheduledItems = new List<ScheduledMaintenanceItem>(),
            OverdueItems = new List<OverdueMaintenanceItem>(),
        };
    }

    public async Task<MaintenanceCostReport> GenerateMaintenanceCostReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate
    )
    {
        _logger.LogInformation(
            "Generating maintenance cost report from {StartDate} to {EndDate} for VMF: {VmfCode}",
            startDate,
            endDate,
            vmfCode?.ToString() ?? "All"
        );

        var maintenanceRecords = vmfCode.HasValue
            ? await _maintenanceRepository.GetByVehicleAsync(vmfCode.Value)
            : await _maintenanceRepository.GetAllAsync();

        var filteredRecords = maintenanceRecords
            .Where(m => m.MaintenanceDate >= startDate && m.MaintenanceDate <= endDate)
            .ToList();

        var lookups = await LoadLookupsAsync();

        var costLines = filteredRecords
            .Select(m => new MaintenanceCostLine
            {
                VmfCode = m.VmfCode,
                RegistrationNumber = lookups.VehicleRegistrations.GetValueOrDefault(m.VmfCode, string.Empty),
                ServiceDate = m.MaintenanceDate,
                MaintenanceType = m.MaintenanceType,
                ServiceProvider = m.ServiceProvider ?? string.Empty,
                Cost = m.TotalCost,
                Odometer = m.OdometerReading,
                WorkDescription = m.Description,
            })
            .ToList();

        var totalCost = costLines.Sum(c => c.Cost);
        var costsByType = costLines
            .GroupBy(c => c.MaintenanceType)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Cost));
        var costsByMonth = costLines
            .GroupBy(c => c.ServiceDate.ToString("yyyy-MM"))
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Cost));

        return new MaintenanceCostReport
        {
            StartDate = startDate,
            EndDate = endDate,
            VmfCode = vmfCode,
            CostLines = costLines,
            TotalCost = totalCost,
            CostsByType = costsByType,
            CostsByMonth = costsByMonth,
        };
    }

    #endregion

    #region Trip Reports

    public async Task<TripSummaryReport> GenerateTripSummaryReportAsync(
        int? vmfCode,
        DateTime startDate,
        DateTime endDate
    )
    {
        _logger.LogInformation(
            "Generating trip summary report from {StartDate} to {EndDate} for VMF: {VmfCode}",
            startDate,
            endDate,
            vmfCode?.ToString() ?? "All"
        );

        var trips = vmfCode.HasValue
            ? await _tripRepository.GetTripsByVehicleAsync(vmfCode.Value)
            : await _tripRepository.GetAllAsync();

        var filteredTrips = trips
            .Where(t => t.issue_date >= startDate && t.issue_date <= endDate)
            .ToList();

        var lookups = await LoadLookupsAsync();

        // Build contract → site lookup for trip department resolution
        var contractIds = filteredTrips.Select(t => t.contract_code).Distinct().ToList();
        var contractSites = await _context.Contracts
            .Where(c => contractIds.Contains(c.contract_code))
            .ToDictionaryAsync(c => c.contract_code, c => c.site_code);

        var tripSummaries = filteredTrips
            .GroupBy(t => t.contract_code)
            .Select(g =>
            {
                contractSites.TryGetValue(g.Key, out var siteCode);
                var siteName = siteCode != 0
                    ? lookups.Sites.GetValueOrDefault(siteCode, string.Empty)
                    : string.Empty;
                return new TripSummaryLine
                {
                    VmfCode = g.Key,
                    RegistrationNumber = string.Empty, // trips don't carry vmf_code directly
                    TripCount = g.Count(),
                    TotalKilometers = g.Sum(t => t.end_odo_meter ?? 0),
                    TotalRevenue = 0,
                    Department = siteName,
                    FirstTrip = g.Min(t => t.issue_date),
                    LastTrip = g.Max(t => t.issue_date),
                };
            })
            .ToList();

        var tripsByDepartment = tripSummaries
            .GroupBy(s => s.Department)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.TripCount));

        return new TripSummaryReport
        {
            StartDate = startDate,
            EndDate = endDate,
            VmfCode = vmfCode,
            TripLines = tripSummaries, // Use TripLines instead of TripSummaries
            TotalTrips = filteredTrips.Count,
            TotalKilometers = tripSummaries.Sum(s => s.TotalKilometers),
            TotalRevenue = tripSummaries.Sum(s => s.TotalRevenue),
            TripsByDepartment = tripsByDepartment,
        };
    }

    public async Task<TripDetailReport> GenerateTripDetailReportAsync(int tripId)
    {
        _logger.LogInformation("Generating trip detail report for trip: {TripId}", tripId);

        var trip = await _tripRepository.GetByIdAsync(tripId);
        if (trip == null)
        {
            throw new ArgumentException($"Trip with ID {tripId} not found");
        }

        return new TripDetailReport
        {
            TripId = trip.trip_authority_code, // Use correct property name
            ContractCode = trip.contract_code, // Use available property
            TripDate = trip.issue_date, // Use correct date property
            ApproverName = trip.approver_name ?? string.Empty,
            ApproverRank = trip.approver_rank ?? string.Empty,
            TripReason = trip.trip_reason ?? string.Empty,
            ExpiryDate = trip.expiry_date,
            EndOdometer = trip.end_odo_meter ?? 0,
            TripKilometers = 0, // TODO: Calculate from odometer readings
            TripRevenue = 0, // TODO: Calculate revenue
            Purpose = trip.trip_reason ?? string.Empty, // Use available property
            AuthorityNumber = trip.trip_request_number ?? string.Empty, // Use available property
            Expenses = new List<TripExpense>(), // TODO: Implement trip expenses
        };
    }

    public async Task<AuthorityReport> GenerateAuthorityReportAsync(int contractId)
    {
        _logger.LogInformation(
            "Generating authority report for contract: {ContractId}",
            contractId
        );

        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract == null)
        {
            throw new ArgumentException($"Contract with ID {contractId} not found");
        }

        // TODO: Get related trips for this contract
        var relatedTrips = new List<Trip>();

        var lookups = await LoadLookupsAsync();

        return new AuthorityReport
        {
            ContractId = contract.contract_code,
            ContractCode = contract.contract_code,
            AuthorityNumber = contract.Authorisation ?? string.Empty,
            IssueDate = contract.start_date,
            ExpiryDate = contract.end_date ?? DateTime.MinValue,
            Purpose = contract.Notes ?? string.Empty,
            VmfCode = contract.vmf_code,
            DriverName = contract.Driver_name ?? string.Empty,
            RegistrationNumber = lookups.VehicleRegistrations.GetValueOrDefault(contract.vmf_code, string.Empty),
            Department = lookups.Sites.GetValueOrDefault(contract.site_code, string.Empty),
            AuthorizedKilometers = contract.monthly_km ?? 0, // Using monthly_km instead of allocated_km
            UsedKilometers = 0, // TODO: Calculate from trips
            RemainingKilometers = (contract.monthly_km ?? 0) - 0,
            Status = "Active", // Default status as schema doesn't contain status field
            RelatedTrips = relatedTrips
                .Select(t => new TripSummaryLine
                {
                    TripId = t.trip_authority_code, // Using trip_authority_code as ID
                    Date = t.issue_date,
                    StartOdometer = 0, // Trip entity doesn't have start_odo_meter
                    EndOdometer = t.end_odo_meter ?? 0,
                    Distance = t.end_odo_meter ?? 0, // Can't calculate distance without start odometer
                    DriverName = string.Empty, // TODO: Join with driver
                    VehicleRegistration = string.Empty, // TODO: Join with vehicle
                    Purpose = t.trip_reason ?? string.Empty,
                })
                .ToList(),
        };
    }

    #endregion

    #region Contract Reports

    public async Task<ContractSummaryReport> GenerateContractSummaryReportAsync(
        int? contractId = null
    )
    {
        _logger.LogInformation(
            "Generating contract summary report for contract: {ContractId}",
            contractId?.ToString() ?? "All"
        );

        List<Contract> contracts;
        if (contractId.HasValue)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId.Value);
            contracts = contract != null ? new List<Contract> { contract } : new List<Contract>();
        }
        else
        {
            contracts = (await _contractRepository.GetAllAsync()).ToList();
        }

        var lookups = await LoadLookupsAsync();

        var contractSummaries = contracts
            .Where(c => c != null)
            .Select(c => new ContractSummaryLine
            {
                ContractId = c.contract_code,
                ContractNumber = c.Authorisation ?? string.Empty,
                Department = lookups.Sites.GetValueOrDefault(c.site_code, string.Empty),
                StartDate = c.start_date,
                EndDate = c.end_date ?? DateTime.MinValue,
                ContractValue = 0,
                VehicleCount = 1,
                Status = c.still_current == "Y"
                    ? "Active"
                    : (c.contract_status_code == 7 ? "Closed"
                        : c.contract_status_code == 6 ? "Cancelled"
                        : "Inactive"),
                UsedKilometers = 0,
                AuthorizedKilometers = c.monthly_km ?? 0,
            })
            .ToList();

        return new ContractSummaryReport
        {
            ContractId = contractId,
            Contracts = contractSummaries,
            TotalContracts = contractSummaries.Count(),
            ActiveContracts = contractSummaries.Count(c => c.Status == "Active"),
            ExpiredContracts = contractSummaries.Count(c => c.EndDate < DateTime.Now),
            TotalValue = contractSummaries.Sum(c => c.ContractValue),
        };
    }

    public async Task<ContractBillingReport> GenerateContractBillingReportAsync(
        int contractId,
        DateTime startDate,
        DateTime endDate
    )
    {
        _logger.LogInformation(
            "Generating contract billing report for contract: {ContractId} from {StartDate} to {EndDate}",
            contractId,
            startDate,
            endDate
        );

        var contract = await _contractRepository.GetByIdAsync(contractId);
        if (contract == null)
        {
            throw new ArgumentException($"Contract with ID {contractId} not found");
        }

        // TODO: Implement billing line retrieval based on trips and rates
        var billingLines = new List<ContractBillingLine>();

        return new ContractBillingReport
        {
            ContractId = contractId,
            ContractNumber = contract.Authorisation ?? string.Empty, // Using Authorisation as contract number
            StartDate = startDate,
            EndDate = endDate,
            BillingLines = billingLines,
            TotalBilling = billingLines.Sum(b => b.Amount),
            BillingByVehicle = billingLines
                .GroupBy(b => b.VehicleRegistration)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Amount)),
        };
    }

    #endregion

    #region PDF Generation

    public async Task<byte[]> GenerateVehicleReportPdfAsync(int vmfCode)
    {
        _logger.LogInformation("Generating PDF for vehicle report: {VmfCode}", vmfCode);

        var report = await GenerateVehicleReportAsync(vmfCode);

        // TODO: Implement PDF generation using modern library
        // Options: PuppeteerSharp (HTML to PDF), QuestPDF, or iText7

        var htmlContent = GenerateVehicleReportHtml(report);
        return await ConvertHtmlToPdfAsync(htmlContent);
    }

    public async Task<byte[]> GenerateCustomReportPdfAsync(
        string reportType,
        Dictionary<string, object> parameters
    )
    {
        _logger.LogInformation("Generating custom PDF report: {ReportType}", reportType);

        // TODO: Implement custom report PDF generation
        // This would handle any report type dynamically

        var htmlContent = GenerateCustomReportHtml(reportType, parameters);
        return await ConvertHtmlToPdfAsync(htmlContent);
    }

    private string GenerateVehicleReportHtml(VehicleReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("    <title>Vehicle Report</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("        .header { border-bottom: 2px solid #000; padding-bottom: 10px; }");
        html.AppendLine("        .section { margin: 20px 0; }");
        html.AppendLine("        .field { margin: 5px 0; }");
        html.AppendLine("        .label { font-weight: bold; }");
        html.AppendLine("        .alert { color: red; font-weight: bold; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class='header'>");
        html.AppendLine($"        <h1>Vehicle Report - {report.RegistrationNumber}</h1>");
        html.AppendLine($"        <p>Generated: {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
        html.AppendLine("    </div>");
        html.AppendLine("    <div class='section'>");
        html.AppendLine("        <h2>Vehicle Details</h2>");
        html.AppendLine(
            $"        <div class='field'><span class='label'>VMF Code:</span> {report.VmfCode}</div>"
        );
        html.AppendLine(
            $"        <div class='field'><span class='label'>Registration:</span> {report.RegistrationNumber}</div>"
        );
        html.AppendLine(
            $"        <div class='field'><span class='label'>Fleet Number:</span> {report.FleetNumber}</div>"
        );
        html.AppendLine(
            $"        <div class='field'><span class='label'>Make/Model:</span> {report.Make} {report.Model}</div>"
        );
        html.AppendLine(
            $"        <div class='field'><span class='label'>Year:</span> {report.YearManufactured}</div>"
        );
        html.AppendLine(
            $"        <div class='field'><span class='label'>Current Odometer:</span> {report.CurrentOdometer:N0} km</div>"
        );
        html.AppendLine("    </div>");

        if (report.Alerts.Any())
        {
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h2>Alerts</h2>");
            foreach (var alert in report.Alerts)
            {
                html.AppendLine($"        <div class='alert'>{alert}</div>");
            }
            html.AppendLine("    </div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    private string GenerateCustomReportHtml(
        string reportType,
        Dictionary<string, object> parameters
    )
    {
        // TODO: Implement custom report HTML generation
        // This would generate HTML based on report type and parameters

        return $"<html><body><h1>{reportType} Report</h1><p>Custom report content here</p></body></html>";
    }

    private async Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent)
    {
        // TODO: Implement HTML to PDF conversion
        // Example using PuppeteerSharp:
        // await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        // await using var page = await browser.NewPageAsync();
        // await page.SetContentAsync(htmlContent);
        // return await page.PdfDataAsync();

        await Task.CompletedTask; // Make method properly async

        // For now, return HTML as bytes (placeholder)
        return Encoding.UTF8.GetBytes(htmlContent);
    }

    #endregion

    #region Export Functions

    public async Task<byte[]> ExportToCsvAsync<T>(List<T> data, string filename)
    {
        _logger.LogInformation(
            "Exporting {Count} records to CSV: {Filename}",
            data.Count,
            filename
        );

        await Task.CompletedTask; // Make method properly async
        var csv = new StringBuilder();

        if (data.Any())
        {
            // Get properties for header
            var properties = typeof(T).GetProperties();
            csv.AppendLine(string.Join(",", properties.Select(p => p.Name)));

            // Add data rows
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item)?.ToString() ?? string.Empty;
                    // Escape CSV values that contain commas, quotes, or newlines
                    if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                    {
                        value = "\"" + value.Replace("\"", "\"\"") + "\"";
                    }
                    return value;
                });
                csv.AppendLine(string.Join(",", values));
            }
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportToExcelAsync<T>(List<T> data, string filename)
    {
        _logger.LogInformation(
            "Exporting {Count} records to Excel: {Filename}",
            data.Count,
            filename
        );

        // TODO: Implement Excel export using EPPlus or similar library
        // For now, export as CSV
        return await ExportToCsvAsync(data, filename.Replace(".xlsx", ".csv"));
    }

    #endregion

    #region Legacy Compatibility

    public async Task<List<ReportDefinition>> GetAvailableReportsAsync()
    {
        _logger.LogInformation("Retrieving available report definitions");
        await Task.CompletedTask; // Make method properly async
        return _availableReports;
    }

    public async Task<ReportDefinition> GetReportDefinitionAsync(string reportName)
    {
        _logger.LogInformation("Retrieving report definition: {ReportName}", reportName);

        await Task.CompletedTask; // Make method properly async

        var definition = _availableReports.FirstOrDefault(r =>
            r.ReportName.Equals(reportName, StringComparison.OrdinalIgnoreCase)
        );

        if (definition == null)
        {
            throw new ArgumentException($"Report definition not found: {reportName}");
        }

        return definition;
    }

    #endregion
}

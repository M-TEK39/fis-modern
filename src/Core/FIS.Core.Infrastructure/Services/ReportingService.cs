using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
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
        ILogger<ReportingService> logger
    )
    {
        _vehicleRepository = vehicleRepository;
        _contractRepository = contractRepository;
        _tripRepository = tripRepository;
        _maintenanceRepository = maintenanceRepository;
        _logger = logger;
    }

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

        return new VehicleReport
        {
            VmfCode = vehicle.vmf_code,
            RegistrationNumber = vehicle.registration_number ?? string.Empty,
            FleetNumber = vehicle.fleet_number ?? string.Empty,
            Make = string.Empty, // TODO: Join with Make table
            Model = string.Empty, // TODO: Join with Model table
            Type = string.Empty, // TODO: Join with Type table
            YearManufactured = vehicle.year_manufactured ?? 0,
            Status = string.Empty, // TODO: Join with Status table
            Department = string.Empty, // TODO: Join with Department table
            Location = string.Empty, // TODO: Join with Location table
            TakeOnDate = vehicle.take_on_date,
            CurrentOdometer = vehicle.current_odo,
            TotalCosts = totalCosts,
            MonthlyOverhead = vehicle.monthly_overhead ?? 0,
            NextServiceDate = null, // TODO: Calculate from maintenance records
            LicenceDueDate = vehicle.licence_due_date,
            CofDueDate = vehicle.cof_last_done, // TODO: Calculate COF due date
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

        var vehicleReports = new List<VehicleReport>();
        var vehiclesByStatus = new Dictionary<string, int>();
        var vehiclesByDepartment = new Dictionary<string, int>();

        foreach (var vehicle in vehicles.Where(v => v != null))
        {
            var report = await GenerateVehicleReportAsync(vehicle.vmf_code);
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
                // Implement detailed vehicle report
                break;

            case "financial":
                // Implement financial report
                break;

            case "maintenance":
                // Implement maintenance report
                break;

            default:
                throw new ArgumentException($"Unknown report type: {request.ReportType}");
        }

        return new UniversalReport
        {
            ReportType = request.ReportType,
            Title = $"Universal {request.ReportType} Report",
            GeneratedDate = DateTime.Now,
            ReportData = request.Parameters,
            DataRows = dataRows,
            Summary = summary,
        };
    }

    #endregion

    #region Financial Reports

    public async Task<SummaryIncomeReport> GenerateSummaryIncomeReportAsync(int financialYear)
    {
        _logger.LogInformation(
            "Generating summary income report for FY: {FinancialYear}",
            financialYear
        );

        // TODO: Implement financial data retrieval
        // This would require implementing financial/billing tables and repositories

        await Task.CompletedTask; // Make method properly async

        return new SummaryIncomeReport
        {
            FinancialYear = financialYear,
            TotalIncome = 0,
            IncomeByDepartment = new Dictionary<string, decimal>(),
            IncomeByMonth = new Dictionary<string, decimal>(),
            IncomeByVehicleType = new Dictionary<string, decimal>(),
            BudgetedIncome = 0,
            VarianceAmount = 0,
            VariancePercentage = 0,
        };
    }

    public async Task<DetailedIncomeReport> GenerateDetailedIncomeReportAsync(int financialYear)
    {
        _logger.LogInformation(
            "Generating detailed income report for FY: {FinancialYear}",
            financialYear
        );

        // TODO: Implement detailed financial data retrieval
        await Task.CompletedTask; // Make method properly async

        // TODO: Implement detailed financial transaction retrieval

        return new DetailedIncomeReport
        {
            FinancialYear = financialYear,
            IncomeDetails = new List<IncomeDetailLine>(),
            Totals = new Dictionary<string, decimal>(),
        };
    }

    public async Task<TariffListReport> GenerateTariffListReportAsync(int financialYear)
    {
        _logger.LogInformation(
            "Generating tariff list report for FY: {FinancialYear}",
            financialYear
        );

        // TODO: Implement tariff retrieval from tariff tables
        await Task.CompletedTask; // Make method properly async

        return new TariffListReport
        {
            FinancialYear = financialYear,
            Tariffs = new List<TariffItem>(),
            GeneratedDate = DateTime.Now,
        };
    }

    public async Task<VehicleBillingHistoryReport> GenerateVehicleBillingHistoryAsync(
        int vmfCode,
        int financialYear
    )
    {
        _logger.LogInformation(
            "Generating vehicle billing history for VMF: {VmfCode}, FY: {FinancialYear}",
            vmfCode,
            financialYear
        );

        var vehicle = await _vehicleRepository.GetByIdAsync(vmfCode);
        if (vehicle == null)
        {
            throw new ArgumentException($"Vehicle with VMF Code {vmfCode} not found");
        }

        // TODO: Implement billing history retrieval

        return new VehicleBillingHistoryReport
        {
            VmfCode = vmfCode,
            RegistrationNumber = vehicle.registration_number ?? string.Empty,
            FinancialYear = financialYear,
            BillingHistory = new List<BillingHistoryLine>(),
            TotalBilled = 0,
            AverageMonthlyBilling = 0,
        };
    }

    public async Task<KiloGapsReport> GenerateKiloGapsReportAsync(int financialYear)
    {
        _logger.LogInformation(
            "Generating kilo gaps report for FY: {FinancialYear}",
            financialYear
        );

        // TODO: Implement gap detection in odometer readings
        // This would analyze trip records for missing or inconsistent odometer readings

        await Task.CompletedTask; // Make method properly async

        return new KiloGapsReport
        {
            FinancialYear = financialYear,
            Gaps = new List<KiloGap>(),
            TotalGaps = 0,
            VehiclesAffected = 0,
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

        var costLines = filteredRecords
            .Select(m => new MaintenanceCostLine
            {
                VmfCode = m.VmfCode,
                RegistrationNumber = string.Empty, // TODO: Join with vehicle
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

        var tripSummaries = filteredTrips
            .GroupBy(t => t.contract_code) // Group by contract instead of vmf_code
            .Select(g => new TripSummaryLine
            {
                VmfCode = g.Key, // Using contract_code as placeholder
                RegistrationNumber = string.Empty, // TODO: Join with vehicle
                TripCount = g.Count(),
                TotalKilometers = 0, // TODO: Calculate from actual trip records
                TotalRevenue = 0, // TODO: Calculate revenue
                Department = string.Empty, // TODO: Join with vehicle/department
                FirstTrip = g.Min(t => t.issue_date),
                LastTrip = g.Max(t => t.issue_date),
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

        return new AuthorityReport
        {
            ContractId = contract.contract_code, // Use correct property name
            ContractCode = contract.contract_code,
            AuthorityNumber = contract.Authorisation ?? string.Empty, // Use available property
            IssueDate = contract.start_date,
            ExpiryDate = contract.end_date ?? DateTime.MinValue,
            Purpose = contract.Notes ?? string.Empty, // Use Notes for purpose
            VmfCode = contract.vmf_code, // Remove null check - it's not nullable
            DriverName = contract.Driver_name ?? string.Empty,
            RegistrationNumber = string.Empty, // TODO: Join with vehicle
            Department = string.Empty, // TODO: Join with department
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

        var contractSummaries = contracts
            .Where(c => c != null)
            .Select(c => new ContractSummaryLine
            {
                ContractId = c.contract_code, // Using contract_code as ID
                ContractNumber = c.Authorisation ?? string.Empty, // Using Authorisation as contract number
                Department = string.Empty, // TODO: Join with department
                StartDate = c.start_date,
                EndDate = c.end_date ?? DateTime.MinValue,
                ContractValue = 0, // TODO: Contract value not available in current schema
                VehicleCount = 1, // TODO: Count vehicles for contract
                Status = "Active", // Default status as schema doesn't contain status field
                UsedKilometers = 0, // TODO: Calculate from trips
                AuthorizedKilometers = c.monthly_km ?? 0, // Using monthly_km instead of allocated_km
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

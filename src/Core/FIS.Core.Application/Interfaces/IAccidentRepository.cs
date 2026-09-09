using FIS.Core.Domain.Entities;

namespace FIS.Core.Application.Interfaces;

public interface IAccidentRepository
{
    Task<Accident?> GetByIdAsync(int accidentCode);
    Task<IEnumerable<Accident>> GetAllAsync();
    Task<AccidentMaintenancePage> GetMaintenancePageAsync(AccidentMaintenancePageQuery query);
    Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<AccidentDriverReportRow>> GetDriverReportAsync(
        string searchTerm,
        bool searchById
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetVehicleReportAsync(
        string searchTerm,
        bool searchByFleet
    );
    Task<IEnumerable<AccidentOutstandingDocumentLookupRow>> GetOutstandingDocumentLookupAsync(
        string searchTerm,
        bool searchByFleet
    );
    Task<AccidentOutstandingDocumentReport?> GetOutstandingDocumentReportAsync(int accidentCode);
    Task<IEnumerable<AccidentOutstandingDocumentLookupRow>> GetInspectionLetterLookupAsync(
        string searchTerm,
        bool searchByFleet
    );
    Task<AccidentOutstandingDocumentReport?> GetInspectionLetterReportAsync(int accidentCode);
    Task<IEnumerable<AccidentVehicleReportRow>> GetPrivateVehicleReportAsync(
        string searchTerm,
        bool searchByDescription
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetNewAccidentsReportAsync(string mode);
    Task<IEnumerable<AccidentVehicleReportRow>> GetAllAccidentsReportAsync(string mode);
    Task<IEnumerable<AccidentVehicleReportRow>> GetGarageAccidentsReportAsync(string mode);
    Task<IEnumerable<AccidentVehicleReportRow>> GetDuplicateAccidentsReportAsync(string garageMode);
    Task<IEnumerable<AccidentVehicleReportRow>> GetDepartmentPeriodReportAsync(
        string departmentNumber,
        DateTime startDate,
        DateTime endDate
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetDepartmentPeriodVipReportAsync(
        string departmentNumber,
        DateTime startDate,
        DateTime endDate,
        string hireTypeMode
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetDepartmentMonthReportAsync(
        string departmentNumber,
        string garageMode,
        string periodMode,
        int? year,
        int? month
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetDepartmentFinancialYearReportAsync(
        string departmentNumber,
        string garageMode,
        string financialYear
    );
    Task<IEnumerable<AccidentVehicleReportRow>> GetAccidentCostsFinancialYearReportAsync(
        string financialYear
    );
    Task<IEnumerable<AccidentPeriodReportRow>> GetPeriodReportAsync(
        string departmentNumber,
        DateTime startDate,
        DateTime endDate,
        bool closed
    );
    Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<AccidentClaimsSummary> GetClaimsSummaryAsync();
    Task<IEnumerable<AccidentReport>> GetRecentReportsAsync(int limit = 10);
    Task<IEnumerable<AccidentLastGgReferenceRow>> GetLastGgReferenceReportAsync();
    Task<IEnumerable<AccidentOutstandingClaim>> GetOutstandingClaimsAsync();
    Task<AccidentStatistics> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<Accident> CreateAsync(Accident accident, int currentUserId);
    Task<Accident> UpdateAsync(Accident accident, int currentUserId);
    Task<Accident> UpdateHqAsync(Accident accident, int currentUserId);
    Task DeleteAsync(int accidentCode, int currentUserId);
}

/// <summary>
/// The operational accident-maintenance grid only. Report and dashboard queries
/// deliberately remain complete result sets.
/// </summary>
public sealed record AccidentMaintenancePageQuery(
    int Page,
    int PageSize,
    string SearchType,
    string SearchTerm,
    int? LocationCode
);

public sealed record AccidentMaintenanceListItem(
    int AccidentCode,
    string? VehicleNumber,
    string? HireType,
    DateTime? AccidentDate,
    string? Reference
);

public sealed record AccidentMaintenancePage(
    IReadOnlyList<AccidentMaintenanceListItem> Data,
    int Page,
    int PageSize,
    int TotalRecords
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
}

public class AccidentDriverReportRow
{
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
    public string driver_name { get; set; } = "";
    public string driver_employ_number { get; set; } = "";
    public DateTime? occurence_date { get; set; }
    public string department_number { get; set; } = "";
    public string site_description { get; set; } = "";
    public decimal? cost_of_repair { get; set; }
}

public class AccidentVehicleReportRow
{
    public int accident_code { get; set; }
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
    public string location_description { get; set; } = "";
    public DateTime? occurence_date { get; set; }
    public DateTime? occurence_time { get; set; }
    public string occurence_place { get; set; } = "";
    public string fin_year { get; set; } = "";
    public decimal? call_refer { get; set; }
    public string hire_type { get; set; } = "";
    public DateTime? date_updated { get; set; }
    public DateTime? reported_date { get; set; }
    public string flag_gg_hq { get; set; } = "";
    public DateTime? flag_gg_hq_date { get; set; }
    public string flag_trip_author { get; set; } = "";
    public DateTime? flag_trip_auth_date { get; set; }
    public string description { get; set; } = "";
    public string accident_type_description { get; set; } = "";
    public string trip_author { get; set; } = "";
    public string driver_name { get; set; } = "";
    public string driver_employ_number { get; set; } = "";
    public string department_number { get; set; } = "";
    public string site_description { get; set; } = "";
    public string transoffic_name { get; set; } = "";
    public string transoffic_tel { get; set; } = "";
    public string hq_reference { get; set; } = "";
    public string gg_reference { get; set; } = "";
    public string sa_reference { get; set; } = "";
    public string case_number { get; set; } = "";
    public decimal? cost_of_repair { get; set; }
    public string damage_description { get; set; } = "";
    public string driver_fault { get; set; } = "";
    public string death { get; set; } = "";
    public string injured { get; set; } = "";
    public string third_party_regno { get; set; } = "";
    public string third_party_owner { get; set; } = "";
    public decimal? third_party_claim { get; set; }
    public DateTime? priv_dampay_date { get; set; }
    public string insurance_claim { get; set; } = "";
    public string th_claim_receive { get; set; } = "";
    public decimal? claim_against_dept { get; set; }
    public string th_claim_accept_reject { get; set; } = "";
    public string th_claim_reject_reason { get; set; } = "";
    public decimal? write_off_amount { get; set; }
    public DateTime? write_off_date { get; set; }
    public string letterhead { get; set; } = "";
    public string z181 { get; set; } = "";
    public DateTime? file_close_date { get; set; }
    public string notes { get; set; } = "";
}

public class AccidentOutstandingDocumentLookupRow
{
    public int accident_code { get; set; }
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
    public string gg_reference { get; set; } = "";
    public DateTime? occurence_date { get; set; }
}

public class AccidentOutstandingDocumentReport
{
    public int accident_code { get; set; }
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
    public string gg_reference { get; set; } = "";
    public string department_number { get; set; } = "";
    public string site_description { get; set; } = "";
    public string address1 { get; set; } = "";
    public string address2 { get; set; } = "";
    public string postal_code { get; set; } = "";
    public string res_person { get; set; } = "";
    public string telephone { get; set; } = "";
    public string fax { get; set; } = "";
    public DateTime? reported_date { get; set; }
    public string damage_description { get; set; } = "";
    public bool document_status_tracking_available { get; set; }
    public IReadOnlyList<string> outstanding_documents { get; set; } = Array.Empty<string>();
}

public class AccidentLastGgReferenceRow
{
    public int accident_code { get; set; }
    public string gg_reference { get; set; } = "";
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
}

public class AccidentPeriodReportRow
{
    public string registration_number { get; set; } = "";
    public string fleet_number { get; set; } = "";
    public DateTime? occurence_date { get; set; }
    public string department_number { get; set; } = "";
    public string site_description { get; set; } = "";
    public string hire_type { get; set; } = "";
    public string accident_description { get; set; } = "";
    public string driver_name { get; set; } = "";
    public string transoffic_name { get; set; } = "";
    public decimal? call_refer { get; set; }
    public decimal? cost_of_repair { get; set; }
    public DateTime? file_close_date { get; set; }
}

/// <summary>
/// Summary of accident claims for dashboard
/// </summary>
public class AccidentClaimsSummary
{
    public int total_accidents { get; set; }
    public decimal total_claims_value { get; set; }
    public int open_claims { get; set; }
    public int closed_claims { get; set; }
    public decimal pending_claim_amount { get; set; }
}

/// <summary>
/// Recent accident report summary
/// </summary>
public class AccidentReport
{
    public int accident_code { get; set; }
    public string accident_reference { get; set; } = "";
    public DateTime accident_date { get; set; }
    public string vehicle_registration { get; set; } = "";
    public string severity { get; set; } = "";
    public string status { get; set; } = "";
}

/// <summary>
/// Outstanding claim information
/// </summary>
public class AccidentOutstandingClaim
{
    public int accident_id { get; set; }
    public string accident_reference { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public DateTime accident_date { get; set; }
    public string claim_type { get; set; } = "";
    public decimal? claim_amount { get; set; }
    public string status { get; set; } = "";
    public int days_outstanding { get; set; }
}

/// <summary>
/// Comprehensive accident statistics
/// </summary>
public class AccidentStatistics
{
    public int total_accidents { get; set; }
    public decimal total_repair_costs { get; set; }
    public decimal total_third_party_claims { get; set; }
    public decimal total_department_claims { get; set; }
    public int accidents_this_month { get; set; }
    public int accidents_this_year { get; set; }
    public decimal average_cost_per_accident { get; set; }
    public string most_common_severity { get; set; } = "";
    public List<DepartmentAccidentSummary> department_breakdown { get; set; } = new();
    public List<MonthlySummary> monthly_breakdown { get; set; } = new();
}

public class DepartmentAccidentSummary
{
    public string department_name { get; set; } = "";
    public int accident_count { get; set; }
    public decimal total_cost { get; set; }
}

public class MonthlySummary
{
    public int year { get; set; }
    public int month { get; set; }
    public string month_name { get; set; } = "";
    public int accident_count { get; set; }
    public decimal total_cost { get; set; }
}

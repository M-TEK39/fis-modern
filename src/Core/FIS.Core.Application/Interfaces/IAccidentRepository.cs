using FIS.Core.Domain.Entities;
namespace FIS.Core.Application.Interfaces;
public interface IAccidentRepository
{
    Task<Accident?> GetByIdAsync(int accidentCode);
    Task<IEnumerable<Accident>> GetAllAsync();
    Task<IEnumerable<Accident>> GetByVehicleAsync(int vmfCode);
    Task<IEnumerable<AccidentDriverReportRow>> GetDriverReportAsync(string searchTerm, bool searchById);
    Task<IEnumerable<AccidentVehicleReportRow>> GetVehicleReportAsync(string searchTerm, bool searchByFleet);
    Task<IEnumerable<Accident>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<AccidentClaimsSummary> GetClaimsSummaryAsync();
    Task<IEnumerable<AccidentReport>> GetRecentReportsAsync(int limit = 10);
    Task<IEnumerable<AccidentOutstandingClaim>> GetOutstandingClaimsAsync();
    Task<AccidentStatistics> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<Accident> CreateAsync(Accident accident, int currentUserId);
    Task<Accident> UpdateAsync(Accident accident, int currentUserId);
    Task<Accident> UpdateHqAsync(Accident accident, int currentUserId);
    Task DeleteAsync(int accidentCode, int currentUserId);
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
    public DateTime? date_updated { get; set; }
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

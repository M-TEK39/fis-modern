namespace FIS.Web.Models;

public sealed record FullMaintenanceLeaseTermDto
{
    public int VehicleContractTermID { get; set; }
    public int leasecontract_code { get; set; }
    public int? vmf_Code { get; set; }
    public int? vmf_code { get; set; }
    public int? AgreedTerms { get; set; }
    public long? AgreedKilos { get; set; }
    public decimal? AppliedInterest { get; set; }
    public decimal? FixedMonthlyAmount { get; set; }
    public int? AuthorityStatus { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
    public int? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public int? created_by_user_code { get; set; }
    public int? modified_by_user_code { get; set; }
    public string? authority_comment { get; set; }
    public string? rejection_reason { get; set; }
    public string? lease_status { get; set; }
    public string? lease_notes { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? lease_startdate { get; set; }
    public DateTime? lease_enddate { get; set; }
    public DateTime date_created { get; set; }
    public DateTime? date_updated { get; set; }
    public bool is_deleted { get; set; }

    public int TermId => VehicleContractTermID != 0 ? VehicleContractTermID : leasecontract_code;
    public int VmfCode => vmf_Code ?? 0;
    public int VmfCode => vmf_code ?? 0;
    public DateTime? EffectiveStartDate => StartDate ?? lease_startdate;
    public DateTime? EffectiveEndDate => EndDate ?? lease_enddate;
}

public static class FullMaintenanceLeaseUi
{
    public static string MapAuthorityStatus(int? status) => status switch
    {
        1 => "Pending",
        2 => "Approved",
        4 => "Rejected",
        _ => "Unknown"
    };

    public static string GetAuthorityBadgeClass(int? status) => status switch
    {
        2 => "badge badge-success",
        4 => "badge badge-error",
        _ => "badge badge-warning"
    };

    public static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;

    public static string FormatVehicleLabel(VehicleDto vehicle)
        => $"{ValueOrDash(vehicle.fleet_number)} / {ValueOrDash(vehicle.registration_number)} ({vehicle.vmf_code})";

    public static List<VehicleDto> FilterVehicleMatches(List<VehicleDto> matches, string keyword, string searchMode)
    {
        var query = keyword.Trim();
        var isGp = string.Equals(searchMode, "GP", StringComparison.OrdinalIgnoreCase);

        return matches
            .Where(v => isGp
                ? !string.IsNullOrWhiteSpace(v.registration_number) && v.registration_number.Contains(query, StringComparison.OrdinalIgnoreCase)
                : !string.IsNullOrWhiteSpace(v.fleet_number) && v.fleet_number.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(v => isGp
                ? string.Equals(v.registration_number?.Trim(), query, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v.fleet_number?.Trim(), query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(v => v.vmf_code)
            .ToList();
    }
}

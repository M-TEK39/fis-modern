namespace FIS.Web.Models;

public class RegistrationHistoryResponseDto
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? current_registration { get; set; }
    public List<RegistrationHistoryEntryDto> history { get; set; } = new();
}

public class RegistrationHistoryEntryDto
{
    public int registration_id { get; set; }
    public string? registration_number { get; set; }
    public DateTime? recorded_date { get; set; }
    public bool is_current { get; set; }
}

public class RegistrationSearchResponseDto
{
    public string? search_term { get; set; }
    public int total { get; set; }
    public List<RegistrationSearchResultDto> results { get; set; } = new();
}

public class RegistrationSearchResultDto
{
    public int vmf_code { get; set; }
    public string? fleet_number { get; set; }
    public string? current_registration { get; set; }
    public string? matched_registration { get; set; }
    public bool is_historical_match { get; set; }
    public DateTime? recorded_date { get; set; }
}

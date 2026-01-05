using FIS.Web.Models;

namespace FIS.Web.Services;

public class AccidentApiService
{
    private readonly HttpClient _httpClient;

    public AccidentApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AccidentDto>> GetAccidentsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Accident");
            response.EnsureSuccessStatusCode();
            
            var accidents = await response.Content.ReadFromJsonAsync<List<AccidentDto>>();
            return accidents ?? new List<AccidentDto>();
        }
        catch (HttpRequestException)
        {
            return new List<AccidentDto>();
        }
        catch (Exception)
        {
            return new List<AccidentDto>();
        }
    }

    public async Task<ClaimsSummaryDto> GetClaimsSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/accidents/claims-summary");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<ClaimsSummaryDto>() ?? new ClaimsSummaryDto();
        }
        catch
        {
            return new ClaimsSummaryDto();
        }
    }

    public async Task<List<ReportDto>> GetRecentReportsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/accidents/reports");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<List<ReportDto>>() ?? new List<ReportDto>();
        }
        catch
        {
            return new List<ReportDto>();
        }
    }

    public async Task<AccidentDto?> GetAccidentAsync(int accidentId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Accident/{accidentId}");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<AccidentDto>();
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<AccidentDto> CreateAccidentAsync(AccidentDto accident)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Accident", accident);
            response.EnsureSuccessStatusCode();
            
            var createdAccident = await response.Content.ReadFromJsonAsync<AccidentDto>();
            return createdAccident ?? throw new InvalidOperationException("Failed to create accident");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<AccidentDto> UpdateAccidentAsync(int accidentId, AccidentDto accident)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/Accident/{accidentId}", accident);
            response.EnsureSuccessStatusCode();
            
            var updatedAccident = await response.Content.ReadFromJsonAsync<AccidentDto>();
            return updatedAccident ?? throw new InvalidOperationException("Failed to update accident");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task DeleteAccidentAsync(int accidentId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/Accident/{accidentId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<List<OutstandingClaimDto>> GetOutstandingClaimsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/accidents/outstanding-claims");
            response.EnsureSuccessStatusCode();
            
            var claims = await response.Content.ReadFromJsonAsync<List<OutstandingClaimDto>>();
            return claims ?? new List<OutstandingClaimDto>();
        }
        catch (HttpRequestException)
        {
            return new List<OutstandingClaimDto>();
        }
        catch (Exception)
        {
            return new List<OutstandingClaimDto>();
        }
    }

    public async Task<AccidentStatisticsDto> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = "api/accidents/statistics";
            var queryParams = new List<string>();
            
            if (fromDate.HasValue)
                queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue)
                queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
                
            if (queryParams.Count > 0)
                query += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(query);
            response.EnsureSuccessStatusCode();
            
            var stats = await response.Content.ReadFromJsonAsync<AccidentStatisticsDto>();
            return stats ?? new AccidentStatisticsDto();
        }
        catch (HttpRequestException)
        {
            return new AccidentStatisticsDto();
        }
        catch (Exception)
        {
            return new AccidentStatisticsDto();
        }
    }
}

// DTOs for Accident operations (AccidentDto is in Models namespace)
public class OutstandingClaimDto
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

public class AccidentStatisticsDto
{
    public int total_accidents { get; set; }
    public decimal total_repair_costs { get; set; }
    public decimal total_third_party_claims { get; set; }
    public decimal total_department_claims { get; set; }
    public int accidents_this_month { get; set; }
    public int accidents_this_year { get; set; }
    public decimal average_cost_per_accident { get; set; }
    public string most_common_severity { get; set; } = "";
    public List<DepartmentAccidentSummaryDto> department_breakdown { get; set; } = new();
    public List<MonthlySummaryDto> monthly_breakdown { get; set; } = new();
}

public class DepartmentAccidentSummaryDto
{
    public string department_name { get; set; } = "";
    public int accident_count { get; set; }
    public decimal total_cost { get; set; }
}

public class MonthlySummaryDto
{
    public int year { get; set; }
    public int month { get; set; }
    public string month_name { get; set; } = "";
    public int accident_count { get; set; }
    public decimal total_cost { get; set; }
}

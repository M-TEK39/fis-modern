using FIS.Web.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FIS.Web.Services;

internal class ApiContractResponse
{
    [JsonPropertyName("contractCode")]
    public int ContractCode { get; set; }

    [JsonPropertyName("vmfCode")]
    public int VmfCode { get; set; }

    [JsonPropertyName("siteCode")]
    public short SiteCode { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("stillCurrent")]
    public string? StillCurrent { get; set; }

    [JsonPropertyName("contractTypeCode")]
    public string? ContractTypeCode { get; set; }

    [JsonPropertyName("contractStatusCode")]
    public short? ContractStatusCode { get; set; }

    [JsonPropertyName("userCode")]
    public short? UserCode { get; set; }

    [JsonPropertyName("approverCode")]
    public int? ApproverCode { get; set; }

    [JsonPropertyName("createdByUserCode")]
    public int? CreatedByUserCode { get; set; }

    [JsonPropertyName("modifiedByUserCode")]
    public int? ModifiedByUserCode { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("startOdometer")]
    public int? StartOdometer { get; set; }

    [JsonPropertyName("driverId")]
    public string? DriverId { get; set; }

    [JsonPropertyName("targetReturnDate")]
    public DateTime? TargetReturnDate { get; set; }
}

public class ContractApiService
{
    private readonly HttpClient _httpClient;
    private const int RecentContractWindow = 80;

    public ContractApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FIS.Web.Models.ContractDto>> GetContractsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Contracts/active");
            response.EnsureSuccessStatusCode();

            var apiContracts = await response.Content.ReadFromJsonAsync<List<ApiContractResponse>>();
            if (apiContracts == null)
            {
                return new List<FIS.Web.Models.ContractDto>();
            }

            return apiContracts.Select(MapToDto).ToList();
        }
        catch (HttpRequestException)
        {
            // Return empty list if API is not available
            return new List<FIS.Web.Models.ContractDto>();
        }
        catch (Exception)
        {
            // Return empty list for any other errors
            return new List<FIS.Web.Models.ContractDto>();
        }
    }

    public async Task<ContractPageResult> GetContractsPageAsync(ContractsPageQuery query)
    {
        try
        {
            var parameters = new List<string>
            {
                $"page={Math.Max(1, query.Page)}",
                $"pageSize={Math.Max(1, query.PageSize)}"
            };

            if (query.StatusCode.HasValue)
            {
                parameters.Add($"status={query.StatusCode.Value}");
            }

            if (query.SiteCode.HasValue)
            {
                parameters.Add($"siteCode={query.SiteCode.Value}");
            }

            if (!string.IsNullOrWhiteSpace(query.StillCurrent))
            {
                parameters.Add($"stillCurrent={Uri.EscapeDataString(query.StillCurrent)}");
            }

            if (query.StartDateFrom.HasValue)
            {
                parameters.Add($"startDateFrom={query.StartDateFrom.Value:yyyy-MM-dd}");
            }

            if (query.StartDateTo.HasValue)
            {
                parameters.Add($"startDateTo={query.StartDateTo.Value:yyyy-MM-dd}");
            }

            if (query.VmfCode.HasValue)
            {
                parameters.Add($"vmfCode={query.VmfCode.Value}");
            }

            var endpoint = $"api/contracts?{string.Join("&", parameters)}";
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<PagedContractsResponse>();
            if (payload == null)
            {
                return ContractPageResult.Empty(query.Page, query.PageSize);
            }

            return new ContractPageResult
            {
                Page = payload.Page,
                PageSize = payload.PageSize,
                TotalRecords = payload.TotalRecords,
                TotalPages = payload.TotalPages,
                Data = payload.Data?.Select(MapToDto).ToList() ?? new List<ContractDto>()
            };
        }
        catch
        {
            return ContractPageResult.Empty(query.Page, query.PageSize);
        }
    }

    public async Task<List<FIS.Web.Models.ContractDto>> GetRecentContractsWindowAsync()
    {
        var active = await GetContractsAsync();
        var results = new List<ContractDto>();
        if (active.Count == 0)
        {
            return results;
        }

        var maxId = active.Max(c => c.contract_id);
        var minId = Math.Max(1, maxId - RecentContractWindow);
        var upperId = maxId + 10;

        for (var id = minId; id <= upperId; id++)
        {
            var contract = await GetContractAsync(id);
            if (contract != null)
            {
                results.Add(contract);
            }
        }

        return results
            .GroupBy(c => c.contract_id)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<ContractDto?> GetLatestContractForVehicleAsync(int vmfCode)
    {
        if (vmfCode <= 0)
        {
            return null;
        }

        var active = await GetContractsAsync();
        var directActive = active
            .Where(c => c.vmf_code == vmfCode)
            .OrderByDescending(c => c.contract_id)
            .FirstOrDefault();
        if (directActive != null)
        {
            return directActive;
        }

        var recent = await GetRecentContractsWindowAsync();
        return recent
            .Where(c => c.vmf_code == vmfCode)
            .OrderByDescending(c => c.contract_id)
            .FirstOrDefault();
    }

    public async Task<FIS.Web.Models.ContractDto?> GetContractAsync(int contractId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/Contracts/{contractId}");
            response.EnsureSuccessStatusCode();

            var apiContract = await response.Content.ReadFromJsonAsync<ApiContractResponse>();
            return apiContract == null ? null : MapToDto(apiContract);
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

    public async Task<FIS.Web.Models.ContractDto> CreateContractAsync(ContractCreateDto contract)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Contracts", contract);
            response.EnsureSuccessStatusCode();
            
            var createdContract = await response.Content.ReadFromJsonAsync<FIS.Web.Models.ContractDto>();
            return createdContract ?? throw new InvalidOperationException("Failed to create contract");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<FIS.Web.Models.ContractDto> UpdateContractAsync(int contractId, ContractUpdateDto contract)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/Contracts/{contractId}", contract);
            response.EnsureSuccessStatusCode();
            
            var updatedContract = await response.Content.ReadFromJsonAsync<FIS.Web.Models.ContractDto>();
            return updatedContract ?? throw new InvalidOperationException("Failed to update contract");
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task DeleteContractAsync(int contractId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/Contracts/{contractId}");
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Unable to connect to the API service");
        }
    }

    public async Task<bool> HireContractAsync(HireContractRequestDto request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/Contracts/hire", request);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<List<ContractSummaryDto>> GetActiveContractsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/Contracts/active");
            response.EnsureSuccessStatusCode();

            var contracts = await response.Content.ReadFromJsonAsync<List<ContractSummaryDto>>();
            return contracts ?? new List<ContractSummaryDto>();
        }
        catch (HttpRequestException)
        {
            return new List<ContractSummaryDto>();
        }
        catch (Exception)
        {
            return new List<ContractSummaryDto>();
        }
    }

    public async Task<ContractPrintoutDto?> GetContractPrintoutAsync(int contractId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ContractPrintoutDto>($"api/contracts/{contractId}/printout");
        }
        catch
        {
            return null;
        }
    }

    public Task<FinanceApiResult> GetActionAsync(string endpoint)
        => SendAsync(HttpMethod.Get, endpoint, null);

    public Task<FinanceApiResult> PostActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Post, endpoint, payload);

    public Task<FinanceApiResult> PutActionAsync(string endpoint, object payload)
        => SendAsync(HttpMethod.Put, endpoint, payload);

    private async Task<FinanceApiResult> SendAsync(HttpMethod method, string endpoint, object? payload)
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint);
            if (payload != null && method != HttpMethod.Get)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            return new FinanceApiResult
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Endpoint = endpoint,
                Message = response.IsSuccessStatusCode
                    ? "Request completed successfully."
                    : SanitizeUserMessage(ExtractApiErrorMessage(body)) ?? $"Request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                ResponseBody = body
            };
        }
        catch (Exception ex)
        {
            return new FinanceApiResult
            {
                Success = false,
                Endpoint = endpoint,
                Message = SanitizeUserMessage(ex.Message) ?? "Request failed."
            };
        }
    }

    private static string? ExtractApiErrorMessage(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorValue))
            {
                var errorMessage = errorValue.GetString();
                if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    return errorMessage.Trim();
                }
            }

            if (root.TryGetProperty("message", out var messageValue))
            {
                var message = messageValue.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message.Trim();
                }
            }
        }
        catch
        {
            // Ignore parse failures and keep the fallback status message.
        }

        return null;
    }

    private static string? SanitizeUserMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        var endpointIndex = trimmed.IndexOf("Endpoint:", StringComparison.OrdinalIgnoreCase);
        if (endpointIndex >= 0)
        {
            trimmed = trimmed[..endpointIndex].Trim();
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static ContractDto MapToDto(ApiContractResponse api)
    {
        var stillCurrent = (api.StillCurrent ?? string.Empty).Trim().ToUpperInvariant();
        var statusLabel = api.ContractStatusCode switch
        {
            1 => "Pending Approval",
            2 => "Approved",
            3 => stillCurrent == "Y" ? "Active" : "Closed",
            4 => "Declined For Correction",
            5 => "Declined",
            _ => stillCurrent == "Y" ? "Active" : stillCurrent == "N" ? "Closed" : "Unknown"
        };

        return new ContractDto
        {
            contract_id = api.ContractCode,
            vmf_code = api.VmfCode,
            site_code = api.SiteCode,
            contract_status_code = api.ContractStatusCode,
            still_current = stillCurrent,
            user_code = api.UserCode,
            approver_code = api.ApproverCode,
            created_by_user_code = api.CreatedByUserCode,
            modified_by_user_code = api.ModifiedByUserCode,
            contract_number = api.ContractCode.ToString(),
            vehicle_registration = api.VmfCode.ToString(),
            department_code = 0,
            contractor_name = api.ContractTypeCode ?? "",
            start_date = api.StartDate,
            end_date = api.EndDate,
            start_odometer = api.StartOdometer,
            driver_id = api.DriverId,
            target_return_date = api.TargetReturnDate,
            status = statusLabel,
            contract_notes = api.Notes
        };
    }

    private static ContractDto MapToDto(PagedContractResponse api)
    {
        var statusCode = api.ContractStatusCode;
        var stillCurrent = (api.StillCurrent ?? string.Empty).Trim().ToUpperInvariant();

        return new ContractDto
        {
            contract_id = api.ContractCode,
            vmf_code = api.VmfCode,
            site_code = api.SiteCode,
            contract_status_code = statusCode,
            still_current = stillCurrent,
            user_code = api.UserCode,
            approver_code = api.ApproverCode,
            created_by_user_code = api.CreatedByUserCode,
            modified_by_user_code = api.ModifiedByUserCode,
            contract_number = api.ContractCode.ToString(),
            vehicle_registration = api.RegistrationNumber ?? string.Empty,
            vehicle_make = api.Make ?? string.Empty,
            vehicle_model = api.Model ?? string.Empty,
            department_name = api.DepartmentName ?? string.Empty,
            contractor_name = api.ContractTypeCode ?? string.Empty,
            start_date = api.StartDate,
            end_date = api.EndDate,
            status = api.ContractStatus ?? StatusLabel(statusCode, stillCurrent),
            contract_notes = api.Notes,
            target_return_date = api.TargetReturnDate,
            fleet_number = api.FleetNumber,
            site_name = api.SiteName,
            driver_name = api.DriverName
        };
    }

    private static string StatusLabel(short? statusCode, string? stillCurrent)
        => statusCode switch
        {
            0 => "Draft",
            1 => "Pending Review",
            2 => "Approved",
            3 => "Active",
            4 => "Declined for Correction",
            5 => "Declined",
            6 => "Cancelled",
            7 => "Closed",
            _ => string.Equals(stillCurrent, "Y", StringComparison.OrdinalIgnoreCase) ? "Active" : "Unknown"
        };
}

public class ContractCreateDto
{
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public int department_code { get; set; }
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }
}

public class ContractUpdateDto
{
    public string contract_number { get; set; } = "";
    public string vehicle_registration { get; set; } = "";
    public int department_code { get; set; }
    public string contractor_name { get; set; } = "";
    public DateTime start_date { get; set; }
    public DateTime? end_date { get; set; }
    public decimal? monthly_cost { get; set; }
    public string status { get; set; } = "Active";
    public string? contract_notes { get; set; }
}

public class HireContractRequestDto
{
    public int VmfCode { get; set; }
    public short SiteCode { get; set; }
    public int? StartOdometer { get; set; }
    public string? DriverId { get; set; }
    public string? Notes { get; set; }
    public DateTime? TargetReturnDate { get; set; }
}

public class ContractSummaryDto
{
    public int contract_code { get; set; }
    public int vmf_code { get; set; }
    public short site_code { get; set; }
    public string? still_current { get; set; }
}

public class ContractsPageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public short? StatusCode { get; set; }
    public short? SiteCode { get; set; }
    public string? StillCurrent { get; set; }
    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public int? VmfCode { get; set; }
}

public class ContractPageResult
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages { get; set; }
    public List<ContractDto> Data { get; set; } = new();

    public static ContractPageResult Empty(int page, int pageSize)
        => new()
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = 0,
            TotalPages = 0,
            Data = new List<ContractDto>()
        };
}

internal class PagedContractsResponse
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("page_size")]
    public int PageSizeLegacy
    {
        get => PageSize;
        set => PageSize = value;
    }

    [JsonPropertyName("totalRecords")]
    public int TotalRecords { get; set; }

    [JsonPropertyName("total_records")]
    public int TotalRecordsLegacy
    {
        get => TotalRecords;
        set => TotalRecords = value;
    }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPagesLegacy
    {
        get => TotalPages;
        set => TotalPages = value;
    }

    [JsonPropertyName("data")]
    public List<PagedContractResponse>? Data { get; set; }
}

internal class PagedContractResponse
{
    [JsonPropertyName("contractCode")]
    public int ContractCode { get; set; }

    [JsonPropertyName("vmfCode")]
    public int? VmfCode { get; set; }

    [JsonPropertyName("fleetNumber")]
    public string? FleetNumber { get; set; }

    [JsonPropertyName("registrationNumber")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("siteCode")]
    public short? SiteCode { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }

    [JsonPropertyName("departmentName")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("driverName")]
    public string? DriverName { get; set; }

    [JsonPropertyName("contractTypeCode")]
    public string? ContractTypeCode { get; set; }

    [JsonPropertyName("contractStatusCode")]
    public short? ContractStatusCode { get; set; }

    [JsonPropertyName("contractStatus")]
    public string? ContractStatus { get; set; }

    [JsonPropertyName("stillCurrent")]
    public string? StillCurrent { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("targetReturnDate")]
    public DateTime? TargetReturnDate { get; set; }

    [JsonPropertyName("userCode")]
    public short? UserCode { get; set; }

    [JsonPropertyName("approverCode")]
    public int? ApproverCode { get; set; }

    [JsonPropertyName("createdByUserCode")]
    public int? CreatedByUserCode { get; set; }

    [JsonPropertyName("modifiedByUserCode")]
    public int? ModifiedByUserCode { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("make")]
    public string? Make { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

public class ContractPrintoutDto
{
    [JsonPropertyName("printed_at")]
    public DateTime? PrintedAt { get; set; }

    [JsonPropertyName("document_title")]
    public string? DocumentTitle { get; set; }

    [JsonPropertyName("contract")]
    public ContractPrintoutContractDto? Contract { get; set; }

    [JsonPropertyName("vehicle")]
    public ContractPrintoutVehicleDto? Vehicle { get; set; }

    [JsonPropertyName("site")]
    public ContractPrintoutSiteDto? Site { get; set; }

    [JsonPropertyName("parties")]
    public ContractPrintoutPartiesDto? Parties { get; set; }

    [JsonPropertyName("audit_trail")]
    public List<ContractPrintoutAuditDto>? AuditTrail { get; set; }
}

public class ContractPrintoutContractDto
{
    [JsonPropertyName("contract_code")]
    public int ContractCode { get; set; }

    [JsonPropertyName("status_code")]
    public short? StatusCode { get; set; }

    [JsonPropertyName("status_text")]
    public string? StatusText { get; set; }

    [JsonPropertyName("still_current")]
    public string? StillCurrent { get; set; }

    [JsonPropertyName("start_date")]
    public string? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public string? EndDate { get; set; }

    [JsonPropertyName("target_return_date")]
    public string? TargetReturnDate { get; set; }

    [JsonPropertyName("start_odometer")]
    public int? StartOdometer { get; set; }

    [JsonPropertyName("end_odometer")]
    public int? EndOdometer { get; set; }

    [JsonPropertyName("contract_type")]
    public string? ContractType { get; set; }

    [JsonPropertyName("driver_id")]
    public string? DriverId { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public class ContractPrintoutVehicleDto
{
    [JsonPropertyName("vmf_code")]
    public int? VmfCode { get; set; }

    [JsonPropertyName("fleet_number")]
    public string? FleetNumber { get; set; }

    [JsonPropertyName("registration_number")]
    public string? RegistrationNumber { get; set; }

    [JsonPropertyName("year_manufactured")]
    public int? YearManufactured { get; set; }

    [JsonPropertyName("model_code")]
    public short? ModelCode { get; set; }

    [JsonPropertyName("current_odo")]
    public int? CurrentOdo { get; set; }
}

public class ContractPrintoutSiteDto
{
    [JsonPropertyName("site_code")]
    public short? SiteCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("res_person")]
    public string? ResponsiblePerson { get; set; }

    [JsonPropertyName("net_address")]
    public string? NetAddress { get; set; }

    [JsonPropertyName("telephone")]
    public string? Telephone { get; set; }
}

public class ContractPrintoutPartiesDto
{
    [JsonPropertyName("capturer")]
    public ContractPrintoutUserDto? Capturer { get; set; }

    [JsonPropertyName("approver")]
    public ContractPrintoutUserDto? Approver { get; set; }
}

public class ContractPrintoutUserDto
{
    [JsonPropertyName("user_code")]
    public int? UserCode { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class ContractPrintoutAuditDto
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("performed_by_user_code")]
    public int? PerformedByUserCode { get; set; }

    [JsonPropertyName("performed_at")]
    public string? PerformedAt { get; set; }

    [JsonPropertyName("old_status")]
    public string? OldStatus { get; set; }

    [JsonPropertyName("new_status")]
    public string? NewStatus { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

using System.Net.Http.Json;

namespace FIS.Web.Services;

public class AuditApiService
{
    private readonly HttpClient _httpClient;
    private readonly TokenService _tokenService;

    public AuditApiService(HttpClient httpClient, TokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    private void AddAuthHeader()
    {
        if (string.IsNullOrWhiteSpace(_tokenService.Token)) return;
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"FIS_Access_Token={_tokenService.Token}");
    }

    public async Task<AuditPagedResult> GetAuditTrailAsync(
        string?   tableName  = null,
        string?   action     = null,
        DateTime? fromDate   = null,
        DateTime? toDate     = null,
        int?      userId     = null,
        string?   primaryKey = null,
        int       pageNumber = 1,
        int       pageSize   = 50)
    {
        try
        {
            AddAuthHeader();
            var url = BuildUrl("api/audit", new()
            {
                ["tableName"]  = tableName,
                ["action"]     = action,
                ["fromDate"]   = fromDate?.ToString("yyyy-MM-dd"),
                ["toDate"]     = toDate?.ToString("yyyy-MM-dd"),
                ["userId"]     = userId?.ToString(),
                ["primaryKey"] = primaryKey,
                ["pageNumber"] = pageNumber.ToString(),
                ["pageSize"]   = pageSize.ToString()
            });

            return await _httpClient.GetFromJsonAsync<AuditPagedResult>(url)
                ?? new AuditPagedResult();
        }
        catch
        {
            return new AuditPagedResult();
        }
    }

    public async Task<AuditUserStatusResult> GetUserStatusHistoryAsync(
        int?      userAccessCode = null,
        DateTime? fromDate       = null,
        DateTime? toDate         = null,
        int       pageNumber     = 1,
        int       pageSize       = 50)
    {
        try
        {
            AddAuthHeader();
            var url = BuildUrl("api/audit/user-status-history", new()
            {
                ["userAccessCode"] = userAccessCode?.ToString(),
                ["fromDate"]       = fromDate?.ToString("yyyy-MM-dd"),
                ["toDate"]         = toDate?.ToString("yyyy-MM-dd"),
                ["pageNumber"]     = pageNumber.ToString(),
                ["pageSize"]       = pageSize.ToString()
            });

            return await _httpClient.GetFromJsonAsync<AuditUserStatusResult>(url)
                ?? new AuditUserStatusResult();
        }
        catch
        {
            return new AuditUserStatusResult();
        }
    }

    public async Task<AuditPasswordResult> GetPasswordHistoryAsync(
        int? userAccessCode = null,
        int  pageNumber     = 1,
        int  pageSize       = 50)
    {
        try
        {
            AddAuthHeader();
            var url = BuildUrl("api/audit/password-history", new()
            {
                ["userAccessCode"] = userAccessCode?.ToString(),
                ["pageNumber"]     = pageNumber.ToString(),
                ["pageSize"]       = pageSize.ToString()
            });

            return await _httpClient.GetFromJsonAsync<AuditPasswordResult>(url)
                ?? new AuditPasswordResult();
        }
        catch
        {
            return new AuditPasswordResult();
        }
    }

    private static string BuildUrl(string base_, Dictionary<string, string?> filters)
    {
        var parts = filters
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}");
        var qs = string.Join("&", parts);
        return string.IsNullOrEmpty(qs) ? base_ : $"{base_}?{qs}";
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class AuditPagedResult
{
    public int TotalCount  { get; set; }
    public int PageNumber  { get; set; }
    public int PageSize    { get; set; }
    public int TotalPages  { get; set; }
    public List<AuditItemDto> Items { get; set; } = new();
}

public class AuditItemDto
{
    public int     AuditID              { get; set; }
    public string? Action               { get; set; }
    public string? TableName            { get; set; }
    public string? PrimaryKey           { get; set; }
    public string? Changes              { get; set; }
    public string? ActionedBy           { get; set; }
    public int?    created_by_user_code { get; set; }
    public DateTime ChangedAt           { get; set; }
}

public class AuditUserStatusResult
{
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<UserStatusItemDto> Items { get; set; } = new();
}

public class UserStatusItemDto
{
    public int     status_history_id    { get; set; }
    public int     user_access_code     { get; set; }
    public string? new_status           { get; set; }
    public string? previous_status      { get; set; }
    public int?    changed_by_user_code { get; set; }
    public DateTime changed_at          { get; set; }
    public string? reason               { get; set; }
}

public class AuditPasswordResult
{
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<PasswordHistoryItemDto> Items { get; set; } = new();
}

public class PasswordHistoryItemDto
{
    public int       user_access_code     { get; set; }
    public DateTime  last_password_change { get; set; }
    public DateTime? password_expiry_date { get; set; }
    public int?      changed_by_user_code { get; set; }
    public int       failed_login_attempts { get; set; }
    public DateTime? account_locked_until { get; set; }
    public bool      IsExpired            { get; set; }
}

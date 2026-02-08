namespace FIS.Web.Services;

public class ValidationApiService(HttpClient httpClient, TokenService tokenService, ILogger<ValidationApiService> logger)
    : BaseApiService(httpClient, tokenService, logger)
{
    private const string BasePath = "api/validation";

    public Task<ValidationMenuResponse?> GetMenuAsync()
        => GetAsync<ValidationMenuResponse>($"{BasePath}/menu");

    public Task<ValidationHelpResponse?> GetHelpAsync()
        => GetAsync<ValidationHelpResponse>($"{BasePath}/help");
}

public class ValidationMenuResponse
{
    public List<string> options { get; set; } = new();
}

public class ValidationHelpResponse
{
    public string? title { get; set; }
    public string? description { get; set; }
    public List<ValidationHelpSection> sections { get; set; } = new();
}

public class ValidationHelpSection
{
    public string? title { get; set; }
    public string? content { get; set; }
}

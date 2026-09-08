using System.Net.Http;
using System.Text;
using System.Text.Json;
using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow.Handlers;

public class WebhookHandler : StepHandlerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public override string HandlerType => "webhook";

    public WebhookHandler(IHttpClientFactory httpClientFactory, ILogger<WebhookHandler> logger)
        : base(logger)
    {
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<StepExecutionResult> ExecuteInternalAsync(
        Dictionary<string, object> parameters,
        WorkflowExecutionContext context
    )
    {
        var url = GetRequiredParameter<string>(parameters, "url");
        var method = GetOptionalParameter<string>(parameters, "method", "POST") ?? "POST";

        var httpClient = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        try
        {
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return StepExecutionResult.SuccessResult(
                    $"Webhook executed: {response.StatusCode}"
                );
            }
            return StepExecutionResult.FailureResult($"Webhook failed: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return StepExecutionResult.FailureResult($"Webhook error: {ex.Message}", ex);
        }
    }

    public override Task<Interfaces.Workflow.ValidationResult> ValidateParametersAsync(
        Dictionary<string, object> parameters
    )
    {
        var result = new Interfaces.Workflow.ValidationResult { IsValid = true };

        if (
            !parameters.ContainsKey("url")
            || string.IsNullOrWhiteSpace(parameters["url"]?.ToString())
        )
            result.AddError("Parameter 'url' is required");
        else if (!Uri.TryCreate(parameters["url"].ToString(), UriKind.Absolute, out _))
            result.AddError("Parameter 'url' must be a valid URL");

        return Task.FromResult(result);
    }
}

using FIS.Core.Application.Interfaces.Workflow;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Application.Services.Workflow;

/// <summary>
/// Factory for resolving and managing step handlers
/// </summary>
public class StepHandlerFactory : IStepHandlerFactory
{
    private readonly Dictionary<string, IStepHandler> _handlers = new();
    private readonly ILogger<StepHandlerFactory> _logger;

    public StepHandlerFactory(
        IEnumerable<IStepHandler> handlers,
        ILogger<StepHandlerFactory> logger
    )
    {
        _logger = logger;

        // Register all injected handlers
        foreach (var handler in handlers)
        {
            RegisterHandler(handler);
        }

        _logger.LogInformation("Registered {Count} step handlers", _handlers.Count);
    }

    /// <summary>
    /// Gets a handler by its type identifier
    /// </summary>
    public IStepHandler? GetHandler(string handlerType)
    {
        if (string.IsNullOrWhiteSpace(handlerType))
        {
            _logger.LogWarning("Attempted to get handler with null or empty handler type");
            return null;
        }

        if (_handlers.TryGetValue(handlerType.ToLowerInvariant(), out var handler))
        {
            return handler;
        }

        _logger.LogWarning("Handler type '{HandlerType}' not found", handlerType);
        return null;
    }

    /// <summary>
    /// Gets all registered handler types
    /// </summary>
    public IEnumerable<string> GetAvailableHandlerTypes()
    {
        return _handlers.Keys.ToList();
    }

    /// <summary>
    /// Registers a handler instance
    /// </summary>
    public void RegisterHandler(IStepHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var handlerType = handler.HandlerType.ToLowerInvariant();

        if (_handlers.ContainsKey(handlerType))
        {
            _logger.LogWarning(
                "Handler type '{HandlerType}' is already registered. Overwriting.",
                handler.HandlerType
            );
        }

        _handlers[handlerType] = handler;
        _logger.LogDebug("Registered handler: {HandlerType}", handler.HandlerType);
    }
}

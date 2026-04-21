namespace FIS.Core.Application.Interfaces.Workflow;

/// <summary>
/// Factory for resolving step handlers by type
/// </summary>
public interface IStepHandlerFactory
{
    /// <summary>
    /// Gets a handler by its type identifier
    /// </summary>
    /// <param name="handlerType">Type identifier of the handler</param>
    /// <returns>Handler instance or null if not found</returns>
    IStepHandler? GetHandler(string handlerType);

    /// <summary>
    /// Gets all registered handler types
    /// </summary>
    /// <returns>List of handler type identifiers</returns>
    IEnumerable<string> GetAvailableHandlerTypes();

    /// <summary>
    /// Registers a handler instance
    /// </summary>
    /// <param name="handler">Handler to register</param>
    void RegisterHandler(IStepHandler handler);
}

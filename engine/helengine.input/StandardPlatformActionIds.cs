namespace helengine;

/// <summary>
/// Provides the reserved input identifiers used by the engine-owned standard platform action layer.
/// </summary>
public static class StandardPlatformActionIds {
    /// <summary>
    /// Gets the reserved input context that owns every standard platform action binding.
    /// </summary>
    public static InputContextId ContextId { get; } = new InputContextId(4100);

    /// <summary>
    /// Gets the reserved logical action identifier used for the standard accept action.
    /// </summary>
    public static InputActionId AcceptActionId { get; } = new InputActionId(4101);

    /// <summary>
    /// Gets the reserved logical action identifier used for the standard return action.
    /// </summary>
    public static InputActionId ReturnActionId { get; } = new InputActionId(4102);

    /// <summary>
    /// Resolves the reserved logical action identifier for one standard platform action.
    /// </summary>
    /// <param name="action">Platform action whose logical action identifier should be returned.</param>
    /// <returns>Reserved logical action identifier for the supplied standard action.</returns>
    public static InputActionId GetActionId(StandardPlatformAction action) {
        if (action == StandardPlatformAction.Accept) {
            return AcceptActionId;
        }
        if (action == StandardPlatformAction.Return) {
            return ReturnActionId;
        }

        throw new InvalidOperationException($"Unsupported standard platform action '{action}'.");
    }
}

namespace helengine;

/// <summary>
/// Describes one binding between a logical action and a physical control.
/// </summary>
public readonly struct InputBinding {
    /// <summary>
    /// Creates a new binding for the supplied context, action, and control.
    /// </summary>
    /// <param name="contextId">Context that owns the binding.</param>
    /// <param name="actionId">Logical action driven by the binding.</param>
    /// <param name="control">Physical control that produces the action.</param>
    /// <param name="scale">Multiplier applied to the resolved control value.</param>
    public InputBinding(InputContextId contextId, InputActionId actionId, InputControlId control, float scale) {
        ContextId = contextId;
        ActionId = actionId;
        Control = control;
        Scale = scale;
    }

    /// <summary>
    /// Gets the context that owns the binding.
    /// </summary>
    public InputContextId ContextId { get; }

    /// <summary>
    /// Gets the logical action driven by the binding.
    /// </summary>
    public InputActionId ActionId { get; }

    /// <summary>
    /// Gets the physical control that produces the action.
    /// </summary>
    public InputControlId Control { get; }

    /// <summary>
    /// Gets the multiplier applied to the resolved control value.
    /// </summary>
    public float Scale { get; }
}

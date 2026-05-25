namespace helengine;

/// <summary>
/// Provides runtime queries for engine-owned standard platform actions such as accept and return.
/// </summary>
public sealed class StandardPlatformInput {
    /// <summary>
    /// Stores the input system that owns the logical action state.
    /// </summary>
    readonly InputSystem InputSystem;

    /// <summary>
    /// Initializes one standard platform input helper for the supplied input system.
    /// </summary>
    /// <param name="inputSystem">Input system that will own the reserved standard platform action bindings.</param>
    public StandardPlatformInput(InputSystem inputSystem) {
        InputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
    }

    /// <summary>
    /// Registers one runtime configuration into the reserved standard platform action context.
    /// </summary>
    /// <param name="configuration">Standard platform action configuration that should be active for the runtime.</param>
    public void Configure(StandardPlatformInputConfiguration configuration) {
        if (configuration == null) {
            throw new ArgumentNullException(nameof(configuration));
        }

        InputSystem.ClearBindings(StandardPlatformActionIds.ContextId);
        InputSystem.RemoveContextInstances(StandardPlatformActionIds.ContextId);
        for (int index = 0; index < configuration.Bindings.Count; index++) {
            StandardPlatformActionBinding binding = configuration.Bindings[index];
            InputSystem.RegisterBinding(new InputBinding(
                StandardPlatformActionIds.ContextId,
                StandardPlatformActionIds.GetActionId(binding.Action),
                binding.Control,
                1f));
        }

        if (configuration.Bindings.Count > 0) {
            InputSystem.PushContext(StandardPlatformActionIds.ContextId);
        }
    }

    /// <summary>
    /// Returns whether the supplied standard platform action is currently active.
    /// </summary>
    /// <param name="action">Standard platform action to query.</param>
    /// <returns>True when the standard platform action is active.</returns>
    public bool IsActionDown(StandardPlatformAction action) {
        return InputSystem.IsActionDown(StandardPlatformActionIds.GetActionId(action));
    }

    /// <summary>
    /// Returns whether the supplied standard platform action transitioned to active on the current frame.
    /// </summary>
    /// <param name="action">Standard platform action to query.</param>
    /// <returns>True when the standard platform action was pressed on the current frame.</returns>
    public bool WasActionPressed(StandardPlatformAction action) {
        return InputSystem.WasActionPressed(StandardPlatformActionIds.GetActionId(action));
    }

    /// <summary>
    /// Returns whether the supplied standard platform action transitioned to inactive on the current frame.
    /// </summary>
    /// <param name="action">Standard platform action to query.</param>
    /// <returns>True when the standard platform action was released on the current frame.</returns>
    public bool WasActionReleased(StandardPlatformAction action) {
        return InputSystem.WasActionReleased(StandardPlatformActionIds.GetActionId(action));
    }
}

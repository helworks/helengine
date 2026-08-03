namespace helengine;

/// <summary>
/// Stores the complete set of standard platform action bindings that should be registered for one runtime.
/// </summary>
public sealed class StandardPlatformInputConfiguration : IDisposable {
    /// <summary>
    /// Stores the copied binding list owned by this configuration.
    /// </summary>
    [NativeOwnedMember]
    List<StandardPlatformActionBinding> BindingsValue;

    /// <summary>
    /// Gets one empty configuration with no configured standard platform actions.
    /// </summary>
    public static StandardPlatformInputConfiguration Empty { get; } = new StandardPlatformInputConfiguration([]);

    /// <summary>
    /// Initializes one standard platform input configuration from the supplied bindings.
    /// </summary>
    /// <param name="bindings">Configured standard platform action bindings that should be registered at runtime.</param>
    public StandardPlatformInputConfiguration(List<StandardPlatformActionBinding> bindings) {
        if (bindings == null) {
            throw new ArgumentNullException(nameof(bindings));
        }

        List<StandardPlatformActionBinding> copiedBindings = new List<StandardPlatformActionBinding>(bindings.Count);
        for (int index = 0; index < bindings.Count; index++) {
            StandardPlatformActionBinding binding = bindings[index];
            copiedBindings.Add(binding ?? throw new InvalidOperationException("Standard platform action bindings cannot contain null entries."));
        }

        BindingsValue = copiedBindings;
    }

    /// <summary>
    /// Gets the configured standard platform action bindings that should be registered at runtime.
    /// </summary>
    public List<StandardPlatformActionBinding> Bindings => BindingsValue;

    /// <summary>
    /// Releases the native binding-list container owned by this configuration.
    /// </summary>
    public void Dispose() {
        NativeOwnership.Release(ref BindingsValue);
    }
}

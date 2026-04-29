namespace helengine.platforms;

/// <summary>
/// Stores the engine-platform bindings read from one launcher-managed installed-binding manifest.
/// </summary>
public sealed class InstalledBindingManifest {
    /// <summary>
    /// Initializes one installed-binding manifest.
    /// </summary>
    /// <param name="bindings">Bindings contained in the installed-binding manifest.</param>
    public InstalledBindingManifest(IReadOnlyList<InstalledEnginePlatformBinding> bindings) {
        Bindings = bindings;
    }

    /// <summary>
    /// Gets the bindings contained in the installed-binding manifest.
    /// </summary>
    public IReadOnlyList<InstalledEnginePlatformBinding> Bindings { get; }
}

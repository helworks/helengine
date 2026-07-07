namespace helengine.editor.tests;

/// <summary>
/// Provides one deterministic registration entrypoint for generated runtime module discovery tests.
/// </summary>
[RuntimeFeatureRequirement("host_file_system", "Generated runtime module registration test path requires host file system support for feature-discovery coverage.")]
public static class GeneratedRuntimeModuleRegistrationTestRegistration {
    /// <summary>
    /// Records the generated runtime module registration call shape required by the editor tests.
    /// </summary>
    /// <param name="core">Initialized core instance supplied by the generated bootstrap.</param>
    public static void Register(Core core) {
        if (core == null) {
            throw new ArgumentNullException(nameof(core));
        }
    }
}

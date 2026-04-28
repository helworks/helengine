namespace helengine.editor.launcher.Models;

/// <summary>
/// Identifies the reusable platform artifact families managed independently from engine installs.
/// </summary>
public enum PlatformArtifactKind {
    /// <summary>
    /// One platform software-development kit dependency.
    /// </summary>
    Sdk,

    /// <summary>
    /// One platform-specific builder toolchain used by the engine.
    /// </summary>
    PlatformBuilder,

    /// <summary>
    /// One shared set of platform support files used by matching engine versions.
    /// </summary>
    PlatformFiles
}

using Xunit;

namespace helengine.editor.tests.testing;

/// <summary>Runs native Darwin interop checks only where the macOS SDK-built dependency can be loaded.</summary>
public sealed class MacOsFactAttribute : FactAttribute {
    /// <summary>Marks non-macOS discovery as skipped rather than counting a native test that never executed.</summary>
    public MacOsFactAttribute() {
        if (!OperatingSystem.IsMacOS()) {
            Skip = "Requires the actual macOS authoring native library.";
        }
    }
}

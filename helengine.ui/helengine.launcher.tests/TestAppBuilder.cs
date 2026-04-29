using Avalonia;
using Avalonia.Headless;
using helengine.editor.launcher;

[assembly: AvaloniaTestApplication(typeof(helengine.editor.launcher.tests.TestAppBuilder))]

namespace helengine.editor.launcher.tests;

/// <summary>
/// Builds the shared headless Avalonia application configuration used by launcher UI tests.
/// </summary>
public static class TestAppBuilder {
    /// <summary>
    /// Creates one application builder configured for headless launcher control tests.
    /// </summary>
    /// <returns>Application builder that can initialize the launcher test environment.</returns>
    public static AppBuilder BuildAvaloniaApp() {
        return AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}

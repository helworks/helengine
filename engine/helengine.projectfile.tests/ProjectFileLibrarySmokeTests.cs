using helengine.projectfile;
using Xunit;

namespace helengine.projectfile.tests;

/// <summary>
/// Verifies that the shared project-file library exposes the canonical entry points used by the launcher and editor.
/// </summary>
public sealed class ProjectFileLibrarySmokeTests {
    /// <summary>
    /// Ensures the shared project-file library exposes its document model and reader entry point.
    /// </summary>
    [Fact]
    public void SharedProjectFileLibrary_ExposesCanonicalEntryPoints() {
        ProjectFileReader reader = new ProjectFileReader();
        ProjectFileDocument document = new ProjectFileDocument();

        Assert.NotNull(reader);
        Assert.NotNull(document);
    }
}

using helengine.projectfile;
using Xunit;

namespace helengine.projectfile.tests;

/// <summary>
/// Verifies the shared project-file document exposes the canonical metadata required by launcher and editor flows.
/// </summary>
public sealed class ProjectFileDocumentTests {
    /// <summary>
    /// Ensures newly created project documents default to the currently supported project format version.
    /// </summary>
    [Fact]
    public void ProjectFileDocument_DefaultsToSupportedFormatVersion() {
        ProjectFileDocument document = new ProjectFileDocument();

        Assert.Equal(1, document.ProjectFormatVersion);
    }

    /// <summary>
    /// Ensures arbitrary platform identifiers are preserved exactly as provided by the project file.
    /// </summary>
    [Fact]
    public void ProjectFileDocument_PreservesArbitrarySupportedPlatforms() {
        ProjectFileDocument document = new ProjectFileDocument {
            SupportedPlatforms = new List<string> { "windows", "future-console" }
        };

        Assert.Equal("future-console", document.SupportedPlatforms[1]);
    }

    /// <summary>
    /// Ensures the required engine version is exposed as a first-class canonical project field.
    /// </summary>
    [Fact]
    public void ProjectFileDocument_StoresRequiredEngineVersion() {
        ProjectFileDocument document = new ProjectFileDocument {
            RequiredEngineVersion = "1.2.3"
        };

        Assert.Equal("1.2.3", document.RequiredEngineVersion);
    }
}

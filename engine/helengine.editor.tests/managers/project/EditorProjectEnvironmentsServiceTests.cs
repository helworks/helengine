using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the project-shared environment registry stored in `settings/environments.json`.
/// </summary>
public sealed class EditorProjectEnvironmentsServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Creates one isolated temporary project root for the current test instance.
    /// </summary>
    public EditorProjectEnvironmentsServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-project-environments-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
    }

    /// <summary>
    /// Deletes the temporary project root created for the current test instance.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempProjectRootPath)) {
            Directory.Delete(TempProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures a missing registry is seeded with the two protected built-in environments.
    /// </summary>
    [Fact]
    public void Load_WhenFileIsMissing_SeedsProtectedDebugAndReleaseEnvironments() {
        EditorProjectEnvironmentsService service = CreateService();

        EditorProjectEnvironmentsDocument document = service.Load();

        Assert.Equal(new[] { "debug", "release" }, document.Environments.Select(environment => environment.Id));
        Assert.All(document.Environments, environment => Assert.True(environment.IsProtected));
        Assert.True(File.Exists(Path.Combine(TempProjectRootPath, "settings", "environments.json")));
    }

    /// <summary>
    /// Ensures existing custom entries survive normalization while built-ins remain canonical and unique.
    /// </summary>
    [Fact]
    public void Load_WhenFileContainsDuplicates_NormalizesBuiltInsAndPreservesCustomEntries() {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(
            Path.Combine(settingsDirectoryPath, "environments.json"),
            """
            {
              "environments": [
                { "id": "release", "isProtected": false },
                { "id": "QA Preview", "isProtected": false },
                { "id": "qa preview", "isProtected": false },
                { "id": "debug", "isProtected": false }
              ]
            }
            """);
        EditorProjectEnvironmentsService service = CreateService();

        EditorProjectEnvironmentsDocument document = service.Load();

        Assert.Equal(new[] { "debug", "release", "QA Preview" }, document.Environments.Select(environment => environment.Id));
        Assert.True(document.Environments[0].IsProtected);
        Assert.True(document.Environments[1].IsProtected);
        Assert.False(document.Environments[2].IsProtected);
    }

    /// <summary>
    /// Ensures custom environments can be added, renamed, and deleted through the service.
    /// </summary>
    [Fact]
    public void Mutate_CustomEnvironment_AddsRenamesAndDeletesEntry() {
        EditorProjectEnvironmentsService service = CreateService();
        EditorProjectEnvironmentsDocument document = service.Load();

        service.Add(document, " QA Preview ");
        service.Rename(document, "QA Preview", "shipping-candidate");
        service.Delete(document, "shipping-candidate");

        Assert.Equal(new[] { "debug", "release" }, document.Environments.Select(environment => environment.Id));
    }

    /// <summary>
    /// Ensures protected built-ins cannot be renamed or deleted and duplicate custom ids are rejected.
    /// </summary>
    [Fact]
    public void Mutate_ProtectedOrDuplicateEnvironment_Throws() {
        EditorProjectEnvironmentsService service = CreateService();
        EditorProjectEnvironmentsDocument document = service.Load();
        service.Add(document, "QA");

        Assert.Throws<InvalidOperationException>(() => service.Rename(document, "debug", "development"));
        Assert.Throws<InvalidOperationException>(() => service.Delete(document, "release"));
        Assert.Throws<InvalidOperationException>(() => service.Add(document, "qa"));
    }

    /// <summary>
    /// Creates one environment service for the current temporary project root.
    /// </summary>
    /// <returns>Environment service configured for the current test project.</returns>
    EditorProjectEnvironmentsService CreateService() {
        return new EditorProjectEnvironmentsService(TempProjectRootPath);
    }
}

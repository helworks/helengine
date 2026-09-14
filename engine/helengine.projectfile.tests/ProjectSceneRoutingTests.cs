using helengine.projectfile;
using Xunit;

namespace helengine.projectfile.tests;

/// <summary>Verifies project-owned scene routing is explicit, arbitrary, and backward compatible.</summary>
public sealed class ProjectSceneRoutingTests : IDisposable {
    readonly string TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-projectfile-routing-tests", Guid.NewGuid().ToString("N"));

    public ProjectSceneRoutingTests() => Directory.CreateDirectory(TempDirectoryPath);

    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    [Fact]
    public async Task WriterAndReader_RoundTripArbitraryPlatformAndSceneNames() {
        string path = Path.Combine(TempDirectoryPath, "routing.heproj");
        ProjectFileDocument document = CreateDocument();
        document.SceneRouting = new ProjectSceneRoutingDocument {
            Platforms = new Dictionary<string, ProjectPlatformSceneRoutingDocument>(StringComparer.Ordinal) {
                ["handheld-test"] = new ProjectPlatformSceneRoutingDocument {
                    BootSceneId = "SmallMenu",
                    SceneAliases = new Dictionary<string, string>(StringComparer.Ordinal) {
                        ["MainMenu"] = "SmallMenu",
                        ["City"] = "CityLow"
                    }
                }
            }
        };

        await new ProjectFileWriter().WriteAsync(path, document);
        ProjectFileReadResult result = await new ProjectFileReader().ReadAsync(path);

        Assert.True(result.Succeeded);
        Assert.Equal("SmallMenu", result.Document.SceneRouting.Platforms["handheld-test"].BootSceneId);
        Assert.Equal("CityLow", result.Document.SceneRouting.Platforms["handheld-test"].SceneAliases["City"]);
    }

    [Fact]
    public async Task Reader_WhenRoutingIsOmitted_PreservesV1Compatibility() {
        string path = Path.Combine(TempDirectoryPath, "legacy.heproj");
        await File.WriteAllTextAsync(path, """
        {"projectFormatVersion":1,"name":"Legacy","version":"1.0.0","requiredEngineVersion":"0.4.0","supportedPlatforms":["windows"],"created":"2026-01-01T00:00:00Z","lastOpened":"2026-01-01T00:00:00Z"}
        """);

        ProjectFileReadResult result = await new ProjectFileReader().ReadAsync(path);

        Assert.True(result.Succeeded);
        Assert.Null(result.Document.SceneRouting);
    }

    [Fact]
    public async Task Reader_WhenAliasesContainCycle_ReturnsValidationError() {
        string path = Path.Combine(TempDirectoryPath, "cycle.heproj");
        await File.WriteAllTextAsync(path, """
        {"projectFormatVersion":1,"name":"Cycle","version":"1.0.0","requiredEngineVersion":"0.4.0","supportedPlatforms":["test"],"created":"2026-01-01T00:00:00Z","lastOpened":"2026-01-01T00:00:00Z","sceneRouting":{"platforms":{"test":{"bootSceneId":"A","sceneAliases":{"A":"B","B":"A"}}}}}
        """);

        ProjectFileReadResult result = await new ProjectFileReader().ReadAsync(path);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.FieldName.Contains("sceneAliases", StringComparison.Ordinal));
    }

    static ProjectFileDocument CreateDocument() => new ProjectFileDocument {
        Name = "Routing",
        Version = "1.0.0",
        RequiredEngineVersion = "0.4.0",
        SupportedPlatforms = new List<string> { "handheld-test" },
        Created = DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime(),
        LastOpened = DateTime.Parse("2026-01-01T00:00:00Z").ToUniversalTime()
    };
}
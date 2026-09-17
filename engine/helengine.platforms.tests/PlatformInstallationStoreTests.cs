using helengine.platforms;
using Xunit;

namespace helengine.platforms.tests;

/// <summary>
/// Verifies the platform registry store ignores the retired codegenToolPath field and reports it once.
/// </summary>
public sealed class PlatformInstallationStoreTests : IDisposable {
    readonly string TempDirectoryPath;

    public PlatformInstallationStoreTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-platform-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    [Fact]
    public void Load_WhenEntryStillCarriesCodegenToolPath_WarnsOncePerEntryAndLoadsIt() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "gamecube",
              "displayName": "Nintendo GameCube",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-gc",
              "codegenToolPath": "../../csharpcodegen/codegen/bin/Release/net9.0/codegen.exe"
            },
            {
              "engineVersion": "1.0.0",
              "platformId": "windows",
              "displayName": "Windows",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-windows"
            }
          ]
        }
        """);
        List<string> warnings = new();
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath, warnings.Add);

        PlatformInstallationManifest manifest = store.Load();

        Assert.Equal(2, manifest.Platforms.Count);
        Assert.Equal("gamecube", manifest.Platforms[0].PlatformId);
        string warning = Assert.Single(warnings);
        Assert.Contains("gamecube", warning, StringComparison.Ordinal);
        Assert.Contains("codegenToolPath", warning, StringComparison.Ordinal);
        Assert.Contains("ignored", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_WhenNoWarningSinkIsSupplied_StillLoadsEntryWithRetiredField() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "gamecube",
              "displayName": "Nintendo GameCube",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-gc",
              "codegenToolPath": "anything"
            }
          ]
        }
        """);
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath);

        PlatformInstallationManifest manifest = store.Load();

        Assert.Single(manifest.Platforms);
    }

    [Fact]
    public void Load_WhenNoEntryCarriesCodegenToolPath_DoesNotWarn() {
        File.WriteAllText(Path.Combine(TempDirectoryPath, "platforms.json"), """
        {
          "platforms": [
            {
              "engineVersion": "1.0.0",
              "platformId": "windows",
              "displayName": "Windows",
              "builderAssemblyPath": "",
              "playerSourceRootPath": "../helengine-windows"
            }
          ]
        }
        """);
        List<string> warnings = new();
        PlatformInstallationStore store = new PlatformInstallationStore(TempDirectoryPath, warnings.Add);

        store.Load();

        Assert.Empty(warnings);
    }
}

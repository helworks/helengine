using System.Text.Json;
using helengine.editor;
using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Resolves the small, checked-in City-style source fixtures used by source-contract tests.
/// </summary>
internal static class CityFixtureRepository {
    const string FixtureRootRelativePath = "fixtures/city";

    public static string ResolveSourcePath(string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Fixture source path must be provided.", nameof(relativePath));
        }

        string path = Path.Combine(
            TestSourceRepositoryLocator.ResolveHelEngineRootPath(),
            "engine",
            "helengine.editor.tests",
            FixtureRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            relativePath.Replace('/', Path.DirectorySeparatorChar) + ".fixture");
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"City fixture source '{relativePath}' was not found.", path);
        }

        return path;
    }

    public static CityFixtureBuildProject CreateBuildProject() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-city-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootPath, "assets", "scenes", "physics"));
        Directory.CreateDirectory(Path.Combine(rootPath, "user_settings"));

        File.WriteAllText(
            Path.Combine(rootPath, "project.heproj"),
            """
            {
              "projectFormatVersion": 1,
              "name": "City Fixture",
              "version": "1.0.0",
              "requiredEngineVersion": "1.0.0+13db86b8a91031015e3d0475799b6e6b1a56b309",
              "supportedPlatforms": [ "ds" ],
              "created": "2026-08-29T00:00:00Z",
              "lastOpened": "2026-08-29T00:00:00Z"
            }
            """);

        string[] sceneIds = [
            "test_scene_dynamic_stack_boxes",
            "test_scene_dynamic_sphere_stack",
            "test_scene_dynamic_mixed_stack",
            "test_scene_static_mesh_showcase",
            "test_scene_static_mesh_minimal"
        ];
        for (int index = 0; index < sceneIds.Length; index++) {
            string relativePath = "assets/scenes/physics/" + sceneIds[index] + ".helen";
            string fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            SceneAsset scene = new() {
                Id = sceneIds[index],
                AuthoringAssetId = BuildAuthoringAssetId(sceneIds[index]),
                RootEntities = []
            };
            using FileStream stream = new(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            global::helengine.files.AssetSerializer.Serialize(stream, scene);
        }

        List<object> sceneOrders = [];
        for (int index = 0; index < sceneIds.Length; index++) {
            sceneOrders.Add(new {
                sceneId = sceneIds[index],
                orderNumber = index + 1
            });
        }
        var config = new {
            platforms = new[] {
                new {
                    platformId = "ds",
                    selectedSceneIds = sceneIds,
                    sceneOrders,
                    outputDirectoryPath = Path.Combine(rootPath, "output", "ds"),
                    debugBuild = true,
                    selectedBuildProfileId = "debug",
                    selectedGraphicsProfileId = "ds-main-2d",
                    selectedCodegenProfileId = "default",
                    selectedStorageProfileId = "loose-files",
                    selectedMediaProfileId = "ds-files"
                }
            },
            queueItems = Array.Empty<object>()
        };
        File.WriteAllText(
            Path.Combine(rootPath, "user_settings", "build_config.json"),
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        return new CityFixtureBuildProject(rootPath);
    }

    static string BuildAuthoringAssetId(string value) {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}

/// <summary>
/// Resolves the checked-in PS Vita source fixtures used by native source-contract tests.
/// </summary>
internal static class PsVitaFixtureRepository {
    const string FixtureRootRelativePath = "fixtures/psvita";

    public static string ResolveSourcePath(string relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath)) {
            throw new ArgumentException("Fixture source path must be provided.", nameof(relativePath));
        }

        string path = Path.Combine(
            TestSourceRepositoryLocator.ResolveHelEngineRootPath(),
            "engine",
            "helengine.editor.tests",
            FixtureRootRelativePath.Replace('/', Path.DirectorySeparatorChar),
            relativePath.Replace('/', Path.DirectorySeparatorChar) + ".fixture");
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"PS Vita fixture source '{relativePath}' was not found.", path);
        }

        return path;
    }
}

internal sealed class CityFixtureBuildProject : IDisposable {
    public CityFixtureBuildProject(string rootPath) {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }
}

internal sealed class CityTextureFixtureProject : IDisposable {
    readonly AssetImportManager AssetImportManager;

    CityTextureFixtureProject(string rootPath) {
        RootPath = rootPath;
        Directory.CreateDirectory(Path.Combine(rootPath, "assets"));
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(rootPath));
        AssetImportManager = new AssetImportManager(rootPath, contentManager);
        AssetImportManager.RegisterTextureImporter(new TextureImporterRegistration("gdi", new TestTextureImporter(), [".bmp"]));
        AssetImportManager.CurrentPlatformId = "gamecube";
    }

    public string RootPath { get; }

    public AssetImportManager Manager => AssetImportManager;

    public static CityTextureFixtureProject Create() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-city-texture-fixture-" + Guid.NewGuid().ToString("N"));
        return new CityTextureFixtureProject(rootPath);
    }

    public string WriteTextureSource(string relativePath) {
        string fullPath = Path.Combine(RootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [1, 2, 3, 4]);
        return fullPath;
    }

    public void Dispose() {
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }
}

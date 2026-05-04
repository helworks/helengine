using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor-owned asset cook service writes runtime cooked assets and startup-scene metadata.
/// </summary>
public sealed class EditorPlatformAssetCookServiceTests : IDisposable {
    readonly string ProjectRootPath;
    readonly string BuildRootPath;

    public EditorPlatformAssetCookServiceTests() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-cook-tests", Guid.NewGuid().ToString("N"));
        ProjectRootPath = workspaceRootPath;
        BuildRootPath = Path.Combine(workspaceRootPath, "Build");
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "shader-cache"));
        Directory.CreateDirectory(BuildRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void Cook_scene_build_outputs_runtime_hasset_and_sets_startup_scene_from_order() {
        string startupSceneId = "Scenes/MainMenu.helen";
        string secondarySceneId = "Scenes/Level01.helen";
        string sourceModelRelativePath = "Models/Sponza.obj";
        string sourceModelPath = Path.Combine(ProjectRootPath, "assets", sourceModelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(sourceModelPath)!);
        File.WriteAllText(sourceModelPath, "o Sponza\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

        WriteSceneAsset(
            startupSceneId,
            new[] {
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = sourceModelRelativePath,
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                }
            });
        WriteSceneAsset(secondarySceneId, Array.Empty<SceneAssetReference>());

        EditorPlatformAssetCookService service = new(
            ProjectRootPath,
            "1.0.0-engine",
            "game",
            "1.0.0",
            new IAssetImporterRegistration[] {
                new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" })
            },
            null);

        PlatformBuildManifest manifest = service.Cook(
            new helengine.baseplatform.Definitions.PlatformDefinition(
                "windows",
                "Windows",
                Array.Empty<helengine.baseplatform.Definitions.PlatformBuildProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformGraphicsProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformAssetRequirementDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentCompatibilityDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformCodegenProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMediaProfileDefinition>()),
            new[] { startupSceneId, secondarySceneId },
            BuildRootPath,
            new[] { "windows" });

        Assert.Equal(startupSceneId, manifest.StartupSceneId);
        Assert.Contains(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.RelativePath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "scenes", "main.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "scenes", "startup.hasset")));
        Assert.True(File.Exists(Path.Combine(BuildRootPath, "scenes", "Scenes", "Level01.hasset")));
        Assert.False(File.Exists(Path.Combine(BuildRootPath, "Models", "Sponza.obj")));
    }

    void WriteSceneAsset(string sceneId, SceneAssetReference[] assetReferences) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = assetReferences ?? Array.Empty<SceneAssetReference>(),
            RootEntities = new[] {
                new SceneEntityAsset {
                    Id = "root-entity",
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            }
        };

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }
}

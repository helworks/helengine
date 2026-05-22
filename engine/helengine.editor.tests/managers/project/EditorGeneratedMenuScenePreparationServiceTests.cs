using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies build-time preparation now requires project-owned menu scene assets to exist before selected-scene resolution begins.
/// </summary>
public sealed class EditorGeneratedMenuScenePreparationServiceTests : IDisposable {
    /// <summary>
    /// Temporary project root used by the current test instance.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Initializes one isolated project root for generated menu-scene preparation tests.
    /// </summary>
    public EditorGeneratedMenuScenePreparationServiceTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-menu-scene-preparation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
    }

    /// <summary>
    /// Deletes the temporary project root after each test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures Nintendo DS build preparation succeeds when the project already contains the generated DS menu scene.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsMenuSceneExists_DoesNotThrow() {
        WriteNintendoDsMenuScene("ExistingNintendoDsMenuRoot");
        EditorGeneratedMenuScenePreparationService service = new EditorGeneratedMenuScenePreparationService(ProjectRootPath, new ScriptTypeResolver());

        service.EnsurePrepared([PlatformMenuSceneResolver.NintendoDsMainMenuSceneId]);
    }

    /// <summary>
    /// Ensures Nintendo DS build preparation fails clearly when the generated DS menu scene is missing.
    /// </summary>
    [Fact]
    public void EnsurePrepared_WhenNintendoDsMenuSceneIsMissing_ThrowsClearError() {
        EditorGeneratedMenuScenePreparationService service = new EditorGeneratedMenuScenePreparationService(ProjectRootPath, null);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.EnsurePrepared([PlatformMenuSceneResolver.NintendoDsMainMenuSceneId]));

        Assert.Contains("DemoDiscMainMenuDs", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Writes one placeholder Nintendo DS menu scene so existence-only preparation behavior can be verified.
    /// </summary>
    /// <param name="rootEntityName">Root entity name written into the placeholder scene.</param>
    void WriteNintendoDsMenuScene(string rootEntityName) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", "DemoDiscMainMenuDs.helen");
        SceneAsset staleSceneAsset = new SceneAsset {
            Id = "scenes/DemoDiscMainMenuDs.helen",
            RootEntities = [
                new SceneEntityAsset {
                    Id = 1,
                    Name = rootEntityName,
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            ]
        };

        using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, staleSceneAsset);
    }
}

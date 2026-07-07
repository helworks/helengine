namespace helengine.editor.tests;

/// <summary>
/// Verifies the high-level platform scene authoring helper stamps sparse existence overrides without requiring generators to edit hidden save metadata directly.
/// </summary>
public sealed class PlatformSceneAuthoringHelperServiceTests : IDisposable {
    /// <summary>
    /// Gets the isolated temporary project root used by the current test instance.
    /// </summary>
    string TempProjectRootPath { get; }

    /// <summary>
    /// Initializes one isolated temporary project root for the current test instance.
    /// </summary>
    public PlatformSceneAuthoringHelperServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-platform-scene-authoring-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
        InitializeCore();
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
    /// Ensures restricting one subtree to DS and 3DS writes explicit false existence overrides only for the other supported platforms and applies them recursively through children.
    /// </summary>
    [Fact]
    public void RestrictEntitySubtreeToPlatforms_WhenSubtreeTargetsNintendoHandhelds_StoresFalseOverridesForOtherPlatformsRecursively() {
        new EditorProjectPlatformsService(TempProjectRootPath).Save(new EditorProjectPlatformsDocument {
            SupportedPlatforms = ["windows", "wii", "ds", "3ds"]
        });
        PlatformSceneAuthoringHelperService service = new PlatformSceneAuthoringHelperService();
        EditorEntity rootEntity = new EditorEntity {
            Name = "HandheldRoot"
        };
        EditorEntity childEntity = new EditorEntity {
            Name = "HandheldChild"
        };
        rootEntity.AddChild(childEntity);

        service.RestrictEntitySubtreeToPlatforms(TempProjectRootPath, rootEntity, ["ds", "3ds"]);

        EntitySaveComponent rootSaveComponent = GetSaveComponent(rootEntity);
        EntitySaveComponent childSaveComponent = GetSaveComponent(childEntity);

        Assert.True(rootSaveComponent.TryGetExistencePlatformOverride("windows", out SceneEntityPlatformExistenceOverrideAsset rootWindowsOverride));
        Assert.False(rootWindowsOverride.Exists);
        Assert.True(rootSaveComponent.TryGetExistencePlatformOverride("wii", out SceneEntityPlatformExistenceOverrideAsset rootWiiOverride));
        Assert.False(rootWiiOverride.Exists);
        Assert.False(rootSaveComponent.TryGetExistencePlatformOverride("ds", out _));
        Assert.False(rootSaveComponent.TryGetExistencePlatformOverride("3ds", out _));

        Assert.True(childSaveComponent.TryGetExistencePlatformOverride("windows", out SceneEntityPlatformExistenceOverrideAsset childWindowsOverride));
        Assert.False(childWindowsOverride.Exists);
        Assert.True(childSaveComponent.TryGetExistencePlatformOverride("wii", out SceneEntityPlatformExistenceOverrideAsset childWiiOverride));
        Assert.False(childWiiOverride.Exists);
        Assert.False(childSaveComponent.TryGetExistencePlatformOverride("ds", out _));
        Assert.False(childSaveComponent.TryGetExistencePlatformOverride("3ds", out _));
    }

    /// <summary>
    /// Ensures excluding one subtree from DS and 3DS writes explicit false existence overrides only for those handheld platforms while preserving the common platforms implicitly.
    /// </summary>
    [Fact]
    public void ExcludeEntitySubtreeFromPlatforms_WhenSubtreeExcludesNintendoHandhelds_StoresFalseOverridesOnlyForHandheldPlatforms() {
        new EditorProjectPlatformsService(TempProjectRootPath).Save(new EditorProjectPlatformsDocument {
            SupportedPlatforms = ["windows", "wii", "ds", "3ds"]
        });
        PlatformSceneAuthoringHelperService service = new PlatformSceneAuthoringHelperService();
        EditorEntity rootEntity = new EditorEntity {
            Name = "DesktopRoot"
        };

        service.ExcludeEntitySubtreeFromPlatforms(TempProjectRootPath, rootEntity, ["ds", "3ds"]);

        EntitySaveComponent saveComponent = GetSaveComponent(rootEntity);
        Assert.False(saveComponent.TryGetExistencePlatformOverride("windows", out _));
        Assert.False(saveComponent.TryGetExistencePlatformOverride("wii", out _));
        Assert.True(saveComponent.TryGetExistencePlatformOverride("ds", out SceneEntityPlatformExistenceOverrideAsset dsOverride));
        Assert.False(dsOverride.Exists);
        Assert.True(saveComponent.TryGetExistencePlatformOverride("3ds", out SceneEntityPlatformExistenceOverrideAsset nintendo3DsOverride));
        Assert.False(nintendo3DsOverride.Exists);
    }

    /// <summary>
    /// Retrieves the hidden save component attached to one editor entity.
    /// </summary>
    /// <param name="entity">Entity whose save component should be returned.</param>
    /// <returns>Attached hidden save component.</returns>
    static EntitySaveComponent GetSaveComponent(EditorEntity entity) {
        if (entity == null) {
            throw new ArgumentNullException(nameof(entity));
        }
        if (entity.Components == null) {
            throw new InvalidOperationException("Entity components were not initialized.");
        }

        for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
            if (entity.Components[componentIndex] is EntitySaveComponent saveComponent) {
                return saveComponent;
            }
        }

        throw new InvalidOperationException("Entity did not receive the expected hidden save component.");
    }

    /// <summary>
    /// Initializes the minimal runtime services required for editor entities created by these tests.
    /// </summary>
    void InitializeCore() {
        Core core = new Core();
        core.Initialize(null, new helengine.editor.tests.testing.TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
    }
}

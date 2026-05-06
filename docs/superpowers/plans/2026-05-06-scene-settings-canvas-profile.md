# Scene Settings Canvas Profile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move authored 2D resolution from camera viewports into one scene-owned CanvasProfile, then wire editor preview and a new Scene Settings UI to that scene-level contract.

**Architecture:** Add one scene-owned settings payload to `SceneAsset`, flow it through editor load/save as part of a loaded-scene document, and make preview systems consume scene canvas dimensions instead of viewport-local width and height. Keep renderer execution details separate by treating the CanvasProfile as logical presentation metadata only, while the editor exposes it through a dedicated Scene Settings dialog rather than entity properties or viewport-local sliders.

**Tech Stack:** C#/.NET 9, existing HELE scene asset serialization, editor UI entities/components, xUnit tests, RTK `dotnet test`

---

## File Map

### Core scene asset and serialization

- Create: `engine/helengine.core/assets/raw/scene/SceneCanvasProfile.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

### Editor scene document flow

- Create: `engine/helengine.editor/serialization/scene/LoadedEditorSceneDocument.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

### Scene-owned canvas state and preview consumers

- Create: `engine/helengine.editor/managers/scene/EditorSceneCanvasProfileState.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor/managers/preview/CameraPreviewSource.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

### Scene Settings UI and session wiring

- Create: `engine/helengine.editor/components/ui/SceneSettingsDialog.cs`
- Create: `engine/helengine.editor/components/ui/SceneSettingsDialogUpdater.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSettingsTests.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

### Demo-disc and regression coverage

- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`

---

### Task 1: Add Scene-Owned Canvas Settings To SceneAsset

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/SceneCanvasProfile.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs`
- Modify: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serialization tests**

```csharp
[Fact]
public void AssetSerializer_SceneAsset_WithSceneCanvasProfile_RoundTripsCanvasSettings() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/Main.helen",
        SceneSettings = new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile(1280, 720)
        },
        RootEntities = Array.Empty<SceneEntityAsset>(),
        AssetReferences = Array.Empty<SceneAssetReference>()
    };

    byte[] data = AssetSerializer.SerializeToBytes(asset);
    SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(data));

    Assert.NotNull(deserialized.SceneSettings);
    Assert.Equal(1280, deserialized.SceneSettings.CanvasProfile.CanvasWidth);
    Assert.Equal(720, deserialized.SceneSettings.CanvasProfile.CanvasHeight);
}

[Fact]
public void AssetSerializer_SceneAsset_WhenLegacyPayloadHasNoSceneSettings_UsesDefaultCanvasProfile() {
    byte[] versionFiveScenePayload = BuildVersionFiveScenePayloadWithoutSceneSettings();

    SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(versionFiveScenePayload));

    Assert.NotNull(deserialized.SceneSettings);
    Assert.Equal(1280, deserialized.SceneSettings.CanvasProfile.CanvasWidth);
    Assert.Equal(720, deserialized.SceneSettings.CanvasProfile.CanvasHeight);
}
```

- [ ] **Step 2: Run the targeted serialization tests to verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests.AssetSerializer_SceneAsset_WithSceneCanvasProfile_RoundTripsCanvasSettings|FullyQualifiedName~BinarySerializationTests.AssetSerializer_SceneAsset_WhenLegacyPayloadHasNoSceneSettings_UsesDefaultCanvasProfile" -v minimal`

Expected: FAIL because `SceneAsset` does not yet expose `SceneSettings`, and serializer code does not read or write the new payload.

- [ ] **Step 3: Add the new scene settings model types**

```csharp
namespace helengine {
    /// <summary>
    /// Stores the logical 2D authoring surface used by one scene.
    /// </summary>
    public sealed class SceneCanvasProfile {
        /// <summary>
        /// Default logical canvas width used by scenes that do not persist explicit settings.
        /// </summary>
        public const int DefaultCanvasWidth = 1280;

        /// <summary>
        /// Default logical canvas height used by scenes that do not persist explicit settings.
        /// </summary>
        public const int DefaultCanvasHeight = 720;

        /// <summary>
        /// Initializes one scene canvas profile with explicit dimensions.
        /// </summary>
        /// <param name="canvasWidth">Logical authored width in pixels.</param>
        /// <param name="canvasHeight">Logical authored height in pixels.</param>
        public SceneCanvasProfile(int canvasWidth, int canvasHeight) {
            CanvasWidth = Math.Max(1, canvasWidth);
            CanvasHeight = Math.Max(1, canvasHeight);
        }

        /// <summary>
        /// Gets the logical authored width in pixels.
        /// </summary>
        public int CanvasWidth { get; }

        /// <summary>
        /// Gets the logical authored height in pixels.
        /// </summary>
        public int CanvasHeight { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Stores scene-owned authored settings that are not tied to one entity or component.
    /// </summary>
    public sealed class SceneSettingsAsset {
        /// <summary>
        /// Initializes one settings payload with the default canvas profile.
        /// </summary>
        public SceneSettingsAsset() {
            CanvasProfile = new SceneCanvasProfile(
                SceneCanvasProfile.DefaultCanvasWidth,
                SceneCanvasProfile.DefaultCanvasHeight);
        }

        /// <summary>
        /// Gets or sets the logical scene canvas profile.
        /// </summary>
        public SceneCanvasProfile CanvasProfile { get; set; }
    }
}
```

- [ ] **Step 4: Extend SceneAsset and both binary serializers**

```csharp
public class SceneAsset : Asset {
    public SceneEntityAsset[] RootEntities { get; set; } = Array.Empty<SceneEntityAsset>();
    public SceneAssetReference[] AssetReferences { get; set; } = Array.Empty<SceneAssetReference>();
    public uint Physics3DSceneFeatureFlags { get; set; }
    public SceneSettingsAsset SceneSettings { get; set; } = new SceneSettingsAsset();
}
```

```csharp
static void WriteSceneAsset(EngineBinaryWriter writer, SceneAsset asset) {
    writer.WriteString(asset.Id);
    writer.WriteArray(asset.RootEntities, WriteSceneEntityAsset);
    writer.WriteArray(asset.AssetReferences, WriteSceneAssetReference);
    writer.WriteUInt32(asset.Physics3DSceneFeatureFlags);
    writer.WriteInt32(asset.SceneSettings.CanvasProfile.CanvasWidth);
    writer.WriteInt32(asset.SceneSettings.CanvasProfile.CanvasHeight);
}

static SceneAsset ReadSceneAsset(EngineBinaryReader reader, byte version) {
    uint physicsFlags = version >= 5 ? reader.ReadUInt32() : 0u;
    SceneSettingsAsset sceneSettings = version >= 6
        ? new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile(reader.ReadInt32(), reader.ReadInt32())
        }
        : new SceneSettingsAsset();

    return new SceneAsset {
        Id = reader.ReadString(),
        RootEntities = ReadSceneEntityAssetArray(reader, version) ?? Array.Empty<SceneEntityAsset>(),
        AssetReferences = version >= 4 ? ReadSceneAssetReferenceArray(reader) ?? Array.Empty<SceneAssetReference>() : Array.Empty<SceneAssetReference>(),
        Physics3DSceneFeatureFlags = physicsFlags,
        SceneSettings = sceneSettings
    };
}
```

- [ ] **Step 5: Run the targeted serialization tests to verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests.AssetSerializer_SceneAsset_WithSceneCanvasProfile_RoundTripsCanvasSettings|FullyQualifiedName~BinarySerializationTests.AssetSerializer_SceneAsset_WhenLegacyPayloadHasNoSceneSettings_UsesDefaultCanvasProfile" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.core/assets/raw/scene/SceneCanvasProfile.cs engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs engine/helengine.core/assets/raw/scene/SceneAsset.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "feat: persist scene canvas settings"
```

### Task 2: Flow Scene Settings Through Editor Load And Save

**Files:**
- Create: `engine/helengine.editor/serialization/scene/LoadedEditorSceneDocument.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write the failing editor scene document tests**

```csharp
[Fact]
public void Load_WhenSceneContainsSceneCanvasProfile_ReturnsLoadedDocumentWithSceneSettings() {
    string scenePath = SaveSceneAssetWithCanvasProfile("Opened.helen", 1920, 1080);
    SceneFileLoadService loadService = CreateLoadService();

    LoadedEditorSceneDocument document = loadService.Load(scenePath);

    Assert.Equal(1920, document.SceneSettings.CanvasProfile.CanvasWidth);
    Assert.Equal(1080, document.SceneSettings.CanvasProfile.CanvasHeight);
    Assert.NotEmpty(document.RootEntities);
}

[Fact]
public void Save_WhenSceneSettingsAreProvided_PersistsCanvasProfileIntoSceneAsset() {
    SceneSaveService saveService = CreateSaveService();
    SceneSettingsAsset settings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile(1600, 900)
    };

    saveService.Save(ScenePath, settings);

    SceneAsset savedScene = LoadSerializedScene(ScenePath);
    Assert.Equal(1600, savedScene.SceneSettings.CanvasProfile.CanvasWidth);
    Assert.Equal(900, savedScene.SceneSettings.CanvasProfile.CanvasHeight);
}
```

- [ ] **Step 2: Run the targeted scene-open/save tests to verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneOpenTests.Load_WhenSceneContainsSceneCanvasProfile_ReturnsLoadedDocumentWithSceneSettings|FullyQualifiedName~EditorSessionSceneSaveTests.Save_WhenSceneSettingsAreProvided_PersistsCanvasProfileIntoSceneAsset|FullyQualifiedName~SceneSaveServiceTests.Save_WhenSceneSettingsAreProvided_PersistsCanvasProfileIntoSceneAsset" -v minimal`

Expected: FAIL because `SceneFileLoadService.Load` still returns only root entities and `SceneSaveService.Save` does not accept scene settings.

- [ ] **Step 3: Add a loaded-scene document type and thread it through file loading**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Represents one loaded scene document including materialized roots and scene-owned settings.
    /// </summary>
    public sealed class LoadedEditorSceneDocument {
        /// <summary>
        /// Initializes one loaded scene document.
        /// </summary>
        public LoadedEditorSceneDocument(IReadOnlyList<EditorEntity> rootEntities, SceneSettingsAsset sceneSettings) {
            RootEntities = rootEntities ?? throw new ArgumentNullException(nameof(rootEntities));
            SceneSettings = sceneSettings ?? throw new ArgumentNullException(nameof(sceneSettings));
        }

        /// <summary>
        /// Gets the loaded root editor entities.
        /// </summary>
        public IReadOnlyList<EditorEntity> RootEntities { get; }

        /// <summary>
        /// Gets the scene-owned settings restored from the scene asset.
        /// </summary>
        public SceneSettingsAsset SceneSettings { get; }
    }
}
```

```csharp
public LoadedEditorSceneDocument Load(string fullPath) {
    // existing path validation and deserialization
    IReadOnlyList<EditorEntity> loadedRoots = SceneLoadService.Load(sceneAsset);
    SetRootsEnabled(loadedRoots, false);
    return new LoadedEditorSceneDocument(loadedRoots, sceneAsset.SceneSettings ?? new SceneSettingsAsset());
}
```

- [ ] **Step 4: Extend SceneSaveService to accept scene-owned settings**

```csharp
public void Save(string fullPath, SceneSettingsAsset sceneSettings) {
    if (sceneSettings == null) {
        throw new ArgumentNullException(nameof(sceneSettings));
    }

    SceneAsset asset = BuildSceneAsset(fullPath, sceneSettings);
    using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
    AssetSerializer.Serialize(stream, asset);
}

SceneAsset BuildSceneAsset(string fullPath, SceneSettingsAsset sceneSettings) {
    // existing root-entity and asset-reference gathering
    return new SceneAsset {
        Id = sceneId,
        RootEntities = rootEntities.ToArray(),
        AssetReferences = assetReferences.ToArray(),
        SceneSettings = sceneSettings
    };
}
```

- [ ] **Step 5: Run the targeted scene-open/save tests to verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneOpenTests.Load_WhenSceneContainsSceneCanvasProfile_ReturnsLoadedDocumentWithSceneSettings|FullyQualifiedName~EditorSessionSceneSaveTests.Save_WhenSceneSettingsAreProvided_PersistsCanvasProfileIntoSceneAsset|FullyQualifiedName~SceneSaveServiceTests.Save_WhenSceneSettingsAreProvided_PersistsCanvasProfileIntoSceneAsset" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/serialization/scene/LoadedEditorSceneDocument.cs engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "feat: flow scene settings through editor scene documents"
```

### Task 3: Introduce Scene-Owned Canvas State In The Editor

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorSceneCanvasProfileState.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write the failing state/preview tests**

```csharp
[Fact]
public void PreviewPlane_WhenSceneCanvasProfileChanges_RebuildsRenderTargetToSceneCanvasDimensions() {
    EditorSceneCanvasProfileState sceneCanvasState = new EditorSceneCanvasProfileState(new SceneCanvasProfile(1280, 720));
    EditorViewportCanvasPreviewSettings viewportSettings = new EditorViewportCanvasPreviewSettings();
    EditorViewportCanvasPlanePreviewComponent component = CreatePreviewComponent(sceneCanvasState, viewportSettings);

    sceneCanvasState.Apply(new SceneCanvasProfile(1920, 1080));
    component.Update();

    Assert.Equal(1920, component.PreviewRenderTarget.Width);
    Assert.Equal(1080, component.PreviewRenderTarget.Height);
}

[Fact]
public void ViewportCanvasOverlay_WhenConstructed_DoesNotOwnCanvasWidthOrHeight() {
    EditorViewport viewport = CreateViewport();

    Assert.Equal(100, viewport.CanvasPreviewSettings.PixelsPerWorldUnit);
    Assert.Equal(1280, viewport.SceneCanvasProfile.CanvasWidth);
    Assert.Equal(720, viewport.SceneCanvasProfile.CanvasHeight);
}
```

- [ ] **Step 2: Run the targeted canvas preview tests to verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests|FullyQualifiedName~EditorViewportCanvasPlaneSelectionServiceTests|FullyQualifiedName~EditorViewportSettingsOverlayTests" -v minimal`

Expected: FAIL because scene-owned canvas state does not yet exist and viewport settings still own width and height.

- [ ] **Step 3: Add one scene-owned editor canvas state object**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores the active scene canvas profile used by editor preview and layout systems.
    /// </summary>
    public sealed class EditorSceneCanvasProfileState {
        /// <summary>
        /// Initializes one active scene canvas state from the supplied profile.
        /// </summary>
        public EditorSceneCanvasProfileState(SceneCanvasProfile canvasProfile) {
            Apply(canvasProfile);
        }

        /// <summary>
        /// Raised whenever the active scene canvas changes.
        /// </summary>
        public event Action Changed;

        /// <summary>
        /// Gets the active logical scene canvas width in pixels.
        /// </summary>
        public int CanvasWidth { get; private set; }

        /// <summary>
        /// Gets the active logical scene canvas height in pixels.
        /// </summary>
        public int CanvasHeight { get; private set; }

        /// <summary>
        /// Applies one new canvas profile to the active scene state.
        /// </summary>
        public void Apply(SceneCanvasProfile canvasProfile) {
            if (canvasProfile == null) {
                throw new ArgumentNullException(nameof(canvasProfile));
            }

            CanvasWidth = canvasProfile.CanvasWidth;
            CanvasHeight = canvasProfile.CanvasHeight;
            Changed?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Strip width and height ownership from viewport-local preview settings**

```csharp
public sealed class EditorViewportCanvasPreviewSettings {
    public const int DefaultPixelsPerWorldUnit = 100;

    int PixelsPerWorldUnitValue = DefaultPixelsPerWorldUnit;

    public event Action SettingsChanged;

    public int PixelsPerWorldUnit {
        get => PixelsPerWorldUnitValue;
        set {
            int clampedValue = Math.Max(1, value);
            if (PixelsPerWorldUnitValue == clampedValue) {
                return;
            }

            PixelsPerWorldUnitValue = clampedValue;
            SettingsChanged?.Invoke();
        }
    }
}
```

```csharp
EditorViewportCanvasPlanePreviewComponent(
    CameraComponent sceneCamera,
    EditorSceneCanvasProfileState sceneCanvasProfile,
    EditorViewportCanvasPreviewSettings settings,
    RenderManager3D render3D)
```

- [ ] **Step 5: Update preview-plane and coordinate-mapper consumers to read scene-owned width and height**

```csharp
int nextCanvasWidth = Math.Max(1, SceneCanvasProfile.CanvasWidth);
int nextCanvasHeight = Math.Max(1, SceneCanvasProfile.CanvasHeight);
int nextPixelsPerWorldUnit = Math.Max(1, Settings.PixelsPerWorldUnit);
```

```csharp
public static int2 MapWorldToCanvas(
    float3 worldPoint,
    EditorSceneCanvasProfileState sceneCanvasProfile,
    EditorViewportCanvasPreviewSettings settings) {
    int canvasHeight = Math.Max(1, sceneCanvasProfile.CanvasHeight);
    // existing mapping math
}
```

- [ ] **Step 6: Run the targeted canvas preview tests to verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests|FullyQualifiedName~EditorViewportCanvasPlaneSelectionServiceTests|FullyQualifiedName~EditorViewportSettingsOverlayTests" -v minimal`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorSceneCanvasProfileState.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionServiceTests.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
git commit -m "refactor: move canvas resolution ownership to scene state"
```

### Task 4: Make Camera Preview Use The Scene Canvas Profile

**Files:**
- Modify: `engine/helengine.editor/managers/preview/CameraPreviewSource.cs`
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Test: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`

- [ ] **Step 1: Write the failing camera preview tests**

```csharp
[Fact]
public void Resize_WhenSceneCanvasProfileExists_UsesSceneCanvasDimensionsInsteadOfPanelSize() {
    EditorSceneCanvasProfileState sceneCanvasState = new EditorSceneCanvasProfileState(new SceneCanvasProfile(1280, 720));
    CameraPreviewSource source = CreatePreviewSource(sceneCanvasState);

    source.Resize(new int2(320, 180));

    TestRenderTarget renderTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
    Assert.Equal(1280, renderTarget.Width);
    Assert.Equal(720, renderTarget.Height);
}

[Fact]
public void Update_WhenSceneCanvasProfileChanges_RebuildsPreviewCameraViewport() {
    EditorSceneCanvasProfileState sceneCanvasState = new EditorSceneCanvasProfileState(new SceneCanvasProfile(1280, 720));
    CameraPreviewSource source = CreatePreviewSource(sceneCanvasState);

    sceneCanvasState.Apply(new SceneCanvasProfile(1920, 1080));
    source.Update();

    Assert.Equal(new float4(0f, 0f, 1920f, 1080f), source.PreviewCamera.Viewport);
}
```

- [ ] **Step 2: Run the targeted preview tests to verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraPreviewSourceTests|FullyQualifiedName~EditorSessionPreviewSelectionTests" -v minimal`

Expected: FAIL because the preview source still derives its effective render size from panel size or suppression metadata only.

- [ ] **Step 3: Pass scene canvas state into the preview source**

```csharp
public CameraPreviewSource(
    Entity sourceEntity,
    CameraComponent sourceCameraComponent,
    EditorSceneCanvasProfileState sceneCanvasProfile,
    RenderManager3D renderManager3D) {
    SceneCanvasProfile = sceneCanvasProfile ?? throw new ArgumentNullException(nameof(sceneCanvasProfile));
    SceneCanvasProfile.Changed += HandleSceneCanvasProfileChanged;
    // existing initialization
}
```

```csharp
int2 ResolvePreviewTargetSize() {
    return new int2(
        Math.Max(1, SceneCanvasProfile.CanvasWidth),
        Math.Max(1, SceneCanvasProfile.CanvasHeight));
}
```

- [ ] **Step 4: Keep preview panel scaling behavior but stop treating panel size as authored render size**

```csharp
ActivePreviewSourceValue.Resize(GetContentSize());
textureSprite.Texture = ActivePreviewSourceValue.Texture;
LayoutPreview();
```

The `PreviewPanel` still scales the offscreen texture to fit, but `CameraPreviewSource` now owns the true render-target dimensions through the scene canvas profile.

- [ ] **Step 5: Run the targeted preview tests to verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraPreviewSourceTests|FullyQualifiedName~EditorSessionPreviewSelectionTests" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/managers/preview/CameraPreviewSource.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs
git commit -m "fix: drive camera preview from scene canvas profile"
```

### Task 5: Add Scene Settings UI And Session Wiring

**Files:**
- Create: `engine/helengine.editor/components/ui/SceneSettingsDialog.cs`
- Create: `engine/helengine.editor/components/ui/SceneSettingsDialogUpdater.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSettingsTests.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write the failing scene-settings UI tests**

```csharp
[Fact]
public void HandleSceneSettingsRequested_WhenDialogConfirms_UpdatesActiveSceneCanvasProfile() {
    EditorSession session = CreateSession();
    InvokePrivate(session, "HandleSceneSettingsRequested");

    SceneSettingsDialog dialog = GetPrivateField<SceneSettingsDialog>(session, "sceneSettingsDialog");
    dialog.SetCanvasProfileForTest(1920, 1080);
    dialog.ConfirmForTest();

    EditorSceneCanvasProfileState sceneCanvasProfile = GetPrivateField<EditorSceneCanvasProfileState>(session, "activeSceneCanvasProfile");
    Assert.Equal(1920, sceneCanvasProfile.CanvasWidth);
    Assert.Equal(1080, sceneCanvasProfile.CanvasHeight);
}

[Fact]
public void TitleBar_WhenSceneSettingsItemIsClicked_RaisesSceneSettingsRequested() {
    EditorTitleBar titleBar = CreateTitleBar();
    bool raised = false;
    titleBar.SceneSettingsRequested += () => raised = true;

    ClickSceneSettingsMenuItem(titleBar);

    Assert.True(raised);
}
```

- [ ] **Step 2: Run the targeted scene-settings tests to verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSettingsTests|FullyQualifiedName~EditorViewportSettingsOverlayTests" -v minimal`

Expected: FAIL because there is no scene settings dialog or title-bar event yet.

- [ ] **Step 3: Add a dedicated Scene Settings dialog and menu event**

```csharp
public event Action SceneSettingsRequested;

IReadOnlyList<ContextMenuItem> CreateSceneMenuItems() {
    return new[] {
        new ContextMenuItem("scene.settings", "Scene Settings...", RaiseSceneSettingsRequested)
    };
}
```

```csharp
public sealed class SceneSettingsDialog : EditorDialogBase {
    public event Action<SceneCanvasProfile> Confirmed;

    public void Show(SceneCanvasProfile canvasProfile) {
        CanvasWidth = canvasProfile.CanvasWidth;
        CanvasHeight = canvasProfile.CanvasHeight;
        base.Show();
    }
}
```

- [ ] **Step 4: Wire the dialog into EditorSession and persist changes back to active scene state**

```csharp
void HandleSceneSettingsRequested() {
    sceneSettingsDialog.Show(new SceneCanvasProfile(activeSceneCanvasProfile.CanvasWidth, activeSceneCanvasProfile.CanvasHeight));
}

void HandleSceneSettingsConfirmed(SceneCanvasProfile canvasProfile) {
    activeSceneCanvasProfile.Apply(canvasProfile);
    activeSceneSettings.CanvasProfile = canvasProfile;
}
```

```csharp
void HandleOpenSceneConfirmed(string fullPath) {
    LoadedEditorSceneDocument document = SceneFileLoadService.Load(fullPath);
    activeSceneSettings = document.SceneSettings;
    activeSceneCanvasProfile.Apply(activeSceneSettings.CanvasProfile);
    LoadRoots(document.RootEntities);
}
```

- [ ] **Step 5: Reduce the viewport overlay to viewport-local presentation controls only**

```csharp
// Remove canvas width/height slider rows.
// Keep only:
// - grid toggle
// - pixels per world unit
// - near plane
// - far plane
```

- [ ] **Step 6: Run the targeted scene-settings tests to verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSettingsTests|FullyQualifiedName~EditorViewportSettingsOverlayTests" -v minimal`

Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/components/ui/SceneSettingsDialog.cs engine/helengine.editor/components/ui/SceneSettingsDialogUpdater.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor.tests/EditorSessionSceneSettingsTests.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
git commit -m "feat: add scene settings canvas profile dialog"
```

### Task 6: Update Demo Menu Defaults And Run End-To-End Regression Coverage

**Files:**
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Test: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs`
- Test: `engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write the failing demo/regression tests**

```csharp
[Fact]
public void BuildSceneAsset_WhenDemoMenuSceneIsGenerated_PersistsSceneCanvasProfile() {
    DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();

    SceneAsset sceneAsset = factory.BuildSceneAsset("Scenes/DemoDiscMainMenu.helen", "Provider", CreateDefinition());

    Assert.Equal(1280, sceneAsset.SceneSettings.CanvasProfile.CanvasWidth);
    Assert.Equal(720, sceneAsset.SceneSettings.CanvasProfile.CanvasHeight);
}

[Fact]
public void PreviewSelection_WhenDemoMenuCameraIsSelected_UsesSceneCanvasProfileDimensions() {
    LoadedEditorSceneDocument document = LoadDemoDiscMainMenu();

    CameraPreviewSource previewSource = ResolvePreviewSourceFromLoadedDemoScene(document);

    Assert.Equal(1280, previewSource.RenderTarget.Width);
    Assert.Equal(720, previewSource.RenderTarget.Height);
}
```

- [ ] **Step 2: Run the targeted regression slice to verify it fails**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~CameraPreviewSourceTests" -v minimal`

Expected: FAIL until the demo scene factory and preview/session flow both honor the persisted scene canvas profile.

- [ ] **Step 3: Persist scene settings from the demo menu factory**

```csharp
return new SceneAsset {
    Id = sceneId,
    SceneSettings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile(DemoMenuLayout.CanvasWidth, DemoMenuLayout.CanvasHeight)
    },
    AssetReferences = assetReferences.ToArray(),
    RootEntities = new[] {
        BuildCameraEntityAsset(),
        BuildMenuRootEntityAsset(providerTypeName, definition)
    }
};
```

- [ ] **Step 4: Run the full CanvasProfile verification slice**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests|FullyQualifiedName~EditorViewportCanvasPlaneSelectionServiceTests|FullyQualifiedName~CameraPreviewSourceTests|FullyQualifiedName~EditorSessionPreviewSelectionTests|FullyQualifiedName~EditorSessionSceneSettingsTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs engine/helengine.editor.tests/EditorSessionPreviewSelectionTests.cs engine/helengine.editor.tests/managers/preview/CameraPreviewSourceTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
git commit -m "feat: apply scene canvas profile to demo scene preview"
```

## Self-Review

- Spec coverage:
  - scene-owned canvas persistence is covered by Task 1
  - editor load/save document flow is covered by Task 2
  - scene-owned preview state replacement is covered by Task 3
  - camera preview migration is covered by Task 4
  - scene settings UI/menu is covered by Task 5
  - demo-disc regression coverage is covered by Task 6
- Placeholder scan:
  - no `TODO`, `TBD`, or implied test steps remain
  - every task includes explicit files, concrete test cases, exact commands, and commit steps
- Type consistency:
  - `SceneCanvasProfile`, `SceneSettingsAsset`, `LoadedEditorSceneDocument`, and `EditorSceneCanvasProfileState` use the same names across all tasks
  - `SceneFileLoadService.Load` consistently returns `LoadedEditorSceneDocument`
  - `SceneSaveService.Save` consistently accepts `SceneSettingsAsset`

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-06-scene-settings-canvas-profile.md`. Two execution options:

1. Subagent-Driven (recommended) - I dispatch a fresh subagent per task, review between tasks, fast iteration

2. Inline Execution - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?

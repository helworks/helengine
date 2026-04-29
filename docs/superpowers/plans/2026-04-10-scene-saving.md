# Scene Saving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `.helen` scene assets that save user-authored editor scenes inside `assets`, using explicit component persistence and an editor-owned save dialog.

**Architecture:** Add a core `SceneAsset` payload to the HELE serializer, then layer editor-only persistence on top through a hidden `EntitySaveComponent`, a descriptor registry, and a save/load service pair. Route `Save Map` and `Save Map As...` through a filesystem-only modal that resolves project-relative paths under `assets`, while the asset browser classifies `.helen` files as scene assets and shows them as read-only summaries instead of import settings.

**Tech Stack:** C#/.NET 9, Hel engine HELE asset serialization, editor UI entities/components, `ContentManager`, xUnit

---

## File Structure

### New Files

- `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
  Defines the top-level `.helen` asset payload and the stable scene file extension.
- `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs`
  Stores one serialized entity name, local transform, child hierarchy, and component records.
- `engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs`
  Stores one persisted component type id, entity-local component index, and opaque payload bytes.
- `engine/helengine.core/assets/raw/scene/SceneAssetReference.cs`
  Stores stable asset-reference metadata shared by descriptors for filesystem and generated assets.
- `engine/helengine.core/assets/raw/scene/SceneAssetReferenceSourceKind.cs`
  Distinguishes filesystem-backed references from generated-provider references.
- `engine/helengine.editor/components/persistence/IEditorHiddenComponent.cs`
  Marks editor-only components that must be hidden from the normal component property UI.
- `engine/helengine.editor/components/persistence/EntityComponentSaveState.cs`
  Stores editor-time per-component asset-reference metadata keyed by a stable reference name.
- `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
  Hidden editor-only component attached to entities to hold persistence metadata for other components.
- `engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs`
  Defines the contract used by descriptors and scene services to validate and resolve saved asset references.
- `engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs`
  Converts `AssetBrowserEntry` selections into stable `SceneAssetReference` values.
- `engine/helengine.editor/serialization/scene/SceneAssetReferenceResolver.cs`
  Resolves filesystem and generated references into runtime models and materials for save validation and load.
- `engine/helengine.editor/serialization/scene/IComponentPersistenceDescriptor.cs`
  Defines the explicit persistence contract for one component type.
- `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
  Stores descriptors keyed by component runtime type and serialized type id.
- `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
  Persists `MeshComponent` model/material references and render order.
- `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
  Builds a `SceneAsset` from the current editor scene and writes `.helen` files to disk.
- `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
  Reconstructs editor entities from `SceneAsset` payloads for round-trip verification and future load flows.
- `engine/helengine.editor/serialization/scene/SceneSavePathResolver.cs`
  Owns scene-save path validation, default folder selection, and `.helen` enforcement.
- `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
  Modal save dialog rooted inside `assets` that reuses the asset-browser view in filesystem-only mode.
- `engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs`
  Verifies `.helen` classification in the asset browser and scene-asset content loading.
- `engine/helengine.editor.tests/EntitySaveComponentTests.cs`
  Verifies `EditorEntity` auto-attaches `EntitySaveComponent` and the properties UI hides it.
- `engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs`
  Verifies descriptor lookup and clear failures for unsupported components.
- `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
  Verifies the `MeshComponent` descriptor round-trips generated and filesystem references.
- `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`
  Verifies filesystem-backed model and material picks store stable references on `EntitySaveComponent`.
- `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
  Verifies save/load round-trip, internal-entity exclusion, and unsupported-component failure behavior.
- `engine/helengine.editor.tests/serialization/scene/SceneSavePathResolverTests.cs`
  Verifies path validation, default folder selection, and `.helen` extension enforcement.
- `engine/helengine.editor.tests/SaveFileDialogTests.cs`
  Verifies the save dialog raises validated save requests and keeps validation errors visible.
- `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
  Verifies `Save Map` routes to `Save As`, successful saves update the current scene path, and scene files are written.
- `engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs`
  Test double that resolves scene asset references without needing real shader packages or generated providers.

### Modified Files

- `engine/helengine.core/Entity.cs`
  Add explicit local transform accessors so scene serialization can persist parent-relative transforms without changing existing world-transform getters.
- `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
  Add the scene asset value kind.
- `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  Teach the HELE serializer to read and write `SceneAsset` payloads.
- `engine/helengine.editor/content/EditorContentProcessorIds.cs`
  Add the stable processor id used for `.helen` scene assets.
- `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
  Register the default scene-asset processor and `.helen` extension mapping.
- `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
  Add a `Scene` browser classification.
- `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
  Classify `.helen` files as scenes.
- `engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs`
  Support filesystem-only browsing mode so the save dialog does not expose generated providers.
- `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
  Expose scene icon styling, public folder navigation, and filesystem-only mode for save dialogs.
- `engine/helengine.editor/EditorEntity.cs`
  Auto-attach the hidden `EntitySaveComponent`.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Skip hidden editor-only components and capture stable references when model/material picks succeed.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  Show scene-asset summaries instead of import settings when `.helen` files are selected.
- `engine/helengine.editor/EditorSession.cs`
  Register scene persistence services, own the save dialog/current scene path, and wire `Save Map` / `Save Map As...`.
- `engine/helengine.editor.tests/BinarySerializationTests.cs`
  Add scene-asset serializer and content-manager coverage.
- `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`
  Add filesystem-only browsing coverage for the save dialog.
- `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
  Extend generated model-pick coverage to assert stored scene references.
- `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`
  Extend asset-selection coverage so scene assets show a summary instead of import settings.
- `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`
  Add material-build support so scene load tests can return placeholder runtime materials.

## Task 1: Add SceneAsset Types, Local Transforms, And HELE Serialization

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneAssetReference.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneAssetReferenceSourceKind.cs`
- Modify: `engine/helengine.core/Entity.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serializer test**

```csharp
[Fact]
public void AssetSerializer_SceneAsset_WritesHeleHeaderAndRoundTrips() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/TestScene.helen",
        RootEntities = new[] {
            new SceneEntityAsset {
                Name = "Root",
                LocalPosition = new float3(1f, 2f, 3f),
                LocalScale = new float3(2f, 2f, 2f),
                LocalOrientation = new float4(0f, 0.70710677f, 0f, 0.70710677f),
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "helengine.core.MeshComponent",
                        ComponentIndex = 0,
                        Payload = new byte[] { 1, 2, 3, 4 }
                    }
                },
                Children = new[] {
                    new SceneEntityAsset {
                        Name = "Child",
                        LocalPosition = new float3(5f, 6f, 7f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            }
        }
    };

    byte[] data = AssetSerializer.SerializeToBytes(asset);
    EngineBinaryHeader header = ReadHeader(data);
    SceneAsset deserialized = (SceneAsset)AssetSerializer.DeserializeFromBytes(data);

    Assert.Equal(EditorAssetBinarySerializer.FormatId, header.FormatId);
    Assert.Equal((ushort)EditorAssetBinarySerializer.RecordKind, header.RecordKind);
    Assert.Equal((ushort)EditorAssetBinaryValueKind.SceneAsset, header.ValueKind);
    Assert.Equal("Scenes/TestScene.helen", deserialized.Id);
    Assert.Single(deserialized.RootEntities);
    Assert.Equal(new float3(1f, 2f, 3f), deserialized.RootEntities[0].LocalPosition);
    Assert.Equal(new float3(2f, 2f, 2f), deserialized.RootEntities[0].LocalScale);
    Assert.Equal(new byte[] { 1, 2, 3, 4 }, deserialized.RootEntities[0].Components[0].Payload);
    Assert.Equal("Child", deserialized.RootEntities[0].Children[0].Name);
}
```

- [ ] **Step 2: Run the serializer test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter BinarySerializationTests`

Expected: build failure because `SceneAsset`, `SceneEntityAsset`, `SceneComponentAssetRecord`, `SceneAssetReference`, local transform accessors, and `EditorAssetBinaryValueKind.SceneAsset` do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine {
    /// <summary>
    /// Represents one serialized editor scene stored as a HELE asset.
    /// </summary>
    public class SceneAsset : Asset {
        /// <summary>
        /// File extension used for serialized editor scenes.
        /// </summary>
        public const string FileExtension = ".helen";

        /// <summary>
        /// Gets or sets the serialized root entities stored in the scene.
        /// </summary>
        public SceneEntityAsset[] RootEntities { get; set; } = Array.Empty<SceneEntityAsset>();
    }
}
```

```csharp
public float3 LocalPosition {
    get { return position; }
    set { position = value; }
}

public float3 LocalScale {
    get { return scale; }
    set { scale = value; }
}

public float4 LocalOrientation {
    get { return orientation; }
    set { orientation = value; }
}
```

```csharp
public enum EditorAssetBinaryValueKind : ushort {
    TextureAsset = 1,
    ModelAsset = 2,
    ShaderAsset = 3,
    TextAsset = 4,
    MaterialAsset = 5,
    SceneAsset = 6
}
```

```csharp
static EditorAssetBinaryValueKind GetValueKind(Asset asset) {
    if (asset is SceneAsset) {
        return EditorAssetBinaryValueKind.SceneAsset;
    }

    // existing asset cases stay unchanged
}

static void WriteSceneAsset(EngineBinaryWriter writer, SceneAsset asset) {
    writer.WriteString(asset.Id);
    writer.WriteArray(asset.RootEntities, WriteSceneEntityAsset);
}

static SceneAsset ReadSceneAsset(EngineBinaryReader reader) {
    return new SceneAsset {
        Id = reader.ReadString(),
        RootEntities = reader.ReadArray(ReadSceneEntityAsset) ?? Array.Empty<SceneEntityAsset>()
    };
}
```

- [ ] **Step 4: Run the serializer test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter BinarySerializationTests`

Expected: PASS with the new scene-asset serializer coverage green alongside the existing binary-serialization tests.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/assets/raw/scene/SceneAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs engine/helengine.core/assets/raw/scene/SceneAssetReference.cs engine/helengine.core/assets/raw/scene/SceneAssetReferenceSourceKind.cs engine/helengine.core/Entity.cs engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "feat: add scene asset serialization"
```

## Task 2: Register `.helen` In Content And Browser Flows

**Files:**
- Modify: `engine/helengine.editor/content/EditorContentProcessorIds.cs`
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`

- [ ] **Step 1: Write the failing content and browser tests**

```csharp
[Fact]
public void ContentManager_SceneAsset_RoundTripsSerializedFile() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/BrowserTest.helen",
        RootEntities = Array.Empty<SceneEntityAsset>()
    };
    string scenePath = Path.Combine(TempRootPath, "BrowserTest.helen");
    ContentManager contentManager = new ContentManager(TempRootPath);
    EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);

    using (FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
        AssetSerializer.Serialize(stream, asset);
    }

    SceneAsset loaded = contentManager.Load<SceneAsset>(scenePath);

    Assert.Equal("Scenes/BrowserTest.helen", loaded.Id);
}
```

```csharp
[Fact]
public void LoadEntries_WhenHelenFileExists_ClassifiesEntryAsScene() {
    string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-browser-tests", Guid.NewGuid().ToString("N"));
    string scenesPath = Path.Combine(projectRootPath, "assets", "Scenes");
    Directory.CreateDirectory(scenesPath);

    using (FileStream stream = new FileStream(Path.Combine(scenesPath, "Sample.helen"), FileMode.Create, FileAccess.Write, FileShare.None)) {
        AssetSerializer.Serialize(stream, new SceneAsset {
            Id = "Scenes/Sample.helen",
            RootEntities = Array.Empty<SceneEntityAsset>()
        });
    }

    EditorAssetManager manager = new EditorAssetManager(projectRootPath);
    List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
    Assert.True(manager.TryNavigateTo("Scenes"));

    manager.LoadEntries(entries);

    AssetBrowserEntry entry = Assert.Single(entries);
    Assert.Equal(AssetEntryKind.Scene, entry.EntryKind);
}
```

```csharp
[Fact]
public void HandleAssetSelected_WhenEntryIsScene_ShowsSceneSummaryInsteadOfImportSettings() {
    EditorSession session = CreateSessionForGeneratedSelection();
    AssetBrowserEntry sceneEntry = AssetBrowserEntry.CreateFileSystemFile(
        "Sample.helen",
        "Scenes/Sample.helen",
        Path.Combine(TempRootPath, "Sample.helen"),
        SceneAsset.FileExtension,
        AssetEntryKind.Scene);

    InvokePrivate(session, "HandleAssetSelected", sceneEntry);

    PropertiesPanel panel = GetPrivateField<PropertiesPanel>(session, "propertiesPanel");
    TextComponent header = GetPrivateField<TextComponent>(panel, "headerText");
    TextComponent status = GetPrivateField<TextComponent>(panel, "statusText");

    Assert.Equal("Properties", header.Text);
    Assert.Equal("Kind: Scene", status.Text);
}
```

- [ ] **Step 2: Run the focused scene-browser tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "BinarySerializationTests|SceneAssetBrowserIntegrationTests|EditorSessionGeneratedAssetTests"`

Expected: build failure because there is no scene processor id, `.helen` is not classified as `AssetEntryKind.Scene`, and `HandleAssetSelected(...)` still falls through to import settings for scene files.

- [ ] **Step 3: Write the minimal implementation**

```csharp
public static class EditorContentProcessorIds {
    public const string SceneAsset = "editor.scene-asset";
}
```

```csharp
RegisterProcessorIfMissing(
    contentManager,
    EditorContentProcessorIds.SceneAsset,
    new AssetContentProcessor<SceneAsset>(),
    new[] { SceneAsset.FileExtension });
```

```csharp
public enum AssetEntryKind {
    Directory,
    Image,
    Model,
    Scene,
    Audio,
    Script,
    Config,
    Unknown,
    File
}
```

```csharp
if (sceneExtensions.Contains(extension)) {
    return AssetEntryKind.Scene;
}
```

```csharp
case AssetEntryKind.Scene:
    color = ThemeManager.Colors.AccentPrimary;
    label = "SCN";
    textColor = ThemeManager.Colors.TextOnAccent;
    return;
```

```csharp
public void ShowSceneAssetSummary(AssetBrowserEntry entry) {
    ApplyLines(new[] {
        "Properties",
        $"Asset: {BuildAssetLabel(entry)}",
        $"Path: {entry.RelativePath}",
        "Kind: Scene"
    });
    LayoutLines();
}
```

```csharp
if (entry.EntryKind == AssetEntryKind.Scene) {
    propertiesPanel.ShowSceneAssetSummary(entry);
    previewPanel.ClearPreview();
    return;
}
```

- [ ] **Step 4: Run the focused scene-browser tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "BinarySerializationTests|SceneAssetBrowserIntegrationTests|EditorSessionGeneratedAssetTests"`

Expected: PASS with scene-asset content loading, `.helen` browser classification, and scene-summary selection coverage green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/content/EditorContentProcessorIds.cs engine/helengine.editor/content/EditorContentManagerConfiguration.cs engine/helengine.editor/managers/asset/AssetEntryKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs
git commit -m "feat: register scene assets in browser"
```

## Task 3: Add The Hidden EntitySaveComponent And Hide It In The Properties UI

**Files:**
- Create: `engine/helengine.editor/components/persistence/IEditorHiddenComponent.cs`
- Create: `engine/helengine.editor/components/persistence/EntityComponentSaveState.cs`
- Create: `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
- Modify: `engine/helengine.editor/EditorEntity.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Create: `engine/helengine.editor.tests/EntitySaveComponentTests.cs`

- [ ] **Step 1: Write the failing hidden-component tests**

```csharp
[Fact]
public void EditorEntity_WhenConstructed_AttachesEntitySaveComponent() {
    EditorEntity entity = new EditorEntity();

    Assert.Contains(entity.Components, component => component is EntitySaveComponent);
}
```

```csharp
[Fact]
public void ShowComponents_WhenEntityHasEntitySaveComponent_HidesThePersistenceComponentHeader() {
    EditorEntity entity = new EditorEntity();
    entity.AddComponent(new MeshComponent());
    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));

    view.ShowComponents(entity);

    FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
    List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));

    Assert.DoesNotContain(rows, row => row.Kind == ComponentPropertyRowKind.Header && row.Label.Text == nameof(EntitySaveComponent));
    Assert.Contains(rows, row => row.Kind == ComponentPropertyRowKind.Header && row.Label.Text == nameof(MeshComponent));
}
```

- [ ] **Step 2: Run the hidden-component tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EntitySaveComponentTests`

Expected: build failure because `EntitySaveComponent`, `EntityComponentSaveState`, and `IEditorHiddenComponent` do not exist, and `EditorEntity` does not attach any persistence component yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Marks editor-only components that should be hidden from normal component editing UI.
    /// </summary>
    public interface IEditorHiddenComponent {
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Hidden editor-only component that stores per-component persistence metadata for an entity.
    /// </summary>
    public class EntitySaveComponent : Component, IEditorHiddenComponent {
        readonly Dictionary<Component, EntityComponentSaveState> ComponentStates;

        public EntitySaveComponent() {
            ComponentStates = new Dictionary<Component, EntityComponentSaveState>();
        }

        public void SetAssetReference(Component component, string referenceKey, SceneAssetReference reference) {
            // create-or-fetch state and store one stable reference
        }

        public bool TryGetAssetReference(Component component, string referenceKey, out SceneAssetReference reference) {
            // return stored reference when present
        }
    }
}
```

```csharp
public EditorEntity() {
    Name = "Entity";

    InitComponents();
    InitChildren();
    AddComponent(new EntitySaveComponent());
}
```

```csharp
if (component is IEditorHiddenComponent) {
    continue;
}
```

- [ ] **Step 4: Run the hidden-component tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EntitySaveComponentTests`

Expected: PASS with the default attachment and hidden UI behavior green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/persistence/IEditorHiddenComponent.cs engine/helengine.editor/components/persistence/EntityComponentSaveState.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/EditorEntity.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/EntitySaveComponentTests.cs
git commit -m "feat: add hidden entity save component"
```

## Task 4: Add The Reference Resolver, Persistence Registry, And MeshComponent Descriptor

**Files:**
- Create: `engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs`
- Create: `engine/helengine.editor/serialization/scene/SceneAssetReferenceResolver.cs`
- Create: `engine/helengine.editor/serialization/scene/IComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs`
- Create: `engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
- Create: `engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`

- [ ] **Step 1: Write the failing descriptor and registry tests**

```csharp
[Fact]
public void SerializeComponent_WhenDescriptorIsMissing_ThrowsClearError() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    EditorEntity entity = new EditorEntity();
    AnchorComponent component = new AnchorComponent();
    entity.AddComponent(component);
    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(entity.Components[0]);

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => registry.SerializeComponent(component, saveComponent, new TestSceneAssetReferenceResolver(), 0));

    Assert.Contains(nameof(AnchorComponent), ex.Message);
}
```

```csharp
[Fact]
public void SerializeAndDeserialize_WhenMeshHasGeneratedModelAndFileMaterial_RoundTripsPayload() {
    SceneAssetReference modelReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.Generated,
        ProviderId = "engine",
        AssetId = "engine:model:cube"
    };
    SceneAssetReference materialReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Materials/Test.helmat"
    };

    EditorEntity entity = new EditorEntity();
    MeshComponent mesh = new MeshComponent {
        Model = new TestRuntimeModel(),
        Material = new TestRuntimeMaterial(),
        RenderOrder3D = 9
    };
    entity.AddComponent(mesh);

    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(entity.Components[0]);
    saveComponent.SetAssetReference(mesh, MeshComponentPersistenceDescriptor.ModelReferenceKey, modelReference);
    saveComponent.SetAssetReference(mesh, MeshComponentPersistenceDescriptor.MaterialReferenceKey, materialReference);

    TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
    resolver.RegisterModel(modelReference, new TestRuntimeModel());
    resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());

    MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
    byte[] payload = descriptor.Serialize(mesh, saveComponent, resolver);
    EntitySaveComponent restoredSaveComponent = new EntitySaveComponent();
    MeshComponent restoredMesh = Assert.IsType<MeshComponent>(descriptor.Deserialize(payload, restoredSaveComponent, resolver));

    Assert.Equal(9, restoredMesh.RenderOrder3D);
    Assert.True(restoredSaveComponent.TryGetAssetReference(restoredMesh, MeshComponentPersistenceDescriptor.ModelReferenceKey, out SceneAssetReference restoredModelReference));
    Assert.Equal(modelReference.AssetId, restoredModelReference.AssetId);
    Assert.True(restoredSaveComponent.TryGetAssetReference(restoredMesh, MeshComponentPersistenceDescriptor.MaterialReferenceKey, out SceneAssetReference restoredMaterialReference));
    Assert.Equal(materialReference.RelativePath, restoredMaterialReference.RelativePath);
}
```

- [ ] **Step 2: Run the descriptor and registry tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPersistenceRegistryTests|MeshComponentPersistenceDescriptorTests"`

Expected: build failure because the resolver contract, registry, mesh descriptor, and test resolver do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Validates and resolves scene asset references for save and load flows.
    /// </summary>
    public interface ISceneAssetReferenceResolver {
        void ValidateModelReference(SceneAssetReference reference);
        void ValidateMaterialReference(SceneAssetReference reference);
        RuntimeModel ResolveModelReference(SceneAssetReference reference);
        RuntimeMaterial ResolveMaterialReference(SceneAssetReference reference);
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Describes how one component type persists itself inside a scene file.
    /// </summary>
    public interface IComponentPersistenceDescriptor {
        Type ComponentType { get; }
        string ComponentTypeId { get; }
        byte[] Serialize(Component component, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver);
        Component Deserialize(byte[] payload, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver);
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores persistence descriptors keyed by runtime type and serialized type id.
    /// </summary>
    public class ComponentPersistenceRegistry {
        readonly Dictionary<Type, IComponentPersistenceDescriptor> DescriptorsByType;
        readonly Dictionary<string, IComponentPersistenceDescriptor> DescriptorsById;

        public ComponentPersistenceRegistry() {
            DescriptorsByType = new Dictionary<Type, IComponentPersistenceDescriptor>();
            DescriptorsById = new Dictionary<string, IComponentPersistenceDescriptor>(StringComparer.Ordinal);
        }

        public void Register(IComponentPersistenceDescriptor descriptor) {
            DescriptorsByType[descriptor.ComponentType] = descriptor;
            DescriptorsById[descriptor.ComponentTypeId] = descriptor;
        }

        public SceneComponentAssetRecord SerializeComponent(Component component, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver, int componentIndex) {
            if (!DescriptorsByType.TryGetValue(component.GetType(), out IComponentPersistenceDescriptor descriptor)) {
                throw new InvalidOperationException($"No persistence descriptor is registered for component type '{component.GetType().Name}'.");
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = componentIndex,
                Payload = descriptor.Serialize(component, saveComponent, referenceResolver)
            };
        }

        public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            if (!DescriptorsById.TryGetValue(record.ComponentTypeId, out IComponentPersistenceDescriptor descriptor)) {
                throw new InvalidOperationException($"No persistence descriptor is registered for component type id '{record.ComponentTypeId}'.");
            }

            return descriptor.Deserialize(record.Payload, saveComponent, referenceResolver);
        }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Persists mesh component model/material references and render order.
    /// </summary>
    public class MeshComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
        public const string ComponentTypeIdValue = "helengine.core.MeshComponent";
        public const string ModelReferenceKey = "Model";
        public const string MaterialReferenceKey = "Material";

        public Type ComponentType => typeof(MeshComponent);
        public string ComponentTypeId => ComponentTypeIdValue;

        public byte[] Serialize(Component component, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            // write payload version, model reference, material reference, and render order
        }

        public Component Deserialize(byte[] payload, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
            // read payload, resolve runtime resources, create MeshComponent, and restore save metadata
        }
    }
}
```

```csharp
public override RuntimeMaterial BuildMaterialFromRaw(MaterialAsset materialAsset, ShaderAsset shaderAsset) {
    if (materialAsset == null) {
        throw new ArgumentNullException(nameof(materialAsset));
    }
    if (shaderAsset == null) {
        throw new ArgumentNullException(nameof(shaderAsset));
    }

    return new TestRuntimeMaterial();
}
```

- [ ] **Step 4: Run the descriptor and registry tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPersistenceRegistryTests|MeshComponentPersistenceDescriptorTests"`

Expected: PASS with registry failure behavior and `MeshComponent` payload round-trip green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/IComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs
git commit -m "feat: add component persistence registry"
```

## Task 5: Capture Stable Mesh References When The User Picks Assets

**Files:**
- Create: `engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Create: `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`

- [ ] **Step 1: Write the failing picker-persistence tests**

```csharp
[Fact]
public void HandleModelPicked_WhenEntryIsGenerated_StoresGeneratedSceneReference() {
    TestRuntimeModel runtimeModel = new TestRuntimeModel();
    GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
        "engine",
        new[] {
            AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
        },
        runtimeModel));

    MeshComponent meshComponent = new MeshComponent();
    EditorEntity entity = CreateEntityWithComponent(meshComponent);
    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);

    ComponentPropertyRow modelRow = FindModelRow(view);
    MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
    handleModelPicked.Invoke(view, new object[] {
        modelRow,
        AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
    });

    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(entity.Components[0]);
    Assert.True(saveComponent.TryGetAssetReference(meshComponent, MeshComponentPersistenceDescriptor.ModelReferenceKey, out SceneAssetReference reference));
    Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
    Assert.Equal("engine", reference.ProviderId);
    Assert.Equal(EngineGeneratedModelCache.CubeAssetId, reference.AssetId);
}
```

```csharp
[Fact]
public void HandleMaterialPicked_WhenEntryIsFileSystem_StoresFileSystemSceneReference() {
    string materialPath = Path.Combine(TempRootPath, "Materials", "Test.helmat");
    Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
    File.WriteAllBytes(materialPath, Array.Empty<byte>());

    MeshComponent meshComponent = new MeshComponent();
    EditorEntity entity = CreateEntityWithComponent(meshComponent);
    ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
    view.ShowComponents(entity);

    ComponentPropertyRow materialRow = FindMaterialRow(view);
    MethodInfo handleMaterialPicked = typeof(ComponentPropertiesView).GetMethod("HandleMaterialPicked", BindingFlags.Instance | BindingFlags.NonPublic);
    handleMaterialPicked.Invoke(view, new object[] {
        materialRow,
        AssetBrowserEntry.CreateFileSystemFile("Test.helmat", "Materials/Test.helmat", materialPath, ".helmat", AssetEntryKind.File)
    });

    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(entity.Components[0]);
    Assert.True(saveComponent.TryGetAssetReference(meshComponent, MeshComponentPersistenceDescriptor.MaterialReferenceKey, out SceneAssetReference reference));
    Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
    Assert.Equal("Materials/Test.helmat", reference.RelativePath);
}
```

- [ ] **Step 2: Run the picker-persistence tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPropertiesViewGeneratedAssetTests|ComponentPropertiesViewScenePersistenceTests"`

Expected: FAIL because picker handlers currently assign runtime assets but never update `EntitySaveComponent`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds stable scene asset references from browser selections.
    /// </summary>
    public static class SceneAssetReferenceFactory {
        public static SceneAssetReference Create(AssetBrowserEntry entry) {
            if (entry.IsGenerated) {
                return new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.Generated,
                    RelativePath = entry.RelativePath,
                    ProviderId = entry.ProviderId,
                    AssetId = entry.AssetId
                };
            }

            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = entry.RelativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }
    }
}
```

```csharp
void HandleMaterialPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
    if (row.TargetComponent == null || row.Property == null) {
        return;
    }

    try {
        RuntimeMaterial material = LoadMaterial(entry);
        row.Property.SetValue(row.TargetComponent, material);
        StoreAssetReference(row.TargetComponent, MeshComponentPersistenceDescriptor.MaterialReferenceKey, entry);
        UpdateMaterialRow(row);
    } catch (Exception ex) {
        Logger.WriteError($"Material pick failed: {ex.Message}");
    }
}

void HandleModelPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
    if (row.TargetComponent == null || row.Property == null) {
        return;
    }

    try {
        RuntimeModel model = LoadModel(entry);
        row.Property.SetValue(row.TargetComponent, model);
        StoreAssetReference(row.TargetComponent, MeshComponentPersistenceDescriptor.ModelReferenceKey, entry);
        if (entry != null) {
            ModelLabels[model] = entry.Name ?? string.Empty;
        }
        UpdateModelRow(row);
    } catch (Exception ex) {
        Logger.WriteError($"Model pick failed: {ex.Message}");
    }
}
```

```csharp
void StoreAssetReference(Component component, string referenceKey, AssetBrowserEntry entry) {
    if (component.Parent == null || component.Parent.Components == null) {
        throw new InvalidOperationException("Picked asset target is not attached to an entity.");
    }

    for (int i = 0; i < component.Parent.Components.Count; i++) {
        if (component.Parent.Components[i] is EntitySaveComponent saveComponent) {
            saveComponent.SetAssetReference(component, referenceKey, SceneAssetReferenceFactory.Create(entry));
            return;
        }
    }

    throw new InvalidOperationException("Entity save component is required to store scene persistence metadata.");
}
```

- [ ] **Step 4: Run the picker-persistence tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ComponentPropertiesViewGeneratedAssetTests|ComponentPropertiesViewScenePersistenceTests"`

Expected: PASS with generated-model and filesystem material/file model reference capture green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs
git commit -m "feat: capture mesh references for scene save"
```

## Task 6: Add Scene Save And Internal Load Services

**Files:**
- Create: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Create: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write the failing scene-save service tests**

```csharp
[Fact]
public void Save_WhenUserSceneContainsUnsupportedComponent_ThrowsClearError() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    registry.Register(new MeshComponentPersistenceDescriptor());
    TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
    SceneSaveService service = new SceneSaveService(registry, resolver);

    EditorEntity userEntity = new EditorEntity {
        Name = "Unsupported"
    };
    userEntity.AddComponent(new AnchorComponent());

    InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => service.BuildSceneAsset());

    Assert.Contains(nameof(AnchorComponent), ex.Message);
}
```

```csharp
[Fact]
public void SaveAndLoad_WhenSceneContainsInternalEntities_OnlyUserEntitiesRoundTrip() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    registry.Register(new MeshComponentPersistenceDescriptor());
    TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
    SceneSaveService saveService = new SceneSaveService(registry, resolver);
    SceneLoadService loadService = new SceneLoadService(registry, resolver);

    SceneAssetReference modelReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.Generated,
        ProviderId = "engine",
        AssetId = "engine:model:cube"
    };
    SceneAssetReference materialReference = new SceneAssetReference {
        SourceKind = SceneAssetReferenceSourceKind.FileSystem,
        RelativePath = "Materials/Test.helmat"
    };
    resolver.RegisterModel(modelReference, new TestRuntimeModel());
    resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());

    EditorEntity userRoot = new EditorEntity {
        Name = "Root"
    };
    userRoot.LocalPosition = new float3(4f, 5f, 6f);
    MeshComponent mesh = new MeshComponent {
        Model = new TestRuntimeModel(),
        Material = new TestRuntimeMaterial(),
        RenderOrder3D = 7
    };
    userRoot.AddComponent(mesh);
    EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(userRoot.Components[0]);
    saveComponent.SetAssetReference(mesh, MeshComponentPersistenceDescriptor.ModelReferenceKey, modelReference);
    saveComponent.SetAssetReference(mesh, MeshComponentPersistenceDescriptor.MaterialReferenceKey, materialReference);

    EditorEntity child = new EditorEntity {
        Name = "Child"
    };
    child.LocalPosition = new float3(1f, 2f, 3f);
    userRoot.AddChild(child);

    EditorEntity internalEntity = new EditorEntity {
        Name = "Internal",
        InternalEntity = true
    };

    SceneAsset sceneAsset = saveService.BuildSceneAsset();
    IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(sceneAsset);

    Assert.Single(sceneAsset.RootEntities);
    Assert.Single(loadedRoots);
    Assert.Equal("Root", loadedRoots[0].Name);
    Assert.Equal(new float3(4f, 5f, 6f), loadedRoots[0].LocalPosition);
    Assert.Single(loadedRoots[0].Children);
}
```

- [ ] **Step 2: Run the scene-save service tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter SceneSaveServiceTests`

Expected: build failure because `SceneSaveService` and `SceneLoadService` do not exist, and there is no user-scene extraction or descriptor-driven serialization path yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Builds scene assets from the current editor object graph and writes them to disk.
    /// </summary>
    public class SceneSaveService {
        readonly ComponentPersistenceRegistry Registry;
        readonly ISceneAssetReferenceResolver ReferenceResolver;

        public SceneSaveService(ComponentPersistenceRegistry registry, ISceneAssetReferenceResolver referenceResolver) {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        }

        public SceneAsset BuildSceneAsset() {
            List<SceneEntityAsset> rootEntities = new List<SceneEntityAsset>();
            List<Entity> entities = Core.Instance.ObjectManager.Entities;
            for (int i = 0; i < entities.Count; i++) {
                if (entities[i] is EditorEntity editorEntity &&
                    editorEntity.Parent == null &&
                    !editorEntity.InternalEntity) {
                    rootEntities.Add(BuildEntityAsset(editorEntity));
                }
            }

            return new SceneAsset {
                RootEntities = rootEntities.ToArray()
            };
        }

        public void Save(string fullPath) {
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            SceneAsset sceneAsset = BuildSceneAsset();
            sceneAsset.Id = Path.GetRelativePath(EditorProjectPaths.AssetsRoot, fullPath).Replace('\\', '/');
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }
    }
}
```

```csharp
SceneEntityAsset BuildEntityAsset(EditorEntity entity) {
    EntitySaveComponent saveComponent = FindSaveComponent(entity);
    List<SceneComponentAssetRecord> componentRecords = new List<SceneComponentAssetRecord>();
    int persistedComponentIndex = 0;
    for (int i = 0; i < entity.Components.Count; i++) {
        Component component = entity.Components[i];
        if (component is IEditorHiddenComponent) {
            continue;
        }

        componentRecords.Add(Registry.SerializeComponent(component, saveComponent, ReferenceResolver, persistedComponentIndex));
        persistedComponentIndex++;
    }

    List<SceneEntityAsset> children = new List<SceneEntityAsset>();
    for (int i = 0; i < entity.Children.Count; i++) {
        if (entity.Children[i] is EditorEntity childEditorEntity && !childEditorEntity.InternalEntity) {
            children.Add(BuildEntityAsset(childEditorEntity));
        }
    }

    return new SceneEntityAsset {
        Name = entity.Name ?? string.Empty,
        LocalPosition = entity.LocalPosition,
        LocalScale = entity.LocalScale,
        LocalOrientation = entity.LocalOrientation,
        Components = componentRecords.ToArray(),
        Children = children.ToArray()
    };
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Reconstructs editor entities from scene assets for internal round-trip verification.
    /// </summary>
    public class SceneLoadService {
        readonly ComponentPersistenceRegistry Registry;
        readonly ISceneAssetReferenceResolver ReferenceResolver;

        public SceneLoadService(ComponentPersistenceRegistry registry, ISceneAssetReferenceResolver referenceResolver) {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
        }

        public IReadOnlyList<EditorEntity> Load(SceneAsset asset) {
            List<EditorEntity> roots = new List<EditorEntity>();
            for (int i = 0; i < asset.RootEntities.Length; i++) {
                roots.Add(CreateEntity(asset.RootEntities[i]));
            }

            return roots;
        }
    }
}
```

- [ ] **Step 4: Run the scene-save service tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter SceneSaveServiceTests`

Expected: PASS with user-scene extraction, internal-entity exclusion, unsupported-component failures, and internal round-trip reconstruction green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "feat: add scene save services"
```

## Task 7: Add The Scene Save Path Resolver And SaveFileDialog

**Files:**
- Create: `engine/helengine.editor/serialization/scene/SceneSavePathResolver.cs`
- Create: `engine/helengine.editor/components/ui/asset/SaveFileDialog.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/SceneSavePathResolverTests.cs`
- Create: `engine/helengine.editor.tests/SaveFileDialogTests.cs`

- [ ] **Step 1: Write the failing save-dialog tests**

```csharp
[Fact]
public void LoadEntries_WhenGeneratedEntriesAreDisabled_DoesNotAppendGeneratedRoots() {
    GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
        "engine",
        new[] {
            AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine")
        },
        new TestRuntimeModel()));

    AssetBrowserDataSource dataSource = new AssetBrowserDataSource(ProjectRootPath, false);
    List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();

    dataSource.LoadEntries(entries);

    Assert.DoesNotContain(entries, entry => entry.IsGenerated);
}
```

```csharp
[Fact]
public void BuildFullPath_WhenNameOmitsExtension_AppendsHelenExtension() {
    SceneSavePathResolver resolver = new SceneSavePathResolver(ProjectRootPath);
    string scenesDirectory = Path.Combine(ProjectRootPath, "assets", "Scenes");
    Directory.CreateDirectory(scenesDirectory);

    string fullPath = resolver.BuildFullPath(scenesDirectory, "Prototype");

    Assert.EndsWith(Path.Combine("assets", "Scenes", "Prototype.helen"), fullPath);
}

[Fact]
public void BuildFullPath_WhenNameContainsInvalidCharacters_Throws() {
    SceneSavePathResolver resolver = new SceneSavePathResolver(ProjectRootPath);
    string scenesDirectory = Path.Combine(ProjectRootPath, "assets", "Scenes");
    Directory.CreateDirectory(scenesDirectory);

    Assert.Throws<InvalidOperationException>(() => resolver.BuildFullPath(scenesDirectory, "bad:name"));
}
```

```csharp
[Fact]
public void HandleSaveClicked_WhenNameIsValid_RaisesResolvedScenePath() {
    SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);
    string raisedPath = string.Empty;
    dialog.SaveRequested += path => raisedPath = path;
    dialog.Show("Scenes", "Prototype");
    dialog.UpdateLayout(1280, 720);

    SetFileName(dialog, "Prototype");
    InvokePrivate(dialog, "HandleSaveClicked");

    Assert.EndsWith(Path.Combine("assets", "Scenes", "Prototype.helen"), raisedPath);
}
```

- [ ] **Step 2: Run the save-dialog tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "AssetBrowserDataSourceTests|SceneSavePathResolverTests|SaveFileDialogTests"`

Expected: build failure because the browser data source cannot disable generated roots, there is no scene path resolver, and the save dialog does not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Resolves scene save locations inside the project assets folder.
    /// </summary>
    public class SceneSavePathResolver {
        public const string DefaultSceneDirectory = "Scenes";

        readonly string ProjectRootPath;

        public SceneSavePathResolver(string projectRootPath) {
            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public string GetInitialRelativeDirectory(string currentScenePath) {
            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                return DefaultSceneDirectory;
            }

            string assetsRootPath = Path.Combine(ProjectRootPath, "assets");
            string relativePath = Path.GetRelativePath(assetsRootPath, currentScenePath).Replace('\\', '/');
            string relativeDirectory = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');
            return string.IsNullOrWhiteSpace(relativeDirectory) ? DefaultSceneDirectory : relativeDirectory;
        }

        public string GetSuggestedFileName(string currentScenePath) {
            return string.IsNullOrWhiteSpace(currentScenePath) ? "NewScene" : Path.GetFileNameWithoutExtension(currentScenePath);
        }

        public string BuildFullPath(string currentDirectoryPath, string fileName) {
            if (string.IsNullOrWhiteSpace(currentDirectoryPath)) {
                throw new InvalidOperationException("A writable assets directory must be selected before saving.");
            }
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new InvalidOperationException("File name is required.");
            }

            string trimmedFileName = fileName.Trim();
            if (!trimmedFileName.EndsWith(SceneAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                trimmedFileName += SceneAsset.FileExtension;
            }

            string fullPath = Path.GetFullPath(Path.Combine(currentDirectoryPath, trimmedFileName));
            string assetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
            if (!fullPath.StartsWith(assetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Scene files must be saved inside the project assets folder.");
            }

            return fullPath;
        }
    }
}
```

```csharp
public AssetBrowserDataSource(string projectPath, bool includeGeneratedEntries = true) {
    FileSystemAssets = new EditorAssetManager(projectPath);
    DirectorySources = new Dictionary<string, AssetBrowserEntrySourceKind>(StringComparer.Ordinal);
    CurrentRelativePathValue = string.Empty;
    CurrentDirectoryIsGenerated = false;
    IncludeGeneratedEntries = includeGeneratedEntries;
}

public void LoadEntries(List<AssetBrowserEntry> entries) {
    entries.Clear();
    if (CurrentDirectoryIsGenerated) {
        if (IncludeGeneratedEntries) {
            GeneratedAssetProviderRegistry.LoadEntries(CurrentRelativePathValue, entries);
        }
    } else {
        FileSystemAssets.TryNavigateTo(CurrentRelativePathValue);
        FileSystemAssets.LoadEntries(entries);
        if (IncludeGeneratedEntries && string.IsNullOrWhiteSpace(CurrentRelativePathValue)) {
            GeneratedAssetProviderRegistry.LoadEntries(string.Empty, entries);
        }
    }

    entries.Sort(CompareEntries);
}
```

```csharp
public AssetBrowserView(
    FontAsset font,
    string projectPath,
    ushort layerMask,
    byte toolbarOrder,
    byte rowBackgroundOrder,
    byte iconBackgroundOrder,
    byte textOrder,
    bool includeGeneratedEntries = true) {
    DataSource = new AssetBrowserDataSource(projectPath, includeGeneratedEntries);
}

public bool TryNavigateTo(string relativePath) {
    bool navigated = DataSource.TryNavigateTo(relativePath);
    if (navigated) {
        RefreshEntries();
    }

    return navigated;
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Filesystem-only modal dialog used to save scene files inside the project assets folder.
    /// </summary>
    public class SaveFileDialog : EditorEntity {
        public event Action<string> SaveRequested;

        public void Show(string initialRelativeDirectory, string suggestedFileName) {
            if (!BrowserView.TryNavigateTo(initialRelativeDirectory)) {
                BrowserView.TryNavigateTo(SceneSavePathResolver.DefaultSceneDirectory);
            }

            FileNameField.Text = suggestedFileName ?? string.Empty;
            StatusText.Text = string.Empty;
            Enabled = true;
            BrowserView.RefreshEntries();
        }

        public void ShowError(string message) {
            StatusText.Text = message;
        }

        void HandleSaveClicked() {
            try {
                string fullPath = PathResolver.BuildFullPath(BrowserView.CurrentDirectoryPath, FileNameField.Text);
                StatusText.Text = string.Empty;
                SaveRequested?.Invoke(fullPath);
            } catch (Exception ex) {
                StatusText.Text = ex.Message;
            }
        }
    }
}
```

- [ ] **Step 4: Run the save-dialog tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "AssetBrowserDataSourceTests|SceneSavePathResolverTests|SaveFileDialogTests"`

Expected: PASS with filesystem-only browser mode, `.helen` path validation, and save-dialog request wiring green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/SceneSavePathResolver.cs engine/helengine.editor/components/ui/asset/SaveFileDialog.cs engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSavePathResolverTests.cs engine/helengine.editor.tests/SaveFileDialogTests.cs
git commit -m "feat: add scene save dialog"
```

## Task 8: Wire Save Map And Save Map As In EditorSession

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] **Step 1: Write the failing editor-session save tests**

```csharp
[Fact]
public void HandleSaveMapRequested_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog() {
    EditorSession session = CreateSessionForSceneSave();

    InvokePrivate(session, "HandleSaveMapRequested");

    SaveFileDialog saveFileDialog = GetPrivateField<SaveFileDialog>(session, "saveFileDialog");
    Assert.True(saveFileDialog.IsVisible);
}
```

```csharp
[Fact]
public void HandleSceneSaveRequested_WhenSaveSucceeds_UpdatesCurrentScenePathAndWritesFile() {
    EditorSession session = CreateSessionForSceneSave();
    string expectedPath = Path.Combine(TempRootPath, "assets", "Scenes", "Saved.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));

    InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

    string currentScenePath = GetPrivateField<string>(session, "CurrentScenePath");
    Assert.Equal(expectedPath, currentScenePath);
    Assert.True(File.Exists(expectedPath));
}
```

- [ ] **Step 2: Run the editor-session save tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionSceneSaveTests`

Expected: build failure because `EditorSession` does not own a save dialog or scene-save service, does not subscribe to title-bar save events, and does not track a current scene path.

- [ ] **Step 3: Write the minimal implementation**

```csharp
readonly SceneSavePathResolver SceneSavePathResolver;
readonly SceneSaveService SceneSaveService;
readonly SaveFileDialog saveFileDialog;
string CurrentScenePath;
```

```csharp
ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
persistenceRegistry.Register(new MeshComponentPersistenceDescriptor());
SceneSavePathResolver = new SceneSavePathResolver(this.projectPath);
SceneSaveService = new SceneSaveService(persistenceRegistry, new SceneAssetReferenceResolver(EditorContentManager));
saveFileDialog = new SaveFileDialog(uiFont, this.projectPath);
titleBar.SaveMapRequested += HandleSaveMapRequested;
titleBar.SaveMapAsRequested += HandleSaveMapAsRequested;
saveFileDialog.SaveRequested += HandleSceneSaveRequested;
```

```csharp
public void UpdateLayout(int renderWidth, int renderHeight) {
    // existing layout logic stays unchanged
    saveFileDialog.UpdateLayout(width, height);
}
```

```csharp
void HandleSaveMapRequested() {
    if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
        ShowSceneSaveDialog();
        return;
    }

    HandleSceneSaveRequested(CurrentScenePath);
}

void HandleSaveMapAsRequested() {
    ShowSceneSaveDialog();
}

void ShowSceneSaveDialog() {
    string initialRelativeDirectory = SceneSavePathResolver.GetInitialRelativeDirectory(CurrentScenePath);
    string suggestedFileName = SceneSavePathResolver.GetSuggestedFileName(CurrentScenePath);
    saveFileDialog.Show(initialRelativeDirectory, suggestedFileName);
}

void HandleSceneSaveRequested(string fullPath) {
    try {
        SceneSaveService.Save(fullPath);
        CurrentScenePath = fullPath;
        assetBrowserPanel.RefreshEntries();
        saveFileDialog.Hide();
    } catch (Exception ex) {
        Logger.WriteError($"Scene save failed: {ex.Message}");
        saveFileDialog.ShowError(ex.Message);
    }
}
```

```csharp
public void Dispose() {
    titleBar.SaveMapRequested -= HandleSaveMapRequested;
    titleBar.SaveMapAsRequested -= HandleSaveMapAsRequested;
    saveFileDialog.SaveRequested -= HandleSceneSaveRequested;
    saveFileDialog.Hide();

    // existing dispose logic stays unchanged
}
```

- [ ] **Step 4: Run the editor-session save tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionSceneSaveTests`

Expected: PASS with `Save Map` fallback, save-path tracking, and actual `.helen` file creation green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs
git commit -m "feat: wire editor scene save commands"
```

## Task 9: Full Verification

**Files:**
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Test: `engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs`
- Test: `engine/helengine.editor.tests/EntitySaveComponentTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSavePathResolverTests.cs`
- Test: `engine/helengine.editor.tests/SaveFileDialogTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] **Step 1: Run the focused scene-saving test set**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "BinarySerializationTests|SceneAssetBrowserIntegrationTests|EntitySaveComponentTests|ComponentPersistenceRegistryTests|MeshComponentPersistenceDescriptorTests|ComponentPropertiesViewGeneratedAssetTests|ComponentPropertiesViewScenePersistenceTests|SceneSaveServiceTests|AssetBrowserDataSourceTests|SceneSavePathResolverTests|SaveFileDialogTests|EditorSessionGeneratedAssetTests|EditorSessionSceneSaveTests"`

Expected: PASS with all new scene-saving and `.helen` browser tests green.

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS with the full editor suite green and no regressions introduced by scene saving.

- [ ] **Step 3: Commit the verification-complete scene saving implementation**

```bash
git add engine/helengine.core/assets/raw/scene/SceneAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs engine/helengine.core/assets/raw/scene/SceneComponentAssetRecord.cs engine/helengine.core/assets/raw/scene/SceneAssetReference.cs engine/helengine.core/assets/raw/scene/SceneAssetReferenceSourceKind.cs engine/helengine.core/Entity.cs engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor/components/persistence/IEditorHiddenComponent.cs engine/helengine.editor/components/persistence/EntityComponentSaveState.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/serialization/scene/ISceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceFactory.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/IComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/ComponentPersistenceRegistry.cs engine/helengine.editor/serialization/scene/MeshComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor/serialization/scene/SceneSavePathResolver.cs engine/helengine.editor/content/EditorContentProcessorIds.cs engine/helengine.editor/content/EditorContentManagerConfiguration.cs engine/helengine.editor/managers/asset/AssetEntryKind.cs engine/helengine.editor/managers/asset/EditorAssetManager.cs engine/helengine.editor/managers/asset/AssetBrowserDataSource.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/asset/SaveFileDialog.cs engine/helengine.editor/EditorEntity.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/managers/asset/SceneAssetBrowserIntegrationTests.cs engine/helengine.editor.tests/EntitySaveComponentTests.cs engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs engine/helengine.editor.tests/serialization/scene/MeshComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/ComponentPropertiesViewGeneratedAssetTests.cs engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/managers/asset/AssetBrowserDataSourceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSavePathResolverTests.cs engine/helengine.editor.tests/SaveFileDialogTests.cs engine/helengine.editor.tests/EditorSessionGeneratedAssetTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/testing/TestSceneAssetReferenceResolver.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs
git commit -m "feat: add scene saving"
```

## Self-Review

### Spec Coverage

- `SceneAsset` in the HELE pipeline: covered by Task 1.
- `.helen` processor registration and browser classification: covered by Task 2.
- Hidden `EntitySaveComponent` attached to editor entities and hidden from the UI: covered by Task 3.
- Explicit descriptor registry and fail-fast behavior for unsupported components: covered by Task 4.
- `MeshComponent` persistence using stable filesystem/generated references: covered by Tasks 4 and 5.
- Saving only user-authored entities and excluding internal/editor infrastructure: covered by Task 6.
- Editor-owned save dialog rooted in `assets` with `.helen` enforcement: covered by Task 7.
- `Save Map` and `Save Map As...` routing with current scene path tracking: covered by Task 8.
- Required verification matrix and full-suite regression pass: covered by Task 9.

### Placeholder Scan

- No `TODO`, `TBD`, or `implement later` placeholders remain.
- Every task lists exact files, commands, and expected outcomes.
- Every code-writing step includes concrete C# snippets rather than abstract instructions.

### Type Consistency

- `SceneAsset`, `SceneEntityAsset`, `SceneComponentAssetRecord`, `SceneAssetReference`, and `SceneAssetReferenceSourceKind` are used consistently across serialization, descriptors, and scene services.
- `EntitySaveComponent`, `EntityComponentSaveState`, `IEditorHiddenComponent`, `IComponentPersistenceDescriptor`, `ComponentPersistenceRegistry`, and `MeshComponentPersistenceDescriptor` keep the same names across all tasks.
- `SceneSaveService`, `SceneLoadService`, `SceneSavePathResolver`, and `SaveFileDialog` are named consistently wherever the plan references them.

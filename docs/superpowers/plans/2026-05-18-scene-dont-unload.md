# Scene Dont Unload Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a scene-level `DontUnload` setting that preserves authored persistent scenes across `SceneLoadMode.Single` transitions while still allowing explicit `UnloadScene(sceneId)` calls to remove them.

**Architecture:** Persist a new `DontUnload` boolean on `SceneSettingsAsset`, thread it through scene serialization and editor scene settings UI, and capture it into `LoadedSceneRecord` so `SceneManager` can filter unload behavior during single-scene transitions. The runtime keeps scene ownership intact and changes only the unload policy for implicit scene replacement.

**Tech Stack:** C#, xUnit, existing `SceneManager` runtime flow, editor scene serialization, editor modal UI

---

## File Structure

### Core runtime and asset model

- Modify: `engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs`
  Add the persisted `DontUnload` boolean to the scene settings payload.
- Modify: `engine/helengine.core/scene/runtime/LoadedSceneRecord.cs`
  Track the resolved persistence flag on loaded scene records.
- Modify: `engine/helengine.core/scene/runtime/SceneManager.cs`
  Preserve persistent scenes during `SceneLoadMode.Single` and continue honoring explicit unload calls.
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
  Read the new scene-settings field from newer payload versions while defaulting legacy payloads to `false`.

### Editor serializer and scene save/load

- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
  Increment the scene-asset binary version and write/read the `DontUnload` flag.
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
  Clone `DontUnload` alongside the existing canvas profile.

### Editor UI and session state

- Modify: `engine/helengine.editor/components/ui/SceneSettingsDialog.cs`
  Add the `Dont Unload` checkbox and include it in dialog round-trip state.
- Modify: `engine/helengine.editor/EditorSession.cs`
  Treat `DontUnload` as part of scene-settings equality and dirty-state behavior.

### Tests

- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`
  Cover serializer round-trip and legacy defaults.
- Modify: `engine/helengine.editor.tests/SceneSettingsDialogTests.cs`
  Cover dialog show/build behavior for the new checkbox.
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
  Cover dirty-state and save persistence for `DontUnload`.
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
  Cover open-time restoration of `DontUnload`.
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
  Cover raw scene-save persistence of `DontUnload`.
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
  Cover raw scene-load restoration of `DontUnload`.
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`
  Cover preserved-scene, explicit-unload, and duplicate-load runtime behavior.

### Validation commands

- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~BinarySerializationTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSettingsDialogTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneManagerTests"`

### Task 1: Add the Scene Settings Payload Flag

**Files:**
- Modify: `engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serializer tests**

Add these tests to `engine/helengine.editor.tests/BinarySerializationTests.cs` near the existing scene-settings coverage:

```csharp
[Fact]
public void AssetSerializer_SceneAsset_WhenDontUnloadIsTrue_RoundTripsSceneSettingsFlag() {
    SceneAsset asset = new SceneAsset {
        Id = "Scenes/Persistent.helen",
        SceneSettings = new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile {
                Width = 1920,
                Height = 1080
            },
            DontUnload = true
        },
        RootEntities = Array.Empty<SceneEntityAsset>()
    };

    byte[] data = AssetSerializer.SerializeToBytes(asset);
    SceneAsset deserialized = Assert.IsType<SceneAsset>(AssetSerializer.DeserializeFromBytes(data));

    Assert.True(deserialized.SceneSettings.DontUnload);
}

[Fact]
public void DeserializeSceneAsset_WhenPayloadVersionIsFourteen_DefaultsDontUnloadToFalse() {
    using MemoryStream stream = new MemoryStream();
    global::helengine.files.EngineBinaryHeader header = new global::helengine.files.EngineBinaryHeader(
        global::helengine.files.EngineBinaryEndianness.LittleEndian,
        14,
        global::helengine.files.EditorAssetBinarySerializer.FormatId,
        (ushort)global::helengine.files.EditorAssetBinarySerializer.RecordKind,
        (ushort)global::helengine.files.EditorAssetBinaryValueKind.SceneAsset);
    global::helengine.files.EngineBinaryHeaderSerializer.Write(stream, header);
    using (global::helengine.files.EngineBinaryWriter writer = global::helengine.files.EngineBinaryWriter.Create(stream, global::helengine.files.EngineBinaryEndianness.LittleEndian, true)) {
        writer.WriteString("scene-id");
        writer.WriteArray(Array.Empty<SceneEntityAsset>(), static (arrayWriter, entity) => throw new InvalidOperationException($"Unexpected entity payload write for '{entity.Id}'"));
        writer.WriteArray(Array.Empty<SceneAssetReference>(), static (arrayWriter, reference) => throw new InvalidOperationException($"Unexpected reference payload write for '{reference.AssetId}'"));
        writer.WriteUInt32(0u);
        writer.WriteInt32(1280);
        writer.WriteInt32(720);
    }

    stream.Position = 0;

    SceneAsset deserialized = Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
    Assert.False(deserialized.SceneSettings.DontUnload);
}
```

- [ ] **Step 2: Run the serializer tests to verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~BinarySerializationTests"
```

Expected: FAIL because `SceneSettingsAsset` has no `DontUnload` property and the serializers do not persist or default it yet.

- [ ] **Step 3: Add the payload field and serializer support**

Update `engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores scene-level authoring settings that apply to the entire scene asset.
    /// </summary>
    public class SceneSettingsAsset {
        /// <summary>
        /// Gets or sets the authored canvas profile used to evaluate 2D layout and previews for the scene.
        /// </summary>
        public SceneCanvasProfile CanvasProfile { get; set; } = new SceneCanvasProfile();

        /// <summary>
        /// Gets or sets whether the scene remains loaded during normal single-scene transitions.
        /// </summary>
        public bool DontUnload { get; set; }
    }
}
```

Update `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`:

```csharp
public const byte CurrentVersion = 15;
```

```csharp
static void WriteSceneSettingsAsset(EngineBinaryWriter writer, SceneSettingsAsset sceneSettings) {
    writer.WriteInt32(sceneSettings.CanvasProfile.Width);
    writer.WriteInt32(sceneSettings.CanvasProfile.Height);
    writer.WriteBoolean(sceneSettings.DontUnload);
}

static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader, byte version) {
    SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
        CanvasProfile = ReadSceneCanvasProfile(reader)
    };
    sceneSettings.DontUnload = version >= 15 && reader.ReadBoolean();
    return sceneSettings;
}
```

Update the call sites in the same file:

```csharp
asset.SceneSettings = version >= 6
    ? ReadSceneSettingsAsset(reader, version)
    : new SceneSettingsAsset();
```

Update `engine/helengine.core/assets/EditorAssetBinarySerializer.cs` to mirror the read-side version gating:

```csharp
public const byte CurrentVersion = 15;
```

```csharp
asset.SceneSettings = version >= 6
    ? ReadSceneSettingsAsset(reader, version)
    : new SceneSettingsAsset();
```

```csharp
static SceneSettingsAsset ReadSceneSettingsAsset(EngineBinaryReader reader, byte version) {
    SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
        CanvasProfile = ReadSceneCanvasProfile(reader)
    };
    sceneSettings.DontUnload = version >= 15 && reader.ReadBoolean();
    return sceneSettings;
}
```

- [ ] **Step 4: Run the serializer tests to verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~BinarySerializationTests"
```

Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.core/assets/raw/scene/SceneSettingsAsset.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "Add scene dont-unload serialization support"
```

### Task 2: Add Editor Scene Settings UI and Dirty-State Support

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneSettingsDialog.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/SceneSettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] **Step 1: Write the failing dialog and session tests**

Add this test to `engine/helengine.editor.tests/SceneSettingsDialogTests.cs`:

```csharp
[Fact]
public void Show_WhenSceneSettingsDontUnloadIsTrue_PopulatesTheDontUnloadCheckbox() {
    InitializeCore();
    SceneSettingsDialog dialog = new SceneSettingsDialog(CreateFont(), EditorUiMetrics.Default);
    SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = 1280,
            Height = 720
        },
        DontUnload = true
    };

    dialog.Show(sceneSettings);

    CheckboxComponent dontUnloadCheckbox = GetNonPublicField<CheckboxComponent>(dialog, "DontUnloadCheckbox");
    Assert.True(dontUnloadCheckbox.IsChecked);
}
```

Add these tests to `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`:

```csharp
[Fact]
public void HandleSceneSettingsDialogConfirmed_WhenDontUnloadChanges_UpdatesStateAndMarksSceneDirty() {
    EditorSession session = CreateSessionForSceneSave();
    SceneSettingsAsset updatedSettings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = SceneCanvasProfile.DefaultWidth,
            Height = SceneCanvasProfile.DefaultHeight
        },
        DontUnload = true
    };
    Action handleSceneMutated = () => InvokePrivate(session, "HandleSceneMutated");

    try {
        EditorSceneMutationService.SceneMutated += handleSceneMutated;
        InvokePrivate(session, "HandleSceneSettingsDialogConfirmed", updatedSettings);
    } finally {
        EditorSceneMutationService.SceneMutated -= handleSceneMutated;
    }

    SceneSettingsAsset currentSceneSettings = GetPrivateField<SceneSettingsAsset>(session, "CurrentSceneSettings");
    bool isSceneDirty = GetPrivateField<bool>(session, "IsSceneDirty");
    Assert.True(currentSceneSettings.DontUnload);
    Assert.True(isSceneDirty);
}

[Fact]
public void HandleSceneSaveRequested_WhenDontUnloadIsEnabled_PersistsSceneSettingsFlag() {
    EditorSession session = CreateSessionForSceneSave();
    string expectedPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Persistent.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
    SetPrivateField(session, "CurrentSceneSettings", new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = 1600,
            Height = 900
        },
        DontUnload = true
    });

    InvokePrivate(session, "HandleSceneSaveRequested", expectedPath);

    using FileStream stream = File.OpenRead(expectedPath);
    SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    Assert.True(sceneAsset.SceneSettings.DontUnload);
}
```

- [ ] **Step 2: Run the editor UI tests to verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSettingsDialogTests|FullyQualifiedName~EditorSessionSceneSaveTests"
```

Expected: FAIL because the dialog does not expose a checkbox and editor-session equality/save logic ignores the new field.

- [ ] **Step 3: Implement the checkbox and dirty-state behavior**

Update `engine/helengine.editor/components/ui/SceneSettingsDialog.cs` with new members:

```csharp
readonly EditorEntity DontUnloadLabelHost;
readonly TextComponent DontUnloadLabel;
readonly EditorEntity DontUnloadCheckboxHost;
readonly CheckboxComponent DontUnloadCheckbox;
```

Construct and show the controls:

```csharp
DontUnloadLabelHost = CreateDialogHost();
DialogPanelRoot.AddChild(DontUnloadLabelHost);
DontUnloadLabel = CreateDialogLabel("Dont Unload");
DontUnloadLabelHost.AddComponent(DontUnloadLabel);

DontUnloadCheckboxHost = CreateDialogHost();
DialogPanelRoot.AddChild(DontUnloadCheckboxHost);
DontUnloadCheckbox = new CheckboxComponent(GetCheckboxSize(), HandleDontUnloadChanged);
DontUnloadCheckboxHost.AddComponent(DontUnloadCheckbox);
```

```csharp
public void Show(SceneSettingsAsset sceneSettings) {
    // existing validation
    CanvasWidthField.Text = sceneSettings.CanvasProfile.Width.ToString(System.Globalization.CultureInfo.InvariantCulture);
    CanvasHeightField.Text = sceneSettings.CanvasProfile.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
    DontUnloadCheckbox.IsChecked = sceneSettings.DontUnload;
    ResetDialogPositioning();
    Enabled = true;
    ShowDialogImmediately();
}
```

```csharp
SceneSettingsAsset BuildSceneSettingsFromFields() {
    // existing width/height parsing
    return new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = canvasWidth,
            Height = canvasHeight
        },
        DontUnload = DontUnloadCheckbox.IsChecked
    };
}
```

Adjust layout in the same file so the label and checkbox sit between the height field and status/footer.

Update `engine/helengine.editor/EditorSession.cs`:

```csharp
static bool AreSceneSettingsEquivalent(SceneSettingsAsset left, SceneSettingsAsset right) {
    if (left == null) {
        throw new InvalidOperationException("Left scene settings are required.");
    }
    if (right == null) {
        throw new InvalidOperationException("Right scene settings are required.");
    }
    if (left.CanvasProfile == null) {
        throw new InvalidOperationException("Left scene settings must include a canvas profile.");
    }
    if (right.CanvasProfile == null) {
        throw new InvalidOperationException("Right scene settings must include a canvas profile.");
    }

    return left.CanvasProfile.Width == right.CanvasProfile.Width
        && left.CanvasProfile.Height == right.CanvasProfile.Height
        && left.DontUnload == right.DontUnload;
}
```

- [ ] **Step 4: Run the editor UI tests to verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSettingsDialogTests|FullyQualifiedName~EditorSessionSceneSaveTests"
```

Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor/components/ui/SceneSettingsDialog.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/SceneSettingsDialogTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs
git commit -m "Add dont-unload scene settings UI"
```

### Task 3: Thread DontUnload Through Editor Save/Open Services

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`

- [ ] **Step 1: Write the failing save/load tests**

Add this test to `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`:

```csharp
[Fact]
public void Save_WhenSceneSettingsDontUnloadIsTrue_PersistsTheFlag() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
    SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
    SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = 1920,
            Height = 1080
        },
        DontUnload = true
    };
    string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Persistent.helen");

    saveService.Save(scenePath, sceneSettings);

    using FileStream stream = File.OpenRead(scenePath);
    SceneAsset asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    Assert.True(asset.SceneSettings.DontUnload);
}
```

Add this test to `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`:

```csharp
[Fact]
public void Load_WhenSceneFileContainsDontUnload_ReturnsSceneSettingsFlag() {
    SceneAssetReference modelReference = CreateGeneratedModelReference();
    SceneAssetReference materialReference = CreateGeneratedMaterialReference();
    SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = 1600,
            Height = 900
        },
        DontUnload = true
    };
    string scenePath = SaveSceneAsset("Persistent.helen", "Loaded Cube", modelReference, materialReference, sceneSettings);
    SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

    LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

    Assert.True(loaded.SceneSettings.DontUnload);
}
```

Add this test to `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`:

```csharp
[Fact]
public void OpenScene_WhenSceneSettingsDontUnloadIsTrue_RestoresCurrentSceneSettingsFlag() {
    SceneAssetReference modelReference = CreateGeneratedModelReference();
    SceneAssetReference materialReference = CreateMaterialReference();
    string scenePath = SaveSceneAsset(
        "Persistent.helen",
        "Persistent Root",
        modelReference,
        materialReference,
        new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile {
                Width = 1920,
                Height = 1080
            },
            DontUnload = true
        });
    EditorSession session = CreateSessionForSceneOpen(modelReference, materialReference);

    InvokePrivate(session, "HandleSceneFileSelected", scenePath);

    SceneSettingsAsset currentSceneSettings = GetPrivateField<SceneSettingsAsset>(session, "CurrentSceneSettings");
    Assert.True(currentSceneSettings.DontUnload);
}
```

- [ ] **Step 2: Run the save/open tests to verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~EditorSessionSceneOpenTests"
```

Expected: FAIL because `SceneSaveService.CloneSceneSettings(...)` only copies the canvas profile today.

- [ ] **Step 3: Implement the scene-save clone update**

Update `engine/helengine.editor/serialization/scene/SceneSaveService.cs`:

```csharp
static SceneSettingsAsset CloneSceneSettings(SceneSettingsAsset sceneSettings) {
    if (sceneSettings == null) {
        throw new ArgumentNullException(nameof(sceneSettings));
    }
    if (sceneSettings.CanvasProfile == null) {
        throw new InvalidOperationException("Scene settings must include a canvas profile.");
    }

    return new SceneSettingsAsset {
        CanvasProfile = new SceneCanvasProfile {
            Width = sceneSettings.CanvasProfile.Width,
            Height = sceneSettings.CanvasProfile.Height
        },
        DontUnload = sceneSettings.DontUnload
    };
}
```

No new editor load code should be necessary beyond serializer support, because `LoadedEditorSceneDocument.SceneSettings` already flows through the existing open path.

- [ ] **Step 4: Run the save/open tests to verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~EditorSessionSceneOpenTests"
```

Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs
git commit -m "Thread dont-unload through scene save and open"
```

### Task 4: Add Runtime Tests for Persistent Scene Behavior

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Write the failing runtime tests**

Extend `WriteSceneAsset(...)` in `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs` to accept a `dontUnload` parameter:

```csharp
void WriteSceneAsset(string relativePath, uint rootEntityId, bool dontUnload = false, params SceneComponentAssetRecord[] components) {
    string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
    SceneAsset sceneAsset = new SceneAsset {
        Id = relativePath,
        SceneSettings = new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile(),
            DontUnload = dontUnload
        },
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = rootEntityId,
                Name = "Entity" + rootEntityId.ToString(),
                Components = components ?? Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            }
        }
    };

    using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
    AssetSerializer.Serialize(stream, sceneAsset);
}
```

Then add these tests:

```csharp
[Fact]
public void LoadScene_WhenSingleLoadTargetsAnotherScene_PreservesPreviouslyLoadedDontUnloadScene() {
    WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
    WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", 2u);
    Core core = CreateCore(CreateSceneCatalog(
        new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/Persistent.hasset"),
        new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));

    core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
    core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);

    Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
    Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
    Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
}

[Fact]
public void UnloadScene_WhenSceneIsMarkedDontUnload_StillUnloadsWhenExplicitlyRequested() {
    WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
    Core core = CreateCore(CreateSceneCatalog(
        new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/Persistent.hasset")));

    core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);
    core.SceneManager.UnloadScene("Scenes/Persistent.helen");

    Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Persistent.helen"));
    Assert.Empty(core.SceneManager.LoadedScenes);
}

[Fact]
public void LoadScene_WhenPersistentSceneIsAlreadyLoadedAndLoadModeIsSingle_ThrowsAlreadyLoaded() {
    WriteSceneAsset("cooked/scenes/Persistent.hasset", 1u, true);
    Core core = CreateCore(CreateSceneCatalog(
        new RuntimeSceneCatalogEntry("Scenes/Persistent.helen", "cooked/scenes/Persistent.hasset")));

    core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        core.SceneManager.LoadScene("Scenes/Persistent.helen", SceneLoadMode.Single));
    Assert.Contains("already loaded", exception.Message, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the runtime tests to verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneManagerTests"
```

Expected: FAIL because `LoadedSceneRecord` does not track persistence and `SceneManager` unloads all scenes on `Single`.

- [ ] **Step 3: Implement the runtime bookkeeping and unload filtering**

Update `engine/helengine.core/scene/runtime/LoadedSceneRecord.cs`:

```csharp
public LoadedSceneRecord(string sceneId, string cookedRelativePath, IReadOnlyList<Entity> rootEntities, RuntimeSceneOwnedAssetSet ownedAssets, bool dontUnload) {
    // existing validation
    SceneId = sceneId;
    CookedRelativePath = cookedRelativePath;
    RootEntities = rootEntities;
    OwnedAssets = ownedAssets;
    DontUnload = dontUnload;
}

/// <summary>
/// Gets whether the scene should survive normal single-scene transitions.
/// </summary>
public bool DontUnload { get; }
```

Update `engine/helengine.core/scene/runtime/SceneManager.cs`:

```csharp
void LoadSceneImmediate(string sceneId, SceneLoadMode loadMode) {
    RecordTraceState("LoadSceneImmediateBegin", sceneId);
    string sceneContentPath = ResolveSceneContentPath(sceneId);
    if (LoadedSceneRecordsById.ContainsKey(sceneId)) {
        throw new InvalidOperationException($"Runtime scene '{sceneId}' is already loaded.");
    }

    if (loadMode == SceneLoadMode.Single) {
        if (LoadedSceneRecords.Count == 0) {
            RecordTraceState("LoadSceneImmediateDisposeUntrackedRoots", sceneId);
            DisposeUntrackedRootEntities();
        } else {
            RecordTraceState("LoadSceneImmediateUnloadNonPersistentScenes", sceneId);
            UnloadScenesForSingleLoad();
        }

        RecordTraceState("LoadSceneImmediateFlushReleasedTextures", sceneId);
        FlushReleasedTextures();
    }

    SceneLoading?.Invoke(this, new SceneLoadingEventArgs(sceneId, sceneContentPath));
    SceneAsset sceneAsset = ContentManager.Load<SceneAsset>(sceneContentPath, RuntimeContentProcessorIds.SceneAsset);
    try {
        bool dontUnload = sceneAsset.SceneSettings != null && sceneAsset.SceneSettings.DontUnload;
        RuntimeSceneLoadResult loadResult = SceneLoadService.LoadTracked(sceneAsset);
        try {
            LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(sceneId, sceneContentPath, loadResult.RootEntities, loadResult.OwnedAssets, dontUnload);
            LoadedSceneRecords.Add(loadedSceneRecord);
            LoadedSceneRecordsById.Add(loadedSceneRecord.SceneId, loadedSceneRecord);
            RegisterOwnedAssets(loadedSceneRecord.OwnedAssets);
            SceneLoaded?.Invoke(this, new SceneLoadedEventArgs(
                loadedSceneRecord.SceneId,
                loadedSceneRecord.CookedRelativePath,
                loadedSceneRecord.RootEntities));
        } finally {
            NativeOwnership.Delete(loadResult);
        }
    } finally {
        ReleaseTransientSceneAsset(sceneAsset);
    }
}
```

Add a helper in the same file:

```csharp
void UnloadScenesForSingleLoad() {
    List<string> sceneIdsToUnload = new List<string>();
    for (int index = 0; index < LoadedSceneRecords.Count; index++) {
        LoadedSceneRecord loadedSceneRecord = LoadedSceneRecords[index];
        if (!loadedSceneRecord.DontUnload) {
            sceneIdsToUnload.Add(loadedSceneRecord.SceneId);
        }
    }

    for (int index = 0; index < sceneIdsToUnload.Count; index++) {
        UnloadScene(sceneIdsToUnload[index]);
    }
}
```

- [ ] **Step 4: Run the runtime tests to verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneManagerTests"
```

Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.core/scene/runtime/LoadedSceneRecord.cs engine/helengine.core/scene/runtime/SceneManager.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
git commit -m "Preserve persistent scenes across single loads"
```

### Task 5: Run the Focused Regression Suite

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Test: `engine/helengine.editor.tests/SceneSettingsDialogTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Run the combined focused suite**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~SceneSettingsDialogTests|FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~SceneManagerTests"
```

Expected: PASS

- [ ] **Step 2: Commit the verification checkpoint**

```powershell
git commit --allow-empty -m "Verify scene dont-unload focused test suite"
```

## Self-Review

### Spec Coverage

- Scene-level authored flag: covered in Task 1 and Task 3.
- Editor scene settings dialog authoring: covered in Task 2.
- Dirty-state semantics: covered in Task 2.
- Legacy serializer defaulting: covered in Task 1.
- Runtime record bookkeeping and filtered `Single` unload behavior: covered in Task 4.
- Explicit unload always winning: covered in Task 4.
- Duplicate-load throws: covered in Task 4.
- Focused verification: covered in Task 5.

### Placeholder Scan

- No `TODO`, `TBD`, or deferred “implement later” steps remain.
- Each code-changing step contains concrete code snippets or explicit method bodies to add/update.
- Each test step contains exact commands and expected outcomes.

### Type Consistency

- `SceneSettingsAsset.DontUnload` is the single persisted property name throughout the plan.
- `LoadedSceneRecord.DontUnload` is the runtime bookkeeping property name throughout the plan.
- `UnloadScenesForSingleLoad()` is the filtered unload helper name used consistently in runtime steps.
- Serializer version target is consistently `15`.

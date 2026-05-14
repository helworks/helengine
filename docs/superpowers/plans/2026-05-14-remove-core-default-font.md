# Remove Core Default Font Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `Core.DefaultFontAsset`, move editor UI-font ownership to `EditorCore`, and make `FPSComponent` load, save, attach, and run safely when no font is assigned.

**Architecture:** The runtime core stops owning any implicit font state. `FPSComponent` becomes font-optional for lifecycle and persistence but font-required for visible overlay creation. Editor-only scene reference inference and generated `ui-font` resolution move to `EditorCore`, preserving editor workflows without leaking fallback behavior into runtime `Core`.

**Tech Stack:** C#, xUnit, .NET 9, helengine core/editor scene persistence, runtime scene deserializers

---

## File Structure

### Runtime component lifecycle

- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`

`FPSComponent` owns the lifecycle change. Runtime deserialization must stop treating a missing font reference as a hard failure. The component tests remain the primary behavior lock for attach, update, activation, and teardown.

### Editor-only font ownership and persistence

- Modify: `engine/helengine.editor/EditorCore.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs`
- Modify: `engine/helengine.editor/serialization/scene/FPSComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/TextComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/FPSComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

`EditorCore` becomes the only place that can answer “what is the editor UI font?” The persistence helpers and scene resolvers stop consulting `Core` and instead use editor-only state.

### Core cleanup and regression removal

- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`
- Modify any additional files returned by `rtk rg -n "DefaultFontAsset" engine`

This final pass removes the property from runtime core and updates any remaining tests or callers that still assume a runtime-global fallback font.

---

### Task 1: Make FPSComponent inert without a font

**Files:**
- Modify: `engine/helengine.editor.tests/FPSComponentTests.cs`
- Modify: `engine/helengine.core/components/2d/FPSComponent.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`

- [ ] **Step 1: Write the failing FPS runtime behavior tests**

Replace the current fallback-oriented assertions in `engine/helengine.editor.tests/FPSComponentTests.cs` with explicit missing-font coverage and late activation coverage. Use the existing `CreateFont` helper and keep the same test fixture setup.

```csharp
[Fact]
public void ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    FPSComponent fps = new FPSComponent();

    entity.AddComponent(fps);

    Assert.Empty(entity.Children);
    Assert.Null(fps.Font);
}

[Fact]
public void CoreUpdateAndDraw_WhenFontIsMissing_DoesNotThrow() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    FPSComponent fps = new FPSComponent {
        RefreshIntervalSeconds = 0d
    };

    entity.AddComponent(fps);

    Core.Instance.Update();
    Core.Instance.Draw();
    Core.Instance.Update();
    Core.Instance.Draw();

    Assert.Empty(entity.Children);
    Assert.Equal("Update FPS: --", fps.UpdateFpsText);
    Assert.Equal("Render FPS: --", fps.RenderFpsText);
}

[Fact]
public void FontProperty_WhenAssignedAfterAttachment_BuildsOverlayChildren() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    FPSComponent fps = new FPSComponent();
    entity.AddComponent(fps);

    FontAsset font = CreateFont(24f);
    fps.Font = font;

    Entity overlayHost = Assert.Single(entity.Children);
    TextComponent updateText = Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
    TextComponent renderText = Assert.Single(overlayHost.Children[1].Components.OfType<TextComponent>());

    Assert.Same(font, updateText.Font);
    Assert.Same(font, renderText.Font);
    Assert.Equal(font.LineHeight, overlayHost.Children[1].LocalPosition.Y);
}

[Fact]
public void FontProperty_WhenClearedAfterAttachment_RemovesOverlayChildren() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    FPSComponent fps = new FPSComponent {
        Font = CreateFont()
    };

    entity.AddComponent(fps);
    Assert.Single(entity.Children);

    fps.Font = null;

    Assert.Empty(entity.Children);
    Assert.Null(fps.Font);
}
```

- [ ] **Step 2: Run the FPS tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests" -v minimal`

Expected: `FAIL` because the current constructor and attach path still depend on `Core.Instance.DefaultFontAsset`, `ComponentAdded` still throws or eagerly builds only with implicit font state, and `Font = null` is currently rejected.

- [ ] **Step 3: Write the minimal FPSComponent implementation**

Update `engine/helengine.core/components/2d/FPSComponent.cs` so the component does not resolve any default font and so overlay activation is driven entirely by `Font` presence.

Key implementation shape:

```csharp
public FontAsset Font {
    get { return font; }
    set {
        if (ReferenceEquals(font, value)) {
            return;
        }

        font = value;
        RefreshOverlayActivation();
    }
}

public FPSComponent() {
}

public override void ComponentAdded(Entity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    base.ComponentAdded(entity);
    RefreshOverlayActivation();
}

public override void Update() {
    if (!Initialized) {
        return;
    }

    TryRefreshOverlay();
}

void RefreshOverlayActivation() {
    if (Parent == null) {
        return;
    }

    if (Font == null) {
        TearDownOverlay();
        return;
    }

    if (!Initialized) {
        BuildOverlay();
        ResetSamplingWindow();
        ApplyPadding();
        ApplyRenderOrder();
        return;
    }

    ApplyFont();
    ApplyRenderOrder();
    ApplyPadding();
}
```

Also add explicit private helpers inside the class for symmetric setup and teardown:

```csharp
void BuildOverlay() {
    if (Parent.Children == null) {
        Parent.InitChildren();
    }

    OverlayHost = new Entity();
    OverlayHost.LayerMask = Parent.LayerMask;
    OverlayHost.InitChildren();
    OverlayHost.InitComponents();
    Parent.AddChild(OverlayHost);

    UpdateRowHost = new Entity();
    UpdateRowHost.LayerMask = Parent.LayerMask;
    UpdateRowHost.InitChildren();
    UpdateRowHost.InitComponents();
    OverlayHost.AddChild(UpdateRowHost);

    UpdateTextComponent = new TextComponent();
    UpdateTextComponent.Color = new byte4(255, 255, 255, 255);
    UpdateRowHost.AddComponent(UpdateTextComponent);

    RenderRowHost = new Entity();
    RenderRowHost.LayerMask = Parent.LayerMask;
    RenderRowHost.InitChildren();
    RenderRowHost.InitComponents();
    OverlayHost.AddChild(RenderRowHost);

    RenderTextComponent = new TextComponent();
    RenderTextComponent.Color = new byte4(255, 255, 255, 255);
    RenderRowHost.AddComponent(RenderTextComponent);

    Initialized = true;
    ActiveComponents.Add(this);
    ApplyFont();
}

void TearDownOverlay() {
    bool removed = ActiveComponents.Remove(this);
    if (Parent != null && OverlayHost != null && OverlayHost.Parent == Parent) {
        Parent.RemoveChild(OverlayHost);
    }

    OverlayHost = null;
    UpdateRowHost = null;
    RenderRowHost = null;
    UpdateTextComponent = null;
    RenderTextComponent = null;
    Initialized = false;
}
```

Update `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs` so missing font references are accepted:

```csharp
if (fontReference != null) {
    fpsComponent.Font = referenceResolver.ResolveFont(fontReference);
}

return fpsComponent;
```

- [ ] **Step 4: Run the FPS tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests" -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit the FPS runtime behavior change**

```bash
git add engine/helengine.core/components/2d/FPSComponent.cs engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs engine/helengine.editor.tests/FPSComponentTests.cs
git commit -m "refactor: make fps component font-optional"
```

### Task 2: Move editor default font ownership into EditorCore

**Files:**
- Modify: `engine/helengine.editor/EditorCore.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/EditorCliCommandRunner.cs`
- Modify: `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs`
- Modify: `engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs`
- Modify: `engine/helengine.editor/serialization/scene/FPSComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/serialization/scene/TextComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/FPSComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Write the failing editor font-ownership and persistence tests**

Update `engine/helengine.editor.tests/serialization/scene/FPSComponentPersistenceDescriptorTests.cs` so the descriptor no longer depends on `Core.DefaultFontAsset` and so missing font references deserialize to a valid inert component.

```csharp
[Fact]
public void DeserializeComponent_WhenFontReferenceIsMissing_LeavesFontNull() {
    FPSComponentPersistenceDescriptor descriptor = new FPSComponentPersistenceDescriptor();

    EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
    writer.WriteField("RefreshIntervalSeconds", fieldWriter => fieldWriter.WriteInt64(BitConverter.DoubleToInt64Bits(1.25d)));
    writer.WriteField("Padding", fieldWriter => fieldWriter.WriteInt2(new int2(13, 21)));
    writer.WriteField("RenderOrder2D", fieldWriter => fieldWriter.WriteByte(243));

    SceneComponentAssetRecord record = new SceneComponentAssetRecord {
        ComponentTypeId = "helengine.FPSComponent",
        ComponentIndex = 0,
        Payload = writer.BuildPayload()
    };

    FPSComponent loadedComponent = Assert.IsType<FPSComponent>(descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

    Assert.Null(loadedComponent.Font);
    Assert.Equal(1.25d, loadedComponent.RefreshIntervalSeconds);
    Assert.Equal(new int2(13, 21), loadedComponent.Padding);
    Assert.Equal((byte)243, loadedComponent.RenderOrder2D);
}
```

Add one explicit editor-font inference test to `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs` that uses `EditorCore` rather than `Core`:

```csharp
[Fact]
public void SaveAndLoad_WhenFpsUsesEditorCoreFont_InfersEditorFontReference() {
    EditorCore core = new EditorCore(null);
    core.SetDefaultFontAssetForEditor(CreateFont("EditorUi"));

    FPSComponent fpsComponent = new FPSComponent {
        Font = core.DefaultFontAssetForEditor
    };

    EntityComponentSaveState saveState = new EntityComponentSaveState();
    new SceneAssetReferenceInferenceService(ProjectRootPath).PopulateAssetReferences(fpsComponent, saveState);

    SceneAssetReference fontReference = Assert.IsType<SceneAssetReference>(saveState.GetAssetReference("Font"));
    Assert.Equal("ui-font", fontReference.AssetId);
}
```

- [ ] **Step 2: Run the editor persistence tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentPersistenceDescriptorTests|FullyQualifiedName~SceneSaveServiceTests" -v minimal`

Expected: `FAIL` because the current descriptors and inference service still read `Core.Instance.DefaultFontAsset` and still require a fallback font on deserialize.

- [ ] **Step 3: Write the minimal editor-only font ownership implementation**

Add editor-only font storage to `engine/helengine.editor/EditorCore.cs`:

```csharp
FontAsset DefaultFontAssetForEditorValue;

public FontAsset DefaultFontAssetForEditor {
    get { return DefaultFontAssetForEditorValue; }
}

public void SetDefaultFontAssetForEditor(FontAsset font) {
    if (font == null) {
        throw new ArgumentNullException(nameof(font));
    }

    DefaultFontAssetForEditorValue = font;
}
```

Update `engine/helengine.editor/EditorSession.cs` and `engine/helengine.editor/EditorCliCommandRunner.cs` to call the new setter:

```csharp
this.core.SetDefaultFontAssetForEditor(this.uiFont);
```

Update `engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs` so it can identify the editor font without `Core`:

```csharp
internal static bool TryResolveEditorCoreFont(FontAsset font, out SceneAssetReference reference) {
    reference = null;
    if (font == null) {
        return false;
    }
    if (Core.Instance is not EditorCore editorCore) {
        return false;
    }
    if (!ReferenceEquals(font, editorCore.DefaultFontAssetForEditor)) {
        return false;
    }

    reference = BuildEditorFontReference();
    return true;
}
```

Use that helper in `SceneAssetReferenceInferenceService`:

```csharp
if (FontAssetScenePersistenceSupport.TryResolveEditorCoreFont(fpsComponent.Font, out SceneAssetReference fontReference)) {
    saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
    return;
}
```

Update `EditorSceneAssetReferenceResolver.ResolveGeneratedFont`:

```csharp
if (Core.Instance is not EditorCore editorCore || editorCore.DefaultFontAssetForEditor == null) {
    throw new InvalidOperationException("The editor font is not available in the active editor core.");
}

return editorCore.DefaultFontAssetForEditor;
```

Update `FPSComponentPersistenceDescriptor.DeserializeComponent` and `TextComponentPersistenceDescriptor.DeserializeComponent` so missing font references no longer fallback through `Core`. `FPSComponent` should succeed with null font; `TextComponent` should keep requiring a resolvable font reference unless the record explicitly provides one.

For `FPSComponentPersistenceDescriptor`:

```csharp
if (reader.TryGetFieldReader(FontReferenceFieldName, out EngineBinaryReader fontReferenceReader)) {
    using (fontReferenceReader) {
        SceneAssetReference fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
        if (fontReference != null) {
            fpsComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
            if (saveComponent != null) {
                saveComponent.SetAssetReference(fpsComponent, FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
            }
        }
    }
}

return fpsComponent;
```

- [ ] **Step 4: Run the editor persistence tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentPersistenceDescriptorTests|FullyQualifiedName~SceneSaveServiceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit the editor-only font ownership change**

```bash
git add engine/helengine.editor/EditorCore.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/EditorCliCommandRunner.cs engine/helengine.editor/serialization/scene/FontAssetScenePersistenceSupport.cs engine/helengine.editor/serialization/scene/EditorSceneAssetReferenceResolver.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs engine/helengine.editor/serialization/scene/FPSComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/TextComponentPersistenceDescriptor.cs engine/helengine.editor.tests/serialization/scene/FPSComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "refactor: move default font ownership to editor core"
```

### Task 3: Remove DefaultFontAsset from Core and clean remaining references

**Files:**
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- Modify: every remaining file reported by `rtk rg -n "DefaultFontAsset" engine`

- [ ] **Step 1: Write the failing cleanup regression tests**

Update the remaining tests that still set or assert `Core.Instance.DefaultFontAsset` so they reflect the new contract.

In `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`, add an explicit runtime load case showing that an FPS component without a packaged font reference still loads:

```csharp
[Fact]
public void LoadScene_WhenFpsPayloadOmitsFontReference_LoadsComponentWithNullFont() {
    SceneComponentAssetRecord record = new SceneComponentAssetRecord {
        ComponentTypeId = "Helengine.FPSComponent",
        ComponentIndex = 0,
        Payload = BuildFpsPayloadWithoutFontReference()
    };

    RuntimeSceneLoadService service = new RuntimeSceneLoadService(new TestRuntimeSceneAssetReferenceResolver(), RuntimeComponentRegistry.CreateDefault());

    Entity[] loadedRoots = service.LoadSceneRoots(new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAssetRecord {
                Name = "Root",
                Components = new[] { record }
            }
        }
    });

    FPSComponent fpsComponent = Assert.IsType<FPSComponent>(Assert.Single(loadedRoots[0].Components, component => component is FPSComponent));
    Assert.Null(fpsComponent.Font);
}
```

Update `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs` to use editor-owned font assignment explicitly instead of `Core.Instance.DefaultFontAsset`.

- [ ] **Step 2: Run the focused regression tests to verify they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~EditorSessionSceneSaveTests" -v minimal`

Expected: `FAIL` because runtime deserialization and editor scene-save tests still contain `Core.DefaultFontAsset` assumptions or helper setup.

- [ ] **Step 3: Remove Core.DefaultFontAsset and update all remaining call sites**

Delete the backing field and property from `engine/helengine.core/Core.cs`:

```csharp
// Remove:
FontAsset DefaultFontAssetValue;

public FontAsset DefaultFontAsset {
    get { return DefaultFontAssetValue; }
    set {
        if (value == null) {
            throw new ArgumentNullException(nameof(value));
        }

        DefaultFontAssetValue = value;
    }
}
```

Then update every remaining `DefaultFontAsset` caller returned by `rtk rg -n "DefaultFontAsset" engine`:

- editor host setup should call `EditorCore.SetDefaultFontAssetForEditor(...)`
- editor tests should set `EditorCore` font state explicitly when they need `ui-font`
- runtime tests should assign `FPSComponent.Font` directly instead of relying on `Core`

Keep `EditorCliBuildRunner`, `EditorPlatformAssetCookService`, `EditorPlatformBuildGraphRunner`, and `EditorWindowsBuildScenePackager` unchanged unless they reference `Core.DefaultFontAsset`; those classes carry packaged editor font assets by constructor dependency, which is still valid.

- [ ] **Step 4: Run the full targeted verification suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests|FullyQualifiedName~FPSComponentPersistenceDescriptorTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~EditorSessionSceneSaveTests" -v minimal`

Expected: `PASS`

Then run one final source scan:

Run: `rtk rg -n "DefaultFontAsset" engine`

Expected: only editor-owned names such as `DefaultFontAssetForEditor` remain, with no `Core.DefaultFontAsset` references.

- [ ] **Step 5: Commit the Core cleanup**

```bash
git add engine/helengine.core/Core.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git add engine
git commit -m "refactor: remove default font from runtime core"
```

## Self-Review

Spec coverage:

- remove `DefaultFontAsset` from `Core`: Task 3
- keep editor font ownership editor-only: Task 2
- make `FPSComponent` inert without a font: Task 1
- allow FPS serialize/deserialize without font reference: Tasks 1 and 2
- preserve editor `ui-font` handling without `Core`: Task 2

Placeholder scan:

- no `TODO`, `TBD`, or deferred steps remain
- each task includes concrete files, commands, and code snippets
- each verification step has an expected fail/pass outcome

Type consistency:

- editor-owned font property name is `DefaultFontAssetForEditor`
- editor setter is `SetDefaultFontAssetForEditor`
- runtime component property remains `FPSComponent.Font`


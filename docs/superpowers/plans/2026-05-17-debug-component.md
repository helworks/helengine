# Debug Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a separate runtime `DebugComponent` that renders a fixed five-line diagnostics overlay showing render FPS, resident memory, committed memory, 2D drawables, 3D drawables, and draw calls.

**Architecture:** Mirror the proven `FPSComponent` overlay pattern instead of extending it. Keep `DebugComponent` backend-agnostic by reading only shared `Core`, `RuntimeDiagnosticsService`, and `ObjectManager` state, with `Core` publishing the last draw-call count through the shared render-manager contract.

**Tech Stack:** C#, xUnit, `helengine.core`, editor scene persistence, runtime scene deserialization, Windows scene packaging.

---

### Task 1: Publish A Shared Core Draw-Call Metric

**Files:**
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.core/managers/rendering/RenderManager3D.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`
- Test: `engine/helengine.editor.tests/CoreTimingTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Draw_WhenCalledRepeatedly_StoresTheLatestDrawCallCount() {
    TestClockDrivenCore core = CreateClockDrivenCore();
    TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(core.RenderManager3D);
    renderManager.QueueDrawCallCounts(new[] { 9, 4 });

    core.Draw();
    Assert.Equal(9, core.LastRenderManager3DDrawCallCount);

    core.Draw();
    Assert.Equal(4, core.LastRenderManager3DDrawCallCount);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CoreTimingTests.Draw_WhenCalledRepeatedly_StoresTheLatestDrawCallCount"`

Expected: FAIL because `Core` does not expose `LastRenderManager3DDrawCallCount` and `TestRenderManager3D` has no queued draw-call support yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// engine/helengine.core/managers/rendering/RenderManager3D.cs
/// <summary>
/// Gets the draw-call count recorded by the most recent completed draw.
/// </summary>
public virtual int LastDrawCallCount => 0;

// engine/helengine.core/Core.cs
/// <summary>
/// Gets the draw-call count reported by the most recent render-manager draw.
/// </summary>
public int LastRenderManager3DDrawCallCount { get; private set; }

public virtual void Draw() {
    LastRenderManager3DDrawMilliseconds = MeasureRenderManager3DDrawMilliseconds();
    LastRenderManager3DDrawCallCount = RenderManager3D == null ? 0 : RenderManager3D.LastDrawCallCount;
    FPSComponent.RecordRenderFrame();
}

// engine/helengine.directx11/DirectX11Renderer3D.cs
/// <summary>
/// Gets the draw call count from the previous frame.
/// </summary>
public override int LastDrawCallCount => lastDrawCalls;

// engine/helengine.editor.tests/testing/TestRenderManager3D.cs
readonly Queue<int> QueuedDrawCallCountsValue = new Queue<int>();
int LastDrawCallCountValue;

public void QueueDrawCallCounts(IEnumerable<int> drawCallCounts) {
    foreach (int drawCallCount in drawCallCounts) {
        QueuedDrawCallCountsValue.Enqueue(drawCallCount);
    }
}

public override void Draw() {
    LastDrawCallCountValue = QueuedDrawCallCountsValue.Count == 0 ? 0 : QueuedDrawCallCountsValue.Dequeue();
}

public override int LastDrawCallCount => LastDrawCallCountValue;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CoreTimingTests"`

Expected: PASS, including the new draw-call-count timing assertion.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/Core.cs engine/helengine.core/managers/rendering/RenderManager3D.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs engine/helengine.editor.tests/CoreTimingTests.cs
git commit -m "feat: publish shared debug draw call metric"
```

### Task 2: Add The Debug Overlay Runtime Shell

**Files:**
- Create: `engine/helengine.core/components/2d/DebugComponent.cs`
- Test: `engine/helengine.editor.tests/DebugComponentTests.cs`

- [ ] **Step 1: Write the failing tests for inert and fixed-hierarchy behavior**

```csharp
[Fact]
public void ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    DebugComponent debug = new DebugComponent();
    entity.AddComponent(debug);

    Assert.Empty(entity.Children);
    Assert.Null(debug.Font);
}

[Fact]
public void FontProperty_WhenAssignedAfterAttachment_BuildsFiveOverlayRows() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    DebugComponent debug = new DebugComponent();
    entity.AddComponent(debug);
    debug.Font = CreateFont(24f);

    Entity overlayHost = Assert.Single(entity.Children);
    Assert.Equal(5, overlayHost.Children.Count);
    Assert.Single(overlayHost.Children[0].Components.OfType<TextComponent>());
    Assert.Single(overlayHost.Children[4].Components.OfType<TextComponent>());
}

[Fact]
public void RemoveComponent_WhenOverlayWasBuilt_DisposesOverlayEntities() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();

    DebugComponent debug = new DebugComponent {
        Font = CreateFont()
    };
    entity.AddComponent(debug);

    entity.RemoveComponent(debug);

    Assert.Empty(entity.Children);
    Assert.Single(Core.Instance.ObjectManager.Entities);
    Assert.Empty(Core.Instance.ObjectManager.Drawables2D);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests"`

Expected: FAIL because `DebugComponent` does not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace helengine {
    /// <summary>
    /// Renders a fixed five-line runtime diagnostics overlay using the shared 2D text pipeline.
    /// </summary>
    public class DebugComponent : UpdateComponent {
        static readonly List<DebugComponent> ActiveComponents = new List<DebugComponent>();

        FontAsset font;
        Entity OverlayHost;
        Entity RenderFpsRowHost;
        Entity ResidentMemoryRowHost;
        Entity CommittedMemoryRowHost;
        Entity Drawables2DRowHost;
        Entity Drawables3DRowHost;
        TextComponent RenderFpsTextComponent;
        TextComponent ResidentMemoryTextComponent;
        TextComponent CommittedMemoryTextComponent;
        TextComponent Drawables2DTextComponent;
        TextComponent Drawables3DTextComponent;
        bool Initialized;

        public double RefreshIntervalSeconds { get; set; } = 0.5d;
        public int2 Padding { get; set; } = new int2(8, 6);
        public byte RenderOrder2D { get; set; } = 250;
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

        public string RenderFpsText { get; private set; } = "Render FPS: --";
        public string ResidentMemoryText { get; private set; } = "Memory Res: --";
        public string CommittedMemoryText { get; private set; } = "Memory Com: --";
        public string Drawables2DText { get; private set; } = "Drawables 2D: --";
        public string Drawables3DText { get; private set; } = "Drawables 3D: -- DrawCalls: --";

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RefreshOverlayActivation();
        }

        public override void ComponentRemoved(Entity entity) {
            TearDownOverlay();
            base.ComponentRemoved(entity);
        }

        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);
            ResetSamplingWindow();
            ApplyVisibleText();
        }

        public override void Update() { if (Initialized) { TryRefreshOverlay(); } }

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
            }
            ApplyFont();
            ApplyPadding();
            ApplyRenderOrder();
        }

        void BuildOverlay() {
            OverlayHost = new Entity();
            OverlayHost.LayerMask = Parent.LayerMask;
            OverlayHost.InitChildren();
            OverlayHost.InitComponents();
            Parent.AddChild(OverlayHost);
            RenderFpsRowHost = CreateRowHost();
            ResidentMemoryRowHost = CreateRowHost();
            CommittedMemoryRowHost = CreateRowHost();
            Drawables2DRowHost = CreateRowHost();
            Drawables3DRowHost = CreateRowHost();
            RenderFpsTextComponent = CreateRowTextComponent(RenderFpsRowHost);
            ResidentMemoryTextComponent = CreateRowTextComponent(ResidentMemoryRowHost);
            CommittedMemoryTextComponent = CreateRowTextComponent(CommittedMemoryRowHost);
            Drawables2DTextComponent = CreateRowTextComponent(Drawables2DRowHost);
            Drawables3DTextComponent = CreateRowTextComponent(Drawables3DRowHost);
            Initialized = true;
            ActiveComponents.Add(this);
        }

        public static void RecordRenderFrame() {
            for (int i = ActiveComponents.Count - 1; i >= 0; i--) {
                DebugComponent component = ActiveComponents[i];
                if (component.Initialized && component.Parent != null && component.Parent.IsHierarchyEnabled) {
                    component.RenderFrameCount++;
                }
            }
        }
    }
}
```

Also copy the deterministic `CreateFont(float lineHeight)` helper from `engine/helengine.editor.tests/FPSComponentTests.cs` into `engine/helengine.editor.tests/DebugComponentTests.cs`, extending the glyph table with the characters needed for `Memory`, `Drawables`, and `Calls`.

Add these private helpers in the same file so the row layout is explicit and stable:

```csharp
Entity CreateRowHost() {
    Entity rowHost = new Entity();
    rowHost.LayerMask = Parent.LayerMask;
    rowHost.InitChildren();
    rowHost.InitComponents();
    OverlayHost.AddChild(rowHost);
    return rowHost;
}

TextComponent CreateRowTextComponent(Entity rowHost) {
    TextComponent textComponent = new TextComponent {
        Color = new byte4(255, 255, 255, 255)
    };
    rowHost.AddComponent(textComponent);
    return textComponent;
}

void ApplyFont() {
    RenderFpsTextComponent.Font = Font;
    ResidentMemoryTextComponent.Font = Font;
    CommittedMemoryTextComponent.Font = Font;
    Drawables2DTextComponent.Font = Font;
    Drawables3DTextComponent.Font = Font;
    ResidentMemoryRowHost.LocalPosition = new float3(0f, Font.LineHeight, 0.1f);
    CommittedMemoryRowHost.LocalPosition = new float3(0f, Font.LineHeight * 2f, 0.2f);
    Drawables2DRowHost.LocalPosition = new float3(0f, Font.LineHeight * 3f, 0.3f);
    Drawables3DRowHost.LocalPosition = new float3(0f, Font.LineHeight * 4f, 0.4f);
}

void ApplyPadding() {
    OverlayHost.LocalPosition = new float3(Padding.X, Padding.Y, 0f);
}

void ApplyRenderOrder() {
    RenderFpsTextComponent.RenderOrder2D = RenderOrder2D;
    ResidentMemoryTextComponent.RenderOrder2D = RenderOrder2D;
    CommittedMemoryTextComponent.RenderOrder2D = RenderOrder2D;
    Drawables2DTextComponent.RenderOrder2D = RenderOrder2D;
    Drawables3DTextComponent.RenderOrder2D = RenderOrder2D;
}

void TearDownOverlay() {
    ActiveComponents.Remove(this);
    if (OverlayHost != null) {
        OverlayHost.Dispose();
    }
    OverlayHost = null;
    RenderFpsRowHost = null;
    ResidentMemoryRowHost = null;
    CommittedMemoryRowHost = null;
    Drawables2DRowHost = null;
    Drawables3DRowHost = null;
    RenderFpsTextComponent = null;
    ResidentMemoryTextComponent = null;
    CommittedMemoryTextComponent = null;
    Drawables2DTextComponent = null;
    Drawables3DTextComponent = null;
    Initialized = false;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests.ComponentAdded_WhenFontIsMissing_DoesNotBuildOverlayChildren|FullyQualifiedName~DebugComponentTests.FontProperty_WhenAssignedAfterAttachment_BuildsFiveOverlayRows|FullyQualifiedName~DebugComponentTests.RemoveComponent_WhenOverlayWasBuilt_DisposesOverlayEntities"`

Expected: PASS with a five-row overlay that mirrors the `FPSComponent` lifecycle.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/2d/DebugComponent.cs engine/helengine.editor.tests/DebugComponentTests.cs
git commit -m "feat: add debug overlay component shell"
```

### Task 3: Fill The Overlay With Live Metrics And Deterministic Formatting

**Files:**
- Modify: `engine/helengine.core/components/2d/DebugComponent.cs`
- Modify: `engine/helengine.editor.tests/DebugComponentTests.cs`
- Reuse test helper: `engine/helengine.editor.tests/testing/FakeRuntimeDiagnosticsProvider.cs`

- [ ] **Step 1: Write the failing tests for sampled metrics and placeholder behavior**

```csharp
[Fact]
public void CoreUpdateAndDraw_WhenSamplingFrames_FormatsAllRows() {
    RuntimeMemoryDiagnosticsSnapshot snapshot = new RuntimeMemoryDiagnosticsSnapshot {
        ResidentBytes = 128UL * 1024UL * 1024UL + 512UL * 1024UL,
        CommittedBytes = 192UL * 1024UL * 1024UL
    };
    TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
        ContentRootPath = TempRootPath,
        RuntimeDiagnosticsProvider = new FakeRuntimeDiagnosticsProvider(snapshot)
    });
    TestRenderManager3D renderManager = new TestRenderManager3D();
    core.Initialize(renderManager, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
    renderManager.QueueDrawCallCounts(new[] { 23 });
    core.QueueMeasuredDrawMilliseconds(new[] { 12.3d });

    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();
    DebugComponent debug = new DebugComponent {
        Font = CreateFont(),
        RefreshIntervalSeconds = 0d
    };
    entity.AddComponent(debug);

    Core.Instance.Update(0.25d);
    Core.Instance.Draw();
    Core.Instance.Update(0.25d);

    Assert.Equal("Render FPS: 4.0", debug.RenderFpsText);
    Assert.Equal("Memory Res: 128.5 MB", debug.ResidentMemoryText);
    Assert.Equal("Memory Com: 192.0 MB", debug.CommittedMemoryText);
    Assert.Equal("Drawables 2D: 5", debug.Drawables2DText);
    Assert.Equal("Drawables 3D: 0 DrawCalls: 23", debug.Drawables3DText);
}

[Fact]
public void ComponentAdded_WhenRuntimeDiagnosticsProviderIsMissing_UsesMemoryPlaceholders() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();
    DebugComponent debug = new DebugComponent {
        Font = CreateFont(),
        RefreshIntervalSeconds = 0d
    };
    entity.AddComponent(debug);

    Assert.Equal("Memory Res: --", debug.ResidentMemoryText);
    Assert.Equal("Memory Com: --", debug.CommittedMemoryText);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests.CoreUpdateAndDraw_WhenSamplingFrames_FormatsAllRows|FullyQualifiedName~DebugComponentTests.ComponentAdded_WhenRuntimeDiagnosticsProviderIsMissing_UsesMemoryPlaceholders"`

Expected: FAIL because `DebugComponent` does not yet compute live metrics or placeholder memory rows.

- [ ] **Step 3: Write minimal implementation**

```csharp
double LastSampleElapsedSeconds;
int RenderFrameCount;

void ResetSamplingWindow() {
    RenderFrameCount = 0;
    LastSampleElapsedSeconds = Core.Instance == null ? 0d : Core.Instance.TotalElapsedSeconds;
    ApplyCurrentOverlayText();
}

void TryRefreshOverlay() {
    Core core = Core.Instance ?? throw new InvalidOperationException("DebugComponent requires an active Core instance.");
    double elapsedSeconds = core.TotalElapsedSeconds - LastSampleElapsedSeconds;
    if (RefreshIntervalSeconds > 0d && elapsedSeconds < RefreshIntervalSeconds) {
        return;
    }

    double safeElapsedSeconds = elapsedSeconds <= 0d ? 1d : elapsedSeconds;
    double renderFps = RenderFrameCount / safeElapsedSeconds;
    RenderFpsText = "Render FPS: " + FormatOneDecimal(renderFps);
    ResidentMemoryText = ResolveResidentMemoryText(core);
    CommittedMemoryText = ResolveCommittedMemoryText(core);
    Drawables2DText = "Drawables 2D: " + core.ObjectManager.Drawables2D.Count;
    Drawables3DText = "Drawables 3D: " + core.ObjectManager.Drawables3D.Count + " DrawCalls: " + core.LastRenderManager3DDrawCallCount;
    ApplyVisibleText();
    RenderFrameCount = 0;
    LastSampleElapsedSeconds = core.TotalElapsedSeconds;
}

string ResolveResidentMemoryText(Core core) {
    if (core.InitializationOptions.RuntimeDiagnosticsProvider == null) {
        return "Memory Res: --";
    }

    RuntimeMemoryDiagnosticsSnapshot snapshot = core.RuntimeDiagnosticsService.CaptureSnapshot();
    return "Memory Res: " + FormatMegabytes(snapshot.ResidentBytes);
}

string ResolveCommittedMemoryText(Core core) {
    if (core.InitializationOptions.RuntimeDiagnosticsProvider == null) {
        return "Memory Com: --";
    }

    RuntimeMemoryDiagnosticsSnapshot snapshot = core.RuntimeDiagnosticsService.CaptureSnapshot();
    return "Memory Com: " + FormatMegabytes(snapshot.CommittedBytes);
}

public static void RecordRenderFrame() {
    for (int i = ActiveComponents.Count - 1; i >= 0; i--) {
        DebugComponent component = ActiveComponents[i];
        if (component.Initialized && component.Parent != null && component.Parent.IsHierarchyEnabled) {
            component.RenderFrameCount++;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests"`

Expected: PASS with fixed five-row formatting, live draw-call values, and placeholder memory rows when no diagnostics provider is configured.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/2d/DebugComponent.cs engine/helengine.editor.tests/DebugComponentTests.cs
git commit -m "feat: add live debug overlay metrics"
```

### Task 4: Add Scene Persistence And Runtime Deserialization

**Files:**
- Create: `engine/helengine.editor/serialization/scene/DebugComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/DebugComponentPersistenceDescriptorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing persistence and runtime-load tests**

```csharp
[Fact]
public void SerializeAndDeserialize_WhenDebugOverlayUsesCustomSettings_RoundTripsTheComponent() {
    DebugComponentPersistenceDescriptor descriptor = new DebugComponentPersistenceDescriptor();
    TestSceneAssetReferenceResolver referenceResolver = new TestSceneAssetReferenceResolver();
    referenceResolver.RegisterFont(BuildEditorFontReference(), ((EditorCore)Core.Instance).DefaultFontAssetForEditor);

    DebugComponent debugComponent = new DebugComponent {
        Font = ((EditorCore)Core.Instance).DefaultFontAssetForEditor,
        RefreshIntervalSeconds = 1.25d,
        Padding = new int2(13, 21),
        RenderOrder2D = 243
    };

    SceneComponentAssetRecord record = descriptor.SerializeComponent(debugComponent, 0, null);
    DebugComponent loadedComponent = Assert.IsType<DebugComponent>(descriptor.DeserializeComponent(record, null, referenceResolver));

    Assert.Equal(1.25d, loadedComponent.RefreshIntervalSeconds);
    Assert.Equal(new int2(13, 21), loadedComponent.Padding);
    Assert.Equal((byte)243, loadedComponent.RenderOrder2D);
    Assert.Same(((EditorCore)Core.Instance).DefaultFontAssetForEditor, loadedComponent.Font);
}

[Fact]
public void Load_WhenSceneContainsDebugComponent_MaterializesTheComponent() {
    WriteFontAsset("fonts/default.hefont", CreateFont());
    RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
        Core.Instance.ContentManager,
        TempRootPath,
        ShaderCompileTarget.DirectX11);
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
    SceneAsset sceneAsset = new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "Root",
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "Helengine.DebugComponent",
                        ComponentIndex = 0,
                        Payload = WriteDebugComponentPayload()
                    }
                }
            }
        }
    };

    Entity loadedRoot = Assert.Single(loadService.Load(sceneAsset));
    DebugComponent debugComponent = Assert.IsType<DebugComponent>(Assert.Single(loadedRoot.Components, component => component is DebugComponent));
    Assert.Equal((byte)250, debugComponent.RenderOrder2D);
}

[Fact]
public void Load_WhenSceneContainsDebugComponentWithoutFontReference_LoadsComponentWithNullFont() {
    RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
        Core.Instance.ContentManager,
        TempRootPath,
        ShaderCompileTarget.DirectX11);
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
    SceneAsset sceneAsset = new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "Root",
                Components = new[] {
                    new SceneComponentAssetRecord {
                        ComponentTypeId = "Helengine.DebugComponent",
                        ComponentIndex = 0,
                        Payload = WriteDebugComponentPayloadWithoutFontReference()
                    }
                }
            }
        }
    };

    Entity loadedRoot = Assert.Single(loadService.Load(sceneAsset));
    DebugComponent debugComponent = Assert.IsType<DebugComponent>(Assert.Single(loadedRoot.Components, component => component is DebugComponent));
    Assert.Null(debugComponent.Font);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsDebugComponent_MaterializesTheComponent"`

Expected: FAIL because the new descriptor, type id, and runtime deserializer do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// engine/helengine.editor/serialization/scene/DebugComponentPersistenceDescriptor.cs
public class DebugComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
    const string FontReferenceFieldName = "FontReference";
    const string RefreshIntervalSecondsFieldName = "RefreshIntervalSeconds";
    const string PaddingFieldName = "Padding";
    const string RenderOrder2DFieldName = "RenderOrder2D";

    public Type ComponentType => typeof(DebugComponent);
    public string ComponentTypeId => "helengine.DebugComponent";

    public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
        DebugComponent debugComponent = component as DebugComponent ?? throw new InvalidOperationException("Debug component descriptor received an unsupported component type.");
        SceneAssetReference fontReference = debugComponent.Font == null
            ? null
            : FontAssetScenePersistenceSupport.ResolveFontReference(nameof(DebugComponent), debugComponent.Font, saveState);
        EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
        writer.WriteField(FontReferenceFieldName, fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(fieldWriter, fontReference));
        writer.WriteField(RefreshIntervalSecondsFieldName, fieldWriter => fieldWriter.WriteInt64(BitConverter.DoubleToInt64Bits(debugComponent.RefreshIntervalSeconds)));
        writer.WriteField(PaddingFieldName, fieldWriter => fieldWriter.WriteInt2(debugComponent.Padding));
        writer.WriteField(RenderOrder2DFieldName, fieldWriter => fieldWriter.WriteByte(debugComponent.RenderOrder2D));
        return new SceneComponentAssetRecord {
            ComponentTypeId = ComponentTypeId,
            ComponentIndex = componentIndex,
            Payload = writer.BuildPayload()
        };
    }

    public Component DeserializeComponent(SceneComponentAssetRecord record, EntitySaveComponent saveComponent, ISceneAssetReferenceResolver referenceResolver) {
        DebugComponent debugComponent = new DebugComponent();
        EditorTaggedSceneComponentFieldReader reader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
        if (reader.TryGetFieldReader(RefreshIntervalSecondsFieldName, out EngineBinaryReader refreshIntervalReader)) {
            using (refreshIntervalReader) {
                debugComponent.RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(refreshIntervalReader.ReadInt64());
            }
        }
        if (reader.TryGetFieldReader(PaddingFieldName, out EngineBinaryReader paddingReader)) {
            using (paddingReader) {
                debugComponent.Padding = paddingReader.ReadInt2();
            }
        }
        if (reader.TryGetFieldReader(RenderOrder2DFieldName, out EngineBinaryReader renderOrderReader)) {
            using (renderOrderReader) {
                debugComponent.RenderOrder2D = renderOrderReader.ReadByte();
            }
        }
        if (reader.TryGetFieldReader(FontReferenceFieldName, out EngineBinaryReader fontReferenceReader)) {
            using (fontReferenceReader) {
                SceneAssetReference fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
                if (fontReference != null) {
                    debugComponent.Font = FontAssetScenePersistenceSupport.ResolveFont(referenceResolver, fontReference);
                }
            }
        }
        return debugComponent;
    }
}

// engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs
public sealed class RuntimeDebugComponentDeserializer : IRuntimeComponentDeserializer {
    const byte CurrentVersion = 1;
    const string ComponentType = "helengine.DebugComponent";
    public string ComponentTypeId => ComponentType;

    public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
        using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
        using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
        byte version = reader.ReadByte();
        if (version != CurrentVersion) {
            throw new InvalidOperationException($"Unsupported Debug component payload version '{version}'.");
        }

        SceneAssetReference fontReference = FontAssetScenePersistenceSupport.ReadOptionalReference(reader);
        DebugComponent debugComponent = new DebugComponent {
            RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
            Padding = reader.ReadInt2(),
            RenderOrder2D = reader.ReadByte()
        };
        if (fontReference != null) {
            debugComponent.Font = referenceResolver.ResolveFont(fontReference);
        }
        return debugComponent;
    }
}

// registrations
registry.Register(new RuntimeDebugComponentDeserializer());
persistenceRegistry.Register(new DebugComponentPersistenceDescriptor());

// SceneAssetReferenceInferenceService.cs
if (component is DebugComponent debugComponent) {
    PopulateOverlayFontAssetReferences(nameof(DebugComponent), debugComponent.Font, saveState);
    return;
}

void PopulateOverlayFontAssetReferences(string componentName, FontAsset font, EntityComponentSaveState saveState) {
    if (saveState.TryGetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, out _)) {
        return;
    }
    if (font == null) {
        return;
    }
    if (FontAssetScenePersistenceSupport.TryResolveEditorCoreFont(font, out SceneAssetReference fontReference)) {
        saveState.SetAssetReference(FontAssetScenePersistenceSupport.FontReferenceName, fontReference);
        return;
    }
    throw new InvalidOperationException(componentName + " Font is assigned but could not be inferred into a stable scene asset reference.");
}
```

In the two new test files, copy the existing `CreateFont()` and `BuildEditorFontReference()` helpers from the FPS persistence and runtime-load tests, then add `WriteDebugComponentPayload()` and `WriteDebugComponentPayloadWithoutFontReference()` by serializing a configured `DebugComponent` through the new descriptor.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsDebugComponent_MaterializesTheComponent|FullyQualifiedName~RuntimeSceneLoadServiceTests.Load_WhenSceneContainsDebugComponentWithoutFontReference_LoadsComponentWithNullFont"`

Expected: PASS with stable `helengine.DebugComponent` persistence and runtime loading.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/DebugComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/serialization/scene/SceneAssetReferenceInferenceService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor.tests/serialization/scene/DebugComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "feat: persist and load debug overlay components"
```

### Task 5: Wire The Editor Catalog And Windows Packaging Path

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorComponentAddCatalog.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/EditorComponentAddCatalogTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing editor-catalog and packaging tests**

```csharp
[Fact]
public void GetAvailableComponents_WhenEntityAlreadyHasDebugComponent_DoesNotIncludeSecondDebugDescriptor() {
    EditorEntity entity = new EditorEntity();
    entity.AddComponent(new DebugComponent());

    IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

    Assert.DoesNotContain(components, component => component.ComponentType == typeof(DebugComponent));
}

[Fact]
public void PackageBuild_WhenSceneContainsDebugComponent_RewritesRuntimePayloadAndFontReference() {
    WriteSceneAsset(sceneId, "Helengine.DebugComponent", WriteDebugComponentPayload(), new[] { CreateEditorFontReference() });

    PackagedSceneAsset packagedScene = LoadPackagedScene(sceneId);
    SceneComponentAssetRecord componentRecord = packagedScene.RootEntities[0].Components[0];

    Assert.Equal("helengine.DebugComponent", componentRecord.ComponentTypeId);
    using MemoryStream stream = new MemoryStream(componentRecord.Payload, false);
    using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
    Assert.Equal(1, reader.ReadByte());
    Assert.NotNull(ReadOptionalReference(reader));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorComponentAddCatalogTests.GetAvailableComponents_WhenEntityAlreadyHasDebugComponent_DoesNotIncludeSecondDebugDescriptor|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.PackageBuild_WhenSceneContainsDebugComponent_RewritesRuntimePayloadAndFontReference"`

Expected: FAIL because the add catalog does not mark `DebugComponent` as single-instance and the packaging transform service has no `helengine.DebugComponent` rewrite branch.

- [ ] **Step 3: Write minimal implementation**

```csharp
// engine/helengine.editor/managers/scene/EditorComponentAddCatalog.cs
bool singleInstance = componentType == typeof(FPSComponent) || componentType == typeof(DebugComponent);

// engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs
const byte DebugComponentPayloadVersion = 1;
const string DebugComponentTypeId = "helengine.DebugComponent";

if (string.Equals(record.ComponentTypeId, DebugComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
    transformedRecord = RewriteDebugComponentRecord(record, buildRootPath);
}

SceneComponentAssetRecord RewriteDebugComponentRecord(SceneComponentAssetRecord record, string buildRootPath) {
    ReadDebugComponentRecord(
        record,
        out SceneAssetReference fontReference,
        out double refreshIntervalSeconds,
        out int2 padding,
        out byte renderOrder2D);

    using MemoryStream writeStream = new MemoryStream();
    using EngineBinaryWriter writer = EngineBinaryWriter.Create(writeStream, EngineBinaryEndianness.LittleEndian);
    writer.WriteByte(DebugComponentPayloadVersion);
    WriteOptionalReference(writer, RewriteFontReference(fontReference, buildRootPath));
    writer.WriteInt64(BitConverter.DoubleToInt64Bits(refreshIntervalSeconds));
    writer.WriteInt2(padding);
    writer.WriteByte(renderOrder2D);
    return new SceneComponentAssetRecord {
        ComponentTypeId = DebugComponentTypeId,
        ComponentIndex = record.ComponentIndex,
        Payload = writeStream.ToArray()
    };
}

void ReadDebugComponentRecord(
    SceneComponentAssetRecord record,
    out SceneAssetReference fontReference,
    out double refreshIntervalSeconds,
    out int2 padding,
    out byte renderOrder2D) {
    EditorTaggedSceneComponentFieldReader taggedReader = new EditorTaggedSceneComponentFieldReader(record.Payload ?? Array.Empty<byte>());
    if (taggedReader.TryGetFieldReader("FontReference", out EngineBinaryReader fontReferenceReader)) {
        using (fontReferenceReader) {
            fontReference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(fontReferenceReader);
        }
        if (!taggedReader.TryGetFieldReader("RefreshIntervalSeconds", out EngineBinaryReader refreshIntervalReader) ||
            !taggedReader.TryGetFieldReader("Padding", out EngineBinaryReader paddingReader) ||
            !taggedReader.TryGetFieldReader("RenderOrder2D", out EngineBinaryReader renderOrderReader)) {
            throw new InvalidOperationException("Tagged Debug component payload is missing one or more required fields.");
        }
        using (refreshIntervalReader)
        using (paddingReader)
        using (renderOrderReader) {
            refreshIntervalSeconds = BitConverter.Int64BitsToDouble(refreshIntervalReader.ReadInt64());
            padding = paddingReader.ReadInt2();
            renderOrder2D = renderOrderReader.ReadByte();
        }
        return;
    }

    using MemoryStream readStream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
    using EngineBinaryReader reader = EngineBinaryReader.Create(readStream, EngineBinaryEndianness.LittleEndian);
    byte version = reader.ReadByte();
    if (version != DebugComponentPayloadVersion) {
        throw new InvalidOperationException($"Unsupported Debug component payload version '{version}'.");
    }
    fontReference = FontAssetScenePersistenceSupport.ReadOptionalReference(reader);
    refreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64());
    padding = reader.ReadInt2();
    renderOrder2D = reader.ReadByte();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorComponentAddCatalogTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"`

Expected: PASS with `DebugComponent` exposed once in the add dialog and packaged through the Windows build path.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorComponentAddCatalog.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/EditorComponentAddCatalogTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: wire debug overlay editor packaging"
```

### Task 6: Run Focused End-To-End Verification

**Files:**
- Verify only: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Run the focused overlay and serialization suite**

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DebugComponentTests|FullyQualifiedName~CoreTimingTests|FullyQualifiedName~DebugComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~EditorComponentAddCatalogTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests"
```

Expected: PASS with the new overlay behavior, scene persistence, runtime load, editor catalog, and packaging paths all green.

- [ ] **Step 2: Run the smallest regression checks that touch the reused FPS paths**

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~FPSComponentTests|FullyQualifiedName~FPSComponentPersistenceDescriptorTests"
```

Expected: PASS to prove the parallel `DebugComponent` work did not break the existing FPS overlay flow.

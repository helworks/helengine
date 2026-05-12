# Fitted Canvas Anchor Space Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `1280x720` and `853x480` resolve the demo menu to the same `16:9` layout while keeping `4:3` adaptation correct through a shared fitted-canvas anchor space.

**Architecture:** Introduce a first-class fitted canvas rect for reference-canvas UI, route anchor resolution through that rect instead of the raw window, and keep pointer/interactable math aligned with the same fitted space. The implementation stays incremental: tests first, then the shared layout contract, then pointer alignment verification.

**Tech Stack:** C#/.NET 9, xUnit, helengine core UI/layout components, editor test harness with `TestRenderManager3D` and `TestInputBackend`.

---

### Task 1: Lock Same-Aspect and 4:3 Layout Expectations

**Files:**
- Modify: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`
- Test: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`
- Test: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`

- [ ] **Step 1: Write the failing same-aspect invariance test for the fit component**

```csharp
[Fact]
public void ComponentAdded_WhenWindowShrinksToMatchingSixteenByNineAspect_PreservesNormalizedLayout() {
    TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
    renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

    Entity menuRoot = CreateEntity(float3.Zero);
    menuRoot.AddComponent(new ViewportComponent {
        BindingMode = ViewportComponent.ScreenBindingMode,
        FixedSize = new int2(1280, 720)
    });

    Entity generatedRoot = CreateEntity(float3.Zero);
    menuRoot.AddChild(generatedRoot);

    Entity panelEntity = CreateEntity(new float3(88f, 190f, 0f));
    RoundedRectComponent panelBackground = new RoundedRectComponent {
        Size = new int2(560, 420)
    };
    AnchorComponent panelAnchor = new AnchorComponent();
    panelEntity.AddComponent(panelBackground);
    panelEntity.AddComponent(panelAnchor);
    generatedRoot.AddChild(panelEntity);
    panelAnchor.SetAnchorDistances(left: 88f, top: 190f);

    menuRoot.AddComponent(new ReferenceCanvasFitComponent {
        ReferenceWidth = 1280,
        ReferenceHeight = 720
    });

    renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
    Core.Instance.Update();

    Assert.Equal(new float3(58.64375f, 126.66667f, 0f), panelEntity.LocalPosition, new Float3Comparer(0.01f));
    Assert.Equal(new int2(373, 280), panelBackground.Size);
    Assert.Equal(new float4(58.64375f, 0f, 126.66667f, 0f), panelAnchor.AnchorDistances, new Float4Comparer(0.01f));
}
```

- [ ] **Step 2: Write the failing fitted-canvas containment test for `853x480`**

```csharp
[Fact]
public void ComponentAdded_WhenWindowIs853x480_KeepsPanelInsideTheVisibleCanvas() {
    TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
    renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

    Entity menuRoot = CreateEntity(float3.Zero);
    menuRoot.AddComponent(new ViewportComponent {
        BindingMode = ViewportComponent.ScreenBindingMode,
        FixedSize = new int2(1280, 720)
    });

    Entity generatedRoot = CreateEntity(float3.Zero);
    menuRoot.AddChild(generatedRoot);

    Entity panelEntity = CreateEntity(new float3(88f, 190f, 0f));
    RoundedRectComponent panelBackground = new RoundedRectComponent {
        Size = new int2(560, 420)
    };
    AnchorComponent panelAnchor = new AnchorComponent();
    panelEntity.AddComponent(panelBackground);
    panelEntity.AddComponent(panelAnchor);
    generatedRoot.AddChild(panelEntity);
    panelAnchor.SetAnchorDistances(left: 88f, top: 190f);

    menuRoot.AddComponent(new ReferenceCanvasFitComponent {
        ReferenceWidth = 1280,
        ReferenceHeight = 720
    });

    renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
    Core.Instance.Update();

    float right = panelEntity.LocalPosition.X + panelBackground.Size.X;
    float bottom = panelEntity.LocalPosition.Y + panelBackground.Size.Y;

    Assert.True(panelEntity.LocalPosition.X >= 0f);
    Assert.True(panelEntity.LocalPosition.Y >= 0f);
    Assert.True(right <= 853f);
    Assert.True(bottom <= 480f);
}
```

- [ ] **Step 3: Write the failing anchor-space regression for same-aspect screen binding**

```csharp
[Fact]
public void AnchorComponent_WhenReferenceCanvasFitUses853x480_Matches1280x720NormalizedPlacement() {
    TestRenderManager3D renderManager = (TestRenderManager3D)Core.Instance.RenderManager3D;
    renderManager.OnWindowResize(IntPtr.Zero, 1280, 720);

    Entity viewportEntity = new Entity();
    viewportEntity.InitComponents();
    viewportEntity.InitChildren();
    viewportEntity.AddComponent(new ViewportComponent {
        BindingMode = ViewportComponent.ScreenBindingMode
    });
    viewportEntity.AddComponent(new ReferenceCanvasFitComponent {
        ReferenceWidth = 1280,
        ReferenceHeight = 720
    });

    Entity contentEntity = new Entity {
        LocalPosition = new float3(88f, 190f, 0f)
    };
    contentEntity.InitComponents();
    contentEntity.InitChildren();
    contentEntity.AddComponent(new RoundedRectComponent {
        Size = new int2(560, 420)
    });
    contentEntity.AddComponent(new AnchorComponent());
    viewportEntity.AddChild(contentEntity);

    AnchorComponent anchor = Assert.IsType<AnchorComponent>(Assert.Single(contentEntity.Components, component => component is AnchorComponent));
    anchor.SetAnchorDistances(left: 88f, top: 190f);

    renderManager.OnWindowResize(IntPtr.Zero, 853, 480);
    Core.Instance.Update();

    Assert.Equal(new float3(58.64375f, 126.66667f, 0f), contentEntity.LocalPosition, new Float3Comparer(0.01f));
}
```

- [ ] **Step 4: Run the focused layout tests to verify they fail for the expected reason**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "ReferenceCanvasFitComponentTests|ViewportAndAnchorLayoutTests" --no-restore
```

Expected:

```text
FAIL with assertions showing `853x480` does not preserve the same normalized layout or leaks outside the fitted canvas.
```

- [ ] **Step 5: Commit the red test baseline**

```bash
git add engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs
git commit -m "test: capture fitted canvas anchor space regressions"
```

### Task 2: Introduce a First-Class Fitted Canvas Rect and Route Anchors Through It

**Files:**
- Create: `engine/helengine.core/model/AnchorSpace.cs`
- Modify: `engine/helengine.core/model/interfaces/IAnchorBoundsProvider.cs`
- Modify: `engine/helengine.core/components/AnchorComponent.cs`
- Modify: `engine/helengine.core/components/ReferenceCanvasFitComponent.cs`
- Modify: `engine/helengine.core/components/ReferenceCanvasFitSnapshot.cs`
- Modify: `engine/helengine.core/components/ViewportComponent.cs`
- Test: `engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs`
- Test: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`

- [ ] **Step 1: Add the failing shared layout-space type usage to the interface and components**

```csharp
public interface IAnchorBoundsProvider {
    AnchorSpace AnchorSpace { get; }
    event Action AnchorBoundsChanged;
}

public sealed class AnchorSpace {
    public AnchorSpace(int2 size, float2 origin) {
        Size = size;
        Origin = origin;
    }

    public int2 Size { get; }
    public float2 Origin { get; }
}
```

- [ ] **Step 2: Run the focused layout tests to verify the interface change breaks compile/test expectations first**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "ReferenceCanvasFitComponentTests|ViewportAndAnchorLayoutTests" --no-restore
```

Expected:

```text
FAIL to build because `IAnchorBoundsProvider` / `AnchorComponent` / `ViewportComponent` do not yet provide the new anchor space contract everywhere.
```

- [ ] **Step 3: Implement the minimal fitted-canvas rect contract**

```csharp
public sealed class AnchorSpace {
    public AnchorSpace(int2 size, float2 origin) {
        Size = size;
        Origin = origin;
    }

    public int2 Size { get; }
    public float2 Origin { get; }
}

public interface IAnchorBoundsProvider {
    AnchorSpace AnchorSpace { get; }
    event Action AnchorBoundsChanged;
}
```

```csharp
AnchorSpace ResolveCurrentAnchorSpace() {
    int2 mainWindowSize = Core.Instance.RenderManager3D.MainWindowSize;
    double liveWidth = mainWindowSize.X > 0 ? mainWindowSize.X : ReferenceWidthValue;
    double liveHeight = mainWindowSize.Y > 0 ? mainWindowSize.Y : ReferenceHeightValue;
    double scale = Math.Min(liveWidth / ReferenceWidthValue, liveHeight / ReferenceHeightValue);
    int fittedWidth = (int)Math.Round(ReferenceWidthValue * scale);
    int fittedHeight = (int)Math.Round(ReferenceHeightValue * scale);
    float originX = (float)((liveWidth - fittedWidth) * 0.5d);
    float originY = (float)((liveHeight - fittedHeight) * 0.5d);
    return new AnchorSpace(new int2(fittedWidth, fittedHeight), new float2(originX, originY));
}
```

```csharp
public void RefreshAnchoring() {
    if (!IsAnchored || Parent == null) {
        return;
    }

    RefreshSubscriptions();
    AnchorSpace anchorSpace = GetAnchorSpace();
    int2 anchorSize = GetAnchorSize();
    float3 localPosition = Parent.LocalPosition;

    if ((AnchorFlags & LeftAnchorFlag) != 0) {
        localPosition.X = anchorSpace.Origin.X + AnchorDistances.X;
    } else if ((AnchorFlags & RightAnchorFlag) != 0) {
        localPosition.X = anchorSpace.Origin.X + anchorSpace.Size.X - AnchorDistances.Y - anchorSize.X;
    }

    if ((AnchorFlags & TopAnchorFlag) != 0) {
        localPosition.Y = anchorSpace.Origin.Y + AnchorDistances.Z;
    } else if ((AnchorFlags & BottomAnchorFlag) != 0) {
        localPosition.Y = anchorSpace.Origin.Y + anchorSpace.Size.Y - AnchorDistances.W - anchorSize.Y;
    }

    Parent.LocalPosition = localPosition;
}
```

```csharp
public void RefreshAnchoring(AnchorSpace anchorSpace) {
    if (TrackedAnchorComponent != null) {
        TrackedAnchorComponent.RefreshAnchoring();
    }
}
```

- [ ] **Step 4: Run the focused layout tests to verify the fitted-canvas anchor-space implementation passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "ReferenceCanvasFitComponentTests|ViewportAndAnchorLayoutTests" --no-restore
```

Expected:

```text
PASS with `1280x720` and `853x480` producing equivalent normalized placement and `640x480` remaining correct.
```

- [ ] **Step 5: Commit the fitted-canvas anchor-space contract**

```bash
git add engine/helengine.core/model/AnchorSpace.cs engine/helengine.core/model/interfaces/IAnchorBoundsProvider.cs engine/helengine.core/components/AnchorComponent.cs engine/helengine.core/components/ReferenceCanvasFitComponent.cs engine/helengine.core/components/ReferenceCanvasFitSnapshot.cs engine/helengine.core/components/ViewportComponent.cs engine/helengine.editor.tests/components/ReferenceCanvasFitComponentTests.cs engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs
git commit -m "feat: route reference canvas anchors through fitted canvas space"
```

### Task 3: Verify Pointer and Interactable Alignment Against the Same Fitted Canvas

**Files:**
- Modify: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`
- Modify: `engine/helengine.core/managers/input/PointerInteractableHitResolver.cs`
- Modify: `engine/helengine.core/managers/input/PointerInteractionSystem.cs`
- Test: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`
- Test: `engine/helengine.editor.tests/ContextMenuInteractionTests.cs`

- [ ] **Step 1: Write the failing pointer-alignment regression for same-aspect fitted canvas**

```csharp
[Fact]
public void ResolveTopInteractableAt_WhenReferenceCanvasFitsTo853x480_HitsTheScaledButton() {
    InitializeCore();
    CameraComponent camera = CreateCamera(new float4(0f, 0f, 1f, 1f), EditorLayerMasks.EditorUi);

    EditorEntity root = new EditorEntity {
        InternalEntity = true,
        LayerMask = EditorLayerMasks.EditorUi
    };
    root.AddComponent(new ViewportComponent {
        BindingMode = ViewportComponent.ScreenBindingMode,
        FixedSize = new int2(1280, 720)
    });
    root.AddComponent(new ReferenceCanvasFitComponent {
        ReferenceWidth = 1280,
        ReferenceHeight = 720
    });

    InteractableComponent interactable = CreateInteractableChild(root, new float3(160f, 240f, 0f), new int2(400, 120), 5);
    Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D).OnWindowResize(IntPtr.Zero, 853, 480);
    Core.Instance.Update();

    IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
        Core.Instance.ObjectManager.Interactables,
        Core.Instance.ObjectManager.Drawables2D,
        camera,
        180,
        180);

    Assert.Same(interactable, hit);
}
```

- [ ] **Step 2: Run the pointer-focused tests to verify they fail for the expected hit-alignment reason**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "PointerInteractableHitResolverTests|ContextMenuInteractionTests" --no-restore
```

Expected:

```text
FAIL because pointer hit resolution still assumes raw window-space interactable placement instead of the fitted canvas result.
```

- [ ] **Step 3: Implement the minimal pointer-alignment changes**

```csharp
public static IInteractable2D ResolveTopInteractableAt(
    List<IInteractable2D> interactables,
    List<IDrawable2D> drawables2D,
    ICamera camera,
    int pointerX,
    int pointerY) {
    // keep the existing visual ordering rules
    // do not introduce a second layout-space transform here
    // rely on the entity positions and interactable sizes already resolved through fitted canvas space
}
```

```csharp
float4 ResolveViewportInWindowSpace(ICamera camera) {
    int2 mainWindowSize = Core.RenderManager3D.MainWindowSize;
    return CameraViewportResolver.ResolveViewport(camera.Viewport, mainWindowSize.X, mainWindowSize.Y);
}
```

```csharp
public static void GetRelativePointerForInteractable(
    IInteractable2D interactable,
    int pointerX,
    int pointerY,
    ICamera camera,
    out int relativeX,
    out int relativeY) {
    float3 position = interactable.Parent.Position;
    relativeX = (int)Math.Round(pointerX - position.X);
    relativeY = (int)Math.Round(pointerY - position.Y);
}
```

- [ ] **Step 4: Run pointer-focused tests, then the full editor suite**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "PointerInteractableHitResolverTests|ContextMenuInteractionTests" --no-restore
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore
```

Expected:

```text
PASS for the pointer-focused tests, then PASS for the full editor test project with zero failures.
```

- [ ] **Step 5: Commit the pointer-alignment verification changes**

```bash
git add engine/helengine.core/managers/input/PointerInteractableHitResolver.cs engine/helengine.core/managers/input/PointerInteractionSystem.cs engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs
git commit -m "test: verify fitted canvas pointer alignment"
```

## Spec Coverage Check

- Same-aspect invariance for `1280x720` and `853x480`: covered by Task 1.
- First-class fitted canvas rect: covered by Task 2.
- Anchors resolving against fitted canvas space instead of raw window: covered by Task 2.
- `4:3` behavior staying correct: covered by Task 1 and reverified in Task 2.
- Pointer/interactable alignment staying in the same layout space: covered by Task 3.

## Placeholder Scan

- No `TODO`, `TBD`, or “similar to” references remain.
- Each task includes concrete file paths, test names, commands, and implementation snippets.
- Commit steps stage only the files from the current task.

## Type Consistency Check

- `AnchorSpace` is introduced as the single shared layout-space type and used consistently in `IAnchorBoundsProvider`, `AnchorComponent`, and `ReferenceCanvasFitComponent`.
- `ReferenceCanvasFitComponent`, `ReferenceCanvasFitSnapshot`, `ViewportComponent`, `PointerInteractableHitResolver`, and `PointerInteractionSystem` are the only runtime seams named in later tasks.
- Test names and command filters match the files listed in each task.

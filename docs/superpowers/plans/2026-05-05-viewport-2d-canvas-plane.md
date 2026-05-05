# Viewport 2D Canvas Plane Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render authored 2D scene content into an always-visible world-space canvas plane in the editor scene viewport, with viewport-owned canvas sizing and depth-based click routing back into the existing 2D selection and gizmo flow.

**Architecture:** Keep one authoritative 2D layout/render path by rendering a dedicated offscreen preview camera into a sampleable render target, then bind that live texture to an internal world-space plane visible to the scene camera. Reuse the core 2D hit-testing rules by extracting a shared interactable hit resolver, then bridge plane hits from `EditorViewportPicker` into that resolver through canvas-coordinate mapping instead of creating a second editing model.

**Tech Stack:** `helengine.core`, `helengine.editor`, DirectX11/Vulkan shader-backed runtime materials, editor scene helper components, xUnit.

---

## File Map

- Create: `engine/helengine.core/managers/input/PointerInteractableHitResolver.cs`
- Modify: `engine/helengine.core/managers/input/PointerInteractionSystem.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneMaterialFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionBridge.cs`
- Create: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Create: `engine/helengine.editor/shaders/builtin/EditorViewportCanvasPlane.hlsl`
- Modify: `engine/helengine.editor/EditorLayerMasks.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`
- Create: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionBridgeTests.cs`

## Execution Order

- [ ] Extract reusable 2D interactable hit resolution out of `PointerInteractionSystem`.
- [ ] Add viewport-owned canvas preview settings and expose them through the existing viewport settings overlay.
- [ ] Add the dedicated canvas-plane layer, shader, material factory, plane factory, and coordinate mapper.
- [ ] Add the preview component that owns the offscreen camera, render target, and internal plane entity, then wire it through `EditorSession`.
- [ ] Route selected plane hits from `EditorViewportPicker` into the shared 2D hit resolver and existing selection service.
- [ ] Run the focused verification suite.

### Task 1: Extract Shared 2D Hit Resolution

**Files:**
- Create: `engine/helengine.core/managers/input/PointerInteractableHitResolver.cs`
- Modify: `engine/helengine.core/managers/input/PointerInteractionSystem.cs`
- Create: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`

- [ ] **Step 1: Write the failing hit-resolver tests**

```csharp
[Fact]
public void ResolveTopInteractableAt_WhenTwoInteractablesOverlap_PrefersHigherRenderOrder() {
    CameraComponent camera = CreateCamera(new float4(0f, 0f, 320f, 180f), EditorLayerMasks.SceneObjects);
    EditorEntity backEntity = CreateInteractableEntity(new float3(10f, 20f, 0f), new int2(100, 60), 2);
    EditorEntity frontEntity = CreateInteractableEntity(new float3(10f, 20f, 0f), new int2(100, 60), 7);

    IInteractable2D hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
        new List<IInteractable2D> {
            Assert.IsType<InteractableComponent>(Assert.Single(backEntity.Components, c => c is InteractableComponent)),
            Assert.IsType<InteractableComponent>(Assert.Single(frontEntity.Components, c => c is InteractableComponent))
        },
        Core.Instance.ObjectManager.Drawables2D,
        camera,
        40,
        50);

    Assert.Same(frontEntity, hit.Parent);
}

[Fact]
public void GetRelativePointerForInteractable_WhenCameraViewportOffsetsPointer_SubtractsViewportAndEntityPosition() {
    CameraComponent camera = CreateCamera(new float4(100f, 50f, 320f, 180f), EditorLayerMasks.SceneObjects);
    EditorEntity entity = CreateInteractableEntity(new float3(32f, 18f, 0f), new int2(40, 30), 3);
    InteractableComponent interactable = Assert.IsType<InteractableComponent>(Assert.Single(entity.Components, c => c is InteractableComponent));

    PointerInteractableHitResolver.GetRelativePointerForInteractable(interactable, 180, 120, camera, out int relativeX, out int relativeY);

    Assert.Equal(48, relativeX);
    Assert.Equal(52, relativeY);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PointerInteractableHitResolverTests -v minimal`

Expected: `FAIL` because `PointerInteractableHitResolver` does not exist yet.

- [ ] **Step 3: Implement the shared hit resolver and refactor the pointer system to use it**

```csharp
namespace helengine {
    /// <summary>
    /// Resolves the top-most 2D interactable under one pointer coordinate for one camera.
    /// </summary>
    public static class PointerInteractableHitResolver {
        public static IInteractable2D ResolveTopInteractableAt(
            List<IInteractable2D> interactables,
            List<IDrawable2D> drawables2D,
            ICamera camera,
            int pointerX,
            int pointerY) {
            if (interactables == null) {
                throw new ArgumentNullException(nameof(interactables));
            } else if (drawables2D == null) {
                throw new ArgumentNullException(nameof(drawables2D));
            } else if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }

            int localPointerX = pointerX - (int)camera.Viewport.X;
            int localPointerY = pointerY - (int)camera.Viewport.Y;
            ushort layerMask = camera.LayerMask;
            IInteractable2D hit = null;
            byte hitRenderOrder = 0;
            int hitDrawableIndex = -1;
            int hitInteractableIndex = -1;

            for (int index = 0; index < interactables.Count; index++) {
                IInteractable2D interactable = interactables[index];
                if ((interactable.Parent.LayerMask & layerMask) == 0) {
                    continue;
                }

                float3 position = interactable.Parent.Position;
                float4 rect = new float4(position.X, position.Y, interactable.Size.X, interactable.Size.Y);
                if (!rect.Contains(localPointerX, localPointerY)) {
                    continue;
                }

                byte candidateRenderOrder = GetTopDrawableRenderOrder(drawables2D, interactable, layerMask, out int candidateDrawableIndex);
                if (hit == null || CandidateIsInFront(candidateRenderOrder, candidateDrawableIndex, index, hitRenderOrder, hitDrawableIndex, hitInteractableIndex)) {
                    hit = interactable;
                    hitRenderOrder = candidateRenderOrder;
                    hitDrawableIndex = candidateDrawableIndex;
                    hitInteractableIndex = index;
                }
            }

            return hit;
        }

        public static void GetRelativePointerForInteractable(IInteractable2D interactable, int pointerX, int pointerY, ICamera camera, out int relativeX, out int relativeY) {
            if (interactable == null) {
                throw new ArgumentNullException(nameof(interactable));
            }

            float2 local = new float2(pointerX, pointerY);
            if (camera != null) {
                local.X -= camera.Viewport.X;
                local.Y -= camera.Viewport.Y;
            }

            float3 position = interactable.Parent.Position;
            relativeX = (int)Math.Round(local.X - position.X);
            relativeY = (int)Math.Round(local.Y - position.Y);
        }
    }
}
```

```csharp
IInteractable2D hit = null;
if (topCamera != null) {
    hit = PointerInteractableHitResolver.ResolveTopInteractableAt(interactables, drawables2D, topCamera, Input.GetMouseX(), Input.GetMouseY());
}

PointerInteractableHitResolver.GetRelativePointerForInteractable(Hovering, Input.GetMouseX(), Input.GetMouseY(), topCamera, out currentPointerX, out currentPointerY);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PointerInteractableHitResolverTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.core/managers/input/PointerInteractableHitResolver.cs engine/helengine.core/managers/input/PointerInteractionSystem.cs engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs
rtk git commit -m "refactor: extract reusable 2d interactable hit resolver"
```

### Task 2: Add Viewport-Owned Canvas Preview Settings And Overlay Controls

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write the failing viewport settings tests**

```csharp
[Fact]
public void CreateViewport_WhenConstructed_UsesDefaultCanvasPreviewSettings() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();

    Assert.Equal(1280, viewport.CanvasPreviewSettings.CanvasWidth);
    Assert.Equal(720, viewport.CanvasPreviewSettings.CanvasHeight);
    Assert.Equal(100, viewport.CanvasPreviewSettings.PixelsPerWorldUnit);
}

[Fact]
public void SetCanvasWidthSliderValue_WhenChanged_UpdatesViewportSettingsImmediately() {
    InitializeCore();
    EditorViewport viewport = CreateViewport();
    EditorViewportSettingsOverlayComponent overlay = GetPrivateField<EditorViewportSettingsOverlayComponent>(viewport, "SettingsOverlayComponent");

    overlay.Open();
    overlay.CanvasWidthSlider.SetValue(1920);
    overlay.CanvasHeightSlider.SetValue(1080);
    overlay.PixelsPerWorldUnitSlider.SetValue(200);

    Assert.Equal(1920, viewport.CanvasPreviewSettings.CanvasWidth);
    Assert.Equal(1080, viewport.CanvasPreviewSettings.CanvasHeight);
    Assert.Equal(200, viewport.CanvasPreviewSettings.PixelsPerWorldUnit);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportSettingsOverlayTests -v minimal`

Expected: `FAIL` because `CanvasPreviewSettings`, `CanvasWidthSlider`, `CanvasHeightSlider`, and `PixelsPerWorldUnitSlider` do not exist yet.

- [ ] **Step 3: Implement viewport-owned settings and overlay controls**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores viewport-local simulated canvas settings used by the world-space 2D preview plane.
    /// </summary>
    public sealed class EditorViewportCanvasPreviewSettings {
        const int DefaultCanvasWidthValue = 1280;
        const int DefaultCanvasHeightValue = 720;
        const int DefaultPixelsPerWorldUnitValue = 100;

        int canvasWidthValue = DefaultCanvasWidthValue;
        int canvasHeightValue = DefaultCanvasHeightValue;
        int pixelsPerWorldUnitValue = DefaultPixelsPerWorldUnitValue;

        public event Action SettingsChanged;

        public int CanvasWidth {
            get { return canvasWidthValue; }
            set {
                int clamped = Math.Max(1, value);
                if (canvasWidthValue == clamped) {
                    return;
                }

                canvasWidthValue = clamped;
                SettingsChanged?.Invoke();
            }
        }

        public int CanvasHeight {
            get { return canvasHeightValue; }
            set {
                int clamped = Math.Max(1, value);
                if (canvasHeightValue == clamped) {
                    return;
                }

                canvasHeightValue = clamped;
                SettingsChanged?.Invoke();
            }
        }

        public int PixelsPerWorldUnit {
            get { return pixelsPerWorldUnitValue; }
            set {
                int clamped = Math.Max(1, value);
                if (pixelsPerWorldUnitValue == clamped) {
                    return;
                }

                pixelsPerWorldUnitValue = clamped;
                SettingsChanged?.Invoke();
            }
        }
    }
}
```

```csharp
public EditorViewportCanvasPreviewSettings CanvasPreviewSettings { get; }

public EditorViewport(CameraComponent camera, FontAsset font, FontAsset snapModifierFont, EditorViewportToolbarIconSet toolbarIcons, EditorUiMetrics metrics) {
    CanvasPreviewSettings = new EditorViewportCanvasPreviewSettings();
    InitializeSettingsOverlay();
}
```

```csharp
readonly EditorViewportCanvasPreviewSettings CanvasPreviewSettings;
EditorSlider CanvasWidthSliderInternal;
EditorSlider CanvasHeightSliderInternal;
EditorSlider PixelsPerWorldUnitSliderInternal;

public EditorSlider CanvasWidthSlider => CanvasWidthSliderInternal;
public EditorSlider CanvasHeightSlider => CanvasHeightSliderInternal;
public EditorSlider PixelsPerWorldUnitSlider => PixelsPerWorldUnitSliderInternal;

void HandleCanvasWidthChanged(double value) {
    CanvasPreviewSettings.CanvasWidth = Math.Max(1, (int)Math.Round(value));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportSettingsOverlayTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
rtk git commit -m "feat: add viewport canvas preview settings"
```

### Task 3: Add Canvas Plane Layer, Shader, Material, Factory, And Coordinate Mapping

**Files:**
- Modify: `engine/helengine.editor/EditorLayerMasks.cs`
- Create: `engine/helengine.editor/shaders/builtin/EditorViewportCanvasPlane.hlsl`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneMaterialFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs`

- [ ] **Step 1: Write the failing plane-factory and coordinate-mapper tests**

```csharp
[Fact]
public void Create_WhenCalled_BuildsInternalCanvasPlaneOnDedicatedLayer() {
    TestRenderManager3D render3D = new TestRenderManager3D();
    TestRenderTarget renderTarget = new TestRenderTarget { Width = 1280, Height = 720 };

    EditorEntity planeEntity = EditorViewportCanvasPlaneFactory.Create(render3D, renderTarget);
    MeshComponent mesh = Assert.IsType<MeshComponent>(Assert.Single(planeEntity.Components, component => component is MeshComponent));

    Assert.True(planeEntity.InternalEntity);
    Assert.Equal(EditorLayerMasks.SceneCanvasPlane, planeEntity.LayerMask);
    Assert.NotNull(mesh.Model);
    Assert.NotNull(mesh.Material.ResolveTexture());
}

[Fact]
public void MapWorldPointToCanvas_WhenUsingDefaultScale_ReturnsExpectedPixels() {
    EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
    int2 pixel = EditorViewportCanvasPlaneCoordinateMapper.MapWorldToCanvas(new float3(6.4f, 3.6f, 0f), settings);

    Assert.Equal(new int2(640, 360), pixel);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlaneFactoryTests -v minimal`

Expected: `FAIL` because the new plane layer, shader, factories, and mapper do not exist yet.

- [ ] **Step 3: Implement the plane layer, shader, material factory, plane factory, and mapper**

```csharp
public static class EditorLayerMasks {
    public const ushort EditorUi = 0b1000000000000000;
    public const ushort SceneObjects = 0b0100000000000000;
    public const ushort SceneGizmo = 0b0010000000000000;
    public const ushort SceneGrid = 0b0001000000000000;
    public const ushort SceneCameraVisuals = 0b0000100000000000;
    public const ushort SceneCanvasPlane = 0b0000010000000000;
}
```

```hlsl
cbuffer TransformBuffer : register(b0) {
    float4x4 worldViewProjection;
};

Texture2D CanvasTexture : register(t0);
SamplerState CanvasSampler : register(s0);

struct VSInput {
    float3 position : POSITION;
    float3 normal : NORMAL;
    float2 texCoord : TEXCOORD0;
};

struct PSInput {
    float4 position : SV_POSITION;
    float2 texCoord : TEXCOORD0;
};

PSInput EditorViewportCanvasPlane_vs(VSInput input) {
    PSInput output;
    output.position = mul(float4(input.position, 1.0f), worldViewProjection);
    output.texCoord = input.texCoord;
    return output;
}

float4 EditorViewportCanvasPlane_ps(PSInput input) : SV_TARGET {
    return CanvasTexture.Sample(CanvasSampler, input.texCoord);
}
```

```csharp
public static RuntimeMaterial Create(RenderManager3D render3D, RuntimeTexture canvasTexture) {
    ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ResolveTarget(render3D), "EditorViewportCanvasPlane.hlsl");
    var materialAsset = new MaterialAsset {
        Id = "EditorViewportCanvasPlane.material",
        ShaderAssetId = shaderAsset.Id,
        VertexProgram = "EditorViewportCanvasPlane.vs",
        PixelProgram = "EditorViewportCanvasPlane.ps",
        Variant = "default",
        RenderState = new MaterialRenderState {
            BlendMode = MaterialBlendMode.AlphaBlend,
            CullMode = MaterialCullMode.None,
            DepthTestEnabled = true,
            DepthWriteEnabled = false
        }
    };

    RuntimeMaterial material = render3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
    material.Properties.SetTexture("CanvasTexture", canvasTexture);
    return material;
}
```

```csharp
public static int2 MapWorldToCanvas(float3 worldPoint, EditorViewportCanvasPreviewSettings settings) {
    return new int2(
        (int)Math.Round(worldPoint.X * settings.PixelsPerWorldUnit),
        (int)Math.Round(worldPoint.Y * settings.PixelsPerWorldUnit));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlaneFactoryTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorLayerMasks.cs engine/helengine.editor/shaders/builtin/EditorViewportCanvasPlane.hlsl engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneMaterialFactory.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneFactory.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs
rtk git commit -m "feat: add viewport canvas plane render primitives"
```

### Task 4: Add The Offscreen Preview Component And Wire It Into The Editor Session

**Files:**
- Create: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs`

- [ ] **Step 1: Write the failing preview-component tests**

```csharp
[Fact]
public void ComponentAdded_WhenAttached_CreatesDefaultSizedRenderTargetAndPlane() {
    Core core = CreateCore();
    CameraComponent sceneCamera = CreateSceneCamera();
    EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
    EditorEntity cameraEntity = Assert.IsType<EditorEntity>(sceneCamera.Parent);
    var component = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, settings, Core.Instance.RenderManager3D);

    cameraEntity.AddComponent(component);
    component.Update();

    TestRenderTarget target = Assert.IsType<TestRenderTarget>(component.PreviewRenderTarget);
    Assert.Equal(1280, target.Width);
    Assert.Equal(720, target.Height);
    Assert.Equal(new float3(6.4f, 3.6f, 0f), component.PlaneEntity.LocalPosition);
    Assert.Equal(new float3(12.8f, 7.2f, 1f), component.PlaneEntity.LocalScale);
}

[Fact]
public void Update_WhenCanvasSettingsChange_RebuildsRenderTargetAndPlaneScale() {
    Core core = CreateCore();
    CameraComponent sceneCamera = CreateSceneCamera();
    EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
    EditorEntity cameraEntity = Assert.IsType<EditorEntity>(sceneCamera.Parent);
    var component = new EditorViewportCanvasPlanePreviewComponent(sceneCamera, settings, Core.Instance.RenderManager3D);
    cameraEntity.AddComponent(component);
    component.Update();
    RenderTarget initialTarget = component.PreviewRenderTarget;

    settings.CanvasWidth = 1920;
    settings.CanvasHeight = 1080;
    settings.PixelsPerWorldUnit = 200;
    component.Update();

    Assert.NotSame(initialTarget, component.PreviewRenderTarget);
    Assert.Equal(new float3(4.8f, 2.7f, 0f), component.PlaneEntity.LocalPosition);
    Assert.Equal(new float3(9.6f, 5.4f, 1f), component.PlaneEntity.LocalScale);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests -v minimal`

Expected: `FAIL` because the preview component does not exist yet.

- [ ] **Step 3: Implement the preview component and session wiring**

```csharp
public sealed class EditorViewportCanvasPlanePreviewComponent : UpdateComponent {
    readonly CameraComponent SceneCamera;
    readonly EditorViewportCanvasPreviewSettings Settings;
    readonly RenderManager3D Render3D;

    EditorEntity PreviewCameraEntity;
    CameraComponent PreviewCameraComponent;
    RenderTarget PreviewRenderTargetValue;
    EditorEntity PlaneEntityValue;
    RuntimeMaterial PlaneMaterial;

    public RenderTarget PreviewRenderTarget => PreviewRenderTargetValue;
    public EditorEntity PlaneEntity => PlaneEntityValue;
    public CameraComponent PreviewCamera => PreviewCameraComponent;

    public override void ComponentAdded(Entity entity) {
        PreviewCameraEntity = CreatePreviewCameraEntity();
        PlaneEntityValue = EditorViewportCanvasPlaneFactory.Create(Render3D, PreviewRenderTargetValue);
        Parent.AddChild(PlaneEntityValue);
        Core.Instance.ObjectManager.RegisterEntity(PreviewCameraEntity);
    }

    public override void Update() {
        EnsureRenderTargetMatchesSettings();
        PreviewCameraComponent.RenderQueue3D.Clear();
        SyncPlaneTransform();
        PlaneMaterial.Properties.SetTexture("CanvasTexture", PreviewRenderTargetValue);
    }
}
```

```csharp
sceneCameraComponent.LayerMask = EditorLayerMasks.SceneObjects | EditorLayerMasks.SceneGrid | EditorLayerMasks.SceneCameraVisuals | EditorLayerMasks.SceneCanvasPlane;
mainViewport = new EditorViewport(sceneCameraComponent, uiFont, snapModifierFont, toolbarIcons, CurrentUiMetrics);
var canvasPreviewComponent = new EditorViewportCanvasPlanePreviewComponent(sceneCameraComponent, mainViewport.CanvasPreviewSettings, render3D);
sceneCameraEntity.AddComponent(canvasPreviewComponent);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs
rtk git commit -m "feat: add viewport canvas plane preview component"
```

### Task 5: Bridge Plane Hits Into The Existing 2D Selection Flow

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionBridge.cs`
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionBridgeTests.cs`

- [ ] **Step 1: Write the failing selection-bridge tests**

```csharp
[Fact]
public void TryResolveSelection_WhenCanvasPointHitsInteractable_ReturnsSelectableEntity() {
    CameraComponent previewCamera = CreatePreviewCamera(new float4(0f, 0f, 1280f, 720f));
    EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
    EditorEntity canvasEntity = CreateInteractableEntity(new float3(100f, 200f, 0f), new int2(240, 90), 9);
    EditorEntity planeEntity = new EditorEntity {
        InternalEntity = true,
        LayerMask = EditorLayerMasks.SceneCanvasPlane,
        LocalPosition = new float3(6.4f, 3.6f, 0f),
        LocalScale = new float3(12.8f, 7.2f, 1f)
    };
    var bridge = new EditorViewportCanvasPlaneSelectionBridge(previewCamera, planeEntity, settings);

    bool resolved = bridge.TryResolveSelectionFromWorldHit(new float3(1.8f, 2.4f, 0f), out Entity selectedEntity);

    Assert.True(resolved);
    Assert.Same(canvasEntity, selectedEntity);
}

[Fact]
public void TryResolveSelection_WhenCanvasPointMissesInteractables_ReturnsFalse() {
    CameraComponent previewCamera = CreatePreviewCamera(new float4(0f, 0f, 1280f, 720f));
    EditorViewportCanvasPreviewSettings settings = new EditorViewportCanvasPreviewSettings();
    EditorEntity planeEntity = new EditorEntity {
        InternalEntity = true,
        LayerMask = EditorLayerMasks.SceneCanvasPlane,
        LocalPosition = new float3(6.4f, 3.6f, 0f),
        LocalScale = new float3(12.8f, 7.2f, 1f)
    };
    var bridge = new EditorViewportCanvasPlaneSelectionBridge(previewCamera, planeEntity, settings);

    bool resolved = bridge.TryResolveSelectionFromWorldHit(new float3(11.9f, 6.8f, 0f), out Entity selectedEntity);

    Assert.False(resolved);
    Assert.Null(selectedEntity);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlaneSelectionBridgeTests -v minimal`

Expected: `FAIL` because the selection bridge does not exist yet.

- [ ] **Step 3: Implement the selection bridge and picker special case**

```csharp
public sealed class EditorViewportCanvasPlaneSelectionBridge {
    readonly CameraComponent PreviewCamera;
    readonly EditorEntity PlaneEntity;
    readonly EditorViewportCanvasPreviewSettings Settings;

    public bool TryResolveSelectionFromWorldHit(float3 worldHit, out Entity selectedEntity) {
        selectedEntity = null;
        int2 canvasPoint = EditorViewportCanvasPlaneCoordinateMapper.MapWorldToCanvas(worldHit, Settings);
        IInteractable2D interactable = PointerInteractableHitResolver.ResolveTopInteractableAt(
            Core.Instance.ObjectManager.Interactables,
            Core.Instance.ObjectManager.Drawables2D,
            PreviewCamera,
            canvasPoint.X,
            canvasPoint.Y);
        if (interactable == null) {
            return false;
        }

        if (!EditorViewportSceneSelectionFilter.ShouldSelectEntity(interactable.Parent)) {
            return false;
        }

        selectedEntity = interactable.Parent;
        return true;
    }
}
```

```csharp
readonly EditorViewportCanvasPlanePreviewComponent CanvasPlanePreviewComponent;

if (ReferenceEquals(entity, CanvasPlanePreviewComponent.PlaneEntity)) {
    if (CanvasPlanePreviewComponent.TryResolveSelectionFromViewportPointer(PendingPointer, out Entity planeSelection)) {
        EditorSelectionService.SetSelectedEntity(planeSelection);
    } else if (ShouldClearSelectionForMissedPick()) {
        EditorSelectionService.ClearSelection();
    }

    return;
}
```

```csharp
sceneCameraEntity.AddComponent(new EditorViewportPicker(
    sceneCameraComponent,
    gizmoCameraComponent,
    hiddenCameraEntity,
    hiddenCameraComponent,
    pickerRenderer,
    canvasPreviewComponent));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorViewportCanvasPlaneSelectionBridgeTests -v minimal`

Expected: `PASS`

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionBridge.cs engine/helengine.editor/components/EditorViewportPicker.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionBridgeTests.cs
rtk git commit -m "feat: route viewport canvas plane picks into 2d selection"
```

### Task 6: Run Focused Verification

**Files:**
- Verify: `engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs`
- Verify: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`
- Verify: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs`
- Verify: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs`
- Verify: `engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionBridgeTests.cs`

- [ ] **Step 1: Run the focused viewport canvas-plane suite**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PointerInteractableHitResolverTests|FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorViewportCanvasPlaneFactoryTests|FullyQualifiedName~EditorViewportCanvasPlanePreviewComponentTests|FullyQualifiedName~EditorViewportCanvasPlaneSelectionBridgeTests" -v minimal`

Expected: `PASS`

- [ ] **Step 2: Run the existing viewport interaction regressions**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportGridToggleTests|FullyQualifiedName~EditorViewportKeyboardFocusTests|FullyQualifiedName~EditorViewportCameraControllerTests|FullyQualifiedName~CameraPreviewSourceTests" -v minimal`

Expected: `PASS`

- [ ] **Step 3: Commit**

```bash
rtk git add engine/helengine.core/managers/input/PointerInteractableHitResolver.cs engine/helengine.core/managers/input/PointerInteractionSystem.cs engine/helengine.editor/EditorLayerMasks.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor/components/EditorViewportPicker.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPreviewSettings.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneCoordinateMapper.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneMaterialFactory.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneFactory.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionBridge.cs engine/helengine.editor/shaders/builtin/EditorViewportCanvasPlane.hlsl engine/helengine.editor.tests/PointerInteractableHitResolverTests.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneFactoryTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlanePreviewComponentTests.cs engine/helengine.editor.tests/managers/scene/EditorViewportCanvasPlaneSelectionBridgeTests.cs
rtk git commit -m "feat: add world-space 2d canvas plane to scene viewport"
```

## Self-Review

**Spec coverage:** The plan covers viewport-owned canvas width, height, and pixels-per-world-unit settings in Task 2. It covers the always-visible world-space plane, bottom-left anchoring, and live render-target texturing in Tasks 3 and 4. It covers depth-based plane picking and reuse of the existing 2D hit-testing and selection path in Tasks 1 and 5. Focused verification and regression protection are covered in Task 6.

**Placeholder scan:** No `TBD`, `TODO`, or deferred implementation language remains in the task steps. Each task names exact files, concrete test targets, concrete commands, and concrete implementation seams.

**Type consistency:** The plan uses one consistent set of names across tasks: `EditorViewportCanvasPreviewSettings`, `EditorViewportCanvasPlaneCoordinateMapper`, `EditorViewportCanvasPlaneMaterialFactory`, `EditorViewportCanvasPlaneFactory`, `EditorViewportCanvasPlanePreviewComponent`, `EditorViewportCanvasPlaneSelectionBridge`, and `PointerInteractableHitResolver`.

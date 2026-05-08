# Demo Menu Responsive Anchor Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the demo-disc menu authored against `1280x720`, but make the scene use engine anchors and bind runtime layout to reusable viewport contracts so the menu runs correctly at `480p`, `4:3`, and `16:9` without letterboxing.

**Architecture:** Add a small reusable viewport/layout capability layer first. Cameras get an authored viewport-binding component that can preserve scene-authored rectangles or bind to the current screen, while anchor bounds hosts can resolve their bounds from fixed values, the screen, or a referenced camera viewport. Then bake the demo menu scene with anchor metadata plus a responsive menu layout component that recomputes shell sizes and positions from the active viewport contract.

**Tech Stack:** C#, xUnit, scene persistence descriptors, runtime component deserializers, menu scene asset generation, editor camera preview plumbing.

---

## File Map

- Create: `engine/helengine.core/model/AnchorBoundsSourceKind.cs`
- Create: `engine/helengine.core/model/CameraViewportBindingMode.cs`
- Create: `engine/helengine.core/components/AnchorBoundsHostComponent.cs`
- Create: `engine/helengine.core/components/CameraViewportBindingComponent.cs`
- Create: `engine/helengine.core/components/2d/menu/MenuResponsiveLayoutComponent.cs`
- Modify: `engine/helengine.core/components/AnchorComponent.cs`
- Modify: `engine/helengine.core/components/2d/RoundedRectComponent.cs`
- Modify: `engine/helengine.core/components/2d/TextComponent.cs`
- Create: `engine/helengine.editor/serialization/scene/AnchorComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/AnchorBoundsHostComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/CameraViewportBindingComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuResponsiveLayoutComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeAnchorComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeAnchorBoundsHostComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeCameraViewportBindingComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuResponsiveLayoutComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/preview/CameraPreviewSource.cs`
- Modify: `engine/helengine.core/components/2d/menu/DemoMenuLayout.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Create: `engine/helengine.editor.tests/AnchorComponentTests.cs`
- Create: `engine/helengine.editor.tests/AnchorBoundsHostComponentTests.cs`
- Create: `engine/helengine.editor.tests/CameraViewportBindingComponentTests.cs`
- Modify: `engine/helengine.editor.tests/CameraPreviewSourceTests.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

## Execution Order

- [ ] Extend anchor infrastructure so generic scene entities can actually consume component-based anchor bounds and component-based size providers.
- [ ] Add reusable viewport-binding and anchor-bounds-host components, plus explicit editor/runtime serialization support.
- [ ] Update editor preview plumbing so scene cameras that bind to the screen preview against the active runtime-sized viewport instead of frozen authored rectangles.
- [ ] Re-bake the demo menu scene with viewport-binding, anchor-bounds hosts, anchor components, and a responsive layout component.
- [ ] Implement the responsive menu layout rules and prove they work in packaged runtime behavior and scene regeneration.

### Task 1: Make Anchors Work With Scene Components

**Files:**
- Create: `engine/helengine.editor.tests/AnchorComponentTests.cs`
- Modify: `engine/helengine.core/components/AnchorComponent.cs`
- Modify: `engine/helengine.core/components/2d/RoundedRectComponent.cs`
- Modify: `engine/helengine.core/components/2d/TextComponent.cs`

- [ ] **Step 1: Write the failing anchor-provider and size-provider tests**

Create `engine/helengine.editor.tests/AnchorComponentTests.cs` with focused coverage for the current gaps:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies anchor resolution against component-based bounds and size providers.
    /// </summary>
    public sealed class AnchorComponentTests {
        /// <summary>
        /// Ensures anchor components can resolve bounds from ancestor components, not only ancestor entity types.
        /// </summary>
        [Fact]
        public void RefreshAnchoring_WhenAncestorCarriesAnchorBoundsHostComponent_UsesComponentBounds() {
            InitializeCore(640, 480);
            Entity root = new Entity();
            root.InitComponents();
            root.InitChildren();
            AnchorBoundsHostComponent boundsHost = new AnchorBoundsHostComponent {
                SourceKind = AnchorBoundsSourceKind.Fixed,
                FixedBounds = new int2(320, 180)
            };
            root.AddComponent(boundsHost);

            Entity child = new Entity {
                LocalPosition = new float3(0f, 0f, 0f)
            };
            child.InitComponents();
            child.InitChildren();
            RoundedRectComponent surface = new RoundedRectComponent {
                Size = new int2(100, 40)
            };
            AnchorComponent anchor = new AnchorComponent();
            child.AddComponent(surface);
            child.AddComponent(anchor);
            root.AddChild(child);

            anchor.SetAnchorDistances(right: 12f, bottom: 18f);

            Assert.Equal(new float3(208f, 122f, 0f), child.LocalPosition);
        }

        /// <summary>
        /// Ensures rounded rectangles expose their size to anchor calculations.
        /// </summary>
        [Fact]
        public void RefreshAnchoring_WhenParentUsesRoundedRectComponent_UsesRoundedRectSize() {
            InitializeCore(640, 480);
            Entity root = new Entity();
            root.InitComponents();
            root.InitChildren();
            AnchorBoundsHostComponent boundsHost = new AnchorBoundsHostComponent {
                SourceKind = AnchorBoundsSourceKind.Fixed,
                FixedBounds = new int2(500, 300)
            };
            root.AddComponent(boundsHost);

            Entity child = new Entity();
            child.InitComponents();
            child.InitChildren();
            RoundedRectComponent surface = new RoundedRectComponent {
                Size = new int2(160, 60)
            };
            AnchorComponent anchor = new AnchorComponent();
            child.AddComponent(surface);
            child.AddComponent(anchor);
            root.AddChild(child);

            anchor.SetAnchorDistances(right: 20f, bottom: 30f);

            Assert.Equal(new float3(320f, 210f, 0f), child.LocalPosition);
        }
    }
}
```

- [ ] **Step 2: Run the new anchor tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnchorComponentTests" -v minimal
```

Expected:

```text
FAIL
```

The failure should show that `AnchorComponent` does not resolve `IAnchorBoundsProvider` from ancestor components yet and that `RoundedRectComponent` does not expose anchor size.

- [ ] **Step 3: Teach `AnchorComponent` to resolve provider components and expose persisted distances**

In `AnchorComponent.cs`, add read-only accessors for persisted distances and update provider lookup so it inspects ancestor components before moving up the hierarchy:

```csharp
/// <summary>
/// Gets the stored left anchor distance in pixels.
/// </summary>
public float? LeftDistance => anchorData?.LeftDistance;

/// <summary>
/// Gets the stored right anchor distance in pixels.
/// </summary>
public float? RightDistance => anchorData?.RightDistance;

/// <summary>
/// Gets the stored top anchor distance in pixels.
/// </summary>
public float? TopDistance => anchorData?.TopDistance;

/// <summary>
/// Gets the stored bottom anchor distance in pixels.
/// </summary>
public float? BottomDistance => anchorData?.BottomDistance;

IAnchorBoundsProvider ResolveAnchorBoundsProvider() {
    Entity current = Parent;

    while (current != null) {
        if (current is IAnchorBoundsProvider entityProvider) {
            return entityProvider;
        }

        if (current.Components != null) {
            for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                if (current.Components[componentIndex] is IAnchorBoundsProvider componentProvider) {
                    return componentProvider;
                }
            }
        }

        current = current.Parent;
    }

    return null;
}
```

- [ ] **Step 4: Make rounded rects and text advertise anchor size**

Update the drawable components so anchor calculations can use their actual size:

```csharp
public class RoundedRectComponent : Component, IRoundedRectDrawable2D, IAnchorSizeProvider {
    /// <summary>
    /// Gets the current size used by anchor calculations.
    /// </summary>
    public int2 AnchorSize => Size;
}
```

```csharp
public class TextComponent : Component, ITextDrawable2D, IAnchorSizeProvider {
    /// <summary>
    /// Gets the layout size used by anchor calculations.
    /// </summary>
    public int2 AnchorSize => Size;
}
```

- [ ] **Step 5: Re-run the anchor tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnchorComponentTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 6: Commit the generic anchor-seam fixes**

```bash
git add engine/helengine.core/components/AnchorComponent.cs engine/helengine.core/components/2d/RoundedRectComponent.cs engine/helengine.core/components/2d/TextComponent.cs engine/helengine.editor.tests/AnchorComponentTests.cs
git commit -m "refactor: let anchors use component bounds and size providers"
```

### Task 2: Add Reusable Viewport And Anchor-Bounds Components

**Files:**
- Create: `engine/helengine.core/model/AnchorBoundsSourceKind.cs`
- Create: `engine/helengine.core/model/CameraViewportBindingMode.cs`
- Create: `engine/helengine.core/components/AnchorBoundsHostComponent.cs`
- Create: `engine/helengine.core/components/CameraViewportBindingComponent.cs`
- Create: `engine/helengine.editor/serialization/scene/AnchorComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/AnchorBoundsHostComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.editor/serialization/scene/CameraViewportBindingComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeAnchorComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeAnchorBoundsHostComponentDeserializer.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeCameraViewportBindingComponentDeserializer.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Create: `engine/helengine.editor.tests/AnchorBoundsHostComponentTests.cs`
- Create: `engine/helengine.editor.tests/CameraViewportBindingComponentTests.cs`

- [ ] **Step 1: Write the failing bounds-host and viewport-binding tests**

Create two focused test files:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the reusable anchor-bounds host component.
    /// </summary>
    public sealed class AnchorBoundsHostComponentTests {
        /// <summary>
        /// Ensures screen-bound hosts report the current render-window size.
        /// </summary>
        [Fact]
        public void AnchorBounds_WhenSourceKindIsScreen_UsesMainWindowSize() {
            InitializeCore(854, 480);
            AnchorBoundsHostComponent component = new AnchorBoundsHostComponent {
                SourceKind = AnchorBoundsSourceKind.Screen
            };

            Assert.Equal(new int2(854, 480), component.AnchorBounds);
        }
    }
}
```

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies camera viewport binding behavior.
    /// </summary>
    public sealed class CameraViewportBindingComponentTests {
        /// <summary>
        /// Ensures screen-bound camera viewports follow the active render-window size.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenModeIsScreen_BindsViewportToMainWindow() {
            InitializeCore(640, 480);
            Entity cameraEntity = new Entity();
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();

            CameraComponent camera = new CameraComponent {
                Viewport = new float4(0f, 0f, 1280f, 720f)
            };
            CameraViewportBindingComponent binding = new CameraViewportBindingComponent {
                Mode = CameraViewportBindingMode.Screen
            };

            cameraEntity.AddComponent(camera);
            cameraEntity.AddComponent(binding);

            Assert.Equal(new float4(0f, 0f, 640f, 480f), camera.Viewport);
        }
    }
}
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnchorBoundsHostComponentTests|FullyQualifiedName~CameraViewportBindingComponentTests" -v minimal
```

Expected:

```text
FAIL
```

- [ ] **Step 3: Implement the source-kind and viewport-binding enums**

Create the two shared enums:

```csharp
namespace helengine {
    /// <summary>
    /// Describes how one anchor-bounds host resolves its active layout bounds.
    /// </summary>
    public enum AnchorBoundsSourceKind : byte {
        Fixed = 0,
        Screen = 1,
        CameraViewport = 2
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Describes how one camera should resolve its viewport rectangle at runtime.
    /// </summary>
    public enum CameraViewportBindingMode : byte {
        Authored = 0,
        Screen = 1
    }
}
```

- [ ] **Step 4: Implement `AnchorBoundsHostComponent` and `CameraViewportBindingComponent`**

Add the reusable components with explicit serialized ids:

```csharp
namespace helengine {
    /// <summary>
    /// Publishes anchor bounds for descendant anchor components from fixed data, the screen, or a referenced camera viewport.
    /// </summary>
    public sealed class AnchorBoundsHostComponent : UpdateComponent, IAnchorBoundsProvider {
        public const byte CurrentVersion = 1;
        public const string SerializedComponentTypeId = "helengine.AnchorBoundsHostComponent";

        int2 fixedBoundsValue = new int2(1280, 720);
        int2 lastResolvedBounds;

        public AnchorBoundsSourceKind SourceKind { get; set; }

        public int2 FixedBounds {
            get { return fixedBoundsValue; }
            set { fixedBoundsValue = value; }
        }

        public SceneEntityReference CameraEntityReference { get; set; }

        public int2 AnchorBounds => ResolveBounds();

        public event Action AnchorBoundsChanged;

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            lastResolvedBounds = ResolveBounds();
        }

        public override void Update() {
            int2 resolvedBounds = ResolveBounds();
            if (resolvedBounds != lastResolvedBounds) {
                lastResolvedBounds = resolvedBounds;
                AnchorBoundsChanged?.Invoke();
            }
        }

        int2 ResolveBounds() {
            if (SourceKind == AnchorBoundsSourceKind.Screen) {
                return Core.Instance.RenderManager3D.MainWindowSize;
            }
            if (SourceKind == AnchorBoundsSourceKind.CameraViewport) {
                CameraComponent camera = ResolveReferencedCamera();
                return new int2(
                    Math.Max(1, (int)Math.Round(camera.Viewport.Z)),
                    Math.Max(1, (int)Math.Round(camera.Viewport.W)));
            }

            return new int2(Math.Max(1, fixedBoundsValue.X), Math.Max(1, fixedBoundsValue.Y));
        }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Applies an authored viewport binding rule to the sibling camera component.
    /// </summary>
    public sealed class CameraViewportBindingComponent : UpdateComponent {
        public const byte CurrentVersion = 1;
        public const string SerializedComponentTypeId = "helengine.CameraViewportBindingComponent";

        public CameraViewportBindingMode Mode { get; set; }

        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            ApplyBinding();
        }

        public override void Update() {
            ApplyBinding();
        }

        void ApplyBinding() {
            if (Mode != CameraViewportBindingMode.Screen) {
                return;
            }

            CameraComponent camera = ResolveCamera();
            int2 size = Core.Instance.RenderManager3D.MainWindowSize;
            camera.Viewport = new float4(0f, 0f, Math.Max(1, size.X), Math.Max(1, size.Y));
        }
    }
}
```

- [ ] **Step 5: Add explicit scene persistence and runtime deserializers**

Persist anchor distances and the reusable component state through explicit descriptors instead of relying on reflected private fields:

```csharp
public sealed class AnchorComponentPersistenceDescriptor : IComponentPersistenceDescriptor {
    public Type ComponentType => typeof(AnchorComponent);
    public string ComponentTypeId => AnchorComponent.SerializedComponentTypeId;

    public SceneComponentAssetRecord SerializeComponent(Component component, int componentIndex, EntityComponentSaveState saveState) {
        AnchorComponent anchorComponent = component as AnchorComponent
            ?? throw new InvalidOperationException("Anchor descriptor received an unsupported component type.");
        EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
        if (anchorComponent.LeftDistance.HasValue) {
            writer.WriteField("LeftDistance", fieldWriter => fieldWriter.WriteSingle(anchorComponent.LeftDistance.Value));
        }
        if (anchorComponent.RightDistance.HasValue) {
            writer.WriteField("RightDistance", fieldWriter => fieldWriter.WriteSingle(anchorComponent.RightDistance.Value));
        }
        if (anchorComponent.TopDistance.HasValue) {
            writer.WriteField("TopDistance", fieldWriter => fieldWriter.WriteSingle(anchorComponent.TopDistance.Value));
        }
        if (anchorComponent.BottomDistance.HasValue) {
            writer.WriteField("BottomDistance", fieldWriter => fieldWriter.WriteSingle(anchorComponent.BottomDistance.Value));
        }

        return new SceneComponentAssetRecord {
            ComponentTypeId = ComponentTypeId,
            ComponentIndex = componentIndex,
            Payload = writer.BuildPayload()
        };
    }
}
```

Register the new descriptors in:

```csharp
persistenceRegistry.Register(new AnchorComponentPersistenceDescriptor());
persistenceRegistry.Register(new AnchorBoundsHostComponentPersistenceDescriptor());
persistenceRegistry.Register(new CameraViewportBindingComponentPersistenceDescriptor());
```

Register the runtime deserializers in:

```csharp
registry.Register(new RuntimeAnchorComponentDeserializer());
registry.Register(new RuntimeAnchorBoundsHostComponentDeserializer());
registry.Register(new RuntimeCameraViewportBindingComponentDeserializer());
```

- [ ] **Step 6: Re-run the focused viewport and bounds-host tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AnchorBoundsHostComponentTests|FullyQualifiedName~CameraViewportBindingComponentTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 7: Commit the reusable viewport/bounds infrastructure**

```bash
git add engine/helengine.core/model/AnchorBoundsSourceKind.cs engine/helengine.core/model/CameraViewportBindingMode.cs engine/helengine.core/components/AnchorBoundsHostComponent.cs engine/helengine.core/components/CameraViewportBindingComponent.cs engine/helengine.editor/serialization/scene/AnchorComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/AnchorBoundsHostComponentPersistenceDescriptor.cs engine/helengine.editor/serialization/scene/CameraViewportBindingComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeAnchorComponentDeserializer.cs engine/helengine.core/scene/runtime/RuntimeAnchorBoundsHostComponentDeserializer.cs engine/helengine.core/scene/runtime/RuntimeCameraViewportBindingComponentDeserializer.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/AnchorBoundsHostComponentTests.cs engine/helengine.editor.tests/CameraViewportBindingComponentTests.cs
git commit -m "feat: add reusable viewport and anchor bounds components"
```

### Task 3: Make Editor Camera Preview Respect Screen-Bound Viewports

**Files:**
- Modify: `engine/helengine.editor/managers/preview/CameraPreviewSource.cs`
- Modify: `engine/helengine.editor.tests/CameraPreviewSourceTests.cs`

- [ ] **Step 1: Write the failing preview regression**

Add a focused test to `CameraPreviewSourceTests.cs`:

```csharp
/// <summary>
/// Ensures screen-bound scene cameras preview against the current render-window size instead of the authored viewport rectangle.
/// </summary>
[Fact]
public void Resize_WhenSuppressedSceneCameraBindsViewportToScreen_UsesCurrentWindowSize() {
    EditorEntity cameraEntity = CreateCameraEntity(new float4(0f, 0f, 1280f, 720f));
    CameraComponent liveCamera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));
    cameraEntity.AddComponent(new CameraViewportBindingComponent {
        Mode = CameraViewportBindingMode.Screen
    });
    EditorSceneCameraSuppressionService.AttachAndSuppress(cameraEntity);
    Core.Instance.RenderManager3D.OnWindowResize(IntPtr.Zero, 640, 480);

    CameraPreviewSource source = new CameraPreviewSource(cameraEntity, liveCamera, Core.Instance.RenderManager3D);
    source.Resize(new int2(320, 180));

    TestRenderTarget resizedRenderTarget = Assert.IsType<TestRenderTarget>(source.RenderTarget);
    Assert.Equal(640, resizedRenderTarget.Width);
    Assert.Equal(480, resizedRenderTarget.Height);
    Assert.Equal(new float4(0f, 0f, 640f, 480f), source.PreviewCamera.Viewport);
}
```

- [ ] **Step 2: Run the preview slice to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraPreviewSourceTests" -v minimal
```

Expected:

```text
FAIL
```

- [ ] **Step 3: Update `CameraPreviewSource` to honor viewport binding**

In `CameraPreviewSource.cs`, prefer the live screen-bound viewport contract over suppressed authored viewport dimensions:

```csharp
int2 ResolvePreviewTargetSize() {
    if (HasScreenViewportBinding()) {
        int2 windowSize = renderManager3D.MainWindowSize;
        return new int2(Math.Max(1, windowSize.X), Math.Max(1, windowSize.Y));
    }
    if (sceneCanvasProfileState != null) {
        return new int2(
            Math.Max(1, sceneCanvasProfileState.CanvasWidth),
            Math.Max(1, sceneCanvasProfileState.CanvasHeight));
    }
    if (suppressionState != null) {
        return new int2(
            Math.Max(1, (int)Math.Round(suppressionState.Viewport.Z)),
            Math.Max(1, (int)Math.Round(suppressionState.Viewport.W)));
    }

    return new int2(Math.Max(1, contentSize.X), Math.Max(1, contentSize.Y));
}

bool HasScreenViewportBinding() {
    if (sourceEntity?.Components == null) {
        return false;
    }

    for (int componentIndex = 0; componentIndex < sourceEntity.Components.Count; componentIndex++) {
        if (sourceEntity.Components[componentIndex] is CameraViewportBindingComponent binding &&
            binding.Mode == CameraViewportBindingMode.Screen) {
            return true;
        }
    }

    return false;
}
```

- [ ] **Step 4: Re-run the preview tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CameraPreviewSourceTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 5: Commit the editor preview update**

```bash
git add engine/helengine.editor/managers/preview/CameraPreviewSource.cs engine/helengine.editor.tests/CameraPreviewSourceTests.cs
git commit -m "feat: let camera previews honor screen viewport bindings"
```

### Task 4: Re-Bake The Demo Menu Scene With Viewport And Anchor Metadata

**Files:**
- Create: `engine/helengine.core/components/2d/menu/MenuResponsiveLayoutComponent.cs`
- Create: `engine/helengine.editor/serialization/scene/MenuResponsiveLayoutComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeMenuResponsiveLayoutComponentDeserializer.cs`
- Modify: `engine/helengine.core/components/2d/menu/DemoMenuLayout.cs`
- Modify: `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs`

- [ ] **Step 1: Write the failing generated-scene structure tests**

Add two tests to `DemoDiscSceneWriterTests.cs`:

```csharp
/// <summary>
/// Ensures the demo menu camera now carries an authored viewport-binding component instead of relying only on a fixed runtime rectangle.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_BakesCameraViewportBindingComponent() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset cameraEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscCamera");

    Assert.Contains(cameraEntity.Components, component => string.Equals(component.ComponentTypeId, CameraViewportBindingComponent.SerializedComponentTypeId, StringComparison.Ordinal));
}

/// <summary>
/// Ensures the generated menu tree carries anchor hosts, anchor components, and the responsive layout component.
/// </summary>
[Fact]
public void WriteAll_WhenMenuSceneIsGenerated_BakesResponsiveAnchorMetadata() {
    DemoDiscSceneWriter writer = new DemoDiscSceneWriter(new DemoDiscFontWriter());

    writer.WriteAll(ProjectRootPath);

    SceneAsset sceneAsset = ReadGeneratedSceneAsset();
    SceneEntityAsset menuEntity = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuEntity.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);
    SceneEntityAsset sceneSelectPanel = Assert.Single(generatedRoot.Children, child => string.Equals(child.Id, "panel-scene-select", StringComparison.Ordinal));
    SceneEntityAsset itemsViewport = Assert.Single(sceneSelectPanel.Children, child => string.Equals(child.Id, "panel-scene-select-items-viewport", StringComparison.Ordinal));
    SceneEntityAsset selectedDescription = Assert.Single(sceneSelectPanel.Children, child => child.Id.StartsWith("selected-description-", StringComparison.Ordinal));

    Assert.Contains(generatedRoot.Components, component => string.Equals(component.ComponentTypeId, AnchorBoundsHostComponent.SerializedComponentTypeId, StringComparison.Ordinal));
    Assert.Contains(generatedRoot.Components, component => string.Equals(component.ComponentTypeId, MenuResponsiveLayoutComponent.SerializedComponentTypeId, StringComparison.Ordinal));
    Assert.Contains(sceneSelectPanel.Components, component => string.Equals(component.ComponentTypeId, AnchorBoundsHostComponent.SerializedComponentTypeId, StringComparison.Ordinal));
    Assert.Contains(itemsViewport.Components, component => string.Equals(component.ComponentTypeId, AnchorComponent.SerializedComponentTypeId, StringComparison.Ordinal));
    Assert.Contains(selectedDescription.Components, component => string.Equals(component.ComponentTypeId, AnchorComponent.SerializedComponentTypeId, StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run the writer tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesCameraViewportBindingComponent|FullyQualifiedName~DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_BakesResponsiveAnchorMetadata" -v minimal
```

Expected:

```text
FAIL
```

- [ ] **Step 3: Add the empty-payload responsive layout component and register it**

Create the component and its persistence/runtime support:

```csharp
namespace helengine {
    /// <summary>
    /// Recomputes the baked demo menu shell from the active viewport bounds.
    /// </summary>
    public sealed class MenuResponsiveLayoutComponent : UpdateComponent {
        public const byte CurrentVersion = 1;
        public const string SerializedComponentTypeId = "helengine.MenuResponsiveLayoutComponent";
    }
}
```

Register the descriptor and runtime deserializer alongside the viewport/anchor descriptors:

```csharp
persistenceRegistry.Register(new MenuResponsiveLayoutComponentPersistenceDescriptor());
registry.Register(new RuntimeMenuResponsiveLayoutComponentDeserializer());
```

- [ ] **Step 4: Re-bake the camera and menu tree with the new generic components**

Update `DemoMenuSceneAssetFactory.cs` so the authored scene includes the reusable viewport and anchor contracts:

```csharp
SceneEntityAsset BuildCameraEntityAsset() {
    return new SceneEntityAsset {
        Id = "demo-disc-camera",
        Name = "DemoDiscCamera",
        LocalPosition = float3.Zero,
        LocalScale = float3.One,
        LocalOrientation = float4.Identity,
        Components = new[] {
            BuildCameraComponentRecord(),
            CameraViewportBindingDescriptor.SerializeComponent(
                new CameraViewportBindingComponent {
                    Mode = CameraViewportBindingMode.Screen
                },
                1,
                null)
        },
        Children = Array.Empty<SceneEntityAsset>()
    };
}
```

Add host/layout/anchor metadata to the generated tree:

```csharp
SceneEntityAsset BuildGeneratedRootEntityAsset(MenuDefinition definition) {
    AnchorBoundsHostComponent rootBoundsHost = new AnchorBoundsHostComponent {
        SourceKind = AnchorBoundsSourceKind.Screen
    };

    return new SceneEntityAsset {
        Id = "demo-disc-generated-menu",
        Name = DemoMenuLayout.GeneratedRootEntityName,
        LocalPosition = float3.Zero,
        LocalScale = float3.One,
        LocalOrientation = float4.Identity,
        Components = new[] {
            AnchorBoundsHostDescriptor.SerializeComponent(rootBoundsHost, 0, null),
            MenuResponsiveLayoutDescriptor.SerializeComponent(new MenuResponsiveLayoutComponent(), 1, null)
        },
        Children = children.ToArray()
    };
}
```

For each panel root, add a fixed-bounds host and anchors on the selected-description and items viewport entities:

```csharp
AnchorBoundsHostComponent panelBoundsHost = new AnchorBoundsHostComponent {
    SourceKind = AnchorBoundsSourceKind.Fixed,
    FixedBounds = new int2(DemoMenuLayout.PanelWidth, DemoMenuLayout.PanelHeight)
};

AnchorComponent viewportAnchor = new AnchorComponent();
viewportAnchor.SetAnchorDistances(left: 32f, top: 78f);

AnchorComponent descriptionAnchor = new AnchorComponent();
descriptionAnchor.SetAnchorDistances(left: 32f, bottom: 24f);
```

- [ ] **Step 5: Re-run the generated-scene tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 6: Commit the authored-scene structure changes**

```bash
git add engine/helengine.core/components/2d/menu/MenuResponsiveLayoutComponent.cs engine/helengine.editor/serialization/scene/MenuResponsiveLayoutComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeMenuResponsiveLayoutComponentDeserializer.cs engine/helengine.core/components/2d/menu/DemoMenuLayout.cs engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs
git commit -m "feat: bake responsive anchor metadata into demo menu scene"
```

### Task 5: Implement The Responsive Menu Layout Rules And Runtime Verification

**Files:**
- Modify: `engine/helengine.core/components/2d/menu/MenuResponsiveLayoutComponent.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing packaged-runtime layout tests**

Add focused coverage to `RuntimeSceneLoadServiceTests.cs`:

```csharp
/// <summary>
/// Ensures packaged menu cameras bound to the screen adopt the active runtime resolution.
/// </summary>
[Fact]
public void Load_WhenBakedMenuCameraBindsToScreen_UsesCurrentWindowViewport() {
    Core.Instance.RenderManager3D.OnWindowResize(IntPtr.Zero, 640, 480);
    string buildRootPath = BuildPackagedMenu();

    SceneAsset sceneAsset;
    using (FileStream packagedSceneStream = File.OpenRead(GetPackagedScenePath(buildRootPath, "Scenes/TestMenu.helen"))) {
        sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(packagedSceneStream));
    }

    RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
        Core.Instance.ContentManager,
        buildRootPath,
        ShaderCompileTarget.DirectX11);
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver, RuntimeComponentRegistry.CreateDefault());
    IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);
    Entity cameraEntity = Assert.Single(loadedRoots, entity => entity.Components.Any(component => component is CameraComponent));
    CameraComponent camera = Assert.IsType<CameraComponent>(Assert.Single(cameraEntity.Components, component => component is CameraComponent));

    Assert.Equal(new float4(0f, 0f, 640f, 480f), camera.Viewport);
}

/// <summary>
/// Ensures responsive menu layout shrinks the scene-list viewport when the runtime resolution drops to 480p.
/// </summary>
[Fact]
public void Load_WhenBakedMenuRunsAt480p_RecomputesPanelViewportHeight() {
    Core.Instance.RenderManager3D.OnWindowResize(IntPtr.Zero, 640, 480);
    string buildRootPath = BuildPackagedMenu();
    MenuComponent menuHostComponent = LoadPackagedMenu(buildRootPath);
    Entity panelEntity = FindPanelEntity(menuHostComponent, "scene-select");
    List<Entity> clipEntities = new List<Entity>();
    CollectEntitiesWithComponent<ClipRectComponent>(panelEntity, clipEntities);
    ClipRectComponent clipComponent = Assert.IsType<ClipRectComponent>(Assert.Single(Assert.Single(clipEntities).Components, component => component is ClipRectComponent));

    Assert.True(clipComponent.Size.Y < 272);
    Assert.True(clipComponent.Size.Y > 180);
}
```

- [ ] **Step 2: Run the runtime menu slice to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected:

```text
FAIL
```

- [ ] **Step 3: Implement the responsive layout rules in `MenuResponsiveLayoutComponent`**

Fill in the component so it locates the generated shell and applies one of two layout profiles based on aspect ratio:

```csharp
public sealed class MenuResponsiveLayoutComponent : UpdateComponent {
    const double NarrowAspectThreshold = 1.5d;

    AnchorBoundsHostComponent rootBoundsHost;
    RoundedRectComponent backgroundSurface;
    List<MenuPanelLayoutRuntime> panelRuntimes;
    bool isInitialized;

    public override void ComponentAdded(Entity entity) {
        base.ComponentAdded(entity);
        rootBoundsHost = FindRequiredComponent<AnchorBoundsHostComponent>(entity);
        panelRuntimes = BindPanels(entity);
        backgroundSurface = FindRequiredBackground(entity);
        ApplyLayout(rootBoundsHost.AnchorBounds);
        isInitialized = true;
    }

    public override void Update() {
        if (!isInitialized) {
            return;
        }

        ApplyLayout(rootBoundsHost.AnchorBounds);
    }

    void ApplyLayout(int2 viewportBounds) {
        int viewportWidth = Math.Max(1, viewportBounds.X);
        int viewportHeight = Math.Max(1, viewportBounds.Y);
        MenuViewportProfile profile = ResolveViewportProfile(viewportWidth, viewportHeight);

        backgroundSurface.Size = new int2(viewportWidth, viewportHeight);

        for (int panelIndex = 0; panelIndex < panelRuntimes.Count; panelIndex++) {
            MenuPanelLayoutRuntime panel = panelRuntimes[panelIndex];
            panel.PanelAnchor.SetAnchorDistances(left: profile.PanelLeft, top: profile.PanelTop);
            panel.PanelBoundsHost.FixedBounds = new int2(profile.PanelWidth, profile.PanelHeight);
            panel.Surface.Size = new int2(profile.PanelWidth, profile.PanelHeight);
            panel.TopBand.Size = new int2(profile.PanelWidth, DemoMenuLayout.PanelTopBandHeight);
            panel.ItemsViewportClip.Size = new int2(profile.ItemsWidth, profile.ItemsHeight);
            panel.ItemsScroll.Size = panel.ItemsViewportClip.Size;
            panel.DescriptionText.Size = new int2(profile.PanelWidth - 64, 64);
            ResizeItemRows(panel, profile.ItemsWidth);
        }
    }
}
```

Use a narrow profile for `4:3` and other tighter viewports:

```csharp
MenuViewportProfile ResolveViewportProfile(int viewportWidth, int viewportHeight) {
    double aspectRatio = viewportWidth / (double)viewportHeight;
    if (aspectRatio < NarrowAspectThreshold) {
        return new MenuViewportProfile(
            panelLeft: 32f,
            panelTop: 48f,
            panelWidth: Math.Min(560, Math.Max(420, viewportWidth - 64)),
            panelHeight: Math.Min(400, Math.Max(300, viewportHeight - 96)),
            itemsWidth: Math.Min(420, Math.Max(300, viewportWidth - 128)),
            itemsHeight: Math.Max(190, viewportHeight - 220));
    }

    return new MenuViewportProfile(
        panelLeft: 88f,
        panelTop: 190f,
        panelWidth: 560,
        panelHeight: 420,
        itemsWidth: 420,
        itemsHeight: 272);
}
```

- [ ] **Step 4: Re-run the runtime menu tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

Expected:

```text
PASS
```

- [ ] **Step 5: Regenerate the real Demo Disc menu scene**

Run:

```bash
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:/dev/helprojs/city/project.heproj --editor-command menu.regenerate-demo-disc-main-menu
```

Expected:

```text
Editor command 'menu.regenerate-demo-disc-main-menu' executed successfully.
```

- [ ] **Step 6: Commit the responsive layout behavior**

```bash
git add engine/helengine.core/components/2d/menu/MenuResponsiveLayoutComponent.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "feat: make demo menu respond to viewport bindings"
```

## Self-Review

**Spec coverage:** The plan covers the approved design goals and the new viewport requirement from the follow-up discussion. Task 1 fixes the current anchor limitation where generic scene entities cannot consume component-based bounds or size providers. Task 2 adds reusable viewport and bounds components plus persistence/runtime support. Task 3 bakes those generic contracts into the generated menu scene. Task 4 updates editor preview behavior for screen-bound cameras. Task 5 implements and verifies the menu-specific responsive layout rules at runtime and during regeneration.

**Placeholder scan:** No `TBD`, `TODO`, or deferred “implement later” language remains. Each task includes concrete files, commands, and implementation seams.

**Type consistency:** The plan uses one consistent naming set throughout: `AnchorBoundsSourceKind`, `CameraViewportBindingMode`, `AnchorBoundsHostComponent`, `CameraViewportBindingComponent`, `MenuResponsiveLayoutComponent`, `AnchorComponentPersistenceDescriptor`, `RuntimeAnchorComponentDeserializer`, `RuntimeAnchorBoundsHostComponentDeserializer`, `RuntimeCameraViewportBindingComponentDeserializer`, and `RuntimeMenuResponsiveLayoutComponentDeserializer`.


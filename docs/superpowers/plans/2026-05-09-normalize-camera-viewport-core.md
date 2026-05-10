# Normalize Camera Viewport Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `CameraComponent.Viewport` mean normalized target bounds engine-wide, update active renderers to resolve those bounds to physical pixels, and normalize generated fullscreen cameras so the PS2 menu renders against the actual PS2 target instead of an authored `1280x720` viewport.

**Architecture:** Keep `CameraComponent.Viewport` as `float4`, but make it a normalized rectangle in core. Add one shared resolver that converts normalized bounds into physical pixel rectangles using either the active window size or render-target size, then switch DirectX11, Vulkan, and PS2 code paths to use that resolver. Update generated demo-disc and rendering scenes so fullscreen cameras serialize as `0,0,1,1` instead of desktop pixel dimensions.

**Tech Stack:** C#/.NET 9, DirectX11, Vulkan, C++ PS2 runtime, xUnit, gsKit

---

### Task 1: Lock The Normalized Viewport Contract In Tests

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CameraViewportContractTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\CameraComponentPersistenceDescriptorTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing core contract test**

```csharp
namespace helengine.editor.tests;

/// <summary>
/// Locks the engine-wide meaning of camera viewports to normalized target bounds.
/// </summary>
public sealed class CameraViewportContractTests {
    /// <summary>
    /// Ensures new cameras default to a fullscreen normalized viewport.
    /// </summary>
    [Fact]
    public void CameraComponent_constructor_defaults_to_fullscreen_normalized_viewport() {
        CameraComponent camera = new CameraComponent();

        Assert.Equal(new float4(0f, 0f, 1f, 1f), camera.Viewport);
    }
}
```

- [ ] **Step 2: Update the generated scene expectations to normalized fullscreen**

```csharp
Assert.Equal(new float4(0f, 0f, 1f, 1f), cameraComponent.Viewport);
```

Apply that assertion change in:

- `DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_ProducesCameraPayloadTheEditorCanDeserialize`
- any generated rendering-scene tests that currently expect `new float4(0f, 0f, 1280f, 720f)`

- [ ] **Step 3: Keep persistence tests for arbitrary rectangles, but add one normalized fullscreen round-trip**

```csharp
[Fact]
public void DeserializeComponent_WhenViewportIsNormalizedFullscreen_PreservesNormalizedBounds() {
    CameraComponent source = new CameraComponent {
        Viewport = new float4(0f, 0f, 1f, 1f),
    };

    SceneComponentAssetRecord record = Serialize(source);
    CameraComponent loadedCamera = Deserialize(record);

    Assert.Equal(new float4(0f, 0f, 1f, 1f), loadedCamera.Viewport);
}
```

- [ ] **Step 4: Run the focused tests and verify at least one fails for the expected reason**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~CameraViewportContractTests|FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~CameraComponentPersistenceDescriptorTests"
```

Expected:

- `CameraViewportContractTests` passes immediately if the constructor is already normalized
- `DemoDiscSceneWriterTests.WriteAll_WhenMenuSceneIsGenerated_ProducesCameraPayloadTheEditorCanDeserialize` fails because it still emits `1280x720`

- [ ] **Step 5: Commit the test-only red state**

```powershell
rtk git -C C:\dev\helworks\helengine add -- `
  engine/helengine.editor.tests/CameraViewportContractTests.cs `
  engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs `
  engine/helengine.editor.tests/serialization/scene/CameraComponentPersistenceDescriptorTests.cs `
  engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "test: lock normalized camera viewport contract"
```

### Task 2: Add Shared Viewport Resolution In Core

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\utils\CameraViewportPixelResolver.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\components\CameraComponent.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\managers\rendering\RenderManager3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CameraViewportContractTests.cs`

- [ ] **Step 1: Write the failing resolver tests first**

Extend `CameraViewportContractTests.cs` with:

```csharp
[Fact]
public void ResolvePixels_WhenViewportIsFullscreenNormalized_UsesEntireTarget() {
    float4 resolved = CameraViewportPixelResolver.ResolvePixelViewport(
        new float4(0f, 0f, 1f, 1f),
        new int2(640, 480));

    Assert.Equal(new float4(0f, 0f, 640f, 480f), resolved);
}

[Fact]
public void ResolvePixels_WhenViewportUsesNormalizedSubRect_ScalesAgainstTargetSize() {
    float4 resolved = CameraViewportPixelResolver.ResolvePixelViewport(
        new float4(0.25f, 0.1f, 0.5f, 0.75f),
        new int2(800, 600));

    Assert.Equal(new float4(200f, 60f, 400f, 450f), resolved);
}
```

- [ ] **Step 2: Run the focused resolver tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter FullyQualifiedName~CameraViewportContractTests
```

Expected:

- FAIL with `CameraViewportPixelResolver` missing

- [ ] **Step 3: Add the shared resolver and update camera XML comments**

Create `CameraViewportPixelResolver.cs` with:

```csharp
namespace helengine {
    /// <summary>
    /// Converts normalized camera viewport bounds into physical target pixels.
    /// </summary>
    public static class CameraViewportPixelResolver {
        /// <summary>
        /// Resolves one normalized viewport rectangle against one physical target size.
        /// </summary>
        /// <param name="normalizedViewport">Normalized viewport bounds in target-space percentages.</param>
        /// <param name="targetSize">Physical target size in pixels.</param>
        /// <returns>Resolved viewport rectangle in physical pixels.</returns>
        public static float4 ResolvePixelViewport(float4 normalizedViewport, int2 targetSize) {
            if (targetSize.X <= 0 || targetSize.Y <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetSize), "Viewport target size must be positive.");
            }

            return new float4(
                normalizedViewport.X * targetSize.X,
                normalizedViewport.Y * targetSize.Y,
                normalizedViewport.Z * targetSize.X,
                normalizedViewport.W * targetSize.Y);
        }
    }
}
```

Update `CameraComponent.cs` XML comments to say:

```csharp
/// <summary>
/// Gets or sets normalized target bounds for this camera. A value of <c>0,0,1,1</c> covers the full active render target.
/// </summary>
```

- [ ] **Step 4: Add one render-manager helper that uses the shared resolver**

Add to `RenderManager3D.cs`:

```csharp
/// <summary>
/// Resolves the supplied camera viewport to physical pixels using the supplied target size.
/// </summary>
/// <param name="camera">Camera whose normalized viewport should be resolved.</param>
/// <param name="targetSize">Physical target size in pixels.</param>
/// <returns>Physical viewport rectangle in pixels.</returns>
protected float4 ResolvePixelViewport(ICamera camera, int2 targetSize) {
    if (camera == null) {
        throw new ArgumentNullException(nameof(camera));
    }

    return CameraViewportPixelResolver.ResolvePixelViewport(camera.Viewport, targetSize);
}
```

- [ ] **Step 5: Run the focused tests and verify green**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter FullyQualifiedName~CameraViewportContractTests
```

Expected:

- PASS for the new normalized viewport contract and resolver tests

- [ ] **Step 6: Commit the shared core contract**

```powershell
rtk git -C C:\dev\helworks\helengine add -- `
  engine/helengine.core/utils/CameraViewportPixelResolver.cs `
  engine/helengine.core/components/CameraComponent.cs `
  engine/helengine.core/managers/rendering/RenderManager3D.cs `
  engine/helengine.editor.tests/CameraViewportContractTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "feat: normalize camera viewport contract"
```

### Task 3: Update Active Renderers To Resolve Normalized Viewports

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.directx11\DirectX11Renderer3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.directx11\DirectX11Renderer2D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.vulkan\VulkanRenderer3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.vulkan\VulkanRenderer2D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.render.validation\RenderValidationRunner.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\ViewportAndAnchorLayoutTests.cs`

- [ ] **Step 1: Write one failing renderer-facing regression around fullscreen viewport resolution**

Add to `ViewportAndAnchorLayoutTests.cs`:

```csharp
[Fact]
public void ResolvePixelViewport_WhenFullscreenCameraUsesNormalizedBounds_ExpandsToWindowSize() {
    float4 resolved = CameraViewportPixelResolver.ResolvePixelViewport(
        new float4(0f, 0f, 1f, 1f),
        new int2(1280, 720));

    Assert.Equal(new float4(0f, 0f, 1280f, 720f), resolved);
}
```

- [ ] **Step 2: Replace direct renderer use of `camera.Viewport` with resolved pixel rectangles**

In `DirectX11Renderer3D.cs`, change patterns like:

```csharp
float4 viewport = camera.Viewport;
deviceContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);
```

to:

```csharp
int2 targetSize = renderTarget != null
    ? new int2(renderTarget.Width, renderTarget.Height)
    : new int2(surface.Width, surface.Height);
float4 viewport = ResolvePixelViewport(camera, targetSize);
deviceContext.Rasterizer.SetViewport(viewport.X, viewport.Y, viewport.Z, viewport.W);
float4x4 projection = CameraProjectionUtils.CreatePerspectiveProjection(camera, (float)Math.PI / 4.0f, viewport.Z / viewport.W);
```

Apply the same pattern to:

- DirectX11 custom passes
- DirectX11 shadow-pass restore path
- Vulkan 3D viewport/scissor setup
- Vulkan 2D viewport/scissor setup

- [ ] **Step 3: Update render validation scenes to author normalized fullscreen cameras**

In `RenderValidationRunner.cs`, replace fullscreen camera creation like:

```csharp
Viewport = new float4(0f, 0f, width, height),
```

with:

```csharp
Viewport = new float4(0f, 0f, 1f, 1f),
```

- [ ] **Step 4: Run renderer-facing tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportAndAnchorLayoutTests"
```

Expected:

- PASS for the normalized viewport resolution assertions

- [ ] **Step 5: Commit the renderer conversion**

```powershell
rtk git -C C:\dev\helworks\helengine add -- `
  engine/helengine.directx11/DirectX11Renderer3D.cs `
  engine/helengine.directx11/DirectX11Renderer2D.cs `
  engine/helengine.vulkan/VulkanRenderer3D.cs `
  engine/helengine.vulkan/VulkanRenderer2D.cs `
  engine/helengine.render.validation/RenderValidationRunner.cs `
  engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "feat: resolve normalized camera viewports in renderers"
```

### Task 4: Normalize Generated Fullscreen Cameras And Verify PS2

**Files:**
- Modify: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DemoDiscSceneWriter.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\GeneratedRuntimeSceneViewportSourceTests.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotlightStreetSliceSceneFactory.cs`
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2NativeBuildInputsTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\GeneratedRuntimeSceneViewportSourceTests.cs`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2NativeBuildInputsTests.cs`

- [ ] **Step 1: Write the failing generated-scene assertions for normalized fullscreen**

In `DemoDiscSceneWriterTests.cs`, keep:

```csharp
Assert.Equal(new float4(0f, 0f, 1f, 1f), cameraComponent.Viewport);
```

Create `GeneratedRuntimeSceneViewportSourceTests.cs` with:

```csharp
namespace helengine.editor.tests;

/// <summary>
/// Locks generated fullscreen runtime scenes to normalized viewport bounds.
/// </summary>
public sealed class GeneratedRuntimeSceneViewportSourceTests {
    /// <summary>
    /// Ensures the city-generated runtime rendering scenes write fullscreen cameras as normalized bounds.
    /// </summary>
    [Fact]
    public void City_rendering_scene_factories_write_normalized_fullscreen_camera_viewports() {
        string directionalSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs");
        string spotlightSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotlightStreetSliceSceneFactory.cs");

        Assert.Contains("new float4(0f, 0f, 1f, 1f)", directionalSource, StringComparison.Ordinal);
        Assert.Contains("new float4(0f, 0f, 1f, 1f)", spotlightSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new float4(0f, 0f, 1280f, 720f)", directionalSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new float4(0f, 0f, 1280f, 720f)", spotlightSource, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Update generated fullscreen camera payloads**

In `DemoDiscSceneWriter.cs` and the city rendering factories, replace fullscreen viewport writes like:

```csharp
writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1280f, 720f)));
```

with:

```csharp
writer.WriteField("Viewport", fieldWriter => fieldWriter.WriteFloat4(new float4(0f, 0f, 1f, 1f)));
```

- [ ] **Step 3: Remove the temporary PS2 diagnostic halt and keep only the real window-size publication**

In `Ps2BootHost.cpp`, delete the diagnostic-only halt path:

```cpp
FatalHaltWithRuntimeState(EngineCore);
```

Keep the earlier host fix:

```cpp
EngineRenderManager3D->AddWindow(
    0,
    static_cast<int32_t>(GsGlobal->Width),
    static_cast<int32_t>(GsGlobal->Height));
```

Update `Ps2NativeBuildInputsTests.cs` so it continues asserting the `AddWindow(...)` host contract after the diagnostic code is removed.

- [ ] **Step 4: Run the focused engine and PS2 tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~CameraViewportContractTests|FullyQualifiedName~ViewportAndAnchorLayoutTests"
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --filter FullyQualifiedName~GeneratedRuntimeSceneViewportSourceTests
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj -c Debug
```

Expected:

- `DemoDiscSceneWriterTests` passes with normalized fullscreen camera assertions
- `GeneratedRuntimeSceneViewportSourceTests` passes against the checked-in city scene-factory sources
- PS2 builder tests pass, including the boot-host `AddWindow(...)` source assertion

- [ ] **Step 5: Build a fresh PS2 ISO and verify runtime behavior**

Run:

```powershell
rtk proxy powershell.exe -NoProfile -Command "& 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.exe' --project 'C:\dev\helprojs\city\project.heproj' --build ps2 --output 'C:\dev\helprojs\output\ps2-normalized-viewport'"
```

Expected:

- `Build completed for platform 'ps2': C:\dev\helprojs\output\ps2-normalized-viewport`

Then boot:

- `C:\dev\helprojs\output\ps2-normalized-viewport\game.iso`

Manual verification:

- main menu is visible again
- the diagnostic camera viewport line is gone because the diagnostic halt code was removed
- selecting `DirectionalShadowPlaza` no longer depends on `1280x720` camera coordinates

- [ ] **Step 6: Commit the authored-scene and PS2 runtime cleanup**

```powershell
rtk git -C C:\dev\helworks\helengine add -- `
  tools/demo-disc-scene-writer/DemoDiscSceneWriter.cs `
  engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs `
  engine/helengine.editor.tests/GeneratedRuntimeSceneViewportSourceTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "feat: normalize generated fullscreen camera viewports"

rtk git -C C:\dev\helprojs\city add -- `
  assets/codebase/rendering.tools/DirectionalShadowPlazaSceneFactory.cs `
  assets/codebase/rendering.tools/SpotlightStreetSliceSceneFactory.cs
rtk git -C C:\dev\helprojs\city commit -m "feat: normalize generated fullscreen camera viewports"

rtk git -C C:\dev\helworks\helengine-ps2 add -- `
  src/platform/ps2/Ps2BootHost.cpp `
  builder.tests/Ps2NativeBuildInputsTests.cs
rtk git -C C:\dev\helworks\helengine-ps2 commit -m "fix: keep ps2 host viewport target sizing"
```

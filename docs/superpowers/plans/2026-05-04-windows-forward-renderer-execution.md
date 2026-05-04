# Windows Forward Renderer Execution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the live DirectX 11 renderer onto the new extracted-frame and ordered-pass architecture for depth, opaque, transparent, and present work, while also adding committed rendering demo scenes under the project scene catalog.

**Architecture:** Expand the shared extraction layer so frames contain real drawable submissions, introduce a small DX11 pass-executor seam that can be tested independently from SharpDX state, then refactor `DirectX11Renderer3D` to build one frame and one plan per camera and execute only the supported pass set. Add authored `.helen` rendering scenes under `test-project/assets/Scenes/rendering/` and validate that they can be loaded by the existing scene pipeline.

**Tech Stack:** C# / .NET 9, xUnit, SharpDX / Direct3D11, `helengine.core`, `helengine.directx11`, `helengine.editor`, `SceneSaveService`, binary `.helen` scene assets

---

## File Structure

### Shared extraction updates

- Modify: `engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs`
  Populate drawable submissions instead of returning empty arrays.
- Create: `engine/helengine.core/managers/rendering/RenderFrameDrawableClassifier.cs`
  Shared helper that determines transparency and conservative batching metadata from one drawable.
- Modify: `engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs`
  Add coverage for opaque and transparent extraction behavior.

### DirectX11 pass execution seam

- Create: `engine/helengine.directx11/rendering/IDirectX11RenderPassExecutor.cs`
  Narrow interface for depth, opaque, transparent, shadow, post, and present pass callbacks.
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPassExecutionContext.cs`
  Carries frame, target surface, and render target references for pass execution.
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPlanExecutor.cs`
  Executes `RenderPlan` in order and skips unsupported planned passes for this slice.
- Create: `engine/helengine.editor.tests/rendering/DirectX11RenderPlanExecutorTests.cs`
  Verifies pass ordering and skip behavior independent of SharpDX.

### DirectX11 renderer integration

- Create: `engine/helengine.directx11/rendering/DirectX11RenderQueueSnapshotVisitor.cs`
  Collects ordered `IDrawable3D` entries from an `IRenderQueue3D`.
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
  Build extracted frames, build plans, and execute depth/opaque/transparent/present through the new executor.
- Modify: `engine/helengine.editor.tests/testing/TestDirectX11RenderManager3D.cs`
  Add a controlled test harness for plan-driven renderer execution without a real device.
- Create: `engine/helengine.editor.tests/rendering/DirectX11RendererPlannedExecutionTests.cs`
  Verifies the live renderer path routes planned transparent draws only through the transparent pass and tolerates planned shadow/post entries.

### Rendering scene catalog

- Create: `test-project/assets/Scenes/rendering/opaque-basics.helen`
  Minimal opaque geometry scene.
- Create: `test-project/assets/Scenes/rendering/transparency-order.helen`
  Layered transparency scene for pass ordering checks.
- Create: `test-project/assets/Scenes/rendering/depth-prepass.helen`
  Geometry arrangement intended to exercise depth-prepass scheduling.
- Create: `test-project/assets/Scenes/rendering/material-inputs.helen`
  Scene with albedo, normal-map, and emissive-oriented material references.
- Create: `engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs`
  Loads committed rendering scenes and verifies they deserialize into expected root/component structures.

---

### Task 1: Populate extracted frames with real drawable submissions

**Files:**
- Create: `engine/helengine.core/managers/rendering/RenderFrameDrawableClassifier.cs`
- Modify: `engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs`
- Modify: `engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs`

- [ ] **Step 1: Write the failing extraction tests**

Add these tests to `engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs`:

```csharp
[Fact]
public void Extract_WhenOpaqueAndTransparentDrawablesExist_ReturnsBothSubmissionsWithTransparencyFlags() {
    CameraComponent camera = new CameraComponent();
    TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);
    TestDrawable3D transparentDrawable = new TestDrawable3D(MaterialBlendMode.AlphaBlend);
    RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

    RenderFrameExtractionResult result = extractionService.Extract(
        [camera],
        [opaqueDrawable, transparentDrawable],
        [],
        new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

    RenderFrame frame = Assert.Single(result.Frames);
    Assert.Equal(2, frame.DrawableSubmissions.Count);
    Assert.False(frame.DrawableSubmissions[0].IsTransparent);
    Assert.True(frame.DrawableSubmissions[1].IsTransparent);
    Assert.True(frame.HasTransparentDrawables);
}

[Fact]
public void Extract_WhenDrawableHasNoMaterial_TreatsItAsOpaque() {
    CameraComponent camera = new CameraComponent();
    TestDrawable3D drawable = new TestDrawable3D(MaterialBlendMode.Opaque);
    drawable.Material = null;
    RenderFrameExtractionService extractionService = new RenderFrameExtractionService();

    RenderFrameExtractionResult result = extractionService.Extract(
        [camera],
        [drawable],
        [],
        new RendererBackendCapabilityProfile(true, false, true, true, 32, 4));

    RenderFrame frame = Assert.Single(result.Frames);
    RenderFrameDrawableSubmission submission = Assert.Single(frame.DrawableSubmissions);
    Assert.False(submission.IsTransparent);
}
```

- [ ] **Step 2: Run the focused extraction tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderFrameExtractionServiceTests -v minimal"
```

Expected: FAIL because extraction still returns empty drawable submission arrays.

- [ ] **Step 3: Implement shared drawable classification and extraction**

Create `engine/helengine.core/managers/rendering/RenderFrameDrawableClassifier.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Classifies one drawable into the shared render-frame representation.
    /// </summary>
    public sealed class RenderFrameDrawableClassifier {
        /// <summary>
        /// Creates one shared drawable submission from a visible drawable.
        /// </summary>
        /// <param name="drawable">Visible drawable to classify.</param>
        /// <returns>Shared render-frame drawable submission.</returns>
        public RenderFrameDrawableSubmission Classify(IDrawable3D drawable) {
            if (drawable == null) {
                throw new ArgumentNullException(nameof(drawable));
            }

            RuntimeMaterial material = drawable.Material;
            bool isTransparent = false;
            if (material != null) {
                MaterialRenderState renderState = material.RenderState;
                if (renderState != null && renderState.BlendMode == MaterialBlendMode.AlphaBlend) {
                    isTransparent = true;
                }
            }

            return new RenderFrameDrawableSubmission(
                drawable,
                isTransparent,
                new RenderFrameBatchingMetadata(false, false, false));
        }
    }
}
```

Update `engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs` so `Extract(...)` classifies all supplied drawables:

```csharp
RenderFrameDrawableClassifier classifier = new RenderFrameDrawableClassifier();
RenderFrameDrawableSubmission[] drawableSubmissions = new RenderFrameDrawableSubmission[drawables.Count];
for (int drawableIndex = 0; drawableIndex < drawables.Count; drawableIndex++) {
    drawableSubmissions[drawableIndex] = classifier.Classify(drawables[drawableIndex]);
}

frames[index] = new RenderFrame(
    cameras[index],
    drawableSubmissions,
    Array.Empty<RenderFrameLightSubmission>(),
    Array.Empty<RenderFrameShadowCasterSubmission>());
```

- [ ] **Step 4: Re-run the focused extraction tests and verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderFrameExtractionServiceTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy powershell -Command "git add 'engine/helengine.core/managers/rendering/RenderFrameDrawableClassifier.cs' 'engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs' 'engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs'; git commit -m 'feat: extract drawable submissions into render frames'"
```

### Task 2: Add a DirectX11 render-plan executor seam

**Files:**
- Create: `engine/helengine.directx11/rendering/IDirectX11RenderPassExecutor.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPassExecutionContext.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPlanExecutor.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11RenderPlanExecutorTests.cs`

- [ ] **Step 1: Write the failing executor tests**

Create `engine/helengine.editor.tests/rendering/DirectX11RenderPlanExecutorTests.cs`:

```csharp
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies ordered DirectX11 render-plan execution.
    /// </summary>
    public class DirectX11RenderPlanExecutorTests {
        /// <summary>
        /// Ensures supported passes execute in plan order.
        /// </summary>
        [Fact]
        public void Execute_WhenPlanContainsSupportedPasses_InvokesExecutorInOrder() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor();
            TestDirectX11RenderPassExecutor passExecutor = new TestDirectX11RenderPassExecutor();
            RenderPlan plan = new RenderPlan([
                RenderPassKind.DepthPrepass,
                RenderPassKind.OpaqueForward,
                RenderPassKind.TransparentForward,
                RenderPassKind.Present
            ]);

            executor.Execute(CreateContext(), plan, passExecutor);

            Assert.Equal(
                ["DepthPrepass", "OpaqueForward", "TransparentForward", "Present"],
                passExecutor.ExecutedPassNames);
        }

        /// <summary>
        /// Ensures shadow and post-process plan entries are skipped cleanly in this slice.
        /// </summary>
        [Fact]
        public void Execute_WhenPlanContainsShadowAndPostProcess_SkipsThemWithoutFailing() {
            DirectX11RenderPlanExecutor executor = new DirectX11RenderPlanExecutor();
            TestDirectX11RenderPassExecutor passExecutor = new TestDirectX11RenderPassExecutor();
            RenderPlan plan = new RenderPlan([
                RenderPassKind.Shadow,
                RenderPassKind.OpaqueForward,
                RenderPassKind.PostProcess,
                RenderPassKind.Present
            ]);

            executor.Execute(CreateContext(), plan, passExecutor);

            Assert.Equal(["OpaqueForward", "Present"], passExecutor.ExecutedPassNames);
        }
    }
}
```

- [ ] **Step 2: Run the focused executor tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RenderPlanExecutorTests -v minimal"
```

Expected: FAIL because the executor seam does not exist yet.

- [ ] **Step 3: Implement the executor seam**

Create `engine/helengine.directx11/rendering/IDirectX11RenderPassExecutor.cs`:

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Receives one ordered DirectX11 render pass at execution time.
    /// </summary>
    public interface IDirectX11RenderPassExecutor {
        /// <summary>
        /// Executes the depth-prepass stage.
        /// </summary>
        void ExecuteDepthPrepass(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the opaque forward pass.
        /// </summary>
        void ExecuteOpaqueForward(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the transparent forward pass.
        /// </summary>
        void ExecuteTransparentForward(DirectX11RenderPassExecutionContext context);

        /// <summary>
        /// Executes the final presentation pass.
        /// </summary>
        void ExecutePresent(DirectX11RenderPassExecutionContext context);
    }
}
```

Create `engine/helengine.directx11/rendering/DirectX11RenderPassExecutionContext.cs`:

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Carries frame and target state for one ordered DirectX11 plan execution.
    /// </summary>
    public sealed class DirectX11RenderPassExecutionContext {
        /// <summary>
        /// Initializes one execution context.
        /// </summary>
        public DirectX11RenderPassExecutionContext(RenderFrame frame, DirectX11SwapChainSurface surface) {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Surface = surface;
        }

        /// <summary>
        /// Gets the extracted render frame being executed.
        /// </summary>
        public RenderFrame Frame { get; }

        /// <summary>
        /// Gets the target surface for swapchain presentation when available.
        /// </summary>
        public DirectX11SwapChainSurface Surface { get; }
    }
}
```

Create `engine/helengine.directx11/rendering/DirectX11RenderPlanExecutor.cs`:

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Executes the supported subset of one DirectX11 render plan.
    /// </summary>
    public sealed class DirectX11RenderPlanExecutor {
        /// <summary>
        /// Executes one ordered render plan for this implementation slice.
        /// </summary>
        public void Execute(
            DirectX11RenderPassExecutionContext context,
            RenderPlan plan,
            IDirectX11RenderPassExecutor passExecutor) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            } else if (plan == null) {
                throw new ArgumentNullException(nameof(plan));
            } else if (passExecutor == null) {
                throw new ArgumentNullException(nameof(passExecutor));
            }

            for (int passIndex = 0; passIndex < plan.Passes.Count; passIndex++) {
                RenderPassKind pass = plan.Passes[passIndex];
                if (pass == RenderPassKind.DepthPrepass) {
                    passExecutor.ExecuteDepthPrepass(context);
                } else if (pass == RenderPassKind.OpaqueForward) {
                    passExecutor.ExecuteOpaqueForward(context);
                } else if (pass == RenderPassKind.TransparentForward) {
                    passExecutor.ExecuteTransparentForward(context);
                } else if (pass == RenderPassKind.Present) {
                    passExecutor.ExecutePresent(context);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Re-run the focused executor tests and verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RenderPlanExecutorTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy powershell -Command "git add 'engine/helengine.directx11/rendering/IDirectX11RenderPassExecutor.cs' 'engine/helengine.directx11/rendering/DirectX11RenderPassExecutionContext.cs' 'engine/helengine.directx11/rendering/DirectX11RenderPlanExecutor.cs' 'engine/helengine.editor.tests/rendering/DirectX11RenderPlanExecutorTests.cs'; git commit -m 'feat: add dx11 render plan executor seam'"
```

### Task 3: Refactor DirectX11Renderer3D onto extracted-frame execution

**Files:**
- Create: `engine/helengine.directx11/rendering/DirectX11RenderQueueSnapshotVisitor.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.editor.tests/testing/TestDirectX11RenderManager3D.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11RendererPlannedExecutionTests.cs`

- [ ] **Step 1: Write the failing renderer execution tests**

Create `engine/helengine.editor.tests/rendering/DirectX11RendererPlannedExecutionTests.cs`:

```csharp
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies that the live DirectX11 renderer consumes render plans for geometry execution.
    /// </summary>
    public class DirectX11RendererPlannedExecutionTests {
        /// <summary>
        /// Ensures a transparent drawable only executes during the transparent pass.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenPlanContainsTransparentPass_DrawsTransparentDrawableOnlyInTransparentStage() {
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);
            TestDrawable3D transparentDrawable = new TestDrawable3D(MaterialBlendMode.AlphaBlend);

            renderer.RenderForTest(
                CreateCamera([opaqueDrawable, transparentDrawable]),
                [opaqueDrawable, transparentDrawable]);

            Assert.Contains("OpaqueForward:Opaque", renderer.ExecutedDrawStages);
            Assert.Contains("TransparentForward:Transparent", renderer.ExecutedDrawStages);
            Assert.DoesNotContain("OpaqueForward:Transparent", renderer.ExecutedDrawStages);
        }

        /// <summary>
        /// Ensures planned shadow and post-process passes do not fail this slice.
        /// </summary>
        [Fact]
        public void RenderCamera_WhenPlanContainsShadowAndPostProcess_SkipsThemAndStillPresents() {
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();
            TestDrawable3D opaqueDrawable = new TestDrawable3D(MaterialBlendMode.Opaque);

            renderer.ForcePlanForTest(new RenderPlan([
                RenderPassKind.Shadow,
                RenderPassKind.OpaqueForward,
                RenderPassKind.PostProcess,
                RenderPassKind.Present
            ]));
            renderer.RenderForTest(CreateCamera([opaqueDrawable]), [opaqueDrawable]);

            Assert.Equal(["OpaqueForward:Opaque", "Present"], renderer.ExecutedDrawStages);
        }
    }
}
```

- [ ] **Step 2: Run the focused renderer execution tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RendererPlannedExecutionTests -v minimal"
```

Expected: FAIL because `DirectX11Renderer3D` still renders directly from the queue and the test harness hooks do not exist.

- [ ] **Step 3: Implement queue snapshotting and plan-driven camera execution**

Create `engine/helengine.directx11/rendering/DirectX11RenderQueueSnapshotVisitor.cs`:

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Collects ordered drawables from one 3D render queue.
    /// </summary>
    public sealed class DirectX11RenderQueueSnapshotVisitor : IRenderVisitor3D {
        /// <summary>
        /// Backing list of ordered drawables.
        /// </summary>
        readonly List<IDrawable3D> DrawablesValue = new List<IDrawable3D>();

        /// <summary>
        /// Gets the ordered drawable snapshot collected by the visitor.
        /// </summary>
        public IReadOnlyList<IDrawable3D> Drawables => DrawablesValue;

        /// <summary>
        /// Visits one drawable and appends it to the snapshot.
        /// </summary>
        public void Visit(IDrawable3D drawable) {
            if (drawable == null) {
                return;
            }

            DrawablesValue.Add(drawable);
        }
    }
}
```

Modify `engine/helengine.directx11/DirectX11Renderer3D.cs`:

- add private fields:

```csharp
readonly RenderFrameExtractionService ExtractionService = new RenderFrameExtractionService();
readonly DirectX11RenderPlanBuilder RenderPlanBuilder = new DirectX11RenderPlanBuilder();
readonly DirectX11RenderPlanExecutor RenderPlanExecutor = new DirectX11RenderPlanExecutor();
```

- make `DirectX11Renderer3D` implement `IDirectX11RenderPassExecutor`
- replace the old `RenderCamera(...)` draw loop with:

```csharp
DirectX11RenderQueueSnapshotVisitor snapshotVisitor = new DirectX11RenderQueueSnapshotVisitor();
camera.RenderQueue3D.VisitOrdered(snapshotVisitor);

RenderFrameExtractionResult extraction = ExtractionService.Extract(
    [AssertCameraComponent(camera)],
    snapshotVisitor.Drawables,
    [],
    GetCapabilityProfile());
RenderFrame frame = AssertSingleFrame(extraction);
RenderPlan plan = ResolvePlanForCamera(frame);
DirectX11RenderPassExecutionContext executionContext = new DirectX11RenderPassExecutionContext(frame, surface);
RenderPlanExecutor.Execute(executionContext, plan, this);

renderer2D.RenderCamera(camera);
```

- add per-pass methods:

```csharp
public void ExecuteDepthPrepass(DirectX11RenderPassExecutionContext context) { ... }
public void ExecuteOpaqueForward(DirectX11RenderPassExecutionContext context) { ... }
public void ExecuteTransparentForward(DirectX11RenderPassExecutionContext context) { ... }
public void ExecutePresent(DirectX11RenderPassExecutionContext context) { ... }
```

- route the existing draw logic into a helper that filters submissions by transparency:

```csharp
void DrawFrameSubmissions(DirectX11RenderPassExecutionContext context, bool transparentOnly) {
    for (int index = 0; index < context.Frame.DrawableSubmissions.Count; index++) {
        RenderFrameDrawableSubmission submission = context.Frame.DrawableSubmissions[index];
        if (submission.IsTransparent != transparentOnly) {
            continue;
        }

        Visit(submission.Drawable);
    }
}
```

- keep `ExecuteDepthPrepass(...)` conservative for this slice: set targets and camera matrices, but do not introduce a new shader path yet. It only needs to run without throwing and be independently callable from the ordered plan.

Modify `engine/helengine.editor.tests/testing/TestDirectX11RenderManager3D.cs` to add:

```csharp
public List<string> ExecutedDrawStages { get; } = new List<string>();
public RenderPlan ForcedPlan { get; private set; }

public void ForcePlanForTest(RenderPlan plan) {
    ForcedPlan = plan;
}

protected override RenderPlan ResolvePlanForCamera(RenderFrame frame) {
    if (ForcedPlan != null) {
        return ForcedPlan;
    }

    return base.ResolvePlanForCamera(frame);
}

protected override void RecordExecutedDrawStage(string stageName, RenderFrameDrawableSubmission submission) {
    string transparencyName = submission.IsTransparent ? "Transparent" : "Opaque";
    ExecutedDrawStages.Add(string.Concat(stageName, ":", transparencyName));
}
```

- [ ] **Step 4: Re-run the focused renderer execution tests and verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RendererPlannedExecutionTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy powershell -Command "git add 'engine/helengine.directx11/rendering/DirectX11RenderQueueSnapshotVisitor.cs' 'engine/helengine.directx11/DirectX11Renderer3D.cs' 'engine/helengine.editor.tests/testing/TestDirectX11RenderManager3D.cs' 'engine/helengine.editor.tests/rendering/DirectX11RendererPlannedExecutionTests.cs'; git commit -m 'feat: execute dx11 geometry through render plans'"
```

### Task 4: Add committed rendering demo scenes under the project scene catalog

**Files:**
- Create: `test-project/assets/Scenes/rendering/opaque-basics.helen`
- Create: `test-project/assets/Scenes/rendering/transparency-order.helen`
- Create: `test-project/assets/Scenes/rendering/depth-prepass.helen`
- Create: `test-project/assets/Scenes/rendering/material-inputs.helen`
- Create: `engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs`

- [ ] **Step 1: Write the failing rendering scene catalog tests**

Create `engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs`:

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the committed rendering demo scene catalog.
    /// </summary>
    public class RenderingSceneCatalogTests {
        /// <summary>
        /// Ensures all committed rendering scenes deserialize as scene assets.
        /// </summary>
        [Theory]
        [InlineData("opaque-basics.helen")]
        [InlineData("transparency-order.helen")]
        [InlineData("depth-prepass.helen")]
        [InlineData("material-inputs.helen")]
        public void RenderingSceneCatalog_WhenSceneIsLoaded_DeserializesAsSceneAsset(string fileName) {
            string fullPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "test-project",
                "assets",
                "Scenes",
                "rendering",
                fileName);

            using FileStream stream = File.OpenRead(Path.GetFullPath(fullPath));
            SceneAsset asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

            Assert.NotNull(asset);
            Assert.NotEmpty(asset.RootEntities);
        }
    }
}
```

- [ ] **Step 2: Run the focused rendering scene catalog tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderingSceneCatalogTests -v minimal"
```

Expected: FAIL because the rendering scene files do not exist yet.

- [ ] **Step 3: Author and commit the rendering scene assets**

Create these committed scene files under `test-project/assets/Scenes/rendering/` using the existing scene save pipeline so the resulting `.helen` files are deterministic and loadable by the current runtime:

- `opaque-basics.helen`
  - one camera
  - three opaque cubes at distinct world positions
  - no transparent materials
- `transparency-order.helen`
  - one camera
  - one opaque background mesh
  - two transparent quads or planes at different depths
- `depth-prepass.helen`
  - one camera
  - overlapping opaque meshes arranged so front-to-back depth behavior is visible
- `material-inputs.helen`
  - one camera
  - one mesh using a material intended to exercise albedo, normal-map, and emissive authored inputs

Use the same scene save path rules already covered by `SceneSaveServiceTests` so scene ids resolve as:

```text
Scenes/rendering/opaque-basics.helen
Scenes/rendering/transparency-order.helen
Scenes/rendering/depth-prepass.helen
Scenes/rendering/material-inputs.helen
```

Do not replace `test-project/assets/Scenes/Bootstrap.helen` in this slice. These scenes are additive catalog entries for the demo-disc set.

- [ ] **Step 4: Re-run the focused rendering scene catalog tests and verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderingSceneCatalogTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy powershell -Command "git add 'test-project/assets/Scenes/rendering/opaque-basics.helen' 'test-project/assets/Scenes/rendering/transparency-order.helen' 'test-project/assets/Scenes/rendering/depth-prepass.helen' 'test-project/assets/Scenes/rendering/material-inputs.helen' 'engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs'; git commit -m 'feat: add rendering demo scene catalog'"
```

### Task 5: Final verification of the plan-driven DX11 execution slice

**Files:**
- Verify only; no new files required.

- [ ] **Step 1: Run the focused renderer validation suite**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~RenderFrameExtractionServiceTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11RenderPlanExecutorTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11RendererPlannedExecutionTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11RenderPlanBuilderTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11MaterialFeatureBindingTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11ShadowResourcePlanningTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~DirectX11PostProcessChainTests -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --no-restore --filter FullyQualifiedName~RenderingSceneCatalogTests -v minimal"
```

Expected: PASS for all focused renderer and scene-catalog tests.

- [ ] **Step 2: Run the affected builds**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.directx11\helengine.directx11.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
```

Expected: PASS, with only the pre-existing warning set.

- [ ] **Step 3: Sanity-check the committed rendering scenes in the project asset tree**

Run:

```powershell
rtk proxy cmd /c dir /b C:\dev\helworks\helengine\test-project\assets\Scenes\rendering
```

Expected output:

```text
depth-prepass.helen
material-inputs.helen
opaque-basics.helen
transparency-order.helen
```

- [ ] **Step 4: Commit any final fixups**

```powershell
rtk proxy powershell -Command "git status --short"
```

Expected: no remaining tracked source edits outside intentionally untracked plan files.

## Self-Review

- Spec coverage: this plan covers the first live DX11 execution slice, the extracted-frame population gap, the plan executor seam, the renderer integration path, and the committed rendering scene catalog requested for the demo-disc set.
- Placeholder scan: there are no `TODO` or `TBD` markers. The rendering scene task is explicit about exact file paths and required authored contents.
- Type consistency: the plan consistently uses `RenderFrameExtractionService`, `RenderFrameDrawableSubmission`, `RenderPlan`, `DirectX11RenderPlanExecutor`, `IDirectX11RenderPassExecutor`, and `DirectX11RenderPassExecutionContext` as the execution seam names.

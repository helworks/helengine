# Windows Forward Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first Windows-forward renderer foundation for HelEngine, with shared render-frame contracts, light and camera render settings, Windows pass planning, shadow-capable light objects, batching hooks, and a post-process-ready runtime shape that stays compatible with future simpler backends.

**Architecture:** Expand `RenderManager3D` into a shared renderer contract, add engine-owned render data and light/camera types in `helengine.core`, then refactor the DirectX11 backend to consume extracted frame data through explicit planning objects instead of one monolithic draw path. The first slice keeps the runtime forward-rendered, but shapes lights, materials, shadow resources, batching eligibility, and post-process configuration so a later deferred renderer or reduced DX9-style backend can consume the same authored scene and settings through different capability profiles.

**Tech Stack:** C# / .NET 9, xUnit, `helengine.core`, `helengine.directx11`, `helengine.editor`, existing graphics-profile persistence, DirectX 11 runtime code, HLSL shader assets.

---

## File Structure

### Shared render contracts and engine scene data

- Create: `engine/helengine.core/managers/rendering/RendererBackendCapabilityProfile.cs`
  Backend feature/capability declaration used by all renderers.
- Create: `engine/helengine.core/managers/rendering/RendererFeatureDowngradeMode.cs`
  Shared downgrade behavior enum for unsupported renderer features.
- Create: `engine/helengine.core/managers/rendering/RenderFrame.cs`
  Per-camera extracted frame payload.
- Create: `engine/helengine.core/managers/rendering/RenderFrameDrawableSubmission.cs`
  One visible drawable submission in extracted frame data.
- Create: `engine/helengine.core/managers/rendering/RenderFrameLightSubmission.cs`
  One visible light submission in extracted frame data.
- Create: `engine/helengine.core/managers/rendering/RenderFrameShadowCasterSubmission.cs`
  One extracted shadow-caster submission.
- Create: `engine/helengine.core/managers/rendering/RenderFrameBatchingMetadata.cs`
  Shared batching and instancing eligibility metadata.
- Create: `engine/helengine.core/managers/rendering/RenderFrameExtractionResult.cs`
  Container for extracted frame data and diagnostics.
- Create: `engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs`
  Shared extraction service that gathers cameras, drawables, lights, and shadow casters.
- Create: `engine/helengine.core/managers/rendering/RenderPassKind.cs`
  Shared pass kind enum.
- Create: `engine/helengine.core/managers/rendering/RenderPlan.cs`
  Backend-neutral planned pass list and selected resources.
- Modify: `engine/helengine.core/managers/rendering/RenderManager3D.cs`
  Grow backend contract to expose capabilities and plan/extract entry points.
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`
  Update test renderer stub for the new contract.

### Engine light and camera rendering settings

- Create: `engine/helengine.core/model/LightType.cs`
  Shared light family enum.
- Create: `engine/helengine.core/model/ShadowMapMode.cs`
  Shared shadow mode enum.
- Create: `engine/helengine.core/model/DepthPrepassMode.cs`
  Shared camera/prepass intent enum.
- Create: `engine/helengine.core/model/PostProcessTier.cs`
  Shared post-process quality enum.
- Create: `engine/helengine.core/model/CameraRenderSettings.cs`
  Per-camera render intent payload.
- Create: `engine/helengine.core/model/GraphicsBackendFeatureDefaults.cs`
  Platform/profile renderer-wide defaults payload.
- Create: `engine/helengine.core/components/LightComponent.cs`
  Base or unified light component.
- Create: `engine/helengine.core/components/DirectionalLightComponent.cs`
  Directional light scene component.
- Create: `engine/helengine.core/components/PointLightComponent.cs`
  Point light scene component.
- Create: `engine/helengine.core/components/SpotLightComponent.cs`
  Spot light scene component.
- Modify: `engine/helengine.core/components/CameraComponent.cs`
  Add camera render settings and runtime accessors.
- Modify: `engine/helengine.core/model/interfaces/ICamera.cs`
  Expose render settings and target behavior needed by extraction/planning.
- Modify: `engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs`
  Persist camera render settings.
- Modify: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
  Load camera render settings at runtime.
- Test: `engine/helengine.editor.tests/serialization/scene/CameraComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/CameraComponentLayerMaskTests.cs`

### Platform graphics profile integration

- Modify: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
  Add renderer-wide Windows profile settings.
- Create: `engine/helengine.core/content/RuntimeGraphicsRendererManifest.cs`
  Runtime-facing graphics renderer settings payload.
- Create: `engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs`
  Writes runtime renderer settings from platform graphics profiles.
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
  Stage renderer settings into build outputs.
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorRuntimeGraphicsRendererManifestWriterTests.cs`

### DirectX11 planning and execution

- Create: `engine/helengine.directx11/rendering/DirectX11RenderCapabilityProfile.cs`
  DX11 capability declaration.
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPlanBuilder.cs`
  Forward render pass planner for DX11.
- Create: `engine/helengine.directx11/rendering/DirectX11ShadowPlan.cs`
  Planned shadow resources and shadowed light selection.
- Create: `engine/helengine.directx11/rendering/DirectX11LightSelectionService.cs`
  Chooses active and shadowed lights within budget.
- Create: `engine/helengine.directx11/rendering/DirectX11BatchingPlanner.cs`
  Computes static/dynamic/instance batching decisions.
- Create: `engine/helengine.directx11/rendering/DirectX11PostProcessPlan.cs`
  Ordered post chain plan.
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
  Consume extraction/planning services and expose capabilities.
- Modify: `engine/helengine.directx11/DirectX11Renderer3DDebugInfoProvider.cs`
  Surface new planning state for inspection.
- Test: `engine/helengine.editor.tests/testing/TestDirectX11RenderManager3D.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11RenderPlanBuilderTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11LightSelectionServiceTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11BatchingPlannerTests.cs`

### Windows material and renderer feature binding

- Create: `engine/helengine.core/model/RuntimeMaterialLightingModel.cs`
  Shared lighting model enum.
- Modify: `engine/helengine.core/assets/RuntimeMaterial.cs`
  Add compact Windows-forward material feature flags and surface inputs.
- Modify: `engine/helengine.editor/components/ui/MaterialAssetView.cs`
  Surface Windows-forward material schema fields through the existing schema model if needed by the first slice.
- Modify: `engine/helengine.editor.tests/RuntimeMaterialTests.cs`
- Modify: `engine/helengine.directx11/assets/DirectX11MaterialResource.cs`
  Carry planned material feature binding state.
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
  Bind the compact PBR feature set in material creation.
- Test: `engine/helengine.editor.tests/RuntimeMaterialParentingTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs`

### Shadow resource execution and post-process foundations

- Create: `engine/helengine.core/managers/rendering/ShadowResourceKind.cs`
  Shared shadow resource family enum.
- Create: `engine/helengine.directx11/rendering/DirectX11ShadowMapAtlas.cs`
  Directional and spot shadow allocation helper.
- Create: `engine/helengine.directx11/rendering/DirectX11PointShadowResource.cs`
  Point-light shadow resource wrapper.
- Create: `engine/helengine.directx11/rendering/DirectX11PostProcessChain.cs`
  Runtime post-process chain executor shell.
- Modify: `engine/helengine.core/assets/RenderTarget.cs`
  Ensure render-target contract supports post-process chaining needs.
- Modify: `engine/helengine.directx11/assets/DirectX11RenderTargetResource.cs`
- Test: `engine/helengine.editor.tests/testing/TestRenderTarget.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11ShadowResourcePlanningTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11PostProcessChainTests.cs`

### Docs

- Modify: `docs/superpowers/specs/2026-05-01-graphics-profile-design.md`
  Cross-link the richer renderer settings direction once implementation starts to land.

---

### Task 1: Establish shared render-frame contracts in core

**Files:**
- Create: `engine/helengine.core/managers/rendering/RendererBackendCapabilityProfile.cs`
- Create: `engine/helengine.core/managers/rendering/RendererFeatureDowngradeMode.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrame.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameDrawableSubmission.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameLightSubmission.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameShadowCasterSubmission.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameBatchingMetadata.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameExtractionResult.cs`
- Create: `engine/helengine.core/managers/rendering/RenderFrameExtractionService.cs`
- Create: `engine/helengine.core/managers/rendering/RenderPassKind.cs`
- Create: `engine/helengine.core/managers/rendering/RenderPlan.cs`
- Modify: `engine/helengine.core/managers/rendering/RenderManager3D.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager3D.cs`
- Create: `engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs`

- [ ] **Step 1: Write the failing shared render-frame tests**

```csharp
using System;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies shared render-frame extraction and backend capability contracts.
    /// </summary>
    public class RenderFrameExtractionServiceTests {
        /// <summary>
        /// Ensures the extraction service returns one frame entry for one active camera and exposes backend capability metadata through the render manager contract.
        /// </summary>
        [Fact]
        public void Extract_WhenOneCameraExists_ReturnsFrameForThatCamera() {
            TestRenderManager3D renderManager = new TestRenderManager3D();
            RenderFrameExtractionService extractionService = new RenderFrameExtractionService();
            CameraComponent camera = new CameraComponent();

            RenderFrameExtractionResult result = extractionService.Extract(
                new[] { camera },
                Array.Empty<IDrawable3D>(),
                Array.Empty<LightComponent>(),
                renderManager.GetCapabilityProfile());

            RenderFrame frame = Assert.Single(result.Frames);
            Assert.Same(camera, frame.Camera);
            Assert.Equal(0, frame.DrawableSubmissions.Count);
            Assert.Equal(0, frame.LightSubmissions.Count);
            Assert.Equal(0, frame.ShadowCasterSubmissions.Count);
            Assert.True(result.BackendCapabilities.SupportsForwardRendering);
        }
    }
}
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderFrameExtractionServiceTests -v minimal"
```

Expected: FAIL because the shared render-frame types and extraction service do not exist yet.

- [ ] **Step 3: Implement the shared renderer contract and extraction shell**

```csharp
namespace helengine {
    /// <summary>
    /// Describes backend renderer capabilities and downgrade rules consumed by shared render planning.
    /// </summary>
    public class RendererBackendCapabilityProfile {
        /// <summary>
        /// Initializes one capability profile with the required renderer feature flags.
        /// </summary>
        public RendererBackendCapabilityProfile(
            bool supportsForwardRendering,
            bool supportsDeferredRendering,
            bool supportsHdr,
            bool supportsNormalMaps,
            int maximumVisibleLights,
            int maximumShadowedLights) {
            SupportsForwardRendering = supportsForwardRendering;
            SupportsDeferredRendering = supportsDeferredRendering;
            SupportsHdr = supportsHdr;
            SupportsNormalMaps = supportsNormalMaps;
            MaximumVisibleLights = maximumVisibleLights;
            MaximumShadowedLights = maximumShadowedLights;
        }

        /// <summary>
        /// Gets whether this backend supports forward rendering.
        /// </summary>
        public bool SupportsForwardRendering { get; }

        /// <summary>
        /// Gets whether this backend supports deferred rendering.
        /// </summary>
        public bool SupportsDeferredRendering { get; }

        /// <summary>
        /// Gets whether this backend supports HDR rendering.
        /// </summary>
        public bool SupportsHdr { get; }

        /// <summary>
        /// Gets whether this backend supports normal-map shading.
        /// </summary>
        public bool SupportsNormalMaps { get; }

        /// <summary>
        /// Gets the maximum number of visible lights the backend plans at once.
        /// </summary>
        public int MaximumVisibleLights { get; }

        /// <summary>
        /// Gets the maximum number of simultaneously shadowed lights.
        /// </summary>
        public int MaximumShadowedLights { get; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Extracts backend-neutral render frame data from scene-visible objects.
    /// </summary>
    public class RenderFrameExtractionService {
        /// <summary>
        /// Extracts one render frame per camera using the provided visible scene data.
        /// </summary>
        public RenderFrameExtractionResult Extract(
            IReadOnlyList<CameraComponent> cameras,
            IReadOnlyList<IDrawable3D> drawables,
            IReadOnlyList<LightComponent> lights,
            RendererBackendCapabilityProfile backendCapabilities) {
            if (cameras == null) {
                throw new ArgumentNullException(nameof(cameras));
            } else if (drawables == null) {
                throw new ArgumentNullException(nameof(drawables));
            } else if (lights == null) {
                throw new ArgumentNullException(nameof(lights));
            } else if (backendCapabilities == null) {
                throw new ArgumentNullException(nameof(backendCapabilities));
            }

            List<RenderFrame> frames = new List<RenderFrame>();
            for (int index = 0; index < cameras.Count; index++) {
                frames.Add(new RenderFrame(
                    cameras[index],
                    Array.Empty<RenderFrameDrawableSubmission>(),
                    Array.Empty<RenderFrameLightSubmission>(),
                    Array.Empty<RenderFrameShadowCasterSubmission>()));
            }

            return new RenderFrameExtractionResult(frames.ToArray(), backendCapabilities);
        }
    }
}
```

```csharp
public abstract class RenderManager3D : IDisposable {
    /// <summary>
    /// Gets the backend capability profile published by this renderer.
    /// </summary>
    public virtual RendererBackendCapabilityProfile GetCapabilityProfile() {
        return new RendererBackendCapabilityProfile(true, false, false, false, 0, 0);
    }
}
```

- [ ] **Step 4: Run the focused test to verify it passes**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderFrameExtractionServiceTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.core/managers/rendering engine/helengine.editor.tests/rendering/RenderFrameExtractionServiceTests.cs engine/helengine.editor.tests/testing/TestRenderManager3D.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add shared render frame contracts"
```

### Task 2: Add engine light objects and camera render settings

**Files:**
- Create: `engine/helengine.core/model/LightType.cs`
- Create: `engine/helengine.core/model/ShadowMapMode.cs`
- Create: `engine/helengine.core/model/DepthPrepassMode.cs`
- Create: `engine/helengine.core/model/PostProcessTier.cs`
- Create: `engine/helengine.core/model/CameraRenderSettings.cs`
- Create: `engine/helengine.core/components/LightComponent.cs`
- Create: `engine/helengine.core/components/DirectionalLightComponent.cs`
- Create: `engine/helengine.core/components/PointLightComponent.cs`
- Create: `engine/helengine.core/components/SpotLightComponent.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Modify: `engine/helengine.core/model/interfaces/ICamera.cs`
- Modify: `engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/CameraComponentPersistenceDescriptorTests.cs`
- Create: `engine/helengine.editor.tests/rendering/LightComponentTests.cs`

- [ ] **Step 1: Write failing tests for camera render settings persistence and light component defaults**

```csharp
[Fact]
public void CameraComponentPersistenceDescriptor_round_trips_render_settings() {
    CameraComponent camera = new CameraComponent();
    camera.RenderSettings.DepthPrepassMode = DepthPrepassMode.Always;
    camera.RenderSettings.ShadowDistance = 75f;
    camera.RenderSettings.PostProcessTier = PostProcessTier.High;

    JsonElement json = SerializeCamera(camera);
    CameraComponent deserialized = DeserializeCamera(json);

    Assert.Equal(DepthPrepassMode.Always, deserialized.RenderSettings.DepthPrepassMode);
    Assert.Equal(75f, deserialized.RenderSettings.ShadowDistance);
    Assert.Equal(PostProcessTier.High, deserialized.RenderSettings.PostProcessTier);
}

[Fact]
public void DirectionalLightComponent_defaults_to_shadow_capable_directional_light() {
    DirectionalLightComponent light = new DirectionalLightComponent();

    Assert.Equal(LightType.Directional, light.LightType);
    Assert.True(light.ShadowsEnabled);
    Assert.Equal(ShadowMapMode.Auto, light.ShadowMapMode);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CameraComponentPersistenceDescriptorTests|FullyQualifiedName~LightComponentTests -v minimal"
```

Expected: FAIL because the render settings payload and light components do not exist yet.

- [ ] **Step 3: Implement render settings and light components**

```csharp
namespace helengine {
    /// <summary>
    /// Stores per-camera rendering intent resolved by the active backend.
    /// </summary>
    public class CameraRenderSettings {
        /// <summary>
        /// Initializes default camera render settings.
        /// </summary>
        public CameraRenderSettings() {
            DepthPrepassMode = DepthPrepassMode.Automatic;
            ShadowDistance = 50f;
            PostProcessTier = PostProcessTier.Medium;
        }

        /// <summary>
        /// Gets or sets the camera depth prepass mode.
        /// </summary>
        public DepthPrepassMode DepthPrepassMode { get; set; }

        /// <summary>
        /// Gets or sets the shadow distance used by this camera.
        /// </summary>
        public float ShadowDistance { get; set; }

        /// <summary>
        /// Gets or sets the post-process quality tier.
        /// </summary>
        public PostProcessTier PostProcessTier { get; set; }
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Base scene light component shared by directional, point, and spot lights.
    /// </summary>
    public class LightComponent : Component {
        /// <summary>
        /// Initializes one light component with default intensity and shadow settings.
        /// </summary>
        public LightComponent(LightType lightType) {
            LightType = lightType;
            Color = Color.White;
            Intensity = 1f;
            ShadowsEnabled = true;
            ShadowMapMode = ShadowMapMode.Auto;
            Importance = 0;
        }

        /// <summary>
        /// Gets the engine light type.
        /// </summary>
        public LightType LightType { get; }

        /// <summary>
        /// Gets or sets the light color.
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Gets or sets the light intensity.
        /// </summary>
        public float Intensity { get; set; }

        /// <summary>
        /// Gets or sets whether shadows are enabled for this light.
        /// </summary>
        public bool ShadowsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the shadow-map mode requested by this light.
        /// </summary>
        public ShadowMapMode ShadowMapMode { get; set; }

        /// <summary>
        /// Gets or sets the render-importance value used for light prioritization.
        /// </summary>
        public int Importance { get; set; }
    }
}
```

```csharp
public class DirectionalLightComponent : LightComponent {
    /// <summary>
    /// Initializes a directional light component.
    /// </summary>
    public DirectionalLightComponent()
        : base(LightType.Directional) {
    }
}
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CameraComponentPersistenceDescriptorTests|FullyQualifiedName~LightComponentTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.core/components engine/helengine.core/model engine/helengine.core/model/interfaces/ICamera.cs engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs engine/helengine.editor.tests/serialization/scene/CameraComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/rendering/LightComponentTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add light objects and camera render settings"
```

### Task 3: Persist Windows renderer defaults in graphics profiles and build outputs

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Create: `engine/helengine.core/content/RuntimeGraphicsRendererManifest.cs`
- Create: `engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorRuntimeGraphicsRendererManifestWriterTests.cs`

- [ ] **Step 1: Write failing tests for runtime renderer manifest writing**

```csharp
[Fact]
public void Write_for_windows_graphics_profile_emits_depth_prepass_and_shadow_defaults() {
    EditorGraphicsProfileSettingsDocument profile = new EditorGraphicsProfileSettingsDocument();
    profile.Platforms["windows"].RendererDepthPrepassMode = DepthPrepassMode.Automatic;
    profile.Platforms["windows"].RendererShadowQualityTier = "high";
    profile.Platforms["windows"].RendererHdrEnabled = true;

    string outputRootPath = CreateTempDirectory();
    EditorRuntimeGraphicsRendererManifestWriter writer = new EditorRuntimeGraphicsRendererManifestWriter();
    writer.Write("windows", profile, outputRootPath);

    RuntimeGraphicsRendererManifest manifest = RuntimeGraphicsRendererManifest.ReadFromFile(Path.Combine(outputRootPath, "runtime-graphics-renderer.json"));
    Assert.Equal(DepthPrepassMode.Automatic, manifest.DepthPrepassMode);
    Assert.Equal("high", manifest.ShadowQualityTier);
    Assert.True(manifest.HdrEnabled);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorRuntimeGraphicsRendererManifestWriterTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests -v minimal"
```

Expected: FAIL because runtime renderer manifests are not yet written.

- [ ] **Step 3: Implement renderer-default graphics profile fields and manifest writing**

```csharp
public class RuntimeGraphicsRendererManifest {
    /// <summary>
    /// Initializes one runtime renderer manifest.
    /// </summary>
    public RuntimeGraphicsRendererManifest(
        DepthPrepassMode depthPrepassMode,
        string shadowQualityTier,
        bool hdrEnabled) {
        DepthPrepassMode = depthPrepassMode;
        ShadowQualityTier = shadowQualityTier ?? throw new ArgumentNullException(nameof(shadowQualityTier));
        HdrEnabled = hdrEnabled;
    }

    /// <summary>
    /// Gets the default runtime depth-prepass mode.
    /// </summary>
    public DepthPrepassMode DepthPrepassMode { get; }

    /// <summary>
    /// Gets the shadow quality tier identifier.
    /// </summary>
    public string ShadowQualityTier { get; }

    /// <summary>
    /// Gets whether HDR is enabled by default.
    /// </summary>
    public bool HdrEnabled { get; }
}
```

```csharp
public class EditorRuntimeGraphicsRendererManifestWriter {
    /// <summary>
    /// Writes the runtime renderer manifest for the selected platform graphics profile.
    /// </summary>
    public void Write(string platformId, EditorGraphicsProfileSettingsDocument profile, string outputRootPath) {
        EditorGraphicsProfilePlatformDocument platformProfile = profile.Platforms[platformId];
        RuntimeGraphicsRendererManifest manifest = new RuntimeGraphicsRendererManifest(
            platformProfile.RendererDepthPrepassMode,
            platformProfile.RendererShadowQualityTier,
            platformProfile.RendererHdrEnabled);

        string json = JsonSerializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(outputRootPath, "runtime-graphics-renderer.json"), json);
    }
}
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorRuntimeGraphicsRendererManifestWriterTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs engine/helengine.core/content/RuntimeGraphicsRendererManifest.cs engine/helengine.editor/managers/project/EditorRuntimeGraphicsRendererManifestWriter.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: persist windows renderer defaults"
```

### Task 4: Add DirectX11 capability publishing and forward pass planning

**Files:**
- Create: `engine/helengine.directx11/rendering/DirectX11RenderCapabilityProfile.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11RenderPlanBuilder.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11ShadowPlan.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11LightSelectionService.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11BatchingPlanner.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11PostProcessPlan.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3DDebugInfoProvider.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11RenderPlanBuilderTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11LightSelectionServiceTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11BatchingPlannerTests.cs`

- [ ] **Step 1: Write failing tests for light budgeting and pass planning**

```csharp
[Fact]
public void Build_for_hdr_camera_with_depth_prepass_and_transparency_emits_expected_pass_order() {
    DirectX11RenderPlanBuilder builder = new DirectX11RenderPlanBuilder();
    RenderFrame frame = CreateFrame(depthPrepassMode: DepthPrepassMode.Always, hasTransparent: true);

    RenderPlan plan = builder.Build(frame, DirectX11RenderCapabilityProfile.CreateDefault());

    Assert.Equal(new[] {
        RenderPassKind.DepthPrepass,
        RenderPassKind.Shadow,
        RenderPassKind.OpaqueForward,
        RenderPassKind.TransparentForward,
        RenderPassKind.PostProcess,
        RenderPassKind.Present
    }, plan.Passes.Select(pass => pass.Kind).ToArray());
}

[Fact]
public void SelectLights_when_budget_is_exceeded_prefers_highest_importance_entries() {
    DirectX11LightSelectionService service = new DirectX11LightSelectionService();
    RenderFrameLightSubmission[] selected = service.SelectVisibleLights(
        new[] {
            CreateLightSubmission(importance: 1),
            CreateLightSubmission(importance: 5),
            CreateLightSubmission(importance: 10)
        },
        maximumVisibleLights: 2);

    Assert.Equal(new[] { 10, 5 }, selected.Select(light => light.Importance).ToArray());
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RenderPlanBuilderTests|FullyQualifiedName~DirectX11LightSelectionServiceTests|FullyQualifiedName~DirectX11BatchingPlannerTests -v minimal"
```

Expected: FAIL because the planning services do not exist yet.

- [ ] **Step 3: Implement capability publishing and planning services**

```csharp
namespace helengine.directx11 {
    /// <summary>
    /// Publishes the default DirectX11 capability profile used by render planning.
    /// </summary>
    public static class DirectX11RenderCapabilityProfile {
        /// <summary>
        /// Creates the default DirectX11 renderer capability profile.
        /// </summary>
        public static RendererBackendCapabilityProfile CreateDefault() {
            return new RendererBackendCapabilityProfile(
                true,
                false,
                true,
                true,
                32,
                4);
        }
    }
}
```

```csharp
public class DirectX11LightSelectionService {
    /// <summary>
    /// Selects the visible lights that survive the DirectX11 light budget.
    /// </summary>
    public RenderFrameLightSubmission[] SelectVisibleLights(IReadOnlyList<RenderFrameLightSubmission> visibleLights, int maximumVisibleLights) {
        return visibleLights
            .OrderByDescending(light => light.Importance)
            .Take(maximumVisibleLights)
            .ToArray();
    }
}
```

```csharp
public class DirectX11RenderPlanBuilder {
    /// <summary>
    /// Builds the forward-render pass list for one extracted frame.
    /// </summary>
    public RenderPlan Build(RenderFrame frame, RendererBackendCapabilityProfile capabilities) {
        List<RenderPassKind> passes = new List<RenderPassKind>();
        if (frame.Camera.RenderSettings.DepthPrepassMode == DepthPrepassMode.Always) {
            passes.Add(RenderPassKind.DepthPrepass);
        }

        if (frame.ShadowCasterSubmissions.Count > 0) {
            passes.Add(RenderPassKind.Shadow);
        }

        passes.Add(RenderPassKind.OpaqueForward);
        if (frame.HasTransparentDrawables) {
            passes.Add(RenderPassKind.TransparentForward);
        }

        if (frame.Camera.RenderSettings.PostProcessTier != PostProcessTier.Disabled) {
            passes.Add(RenderPassKind.PostProcess);
        }

        passes.Add(RenderPassKind.Present);
        return new RenderPlan(passes);
    }
}
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11RenderPlanBuilderTests|FullyQualifiedName~DirectX11LightSelectionServiceTests|FullyQualifiedName~DirectX11BatchingPlannerTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.directx11/rendering engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.directx11/DirectX11Renderer3DDebugInfoProvider.cs engine/helengine.editor.tests/rendering
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add dx11 forward pass planning"
```

### Task 5: Add compact Windows material feature binding

**Files:**
- Create: `engine/helengine.core/model/RuntimeMaterialLightingModel.cs`
- Modify: `engine/helengine.core/assets/RuntimeMaterial.cs`
- Modify: `engine/helengine.directx11/assets/DirectX11MaterialResource.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.editor.tests/RuntimeMaterialTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs`

- [ ] **Step 1: Write failing tests for compact PBR feature binding**

```csharp
[Fact]
public void BuildMaterialFromRaw_sets_compact_pbr_feature_flags() {
    MaterialAsset materialAsset = CreateMaterialAsset();
    materialAsset.NormalTextureAssetId = "normals";
    materialAsset.EmissiveTextureAssetId = "emissive";

    TestDirectX11RenderManager3D renderer = new TestDirectX11RenderManager3D();
    RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, CreateShaderAsset());

    Assert.Equal(RuntimeMaterialLightingModel.MetalRoughPbr, material.LightingModel);
    Assert.True(material.SupportsNormalMapping);
    Assert.True(material.SupportsEmissive);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeMaterialTests|FullyQualifiedName~DirectX11MaterialFeatureBindingTests -v minimal"
```

Expected: FAIL because runtime materials do not yet expose compact PBR feature metadata.

- [ ] **Step 3: Implement compact Windows material feature metadata**

```csharp
public class RuntimeMaterial {
    /// <summary>
    /// Gets or sets the runtime lighting model expected by this material.
    /// </summary>
    public RuntimeMaterialLightingModel LightingModel { get; set; }

    /// <summary>
    /// Gets or sets whether the material binds a normal map.
    /// </summary>
    public bool SupportsNormalMapping { get; set; }

    /// <summary>
    /// Gets or sets whether the material binds emissive inputs.
    /// </summary>
    public bool SupportsEmissive { get; set; }
}
```

```csharp
RuntimeMaterial runtimeMaterial = new RuntimeMaterial();
runtimeMaterial.LightingModel = RuntimeMaterialLightingModel.MetalRoughPbr;
runtimeMaterial.SupportsNormalMapping = !string.IsNullOrWhiteSpace(materialAsset.NormalTextureAssetId);
runtimeMaterial.SupportsEmissive = !string.IsNullOrWhiteSpace(materialAsset.EmissiveTextureAssetId);
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeMaterialTests|FullyQualifiedName~DirectX11MaterialFeatureBindingTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.core/assets/RuntimeMaterial.cs engine/helengine.core/model/RuntimeMaterialLightingModel.cs engine/helengine.directx11/assets/DirectX11MaterialResource.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.editor.tests/RuntimeMaterialTests.cs engine/helengine.editor.tests/rendering/DirectX11MaterialFeatureBindingTests.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add compact windows material feature binding"
```

### Task 6: Add shadow resource planning and post-process chain foundations

**Files:**
- Create: `engine/helengine.core/managers/rendering/ShadowResourceKind.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11ShadowMapAtlas.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11PointShadowResource.cs`
- Create: `engine/helengine.directx11/rendering/DirectX11PostProcessChain.cs`
- Modify: `engine/helengine.core/assets/RenderTarget.cs`
- Modify: `engine/helengine.directx11/assets/DirectX11RenderTargetResource.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11ShadowResourcePlanningTests.cs`
- Create: `engine/helengine.editor.tests/rendering/DirectX11PostProcessChainTests.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderTarget.cs`

- [ ] **Step 1: Write failing tests for shadow resource planning and post chain ordering**

```csharp
[Fact]
public void PlanDirectionalAndSpotShadows_allocates_shadow_atlas_entries() {
    DirectX11ShadowMapAtlas atlas = new DirectX11ShadowMapAtlas(2048, 2048);
    ShadowAtlasAllocation[] allocations = atlas.PlanAllocations(new[] {
        CreateShadowedLight(LightType.Directional),
        CreateShadowedLight(LightType.Spot)
    });

    Assert.Equal(2, allocations.Length);
}

[Fact]
public void Build_when_post_process_tier_is_disabled_skips_post_chain() {
    DirectX11PostProcessChain chain = new DirectX11PostProcessChain();
    CameraRenderSettings renderSettings = new CameraRenderSettings();
    renderSettings.PostProcessTier = PostProcessTier.Disabled;

    PostProcessPass[] passes = chain.Build(renderSettings);
    Assert.Empty(passes);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11ShadowResourcePlanningTests|FullyQualifiedName~DirectX11PostProcessChainTests -v minimal"
```

Expected: FAIL because the shadow resource wrappers and post chain shell do not exist yet.

- [ ] **Step 3: Implement the shadow planning and post-process shells**

```csharp
public class DirectX11ShadowMapAtlas {
    /// <summary>
    /// Initializes one shadow-map atlas planner.
    /// </summary>
    public DirectX11ShadowMapAtlas(int width, int height) {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the atlas width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the atlas height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Plans one atlas allocation per shadowed non-point light.
    /// </summary>
    public ShadowAtlasAllocation[] PlanAllocations(IReadOnlyList<RenderFrameLightSubmission> lights) {
        List<ShadowAtlasAllocation> allocations = new List<ShadowAtlasAllocation>();
        for (int index = 0; index < lights.Count; index++) {
            if (lights[index].LightType == LightType.Point) {
                continue;
            }

            allocations.Add(new ShadowAtlasAllocation(index, 0, 0, Width / 2, Height / 2));
        }

        return allocations.ToArray();
    }
}
```

```csharp
public class DirectX11PostProcessChain {
    /// <summary>
    /// Builds the ordered post-process pass list for one camera.
    /// </summary>
    public PostProcessPass[] Build(CameraRenderSettings renderSettings) {
        if (renderSettings.PostProcessTier == PostProcessTier.Disabled) {
            return Array.Empty<PostProcessPass>();
        }

        return new[] {
            new PostProcessPass("tonemap"),
            new PostProcessPass("bloom"),
            new PostProcessPass("fxaa")
        };
    }
}
```

- [ ] **Step 4: Run the focused tests to verify they pass**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~DirectX11ShadowResourcePlanningTests|FullyQualifiedName~DirectX11PostProcessChainTests -v minimal"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add engine/helengine.core/managers/rendering/ShadowResourceKind.cs engine/helengine.core/assets/RenderTarget.cs engine/helengine.directx11/rendering/DirectX11ShadowMapAtlas.cs engine/helengine.directx11/rendering/DirectX11PointShadowResource.cs engine/helengine.directx11/rendering/DirectX11PostProcessChain.cs engine/helengine.directx11/assets/DirectX11RenderTargetResource.cs engine/helengine.editor.tests/rendering/DirectX11ShadowResourcePlanningTests.cs engine/helengine.editor.tests/rendering/DirectX11PostProcessChainTests.cs engine/helengine.editor.tests/testing/TestRenderTarget.cs
rtk proxy git -C C:\dev\helworks\helengine commit -m "feat: add dx11 shadow and postprocess foundations"
```

### Task 7: Final verification and documentation alignment

**Files:**
- Modify: `docs/superpowers/specs/2026-05-01-graphics-profile-design.md`
- Modify: `docs/superpowers/specs/2026-05-04-windows-forward-renderer-design.md`

- [ ] **Step 1: Update the graphics-profile spec with renderer-setting integration notes**

Add this section to `docs/superpowers/specs/2026-05-01-graphics-profile-design.md`:

```markdown
## Renderer Settings Follow-Up

The initial graphics profile only standardized width, height, fullscreen, vsync, and backend target.

The Windows forward renderer expands that role. Platform graphics profiles also carry renderer-default settings such as depth prepass mode, HDR default, shadow quality tier, and post-processing tier. These values are staged into runtime-facing build data and consumed by the player startup and renderer planning path.
```

- [ ] **Step 2: Run the focused renderer validation suite**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RenderFrameExtractionServiceTests|FullyQualifiedName~CameraComponentPersistenceDescriptorTests|FullyQualifiedName~LightComponentTests|FullyQualifiedName~EditorRuntimeGraphicsRendererManifestWriterTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~DirectX11RenderPlanBuilderTests|FullyQualifiedName~DirectX11LightSelectionServiceTests|FullyQualifiedName~DirectX11BatchingPlannerTests|FullyQualifiedName~RuntimeMaterialTests|FullyQualifiedName~DirectX11MaterialFeatureBindingTests|FullyQualifiedName~DirectX11ShadowResourcePlanningTests|FullyQualifiedName~DirectX11PostProcessChainTests -v minimal"
```

Expected: PASS.

- [ ] **Step 3: Run the affected project builds**

Run:

```powershell
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.directx11\helengine.directx11.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
rtk proxy cmd /c "C:\dev\helworks\helengine\dotnetw.cmd build C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj -c Debug --no-restore -p:UseSharedCompilation=false -m:1 -v minimal"
```

Expected: PASS.

- [ ] **Step 4: Commit**

```powershell
rtk proxy git -C C:\dev\helworks\helengine add docs/superpowers/specs/2026-05-01-graphics-profile-design.md docs/superpowers/specs/2026-05-04-windows-forward-renderer-design.md
rtk proxy git -C C:\dev\helworks\helengine commit -m "docs: align renderer and graphics profile specs"
```

## Self-Review

- Spec coverage: the plan covers shared render-frame contracts, `RenderManager3D` expansion, engine light objects, per-camera render settings, Windows graphics profile integration, DirectX11 pass planning, compact PBR feature metadata, shadow resource planning, post-process chain foundations, and validation coverage. Deferred rendering remains intentionally out of scope, matching the spec.
- Placeholder scan: the task steps contain no unresolved placeholders or deferred implementation markers. Each task names exact files, test targets, and commands.
- Type consistency: the plan consistently uses `RendererBackendCapabilityProfile`, `RenderFrame`, `RenderPlan`, `CameraRenderSettings`, `LightComponent`, `DepthPrepassMode`, `PostProcessTier`, `RuntimeGraphicsRendererManifest`, `DirectX11RenderPlanBuilder`, and `RuntimeMaterialLightingModel` across tasks.

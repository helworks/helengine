# Directional Shadow Plaza Readability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retune the city-owned `Directional Shadow Plaza` generator so the canonical source scene reads clearly in editor and player with `Intensity = 1`, camera far plane `= 200`, and more legible authored motion and silhouettes.

**Architecture:** Keep all changes in the city scene-generation path. Update `DirectionalShadowPlazaSceneFactory` to retune object proportions, motion parameters, camera framing, and light defaults, then regenerate the authored `.helen` scene through the existing city rendering-scene command and manually verify in editor and Windows player.

**Tech Stack:** C# / .NET 9, city user-side scene generation, `SceneAsset`, `SceneComponentAssetRecord`, helengine editor command pipeline, manual editor/player verification

---

## File Structure

**Modify:**
- `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`
  Responsibility: canonical authored scene layout, camera defaults, directional light defaults, and motion parameter tuning for the plaza showcase.

**Regenerate:**
- `C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen`
  Responsibility: generated scene asset that reflects the updated authored plaza defaults.

**Use without modifying:**
- `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneGenerator.cs`
  Responsibility: current city-side rendering scene generation entrypoint.
- `C:\dev\helprojs\city\assets\codebase\menu.tools\GenerateRenderingScenesCommand.cs`
  Responsibility: current city-side editor command that regenerates rendering scenes.

**Deliberately unchanged in this slice:**
- `engine/helengine.directx11/...`
- `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_render_bridge.cpp`
  Rationale: renderer work is out of scope for this readability retune.

### Task 1: Retune The Canonical Plaza Scene Factory

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`

- [ ] **Step 1: Retune the tower, hero, and receiver authored transforms for readability**

Update the canonical root-entity construction so the rotating silhouettes are asymmetric by default and the overall composition reads better from the orbit camera.

Replace the current tower and hero shape/layout section with values in this range:

```csharp
return new SceneAsset {
    Id = SceneId,
    AssetReferences = new[] {
        planeReference,
        cubeReference,
        standardMaterialReference
    },
    RootEntities = new[] {
        CreateCameraEntity(),
        CreateDirectionalLightEntity(),
        CreateGroundEntity(planeReference, standardMaterialReference),
        CreateTowerEntity("directional-shadow-plaza-tower-left", "DirectionalShadowPlazaTowerLeft", new float3(-12f, 4f, -8f), new float3(3f, 8f, 1.5f), -0.35f, 0.20f, cubeReference, standardMaterialReference),
        CreateTowerEntity("directional-shadow-plaza-tower-center", "DirectionalShadowPlazaTowerCenter", new float3(0f, 5f, 0f), new float3(4f, 10f, 2f), 0f, 0.09f, cubeReference, standardMaterialReference),
        CreateTowerEntity("directional-shadow-plaza-tower-right", "DirectionalShadowPlazaTowerRight", new float3(13f, 3.5f, 9f), new float3(2f, 7f, 4.5f), 0.45f, -0.15f, cubeReference, standardMaterialReference),
        CreateOrbitHeroEntity(cubeReference, standardMaterialReference),
        CreateReceiverEntity("directional-shadow-plaza-receiver-a", "DirectionalShadowPlazaReceiverA", new float3(-18f, 0.5f, 16f), new float3(2f, 1f, 2f), cubeReference, standardMaterialReference),
        CreateReceiverEntity("directional-shadow-plaza-receiver-b", "DirectionalShadowPlazaReceiverB", new float3(-8f, 1.5f, 19f), new float3(3f, 3f, 2f), cubeReference, standardMaterialReference),
        CreateReceiverEntity("directional-shadow-plaza-receiver-c", "DirectionalShadowPlazaReceiverC", new float3(16f, 1f, -14f), new float3(2f, 2f, 5f), cubeReference, standardMaterialReference),
        CreateReceiverEntity("directional-shadow-plaza-receiver-d", "DirectionalShadowPlazaReceiverD", new float3(17f, 2f, 12f), new float3(4f, 4f, 2f), cubeReference, standardMaterialReference)
    }
};
```

- [ ] **Step 2: Retune the authored camera framing and far plane**

Update `CreateCameraEntity()` and `WriteCameraPayload()` so the camera keeps the plaza readable while extending the view depth to `200`.

Use this shape:

```csharp
SceneEntityAsset CreateCameraEntity() {
    float4 orientation;
    float4.CreateFromYawPitchRoll(0f, -0.30f, 0f, out orientation);
    return new SceneEntityAsset {
        Id = "directional-shadow-plaza-camera",
        Name = "DirectionalShadowPlazaCamera",
        LocalPosition = new float3(0f, 22f, 72f),
        LocalScale = float3.One,
        LocalOrientation = orientation,
        Components = new[] {
            CreateCameraComponentRecord(),
            RenderingScriptComponentRecordFactory.CreateCameraOrbitRecord(1, new float3(0f, 0f, 0f), 72f, 22f, 0f, 0.10f, -0.30f)
        },
        Children = Array.Empty<SceneEntityAsset>()
    };
}
```

```csharp
writer.WriteField(
    "RenderSettings",
    fieldWriter => SceneComponentBinaryFieldEncoding.WriteCameraRenderSettings(
        fieldWriter,
        new CameraRenderSettings {
            DepthPrepassMode = DepthPrepassMode.Auto,
            ShadowDistance = 60f,
            FarPlaneDistance = 200f,
            PostProcessTier = PostProcessTier.Disabled
        }));
```

If `CameraRenderSettings` in this codebase uses a different far-plane property name, use the real property name there and keep the value `200f`.

- [ ] **Step 3: Retune the authored directional light defaults and sun sweep**

Update `CreateDirectionalLightEntity()` and `WriteDirectionalLightPayload()` so the plaza uses the approved light defaults and a less aggressive sun sweep.

Use this target shape:

```csharp
SceneEntityAsset CreateDirectionalLightEntity() {
    float4 orientation;
    float4.CreateFromYawPitchRoll(0f, -0.95f, 0f, out orientation);
    return new SceneEntityAsset {
        Id = "directional-shadow-plaza-sun",
        Name = "DirectionalShadowPlazaSun",
        LocalPosition = new float3(0f, 18f, 0f),
        LocalScale = float3.One,
        LocalOrientation = orientation,
        Components = new[] {
            CreateDirectionalLightComponentRecord(1f, 60f),
            RenderingScriptComponentRecordFactory.CreateSunSweepRecord(1, -0.45f, 0.45f, -0.95f, 0.18f)
        },
        Children = Array.Empty<SceneEntityAsset>()
    };
}
```

```csharp
DirectionalLightComponent lightComponent = new DirectionalLightComponent {
    Color = new float4(1f, 0.96f, 0.90f, 1f),
    Intensity = intensity,
    ShadowsEnabled = true,
    ShadowMapMode = ShadowMapMode.Forced,
    ShadowStrength = 1f,
    ShadowDistance = shadowDistance
};
```

The important authored default is `Intensity = 1f`.

- [ ] **Step 4: Retune the hero orbit so it does not visually cancel the camera**

Update `CreateOrbitHeroEntity(...)` so the hero remains active, but reads clearly against the slower camera orbit.

Use this target shape:

```csharp
SceneEntityAsset CreateOrbitHeroEntity(SceneAssetReference modelReference, SceneAssetReference materialReference) {
    return new SceneEntityAsset {
        Id = "directional-shadow-plaza-hero",
        Name = "DirectionalShadowPlazaHero",
        LocalPosition = new float3(0f, 3f, 14f),
        LocalScale = new float3(3f, 6f, 1.25f),
        LocalOrientation = float4.Identity,
        Components = new[] {
            CreateMeshComponentRecord(modelReference, materialReference),
            RenderingScriptComponentRecordFactory.CreateOrbitRecord(1, new float3(0f, 0f, 0f), 14f, 3f, 0.25f, -0.28f)
        },
        Children = Array.Empty<SceneEntityAsset>()
    };
}
```

The important authored change is that the hero no longer runs at a near-match with the camera orbit.

- [ ] **Step 5: Review the file diff before regeneration**

Run:

```powershell
git diff -- "C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs"
```

Expected: only authored plaza readability changes in the city scene factory.

### Task 2: Regenerate The City Rendering Scene Asset

**Files:**
- Modify by generation: `C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen`
- Use: `C:\dev\helprojs\city\assets\codebase\menu.tools\GenerateRenderingScenesCommand.cs`

- [ ] **Step 1: Regenerate the rendering scenes from the existing city command path**

Run the existing city-side generation path. If using the editor UI, use:

```text
Demo -> Generate Rendering Scenes
```

If using the headless path already established for this project, use the equivalent command route that executes `menu.generate-rendering-scenes` against the city project.

Expected: the city rendering scene set is regenerated successfully, and `directional_shadow_plaza.helen` is rewritten.

- [ ] **Step 2: Inspect the generated plaza scene artifact timestamp**

Run:

```powershell
Get-Item "C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen" | Select-Object FullName,Length,LastWriteTime
```

Expected: the file exists and has a fresh timestamp from the regeneration step.

- [ ] **Step 3: Review the generated scene diff**

Run:

```powershell
git diff -- "C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen"
```

Expected: the generated scene changes reflect the new camera/light/motion/layout tuning only.

- [ ] **Step 4: Confirm no unrelated city scene files changed**

Run:

```powershell
git status --short "C:\dev\helprojs\city\assets\scenes\rendering" "C:\dev\helprojs\city\assets\codebase\rendering.tools"
```

Expected: only the plaza generator source file and the generated plaza scene show up for this slice.

- [ ] **Step 5: Save the work before manual verification**

Run:

```powershell
git add "C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs" "C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen"
```

Expected: the source and generated scene are both staged together for the readability pass.

### Task 3: Manual Verification In Editor And Player

**Files:**
- Verify: `C:\dev\helprojs\city\assets\scenes\rendering\directional_shadow_plaza.helen`
- Verify: `C:\dev\helprojs\output\windows\helengine_windows.exe`

- [ ] **Step 1: Open the updated plaza scene in the editor**

Run the editor against the city project and open:

```text
scenes/rendering/directional_shadow_plaza.helen
```

Expected:
- the scene opens without script or persistence errors
- the tower silhouettes read as asymmetric
- the center tower still anchors the composition, but no longer looks like a featureless square prism

- [ ] **Step 2: Check the authored motion readability in the editor**

Observe the running scene in the editor.

Expected:
- camera orbit stays active
- left, center, and right towers rotate distinctly
- hero orbit stays visible and no longer visually cancels the camera
- sun sweep remains active but less dominant than before

- [ ] **Step 3: Rebuild the Windows player export**

Run:

```powershell
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
```

Expected: `Build completed for platform 'windows': C:\dev\helprojs\output\windows`

- [ ] **Step 4: Launch the rebuilt Windows player and enter the plaza**

Run:

```powershell
C:\dev\helprojs\output\windows\helengine_windows.exe
```

Expected:
- the player starts successfully
- the plaza scene remains readable without cooked artifact patching
- shadows stay stable
- the new authored readability improvements carry through to the player

- [ ] **Step 5: Commit the readability retune**

Run:

```powershell
git commit -m "feat: retune directional shadow plaza readability"
```

Expected: one commit containing only the city plaza source retune and regenerated scene asset for this pass.

## Self-Review

- Spec coverage: this plan covers the approved scope only: source-scene retune, `Intensity = 1`, camera far plane `= 200`, regeneration, and manual verification.
- Placeholder scan: no test placeholders were added because the approved scope explicitly skips new automated tests for this iterative pass.
- Type consistency: the plan uses the existing city generator entrypoints and keeps the work centered on `DirectionalShadowPlazaSceneFactory.cs` and the generated `.helen` output.

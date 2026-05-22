# City Rendering Motion Component Ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the directional-shadow motion components and `RotateComponent` fully into the city project and remove all engine serializer, deserializer, and packaging special cases for them.

**Architecture:** The migration is a clean ownership break. `city` becomes the only owner of the five motion behaviors, while `helengine.core` and `helengine.editor` keep only generic script-component infrastructure. Affected city scenes must serialize these components through the normal automatic script-component path, with no engine-defined ids or payload rewrite logic.

**Tech Stack:** C#, helengine scene serialization/runtime loading, city scene generators, Windows export pipeline, xUnit, RTK command wrapper

---

## File Structure

### City-owned runtime and authoring files

- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowCameraOrbitComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowOrbitComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowSunSweepComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowTowerSpinComponent.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering\RotateComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\ScaledCubeSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotlightStreetSliceSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingScriptComponentRecordFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DemoDiscSceneComponentRecordFactory.cs`

### Engine files to delete or simplify

- Delete: `engine\helengine.core\components\DirectionalShadowCameraOrbitComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowOrbitComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowSunSweepComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowTowerSpinComponent.cs`
- Delete: `engine\helengine.core\components\RotateComponent.cs`
- Delete: `engine\helengine.core\scene\DirectionalShadowMotionComponentScenePayloadSerializer.cs`
- Modify: `engine\helengine.core\scene\runtime\RuntimeComponentRegistry.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowCameraOrbitComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowOrbitComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowSunSweepComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowTowerSpinComponentDeserializer.cs`
- Modify: `engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`

### Tests and validation support

- Modify: `engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs`
- Modify: `engine\helengine.editor.tests\rendering\RenderingSceneCatalogTests.cs`
- Modify: `engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine\helengine.editor.tests\managers\project\EditorPlatformCodeCookServiceTests.cs`

---

### Task 1: Lock the New Ownership Boundary with Failing Tests

**Files:**
- Modify: `engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs`
- Modify: `engine\helengine.editor.tests\rendering\RenderingSceneCatalogTests.cs`
- Modify: `engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write failing assertions that the engine no longer owns these component ids**

Add or update tests so they assert city-owned script component ids remain intact and engine-owned ids are absent. Use assertions shaped like this:

```csharp
[Fact]
public void ComponentPersistenceRegistry_DoesNotExposeEngineDirectionalShadowDescriptors() {
    ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();

    Assert.Throws<KeyNotFoundException>(() => registry.GetDescriptor("helengine.DirectionalShadowCameraOrbitComponent"));
    Assert.Throws<KeyNotFoundException>(() => registry.GetDescriptor("helengine.DirectionalShadowOrbitComponent"));
    Assert.Throws<KeyNotFoundException>(() => registry.GetDescriptor("helengine.DirectionalShadowSunSweepComponent"));
    Assert.Throws<KeyNotFoundException>(() => registry.GetDescriptor("helengine.DirectionalShadowTowerSpinComponent"));
}
```

Update the build packager tests to assert that city script type ids survive packaging instead of being rewritten:

```csharp
Assert.Equal("city.rendering.DirectionalShadowCameraOrbitComponent, gameplay", packagedRecord.ComponentTypeId);
Assert.Equal("city.rendering.DirectionalShadowOrbitComponent, gameplay", packagedRecord.ComponentTypeId);
Assert.Equal("city.rendering.DirectionalShadowSunSweepComponent, gameplay", packagedRecord.ComponentTypeId);
Assert.Equal("city.rendering.DirectionalShadowTowerSpinComponent, gameplay", packagedRecord.ComponentTypeId);
```

- [ ] **Step 2: Run the focused tests to verify they fail before implementation**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~ComponentPersistenceRegistryTests|FullyQualifiedName~RenderingSceneCatalogTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: failures showing engine-owned directional-shadow handling still exists.

- [ ] **Step 3: Commit the red test state**

```powershell
git add engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs engine/helengine.editor.tests/rendering/RenderingSceneCatalogTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "Add red tests for city motion component ownership"
```

### Task 2: Make City the Only Runtime Owner of the Five Components

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowCameraOrbitComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowOrbitComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowSunSweepComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowTowerSpinComponent.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering\RotateComponent.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\ScaledCubeSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotlightStreetSliceSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingScriptComponentRecordFactory.cs`

- [ ] **Step 1: Normalize the four directional-shadow components as plain city script components**

Ensure each city file contains only the runtime behavior and no engine serialized id constant. The structure should look like:

```csharp
namespace city.rendering {
    /// <summary>
    /// Rotates the demonstration camera around the plaza scene so directional shadow movement is visible from multiple angles.
    /// </summary>
    public sealed class DirectionalShadowCameraOrbitComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the orbit radius in world units.
        /// </summary>
        public float OrbitRadius { get; set; }

        /// <summary>
        /// Advances the orbit using the frame delta time.
        /// </summary>
        public override void Update() {
            double angle = Parent.Orientation.GetYawRadians();
            angle += Time.DeltaTime;
            Parent.Position = new Vector3((float)(Math.Cos(angle) * OrbitRadius), Parent.Position.Y, (float)(Math.Sin(angle) * OrbitRadius));
        }
    }
}
```

Keep each component’s existing behavior, but remove any dependency on engine-owned component ids or helper serializers.

- [ ] **Step 2: Add the city-owned `RotateComponent`**

Create `C:\dev\helprojs\city\assets\codebase\rendering\RotateComponent.cs` as a plain script component mirroring the generic rotation behavior:

```csharp
namespace city.rendering {
    /// <summary>
    /// Applies a continuous Euler-angle rotation to the owning entity for authored city showcase scenes.
    /// </summary>
    public sealed class RotateComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the angular velocity in degrees per second on each axis.
        /// </summary>
        public Vector3 DegreesPerSecond { get; set; }

        /// <summary>
        /// Rotates the entity every frame using delta time.
        /// </summary>
        public override void Update() {
            Vector3 rotationDelta = DegreesPerSecond * Time.DeltaTime;
            Parent.Orientation *= Quaternion.CreateFromYawPitchRoll(rotationDelta.Y.ToRadians(), rotationDelta.X.ToRadians(), rotationDelta.Z.ToRadians());
        }
    }
}
```

Use the repo’s `double` math preference if the existing city rendering components already follow that pattern.

- [ ] **Step 3: Repoint city scene factories and record factories to city-owned types only**

Update the scene generators so their type ids and instantiations point at city-owned classes:

```csharp
const string CameraOrbitTypeId = "city.rendering.DirectionalShadowCameraOrbitComponent, gameplay";
const string OrbitTypeId = "city.rendering.DirectionalShadowOrbitComponent, gameplay";
const string SunSweepTypeId = "city.rendering.DirectionalShadowSunSweepComponent, gameplay";
const string TowerSpinTypeId = "city.rendering.DirectionalShadowTowerSpinComponent, gameplay";
const string RotateTypeId = "city.rendering.RotateComponent, gameplay";
```

Any existing `gameplay.rendering.*` constant should be normalized in the same pass instead of left behind.

- [ ] **Step 4: Run focused city scene catalog tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~RenderingSceneCatalogTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: still failing until engine special cases are removed, but city scene generation should now reference city-owned types consistently.

- [ ] **Step 5: Commit the city ownership move**

```powershell
git add C:/dev/helprojs/city/assets/codebase/rendering/DirectionalShadowCameraOrbitComponent.cs C:/dev/helprojs/city/assets/codebase/rendering/DirectionalShadowOrbitComponent.cs C:/dev/helprojs/city/assets/codebase/rendering/DirectionalShadowSunSweepComponent.cs C:/dev/helprojs/city/assets/codebase/rendering/DirectionalShadowTowerSpinComponent.cs C:/dev/helprojs/city/assets/codebase/rendering/RotateComponent.cs C:/dev/helprojs/city/assets/codebase/rendering.tools/DirectionalShadowPlazaSceneFactory.cs C:/dev/helprojs/city/assets/codebase/rendering.tools/ScaledCubeSceneFactory.cs C:/dev/helprojs/city/assets/codebase/rendering.tools/SpotlightStreetSliceSceneFactory.cs C:/dev/helprojs/city/assets/codebase/rendering.tools/RenderingScriptComponentRecordFactory.cs
git commit -m "Move rendering motion components into city"
```

### Task 3: Remove Engine Serializer and Runtime Deserializer Ownership

**Files:**
- Delete: `engine\helengine.core\components\DirectionalShadowCameraOrbitComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowOrbitComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowSunSweepComponent.cs`
- Delete: `engine\helengine.core\components\DirectionalShadowTowerSpinComponent.cs`
- Delete: `engine\helengine.core\components\RotateComponent.cs`
- Delete: `engine\helengine.core\scene\DirectionalShadowMotionComponentScenePayloadSerializer.cs`
- Modify: `engine\helengine.core\scene\runtime\RuntimeComponentRegistry.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowCameraOrbitComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowOrbitComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowSunSweepComponentDeserializer.cs`
- Delete: `engine\helengine.core\scene\runtime\RuntimeDirectionalShadowTowerSpinComponentDeserializer.cs`

- [ ] **Step 1: Delete the engine-owned component classes and directional-shadow payload serializer**

Remove the files listed above and confirm no compile-time references remain to:

```text
DirectionalShadowCameraOrbitComponent.SerializedComponentTypeId
DirectionalShadowOrbitComponent.SerializedComponentTypeId
DirectionalShadowSunSweepComponent.SerializedComponentTypeId
DirectionalShadowTowerSpinComponent.SerializedComponentTypeId
DirectionalShadowMotionComponentScenePayloadSerializer
RotateComponent
```

- [ ] **Step 2: Remove runtime component registry registrations for the deleted deserializers**

Update `RuntimeComponentRegistry.cs` by removing the directional-shadow entries so the registry retains only generic and still-supported engine component deserializers:

```csharp
public static RuntimeComponentRegistry CreateDefault() {
    RuntimeComponentRegistry registry = new RuntimeComponentRegistry();
    registry.Register(new RuntimeAutomaticScriptComponentDeserializer());
    registry.Register(new RuntimeCameraComponentDeserializer());
    registry.Register(new RuntimeMeshComponentDeserializer());
    return registry;
}
```

Do not add a replacement deserializer for these motion types. They must come through the automatic script path.

- [ ] **Step 3: Run a core build to verify the engine compiles without those files**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet build 'engine\helengine.core\helengine.core.csproj' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: build passes or surfaces only downstream editor references that still need cleanup.

- [ ] **Step 4: Commit the engine cleanup**

```powershell
git add engine/helengine.core
git commit -m "Remove engine ownership of city motion components"
```

### Task 4: Remove Editor Packaging Rewrite Logic for the Motion Components

**Files:**
- Modify: `engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
- Modify: `engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
- Modify: `engine\helengine.editor.tests\serialization\scene\ComponentPersistenceRegistryTests.cs`
- Modify: `engine\helengine.editor.tests\managers\project\EditorPlatformCodeCookServiceTests.cs`

- [ ] **Step 1: Delete the component-id rewrite branches from the packaging transform service**

Remove the constant ids and rewrite methods dedicated to these types. The service should stop pattern-matching them entirely. The deleted shapes include code like:

```csharp
const string CityDirectionalShadowCameraOrbitComponentTypeId = "city.rendering.DirectionalShadowCameraOrbitComponent, gameplay";

if (string.Equals(record.ComponentTypeId, CityDirectionalShadowCameraOrbitComponentTypeId, StringComparison.OrdinalIgnoreCase)) {
    transformedRecord = RewriteDirectionalShadowCameraOrbitComponentRecord(record);
}
```

After the edit, those records should flow through unchanged.

- [ ] **Step 2: Update tests to assert pass-through behavior instead of rewrites**

Rewrite the packager tests so they expect the packaged records to keep the city type ids:

```csharp
Assert.Collection(
    packagedScene.RootEntities,
    cameraRoot => Assert.Equal("city.rendering.DirectionalShadowCameraOrbitComponent, gameplay", Assert.Single(cameraRoot.Components).ComponentTypeId),
    orbitRoot => Assert.Equal("city.rendering.DirectionalShadowOrbitComponent, gameplay", Assert.Single(orbitRoot.Components).ComponentTypeId),
    sunRoot => Assert.Equal("city.rendering.DirectionalShadowSunSweepComponent, gameplay", Assert.Single(sunRoot.Components).ComponentTypeId),
    towerRoot => Assert.Equal("city.rendering.DirectionalShadowTowerSpinComponent, gameplay", Assert.Single(towerRoot.Components).ComponentTypeId));
```

Remove assertions that reference deleted engine component types or engine serializer payload helpers.

- [ ] **Step 3: Run the focused packager tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~ComponentPersistenceRegistryTests|FullyQualifiedName~EditorPlatformCodeCookServiceTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: pass after the rewrite code is removed and tests are aligned.

- [ ] **Step 4: Commit the editor transform cleanup**

```powershell
git add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/serialization/scene/ComponentPersistenceRegistryTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformCodeCookServiceTests.cs
git commit -m "Remove packaging rewrites for city motion components"
```

### Task 5: Regenerate Scenes and Validate with a Windows Build

**Files:**
- Modify: `C:\dev\helprojs\city\assets\Scenes\DirectionalShadowPlaza.helen`
- Modify: `C:\dev\helprojs\city\assets\Scenes\ScaledCube.helen`
- Modify: `C:\dev\helprojs\city\assets\Scenes\SpotlightStreetSlice.helen`
- Modify: any additional regenerated rendering scene assets touched by the city generators

- [ ] **Step 1: Regenerate the affected authored scenes**

Run the city scene generation command that rebuilds the rendering scenes after the component-id normalization:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet run --project 'helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --editor-command menu.generate-rendering-scenes 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

If the project uses a different rendering-scene generation command, substitute the existing city command used by `RenderingSceneGenerator`.

- [ ] **Step 2: Verify the authored scene output references city-owned types only**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "rg -n 'DirectionalShadow(CameraOrbit|Orbit|SunSweep|TowerSpin)Component|RotateComponent' 'C:\dev\helprojs\city\assets\Scenes' 2>&1 | Select-Object -First 220 | Out-String -Width 260 | Write-Output"
```

Expected: only `city.rendering.*` references remain for the moved component types.

- [ ] **Step 3: Run the fresh Windows export**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-city-demo-disc' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: `Build completed for platform 'windows': C:\dev\helprojs\output\windows-city-demo-disc`

- [ ] **Step 4: Launch the Windows build for a smoke test**

Run:

```powershell
rtk proxy powershell -NoProfile -Command '$process = Get-Process helengine_windows -ErrorAction SilentlyContinue; if ($process) { $process | Stop-Process -Force }; $launched = Start-Process -FilePath "C:\dev\helprojs\output\windows-city-demo-disc\helengine_windows.exe" -PassThru; Start-Sleep -Seconds 2; Get-Process -Id $launched.Id | Select-Object Id, ProcessName, MainWindowTitle | Out-String -Width 260 | Write-Output'
```

Expected: running `helengine_windows` process with a real window title and no immediate crash.

- [ ] **Step 5: Commit the regenerated scenes and final validation changes**

```powershell
git add C:/dev/helprojs/city/assets/Scenes engine/helengine.core engine/helengine.editor engine/helengine.editor.tests C:/dev/helprojs/city/assets/codebase/rendering C:/dev/helprojs/city/assets/codebase/rendering.tools
git commit -m "Make city rendering motion components auto-serialized"
```

## Self-Review

- Spec coverage: the plan covers city-only ownership, deletion of engine classes, removal of runtime deserializers, removal of packaging rewrites, scene regeneration, and Windows validation.
- Placeholder scan: commands, files, assertions, and expected outcomes are concrete; the only conditional note is to use the existing rendering-scene generation command if the exact command name differs in the local city project.
- Type consistency: the plan consistently uses `city.rendering.*` ids for the moved components and treats them as ordinary script components with no engine serialized id constants.

# Axis Rotation Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `cube_test`'s stale directional-shadow spin path with a reusable city-owned `AxisRotationComponent` that rotates in local space using `DeltaTime`.

**Architecture:** Add a new `gameplay.rendering.AxisRotationComponent` in the city gameplay module, switch `CubeTestSceneFactory` to author it, and keep the packaged scene on the gameplay-script runtime path instead of rewriting to the old engine `DirectionalShadowTowerSpinComponent`. Verify the authored scene, cooked scene, and runtime behavior all align, and confirm the Windows player build includes the `gameplay` module.

**Tech Stack:** C# 13 / .NET 9, `helengine.core`, `helengine.editor`, city generated code modules, xUnit

---

## File Structure

- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs`
  - Replace `DirectionalShadowTowerSpinComponent` authoring with `AxisRotationComponent`.
- Create: `C:\dev\helprojs\city\assets\codebase\rendering\AxisRotationComponent.cs`
  - Reusable city gameplay update component for local-axis rotation using `DeltaTime`.
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\code.module.json`
  - Keep `gameplay` as an authored dependency for `rendering.tools`.
- Modify: `C:\dev\helprojs\city\user_settings\generated_code\projects\rendering.tools\rendering.tools.csproj`
  - Keep generated project in sync in the current workspace so verification can run immediately.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
  - Stop `cube_test` from being rewritten into `helengine.DirectionalShadowTowerSpinComponent` and preserve the gameplay script component path.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
  - Assert that regenerated `cube_test` contains `AxisRotationComponent` and no longer contains tower-spin.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
  - Assert that cooked `cube_test` preserves `AxisRotationComponent` instead of the engine tower-spin type.
- Create or Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AxisRotationComponentTests.cs`
  - Verify `DeltaTime`-driven local rotation behavior is frame-rate independent.
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json`
  - Ensure the Windows platform build includes the `gameplay` module for player validation.

## Task 1: Add the City Gameplay Component

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\rendering\AxisRotationComponent.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\AxisRotationComponentTests.cs`

- [ ] **Step 1: Write the failing runtime-behavior test**

```csharp
[Fact]
public void AxisRotationComponent_WithEquivalentElapsedTime_ReachesEquivalentOrientation() {
    using TestClockDrivenCore core = new TestClockDrivenCore();
    Entity entityA = new Entity();
    Entity entityB = new Entity();

    AxisRotationComponent componentA = new AxisRotationComponent {
        Axis = new float3(0f, 1f, 0f),
        AngularSpeedRadiansPerSecond = (float)(Math.PI / 2.0)
    };
    AxisRotationComponent componentB = new AxisRotationComponent {
        Axis = new float3(0f, 1f, 0f),
        AngularSpeedRadiansPerSecond = (float)(Math.PI / 2.0)
    };

    entityA.AddComponent(componentA);
    entityB.AddComponent(componentB);

    core.StepSeconds(0.5);
    componentA.Update();

    for (int index = 0; index < 30; index++) {
        core.StepSeconds(1.0 / 60.0);
        componentB.Update();
    }

    Assert.True(float4.Distance(entityA.LocalOrientation, entityB.LocalOrientation) < 0.001f);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AxisRotationComponent_WithEquivalentElapsedTime_ReachesEquivalentOrientation"
```

Expected: FAIL because `AxisRotationComponent` does not exist yet.

- [ ] **Step 3: Write the minimal component**

```csharp
namespace gameplay.rendering {
    /// <summary>
    /// Rotates the parent entity around one authored local-space axis using frame-rate-independent delta time.
    /// </summary>
    public sealed class AxisRotationComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the local-space axis used for incremental rotation.
        /// </summary>
        public float3 Axis { get; set; }

        /// <summary>
        /// Gets or sets the angular speed in radians per second.
        /// </summary>
        public float AngularSpeedRadiansPerSecond { get; set; }

        /// <summary>
        /// Advances the parent orientation by the authored delta-time rotation step.
        /// </summary>
        public override void Update() {
            base.Update();

            float axisLength = Axis.Length();
            if (axisLength <= 0f) {
                throw new InvalidOperationException("AxisRotationComponent requires a non-zero axis.");
            }

            float3 normalizedAxis = Axis / axisLength;
            float deltaAngle = AngularSpeedRadiansPerSecond * (float)Core.Instance.DeltaTime;
            float4 deltaRotation;
            float4.CreateFromAxisAngle(normalizedAxis, deltaAngle, out deltaRotation);
            float4 orientation = Parent.LocalOrientation * deltaRotation;
            orientation.Normalize();
            Parent.LocalOrientation = orientation;
        }
    }
}
```

- [ ] **Step 4: Run the focused test to verify it passes**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AxisRotationComponent_WithEquivalentElapsedTime_ReachesEquivalentOrientation"
```

Expected: PASS.

- [ ] **Step 5: Add the invalid-axis behavior test**

```csharp
[Fact]
public void AxisRotationComponent_WithZeroAxis_ThrowsInvalidOperationException() {
    using TestClockDrivenCore core = new TestClockDrivenCore();
    Entity entity = new Entity();
    AxisRotationComponent component = new AxisRotationComponent {
        Axis = float3.Zero,
        AngularSpeedRadiansPerSecond = 1f
    };
    entity.AddComponent(component);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => component.Update());
    Assert.Contains("non-zero axis", exception.Message, StringComparison.Ordinal);
}
```

- [ ] **Step 6: Run both runtime-behavior tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AxisRotationComponent_"
```

Expected: PASS with the new `AxisRotationComponent` tests.

## Task 2: Switch Cube Test Scene Authoring to AxisRotationComponent

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Write the failing authored-scene assertions**

```csharp
[Fact]
public void DeserializeCityCubeTestSceneAsset_CubeRootContainsAxisRotationComponent() {
    string sceneContents = File.ReadAllText(CityCubeTestScenePath);
    Assert.Contains("gameplay.rendering.AxisRotationComponent, gameplay", sceneContents, StringComparison.Ordinal);
}

[Fact]
public void DeserializeCityCubeTestSceneAsset_DoesNotContainDirectionalShadowTowerSpinComponent() {
    string sceneContents = File.ReadAllText(CityCubeTestScenePath);
    Assert.DoesNotContain("DirectionalShadowTowerSpinComponent", sceneContents, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the authored-scene tests to verify they fail**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_CubeRootContainsAxisRotationComponent|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_DoesNotContainDirectionalShadowTowerSpinComponent"
```

Expected: FAIL because `cube_test` still serializes the old gameplay tower-spin component.

- [ ] **Step 3: Update the scene factory to author AxisRotationComponent**

```csharp
entity.AddComponent(new AxisRotationComponent {
    Axis = new float3(0f, 1f, 0f),
    AngularSpeedRadiansPerSecond = (float)(Math.PI / 2.0)
});
```

And remove the `GameplayDirectionalShadowTowerSpinComponent` alias and usage from the factory.

- [ ] **Step 4: Regenerate the authored scene through the live-authoring path**

Run:

```powershell
dotnet run --project C:\tmp\cube-test-live-authoring-runner\cube-test-live-authoring-runner.csproj
```

Expected: PASS, rewriting `C:\dev\helprojs\city\assets\scenes\rendering\cube_test.helen`.

- [ ] **Step 5: Re-run the authored-scene tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_CubeRootContainsAxisRotationComponent|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_DoesNotContainDirectionalShadowTowerSpinComponent|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_RemainsReadableAfterLiveAuthoringSavePath"
```

Expected: PASS.

## Task 3: Stop Packaging Cube Test Through the Old Engine Tower-Spin Path

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing cooked-scene packaging test**

```csharp
[Fact]
public void PackageCityCubeTest_PreservesAxisRotationGameplayComponent() {
    string cookedSceneContents = File.ReadAllText(CookedCubeTestScenePath);
    Assert.Contains("gameplay.rendering.AxisRotationComponent, gameplay", cookedSceneContents, StringComparison.Ordinal);
    Assert.DoesNotContain("helengine.DirectionalShadowTowerSpinComponent", cookedSceneContents, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the packaging test to verify it fails**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackageCityCubeTest_PreservesAxisRotationGameplayComponent"
```

Expected: FAIL because the cooked scene still uses `helengine.DirectionalShadowTowerSpinComponent`.

- [ ] **Step 3: Narrow the packaging transform logic**

Update `SceneComponentPackagingTransformService` so:

- old authored directional-shadow component ids keep their existing compatibility rewrite
- the new `gameplay.rendering.AxisRotationComponent, gameplay` type id is treated as a normal automatic script component
- `cube_test` no longer routes through `RewriteDirectionalShadowTowerSpinComponentRecord(...)`

Code target:

```csharp
const string GameplayAxisRotationComponentTypeId = "gameplay.rendering.AxisRotationComponent, gameplay";
```

And ensure the rewrite condition does not include that type id.

- [ ] **Step 4: Rebuild the city gameplay/rendering module if needed**

Run:

```powershell
dotnet build C:\dev\helprojs\city\user_settings\generated_code\projects\rendering.tools\rendering.tools.csproj
```

Expected: PASS.

- [ ] **Step 5: Re-run the packaging test**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackageCityCubeTest_PreservesAxisRotationGameplayComponent"
```

Expected: PASS.

## Task 4: Keep the Module Graph and Windows Build Config Valid

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\code.module.json`
- Modify: `C:\dev\helprojs\city\user_settings\generated_code\projects\rendering.tools\rendering.tools.csproj`
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json`

- [ ] **Step 1: Assert the authored module dependency remains present**

Expected authored manifest snippet:

```json
{
  "moduleId": "rendering.tools",
  "dependencyModuleIds": [
    "gameplay",
    "scene.tools"
  ]
}
```

- [ ] **Step 2: Ensure the generated `rendering.tools` project references `gameplay` for current-workspace verification**

Expected project snippet:

```xml
<ItemGroup>
  <ProjectReference Include="..\gameplay\gameplay.csproj" />
  <ProjectReference Include="..\scene.tools\scene.tools.csproj" />
</ItemGroup>
```

- [ ] **Step 3: Add `gameplay` to the Windows selected code modules**

Expected JSON snippet:

```json
"selectedCodeModuleIds": [
  "gameplay"
]
```

Apply that to both the Windows platform entry and the queued Windows build entry if present.

- [ ] **Step 4: Verify the Windows build config change**

Run:

```powershell
Get-Content C:\dev\helprojs\city\user_settings\build_config.json
```

Expected: the Windows platform includes `"gameplay"` under `selectedCodeModuleIds`.

## Task 5: Final Verification Sweep

**Files:**
- Reference: `C:\dev\helprojs\city\assets\scenes\rendering\cube_test.helen`
- Reference: `C:\dev\helprojs\output\windows\cooked\scenes\rendering\cube_test.hasset`

- [ ] **Step 1: Run the city authored-scene tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_"
```

Expected: PASS.

- [ ] **Step 2: Run the axis-rotation behavior tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AxisRotationComponent_"
```

Expected: PASS.

- [ ] **Step 3: Run the cube-test packaging test**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackageCityCubeTest_PreservesAxisRotationGameplayComponent"
```

Expected: PASS.

- [ ] **Step 4: Regenerate the authored scene one final time**

Run:

```powershell
dotnet run --project C:\tmp\cube-test-live-authoring-runner\cube-test-live-authoring-runner.csproj
```

Expected: PASS.

- [ ] **Step 5: Inspect the authored scene artifact**

Run:

```powershell
Get-Content C:\dev\helprojs\city\assets\scenes\rendering\cube_test.helen
```

Expected: contains `gameplay.rendering.AxisRotationComponent, gameplay` and does not contain `DirectionalShadowTowerSpinComponent`.

- [ ] **Step 6: Build or repackage Windows and inspect the cooked artifact**

Run the existing Windows packaging path used in this workspace, then inspect:

```powershell
Get-Content C:\dev\helprojs\output\windows\cooked\scenes\rendering\cube_test.hasset
```

Expected: contains `gameplay.rendering.AxisRotationComponent, gameplay` and does not contain `helengine.DirectionalShadowTowerSpinComponent`.

- [ ] **Step 7: Record any remaining blocker explicitly**

If player validation still shows a non-rotating cube after the cooked scene contains `AxisRotationComponent` and the Windows build includes `gameplay`, stop and debug the player runtime load path rather than adding a new rotation component.

## Plan Notes

- Do not commit during this execution. The user explicitly approved working in the current `main` workspace without commits.
- Do not add a new engine rotation component.
- Do not preserve `cube_test` on the old `DirectionalShadowTowerSpinComponent` compatibility path.

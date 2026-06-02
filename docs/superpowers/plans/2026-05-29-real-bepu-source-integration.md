# Real BEPU Source Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fake `helengine.bepu` solver with a real adapter over vendored upstream BEPU v2 source for static/dynamic boxes and spheres.

**Architecture:** Vendor upstream `BepuPhysics` and `BepuUtilities` source into the repo at a pinned revision, then make `helengine.bepu` a thin Helengine adapter that creates BEPU bodies/shapes, advances BEPU `Simulation`, and synchronizes transforms back into entities. Remove the custom instability heuristic and validate the cooked `city` stack-box scene through both direct runtime stepping and `Core.Update`.

**Tech Stack:** C#, xUnit, upstream BEPU v2 source, Helengine runtime scene loading, RTK shell workflow.

---

### Task 1: Lock the current regression surface

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuPhysicsWorld3DTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj`

- [ ] **Step 1: Tighten the failing physics expectations so fake instability cannot satisfy them**

Add assertions that require angularly plausible toppling outcomes without helper-specific behavior:

```csharp
Assert.True(
    fourthBoxEntity.LocalPosition.Y < 2.9f || Math.Abs(fourthBoxEntity.LocalPosition.X - 1.5f) > 0.4f,
    $"Expected a real topple outcome, but box04 ended at ({fourthBoxEntity.LocalPosition.X}, {fourthBoxEntity.LocalPosition.Y}, {fourthBoxEntity.LocalPosition.Z}).");
```

- [ ] **Step 2: Run the focused BEPU tests to capture the current failing baseline**

Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Topples`

Expected: FAIL once the heuristic is removed or once stronger expectations reject the fake solver.

- [ ] **Step 3: Commit the regression tightening**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs engine/helengine.bepu.tests/BepuCityDynamicStackBoxesSceneTests.cs
git -C C:\dev\helworks\helengine commit -m "test: lock real topple expectations for BEPU integration"
```

### Task 2: Vendor upstream BEPU source

**Files:**
- Create: `C:\dev\helworks\helengine\engine\vendor\bepuphysics2\...`
- Modify: `C:\dev\helworks\helengine\engine\helengine.ui\helengine.sln`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\helengine.bepu.csproj`
- Create: `C:\dev\helworks\helengine\docs\superpowers\specs\2026-05-29-real-bepu-source-integration-design.md` (already written; update if commit hash changes)

- [ ] **Step 1: Import upstream BEPU source at a pinned revision**

Run one of:

```bash
rtk git clone https://github.com/bepu/bepuphysics2 C:\dev\helworks\helengine\engine\vendor\bepuphysics2
```

or

```bash
rtk git -C C:\dev\helworks\helengine\engine\vendor\bepuphysics2 checkout <pinned-commit>
```

Expected: local vendor tree contains upstream `BepuPhysics` and `BepuUtilities` source.

- [ ] **Step 2: Add only the required upstream projects or source files to the solution and `helengine.bepu` dependency graph**

Wire the vendor projects through project references, for example:

```xml
<ProjectReference Include="..\vendor\bepuphysics2\BepuPhysics\BepuPhysics.csproj" />
<ProjectReference Include="..\vendor\bepuphysics2\BepuUtilities\BepuUtilities.csproj" />
```

- [ ] **Step 3: Build `helengine.bepu` with the vendored source**

Run: `rtk dotnet build C:\dev\helworks\helengine\engine\helengine.bepu\helengine.bepu.csproj -c Debug`

Expected: PASS.

- [ ] **Step 4: Commit the vendor wiring**

```bash
git -C C:\dev\helworks\helengine add engine/vendor/bepuphysics2 engine/helengine.bepu/helengine.bepu.csproj helengine.ui/helengine.sln docs/superpowers/specs/2026-05-29-real-bepu-source-integration-design.md
git -C C:\dev\helworks\helengine commit -m "build: vendor upstream BEPU source"
```

### Task 3: Remove the fake instability path

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3D.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj`

- [ ] **Step 1: Delete the heuristic-only path from `BepuPhysicsWorld3D`**

Remove:

```csharp
const float SupportContactTolerance = 0.08f;
const float SupportEdgeInstabilityThreshold = 0.9f;
const float SupportSlipAcceleration = 6f;
void ApplySupportInstability(float stepSeconds) { ... }
```

and the call site:

```csharp
ApplySupportInstability(stepSecondsFloat);
```

- [ ] **Step 2: Run the topple-focused tests to prove the current fake runtime is no longer acceptable**

Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Topples`

Expected: FAIL.

- [ ] **Step 3: Commit the heuristic removal baseline**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.bepu/BepuPhysicsWorld3D.cs
git -C C:\dev\helworks\helengine commit -m "refactor: remove fake topple heuristic from helengine.bepu"
```

### Task 4: Build the real BEPU adapter

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuPhysicsWorld3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuBodyHandle3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuBodyRegistry3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuShapeFactory3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuEntitySynchronization3D.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu\BepuRuntimeBodyState3D.cs`

- [ ] **Step 1: Replace the custom overlap solver with BEPU `Simulation` ownership**

Introduce fields shaped like:

```csharp
Simulation SimulationValue;
BufferPool BufferPoolValue;
ThreadDispatcher ThreadDispatcherValue;
```

and initialize them using BEPU callbacks and narrow-scope pose integrator settings.

- [ ] **Step 2: Map supported Helengine rigid bodies into BEPU bodies/statics**

Create helper code that:

```csharp
var shape = new Box(width, height, depth);
var inertia = shape.ComputeInertia((float)rigidBody.Mass);
var bodyDescription = BodyDescription.CreateDynamic(position, inertia, collidable, activity);
```

and equivalent sphere/static variants.

- [ ] **Step 3: Store BEPU handles inside the Helengine runtime registry**

`BepuBodyHandle3D` should track:

```csharp
public BodyHandle? DynamicHandle { get; }
public StaticHandle? StaticHandle { get; }
```

or an equivalent non-nullable layout compatible with repo conventions.

- [ ] **Step 4: Advance BEPU simulation and synchronize results back to entities**

Replace manual integration/contact code with:

```csharp
SimulationValue.Timestep((float)stepSeconds);
```

then read solved poses/velocities back into entities and rigid-body components.

- [ ] **Step 5: Run focused tests for ground contact, box stack, sphere stack, and topple behavior**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Step_WithDynamicBoxAboveStaticGround_FallsAndResolvesGroundContact
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Step_WithTwoDynamicBoxesAboveStaticGround_ResolvesSimpleStack
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Step_WithDynamicSphereAboveStaticGround_FallsAndResolvesGroundContact
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Topples
```

Expected: PASS.

- [ ] **Step 6: Commit the real BEPU adapter**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.bepu
git -C C:\dev\helworks\helengine commit -m "feat: back helengine.bepu with real BEPU simulation"
```

### Task 5: Validate runtime registration and cooked city scene behavior

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuRuntimeComponentRegistrationTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\LegacyPhysics3DRuntimeComponentRegistrationTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.bepu.tests\BepuCityDynamicStackBoxesSceneTests.cs`

- [ ] **Step 1: Keep runtime registration tests green**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~Register_WhenCalled
```

Expected: PASS.

- [ ] **Step 2: Run cooked city scene tests through direct `world.Step` and `Core.Update`**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj -c Debug --filter FullyQualifiedName~LoadCityStackBoxesScene
```

Expected: PASS.

- [ ] **Step 3: Commit runtime-scene validation coverage**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.bepu.tests
git -C C:\dev\helworks\helengine commit -m "test: validate cooked city stack boxes with real BEPU runtime"
```

### Task 6: Rebuild and verify the Windows player

**Files:**
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json` temporarily if a direct-start build is needed
- Output: `C:\dev\helprojs\city\windows-build-20260529-stack-box-direct`

- [ ] **Step 1: Produce a direct-start Windows build for `test_scene_dynamic_stack_boxes`**

Run:

```bash
rtk proxy powershell -NoProfile -Command "& { dotnet 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll' --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\city\windows-build-20260529-stack-box-direct' }"
```

Expected: `Build completed for platform 'windows'`.

- [ ] **Step 2: Launch the direct-start build and verify the startup scene**

Run:

```bash
rtk proxy powershell -NoProfile -Command "& { Start-Process -FilePath 'C:\dev\helprojs\city\windows-build-20260529-stack-box-direct\helengine_windows.exe'; Start-Sleep -Seconds 5; Get-Content -LiteralPath 'C:\dev\helprojs\city\windows-build-20260529-stack-box-direct\helengine_windows.startup.log' -Tail 20 }"
```

Expected log line: `Loading startup scene from runtime scene catalog entry 'test_scene_dynamic_stack_boxes'.`

- [ ] **Step 3: Commit any required build-setting changes only if they are intended to persist**

```bash
git -C C:\dev\helworks\helengine add .
git -C C:\dev\helworks\helengine commit -m "chore: verify real BEPU city stack box runtime build"
```

If the build-setting edits were temporary only, do not commit them; restore them before completion.

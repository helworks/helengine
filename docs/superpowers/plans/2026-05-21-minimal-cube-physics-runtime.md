# Minimal Cube Physics Runtime Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the active 3D physics runtime with a cube-only solver that is stable enough for box stacks and simple enough for dynamic C++ conversion.

**Architecture:** `PhysicsWorld3D` becomes the compatibility wrapper for a compact box-only runtime. The runtime keeps fixed reusable arrays for body state, pair candidates, contact manifolds, and solver contacts, and rejects unsupported collider types clearly. BEPU remains only a reference for timestep order and sequential impulse contact solving.

**Tech Stack:** C#/.NET 9, xUnit, `helengine.physics3d`, `rtk proxy powershell` commands.

---

## File Structure

- `engine/helengine.physics3d/PhysicsWorld3D.cs`: Replace active runtime flow with cube-only binding, stepping, contact generation, and solver orchestration.
- `engine/helengine.physics3d/runtime/CubeBodyState3D.cs`: Create compact body state for box-only runtime.
- `engine/helengine.physics3d/runtime/CubeBodyPair3D.cs`: Create fixed candidate-pair record.
- `engine/helengine.physics3d/collision/CubeContactPoint3D.cs`: Create compact contact point.
- `engine/helengine.physics3d/collision/CubeContactManifold3D.cs`: Create up-to-four point box contact manifold.
- `engine/helengine.physics3d/collision/CubeBoxContactResolver3D.cs`: Create box-box SAT and manifold generation.
- `engine/helengine.physics3d/collision/CubeSequentialImpulseSolver3D.cs`: Create normal/friction impulse solver and residual positional correction.
- `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`: Replace broad advanced physics expectations with cube-only regressions.
- `engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs`: Keep box-box resolver tests only.
- Delete old runtime-only classes after compile proves replacement path is complete: sphere/capsule/static-mesh/character resolver files, non-box runtime deserializers, old broadphase and trigger runtime files.

## Task 1: Cube Runtime State Types

**Files:**
- Create: `engine/helengine.physics3d/runtime/CubeBodyState3D.cs`
- Create: `engine/helengine.physics3d/runtime/CubeBodyPair3D.cs`
- Create: `engine/helengine.physics3d/collision/CubeContactPoint3D.cs`
- Create: `engine/helengine.physics3d/collision/CubeContactManifold3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Add failing unsupported-collider and cube-stack tests**

Add or keep tests named:

```csharp
[Fact]
public void BindScene_WithUnsupportedSphereCollider_ThrowsNotSupportedException()

[Fact]
public void Step_WithOffsetUpperBoxLoadingGroundedBox_DoesNotLaunchLowerBoxUpward()
```

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~BindScene_WithUnsupportedSphereCollider_ThrowsNotSupportedException|FullyQualifiedName~Step_WithOffsetUpperBoxLoadingGroundedBox_DoesNotLaunchLowerBoxUpward' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: unsupported-collider test fails until the new cube-only bind policy exists.

- [ ] **Step 2: Implement state records**

Create `CubeBodyState3D`, `CubeBodyPair3D`, `CubeContactPoint3D`, and `CubeContactManifold3D` with XML comments on every type and member. Keep fields/properties explicit, PascalCase, and avoid tuples.

- [ ] **Step 3: Verify compile**

Run the same filtered test command.

Expected: compile succeeds; behavior may still fail before `PhysicsWorld3D` uses the new state.

## Task 2: Box-Only Contact Generation

**Files:**
- Create: `engine/helengine.physics3d/collision/CubeBoxContactResolver3D.cs`
- Test: `engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs`

- [ ] **Step 1: Keep only box-box primitive tests**

Primitive tests should cover:

```csharp
TryResolveManifold_WithFlatFaceOverlap_ReportsFourPointPatch
TryResolveManifold_WithSeparatedRotatedBoxesInsideAabb_ReturnsFalse
```

- [ ] **Step 2: Implement SAT and four-point face patch**

`CubeBoxContactResolver3D.TryResolveManifold` must return false for separated OBBs and up to four points for face contact.

- [ ] **Step 3: Run primitive tests**

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PrimitiveContactResolver3DTests' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: primitive box tests pass.

## Task 3: Cube Sequential Impulse Solver

**Files:**
- Create: `engine/helengine.physics3d/collision/CubeSequentialImpulseSolver3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Route `PhysicsWorld3D` through cube-only arrays**

`BindScene` should collect only entities with `RigidBody3DComponent` and `BoxCollider3DComponent`. Entities with sphere, capsule, static mesh, or character controller physics components should throw `NotSupportedException`.

- [ ] **Step 2: Implement step order**

Use:

```text
sync from entities
clear contacts
for each substep:
  integrate dynamic velocities with gravity
  predict positions/orientations
  collect all box-box candidate pairs
  generate manifolds
  solve velocity constraints for Settings.SolverIterations
  apply small residual positional correction
  update activity sleep state
sync to entities
```

- [ ] **Step 3: Run dynamics tests**

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PhysicsWorld3DDynamicsTests|FullyQualifiedName~PhysicsWorld3DStabilityScenarioTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: cube-only dynamics tests pass.

## Task 4: Remove Old Runtime Physics Paths

**Files:**
- Delete unsupported runtime resolver files under `engine/helengine.physics3d/collision`
- Delete unsupported runtime deserializer files under `engine/helengine.physics3d/runtime`
- Delete unsupported tests under `engine/helengine.physics3d.tests` or rewrite them to cube-only expectations

- [ ] **Step 1: Delete unsupported contact resolvers**

Delete capsule, sphere, character, static mesh, and trigger resolver files from the active project.

- [ ] **Step 2: Keep public authoring components only if required for serialization/editor compile**

Keep `SphereCollider3DComponent`, `CapsuleCollider3DComponent`, `StaticMeshCollider3DComponent`, and `CharacterController3DComponent` only if other projects still reference them. Runtime binding must reject them.

- [ ] **Step 3: Run filtered physics suite**

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName!~Register_AttachesDefaultWorld' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: cube-only suite passes.

## Task 5: One Physics Scene For Now

**Files:**
- Modify: `engine/helengine.editor/managers/physics/PhysicsValidationSceneCatalog.cs`
- Modify: `engine/helengine.editor/managers/physics/PhysicsValidationSceneFactory.cs`
- Do not edit `C:\dev\helprojs\city` unless the user explicitly permits writing outside the worktree.

- [ ] **Step 1: Narrow worktree physics validation catalog**

`PhysicsValidationSceneCatalog.GetSceneIds()` should return only `DynamicStackBoxesSceneId`.

- [ ] **Step 2: Guard factory dispatch**

`PhysicsValidationSceneFactory.CreateSceneAsset` should support only `DynamicStackBoxesSceneId` and throw `NotSupportedException` for all other physics validation scene ids.

- [ ] **Step 3: Run generator-related tests**

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~PhysicsValidationSceneFactoryTests|FullyQualifiedName~CityRenderingSceneAuthoringTests' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: tests updated to cube-only scene expectations pass.

## Task 6: Final Validation

**Files:**
- No new files.

- [ ] **Step 1: Run physics test suite**

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName!~Register_AttachesDefaultWorld' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: all cube-only physics tests pass.

- [ ] **Step 2: Build Windows city output**

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\.worktrees\bepu-style-physics-stability\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-physics' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: build completes. If city still generates unsupported physics scenes, stop and ask for permission to edit the city project generator outside the worktree.

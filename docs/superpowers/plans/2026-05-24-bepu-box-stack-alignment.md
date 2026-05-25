# BEPU Box Stack Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Helengine dynamic box-box-box tower behavior follow the BEPU reference collapse instead of preserving unstable vertical stacks.

**Architecture:** Keep the existing lightweight solver, but remove dynamic-dynamic stack-preservation rules that conflict with BEPU. Dynamic-dynamic box contacts should use contact impulses and bounded correction, not vertical-axis favoritism or rest stabilization that freezes falling boxes.

**Tech Stack:** C#/.NET 9, `helengine.physics3d`, `helengine.physics3d.tests`, local BEPU comparison harness under `artifacts/physics-comparison`.

---

### Task 1: Add BEPU-Topology Regression

**Files:**
- Modify: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Add a failing test for unstable tower collapse**

Add a test near the existing eight-box tower tests. It should simulate the city tower for 1200 fixed steps and assert the final stack does not remain tall: at most four boxes may remain above `Y=2.25`, and the top box should fall below `Y=3.25`.

- [ ] **Step 2: Run targeted test**

Run: `dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj -c Debug --no-restore --filter FullyQualifiedName~Step_WithCityEightBoxTower_CollapsesUnstableStackNearBepuTopology`

Expected before implementation: FAIL because the current solver leaves several boxes stacked high.

### Task 2: Remove Dynamic-Dynamic Vertical Stack Preservation

**Files:**
- Modify: `engine/helengine.physics3d/collision/BoxBoxContactResolver3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`

- [ ] **Step 1: Stop preferring vertical support for dynamic-dynamic box pairs**

Change `BoxBoxContactResolver3D.TryResolveContact` to use the minimum-penetration axis unless the pair is not dynamic-dynamic. This keeps support behavior for ground/static cases but avoids freezing dynamic towers.

- [ ] **Step 2: Stop rest stabilization for unstable dynamic-dynamic support contacts**

Do not mark dynamic-dynamic vertical box support contacts as stable support for rest damping/sleep. Static/kinematic support can still use rest stabilization.

- [ ] **Step 3: Keep the existing fallback angular fix**

Leave `ContactMaterialResponse3D.ApplyAxisPairResponse` linear-only for dynamic-dynamic fallback contacts so guessed contact points do not inject fake spin.

### Task 3: Verify Against Tests and BEPU Trace

**Files:**
- Test: `engine/helengine.physics3d.tests/helengine.physics3d.tests.csproj`
- Test: `artifacts/physics-comparison/PhysicsComparisonHarness.csproj`

- [ ] **Step 1: Run targeted physics tests**

Run the tower/topology tests and the runaway angular velocity test. Expected: all pass.

- [ ] **Step 2: Run full physics test suite**

Run: `dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj -c Debug --no-restore`

Expected: all tests pass.

- [ ] **Step 3: Run BEPU comparison harness**

Run: `dotnet run --project artifacts\physics-comparison\PhysicsComparisonHarness.csproj --no-restore`

Expected: final Helengine tower topology is closer to BEPU, with upper boxes no longer frozen high.

### Task 4: Rebuild City Windows Output

**Files:**
- Output: `C:\dev\helprojs\city\windows-build`

- [ ] **Step 1: Run city Windows build harness**

Run: `dotnet run --project artifacts\city-windows-build-harness\CityWindowsBuildHarness.csproj --no-restore`

Expected: build completes for platform `windows`.

- [ ] **Step 2: Smoke launch packaged build**

Launch `C:\dev\helprojs\city\windows-build\helengine_windows.exe` for 12 seconds.

Expected: process remains running without startup crash.

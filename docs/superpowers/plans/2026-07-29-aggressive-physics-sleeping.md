# Aggressive Physics Sleeping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make low-performance-console-oriented dynamic rigid bodies enter BEPU island sleep quickly after settling, while preserving BEPU's standard wake behavior.

**Architecture:** Author sleep activity on `RigidBody3DComponent` so it serializes through the existing reflected component path. `BepuPhysicsWorld3D` creates a BEPU `BodyActivityDescription` from those settings for dynamic bodies only; it does not force sleeping or manually wake bodies during normal stepping. The profiler exposes awake, sleep-candidate, and sleeping dynamic-body counts after each fixed update.

**Tech Stack:** C#/.NET, Helengine physics components, BEPU v2, existing Windows Tracy profiler bridge, xUnit.

---

## File structure

- `engine/helengine.physics/RigidBody3DComponent.cs` — owns authored and serialized sleep settings plus validation.
- `engine/helengine.bepu/BepuPhysicsWorld3D.cs` — maps authored dynamic-body sleep settings to BEPU and counts sleep states after a step.
- `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs` — verifies BEPU body registration, sleep, and wake behavior.
- `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs` — verifies profiler-side sleep counters from a runtime-owned world.
- `engine/helengine-windows/src/platform/windows/win32/win32_application.cpp` — emits the authoritative BEPU sleep counters as Tracy plots.

### Task 1: Add authored aggressive-sleep settings

**Files:**
- Modify: `engine/helengine.physics/RigidBody3DComponent.cs`
- Test: `engine/helengine.physics.tests/RigidBody3DComponentTests.cs`

- [ ] **Step 1: Write failing component-default and validation tests**

```csharp
[Fact]
public void Constructor_WhenCreated_UsesAggressiveSleepDefaults() {
    RigidBody3DComponent rigidBody = new RigidBody3DComponent();

    Assert.Equal(0.5d, rigidBody.SleepThreshold);
    Assert.Equal(10, rigidBody.SleepTicks);
}

[Theory]
[InlineData(0d)]
[InlineData(-0.1d)]
public void SleepThreshold_WhenNotFiniteOrPositive_Throws(double value) {
    RigidBody3DComponent rigidBody = new RigidBody3DComponent();

    Assert.Throws<ArgumentOutOfRangeException>(() => rigidBody.SleepThreshold = value);
}

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void SleepTicks_WhenNotPositive_Throws(int value) {
    RigidBody3DComponent rigidBody = new RigidBody3DComponent();

    Assert.Throws<ArgumentOutOfRangeException>(() => rigidBody.SleepTicks = value);
}
```

- [ ] **Step 2: Run the new test class and confirm failure**

Run:

```powershell
dotnet test engine\helengine.physics.tests\helengine.physics.tests.csproj --no-restore --filter FullyQualifiedName~RigidBody3DComponentTests
```

Expected: failure because `SleepThreshold` and `SleepTicks` do not exist.

- [ ] **Step 3: Add the two validated component properties**

Add PascalCase backing fields and XML comments. Initialize the constructor with `SleepThresholdValue = 0.5d` and `SleepTicksValue = 10`. Implement the public properties as follows:

```csharp
public double SleepThreshold {
    get { return SleepThresholdValue; }
    set {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d) {
            throw new ArgumentOutOfRangeException(nameof(value), "Sleep threshold must be a finite value greater than zero.");
        }

        SleepThresholdValue = value;
    }
}

public int SleepTicks {
    get { return SleepTicksValue; }
    set {
        if (value <= 0) {
            throw new ArgumentOutOfRangeException(nameof(value), "Sleep tick count must be greater than zero.");
        }

        SleepTicksValue = value;
    }
}
```

- [ ] **Step 4: Run the focused component tests**

Run the Task 1 command again.

Expected: PASS.

### Task 2: Register authored dynamic-body activity with BEPU

**Files:**
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Test: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Write the failing BEPU registration test**

Create a dynamic unit-box entity with `SleepThreshold = 0.5d` and `SleepTicks = 10`, bind it, and assert the BEPU body description reports matching activity values:

```csharp
Assert.Equal(0.5f, bodyDescription.Activity.SleepThreshold);
Assert.Equal((byte)10, bodyDescription.Activity.MinimumTimestepCountUnderThreshold);
```

Create a companion test with `SleepThreshold = 0.8d` and `SleepTicks = 7` to verify explicit authored values survive registration.

- [ ] **Step 2: Run the two registration tests and confirm failure**

Run:

```powershell
dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter "FullyQualifiedName~BepuPhysicsWorld3DTests"
```

Expected: failure because `BepuPhysicsWorld3D` currently calls `BodyDescription.GetDefaultActivity(shape)`.

- [ ] **Step 3: Add one BEPU activity factory in `BepuPhysicsWorld3D`**

Add a private `CreateDynamicActivityDescription(RigidBody3DComponent rigidBody)` method that validates a non-null dynamic body, rejects a threshold too large for a finite `float`, rejects `SleepTicks > byte.MaxValue`, then returns:

```csharp
return new BodyActivityDescription((float)rigidBody.SleepThreshold, (byte)rigidBody.SleepTicks);
```

Replace only the dynamic `BodyDescription.CreateDynamic` activity arguments for boxes and spheres with that factory. Keep kinematic activity construction unchanged because kinematics do not sleep through this policy.

- [ ] **Step 4: Run the focused BEPU world tests**

Run the Task 2 command again.

Expected: PASS.

### Task 3: Prove aggressive island sleeping and normal wake-up

**Files:**
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`

- [ ] **Step 1: Write a failing quiet-island sleep test**

Build a static ground and one dynamic unit box at rest, with `UseGravity = false`, `SleepThreshold = 0.5d`, and `SleepTicks = 2`. Advance `world.Step(1d / 20d)` until the configured candidate window has elapsed. Assert the dynamic body is not awake through its `BodyReference`.

- [ ] **Step 2: Write a failing wake-after-sleep test**

Start from the sleeping body created in Step 1. Set `rigidBody.LinearVelocity = new float3(1f, 0f, 0f)`, call the existing `SynchronizeDynamicBodyVelocity(entity)`, then assert `BodyReference.Awake` is true before the next simulation step.

- [ ] **Step 3: Run the sleep and wake tests and confirm the sleep test fails before the registration change**

Run:

```powershell
dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter "FullyQualifiedName~Sleeps|FullyQualifiedName~Wakes"
```

Expected before Task 2 implementation: the sleep test fails under the old shape-derived threshold. Expected after Task 2: both tests PASS.

- [ ] **Step 4: Run the full BEPU test project**

Run:

```powershell
dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore
```

Expected: PASS.

### Task 4: Expose authoritative BEPU sleep counters

**Files:**
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Modify: `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`

- [ ] **Step 1: Write the failing profiler-counter test**

After stepping a world containing a dynamic body configured with two sleep ticks, assert that the world exposes non-negative `SleepCandidateDynamicBodyCount` and `SleepingDynamicBodyCount`. After it sleeps, assert `AwakeDynamicBodyCount == 0` and `SleepingDynamicBodyCount == 1`.

- [ ] **Step 2: Add post-step state counting**

Extend the current `CountAwakeDynamicBodies` pass into a single private method that inspects registered dynamic handles after `CollectTriggerEvents`. Count:

```csharp
if (!bodyReference.Awake) {
    sleepingDynamicBodyCount++;
} else if (activity.SleepCandidate) {
    sleepCandidateDynamicBodyCount++;
} else {
    awakeDynamicBodyCount++;
}
```

Read `activity` from the active set only when the body is awake. Store the three results in separate fields and expose read-only documented properties. A sleeping body is not in the active set, so it must be counted only through `!bodyReference.Awake`.

- [ ] **Step 3: Run the profiler-counter test**

Run:

```powershell
dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter FullyQualifiedName~BepuRuntimeComponentRegistrationTests
```

Expected: PASS.

### Task 5: Emit the new Tracy plots

**Files:**
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp`
- Test: `C:\dev\helworks\helengine-windows\builder.tests\WindowsNativeBuildExecutorTests.cs`

- [ ] **Step 1: Add a source-level regression test for plot identifiers**

Follow the existing profiler-source test style to require these exact plot names:

```text
Physics.Bepu.SleepCandidates
Physics.Bepu.SleepingDynamicBodies
```

- [ ] **Step 2: Emit the plot values beside `Physics.Bepu.AwakeDynamicBodies`**

Inside the existing `BepuPhysicsWorld3D` profiler branch, emit:

```cpp
EmitWindowsTracyProfilerPlot("Physics.Bepu.SleepCandidates", physicsWorld->get_SleepCandidateDynamicBodyCount());
EmitWindowsTracyProfilerPlot("Physics.Bepu.SleepingDynamicBodies", physicsWorld->get_SleepingDynamicBodyCount());
```

Do not emit synthetic data when no fixed update occurred.

- [ ] **Step 3: Run the focused Windows builder tests**

Run:

```powershell
dotnet test builder.tests\builder.tests.csproj --no-restore --filter FullyQualifiedName~WindowsNativeBuildExecutorTests
```

Expected: PASS.

### Task 6: Validate the console-like stack scenario

**Files:**
- Modify: `engine/helengine.bepu.tests/BepuCityDynamicStackBoxesSceneTests.cs`

- [ ] **Step 1: Add a quiet supported-box sleep test at 20 Hz / 1 iteration**

Use `BepuPhysicsWorld3D.CreateWithSolveSchedule(1, 1)`, a static ground, and a non-overhung dynamic unit box with aggressive defaults. Advance 40 steps at `1d / 20d`. Assert `AwakeDynamicBodyCount == 0` and `SleepingDynamicBodyCount == 1`.

- [ ] **Step 2: Run the focused scenario test**

Run:

```powershell
dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter FullyQualifiedName~BepuCityDynamicStackBoxesSceneTests
```

Expected: PASS.

- [ ] **Step 3: Build the existing Windows profiler stacked-cubes package**

Run the existing `C:\dev\helworks\helengine-windows\build-demodisc-windows.ps1` profile invocation with the already-established `20 Hz`, `1` velocity iteration, and `1` substep overrides.

Expected: build succeeds and the package contains `helengine_windows.exe`, its PDB, and `runtime\generated_profiler_manifest.json`.

- [ ] **Step 4: Capture and inspect the result**

Launch the packaged player, allow the supported test body to settle for at least two seconds, save a Tracy capture, and verify `Physics.Bepu.AwakeDynamicBodies` reaches zero while `Physics.Bepu.SleepingDynamicBodies` reaches the dynamic-body count.

### Task 7: Review and commit

**Files:**
- Modify: all files from Tasks 1–6

- [ ] **Step 1: Review the targeted diff**

Run:

```powershell
git -C C:\dev\helworks\helengine diff -- engine/helengine.physics/RigidBody3DComponent.cs engine/helengine.bepu/BepuPhysicsWorld3D.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs engine/helengine.bepu.tests/BepuCityDynamicStackBoxesSceneTests.cs
git -C C:\dev\helworks\helengine-windows diff -- src/platform/windows/win32/win32_application.cpp builder.tests/WindowsNativeBuildExecutorTests.cs
```

Expected: only the approved sleeping policy and profiler-counter changes appear; unrelated existing edits remain untouched.

- [ ] **Step 2: Commit only after explicit user authorization**

Stage exact modified files in their owning repositories and create a focused commit. Do not include existing unrelated edits.

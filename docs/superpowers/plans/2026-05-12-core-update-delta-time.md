# Core Update Delta Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Core.Instance.DeltaTime` and `Core.Instance.UnscaledDeltaTime` as shared per-update timing values backed by real elapsed wall-clock time on the parameterless `Update()` path.

**Architecture:** Keep timing state on `Core` itself. The parameterless `Core.Update()` path measures elapsed time from a monotonic timestamp source, the explicit `Core.Update(double)` path continues to accept host-supplied elapsed time, and both paths write the same cached delta properties before components update. Add one deterministic test-only `Core` subclass so timing tests do not rely on `Thread.Sleep`.

**Tech Stack:** C#, xUnit, `System.Diagnostics.Stopwatch`, existing core/editor runtime update loop.

---

### Task 1: Add Deterministic Timing Tests And Probe Helpers

**Files:**
- Create: `engine/helengine.editor.tests/testing/TestClockDrivenCore.cs`
- Create: `engine/helengine.editor.tests/testing/TestDeltaTimeProbeComponent.cs`
- Modify: `engine/helengine.editor.tests/CoreTimingTests.cs`

- [ ] **Step 1: Add failing timing API coverage in `CoreTimingTests.cs`**

Replace the old parameterless-default-delta test with these four tests in `engine/helengine.editor.tests/CoreTimingTests.cs`:

```csharp
[Fact]
public void Update_WhenCalledFirstTimeWithoutExplicitElapsedSeconds_SetsDeltaPropertiesToZero() {
    TestClockDrivenCore core = CreateClockDrivenCore(1000L);

    core.Update();

    Assert.Equal(0f, core.DeltaTime);
    Assert.Equal(0f, core.UnscaledDeltaTime);
    Assert.Equal(0d, core.FrameDeltaSeconds, 10);
    Assert.Equal(0d, core.TotalElapsedSeconds, 10);
}

[Fact]
public void Update_WhenCalledAgainWithoutExplicitElapsedSeconds_UsesMeasuredElapsedSeconds() {
    TestClockDrivenCore core = CreateClockDrivenCore(1000L, 1000L + Stopwatch.Frequency / 20L);

    core.Update();
    core.Update();

    Assert.Equal(0.05f, core.DeltaTime, 3);
    Assert.Equal(core.DeltaTime, core.UnscaledDeltaTime, 6);
    Assert.Equal(0.05d, core.FrameDeltaSeconds, 3);
    Assert.Equal(0.05d, core.TotalElapsedSeconds, 3);
}

[Fact]
public void Update_WhenCalledWithExplicitElapsedSeconds_UpdatesDeltaPropertiesAndAccumulatedTime() {
    Core core = CreateCore();

    core.Update(0.25d);
    core.Update(0.5d);

    Assert.Equal(0.5f, core.DeltaTime, 6);
    Assert.Equal(0.5f, core.UnscaledDeltaTime, 6);
    Assert.Equal(0.5d, core.FrameDeltaSeconds, 10);
    Assert.Equal(0.75d, core.TotalElapsedSeconds, 10);
}

[Fact]
public void UpdateComponent_WhenRunningInsideCoreUpdate_CanReadCurrentDeltaTime() {
    TestClockDrivenCore core = CreateClockDrivenCore(1000L, 1000L + Stopwatch.Frequency / 10L);
    Entity entity = new Entity();
    entity.InitComponents();
    TestDeltaTimeProbeComponent component = new TestDeltaTimeProbeComponent();
    entity.AddComponent(component);

    core.Update();
    core.Update();

    Assert.Equal(0.1f, component.LastObservedDeltaTime, 3);
    Assert.Equal(0.1f, component.LastObservedUnscaledDeltaTime, 3);
    Assert.Equal(1, component.ObservedUpdateCount);
}
```

- [ ] **Step 2: Add a deterministic clock-driven core helper**

Create `engine/helengine.editor.tests/testing/TestClockDrivenCore.cs`:

```csharp
using System.Diagnostics;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a deterministic core clock for timing tests by returning queued update timestamps.
    /// </summary>
    internal class TestClockDrivenCore : Core {
        /// <summary>
        /// Stores the queued timestamps that should be returned to the parameterless update path.
        /// </summary>
        readonly Queue<long> UpdateTimestamps;

        /// <summary>
        /// Initializes the core with one deterministic sequence of stopwatch timestamps.
        /// </summary>
        /// <param name="timestamps">Queued stopwatch timestamps returned by subsequent update calls.</param>
        public TestClockDrivenCore(IEnumerable<long> timestamps)
            : base(new CoreInitializationOptions()) {
            if (timestamps == null) {
                throw new ArgumentNullException(nameof(timestamps));
            }

            UpdateTimestamps = new Queue<long>(timestamps);
        }

        /// <summary>
        /// Returns the next queued timestamp for the measured update path.
        /// </summary>
        /// <returns>One deterministic stopwatch timestamp.</returns>
        protected override long GetCurrentUpdateTimestamp() {
            if (UpdateTimestamps.Count == 0) {
                throw new InvalidOperationException("No queued update timestamps remain for the timing test.");
            }

            return UpdateTimestamps.Dequeue();
        }
    }
}
```

- [ ] **Step 3: Add a probe component that records the delta visible during update**

Create `engine/helengine.editor.tests/testing/TestDeltaTimeProbeComponent.cs`:

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records the delta-time values that were visible from the current core during update execution.
    /// </summary>
    internal class TestDeltaTimeProbeComponent : UpdateComponent {
        /// <summary>
        /// Gets the most recent scaled delta time observed by the component.
        /// </summary>
        public float LastObservedDeltaTime { get; private set; }

        /// <summary>
        /// Gets the most recent unscaled delta time observed by the component.
        /// </summary>
        public float LastObservedUnscaledDeltaTime { get; private set; }

        /// <summary>
        /// Gets the number of update callbacks that observed a non-zero delta.
        /// </summary>
        public int ObservedUpdateCount { get; private set; }

        /// <summary>
        /// Records the current core delta values during the update callback.
        /// </summary>
        public override void Update() {
            base.Update();
            LastObservedDeltaTime = Core.Instance.DeltaTime;
            LastObservedUnscaledDeltaTime = Core.Instance.UnscaledDeltaTime;
            if (LastObservedDeltaTime > 0f) {
                ObservedUpdateCount++;
            }
        }
    }
}
```

- [ ] **Step 4: Run the focused timing tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CoreTimingTests
```

Expected: FAIL because `Core` does not yet expose `DeltaTime` / `UnscaledDeltaTime`, `GetCurrentUpdateTimestamp()` does not exist, and the parameterless update path still uses the fixed default delta.

- [ ] **Step 5: Commit the failing-test harness**

```powershell
rtk git add engine/helengine.editor.tests/testing/TestClockDrivenCore.cs engine/helengine.editor.tests/testing/TestDeltaTimeProbeComponent.cs engine/helengine.editor.tests/CoreTimingTests.cs
rtk git commit -m "Add core delta time timing tests"
```

### Task 2: Implement Core-Owned Delta Time And Parameterless Measured Updates

**Files:**
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.core/CoreInitializationOptions.cs`
- Modify: `engine/helengine.editor/EditorCore.cs`

- [ ] **Step 1: Add cached delta properties and timestamp state to `Core.cs`**

Add these members near the existing timing state in `engine/helengine.core/Core.cs`:

```csharp
/// <summary>
/// Stores whether one previous measured update timestamp has been captured yet.
/// </summary>
bool HasPreviousUpdateTimestamp;

/// <summary>
/// Stores the previous measured update timestamp returned by the host clock.
/// </summary>
long PreviousUpdateTimestamp;

/// <summary>
/// Gets the elapsed scaled update time, in seconds, that components can read during the current update.
/// </summary>
public float DeltaTime { get; private set; }

/// <summary>
/// Gets the elapsed unscaled update time, in seconds, that components can read during the current update.
/// </summary>
public float UnscaledDeltaTime { get; private set; }
```

- [ ] **Step 2: Change the parameterless update path to measure real elapsed time**

Replace the current `Update()` implementation in `engine/helengine.core/Core.cs` with:

```csharp
/// <summary>
/// Advances the engine update loop using real elapsed time measured between parameterless update calls.
/// </summary>
public virtual void Update() {
    long currentTimestamp = GetCurrentUpdateTimestamp();
    double elapsedSeconds = ResolveMeasuredElapsedSeconds(currentTimestamp);
    AdvanceUpdate(elapsedSeconds, currentTimestamp);
}
```

Add the helper methods used above:

```csharp
/// <summary>
/// Returns the current monotonic timestamp used to measure elapsed time between parameterless updates.
/// </summary>
/// <returns>One stopwatch timestamp from the active host clock.</returns>
protected virtual long GetCurrentUpdateTimestamp() {
    return Stopwatch.GetTimestamp();
}

/// <summary>
/// Computes elapsed seconds from the measured update timestamp stream.
/// </summary>
/// <param name="currentTimestamp">Current stopwatch timestamp.</param>
/// <returns>Elapsed seconds since the previous measured update, or zero on the first update.</returns>
double ResolveMeasuredElapsedSeconds(long currentTimestamp) {
    if (!HasPreviousUpdateTimestamp) {
        return 0d;
    }

    long elapsedTimestampDelta = currentTimestamp - PreviousUpdateTimestamp;
    return elapsedTimestampDelta / (double)Stopwatch.Frequency;
}
```

- [ ] **Step 3: Route both update overloads through one shared timing-state writer**

Update `Core.Update(double elapsedSeconds)` and add one shared helper in `engine/helengine.core/Core.cs`:

```csharp
/// <summary>
/// Advances the engine update loop for objects and input using one explicit elapsed frame time.
/// </summary>
/// <param name="elapsedSeconds">Elapsed frame time in seconds supplied by the host runtime.</param>
public virtual void Update(double elapsedSeconds) {
    ValidateElapsedSeconds(elapsedSeconds);
    long currentTimestamp = GetCurrentUpdateTimestamp();
    AdvanceUpdate(elapsedSeconds, currentTimestamp);
}

/// <summary>
/// Applies one elapsed update slice to cached timing state and the normal update pipeline.
/// </summary>
/// <param name="elapsedSeconds">Elapsed update time in seconds.</param>
/// <param name="currentTimestamp">Current stopwatch timestamp captured for this update.</param>
void AdvanceUpdate(double elapsedSeconds, long currentTimestamp) {
    float elapsedSecondsFloat = (float)elapsedSeconds;
    FrameDeltaSeconds = elapsedSeconds;
    UnscaledDeltaTime = elapsedSecondsFloat;
    DeltaTime = elapsedSecondsFloat;
    TotalElapsedSeconds += elapsedSeconds;
    PreviousUpdateTimestamp = currentTimestamp;
    HasPreviousUpdateTimestamp = true;

    Input.EarlyUpdate();
    FPSComponent.RecordUpdateFrame();

    ObjectManager.Update();
    if (SceneManager != null) {
        SceneManager.FlushPendingOperations();
    }
    UpdatePhysics(elapsedSeconds);

    Input.Update();
    PointerInteractionSystem.Update();
}
```

This keeps explicit host-driven updates and measured parameterless updates consistent and prevents the next parameterless update from measuring across an earlier explicit update.

- [ ] **Step 4: Update the stale `DefaultUpdateDeltaSeconds` comment and the `EditorCore` parameterless path**

Change the `DefaultUpdateDeltaSeconds` XML comment in `engine/helengine.core/CoreInitializationOptions.cs` to describe it as a host-configurable fixed-step value for callers that still choose to drive `Update(double)` explicitly.

Update `engine/helengine.editor/EditorCore.cs` so the parameterless editor update path measures real elapsed time too:

```csharp
/// <inheritdoc />
public override void Update() {
    ComponentExecutionContext.EnterEditor();
    try {
        base.Update();
        EditorObjectManager.Update();
    } finally {
        ComponentExecutionContext.ExitEditor();
    }
}
```

Keep `EditorCore.Update(double elapsedSeconds)` as the explicit fixed-step override for callers that want to supply their own delta.

- [ ] **Step 5: Run the timing tests to verify the implementation passes**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CoreTimingTests
```

Expected: PASS.

- [ ] **Step 6: Commit the runtime timing implementation**

```powershell
rtk git add engine/helengine.core/Core.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.editor/EditorCore.cs
rtk git commit -m "Add core delta time properties"
```

### Task 3: Final Verification And API-Footprint Review

**Files:**
- Modify: `engine/helengine.editor.tests/CoreTimingTests.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor/EditorCore.cs`

- [ ] **Step 1: Sanity-scan the repo for the new timing API surface**

Run:

```powershell
rtk rg -n "DeltaTime|UnscaledDeltaTime|FrameDeltaSeconds|DefaultUpdateDeltaSeconds" engine/helengine.core engine/helengine.editor engine/helengine.editor.tests engine/helengine.render.validation
```

Expected:
- `Core.cs` exposes `DeltaTime` and `UnscaledDeltaTime`
- `CoreTimingTests.cs` covers first-update zero, measured-update positive, explicit-update behavior, and in-component visibility
- `EditorCore.cs` no longer hardcodes `InitializationOptions.DefaultUpdateDeltaSeconds` in its parameterless path
- `RenderValidationRunner.cs` is still free to use `DefaultUpdateDeltaSeconds` for its explicit first-frame warmup

- [ ] **Step 2: Run the narrow verification bundle**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CoreTimingTests|FullyQualifiedName~EditorUpdateComponentExecutionPolicyTests|FullyQualifiedName~FPSComponentTests|FullyQualifiedName~AnimationPlayerComponentTests"
```

Expected: PASS.

- [ ] **Step 3: Run full editor test verification**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Review the changed files against repository conventions**

Check the touched files for:

- substantive XML comments on all new or changed classes, properties, and methods
- one class per file for the two new test helpers
- PascalCase private fields in `Core.cs`
- no tuples
- no local helper functions
- no hidden static `Time` singleton

- [ ] **Step 5: Create the final integration commit**

```powershell
rtk git add engine/helengine.core/Core.cs engine/helengine.core/CoreInitializationOptions.cs engine/helengine.editor/EditorCore.cs engine/helengine.editor.tests/CoreTimingTests.cs engine/helengine.editor.tests/testing/TestClockDrivenCore.cs engine/helengine.editor.tests/testing/TestDeltaTimeProbeComponent.cs
rtk git commit -m "Complete core delta time API"
```

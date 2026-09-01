# BEPU Buffer Pool Lifetime Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every `BepuPhysicsWorld3D` release its simulation-owned pooled memory deterministically so the editor testhost cannot abort from the BEPU `BufferPool` finalizer.

**Architecture:** `BepuPhysicsWorld3D` becomes the disposable owner of its simulation, collidable-property stores, gravity store, and buffer pool. `BepuRuntimeComponentRegistration.RegistrationState` disposes the world it owns when the core releases registration state, while standalone test worlds use explicit `using` ownership and the editor integration test disposes its core.

**Tech Stack:** C#/.NET 9, xUnit, BEPUphysics v2 `BufferPool`, HelEngine native-ownership helpers

---

### Task 1: Make BEPU world ownership deterministic

**Files:**
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Modify: `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuEntitySynchronization3DTests.cs`
- Modify: `engine/helengine.editor.tests/AutomaticPhysicsRuntimePayloadTests.cs`

- [ ] **Step 1: Add failing world-disposal coverage**

Add a test to `BepuPhysicsWorld3DTests` that obtains the private `BufferPoolValue` through reflection, confirms the constructed simulation allocated pooled bytes, asserts the world implements `IDisposable`, disposes it twice, and confirms `GetTotalAllocatedByteCount()` is zero:

```csharp
/// <summary>
/// Ensures disposing a world releases every native block retained by its BEPU buffer pool and remains idempotent.
/// </summary>
[Fact]
public void Dispose_WhenWorldOwnsPooledResources_ClearsBufferPoolAndIsIdempotent() {
    BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
    FieldInfo bufferPoolField = typeof(BepuPhysicsWorld3D).GetField("BufferPoolValue", BindingFlags.Instance | BindingFlags.NonPublic);

    Assert.NotNull(bufferPoolField);
    BepuUtilities.Memory.BufferPool bufferPool = Assert.IsType<BepuUtilities.Memory.BufferPool>(bufferPoolField.GetValue(world));
    Assert.True(bufferPool.GetTotalAllocatedByteCount() > 0UL);

    IDisposable disposable = Assert.IsAssignableFrom<IDisposable>(world);
    disposable.Dispose();
    disposable.Dispose();

    Assert.Equal(0UL, bufferPool.GetTotalAllocatedByteCount());
}
```

- [ ] **Step 2: Add failing registration-state ownership coverage**

Add a test to `BepuRuntimeComponentRegistrationTests` that initializes a core, registers BEPU, loads a static physics entity so a world is created, reflects its `BufferPoolValue`, disposes the core twice, and asserts the core detached the world and the pool owns zero bytes:

```csharp
/// <summary>
/// Ensures releasing one core also releases the BEPU world and native pool owned by its registration state.
/// </summary>
[Fact]
public void Core_Dispose_WhenRegistrationOwnsRuntimeWorld_ReleasesWorldBufferPool() {
    Core core = CreateInitializedCore();
    BepuRuntimeComponentRegistration.Register(core);
    BepuRuntimeComponentRegistration.HandleLoadedScene(core, [CreateStaticBoxPhysicsEntity(core)]);
    BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
    System.Reflection.FieldInfo bufferPoolField = typeof(BepuPhysicsWorld3D).GetField(
        "BufferPoolValue",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    Assert.NotNull(bufferPoolField);
    BepuUtilities.Memory.BufferPool bufferPool = Assert.IsType<BepuUtilities.Memory.BufferPool>(bufferPoolField.GetValue(world));
    Assert.True(bufferPool.GetTotalAllocatedByteCount() > 0UL);

    core.Dispose();
    core.Dispose();

    Assert.Null(core.PhysicsRuntime);
    Assert.Equal(0UL, bufferPool.GetTotalAllocatedByteCount());
}
```

- [ ] **Step 3: Run the two new tests and verify RED**

Run:

```powershell
rtk dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter "FullyQualifiedName~Dispose_WhenWorldOwnsPooledResources_ClearsBufferPoolAndIsIdempotent|FullyQualifiedName~Core_Dispose_WhenRegistrationOwnsRuntimeWorld_ReleasesWorldBufferPool" -v:minimal
```

Expected: both tests fail. The first reports that `BepuPhysicsWorld3D` is not assignable to `IDisposable`; the second reports retained pool bytes and a still-attached runtime after core disposal.

- [ ] **Step 4: Implement idempotent world disposal**

Update the class declaration to implement `IDisposable`, add an `IsDisposed` field, and add a public `Dispose()` method. The method must return immediately after the first call, dispose `SimulationValue`, `CollidablePropertiesValue`, and `GravityAccelerationsValue`, clear the body and trigger registries, call `BufferPoolValue.Clear()`, and leave total allocated pool bytes at zero. Extract the existing three simulation-resource disposal statements from `ResetSimulation()` into one class-level `DisposeSimulationResources()` method so reset and final disposal share the same release order; do not add a local function and do not call `IDisposable.Dispose()` on `BufferPool` because the vendored finalizer expects the pool arrays to remain readable after `Clear()`.

- [ ] **Step 5: Make registration state release its owned world**

In `RegistrationState.Dispose()`, detach `RuntimeWorld` when it is the core's active runtime, call `RuntimeWorld.Dispose()`, then clear the reference. Keep disposal idempotent. When an explicit registration or attachment replaces a different reserved world, release the old owned world through one class-level helper before storing the replacement; never dispose the same instance being reattached.

- [ ] **Step 6: Mark every standalone test world as explicitly owned**

Change each local world construction in `BepuPhysicsWorld3DTests.cs`, `BepuEntitySynchronization3DTests.cs`, and the standalone profiler test in `BepuRuntimeComponentRegistrationTests.cs` from:

```csharp
BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
```

or its solve-schedule equivalent to:

```csharp
using BepuPhysicsWorld3D world = BepuPhysicsWorld3D.CreateDefault();
```

Use the matching existing factory call at each site. Do not alter test assertions.

- [ ] **Step 7: Dispose the editor integration test's owning core**

In `AutomaticPhysicsRuntimePayloadTests.Load_WhenAutomaticPhysicsPayloadsDescribeStackedBoxes_DynamicUpperBoxFalls`, declare the core with `using`:

```csharp
using Core core = new Core(new CoreInitializationOptions {
    ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
});
```

The registration state then proves the production ownership path releases the attached world at test exit.

- [ ] **Step 8: Verify GREEN for focused disposal and editor tests**

Run:

```powershell
rtk dotnet test engine\helengine.bepu.tests\helengine.bepu.tests.csproj --no-restore --filter "FullyQualifiedName~BepuPhysicsWorld3DTests|FullyQualifiedName~BepuRuntimeComponentRegistrationTests|FullyQualifiedName~BepuEntitySynchronization3DTests" -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AutomaticPhysicsRuntimePayloadTests" -v:minimal
```

Expected: both commands exit 0 with all selected tests passing and no testhost crash.

- [ ] **Step 9: Commit the ownership repair**

Run:

```powershell
rtk git add -- engine/helengine.bepu/BepuPhysicsWorld3D.cs engine/helengine.bepu/BepuRuntimeComponentRegistration.cs engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs engine/helengine.bepu.tests/BepuRuntimeComponentRegistrationTests.cs engine/helengine.bepu.tests/BepuEntitySynchronization3DTests.cs engine/helengine.editor.tests/AutomaticPhysicsRuntimePayloadTests.cs
rtk git diff --cached --check
rtk git commit -m "Dispose BEPU runtime buffer pools"
```

Expected: only the six listed files are committed; the unrelated dirty runtime deserializer remains unstaged and untouched.

### Task 2: Re-establish a complete clean editor failure inventory

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Advance the clean detached verifier to the repair commit**

Run `rtk git checkout --detach <repair-commit>` inside `C:\dev\helprojs\.worktrees\helengine-software-path-tracer-green`, preserving its verifier-only BEPU submodule junction and restored dependencies.

- [ ] **Step 2: Run the full editor suite with persistent results**

Run:

```powershell
$results = 'C:\dev\helprojs\.worktrees\helengine-software-path-tracer-green\build\verification\editor-after-bepu-lifetime'
New-Item -ItemType Directory -Force -Path $results | Out-Null
rtk proxy dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --logger 'trx;LogFileName=editor-after-bepu-lifetime.trx' --results-directory $results -v:minimal
```

Expected: the testhost completes without a `BufferPool` finalizer abort. Preserve the TRX and group every remaining failed result by test class and error signature before writing the next repair plan.

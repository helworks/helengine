# Debug-Only Scene Load Timing Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic debug-only engine-side scene-load timing diagnostics contract, instrument the core scene-load boundaries, and hook the PS2 boot host up as a consumer without introducing PS2-specific engine branching.

**Architecture:** Extend the existing `IRuntimeDiagnosticsProvider` attachment model with one new debug-only scene-load timing interface and route it through `Core`, `SceneManager`, and `RuntimeSceneLoadService`. Keep all timing ownership in shared engine runtime code, then attach a PS2-side sink that only consumes and prints the structured timing events.

**Tech Stack:** C# (.NET 9), helengine runtime/core, helengine editor test harness, generated C++ runtime contract, PS2 native host source tests.

---

### Task 1: Add the Debug-Only Scene-Load Timing Contract

**Files:**
- Create: `C:\dev\helworks\helengine\engine\helengine.core\diagnostics\IRuntimeSceneLoadTimingDiagnosticsProvider.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\Core.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneManagerTests.cs`

- [ ] **Step 1: Write the failing core wiring test**

Add this test near the diagnostics-oriented tests in [`SceneManagerTests.cs`](C:/dev/helworks/helengine/engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs):

```csharp
/// <summary>
/// Ensures core bootstrap forwards the debug-only scene-load timing diagnostics provider into runtime scene services.
/// </summary>
[Fact]
public void Initialize_whenRuntimeDiagnosticsProviderSupportsSceneLoadTimings_forwardsProviderIntoSceneRuntimeServices() {
    WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
    FakeRuntimeSceneLoadTimingDiagnosticsProvider diagnosticsProvider = new FakeRuntimeSceneLoadTimingDiagnosticsProvider();
    Core core = CreateCore(
        CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")),
        diagnosticsProvider);

    core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

#if DEBUG
    Assert.True(diagnosticsProvider.HasReceivedEvents);
#else
    Assert.False(diagnosticsProvider.HasReceivedEvents);
#endif
}
```

Add this test helper to the same file:

```csharp
sealed class FakeRuntimeSceneLoadTimingDiagnosticsProvider
    : IRuntimeDiagnosticsProvider
#if DEBUG
    , IRuntimeSceneLoadTimingDiagnosticsProvider
#endif
{
    public bool HasReceivedEvents { get; private set; }

    public RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot() {
        return new RuntimeMemoryDiagnosticsSnapshot();
    }

#if DEBUG
    public void ReportSceneLoadPhaseTiming(string phaseName, double elapsedMilliseconds) {
        HasReceivedEvents = true;
    }
#endif
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Initialize_whenRuntimeDiagnosticsProviderSupportsSceneLoadTimings_forwardsProviderIntoSceneRuntimeServices" -v minimal
```

Expected:

- test fails because `IRuntimeSceneLoadTimingDiagnosticsProvider` does not exist yet

- [ ] **Step 3: Add the debug-only scene-load timing interface**

Create [`IRuntimeSceneLoadTimingDiagnosticsProvider.cs`](C:/dev/helworks/helengine/engine/helengine.core/diagnostics/IRuntimeSceneLoadTimingDiagnosticsProvider.cs):

```csharp
namespace helengine {
#if DEBUG
    /// <summary>
    /// Receives exclusive scene-load phase timing notifications from the runtime while debug diagnostics are enabled.
    /// </summary>
    public interface IRuntimeSceneLoadTimingDiagnosticsProvider {
        /// <summary>
        /// Reports the elapsed duration for one runtime scene-load phase.
        /// </summary>
        /// <param name="phaseName">Stable scene-load phase name.</param>
        /// <param name="elapsedMilliseconds">Exclusive elapsed phase duration in milliseconds.</param>
        void ReportSceneLoadPhaseTiming(string phaseName, double elapsedMilliseconds);
    }
#endif
}
```

- [ ] **Step 4: Forward the optional diagnostics provider through `Core`**

In [`Core.cs`](C:/dev/helworks/helengine/engine/helengine.core/Core.cs), update scene-load-service creation and scene-manager creation so they resolve the timing provider through the existing `RuntimeDiagnosticsProvider` seam.

Add this helper near the other private factory helpers:

```csharp
#if DEBUG
        IRuntimeSceneLoadTimingDiagnosticsProvider ResolveSceneLoadTimingDiagnosticsProvider() {
            if (InitializationOptions.RuntimeDiagnosticsProvider is IRuntimeSceneLoadTimingDiagnosticsProvider diagnosticsProvider) {
                return diagnosticsProvider;
            }

            return null;
        }
#endif
```

Update runtime scene-load-service creation to use the provider:

```csharp
#if DEBUG
            IRuntimeSceneLoadTimingDiagnosticsProvider sceneLoadTimingDiagnosticsProvider = ResolveSceneLoadTimingDiagnosticsProvider();
            SceneLoadService = new RuntimeSceneLoadService(SceneAssetReferenceResolver, SceneRuntimeComponentRegistry, sceneLoadTimingDiagnosticsProvider);
#else
            SceneLoadService = new RuntimeSceneLoadService(SceneAssetReferenceResolver, SceneRuntimeComponentRegistry);
#endif
```

Update `CreateSceneManager(...)` to resolve the same optional provider:

```csharp
#if DEBUG
            IRuntimeSceneLoadTimingDiagnosticsProvider sceneLoadTimingDiagnosticsProvider = ResolveSceneLoadTimingDiagnosticsProvider();
#endif

            return new SceneManager(
                sceneCatalog,
                contentManager,
                SceneLoadService,
                ObjectManager,
                InitializationOptions.ScenePathResolver,
                sceneTransitionDiagnosticsProvider,
                entityDisposalDiagnosticsProvider
#if DEBUG
                ,
                sceneLoadTimingDiagnosticsProvider
#endif
            );
```

- [ ] **Step 5: Run the targeted test to verify it passes**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Initialize_whenRuntimeDiagnosticsProviderSupportsSceneLoadTimings_forwardsProviderIntoSceneRuntimeServices" -v minimal
```

Expected:

- target test passes

- [ ] **Step 6: Commit**

```powershell
git -C C:\dev\helworks\helengine add engine/helengine.core/diagnostics/IRuntimeSceneLoadTimingDiagnosticsProvider.cs engine/helengine.core/Core.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
git -C C:\dev\helworks\helengine commit -m "Add debug scene load timing diagnostics contract"
```

### Task 2: Instrument `SceneManager` Load Phases

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\scene\runtime\SceneManager.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneManagerTests.cs`

- [ ] **Step 1: Write the failing scene-manager timing-order test**

Add this test to [`SceneManagerTests.cs`](C:/dev/helworks/helengine/engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs):

```csharp
/// <summary>
/// Ensures debug scene-load timing diagnostics split scene-manager orchestration from content load and activation boundaries.
/// </summary>
[Fact]
public void LoadScene_whenDebugTimingProviderIsAttached_reportsSceneManagerTimingPhasesInOrder() {
    WriteSceneAsset("cooked/scenes/Bootstrap.hasset", 1u);
    RecordingRuntimeSceneLoadTimingDiagnosticsProvider diagnosticsProvider = new RecordingRuntimeSceneLoadTimingDiagnosticsProvider();
    Core core = CreateCore(
        CreateSceneCatalog(new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")),
        diagnosticsProvider);

    core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

#if DEBUG
    Assert.Contains("SceneManager.LoadSceneRequest", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.LoadSceneImmediate", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.BeforeContentLoad", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.SceneContentLoad", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.BeforeSceneLoadServiceLoad", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.AfterSceneLoadServiceLoad", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.SceneActivation", diagnosticsProvider.PhaseNames);
    Assert.Contains("SceneManager.SceneRegistrationComplete", diagnosticsProvider.PhaseNames);
    Assert.True(diagnosticsProvider.GetIndex("SceneManager.LoadSceneRequest") < diagnosticsProvider.GetIndex("SceneManager.SceneRegistrationComplete"));
#endif
}
```

Add this helper to the same file:

```csharp
sealed class RecordingRuntimeSceneLoadTimingDiagnosticsProvider
    : IRuntimeDiagnosticsProvider
#if DEBUG
    , IRuntimeSceneLoadTimingDiagnosticsProvider
#endif
{
    public List<string> PhaseNames { get; } = new List<string>();

    public RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot() {
        return new RuntimeMemoryDiagnosticsSnapshot();
    }

#if DEBUG
    public void ReportSceneLoadPhaseTiming(string phaseName, double elapsedMilliseconds) {
        PhaseNames.Add(phaseName);
        Assert.True(elapsedMilliseconds >= 0d);
    }
#endif

    public int GetIndex(string phaseName) {
        return PhaseNames.IndexOf(phaseName);
    }
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~LoadScene_whenDebugTimingProviderIsAttached_reportsSceneManagerTimingPhasesInOrder" -v minimal
```

Expected:

- test fails because `SceneManager` does not emit timing phases yet

- [ ] **Step 3: Add timing-provider storage and timing emission to `SceneManager`**

In [`SceneManager.cs`](C:/dev/helworks/helengine/engine/helengine.core/scene/runtime/SceneManager.cs), add the debug-only provider field and constructor parameter:

```csharp
#if DEBUG
        readonly IRuntimeSceneLoadTimingDiagnosticsProvider SceneLoadTimingDiagnosticsProvider;
#endif
```

Extend the constructor signature and store the provider:

```csharp
        public SceneManager(
            RuntimeSceneCatalog sceneCatalog,
            ContentManager contentManager,
            RuntimeSceneLoadService sceneLoadService,
            ObjectManager objectManager,
            ISceneIdPathResolver scenePathResolver,
            IRuntimeSceneTransitionDiagnosticsProvider sceneTransitionDiagnosticsProvider,
            IRuntimeEntityDisposalDiagnosticsProvider entityDisposalDiagnosticsProvider
#if DEBUG
            ,
            IRuntimeSceneLoadTimingDiagnosticsProvider sceneLoadTimingDiagnosticsProvider
#endif
        ) {
            ...
#if DEBUG
            SceneLoadTimingDiagnosticsProvider = sceneLoadTimingDiagnosticsProvider;
#endif
        }
```

Add this helper inside the class:

```csharp
#if DEBUG
        void ReportSceneLoadTiming(string phaseName, Stopwatch stopwatch) {
            if (SceneLoadTimingDiagnosticsProvider == null || stopwatch == null) {
                return;
            }

            SceneLoadTimingDiagnosticsProvider.ReportSceneLoadPhaseTiming(phaseName, stopwatch.Elapsed.TotalMilliseconds);
        }
#endif
```

Instrument `LoadScene(...)` and `LoadSceneImmediate(...)` with explicit stopwatches around:

```csharp
SceneManager.LoadSceneRequest
SceneManager.LoadSceneImmediate
SceneManager.BeforeContentLoad
SceneManager.SceneContentLoad
SceneManager.BeforeSceneLoadServiceLoad
SceneManager.AfterSceneLoadServiceLoad
SceneManager.SceneActivation
SceneManager.SceneRegistrationComplete
```

Use this pattern for each boundary:

```csharp
#if DEBUG
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
#endif
            // existing phase body
#if DEBUG
            phaseStopwatch.Stop();
            ReportSceneLoadTiming("SceneManager.SceneContentLoad", phaseStopwatch);
#endif
```

- [ ] **Step 4: Run the targeted scene-manager test to verify it passes**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~LoadScene_whenDebugTimingProviderIsAttached_reportsSceneManagerTimingPhasesInOrder" -v minimal
```

Expected:

- target test passes

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helworks\helengine add engine/helengine.core/scene/runtime/SceneManager.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
git -C C:\dev\helworks\helengine commit -m "Add scene manager load timing diagnostics"
```

### Task 3: Instrument `RuntimeSceneLoadService` Load Phases

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\scene\runtime\RuntimeSceneLoadService.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing runtime-scene-load-service timing test**

Add this test to [`RuntimeSceneLoadServiceTests.cs`](C:/dev/helworks/helengine/engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs):

```csharp
/// <summary>
/// Ensures debug scene-load timing diagnostics split runtime scene materialization from hierarchy initialization.
/// </summary>
[Fact]
public void Load_whenDebugTimingProviderIsAttached_reportsRuntimeSceneLoadServiceTimingPhases() {
    RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
        Core.Instance.ContentManager,
        TempRootPath,
        ShaderCompileTarget.DirectX11);
    RecordingRuntimeSceneLoadTimingDiagnosticsProvider diagnosticsProvider = new RecordingRuntimeSceneLoadTimingDiagnosticsProvider();
    RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(
        resolver,
        RuntimeComponentRegistry.CreateDefault(),
        diagnosticsProvider);
    SceneAsset sceneAsset = new SceneAsset {
        RootEntities = new[] {
            new SceneEntityAsset {
                Id = 1u,
                Name = "Root",
                Children = new[] {
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Child"
                    }
                }
            }
        }
    };

    loadService.Load(sceneAsset);

#if DEBUG
    Assert.Contains("RuntimeSceneLoadService.Load", diagnosticsProvider.PhaseNames);
    Assert.Contains("RuntimeSceneLoadService.RootEntityLoadLoop", diagnosticsProvider.PhaseNames);
    Assert.Contains("RuntimeSceneLoadService.InitializeHierarchyLoop", diagnosticsProvider.PhaseNames);
    Assert.True(diagnosticsProvider.GetIndex("RuntimeSceneLoadService.RootEntityLoadLoop") < diagnosticsProvider.GetIndex("RuntimeSceneLoadService.InitializeHierarchyLoop"));
#endif
}
```

Reuse or duplicate the same `RecordingRuntimeSceneLoadTimingDiagnosticsProvider` helper pattern in this test file.

- [ ] **Step 2: Run the targeted runtime-scene-load-service test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Load_whenDebugTimingProviderIsAttached_reportsRuntimeSceneLoadServiceTimingPhases" -v minimal
```

Expected:

- test fails because `RuntimeSceneLoadService` does not emit timing phases yet

- [ ] **Step 3: Add the timing provider and phase instrumentation to `RuntimeSceneLoadService`**

In [`RuntimeSceneLoadService.cs`](C:/dev/helworks/helengine/engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs), add the debug-only field:

```csharp
#if DEBUG
        readonly IRuntimeSceneLoadTimingDiagnosticsProvider SceneLoadTimingDiagnosticsProvider;
#endif
```

Extend both constructors so the default constructor delegates to the full constructor with an optional provider:

```csharp
        public RuntimeSceneLoadService(RuntimeSceneAssetReferenceResolver referenceResolver)
#if DEBUG
            : this(referenceResolver, RuntimeComponentRegistry.CreateDefault(), null) {
#else
            : this(referenceResolver, RuntimeComponentRegistry.CreateDefault()) {
#endif
        }
```

Update the full constructor:

```csharp
        public RuntimeSceneLoadService(
            RuntimeSceneAssetReferenceResolver referenceResolver,
            RuntimeComponentRegistry componentRegistry
#if DEBUG
            ,
            IRuntimeSceneLoadTimingDiagnosticsProvider sceneLoadTimingDiagnosticsProvider
#endif
        ) {
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            ComponentRegistry = componentRegistry ?? throw new ArgumentNullException(nameof(componentRegistry));
#if DEBUG
            SceneLoadTimingDiagnosticsProvider = sceneLoadTimingDiagnosticsProvider;
#endif
        }
```

Add the helper:

```csharp
#if DEBUG
        void ReportSceneLoadTiming(string phaseName, Stopwatch stopwatch) {
            if (SceneLoadTimingDiagnosticsProvider == null || stopwatch == null) {
                return;
            }

            SceneLoadTimingDiagnosticsProvider.ReportSceneLoadPhaseTiming(phaseName, stopwatch.Elapsed.TotalMilliseconds);
        }
#endif
```

Instrument these phases in `Load(...)`:

```csharp
RuntimeSceneLoadService.Load
RuntimeSceneLoadService.RootEntityLoadLoop
RuntimeSceneLoadService.InitializeHierarchyLoop
```

Use explicit stopwatches around each region, for example:

```csharp
#if DEBUG
            Stopwatch rootEntityLoopStopwatch = Stopwatch.StartNew();
#endif
            for (int index = 0; index < rootEntityAssets.Length; index++) {
                ...
            }
#if DEBUG
            rootEntityLoopStopwatch.Stop();
            ReportSceneLoadTiming("RuntimeSceneLoadService.RootEntityLoadLoop", rootEntityLoopStopwatch);
#endif
```

- [ ] **Step 4: Run the targeted runtime-scene-load-service test to verify it passes**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Load_whenDebugTimingProviderIsAttached_reportsRuntimeSceneLoadServiceTimingPhases" -v minimal
```

Expected:

- target test passes

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helworks\helengine add engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git -C C:\dev\helworks\helengine commit -m "Add runtime scene load service timing diagnostics"
```

### Task 4: Hook the PS2 Boot Host to the Generic Timing Sink and Verify Debug/Release Boundaries

**Files:**
- Modify: `C:\dev\helworks\helengine-ps2\src\platform\ps2\Ps2BootHost.cpp`
- Modify: `C:\dev\helworks\helengine-ps2\builder.tests\Ps2BootHostSourceTests.cs`

- [ ] **Step 1: Write the failing PS2 source-contract test**

Add this test to [`Ps2BootHostSourceTests.cs`](C:/dev/helworks/helengine-ps2/builder.tests/Ps2BootHostSourceTests.cs):

```csharp
/// <summary>
/// Ensures PS2 debug boot diagnostics consume the shared engine-owned scene-load timing contract instead of defining PS2-specific timing boundaries.
/// </summary>
[Fact]
public void Ps2BootHost_WhenDebugSceneLoadTimingDiagnosticsAreEnabled_ImplementsSharedTimingSink() {
    string sourcePath = Path.Combine(GetRepositoryRootPath(), "src", "platform", "ps2", "Ps2BootHost.cpp");
    Assert.True(File.Exists(sourcePath), $"Expected boot host source at '{sourcePath}'.");

    string source = File.ReadAllText(sourcePath);

    Assert.Contains("IRuntimeSceneLoadTimingDiagnosticsProvider", source, StringComparison.Ordinal);
    Assert.Contains("ReportSceneLoadPhaseTiming", source, StringComparison.Ordinal);
    Assert.DoesNotContain("SceneManager.LoadSceneRequest", source, StringComparison.Ordinal);
    Assert.DoesNotContain("RuntimeSceneLoadService.RootEntityLoadLoop", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the targeted PS2 source-contract test to verify it fails**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore --filter "FullyQualifiedName~Ps2BootHost_WhenDebugSceneLoadTimingDiagnosticsAreEnabled_ImplementsSharedTimingSink" -v minimal
```

Expected:

- test fails because the boot host does not implement the new shared interface yet

- [ ] **Step 3: Implement the PS2 debug sink against the shared interface**

In [`Ps2BootHost.cpp`](C:/dev/helworks/helengine-ps2/src/platform/ps2/Ps2BootHost.cpp):

- include the generated header:

```cpp
#include "IRuntimeSceneLoadTimingDiagnosticsProvider.hpp"
```

- update the existing boot diagnostics provider declaration:

```cpp
    class Ps2BootRuntimeDiagnosticsProvider final
        : public ::IRuntimeDiagnosticsProvider
        , public ::IRuntimeSceneTransitionDiagnosticsProvider
        , public ::IRuntimeEntityDisposalDiagnosticsProvider
        , public ::IRuntimeUpdateStageDiagnosticsProvider
#if DEBUG
        , public ::IRuntimeSceneLoadTimingDiagnosticsProvider
#endif
```

- implement the sink method by only printing the emitted engine phase name and elapsed time:

```cpp
#if DEBUG
        void ReportSceneLoadPhaseTiming(std::string phaseName, double elapsedMilliseconds) override {
            BootLog(
                std::string("scene timing ")
                + phaseName
                + " ms="
                + std::to_string(elapsedMilliseconds));
        }
#endif
```

Do not add new PS2-owned timing boundaries in this step. The PS2 host should only consume the engine-emitted callbacks.

- [ ] **Step 4: Run the PS2 source-contract test and the release-safety verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore --filter "FullyQualifiedName~Ps2BootHost_WhenDebugSceneLoadTimingDiagnosticsAreEnabled_ImplementsSharedTimingSink" -v minimal
rtk dotnet build C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj -c Release --no-restore
```

Expected:

- PS2 source-contract test passes
- release core build passes without requiring the debug-only diagnostics interface

- [ ] **Step 5: Run the focused integration verification**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneManagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore --filter "FullyQualifiedName~Ps2BootHostSourceTests" -v minimal
```

Expected:

- focused engine scene tests pass
- PS2 boot-host source tests pass

- [ ] **Step 6: Commit**

```powershell
git -C C:\dev\helworks\helengine-ps2 add src/platform/ps2/Ps2BootHost.cpp builder.tests/Ps2BootHostSourceTests.cs
git -C C:\dev\helworks\helengine-ps2 commit -m "Consume scene load timing diagnostics on PS2"
```

### Task 5: Final Verification Sweep

**Files:**
- Modify: none
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj`
- Test: `C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj`

- [ ] **Step 1: Run the full focused verification set**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~SceneManagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
rtk dotnet build C:\dev\helworks\helengine\engine\helengine.core\helengine.core.csproj -c Release --no-restore
rtk dotnet test C:\dev\helworks\helengine-ps2\builder.tests\helengine.ps2.builder.tests.csproj --no-restore --filter "FullyQualifiedName~Ps2BootHostSourceTests" -v minimal
```

Expected:

- focused editor/runtime tests pass
- release core build passes
- PS2 source-contract tests pass

- [ ] **Step 2: Commit any remaining test-only or follow-up adjustments**

```powershell
git -C C:\dev\helworks\helengine status --short
git -C C:\dev\helworks\helengine-ps2 status --short
```

Expected:

- no unexpected uncommitted files remain for this feature

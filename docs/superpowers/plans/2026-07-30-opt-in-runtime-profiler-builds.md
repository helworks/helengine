# Opt-In Runtime Profiler Builds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove generic runtime-profiler execution from ordinary generated builds and enable it only through an explicit profiling codegen profile.

**Architecture:** The shared build graph treats `runtime_profiler` as default-disabled for generated runtimes and removes that disable symbol only when the selected codegen profile explicitly enables the feature. Managed editor/test builds continue compiling profiler APIs because the generated-runtime disable symbol is absent. Core and physics integration points use that symbol to omit profiler state, collection, and supporting types from normal native output; PS2 exposes separate Default and Profiling profiles.

**Tech Stack:** C#/.NET 10, xUnit, Helengine platform metadata, Helengine C#-to-C++ codegen, PS2 C++/ps2sdk, PowerShell build waiter, PCSX2.

---

### Task 1: Add shared opt-in generated-runtime feature selection

**Files:**
- Modify: `engine/helengine.baseplatform/Definitions/PlatformCodegenSettingIds.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write failing build-graph tests**

Add one test proving an ordinary profile receives `HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER`, and one proving a profile whose `codegen-enabled-features` default contains `runtime_profiler` does not receive that disable symbol. Reuse `RecordingGeneratedCoreRegenerationService` and invoke `RunRegenerateCore` as the existing forced-disabled-feature test does.

```csharp
Assert.Contains(
    EditorPlatformPreprocessorSymbolService.RuntimeProfilerDisabledSymbol,
    regenerationService.AdditionalPreprocessorSymbols);

Assert.DoesNotContain(
    EditorPlatformPreprocessorSymbolService.RuntimeProfilerDisabledSymbol,
    regenerationService.AdditionalPreprocessorSymbols);
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
rtk.exe dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeProfiler" --no-restore
```

Expected: FAIL because the enabled-feature setting and runtime-profiler disable symbol do not exist.

- [ ] **Step 3: Add the enabled-feature setting identifier**

Add this documented constant to `PlatformCodegenSettingIds`:

```csharp
/// <summary>
/// Stable setting identifier for generated-runtime features explicitly enabled by one codegen profile.
/// </summary>
public const string EnabledFeatures = "codegen-enabled-features";
```

- [ ] **Step 4: Resolve the default-off profiler symbol from effective profile settings**

Add the following public constant and resolver behavior to `EditorPlatformPreprocessorSymbolService`:

```csharp
/// <summary>
/// Generated-runtime symbol that removes generic runtime profiling from ordinary builds.
/// </summary>
public const string RuntimeProfilerDisabledSymbol = "HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER";
```

Add a method that reads the selected override first, otherwise reads the selected profile's `PlatformCodegenSettingIds.EnabledFeatures` default, splits feature ids on semicolon/comma/space, and returns `[RuntimeProfilerDisabledSymbol]` unless `runtime_profiler` is present. Validate both arguments and compare feature ids ordinal-ignore-case.

```csharp
public static IReadOnlyList<string> ResolveDefaultDisabledFeatureSymbols(
    PlatformCodegenProfileDefinition codegenProfile,
    IReadOnlyDictionary<string, string> selectedCodegenOptionValues) {
    string enabledFeatures = ResolveEnabledFeatureValue(codegenProfile, selectedCodegenOptionValues);
    string[] featureIds = enabledFeatures.Split(
        [';', ',', ' '],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return featureIds.Contains("runtime_profiler", StringComparer.OrdinalIgnoreCase)
        ? []
        : [RuntimeProfilerDisabledSymbol];
}
```

Keep value resolution in a separately documented static method on the service; do not add a local helper.

- [ ] **Step 5: Forward the default-disabled symbol through core regeneration**

In `RunRegenerateCore`, resolve and combine the new symbols before explicit forced-disabled symbols:

```csharp
IReadOnlyList<string> defaultDisabledFeatureSymbols =
    EditorPlatformPreprocessorSymbolService.ResolveDefaultDisabledFeatureSymbols(
        selectedCodegenProfile,
        selectedCodegenOptionValues);
additionalPreprocessorSymbols = EditorGeneratedCoreRegenerationService.CombineAdditionalPreprocessorSymbols(
    additionalPreprocessorSymbols,
    defaultDisabledFeatureSymbols);
```

Explicit forced-disabled features remain additive, so an explicit disable still wins over an enabled feature.

- [ ] **Step 6: Run the focused editor tests and verify GREEN**

Run the Step 2 command. Expected: both runtime-profiler build-graph tests PASS.

- [ ] **Step 7: Commit the shared build-selection change**

```powershell
rtk.exe git add -- engine/helengine.baseplatform/Definitions/PlatformCodegenSettingIds.cs engine/helengine.editor/managers/project/EditorPlatformPreprocessorSymbolService.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk.exe git commit -m "feat: make generated runtime profiling opt in"
```

### Task 2: Exclude profiler runtime code when the feature is disabled

**Files:**
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.core/diagnostics/RuntimePhysicsProfilerMetrics.cs`
- Modify: `engine/helengine.core/diagnostics/RuntimeProfilerMetrics.cs`
- Modify: `engine/helengine.core/diagnostics/RuntimeProfilerMetricsSnapshot.cs`
- Modify: `engine/helengine.core/physics/IPhysicsRuntimeProfilerMetricsProvider.cs`
- Modify: `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3DCompatibilityRuntime.cs`
- Create: `engine/helengine.physics3d.tests/RuntimeProfilerBuildFeatureSourceTests.cs`

- [ ] **Step 1: Write the failing source-boundary test**

Create a test class that loads the files listed above and asserts that profiler-only files are enclosed by `#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER`, that `Core.cs` guards every `RuntimeProfilerMetricsValue` use, and that each physics provider conditionally implements and conditionally defines `TryGetRuntimeProfilerMetrics`.

```csharp
[Fact]
public void RuntimeProfiler_WhenGeneratedFeatureIsDisabled_IsExcludedFromCoreAndPhysicsProviders() {
    AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimeProfilerMetrics.cs");
    AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimeProfilerMetricsSnapshot.cs");
    AssertProfilerFileIsFeatureGuarded("diagnostics", "RuntimePhysicsProfilerMetrics.cs");
    AssertProfilerFileIsFeatureGuarded("physics", "IPhysicsRuntimeProfilerMetricsProvider.cs");
    Assert.Contains("#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER", LoadCoreSource());
}
```

Use documented class methods for path resolution and source loading; do not create local helper functions.

- [ ] **Step 2: Run the focused source test and verify RED**

Run:

```powershell
rtk.exe dotnet test engine/helengine.physics3d.tests/helengine.physics3d.tests.csproj --filter "FullyQualifiedName~RuntimeProfilerBuildFeatureSourceTests" --no-restore
```

Expected: FAIL because profiler code is currently unconditional.

- [ ] **Step 3: Guard profiler-only data contracts**

Wrap each profiler-only file with:

```csharp
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
// Existing namespace and type.
#endif
```

Managed editor and test builds continue including these types because they do not define the generated-runtime disable symbol.

- [ ] **Step 4: Guard core-owned profiler state and calls**

Wrap the profiler field, constructor initialization, public snapshot property, rendering report method, frame reset, fixed-update metric writes, and physics metric query in the same preprocessor condition.

Preserve scene commits independently of profiler collection:

```csharp
int committedOperationCount = SceneManager.CommitPendingOperationsAtFrameBoundary();
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
RuntimeProfilerMetricsValue.AddSceneOperationCount(committedOperationCount);
#endif
```

Normal generated builds must still commit pending scene operations exactly once.

- [ ] **Step 5: Guard physics-provider interfaces and methods**

Conditionally append `IPhysicsRuntimeProfilerMetricsProvider` to each physics class declaration and wrap each `TryGetRuntimeProfilerMetrics` method:

```csharp
public sealed class BepuPhysicsWorld3D : ISceneBindablePhysicsRuntime, IPhysicsBodySynchronizationRuntime3D, IPhysicsTriggerEventRuntime3D
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
    , IPhysicsRuntimeProfilerMetricsProvider
#endif
{
```

```csharp
#if !HELENGINE_CODEGEN_FEATURE_DISABLED_RUNTIME_PROFILER
public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics) {
    metrics = new RuntimePhysicsProfilerMetrics(RegisteredBodyCount);
    return true;
}
#endif
```

Apply the same boundary to `PhysicsWorld3D` and `PhysicsWorld3DCompatibilityRuntime` without altering their simulation behavior.

- [ ] **Step 6: Run focused profiler tests and verify GREEN**

Run:

```powershell
rtk.exe dotnet test engine/helengine.physics3d.tests/helengine.physics3d.tests.csproj --filter "FullyQualifiedName~RuntimeProfiler" --no-restore
```

Expected: existing profiler behavior tests and the new source-boundary test PASS.

- [ ] **Step 7: Commit the runtime exclusion boundary**

Stage only the listed profiler files and the new test, inspect `git diff --cached --name-only`, then commit:

```powershell
rtk.exe git commit -m "perf: exclude profiler from normal generated runtimes"
```

### Task 3: Add explicit PS2 Default and Profiling profiles

**Files:**
- Modify: `builder/Ps2PlatformDefinitionFactory.cs`
- Modify: `builder.tests/Ps2PlatformAssetBuilderTests.cs`

- [ ] **Step 1: Write failing PS2 profile metadata tests**

Extend the platform-definition test to assert:

```csharp
PlatformBuildProfileDefinition defaultBuild = Assert.Single(
    builder.Definition.BuildProfiles,
    profile => profile.ProfileId == "ps2-default");
PlatformBuildProfileDefinition profilingBuild = Assert.Single(
    builder.Definition.BuildProfiles,
    profile => profile.ProfileId == "ps2-profiling");
Assert.Equal("default", defaultBuild.DefaultCodegenProfileId);
Assert.Equal("profiling", profilingBuild.DefaultCodegenProfileId);

PlatformCodegenProfileDefinition profilingCodegen = Assert.Single(
    builder.Definition.CodegenProfiles,
    profile => profile.ProfileId == "profiling");
Assert.Contains(
    profilingCodegen.Settings,
    setting => setting.SettingId == PlatformCodegenSettingIds.EnabledFeatures
        && setting.DefaultValue == "runtime_profiler");
```

- [ ] **Step 2: Run the focused PS2 metadata test and verify RED**

Run:

```powershell
rtk.exe dotnet test builder.tests/helengine.ps2.builder.tests.csproj --filter "FullyQualifiedName~Descriptor_and_definition_return_ps2_metadata" --no-restore
```

Expected: FAIL because `ps2-profiling` and the profiling codegen profile do not exist.

- [ ] **Step 3: Add PS2 profiling metadata**

Keep `ps2-default` unchanged. Add `ps2-profiling` with the same graphics/build settings and select a new `profiling` codegen profile. Factor the duplicated codegen settings into a documented static factory method and append this required setting only for profiling:

```csharp
new PlatformSettingDefinition(
    PlatformCodegenSettingIds.EnabledFeatures,
    "Enabled Runtime Features",
    PlatformSettingKind.Text,
    "runtime_profiler",
    true,
    [])
```

- [ ] **Step 4: Run the focused PS2 metadata test and verify GREEN**

Run the Step 2 command. Expected: PASS.

- [ ] **Step 5: Commit the PS2 profile metadata**

```powershell
rtk.exe git add -- builder/Ps2PlatformDefinitionFactory.cs builder.tests/Ps2PlatformAssetBuilderTests.cs
rtk.exe git commit -m "feat: add explicit PS2 profiling build"
```

### Task 4: Prove normal PS2 output omits profiler execution and retest Level 01

**Files:**
- Modify: `builder.tests/Ps2StartupManifestSourceTests.cs`
- Modify: `builder.tests/Ps2VuNearPlaneClippingSourceTests.cs`
- Modify: `src/platform/ps2/Ps2BootHost.cpp`
- Output: `C:/dev/helworks/builds/demodisc/ps2/B301-full-demodisc-profiler-off/game.iso`

- [ ] **Step 1: Advance the visible build marker test-first**

Change focused marker expectations from B300 to B301, run them to verify RED, then set:

```cpp
constexpr const char* FrameTimingOverlayBuildNumber = "B301";
```

- [ ] **Step 2: Run the smallest combined managed validation**

Run the focused editor build-graph tests, physics profiler tests, PS2 platform metadata test, PS2 ownership tests, and B301 marker tests. Expected: all PASS with zero build errors.

- [ ] **Step 3: Build DemoDisc with PS2 Default through the build waiter**

Use the repository's build waiter and the existing editor CLI, selecting `ps2-default`. Write all outputs and logs beneath:

```text
C:\dev\helworks\builds\demodisc\ps2\B301-full-demodisc-profiler-off
```

Do not use `%TEMP%` for any agent-owned output and do not impose a wall-clock timeout; wait for deterministic process exit and artifact checks.

Expected: build exits 0 and `game.iso` exists with a post-invocation timestamp.

- [ ] **Step 4: Verify generated Default code contains no profiler update path**

Inspect the fresh generated `Core.cpp` and generated physics providers. Expected:

```text
RuntimeProfilerMetricsValue: no matches
TryGetRuntimeProfilerMetrics: no matches in generated physics providers
```

- [ ] **Step 5: Launch the exact B301 ISO through the repository launcher**

Run only `scripts/launch_in_emulator.ps1` against the exact artifact path. Attach HelenUI to the returned PCSX2 PID; do not inspect screenshots manually.

- [ ] **Step 6: Navigate to Tilt Play Scene 01 and verify runtime behavior**

Use HelenUI input/OCR to open Level 01. Verify:

- overlay reports B301;
- loading transition completes;
- FPS is numeric rather than N/A;
- `ps2_bootlog.txt` has no `Failed to allocate PS2 VU packet` or `std::bad_alloc`;
- power-of-two memory samples no longer increase by 128 bytes per update.

- [ ] **Step 7: Run diff checks and commit the PS2 marker/test changes**

Run `git diff --check` in both repositories, inspect scoped status, and commit only the B301 marker/test files and any remaining task-owned files. Do not stage unrelated dirty-worktree changes.

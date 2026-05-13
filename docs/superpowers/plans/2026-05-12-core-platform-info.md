# Core Platform Info Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add required runtime `PlatformInfo` to `Core`, stamp stable platform id and builder version into packaged startup metadata, and expose those values to running players through `Core.Instance.PlatformInfo`.

**Architecture:** Add one immutable runtime `PlatformInfo` type and require it during `Core.Initialize(...)` / `EditorCore.Initialize(...)`. Reuse the existing runtime startup manifest/codegen path to carry the stable platform id and builder version from the selected builder into the packaged player bootstrap, then update direct core callers in tests/editor code to pass a shared test platform value.

**Tech Stack:** C#, xUnit, native runtime startup manifest generation, Windows packaged player bootstrap, existing editor build graph.

---

### Task 1: Add Runtime PlatformInfo And Core Injection Coverage

**Files:**
- Create: `engine/helengine.core/PlatformInfo.cs`
- Modify: `engine/helengine.core/Core.cs`
- Modify: `engine/helengine.editor/EditorCore.cs`
- Modify: `engine/helengine.editor.tests/CoreTimingTests.cs`

- [ ] **Step 1: Write failing `Core` injection tests**

Add these tests to `engine/helengine.editor.tests/CoreTimingTests.cs` near the existing core-initialization coverage:

```csharp
[Fact]
public void Initialize_WhenPlatformInfoIsProvided_StoresItOnCore() {
    Core core = new Core();
    PlatformInfo platformInfo = new PlatformInfo("windows", "42");

    core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new CoreInitializationOptions(), platformInfo);

    Assert.Same(platformInfo, core.PlatformInfo);
    Assert.Equal("windows", core.PlatformInfo.Name);
    Assert.Equal("42", core.PlatformInfo.Version);
}

[Fact]
public void Initialize_WhenPlatformInfoIsMissing_ThrowsArgumentNullException() {
    Core core = new Core();

    Assert.Throws<ArgumentNullException>(() =>
        core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new CoreInitializationOptions(), null));
}
```

Also add one constructor guard test:

```csharp
[Fact]
public void PlatformInfo_WhenVersionIsBlank_ThrowsArgumentException() {
    Assert.Throws<ArgumentException>(() => new PlatformInfo("windows", string.Empty));
}
```

- [ ] **Step 2: Run the focused core timing tests to verify they fail**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CoreTimingTests
```

Expected: FAIL because `PlatformInfo` does not exist yet and `Core.Initialize(...)` does not accept it.

- [ ] **Step 3: Add the immutable runtime `PlatformInfo` type**

Create `engine/helengine.core/PlatformInfo.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Describes the packaged runtime platform identity and builder-stamped version visible to game code.
    /// </summary>
    public class PlatformInfo {
        /// <summary>
        /// Initializes one immutable runtime platform info record.
        /// </summary>
        /// <param name="name">Stable platform identifier such as windows or psp.</param>
        /// <param name="version">Builder-stamped version string embedded into the packaged runtime.</param>
        public PlatformInfo(string name, string version) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Platform name is required.", nameof(name));
            } else if (string.IsNullOrWhiteSpace(version)) {
                throw new ArgumentException("Platform version is required.", nameof(version));
            }

            Name = name;
            Version = version;
        }

        /// <summary>
        /// Gets the stable platform identifier embedded into the runtime.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the builder-stamped version string embedded into the runtime.
        /// </summary>
        public string Version { get; }
    }
}
```

- [ ] **Step 4: Change `Core` and `EditorCore` to require `PlatformInfo`**

Update `engine/helengine.core/Core.cs`:

```csharp
/// <summary>
/// Gets the packaged runtime platform identity and builder-stamped version.
/// </summary>
public PlatformInfo PlatformInfo { get; private set; }
```

Change both `Initialize(...)` overloads to require `PlatformInfo`:

```csharp
public virtual void Initialize(RenderManager3D render3D, RenderManager2D render2D, IInputBackend input, PlatformInfo platformInfo) {
    Initialize(render3D, render2D, input, InitializationOptions, platformInfo);
}

public virtual void Initialize(
    RenderManager3D render3D,
    RenderManager2D render2D,
    IInputBackend input,
    CoreInitializationOptions options,
    PlatformInfo platformInfo) {
    RenderManager3D = render3D;
    RenderManager2D = render2D;
    Input.SetBackend(input);

    if (options == null) {
        throw new ArgumentNullException(nameof(options));
    } else if (platformInfo == null) {
        throw new ArgumentNullException(nameof(platformInfo));
    }

    PlatformInfo = platformInfo;
    options.Normalize();
    InitializationOptions = options;
    PhysicsSchedulerValue = CreatePhysicsScheduler(options);
    ...
}
```

Update `engine/helengine.editor/EditorCore.cs`:

```csharp
public override void Initialize(
    RenderManager3D render3D,
    RenderManager2D render2D,
    IInputBackend input,
    CoreInitializationOptions options,
    PlatformInfo platformInfo) {
    base.Initialize(render3D, render2D, input, options, platformInfo);
    EditorObjectManager = new ObjectManager(InitializationOptions);
}
```

- [ ] **Step 5: Run the focused core timing tests to verify they pass**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~CoreTimingTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
rtk git add engine/helengine.core/PlatformInfo.cs engine/helengine.core/Core.cs engine/helengine.editor/EditorCore.cs engine/helengine.editor.tests/CoreTimingTests.cs
rtk git commit -m "Add runtime platform info to core"
```

### Task 2: Stamp PlatformInfo Through Runtime Startup Metadata

**Files:**
- Modify: `engine/helengine.core/content/RuntimeStartupManifest.cs`
- Modify: `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformLayoutPlanService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs`
- Modify: `engine/helengine.editor.tests/RuntimeStartupManifestTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformLayoutPlanServiceTests.cs`

- [ ] **Step 1: Write failing startup-manifest and native-writer tests**

Extend `engine/helengine.editor.tests/RuntimeStartupManifestTests.cs`:

```csharp
[Fact]
public void Constructor_preserves_platform_info_metadata() {
    RuntimeStartupManifest manifest = new RuntimeStartupManifest(
        "Scenes/MainMenu.helen",
        new RuntimeStorageProfileId("windows-loose-files"),
        "windows",
        "42");

    Assert.Equal("windows", manifest.PlatformName);
    Assert.Equal("42", manifest.PlatformVersion);
}
```

Extend `engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs`:

```csharp
Assert.Contains("he_get_runtime_platform_name", startupHeaderSource);
Assert.Contains("he_get_runtime_platform_version", startupHeaderSource);
Assert.Contains("static const char kRuntimePlatformName[] = \"windows\";", startupSource);
Assert.Contains("static const char kRuntimePlatformVersion[] = \"42\";", startupSource);
```

Update the test manifest construction to include platform id/version in the cooked manifest payload:

```csharp
PlatformBuildManifest manifest = new(
    1,
    "project",
    "1.0.0",
    "1.0.0",
    "windows",
    "42",
    "NewScene",
    ...);
```

- [ ] **Step 2: Run the startup-manifest slice to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeStartupManifestTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~EditorPlatformLayoutPlanServiceTests"
```

Expected: FAIL because the manifest and writer do not carry platform info yet.

- [ ] **Step 3: Add platform fields to `RuntimeStartupManifest` and `PlatformBuildManifest`**

Update `engine/helengine.core/content/RuntimeStartupManifest.cs`:

```csharp
public RuntimeStartupManifest(
    string startupSceneId,
    RuntimeStorageProfileId storageProfileId,
    string platformName,
    string platformVersion) {
    if (string.IsNullOrWhiteSpace(startupSceneId)) {
        throw new ArgumentException("Startup scene id is required.", nameof(startupSceneId));
    }
    if (storageProfileId == null) {
        throw new ArgumentNullException(nameof(storageProfileId));
    }
    if (string.IsNullOrWhiteSpace(platformName)) {
        throw new ArgumentException("Platform name is required.", nameof(platformName));
    }
    if (string.IsNullOrWhiteSpace(platformVersion)) {
        throw new ArgumentException("Platform version is required.", nameof(platformVersion));
    }

    StartupSceneId = startupSceneId;
    StorageProfileId = storageProfileId;
    PlatformName = platformName;
    PlatformVersion = platformVersion;
}

public string PlatformName { get; }
public string PlatformVersion { get; }
```

Update `engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs`:

```csharp
public PlatformBuildManifest(
    int manifestVersion,
    string projectId,
    string projectVersion,
    string requiredEngineVersion,
    string platformName,
    string platformVersion,
    string startupSceneId,
    ...) {
    ...
    if (string.IsNullOrWhiteSpace(platformName)) {
        throw new ArgumentException("Platform name is required.", nameof(platformName));
    } else if (string.IsNullOrWhiteSpace(platformVersion)) {
        throw new ArgumentException("Platform version is required.", nameof(platformVersion));
    }

    PlatformName = platformName;
    PlatformVersion = platformVersion;
    ...
}

public string PlatformName { get; }
public string PlatformVersion { get; }
```

Keep `PlatformName` as the stable platform id from the selected builder.

- [ ] **Step 4: Stamp the selected builder values into cooked manifests**

Update `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs` wherever it constructs or clones `PlatformBuildManifest`:

```csharp
return new PlatformBuildManifest(
    cookedManifest.ManifestVersion,
    cookedManifest.ProjectId,
    cookedManifest.ProjectVersion,
    cookedManifest.RequiredEngineVersion,
    PlatformDescriptor.Id,
    builder.Descriptor.BuilderVersion,
    cookedManifest.StartupSceneId,
    ...);
```

Do the same in:

- `EditorPlatformLayoutPlanService.cs`
- `EditorPlatformAssetCookService.cs`
- `ReplaceCodeModules(...)`
- any manifest-clone paths in `BuildRequest(...)`

Every `new PlatformBuildManifest(...)` call must forward `PlatformName` and `PlatformVersion` explicitly so the metadata is never dropped.

- [ ] **Step 5: Extend the native startup manifest writer to emit platform info accessors**

Update `engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs` header generation:

```csharp
builder.AppendLine("const char* he_get_runtime_startup_scene_relative_path();");
builder.AppendLine("const char* he_get_runtime_platform_name();");
builder.AppendLine("const char* he_get_runtime_platform_version();");
```

Update source generation:

```csharp
static string BuildStartupManifestSourceContents(
    string startupSceneRelativePath,
    string platformName,
    string platformVersion) {
    StringBuilder builder = new();
    builder.AppendLine("#include \"runtime/runtime_startup_manifest.hpp\"");
    builder.AppendLine();
    builder.AppendLine("static const char kRuntimeStartupSceneRelativePath[] = \"" + EscapeCppStringLiteral(startupSceneRelativePath) + "\";");
    builder.AppendLine("static const char kRuntimePlatformName[] = \"" + EscapeCppStringLiteral(platformName) + "\";");
    builder.AppendLine("static const char kRuntimePlatformVersion[] = \"" + EscapeCppStringLiteral(platformVersion) + "\";");
    builder.AppendLine();
    builder.AppendLine("const char* he_get_runtime_startup_scene_relative_path() {");
    builder.AppendLine("    return kRuntimeStartupSceneRelativePath;");
    builder.AppendLine("}");
    builder.AppendLine();
    builder.AppendLine("const char* he_get_runtime_platform_name() {");
    builder.AppendLine("    return kRuntimePlatformName;");
    builder.AppendLine("}");
    builder.AppendLine();
    builder.AppendLine("const char* he_get_runtime_platform_version() {");
    builder.AppendLine("    return kRuntimePlatformVersion;");
    builder.AppendLine("}");
    return builder.ToString();
}
```

Update `WriteStartupManifestSource(...)` to pass `cookedManifest.PlatformName` and `cookedManifest.PlatformVersion`.

- [ ] **Step 6: Run the startup-manifest slice to verify it passes**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~RuntimeStartupManifestTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~EditorPlatformLayoutPlanServiceTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.core/content/RuntimeStartupManifest.cs engine/helengine.baseplatform/Manifest/PlatformBuildManifest.cs engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs engine/helengine.editor/managers/project/EditorPlatformLayoutPlanService.cs engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs engine/helengine.editor/managers/project/EditorRuntimeNativeManifestWriter.cs engine/helengine.editor.tests/RuntimeStartupManifestTests.cs engine/helengine.editor.tests/managers/project/EditorRuntimeNativeManifestWriterTests.cs engine/helengine.editor.tests/managers/project/EditorPlatformLayoutPlanServiceTests.cs
rtk git commit -m "Stamp platform info into runtime startup manifest"
```

### Task 3: Inject PlatformInfo Into Packaged Runtime Bootstrap

**Files:**
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp`
- Modify: `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.hpp`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] **Step 1: Write failing packaged-runtime assertions**

Add one focused assertion in `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs` after the packaged player launches and validates startup content:

```csharp
Assert.Contains("platform=windows", startupLog, StringComparison.OrdinalIgnoreCase);
Assert.Contains("version=42", startupLog, StringComparison.OrdinalIgnoreCase);
```

Use the existing build-runner harness’ generated startup log or runtime output file path rather than inventing a new probe channel.

- [ ] **Step 2: Run the build-graph test slice to verify it fails**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithClipCommandsInGeneratedCore_Succeeds|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithRoundedRectSdfParity_Succeeds"
```

Expected: FAIL because the runtime bootstrap does not yet construct or log `PlatformInfo`.

- [ ] **Step 3: Construct and inject `PlatformInfo` in Windows bootstrap**

Update `C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp` inside `InitializeEngineCore()`:

```cpp
        const char* runtimePlatformName = he_get_runtime_platform_name();
        const char* runtimePlatformVersion = he_get_runtime_platform_version();
        if (runtimePlatformName == nullptr || runtimePlatformName[0] == '\0') {
            throw std::runtime_error("Runtime platform name was not embedded into the startup manifest.");
        }
        if (runtimePlatformVersion == nullptr || runtimePlatformVersion[0] == '\0') {
            throw std::runtime_error("Runtime platform version was not embedded into the startup manifest.");
        }

        PlatformInfo* platformInfo = new PlatformInfo(
            std::string(runtimePlatformName),
            std::string(runtimePlatformVersion));

        EngineCore->Initialize(EngineRenderManager3D, EngineRenderManager2D, EngineInputBackend, options, platformInfo);

        {
            std::ostringstream messageBuilder;
            messageBuilder << "Runtime platform info initialized: platform="
                << runtimePlatformName
                << " version="
                << runtimePlatformVersion
                << '.';
            std::string message = messageBuilder.str();
            WriteLifecycleLog(message.c_str());
        }
```

Add the forward declaration in `win32_application.hpp`:

```cpp
class PlatformInfo;
```

Do not silently substitute fallback values here; fail fast if the generated manifest is missing the stamped strings.

- [ ] **Step 4: Run the build-graph slice to verify it passes**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithClipCommandsInGeneratedCore_Succeeds|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests.Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithRoundedRectSdfParity_Succeeds"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
rtk git add C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.cpp C:\dev\helworks\helengine-windows\src\platform\windows\win32\win32_application.hpp engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs
rtk git commit -m "Inject platform info into packaged runtime"
```

### Task 4: Update Shared Test And Editor Core Callers To Pass PlatformInfo

**Files:**
- Create: `engine/helengine.editor.tests/testing/TestPlatformInfo.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.render.validation/RenderValidationRunner.cs`
- Modify: `engine/helengine.physics3d.tests/*.cs`
- Modify: `engine/helengine.editor.tests/**/*.cs`

- [ ] **Step 1: Add a shared test helper for platform info**

Create `engine/helengine.editor.tests/testing/TestPlatformInfo.cs`:

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides one shared runtime platform info value for editor and runtime unit tests.
    /// </summary>
    internal static class TestPlatformInfo {
        /// <summary>
        /// Gets the default test platform info used by core initialization helpers.
        /// </summary>
        public static PlatformInfo Default { get; } = new PlatformInfo("windows", "1");
    }
}
```

- [ ] **Step 2: Update editor/runtime harness entrypoints first**

Update `engine/helengine.editor/EditorSession.cs`:

```csharp
core.Initialize(render3D, render2D, input, new PlatformInfo("windows", "editor"));
```

Update `engine/helengine.render.validation/RenderValidationRunner.cs`:

```csharp
core.Initialize(renderer3D, renderer2D, inputManager, new CoreInitializationOptions(), new PlatformInfo("windows", "validation"));
```

These two callers should use explicit local values instead of the test helper because they are production/editor code, not test infrastructure.

- [ ] **Step 3: Update direct test callers to use the shared helper**

Change direct core initialization calls across `engine/helengine.editor.tests` and `engine/helengine.physics3d.tests` from:

```csharp
core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
```

to:

```csharp
core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, TestPlatformInfo.Default);
```

and from:

```csharp
core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), resolvedOptions);
```

to:

```csharp
core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), resolvedOptions, TestPlatformInfo.Default);
```

Apply the same pattern for `EditorCore.Initialize(...)`.

Use `rg -n "core.Initialize\\(|Core.Initialize\\(" engine/helengine.editor.tests engine/helengine.physics3d.tests` to find the full call surface and update them all consistently.

- [ ] **Step 4: Run the narrow regression bundle**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CoreTimingTests|FullyQualifiedName~RuntimeStartupManifestTests|FullyQualifiedName~EditorRuntimeNativeManifestWriterTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests|FullyQualifiedName~RenderingSceneCatalogTests"
rtk dotnet test .\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Run full verification**

Run:

```powershell
rtk dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj
rtk dotnet test .\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj
```

Expected: PASS.

- [ ] **Step 6: Review for repository conventions**

Check the changed files for:

- substantive XML comments on all new or changed classes, constructors, properties, and methods
- one class per new file
- no tuples
- no local helper functions
- no silent fallback `PlatformInfo`

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.editor.tests/testing/TestPlatformInfo.cs engine/helengine.editor/EditorSession.cs engine/helengine.render.validation/RenderValidationRunner.cs engine/helengine.editor.tests engine/helengine.physics3d.tests
rtk git commit -m "Require platform info across core callers"
```

# Graphics Profile Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make per-platform graphics profiles drive Windows player startup, build output selection, and runtime presentation defaults.

**Architecture:** Persist a concrete `ShaderCompileTarget` with each platform graphics profile, snapshot that profile into queued builds, and stage a tiny runtime graphics manifest into the Windows output tree. The native Windows host will load that manifest before creating the window and render loop, apply width/height/fullscreen/vsync from it, and reject unsupported runtime backends instead of inventing defaults. Windows remains DirectX11-only for the player host today, so anything else must fail clearly until a real Vulkan host exists.

**Tech Stack:** C#, xUnit, System.Text.Json, Win32, DirectX11, C++17.

---

## File Map

### Graphics profile data model and editor persistence
- Modify: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

### Build queue snapshot and Windows staging
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestService.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`

### Windows host manifest consumption
- Create: `helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.hpp`
- Create: `helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/directx11/directx11_presenter.hpp`
- Modify: `helengine-windows/src/platform/windows/directx11/directx11_presenter.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_window.hpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_window.cpp`

## Task 1: Persist the runtime target in each graphics profile and expose it in the dialog

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one persistence test and one dialog test that prove the runtime backend target survives a round trip and is editable in the Graphics tab:

```csharp
[Fact]
public void SaveAndReload_PreservesRuntimeTargetPerPlatform() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
    EditorProfileSettingsDocument document = service.Load(["windows"]);
    document.Platforms[0].Graphics.RuntimeTarget = ShaderCompileTarget.Vulkan;

    service.Save(document);

    EditorProfileSettingsDocument reloaded = service.Load(["windows"]);
    Assert.Equal(ShaderCompileTarget.Vulkan, reloaded.Platforms[0].Graphics.RuntimeTarget);
}

[Fact]
public void Show_WhenGraphicsTabChanges_RuntimeTargetComboBoxUpdatesTheDraftDocument() {
    ProfilesDialog dialog = new ProfilesDialog(CreateFont());
    EditorProfileSettingsDocument document = CreateProfileDocument();

    dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

    ComboBoxComponent runtimeTargetComboBox = GetPrivateField<ComboBoxComponent>(dialog, "RuntimeTargetComboBox");
    runtimeTargetComboBox.SelectedItem = ShaderCompileTarget.Vulkan;

    ProfilesDialogSelection selection = null;
    dialog.ConfirmRequested += value => selection = value;
    InvokePrivate(dialog, "HandleSaveClicked");

    Assert.NotNull(selection);
    Assert.Equal(ShaderCompileTarget.Vulkan, selection.ProfileSettingsDocument.Platforms[0].Graphics.RuntimeTarget);
    Assert.Equal(ShaderCompileTarget.DirectX11, document.Platforms[0].Graphics.RuntimeTarget);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProfileSettingsServiceTests|FullyQualifiedName~ProfilesDialogTests" -v minimal
```

Expected: the tests fail because `EditorGraphicsProfileSettingsDocument` does not yet carry a runtime target and the Graphics tab cannot stage it.

- [ ] **Step 3: Write the minimal implementation**

Add the runtime target property and bind it in the dialog:

```csharp
/// <summary>
/// Gets or sets the concrete runtime shader backend used by this platform profile.
/// </summary>
public ShaderCompileTarget RuntimeTarget { get; set; } = ShaderCompileTarget.DirectX11;
```

In `ProfilesDialog`, add a graphics-tab combo box that loads and stores `RuntimeTarget` alongside width, height, fullscreen, and vsync. In `EditorProfileSettingsService`, normalize malformed values to `ShaderCompileTarget.DirectX11` when loading the JSON document so the editor never persists an invalid backend silently.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the persistence and dialog tests pass, proving the runtime target is part of the saved per-platform graphics profile.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs engine/helengine.editor/managers/project/EditorProfileSettingsService.cs engine/helengine.editor/components/ui/ProfilesDialog.cs engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs engine/helengine.editor.tests/ProfilesDialogTests.cs
git commit -m "Persist graphics runtime target in profiles"
```

## Task 2: Snapshot graphics profiles into builds and stage a runtime manifest

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestService.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one queue snapshot test and one executor staging test that prove the graphics target is copied into the queued build and written into the staged manifest path:

```csharp
[Fact]
public void QueueBuild_WhenCurrentPlatformHasGraphicsProfile_SnapshotsRuntimeTargetIntoQueueItem() {
    EditorProfileSettingsService profileService = new EditorProfileSettingsService(TempProjectRootPath);
    EditorProfileSettingsDocument profileSettings = profileService.Load(["windows"]);
    profileSettings.Platforms[0].Graphics.RuntimeTarget = ShaderCompileTarget.Vulkan;
    profileService.Save(profileSettings);

    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
    EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");

    InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest("windows", [
        CurrentSceneId
    ], @"C:\builds\windows"));

    EditorBuildConfigDocument persistedDocument = buildConfigService.Load([
        "windows"
    ], CurrentSceneId);
    EditorBuildQueueItemDocument queueItem = Assert.Single(persistedDocument.QueueItems);
    Assert.Equal(ShaderCompileTarget.Vulkan, queueItem.GraphicsTarget);
}

[Fact]
public void Execute_WhenGraphicsTargetIsDirectX11_WritesRuntimeGraphicsManifestUnderConfigAndTargetFolder() {
    EditorWindowsBuildExecutor executor = CreateExecutor();
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        PlatformId = "windows",
        SelectedSceneIds = [ "NewScene.helen" ],
        OutputDirectoryPath = BuildRootPath,
        DebugBuild = true,
        GraphicsTarget = ShaderCompileTarget.DirectX11
    };

    EditorBuildExecutionResult result = executor.Execute(queueItem);

    Assert.True(result.IsSuccess);
    Assert.True(File.Exists(Path.Combine(BuildRootPath, "Build", "Debug", "DirectX11", "graphics.manifest")));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorWindowsBuildExecutorTests" -v minimal
```

Expected: the tests fail because the queue item has no graphics-target snapshot and the executor does not yet stage a runtime graphics manifest or target-specific build root.

- [ ] **Step 3: Write the minimal implementation**

Add a graphics snapshot property to the queue item and thread it from the active platform profile through `EditorSession` into `EditorWindowsBuildExecutor`. Create a tiny runtime-manifest document/service pair and write a simple line-oriented file, not a best-effort fallback:

```csharp
File.WriteAllLines(manifestPath, [
    $"width={document.DefaultWidth}",
    $"height={document.DefaultHeight}",
    $"fullscreen={document.FullscreenEnabled}",
    $"vsync={document.VSyncEnabled}",
    $"target={document.RuntimeTarget}"
]);
```

Stage the manifest under `Build/{Debug|Release}/{ShaderTargetName}/graphics.manifest` so build configuration and runtime backend cannot collide. Pass the queued `GraphicsTarget` into shader export, and fail the build if the selected graphics target is unsupported by the current Windows player path.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the queue snapshot test passes and the executor writes a manifest into the configuration- and target-specific build root.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestDocument.cs engine/helengine.editor/managers/project/EditorGraphicsRuntimeManifestService.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs
git commit -m "Snapshot graphics profiles into build queue"
```

## Task 3: Load the staged graphics manifest in the Windows player and apply it before startup

**Files:**
- Create: `helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.hpp`
- Create: `helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.hpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`
- Modify: `helengine-windows/src/platform/windows/directx11/directx11_presenter.hpp`
- Modify: `helengine-windows/src/platform/windows/directx11/directx11_presenter.cpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_window.hpp`
- Modify: `helengine-windows/src/platform/windows/win32/win32_window.cpp`

- [ ] **Step 1: Write the failing tests or smoke contract**

Add a native smoke contract by asserting the host log sequence through a small manifest-backed run. The manifest parser itself should be strict and fail on missing fields:

```cpp
// windows_graphics_manifest.hpp
class WindowsGraphicsManifest {
public:
    int Width;
    int Height;
    bool FullscreenEnabled;
    bool VSyncEnabled;
    std::string RuntimeTargetName;

    static WindowsGraphicsManifest Load(const std::filesystem::path& manifestPath);
};
```

The run should fail immediately if `graphics.manifest` is missing, malformed, or names a backend other than DirectX11, because the current native player is DirectX11-only.

- [ ] **Step 2: Run the test to verify it fails**

Build the Windows host and run the exported player once with a staged manifest missing. The expected failure is a clear startup exception that points at the missing or unsupported manifest field, not a silent fallback to default window settings.

- [ ] **Step 3: Write the minimal implementation**

Implement a tiny line-oriented parser in `windows_graphics_manifest.cpp`, then wire `Win32Application::Run()` to:
- load the manifest before `CreateMainWindow()`
- create the window using the manifest width and height
- apply fullscreen before entering the loop
- pass `VSyncEnabled` into `DirectX11Presenter`
- reject unsupported `RuntimeTarget` values explicitly

Update `Win32Window` so fullscreen is a real window state instead of an editor assumption, and update `DirectX11Presenter::RenderFrame()` so it uses `Present(0, 0)` when vsync is disabled.

- [ ] **Step 4: Run the test to verify it passes**

Rebuild the Windows player and run the exported exe from the build output. Expected logs:

```text
[Host] Host startup began.
[Host] Loaded graphics manifest.
[Host] Main window loaded and shown.
[Host] DirectX 11 bootstrap initialized.
[Host] Engine core initialized.
```

The window should open at the staged size, fullscreen if requested, and vsync should change the measured frame rate when disabled.

- [ ] **Step 5: Commit**

```bash
git add helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.hpp helengine-windows/src/platform/windows/runtime/windows_graphics_manifest.cpp helengine-windows/src/platform/windows/win32/win32_application.hpp helengine-windows/src/platform/windows/win32/win32_application.cpp helengine-windows/src/platform/windows/directx11/directx11_presenter.hpp helengine-windows/src/platform/windows/directx11/directx11_presenter.cpp helengine-windows/src/platform/windows/win32/win32_window.hpp helengine-windows/src/platform/windows/win32/win32_window.cpp
git commit -m "Load runtime graphics manifest in Windows player"
```

## Task 4: Verify the full graphics-profile flow end to end

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`
- Modify: `helengine-windows/src/platform/windows/win32/win32_application.cpp`

- [ ] **Step 1: Write the final integration checks**

Add explicit assertions for the staged manifest path and the player startup log line that includes the loaded graphics manifest:

```csharp
Assert.Equal(
    Path.Combine(BuildRootPath, "Build", "Debug", "DirectX11", "graphics.manifest"),
    manifestPath);
```

```text
[Host] Loaded graphics manifest.
[Host] Packaged startup scene loaded in ...
```

- [ ] **Step 2: Run the focused editor tests**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProfileSettingsServiceTests|FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorWindowsBuildExecutorTests" -v minimal
```

Expected: pass.

- [ ] **Step 3: Rebuild and run the Windows player**

Run the Windows build from Visual Studio or the existing build queue, then launch the staged exe from `Build/{Debug|Release}/{ShaderTargetName}/helengine_windows.exe`.

Expected: the console shows the graphics-manifest load log and the frame loop respects the staged vsync value instead of always presenting with `Present(1, 0)`.

- [ ] **Step 4: Capture the failure mode for unsupported backends**

Temporarily set one platform graphics profile to `ShaderCompileTarget.Vulkan` and confirm the Windows player build fails clearly before launch with an unsupported-backend message. The plan is not complete if the host silently ignores that value.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs helengine-windows/src/platform/windows/win32/win32_application.cpp
git commit -m "Verify graphics profile startup flow"
```

## Coverage Check

- Per-platform graphics profile persistence and editor editing: Task 1.
- Queue snapshotting and target-specific build roots: Task 2.
- Runtime manifest staging with strict error handling: Task 2.
- Native player consumption before window creation and render-loop start: Task 3.
- Vsync, fullscreen, and resolution application at runtime: Task 3.
- Unsupported backend rejection instead of silent fallback: Tasks 2 and 3.
- End-to-end validation from editor save to player launch: Task 4.

## Self-Review

1. **Spec coverage:** The spec requires per-platform graphics defaults, a build-time manifest handoff, runtime startup consumption, and clear errors for missing or unsupported data. Tasks 1 through 4 cover those requirements directly.
2. **Placeholder scan:** The plan uses concrete files, concrete commands, and concrete assertions. There are no TBDs or vague “add validation” steps.
3. **Type consistency:** The plan uses `ShaderCompileTarget` consistently in the graphics profile, queue item, manifest document, and runtime parser. The build output uses `Build/{Debug|Release}/{ShaderTargetName}` everywhere the staged target matters.

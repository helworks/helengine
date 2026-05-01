# Per-Platform Debug Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist a per-platform `Debug build` flag, expose it in the Build modal, snapshot it into queued builds, and make the Windows player build Debug or Release from that snapshot.

**Architecture:** Store the default build mode on each platform’s persisted build-config record, not in the platform catalog. The Build modal edits the active platform’s local default, queue items snapshot that value at creation time, and the Windows executor reads only the queue snapshot when selecting the native build configuration. Keep the user-selected output root unchanged, but stage Debug and Release into separate subfolders under it so artifacts never collide.

**Tech Stack:** C#, xUnit, existing editor build-config persistence, existing Windows build executor, CMake/MSBuild native build invocation.

---

### Task 1: Persist `DebugBuild` on the per-platform config documents

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Test: `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add two focused tests to `EditorBuildConfigServiceTests`:

```csharp
[Fact]
public void Load_WhenPlatformConfigContainsDebugBuild_PreservesItAfterSaveAndReload() {
    EditorBuildConfigService service = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildConfigDocument document = new EditorBuildConfigDocument {
        Platforms = new List<EditorBuildPlatformConfigDocument> {
            new EditorBuildPlatformConfigDocument {
                PlatformId = "windows",
                OutputDirectoryPath = @"C:\builds\windows",
                DebugBuild = true
            }
        }
    };

    service.Save(document);

    EditorBuildConfigDocument reloaded = service.Load(new[] { "windows" }, "scene-a");
    Assert.True(reloaded.Platforms[0].DebugBuild);
}

[Fact]
public void Load_WhenDebugBuildFieldIsMissing_DefaultsToFalse() {
    EditorBuildConfigService service = new EditorBuildConfigService(TempProjectRootPath);
    string buildConfigFilePath = Path.Combine(TempProjectRootPath, "user_settings", "build_config.json");
    Directory.CreateDirectory(Path.GetDirectoryName(buildConfigFilePath));
    File.WriteAllText(buildConfigFilePath, """
    {
      "platforms": [
        {
          "platformId": "windows",
          "selectedSceneIds": ["scene-a"],
          "sceneOrders": [],
          "outputDirectoryPath": "C:\\builds\\windows"
        }
      ],
      "queueItems": []
    }
    """);

    EditorBuildConfigDocument reloaded = service.Load(new[] { "windows" }, "scene-a");
    Assert.False(reloaded.Platforms[0].DebugBuild);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal
```

Expected: the new assertions fail because `DebugBuild` is not yet persisted or normalized.

- [ ] **Step 3: Write the minimal implementation**

Add the new persisted field and seed it during load normalization:

```csharp
public bool DebugBuild { get; set; }
```

and seed new platform entries with:

```csharp
DebugBuild = false
```

`EditorBuildConfigService.TryLoadDocument()` does not need a special migration branch for old JSON because the CLR default for a missing `bool` property is already `false`; the important part is that `CreatePlatformDocument()` explicitly seeds the value for newly added platforms so the saved JSON is stable and intentional.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: both new tests pass and the existing config tests stay green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs
git commit -m "Persist per-platform debug build flag"
```

### Task 2: Add the `Debug build` control to the Build modal

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/model/BuildDialogAddRequest.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Write the failing test**

Add one test that proves the active platform restores its own checkbox state and that Add to Build snapshots the current value:

```csharp
[Fact]
public void BuildDialog_WhenSwitchingPlatforms_RestoresEachPlatformsDebugBuildValue() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    dialog.Show(
        ["windows", "linux"],
        [
            "Scenes/City.helen",
            "Scenes/Menu.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen"],
                    OutputDirectoryPath = @"C:\builds\windows",
                    DebugBuild = true
                },
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "linux",
                    SelectedSceneIds = ["Scenes/Menu.helen"],
                    OutputDirectoryPath = "/tmp/linux-build",
                    DebugBuild = false
                }
            ]
        });

    CheckBoxComponent debugBuildCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "DebugBuildCheckBox");
    Assert.True(debugBuildCheckBox.IsChecked);

    InvokePrivate(dialog, "HandlePlatformTabClicked", "linux");
    Assert.False(debugBuildCheckBox.IsChecked);
}

[Fact]
public void HandleAddToBuildClicked_WhenDebugBuildIsEnabled_SnapshotsItIntoTheRequest() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    BuildDialogAddRequest raisedRequest = null;
    dialog.AddRequested += request => raisedRequest = request;
    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = ["Scenes/City.helen"],
                    OutputDirectoryPath = @"C:\builds\windows",
                    DebugBuild = true
                }
            ]
        });

    InvokePrivate(dialog, "HandleAddToBuildClicked");

    Assert.True(raisedRequest.DebugBuild);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests" -v minimal
```

Expected: the new assertions fail because the dialog and request do not yet carry `DebugBuild`.

- [ ] **Step 3: Write the minimal implementation**

Add a `DebugBuild` property to `BuildDialogAddRequest`, add a checkbox to `BuildDialog`, and sync it when the active platform changes and when Add to Build is clicked:

```csharp
public bool DebugBuild { get; }

public BuildDialogAddRequest(
    string platformId,
    IReadOnlyList<string> selectedSceneIds,
    string outputDirectoryPath,
    bool debugBuild) {
    if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id is required.", nameof(platformId));
    }

    if (selectedSceneIds == null) {
        throw new ArgumentNullException(nameof(selectedSceneIds));
    }

    if (outputDirectoryPath == null) {
        throw new ArgumentNullException(nameof(outputDirectoryPath));
    }

    List<string> copiedSceneIds = new List<string>(selectedSceneIds.Count);
    for (int index = 0; index < selectedSceneIds.Count; index++) {
        copiedSceneIds.Add(selectedSceneIds[index]);
    }

    PlatformId = platformId;
    SelectedSceneIds = copiedSceneIds;
    OutputDirectoryPath = outputDirectoryPath;
    DebugBuild = debugBuild;
}
```

and in the dialog, make the active platform config drive the checkbox state before queueing a request:

```csharp
activePlatformConfig.DebugBuild = DebugBuildCheckBox.IsChecked;
AddRequested?.Invoke(new BuildDialogAddRequest(ActivePlatformId, orderedSceneIds, platformConfig.OutputDirectoryPath, platformConfig.DebugBuild));
```

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the dialog tests pass and the existing Build modal tests still pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/model/BuildDialogAddRequest.cs engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "Add debug build toggle to build modal"
```

### Task 3: Snapshot `DebugBuild` into queue items

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueService.cs`
- Test: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Test: `engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs`

- [ ] **Step 1: Write the failing test**

Add one editor-session test that verifies the queue item inherits the current platform’s debug mode, and one queue-service test that confirms the queue item persists that value unchanged:

```csharp
[Fact]
public void HandleBuildDialogAddRequested_WhenDebugBuildIsEnabled_CreatesQueueItemWithSnapshot() {
    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
    EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
    BuildDialogAddRequest request = new BuildDialogAddRequest(
        "windows",
        ["Scenes/City.helen"],
        @"C:\builds\windows",
        true);

    InvokePrivate(session, "HandleBuildDialogAddRequested", request);

    EditorBuildConfigDocument persistedDocument = buildConfigService.Load(["windows"], CurrentSceneId);
    EditorBuildQueueItemDocument queueItem = Assert.Single(persistedDocument.QueueItems);
    Assert.True(queueItem.DebugBuild);
}

[Fact]
public void RunPending_WhenQueueItemHasDebugBuild_PreservesItDuringStatusUpdates() {
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        QueueItemId = "queue-1",
        PlatformId = "windows",
        DebugBuild = true,
        SelectedSceneIds = new List<string> { "scene-a" },
        OutputDirectoryPath = @"C:\builds\windows"
    };

    Assert.True(queueItem.DebugBuild);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorBuildQueueServiceTests" -v minimal
```

Expected: the new snapshot assertion fails until the queue item carries the new flag through the request path.

- [ ] **Step 3: Write the minimal implementation**

Add `DebugBuild` to `EditorBuildQueueItemDocument`, then copy the flag from the active platform config when queue items are created in `EditorSession`:

```csharp
queueItem.DebugBuild = platformConfig.DebugBuild;
```

Keep `EditorBuildQueueService` unchanged unless normalization is needed for old persisted queue items.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: queue items keep the snapshot value and the queue-service tests remain green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorBuildQueueService.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs
git commit -m "Snapshot debug build flag into queued builds"
```

### Task 4: Make the Windows executor honor Debug or Release

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildPaths.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs`

- [ ] **Step 1: Write the failing test**

Add two executor tests: one for Debug and one for Release. The test should assert the executor stages into a configuration-specific subfolder and uses the matching native build configuration:

```csharp
[Fact]
public void ResolveBuildConfigurationName_WhenQueueItemRequestsDebugBuild_ReturnsDebug() {
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        QueueItemId = "queue-debug",
        PlatformId = "windows",
        DebugBuild = true,
        SelectedSceneIds = new List<string> { "scene-a" },
        OutputDirectoryPath = BuildRootPath
    };

    MethodInfo resolveMethod = typeof(EditorWindowsBuildExecutor).GetMethod("ResolveBuildConfigurationName", BindingFlags.Instance | BindingFlags.NonPublic);
    string buildConfiguration = (string)resolveMethod.Invoke(executor, new object[] { queueItem });
    Assert.Equal("Debug", buildConfiguration);
}

[Fact]
public void ResolveBuildConfigurationName_WhenQueueItemRequestsReleaseBuild_ReturnsRelease() {
    EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemDocument {
        QueueItemId = "queue-release",
        PlatformId = "windows",
        DebugBuild = false,
        SelectedSceneIds = new List<string> { "scene-a" },
        OutputDirectoryPath = BuildRootPath
    };

    MethodInfo resolveMethod = typeof(EditorWindowsBuildExecutor).GetMethod("ResolveBuildConfigurationName", BindingFlags.Instance | BindingFlags.NonPublic);
    string buildConfiguration = (string)resolveMethod.Invoke(executor, new object[] { queueItem });
    Assert.Equal("Release", buildConfiguration);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildExecutorTests" -v minimal
```

Expected: the build path/config assertions fail until the executor reads `DebugBuild`.

- [ ] **Step 3: Write the minimal implementation**

Extend the Windows build paths with a configuration-specific subfolder and use the queue snapshot when invoking CMake:

```csharp
public EditorWindowsBuildPaths(string deploymentRootPath, string buildConfigurationName) {
    if (string.IsNullOrWhiteSpace(buildConfigurationName)) {
        throw new ArgumentException("Build configuration name must be provided.", nameof(buildConfigurationName));
    }

    BuildConfigurationName = buildConfigurationName;
    BuildRootPath = Path.Combine(DeploymentRootPath, "Build", BuildConfigurationName);
}
```

Then select the native config from the queue item:

```csharp
string ResolveBuildConfigurationName(EditorBuildQueueItemDocument queueItem) {
    if (queueItem.DebugBuild) {
        return "Debug";
    }

    return "Release";
}

string buildConfiguration = queueItem.DebugBuild ? "Debug" : "Release";
EditorWindowsBuildPaths buildPaths = new EditorWindowsBuildPaths(queueItem.OutputDirectoryPath, buildConfiguration);
RunProcess(cmakePath, string.Concat("--build \"", buildPaths.CMakeBuildRootPath, "\" --config ", buildConfiguration), helEngineWindowsRootPath);
```

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the executor tests pass and the build output path is configuration-specific.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildPaths.cs engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildExecutorTests.cs
git commit -m "Honor per-platform debug build in Windows executor"
```

## Coverage Check

This plan covers every requirement in the spec:

- persisted per-platform `DebugBuild` storage: Task 1
- Build modal control and per-platform restore: Task 2
- queue snapshot behavior: Task 3
- Windows Debug/Release selection and separate artifacts: Task 4
- backward compatibility for older build-config files: Task 1

## Notes for the Implementer

- Keep the new field on the existing per-platform build-config document; do not add a second settings file.
- Keep the queue item snapshot authoritative during execution.
- Do not infer build mode from the presence of a `.pdb` or from the current IDE configuration.
- Leave platform catalog loading untouched; this feature belongs entirely to build settings and build execution.

# Build Dialog Reset And Click Regressions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the Build dialog position during queue refreshes and restore immediate clickability for the close button and queue-item remove buttons before the panel is dragged.

**Architecture:** Keep first-open dialog behavior on `BuildDialog.Show(...)`, but introduce an explicit refresh-in-place path for queue mutations so the same visible dialog can rebind its data without resetting manual positioning. Cover the pointer regression with direct input-driven dialog tests and, if needed, fix the shared dialog shell in `EditorDialogBase` so initial pointer hit testing works without relying on a prior drag.

**Tech Stack:** C#, xUnit, helengine editor UI components, `TestInputBackend`, reflection-based private test helpers.

---

### Task 1: Add RED tests for queue refresh position preservation

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Write the failing add-refresh position test**

Add this test near the existing add/remove queue session coverage:

```csharp
/// <summary>
/// Ensures adding one queued build refreshes the visible dialog without discarding a manual panel position.
/// </summary>
[Fact]
public void HandleBuildDialogAddRequested_WhenDialogWasMoved_PreservesDialogPosition() {
    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
    EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
    BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
    EditorBuildConfigDocument buildConfig = buildConfigService.Load([
        "windows"
    ], CurrentSceneId);

    dialog.Show([
            "windows"
        ],
        [
            CurrentSceneId
        ],
        "windows",
        buildConfig);
    dialog.UpdateLayout(1280, 720);
    SetPrivateField(dialog, "PanelPosition", new int2(164, 118));
    SetPrivateField(dialog, "IsUserPositioned", true);
    InvokePrivate(dialog, "ApplyDialogPosition");

    InvokePrivate(session, "HandleBuildDialogAddRequested", new BuildDialogAddRequest(
        "windows",
        [
            CurrentSceneId
        ],
        @"C:\builds\windows"));

    Assert.Equal(new int2(164, 118), GetPrivateField<int2>(dialog, "PanelPosition"));
    Assert.True(GetPrivateField<bool>(dialog, "IsUserPositioned"));
}
```

- [ ] **Step 2: Write the failing remove-refresh position test**

Add this test near the existing queue-item removal coverage:

```csharp
/// <summary>
/// Ensures removing one queued build refreshes the visible dialog without discarding a manual panel position.
/// </summary>
[Fact]
public void HandleBuildDialogRemoveQueueItemRequested_WhenDialogWasMoved_PreservesDialogPosition() {
    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildConfigDocument buildConfig = buildConfigService.Load([
        "windows"
    ], CurrentSceneId);
    buildConfig.QueueItems.Add(new EditorBuildQueueItemDocument {
        QueueItemId = "queue-1",
        PlatformId = "windows",
        SelectedSceneIds = [
            CurrentSceneId
        ],
        OutputDirectoryPath = @"C:\builds\windows",
        Status = EditorBuildQueueItemStatus.Pending
    });
    buildConfigService.Save(buildConfig);
    EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
    EditorSession session = CreateSession(buildConfigService, buildQueueService, "windows");
    BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");

    dialog.Show([
            "windows"
        ],
        [
            CurrentSceneId
        ],
        "windows",
        buildConfigService.Load([
            "windows"
        ], CurrentSceneId));
    dialog.UpdateLayout(1280, 720);
    SetPrivateField(dialog, "PanelPosition", new int2(212, 146));
    SetPrivateField(dialog, "IsUserPositioned", true);
    InvokePrivate(dialog, "ApplyDialogPosition");

    InvokePrivate(session, "HandleBuildDialogRemoveQueueItemRequested", "queue-1");

    Assert.Equal(new int2(212, 146), GetPrivateField<int2>(dialog, "PanelPosition"));
    Assert.True(GetPrivateField<bool>(dialog, "IsUserPositioned"));
}
```

- [ ] **Step 3: Run the position-preservation tests to verify RED**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests.HandleBuildDialogAddRequested_WhenDialogWasMoved_PreservesDialogPosition|FullyQualifiedName~EditorSessionBuildQueueTests.HandleBuildDialogRemoveQueueItemRequested_WhenDialogWasMoved_PreservesDialogPosition"
```

Expected: FAIL because `BuildDialog.Show(...)` resets `PanelPosition` / `IsUserPositioned` during the session refresh path.

- [ ] **Step 4: Commit the RED tests**

```powershell
git add engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs
git commit -m "test: cover build dialog refresh position regression"
```

### Task 2: Add RED tests for initial Build dialog pointer clickability

**Files:**
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Add input-backed dialog test infrastructure**

Update the test class setup so pointer-driven tests can run through the real input system:

```csharp
/// <summary>
/// Configurable input backend used by pointer-routing build dialog tests.
/// </summary>
readonly TestInputBackend Input;

public BuildDialogTests() {
    TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-dialog-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(TempRootPath);
    EditorInputCaptureService.Reset();

    Core core = new Core(new CoreInitializationOptions {
        ContentRootPath = TempRootPath
    });
    Input = new TestInputBackend();
    core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input);
}
```

Add helpers near the bottom of the file:

```csharp
void CreateModalCamera(int width, int height) {
    EditorEntity cameraEntity = new EditorEntity {
        InternalEntity = true,
        LayerMask = 0b1000000000000000
    };

    CameraComponent camera = new CameraComponent {
        LayerMask = 0b1000000000000000,
        CameraDrawOrder = 255,
        Viewport = new float4(0f, 0f, width, height)
    };
    cameraEntity.AddComponent(camera);
}

void AdvanceInput(MouseState mouseState) {
    Input.SetMouseState(mouseState);
    Input.EarlyUpdate();
    Input.Update();
}
```

- [ ] **Step 2: Write the failing close-button pointer test**

Add this test near the existing modal input assertions:

```csharp
/// <summary>
/// Ensures clicking the Build dialog close button works before the panel has been dragged.
/// </summary>
[Fact]
public void Update_WhenPointerClicksTitleBarCloseButtonBeforeMoving_HidesDialog() {
    CreateModalCamera(1280, 720);

    BuildDialog dialog = new BuildDialog(CreateFont());
    dialog.Show([
            "windows"
        ],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ]
        });
    dialog.UpdateLayout(1280, 720);

    int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
    EditorEntity closeButtonHost = GetPrivateField<EditorEntity>(dialog, "CloseButtonHost");
    ButtonComponent closeButton = GetPrivateField<ButtonComponent>(dialog, "CloseButton");
    int pointerX = panelPosition.X + (int)Math.Round(closeButtonHost.LocalPosition.X) + (closeButton.Size.X / 2);
    int pointerY = panelPosition.Y + (int)Math.Round(closeButtonHost.LocalPosition.Y) + (closeButton.Size.Y / 2);

    AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

    Assert.False(dialog.Enabled);
}
```

- [ ] **Step 3: Write the failing queue-remove pointer test**

Add this test next to the close-button pointer test:

```csharp
/// <summary>
/// Ensures clicking one queue-item remove button works before the panel has been dragged.
/// </summary>
[Fact]
public void Update_WhenPointerClicksQueueItemRemoveButtonBeforeMoving_RaisesRemoveRequest() {
    CreateModalCamera(1280, 720);

    BuildDialog dialog = new BuildDialog(CreateFont());
    string removedQueueItemId = string.Empty;
    dialog.RemoveQueueItemRequested += queueItemId => removedQueueItemId = queueItemId;
    dialog.Show([
            "windows"
        ],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-1",
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending
                }
            ]
        });
    dialog.UpdateLayout(1280, 720);

    int2 panelPosition = GetPrivateField<int2>(dialog, "PanelPosition");
    EditorEntity removeButtonHost = Assert.Single(GetPrivateField<List<EditorEntity>>(dialog, "QueueItemRemoveButtonHosts"));
    ButtonComponent removeButton = Assert.Single(GetPrivateField<List<ButtonComponent>>(dialog, "QueueItemRemoveButtons"));
    int pointerX = panelPosition.X + (int)Math.Round(removeButtonHost.LocalPosition.X) + (removeButton.Size.X / 2);
    int pointerY = panelPosition.Y + (int)Math.Round(removeButtonHost.LocalPosition.Y) + (removeButton.Size.Y / 2);

    AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
    AdvanceInput(new MouseState(pointerX, pointerY, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

    Assert.Equal("queue-1", removedQueueItemId);
}
```

- [ ] **Step 4: Run the pointer tests to verify RED**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Update_WhenPointerClicksTitleBarCloseButtonBeforeMoving_HidesDialog|FullyQualifiedName~BuildDialogTests.Update_WhenPointerClicksQueueItemRemoveButtonBeforeMoving_RaisesRemoveRequest"
```

Expected: FAIL because the initial shown state does not route pointer clicks to one or both dialog-owned buttons until the panel has been moved.

- [ ] **Step 5: Commit the RED tests**

```powershell
git add engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "test: cover build dialog initial click regressions"
```

### Task 3: Implement the minimal refresh-path and input fix

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/EditorDialogBase.cs`
- Test: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Extract one shared bind/rebuild method in `BuildDialog`**

Refactor `BuildDialog` so the binding and row rebuild logic is shared by first-open and refresh-in-place flows. The new structure should look like this:

```csharp
/// <summary>
/// Rebinds the dialog state and rebuilds the visible controls.
/// </summary>
/// <param name="supportedPlatformIds">Visible platform ids rendered as tabs.</param>
/// <param name="sceneIds">Project-relative scene ids available to the active platform.</param>
/// <param name="activePlatformId">Platform id that should stay active after the refresh.</param>
/// <param name="buildConfig">Mutable build config currently being edited.</param>
/// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
/// <param name="resetScrollOffsets">True when the caller wants to reset scroll state.</param>
void BindDialogState(
    IReadOnlyList<string> supportedPlatformIds,
    IReadOnlyList<string> sceneIds,
    string activePlatformId,
    EditorBuildConfigDocument buildConfig,
    EditorPlatformBuildSelectionModel selectionModel,
    bool resetScrollOffsets) {
    CopyPlatforms(supportedPlatformIds);
    CopyScenes(sceneIds);
    CurrentBuildConfig = buildConfig;
    ActivePlatformSelectionModel = selectionModel;
    if (resetScrollOffsets) {
        SceneListScrollComponent.ResetScrollOffset();
        QueueScrollComponent.ResetScrollOffset();
        BuildLogsScrollComponent.ResetScrollOffset();
    }

    EnsurePlatformConfigs();
    SetActivePlatform(activePlatformId);
    RebuildPlatformTabs();
    RebuildActivePlatformSceneRows();
    RebuildQueueRows();
    RebuildBuildLogs();
    LayoutStaticControls();
    UpdateDialogChromeLayout();
}
```

- [ ] **Step 2: Add the explicit refresh-in-place API**

Keep `Show(...)` as the reset-and-center entry point, and add a refresh method that preserves manual position:

```csharp
/// <summary>
/// Refreshes the visible dialog state after queue mutations without resetting a manual position.
/// </summary>
/// <param name="supportedPlatformIds">Visible platform ids rendered as tabs.</param>
/// <param name="sceneIds">Project-relative scene ids available to the active platform.</param>
/// <param name="activePlatformId">Platform id that should stay active after the refresh.</param>
/// <param name="buildConfig">Mutable build config currently being edited.</param>
/// <param name="selectionModel">Builder-provided selection model for the active platform.</param>
public void Refresh(
    IReadOnlyList<string> supportedPlatformIds,
    IReadOnlyList<string> sceneIds,
    string activePlatformId,
    EditorBuildConfigDocument buildConfig,
    EditorPlatformBuildSelectionModel selectionModel = null) {
    if (!Enabled) {
        Show(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, selectionModel);
        return;
    }

    BindDialogState(supportedPlatformIds, sceneIds, activePlatformId, buildConfig, selectionModel, true);
    ClampDialogPosition();
    ApplyDialogPosition();
    UpdateDialogBackdrop();
}
```

Update `Show(...)` to call `ResetDialogPositioning();`, `BindDialogState(..., true);`, `CenterDialogIfNeeded();`, `UpdateDialogBackdrop();`, and then enable the dialog.

- [ ] **Step 3: Switch `EditorSession` queue refreshes to `BuildDialog.Refresh(...)`**

Replace the session re-show calls after queue mutations with the new refresh API:

```csharp
buildDialog.Refresh(
    ResolveVisibleSupportedPlatforms(),
    sceneCatalogService.GetSceneIds(),
    request.PlatformId,
    buildConfig,
    ResolvePlatformSelectionModel(request.PlatformId));
```

and:

```csharp
buildDialog.Refresh(
    visiblePlatformIds,
    sceneCatalogService.GetSceneIds(),
    dialogPlatformId,
    buildConfig,
    ResolvePlatformSelectionModel(dialogPlatformId));
```

This keeps queue text, rows, and builder metadata current while leaving `PanelPosition` / `IsUserPositioned` intact.

- [ ] **Step 4: Fix the initial pointer-routing state in the shared dialog shell**

If the RED pointer tests fail as expected, make the shared dialog shell update its modal blockers and positioned chrome as part of the first visible state instead of relying on a later drag/update side effect. The concrete change should preserve the existing layout helpers and end up with code in `EditorDialogBase` shaped like this:

```csharp
/// <summary>
/// Applies the current positioned dialog shell and modal backdrop state.
/// </summary>
protected void ApplyVisibleDialogState() {
    ClampDialogPosition();
    ApplyDialogPosition();
    UpdateDialogBackdrop();
    UpdateDialogChromeLayout();
}
```

Use that helper from both `UpdateDialogFrame(...)` and the Build-dialog first-open / refresh paths so button hit targets and blockers are correct immediately after show.

- [ ] **Step 5: Run focused tests to verify GREEN**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests.HandleBuildDialogAddRequested_WhenDialogWasMoved_PreservesDialogPosition|FullyQualifiedName~EditorSessionBuildQueueTests.HandleBuildDialogRemoveQueueItemRequested_WhenDialogWasMoved_PreservesDialogPosition|FullyQualifiedName~BuildDialogTests.Update_WhenPointerClicksTitleBarCloseButtonBeforeMoving_HidesDialog|FullyQualifiedName~BuildDialogTests.Update_WhenPointerClicksQueueItemRemoveButtonBeforeMoving_RaisesRemoveRequest"
```

Expected: PASS with all four regression tests green.

- [ ] **Step 6: Run the broader related suites**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests"
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~OpenFileDialogTests"
```

Expected: PASS to confirm the shared dialog-shell change did not regress adjacent modal behavior.

- [ ] **Step 7: Commit the fix**

```powershell
git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/components/ui/EditorDialogBase.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/BuildDialogTests.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs
git commit -m "fix: preserve build dialog state across queue refreshes"
```

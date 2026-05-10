# UI Layout Slots And Panels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add multi-instance editor panels, panel close menus, `UI -> Show/Save/Load`, workspace slot persistence in `user_settings/layout.json`, and preview lock/follow behavior for assets and cameras.

**Architecture:** Introduce one workspace layer above the existing dock layout. The workspace layer owns panel-type registration, live panel instances, workspace persistence DTOs, and dock snapshot/restore. `EditorSession` stops treating user-facing panels as singletons and instead manages typed panel instances plus compatibility helpers for systems that still need one primary instance. `Preview` instances gain per-instance binding state, using asset relative paths and scene entity ids for persistent lock targets.

**Tech Stack:** C#/.NET 9, xUnit, existing editor UI/docking system, `System.Text.Json`, existing `ContextMenu`, `DockLayoutEngine`, `EditorProjectLocalSettingsService` persistence patterns, and `EntitySaveComponent` scene entity ids.

---

## File Structure

### Existing Files To Modify

- `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- `engine/helengine.editor/components/ui/PreviewPanel.cs`
- `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
- `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs`
- `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`
- `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- `engine/helengine.editor.tests/managers/dock/DockLayoutEngineKeyboardFocusTests.cs`

### New Files To Create

- `engine/helengine.editor/components/ui/EditorTitleBarUiMenuAction.cs`
- `engine/helengine.editor/components/ui/dock/DockableEntityPanelMenuAction.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutService.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceSlotDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePanelDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceFloatingPanelDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockNodeDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSplitNodeDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockLeafNodeDocument.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePanelTypeDescriptor.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePanelInstance.cs`
- `engine/helengine.editor/managers/workspace/IEditorWorkspacePanelController.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePanelRegistry.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSnapshot.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSplitSnapshot.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspaceDockLeafSnapshot.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingState.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingTargetKind.cs`
- `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingDocument.cs`
- `engine/helengine.editor.tests/EditorWorkspaceLayoutServiceTests.cs`
- `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
- `engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs`
- `engine/helengine.editor.tests/managers/dock/DockLayoutEngineSnapshotTests.cs`

### Responsibilities

- `EditorTitleBar.cs` owns the new built-in `UI` top-level menu and raises action ids back to the session.
- `DockableEntity.cs` owns the per-panel title-bar menu and `Close` event.
- `EditorWorkspaceLayoutService.cs` owns `user_settings/layout.json` load/save and slot overwrite semantics.
- `EditorWorkspace*Document.cs` files define explicit JSON DTOs for persisted workspaces.
- `EditorWorkspacePanelRegistry.cs` maps panel type ids to factories and panel-state adapters.
- `IEditorWorkspacePanelController.cs` abstracts one live panel instance, its `DockableEntity`, and its state capture/restore behavior.
- `DockLayoutEngine.cs` learns how to snapshot and rebuild tabbed split trees without owning floating panels.
- `EditorSession.cs` becomes the orchestrator for panel instances, show/close/load/save, and compatibility bridging to existing editor systems.
- `PreviewPanel.cs` owns the preview toolbar lock button and exposes instance binding state hooks.
- `PreviewSourceResolver.cs` resolves preview sources from one explicit binding target instead of only the global singleton selection snapshot.

### Task 1: Add Workspace Persistence DTOs And Layout Service

**Files:**
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutService.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceSlotDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePanelDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceFloatingPanelDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockNodeDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSplitNodeDocument.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockLeafNodeDocument.cs`
- Test: `engine/helengine.editor.tests/EditorWorkspaceLayoutServiceTests.cs`

- [ ] **Step 1: Write the failing layout-service tests**

```csharp
using System.Text.Json;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies workspace layout persistence in `user_settings/layout.json`.
/// </summary>
public sealed class EditorWorkspaceLayoutServiceTests : IDisposable {
    string TempProjectRootPath { get; }

    public EditorWorkspaceLayoutServiceTests() {
        TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-workspace-layout-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempProjectRootPath);
    }

    public void Dispose() {
        if (Directory.Exists(TempProjectRootPath)) {
            Directory.Delete(TempProjectRootPath, true);
        }
    }

    [Fact]
    public void SaveSlot_WhenSlotOneIsWritten_CreatesLayoutJsonWithSlotOnePayload() {
        EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);
        EditorWorkspaceSlotDocument slot = new EditorWorkspaceSlotDocument {
            SchemaVersion = 1,
            Panels = [
                new EditorWorkspacePanelDocument {
                    InstanceId = "preview-1",
                    PanelTypeId = "preview",
                    IsDocked = false
                }
            ]
        };

        service.SaveSlot(1, slot);

        string filePath = Path.Combine(TempProjectRootPath, "user_settings", "layout.json");
        Assert.True(File.Exists(filePath));
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(filePath));
        Assert.True(json.RootElement.TryGetProperty("slots", out JsonElement slots));
        Assert.True(slots.TryGetProperty("slot1", out JsonElement slot1));
        Assert.Equal(1, slot1.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void LoadSlot_WhenLayoutFileIsMissing_ReturnsNull() {
        EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);

        EditorWorkspaceSlotDocument slot = service.LoadSlot(3);

        Assert.Null(slot);
    }

    [Fact]
    public void LoadSlot_WhenJsonIsMalformed_ReturnsNullWithoutThrowing() {
        string settingsDirectoryPath = Path.Combine(TempProjectRootPath, "user_settings");
        Directory.CreateDirectory(settingsDirectoryPath);
        File.WriteAllText(Path.Combine(settingsDirectoryPath, "layout.json"), "{ invalid json");
        EditorWorkspaceLayoutService service = new EditorWorkspaceLayoutService(TempProjectRootPath);

        EditorWorkspaceSlotDocument slot = service.LoadSlot(2);

        Assert.Null(slot);
    }
}
```

- [ ] **Step 2: Run the new workspace-layout tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorWorkspaceLayoutServiceTests" -v minimal`

Expected: FAIL with missing `EditorWorkspaceLayoutService` and DTO types.

- [ ] **Step 3: Write the minimal DTOs and layout service**

```csharp
using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Loads and saves editor workspace slots in `user_settings/layout.json`.
    /// </summary>
    public sealed class EditorWorkspaceLayoutService {
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string ProjectRootPath { get; }

        string LayoutFilePath {
            get {
                return Path.Combine(ProjectRootPath, "user_settings", "layout.json");
            }
        }

        public EditorWorkspaceLayoutService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public EditorWorkspaceSlotDocument LoadSlot(int slotNumber) {
            ValidateSlotNumber(slotNumber);
            EditorWorkspaceLayoutDocument document = TryLoadDocument();
            if (document == null) {
                return null;
            }

            return document.GetSlot(slotNumber);
        }

        public void SaveSlot(int slotNumber, EditorWorkspaceSlotDocument slot) {
            ValidateSlotNumber(slotNumber);
            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            }

            EditorWorkspaceLayoutDocument document = TryLoadDocument() ?? EditorWorkspaceLayoutDocument.CreateDefault();
            document.SetSlot(slotNumber, slot);

            string directoryPath = Path.GetDirectoryName(LayoutFilePath) ?? throw new InvalidOperationException("Layout directory path could not be resolved.");
            Directory.CreateDirectory(directoryPath);
            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(LayoutFilePath, json);
        }

        EditorWorkspaceLayoutDocument TryLoadDocument() {
            if (!File.Exists(LayoutFilePath)) {
                return null;
            }

            try {
                string json = File.ReadAllText(LayoutFilePath);
                return JsonSerializer.Deserialize<EditorWorkspaceLayoutDocument>(json, JsonSerializerOptions);
            } catch {
                return null;
            }
        }

        static void ValidateSlotNumber(int slotNumber) {
            if (slotNumber < 1 || slotNumber > 5) {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Workspace slot number must be between 1 and 5.");
            }
        }
    }
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Represents the persisted workspace layout file.
    /// </summary>
    public sealed class EditorWorkspaceLayoutDocument {
        public EditorWorkspaceSlotDocument Slot1 { get; set; } = new EditorWorkspaceSlotDocument();
        public EditorWorkspaceSlotDocument Slot2 { get; set; } = new EditorWorkspaceSlotDocument();
        public EditorWorkspaceSlotDocument Slot3 { get; set; } = new EditorWorkspaceSlotDocument();
        public EditorWorkspaceSlotDocument Slot4 { get; set; } = new EditorWorkspaceSlotDocument();
        public EditorWorkspaceSlotDocument Slot5 { get; set; } = new EditorWorkspaceSlotDocument();

        public static EditorWorkspaceLayoutDocument CreateDefault() {
            return new EditorWorkspaceLayoutDocument();
        }

        public EditorWorkspaceSlotDocument GetSlot(int slotNumber) {
            return slotNumber switch {
                1 => Slot1,
                2 => Slot2,
                3 => Slot3,
                4 => Slot4,
                5 => Slot5,
                _ => throw new ArgumentOutOfRangeException(nameof(slotNumber))
            };
        }

        public void SetSlot(int slotNumber, EditorWorkspaceSlotDocument slot) {
            if (slotNumber == 1) {
                Slot1 = slot;
            } else if (slotNumber == 2) {
                Slot2 = slot;
            } else if (slotNumber == 3) {
                Slot3 = slot;
            } else if (slotNumber == 4) {
                Slot4 = slot;
            } else if (slotNumber == 5) {
                Slot5 = slot;
            } else {
                throw new ArgumentOutOfRangeException(nameof(slotNumber));
            }
        }
    }
}
```

- [ ] **Step 4: Run the workspace-layout tests and verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorWorkspaceLayoutServiceTests" -v minimal`

Expected: PASS, `3` tests passed.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/workspace engine/helengine.editor.tests/EditorWorkspaceLayoutServiceTests.cs
rtk git commit -m "Add workspace layout slot persistence"
```

### Task 2: Add The Built-In UI Menu To The Title Bar

**Files:**
- Create: `engine/helengine.editor/components/ui/EditorTitleBarUiMenuAction.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Test: `engine/helengine.editor.tests/EditorTitleBarTests.cs`

- [ ] **Step 1: Write the failing UI-menu title-bar tests**

```csharp
[Fact]
public void Constructor_BuildsUiMenuButtonBesideBuildButton() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

    EditorEntity uiButtonEntity = GetPrivateField<EditorEntity>(titleBar, "UiMenuButtonEntity");
    Assert.NotNull(uiButtonEntity);
}

[Fact]
public void ActivateUiMenuItemForTest_WhenSaveSlotThreeIsRequested_RaisesUiMenuAction() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");
    EditorTitleBarUiMenuAction? action = null;
    titleBar.UiMenuActionRequested += value => action = value;

    titleBar.ApplyUiShowMenuItems(["Viewport", "Preview"]);
    titleBar.ActivateUiMenuItemForTest("save-slot-3");

    Assert.Equal(EditorTitleBarUiMenuAction.SaveSlot3, Assert.IsType<EditorTitleBarUiMenuAction>(action));
}

[Fact]
public void ApplyUiShowMenuItems_WhenPanelTypesChange_RebuildsShowSubmenuItems() {
    InitializeCore();
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Main Editor Title");

    titleBar.ApplyUiShowMenuItems(["Viewport", "Preview", "Logger"]);

    ContextMenu uiMenu = GetPrivateField<ContextMenu>(titleBar, "UiMenu");
    titleBar.ToggleUiMenuForTest();
    TextComponent loggerText = FindTextComponent(uiMenu.Entity, "Logger");
    Assert.NotNull(loggerText);
}
```

- [ ] **Step 2: Run the title-bar UI-menu tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorTitleBarTests" -v minimal`

Expected: FAIL with missing `UiMenuButtonEntity`, missing `UiMenuActionRequested`, and missing test hooks.

- [ ] **Step 3: Implement the built-in UI menu, submenu state, and routing**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Identifies built-in UI menu actions raised by the editor title bar.
    /// </summary>
    public enum EditorTitleBarUiMenuAction {
        ShowViewport,
        ShowSceneHierarchy,
        ShowAssetBrowser,
        ShowProperties,
        ShowLogger,
        ShowPreview,
        SaveSlot1,
        SaveSlot2,
        SaveSlot3,
        SaveSlot4,
        SaveSlot5,
        LoadSlot1,
        LoadSlot2,
        LoadSlot3,
        LoadSlot4,
        LoadSlot5
    }
}
```

```csharp
readonly EditorEntity UiMenuButtonEntity;
readonly ContextMenu UiMenu;
readonly ContextMenu UiShowMenu;
readonly ContextMenu UiSaveMenu;
readonly ContextMenu UiLoadMenu;
readonly List<string> UiShowPanelLabels;

public event Action<EditorTitleBarUiMenuAction> UiMenuActionRequested;

public void ApplyUiShowMenuItems(IReadOnlyList<string> panelLabels) {
    if (panelLabels == null) {
        throw new ArgumentNullException(nameof(panelLabels));
    }

    UiShowPanelLabels.Clear();
    for (int index = 0; index < panelLabels.Count; index++) {
        string label = panelLabels[index];
        if (!string.IsNullOrWhiteSpace(label)) {
            UiShowPanelLabels.Add(label);
        }
    }
}

internal void ActivateUiMenuItemForTest(string actionId) {
    RaiseUiMenuAction(actionId);
}
```

- [ ] **Step 4: Run the title-bar test suite and verify it passes**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorTitleBarTests" -v minimal`

Expected: PASS with the new `UI` menu tests included.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/components/ui/EditorTitleBarUiMenuAction.cs engine/helengine.editor.tests/EditorTitleBarTests.cs
rtk git commit -m "Add title bar UI workspace menu"
```

### Task 3: Add Per-Panel Close Menus To DockableEntity

**Files:**
- Create: `engine/helengine.editor/components/ui/dock/DockableEntityPanelMenuAction.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Test: `engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs`

- [ ] **Step 1: Write the failing dockable panel-menu tests**

```csharp
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies per-panel title-bar menu behavior on dockable entities.
/// </summary>
public sealed class DockableEntityPanelMenuTests {
    [Fact]
    public void Constructor_CreatesPanelMenuButtonInsideTitleBar() {
        DockableEntity dock = new DockableEntity(CreateFont());

        EditorEntity panelMenuButtonEntity = GetPrivateField<EditorEntity>(dock, "PanelMenuButtonEntity");

        Assert.NotNull(panelMenuButtonEntity);
    }

    [Fact]
    public void ActivatePanelMenuActionForTest_WhenCloseIsRequested_RaisesCloseRequested() {
        DockableEntity dock = new DockableEntity(CreateFont());
        bool raised = false;
        dock.CloseRequested += () => raised = true;

        dock.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

        Assert.True(raised);
    }
}
```

- [ ] **Step 2: Run the dockable panel-menu tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DockableEntityPanelMenuTests" -v minimal`

Expected: FAIL because `CloseRequested`, panel-menu state, and test hooks do not exist.

- [ ] **Step 3: Add the title-bar menu button, `CloseRequested`, and menu routing**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Identifies the actions available from one dockable panel title-bar menu.
    /// </summary>
    public enum DockableEntityPanelMenuAction {
        Close
    }
}
```

```csharp
readonly EditorEntity panelMenuButtonEntity;
readonly ContextMenu panelMenu;
readonly IReadOnlyList<ContextMenuItem> panelMenuItems;

public event Action CloseRequested;

internal void ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction action) {
    if (action == DockableEntityPanelMenuAction.Close) {
        RaiseCloseRequested();
    }
}

void RaiseCloseRequested() {
    CloseRequested?.Invoke();
}
```

- [ ] **Step 4: Run the dockable panel-menu tests and the tab-strip keyboard-focus suite**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DockableEntityPanelMenuTests|FullyQualifiedName~DockLayoutEngineKeyboardFocusTests|FullyQualifiedName~DockTabStripTests" -v minimal`

Expected: PASS, proving the close-menu affordance did not break existing tab-strip behavior.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/components/ui/dock/DockableEntityPanelMenuAction.cs engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs
rtk git commit -m "Add close menu to dockable panels"
```

### Task 4: Teach DockLayoutEngine To Snapshot And Restore Dock Trees

**Files:**
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSnapshot.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockSplitSnapshot.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspaceDockLeafSnapshot.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Test: `engine/helengine.editor.tests/managers/dock/DockLayoutEngineSnapshotTests.cs`

- [ ] **Step 1: Write the failing dock snapshot/restore tests**

```csharp
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies dock layout snapshot and restore behavior.
/// </summary>
public sealed class DockLayoutEngineSnapshotTests {
    [Fact]
    public void CaptureSnapshot_WhenLayoutContainsSplitAndTabs_ReturnsTreeWithActiveTabAndFractions() {
        DockLayoutEngine layout = new DockLayoutEngine();
        DockableEntity viewport = new DockableEntity(CreateFont()) { Title = "Viewport" };
        DockableEntity logger = new DockableEntity(CreateFont()) { Title = "Logger" };
        DockableEntity preview = new DockableEntity(CreateFont()) { Title = "Preview" };

        layout.Add(viewport);
        layout.Add(logger);
        layout.Add(preview);
        layout.DockAsRoot(viewport);
        layout.DockRelative(logger, viewport, DockInsertDirection.Bottom, 0.7f);
        layout.DockRelative(preview, logger, DockInsertDirection.Fill, 0.5f);

        EditorWorkspaceDockSnapshot snapshot = layout.CaptureSnapshot(dock => dock.Title.ToLowerInvariant());

        EditorWorkspaceDockSplitSnapshot root = Assert.IsType<EditorWorkspaceDockSplitSnapshot>(snapshot.Root);
        Assert.Equal(0.7f, root.SplitFraction, 3);
    }

    [Fact]
    public void RestoreSnapshot_WhenDockablesAreProvided_RebuildsVisibleTraversalOrder() {
        DockLayoutEngine layout = new DockLayoutEngine();
        DockableEntity viewport = new DockableEntity(CreateFont()) { Title = "Viewport" };
        DockableEntity logger = new DockableEntity(CreateFont()) { Title = "Logger" };

        EditorWorkspaceDockSnapshot snapshot = new EditorWorkspaceDockSnapshot {
            Root = new EditorWorkspaceDockSplitSnapshot {
                IsVertical = false,
                SplitFraction = 0.7f,
                First = new EditorWorkspaceDockLeafSnapshot {
                    ActiveInstanceId = "viewport",
                    InstanceIds = ["viewport"]
                },
                Second = new EditorWorkspaceDockLeafSnapshot {
                    ActiveInstanceId = "logger",
                    InstanceIds = ["logger"]
                }
            }
        };

        layout.Add(viewport);
        layout.Add(logger);
        layout.RestoreSnapshot(snapshot, instanceId => instanceId == "viewport" ? viewport : logger);

        IReadOnlyList<DockableEntity> visible = layout.GetVisibleDockablesInTraversalOrder();
        Assert.Equal(["Viewport", "Logger"], visible.Select(dock => dock.Title).ToArray());
    }
}
```

- [ ] **Step 2: Run the dock snapshot tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DockLayoutEngineSnapshotTests" -v minimal`

Expected: FAIL with missing snapshot types and missing `CaptureSnapshot` / `RestoreSnapshot`.

- [ ] **Step 3: Implement dock snapshot DTOs and `DockLayoutEngine` capture/restore**

```csharp
public EditorWorkspaceDockSnapshot CaptureSnapshot(Func<DockableEntity, string> instanceIdResolver) {
    if (instanceIdResolver == null) {
        throw new ArgumentNullException(nameof(instanceIdResolver));
    }

    return new EditorWorkspaceDockSnapshot {
        Root = root == null ? null : CaptureNode(root, instanceIdResolver)
    };
}

public void RestoreSnapshot(EditorWorkspaceDockSnapshot snapshot, Func<string, DockableEntity> dockResolver) {
    if (snapshot == null) {
        throw new ArgumentNullException(nameof(snapshot));
    }
    if (dockResolver == null) {
        throw new ArgumentNullException(nameof(dockResolver));
    }

    root = snapshot.Root == null ? null : RestoreNode(snapshot.Root, dockResolver);
    EndResize();
}
```

- [ ] **Step 4: Run dock snapshot tests plus keyboard-focus dock tests**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~DockLayoutEngineSnapshotTests|FullyQualifiedName~DockLayoutEngineKeyboardFocusTests" -v minimal`

Expected: PASS with restored traversal and active-tab behavior still intact.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor/managers/workspace/EditorWorkspaceDockSnapshot.cs engine/helengine.editor/managers/workspace/EditorWorkspaceDockSplitSnapshot.cs engine/helengine.editor/managers/workspace/EditorWorkspaceDockLeafSnapshot.cs engine/helengine.editor.tests/managers/dock/DockLayoutEngineSnapshotTests.cs
rtk git commit -m "Add dock layout snapshot support"
```

### Task 5: Introduce Panel Registry And Live Panel Instances In EditorSession

**Files:**
- Create: `engine/helengine.editor/managers/workspace/IEditorWorkspacePanelController.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePanelTypeDescriptor.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePanelInstance.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePanelRegistry.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`

- [ ] **Step 1: Write the failing session workspace tests for duplicate panel creation and close**

```csharp
[Fact]
public void UiShow_WhenPreviewIsOpenedTwice_CreatesTwoIndependentPreviewInstances() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);

    IReadOnlyList<DockableEntity> previews = harness.Session.GetPanelInstancesForTest("preview").Select(instance => instance.Dockable).ToArray();
    Assert.Equal(2, previews.Count);
    Assert.NotSame(previews[0], previews[1]);
}

[Fact]
public void ClosePanel_WhenOneLoggerInstanceIsClosed_LeavesSiblingLoggerOpen() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowLogger);
    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowLogger);
    EditorWorkspacePanelInstance first = harness.Session.GetPanelInstancesForTest("logger")[0];
    EditorWorkspacePanelInstance second = harness.Session.GetPanelInstancesForTest("logger")[1];

    first.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);

    IReadOnlyList<EditorWorkspacePanelInstance> remaining = harness.Session.GetPanelInstancesForTest("logger");
    Assert.Single(remaining);
    Assert.Same(second, remaining[0]);
}
```

- [ ] **Step 2: Run the session workspace tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests" -v minimal`

Expected: FAIL with missing panel-registry/session helpers.

- [ ] **Step 3: Implement panel descriptors, live instances, and registry-backed creation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Represents one live workspace panel instance.
    /// </summary>
    public sealed class EditorWorkspacePanelInstance {
        public string InstanceId { get; set; } = string.Empty;
        public string PanelTypeId { get; set; } = string.Empty;
        public DockableEntity Dockable { get; set; } = null!;
        public IEditorWorkspacePanelController Controller { get; set; } = null!;
    }
}
```

```csharp
public interface IEditorWorkspacePanelController : IDisposable {
    DockableEntity Dockable { get; }
    string PanelTypeId { get; }
    string DisplayTitle { get; }
    object CaptureState();
    void RestoreState(object state);
}
```

```csharp
readonly List<EditorWorkspacePanelInstance> PanelInstances = [];
readonly EditorWorkspacePanelRegistry PanelRegistry;

internal IReadOnlyList<EditorWorkspacePanelInstance> GetPanelInstancesForTest(string panelTypeId) {
    return PanelInstances.Where(instance => string.Equals(instance.PanelTypeId, panelTypeId, StringComparison.OrdinalIgnoreCase)).ToArray();
}

internal void HandleUiMenuActionForTest(EditorTitleBarUiMenuAction action) {
    HandleUiMenuActionRequested(action);
}
```

- [ ] **Step 4: Run the session workspace tests and existing keyboard-focus integration tests**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests" -v minimal`

Expected: PASS, proving duplicate panel instances and close do not regress focus publication.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/workspace/IEditorWorkspacePanelController.cs engine/helengine.editor/managers/workspace/EditorWorkspacePanelTypeDescriptor.cs engine/helengine.editor/managers/workspace/EditorWorkspacePanelInstance.cs engine/helengine.editor/managers/workspace/EditorWorkspacePanelRegistry.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git commit -m "Add workspace panel registry and instances"
```

### Task 6: Wire UI Show/Save/Load And Workspace Slot Restore In EditorSession

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutService.cs`
- Test: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
- Test: `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs`

- [ ] **Step 1: Write failing tests for save/load slot orchestration**

```csharp
[Fact]
public void SaveLayoutSlot_WhenWorkspaceContainsFloatingPreview_WritesSlotPayloadToLayoutJson() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();
    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowPreview);
    EditorWorkspacePanelInstance preview = Assert.Single(harness.Session.GetPanelInstancesForTest("preview"));
    preview.Dockable.Position = new float3(140f, 220f, 0f);
    preview.Dockable.Size = new int2(360, 240);

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);

    string layoutPath = Path.Combine(harness.ProjectRootPath, "user_settings", "layout.json");
    Assert.True(File.Exists(layoutPath));
}

[Fact]
public void LoadLayoutSlot_WhenSavedWorkspaceContainsTwoViewports_RecreatesBothInstances() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();
    harness.SeedLayoutSlotWithTwoViewportPanels(2);

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot2);

    Assert.Equal(2, harness.Session.GetPanelInstancesForTest("viewport").Count);
}
```

- [ ] **Step 2: Run the workspace orchestration tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests" -v minimal`

Expected: FAIL because session save/load routing and slot restore logic do not exist.

- [ ] **Step 3: Implement UI menu routing, slot save/load, centered floating creation, and settings migration**

```csharp
void HandleUiMenuActionRequested(EditorTitleBarUiMenuAction action) {
    if (action == EditorTitleBarUiMenuAction.SaveSlot1) {
        SaveWorkspaceSlot(1);
    } else if (action == EditorTitleBarUiMenuAction.SaveSlot2) {
        SaveWorkspaceSlot(2);
    } else if (action == EditorTitleBarUiMenuAction.SaveSlot3) {
        SaveWorkspaceSlot(3);
    } else if (action == EditorTitleBarUiMenuAction.SaveSlot4) {
        SaveWorkspaceSlot(4);
    } else if (action == EditorTitleBarUiMenuAction.SaveSlot5) {
        SaveWorkspaceSlot(5);
    } else if (action == EditorTitleBarUiMenuAction.LoadSlot1) {
        LoadWorkspaceSlot(1);
    } else if (action == EditorTitleBarUiMenuAction.LoadSlot2) {
        LoadWorkspaceSlot(2);
    } else if (action == EditorTitleBarUiMenuAction.LoadSlot3) {
        LoadWorkspaceSlot(3);
    } else if (action == EditorTitleBarUiMenuAction.LoadSlot4) {
        LoadWorkspaceSlot(4);
    } else if (action == EditorTitleBarUiMenuAction.LoadSlot5) {
        LoadWorkspaceSlot(5);
    } else {
        CreatePanelInstanceFromUiAction(action);
    }
}
```

```csharp
void PositionFloatingPanelAtCenter(DockableEntity dockable) {
    int width = dockable.Size.X;
    int height = dockable.Size.Y + dockable.TitleBarHeightPixels;
    int centeredX = Math.Max(0, (MostRecentHostWidth - width) / 2);
    int centeredY = Math.Max(titleBar.Height, titleBar.Height + ((MostRecentHostHeight - titleBar.Height - height) / 2));
    dockable.Position = new float3(centeredX, centeredY, 0f);
}
```

- [ ] **Step 4: Run the workspace orchestration tests and local-settings tests**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal`

Expected: PASS with slot save/load covered and existing project settings tests still green.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs engine/helengine.editor/managers/workspace/EditorWorkspaceLayoutService.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs
rtk git commit -m "Wire UI workspace show save and load"
```

### Task 7: Add Preview Lock Toolbar And Explicit Binding Targets

**Files:**
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingState.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingTargetKind.cs`
- Create: `engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingDocument.cs`
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/managers/preview/PreviewSourceResolver.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`
- Test: `engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`

- [ ] **Step 1: Write failing preview lock tests**

```csharp
[Fact]
public void ToggleLock_WhenPreviewHasTextureSelection_LocksToCurrentAsset() {
    PreviewPanel panel = new PreviewPanel(CreateFont());
    panel.SetBindingStateForTest(new EditorWorkspacePreviewBindingState {
        IsLocked = false,
        TargetKind = EditorWorkspacePreviewBindingTargetKind.Asset,
        AssetRelativePath = "Textures/Preview.png"
    });

    panel.ToggleLockForTest();

    EditorWorkspacePreviewBindingState state = panel.GetBindingStateForTest();
    Assert.True(state.IsLocked);
    Assert.Equal("Textures/Preview.png", state.AssetRelativePath);
}

[Fact]
public void TryResolve_WhenLockedCameraEntityIdResolves_ReturnsCameraPreviewSource() {
    PreviewSourceResolver resolver = CreateResolver();
    EditorEntity cameraEntity = CreateCameraEntityWithPersistentId("camera-1");
    EditorWorkspacePreviewBindingState state = new EditorWorkspacePreviewBindingState {
        IsLocked = true,
        TargetKind = EditorWorkspacePreviewBindingTargetKind.Camera,
        SceneEntityId = "camera-1"
    };

    bool resolved = resolver.TryResolve(state, null, cameraEntity, out IPreviewSource source);

    Assert.True(resolved);
    Assert.IsType<CameraPreviewSource>(source);
}

[Fact]
public void RefreshPreviewPanels_WhenLockedTargetWasDeleted_ClearsPreview() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();
    EditorWorkspacePanelInstance preview = Assert.Single(harness.Session.GetPanelInstancesForTest("preview"));

    harness.Session.LockPreviewToAssetForTest(preview.InstanceId, "Textures/Preview.png");
    harness.Session.SimulatePreviewAssetDeletionForTest("Textures/Preview.png");

    Assert.True(harness.Session.IsPreviewClearForTest(preview.InstanceId));
}
```

- [ ] **Step 2: Run preview tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests|FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~EditorSessionWorkspaceTests" -v minimal`

Expected: FAIL because preview lock state, toolbar toggle, and explicit target resolution do not exist.

- [ ] **Step 3: Implement preview binding state, lock toolbar, and entity-id persistence**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores one preview panel binding state.
    /// </summary>
    public sealed class EditorWorkspacePreviewBindingState {
        public bool IsLocked { get; set; }
        public EditorWorkspacePreviewBindingTargetKind TargetKind { get; set; }
        public string AssetRelativePath { get; set; } = string.Empty;
        public string SceneEntityId { get; set; } = string.Empty;
    }
}
```

```csharp
public bool TryResolve(
    EditorWorkspacePreviewBindingState bindingState,
    AssetBrowserEntry selectedAssetEntry,
    Entity selectedEntity,
    out IPreviewSource source) {
    if (bindingState != null && bindingState.IsLocked) {
        return TryResolveLockedBinding(bindingState, out source);
    }

    return TryResolve(selectedAssetEntry, selectedEntity, out source);
}
```

```csharp
void HandleLockButtonPressed() {
    if (ActiveBindingState.TargetKind == EditorWorkspacePreviewBindingTargetKind.None) {
        return;
    }

    ActiveBindingState.IsLocked = !ActiveBindingState.IsLocked;
    UpdateLockButtonVisual();
}
```

- [ ] **Step 4: Run preview tests and targeted session workspace tests**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests|FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~EditorSessionWorkspaceTests" -v minimal`

Expected: PASS with both asset lock and camera lock behavior covered.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/managers/preview/PreviewSourceResolver.cs engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingState.cs engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingTargetKind.cs engine/helengine.editor/managers/workspace/EditorWorkspacePreviewBindingDocument.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs engine/helengine.editor.tests/managers/preview/PreviewSourceResolverTests.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git commit -m "Add lockable preview panel bindings"
```

### Task 8: Finish Startup Layout Integration And Full Regression Pass

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/tests/EditorSessionWorkspaceTests.cs`
- Modify: `engine/helengine.editor/tests/EditorSessionKeyboardFocusIntegrationTests.cs`

- [ ] **Step 1: Add the failing startup/default-layout regression tests**

```csharp
[Fact]
public void Startup_WhenNoLayoutFileExists_CreatesCurrentDefaultWorkspace() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();

    IReadOnlyList<EditorWorkspacePanelInstance> viewports = harness.Session.GetPanelInstancesForTest("viewport");
    IReadOnlyList<EditorWorkspacePanelInstance> previews = harness.Session.GetPanelInstancesForTest("preview");

    Assert.Single(viewports);
    Assert.Single(previews);
}

[Fact]
public void LoadLayout_WhenUnknownPanelTypeIsEncountered_SkipsItAndRestoresKnownPanels() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();
    harness.SeedLayoutSlotWithUnknownPanelType(4);

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot4);

    Assert.NotEmpty(harness.Session.GetAllPanelInstancesForTest());
}
```

- [ ] **Step 2: Run the workspace/session integration tests and verify they fail**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests" -v minimal`

Expected: FAIL until startup/default-layout compatibility and unknown-panel skipping are wired.

- [ ] **Step 3: Implement default-layout fallback and full restore cleanup**

```csharp
void InitializeWorkspace() {
    EditorWorkspaceSlotDocument startupSlot = WorkspaceLayoutService.LoadSlot(1);
    if (startupSlot != null && startupSlot.Panels.Count > 0) {
        RestoreWorkspaceSlot(startupSlot);
        return;
    }

    CreateDefaultWorkspace();
}
```

```csharp
void RestoreWorkspaceSlot(EditorWorkspaceSlotDocument slot) {
    DisposeAllPanelInstances();
    CreatePanelsFromSlot(slot);
    RestoreDockedPanelsFromSlot(slot);
    RestoreFloatingPanelsFromSlot(slot);
    RefreshWorkspaceMenus();
    PublishKeyboardFocusDockOrder();
}
```

- [ ] **Step 4: Run the full targeted editor regression pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests|FullyQualifiedName~EditorTitleBarTests|FullyQualifiedName~PreviewPanelTests|FullyQualifiedName~PreviewSourceResolverTests|FullyQualifiedName~EditorWorkspaceLayoutServiceTests|FullyQualifiedName~DockLayoutEngineSnapshotTests|FullyQualifiedName~DockableEntityPanelMenuTests" -v minimal`

Expected: PASS across the full workspace feature surface.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs
rtk git commit -m "Finish workspace layout integration"
```

## Self-Review

### Spec Coverage

- `UI -> Show` is covered by Tasks 2, 5, and 6.
- `UI -> Save -> Slot 1..5` and `UI -> Load -> Slot 1..5` are covered by Tasks 1 and 6.
- multi-instance panels are covered by Tasks 5 and 8.
- per-panel close menus are covered by Task 3.
- dock split tree, tabs, and floating panel persistence are covered by Tasks 1, 4, and 6.
- preview lock/follow behavior for assets and cameras is covered by Task 7.
- startup fallback and unknown-panel resilience are covered by Task 8.

### Placeholder Scan

- No `TODO`, `TBD`, or “similar to” shortcuts remain in the task steps.
- Every code-changing step includes concrete code or method signatures.
- Every verification step includes an exact `rtk dotnet test` command.

### Type Consistency

- Workspace persistence consistently uses `EditorWorkspace*` names.
- Live panel abstractions consistently use `EditorWorkspacePanelInstance` and `IEditorWorkspacePanelController`.
- Preview persistence consistently uses `EditorWorkspacePreviewBindingState` with `AssetRelativePath` and `SceneEntityId`.

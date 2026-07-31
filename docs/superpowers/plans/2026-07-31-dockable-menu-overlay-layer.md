# Dockable Menu Overlay Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render each dockable `...` menu above all dockable content and below modal UI.

**Architecture:** Construct the dockable `ContextMenu` with `EditorLayerMasks.EditorModalUi`, so it uses the existing modal UI camera after panel-content cameras. Its ordinary overlay render orders remain beneath actual modal surfaces, whose render-order band begins at `RenderOrder2D.ModalBackground`.

**Tech Stack:** C#/.NET 9, helengine 2D camera layers, xUnit.

---

### Task 1: Lock the dockable menu layer contract

**Files:**
- Modify: `engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs`
- Test: `engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Constructor_PlacesPanelMenuOnModalUiLayerBelowModalSurfaces() {
    InitializeCore();
    DockableEntity dock = new DockableEntity(CreateFont());
    ContextMenu panelMenu = GetPrivateField<ContextMenu>(dock, "PanelMenu");
    RoundedRectComponent background = GetPrivateField<RoundedRectComponent>(panelMenu, "Background");

    Assert.Equal(EditorLayerMasks.EditorModalUi, panelMenu.Entity.LayerMask);
    Assert.Equal(RenderOrder2D.OverlayBackground, background.RenderOrder2D);
    Assert.True(background.RenderOrder2D < RenderOrder2D.ModalBackground);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~DockableEntityPanelMenuTests.Constructor_PlacesPanelMenuOnModalUiLayerBelowModalSurfaces"
```

Expected: FAIL because the current panel-menu layer is `EditorLayerMasks.EditorUi`.

### Task 2: Move the dockable menu to the overlay camera

**Files:**
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs:209`
- Test: `engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs`

- [ ] **Step 1: Change the menu construction layer**

```csharp
PanelMenu = new ContextMenu(
    font,
    EditorLayerMasks.EditorModalUi,
    RenderOrder2D.OverlayBackground,
    RenderOrder2D.OverlayForeground);
```

- [ ] **Step 2: Run the focused regression to verify it passes**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~DockableEntityPanelMenuTests.Constructor_PlacesPanelMenuOnModalUiLayerBelowModalSurfaces"
```

Expected: PASS, one test.

- [ ] **Step 3: Run dockable panel-menu coverage**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~DockableEntityPanelMenuTests"
```

Expected: PASS with no failures.

- [ ] **Step 4: Build the Visual Studio editor launch project**

Run:

```powershell
dotnet build 'helengine.ui/helengine.editor.app/helengine.editor.app.csproj' --no-restore -p:BuildingInsideVisualStudio=true
```

Expected: build succeeds with zero errors.

- [ ] **Step 5: Commit**

```powershell
git add -- 'engine/helengine.editor/components/ui/dock/DockableEntity.cs' 'engine/helengine.editor.tests/components/ui/DockableEntityPanelMenuTests.cs'
git commit -m "fix: layer dockable panel menus above content"
```

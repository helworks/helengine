# Keyboard Focus, TabIndex, And Viewport Shortcuts Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one editor-wide keyboard focus system with WinForms-style `TabIndex`, dock-local and dock-to-dock traversal, shared active outlines on docks and focusable controls, and viewport-local `W` / `R` / `S` gizmo shortcuts that only work while the viewport content is focused and the right mouse button is not pressed.

**Architecture:** Add minimal focus contracts in `helengine.core`, then build one static `EditorKeyboardFocusService` in `helengine.editor` that owns root-dock activation, nested subgroup traversal, mouse-to-keyboard focus synchronization, and activation-key dispatch. Use persistent focus targets for pooled UI such as hierarchy rows, asset rows, and dock tabs; register once, gate with `CanReceiveFocus`, and only unregister when the owning component or strip is actually removed.

**Tech Stack:** C#/.NET 9, Hel engine core/editor UI, xUnit

---

## Scope Check

This remains one plan. The focus contracts, service, shared controls, dock traversal, row pooling, viewport shortcut gating, and session wiring all depend on the same source of truth. Splitting this would produce intermediate states that are not keyboard-usable.

## Plan Corrections From The Previous Draft

This revision fixes the execution gaps that blocked implementation:

- Every test helper referenced by name is defined as a concrete file in this plan.
- `EditorKeyboardFocusService` is treated as a new file, not a modified one.
- No task depends on test-only service accessors. Tests use behavior, reflection, or purpose-built test doubles.
- Dynamic UI lifetime is explicit:
  `ButtonComponent`, `TextBoxComponent`, and `ComboBoxComponent` register in `ComponentAdded` and unregister in `ComponentRemoved`.
  Pooled rows and pooled tabs create one persistent `EditorFocusTarget` per pooled object and never recreate it during layout.
  `DockTabStrip` gets an explicit `DisposeFocusTargets()` path and `DockLayoutEngine.PanelNode.Remove` calls it when the strip is discarded.
- `SceneHierarchyPanel` currently has no primary row action. This plan adds row selection on mouse release first, then reuses that same path for keyboard `Enter`.

## Lifetime Rules

These rules are part of the implementation, not optional follow-up work:

- `EditorKeyboardFocusService.RegisterGroup` and `RegisterTarget` must ignore duplicates.
- `EditorKeyboardFocusService.UnregisterGroup` and `UnregisterTarget` must clear active/focused state if the removed item owned it.
- Shared controls unregister themselves from the service in `ComponentRemoved`.
- Pooled editor rows and tabs stay registered for the life of the pool object; they become unreachable by returning `false` from `CanReceiveFocus` when the backing row/tab is disabled or has no bound data.
- `EditorSession.Dispose()` must call `EditorKeyboardFocusService.Reset()` after detaching event handlers so static state never leaks into the next session.

## File Structure

### New Files

- `engine/helengine.core/model/interfaces/IFocusGroup.cs`
- `engine/helengine.core/model/interfaces/IFocusTarget.cs`
- `engine/helengine.editor/EditorFocusGroup.cs`
- `engine/helengine.editor/EditorFocusTarget.cs`
- `engine/helengine.editor/EditorKeyboardFocusService.cs`
- `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
- `engine/helengine.editor.tests/testing/TestFocusGroup.cs`
- `engine/helengine.editor.tests/testing/TestFocusTarget.cs`
- `engine/helengine.editor.tests/EditorKeyboardFocusServiceTests.cs`
- `engine/helengine.editor.tests/DockableEntityKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/managers/dock/DockLayoutEngineKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/ButtonComponentKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/ComboBoxComponentKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/DockTabStripKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/SceneHierarchyPanelKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/AssetBrowserViewKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`
- `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`

### Modified Files

- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`
- `engine/helengine.core/components/2d/interactable/TextBoxComponent.cs`
- `engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs`
- `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
- `engine/helengine.editor/components/ui/dock/DockTabEntry.cs`
- `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- `engine/helengine.editor/components/ui/asset/AssetBrowserRow.cs`
- `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- `engine/helengine.editor/components/ui/EditorViewport.cs`
- `engine/helengine.editor.tests/DockTabStripTests.cs`
- `engine/helengine.editor.tests/AssetBrowserViewGeneratedAssetTests.cs`

## Task 1: Create The Focus Contracts, Test Doubles, And Service

**Files:**
- Create: `engine/helengine.core/model/interfaces/IFocusGroup.cs`
- Create: `engine/helengine.core/model/interfaces/IFocusTarget.cs`
- Create: `engine/helengine.editor/EditorFocusGroup.cs`
- Create: `engine/helengine.editor/EditorFocusTarget.cs`
- Create: `engine/helengine.editor/EditorKeyboardFocusService.cs`
- Create: `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- Create: `engine/helengine.editor.tests/testing/TestFocusGroup.cs`
- Create: `engine/helengine.editor.tests/testing/TestFocusTarget.cs`
- Create: `engine/helengine.editor.tests/EditorKeyboardFocusServiceTests.cs`

- [ ] **Step 1: Write the failing service tests and their concrete test doubles**

Create `TestFocusGroup` as one simple `IFocusGroup` implementation:

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Focus-group test double that records active-state changes and screen bounds.
    /// </summary>
    public sealed class TestFocusGroup : IFocusGroup {
        /// <summary>
        /// Initializes one focus-group test double.
        /// </summary>
        public TestFocusGroup(IFocusGroup rootGroup, int groupOrder, int left, int top, int width, int height) {
            RootGroup = rootGroup ?? this;
            GroupOrder = groupOrder;
            Bounds = new int4(left, top, width, height);
            CanReceiveFocusValue = true;
        }

        public IFocusGroup RootGroup { get; }
        public int GroupOrder { get; }
        public bool CanReceiveFocusValue { get; set; }
        public bool IsActive { get; private set; }
        public int4 Bounds { get; }
        public bool CanReceiveFocus => CanReceiveFocusValue;

        public bool ContainsScreenPoint(int2 point) {
            return point.X >= Bounds.X &&
                   point.X < Bounds.X + Bounds.Z &&
                   point.Y >= Bounds.Y &&
                   point.Y < Bounds.Y + Bounds.W;
        }

        public void SetGroupActive(bool isActive) {
            IsActive = isActive;
        }
    }
}
```

Create `TestFocusTarget` as one simple `IFocusTarget` implementation:

```csharp
namespace helengine.editor.tests.testing {
    /// <summary>
    /// Focus-target test double that records focus and activation requests.
    /// </summary>
    public sealed class TestFocusTarget : IFocusTarget {
        /// <summary>
        /// Initializes one focus-target test double.
        /// </summary>
        public TestFocusTarget(IFocusGroup focusGroup, int tabIndex, bool isDefaultTarget, int left, int top, int width, int height) {
            FocusGroup = focusGroup;
            TabIndex = tabIndex;
            IsDefaultTarget = isDefaultTarget;
            Bounds = new int4(left, top, width, height);
            CanReceiveFocusValue = true;
        }

        public IFocusGroup FocusGroup { get; set; }
        public int TabIndex { get; set; }
        public bool IsDefaultTarget { get; set; }
        public bool CanReceiveFocusValue { get; set; }
        public bool IsFocused { get; private set; }
        public Keys LastActivationKey { get; private set; }
        public int4 Bounds { get; }
        public bool CanReceiveFocus => CanReceiveFocusValue;

        public bool ContainsScreenPoint(int2 point) {
            return point.X >= Bounds.X &&
                   point.X < Bounds.X + Bounds.Z &&
                   point.Y >= Bounds.Y &&
                   point.Y < Bounds.Y + Bounds.W;
        }

        public void SetTargetFocused(bool isFocused) {
            IsFocused = isFocused;
        }

        public bool CanActivateWithKey(Keys key) {
            return key == Keys.Enter || key == Keys.Space;
        }

        public void ActivateFromKey(Keys key) {
            LastActivationKey = key;
        }
    }
}
```

Write service tests that cover:

```csharp
[Fact]
public void HandleTab_WhenToolbarAndContentGroupsExist_MovesByGroupOrderThenTabIndex()
```

```csharp
[Fact]
public void HandleShiftTab_WhenTargetIsFocused_MovesBackwardWithinTheActiveRootDock()
```

```csharp
[Fact]
public void HandleCtrlTab_WhenForwardIsTrue_ActivatesTheNextVisibleDockDefaultTarget()
```

```csharp
[Fact]
public void HandleCtrlTab_WhenForwardIsFalse_ActivatesThePreviousVisibleDockDefaultTarget()
```

```csharp
[Fact]
public void HandlePointerPressed_WhenLeftClickHitsTarget_FocusesTargetAndDock()
```

```csharp
[Fact]
public void HandlePointerPressed_WhenRightClickHitsRootWithoutTarget_ActivatesDockAndLeavesTargetUnchanged()
```

```csharp
[Fact]
public void Update_WhenFocusedTargetIsUnregistered_FallsBackToTheNextValidTargetInsideTheSameDock()
```

- [ ] **Step 2: Run the service tests to verify the new files are missing**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorKeyboardFocusServiceTests`

Expected: FAIL because the focus contracts, wrappers, and service do not exist yet.

- [ ] **Step 3: Implement the contracts, wrappers, service, and per-frame update component**

Create the shared contracts with these exact members:

```csharp
namespace helengine {
    /// <summary>
    /// Represents one logical keyboard-focus scope.
    /// </summary>
    public interface IFocusGroup {
        IFocusGroup RootGroup { get; }
        int GroupOrder { get; }
        bool CanReceiveFocus { get; }
        bool ContainsScreenPoint(int2 point);
        void SetGroupActive(bool isActive);
    }
}
```

```csharp
namespace helengine {
    /// <summary>
    /// Represents one keyboard-focusable control or logical target.
    /// </summary>
    public interface IFocusTarget {
        IFocusGroup FocusGroup { get; }
        int TabIndex { get; }
        bool IsDefaultTarget { get; }
        bool CanReceiveFocus { get; }
        bool ContainsScreenPoint(int2 point);
        void SetTargetFocused(bool isFocused);
        bool CanActivateWithKey(Keys key);
        void ActivateFromKey(Keys key);
    }
}
```

Implement `EditorFocusGroup` and `EditorFocusTarget` as delegate-backed wrappers with mutable `FocusGroup`, `TabIndex`, and `IsDefaultTarget` properties so pooled tabs and rows can update their ordering during layout.

Implement `EditorKeyboardFocusService` with these public methods:

```csharp
public static void RegisterGroup(IFocusGroup group)
public static void UnregisterGroup(IFocusGroup group)
public static void RegisterTarget(IFocusTarget target)
public static void UnregisterTarget(IFocusTarget target)
public static void SetDockOrder(IReadOnlyList<DockableEntity> dockOrder)
public static void SetFocusedTarget(IFocusTarget target)
public static void HandleTab(bool forward)
public static void HandleCtrlTab(bool forward)
public static void HandleActivationKey(Keys key)
public static void HandlePointerPressed(int2 point, bool isRightButton)
public static void Update()
public static void Reset()
```

Use one internal registration sequence so equal `TabIndex` values remain stable by registration order. Service behavior must be:

- `HandleTab(true)` and `HandleTab(false)` traverse within the active root dock only.
- Traversal order is `GroupOrder`, then `TabIndex`, then registration sequence.
- `HandleCtrlTab` uses the last `SetDockOrder` list and skips docks with no valid targets.
- `HandlePointerPressed` first looks for the front-most valid target hit, then falls back to a root group hit.
- `Update()` repairs invalid state after `CanReceiveFocus` changes or unregister calls.

Add one `EditorKeyboardFocusUpdateComponent` that polls the real `InputManager` once per frame:

```csharp
public override void Update() {
    InputManager input = Core.Instance.InputManager;
    if (input == null) {
        return;
    }

    if (input.WasMouseLeftButtonPressed()) {
        EditorKeyboardFocusService.HandlePointerPressed(input.GetMousePosition(), false);
    } else if (input.WasMouseRightButtonPressed()) {
        EditorKeyboardFocusService.HandlePointerPressed(input.GetMousePosition(), true);
    }

    bool shiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);
    bool controlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
    if (input.WasKeyPressed(Keys.Tab)) {
        if (controlPressed) {
            EditorKeyboardFocusService.HandleCtrlTab(!shiftPressed);
        } else {
            EditorKeyboardFocusService.HandleTab(!shiftPressed);
        }
    } else if (input.WasKeyPressed(Keys.Enter)) {
        EditorKeyboardFocusService.HandleActivationKey(Keys.Enter);
    } else if (input.WasKeyPressed(Keys.Space)) {
        EditorKeyboardFocusService.HandleActivationKey(Keys.Space);
    } else if (input.WasKeyPressed(Keys.W)) {
        EditorKeyboardFocusService.HandleActivationKey(Keys.W);
    } else if (input.WasKeyPressed(Keys.R)) {
        EditorKeyboardFocusService.HandleActivationKey(Keys.R);
    } else if (input.WasKeyPressed(Keys.S)) {
        EditorKeyboardFocusService.HandleActivationKey(Keys.S);
    }

    EditorKeyboardFocusService.Update();
}
```

- [ ] **Step 4: Run the service tests to verify the foundation passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorKeyboardFocusServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/model/interfaces/IFocusGroup.cs engine/helengine.core/model/interfaces/IFocusTarget.cs engine/helengine.editor/EditorFocusGroup.cs engine/helengine.editor/EditorFocusTarget.cs engine/helengine.editor/EditorKeyboardFocusService.cs engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs engine/helengine.editor.tests/testing/TestFocusGroup.cs engine/helengine.editor.tests/testing/TestFocusTarget.cs engine/helengine.editor.tests/EditorKeyboardFocusServiceTests.cs
git commit -m "feat: add editor keyboard focus foundation"
```

## Task 2: Make Shared Controls Focus Targets With Explicit Registration Lifetime

**Files:**
- Modify: `engine/helengine.core/components/2d/interactable/ButtonComponent.cs`
- Modify: `engine/helengine.core/components/2d/interactable/TextBoxComponent.cs`
- Modify: `engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs`
- Create: `engine/helengine.editor.tests/ButtonComponentKeyboardFocusTests.cs`
- Create: `engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs`
- Create: `engine/helengine.editor.tests/ComboBoxComponentKeyboardFocusTests.cs`

- [ ] **Step 1: Write failing tests for button, text box, and combo box focus behavior**

Write tests that prove:

```csharp
[Fact]
public void ButtonComponent_WhenFocused_ActivatesFromEnterAndSpace()
```

```csharp
[Fact]
public void ButtonComponent_ComponentRemoved_UnregistersItsFocusTarget()
```

```csharp
[Fact]
public void TextBoxComponent_SetTargetFocused_UsesExistingTextFocusSemanticsWithoutSpaceActivation()
```

```csharp
[Fact]
public void TextBoxComponent_ComponentRemoved_ClearsStaticTextFocusAndUnregisters()
```

```csharp
[Fact]
public void ComboBoxComponent_WhenFocused_EnterAndSpaceToggleTheMainDropdown()
```

```csharp
[Fact]
public void ComboBoxComponent_ComponentRemoved_UnregistersItsMainFocusTarget()
```

- [ ] **Step 2: Run the shared-control tests to verify the focus hooks do not exist**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ButtonComponentKeyboardFocusTests|TextBoxComponentKeyboardFocusTests|ComboBoxComponentKeyboardFocusTests"`

Expected: FAIL.

- [ ] **Step 3: Add `TabIndex`, `FocusGroup`, focus visuals, and unregister paths to the shared controls**

Add these members to `ButtonComponent`:

```csharp
public int TabIndex { get; set; }
public IFocusGroup FocusGroup { get; set; }
public bool IsKeyboardFocused { get; private set; }
public bool IsDefaultFocusTarget { get; set; }
```

Register and unregister in the existing component lifetime:

```csharp
public override void ComponentAdded(Entity entity) {
    base.ComponentAdded(entity);
    if (FocusGroup != null) {
        EditorKeyboardFocusService.RegisterTarget(this);
    }
}

public override void ComponentRemoved(Entity entity) {
    base.ComponentRemoved(entity);
    if (FocusGroup != null) {
        EditorKeyboardFocusService.UnregisterTarget(this);
    }
}
```

Make the button implement `IFocusTarget` directly:

```csharp
public bool CanReceiveFocus => Parent != null && Parent.Enabled && interactableComponent != null;
public bool ContainsScreenPoint(int2 point) {
    if (Parent == null) {
        return false;
    }

    float3 worldPosition = Parent.Position;
    return point.X >= worldPosition.X &&
           point.X < worldPosition.X + size.X &&
           point.Y >= worldPosition.Y &&
           point.Y < worldPosition.Y + size.Y;
}
public void SetTargetFocused(bool isFocused) { IsKeyboardFocused = isFocused; UpdateButtonColor(); }
public bool CanActivateWithKey(Keys key) { return key == Keys.Enter || key == Keys.Space; }
public void ActivateFromKey(Keys key) { onClickAction?.Invoke(); }
```

Update the button visual priority to keep existing hover and press states, but show the keyboard outline whenever `IsKeyboardFocused` is true.

For `TextBoxComponent`, implement `IFocusTarget` and keep the existing text-editing behavior as the source of truth:

```csharp
public int TabIndex { get; set; }
public IFocusGroup FocusGroup { get; set; }
public bool IsDefaultFocusTarget { get; set; }
public bool CanReceiveFocus => Parent != null && Parent.Enabled && interactableComponent != null;
public bool CanActivateWithKey(Keys key) { return key == Keys.Enter; }
public void ActivateFromKey(Keys key) { Submitted?.Invoke(this); IsFocused = false; }
public void SetTargetFocused(bool isFocused) { IsFocused = isFocused; }
```

In `ComponentAdded`, register when `FocusGroup != null`. In `ComponentRemoved`, unregister and clear `focusedTextBox` if needed. Do not treat `Space` as activation.

For `ComboBoxComponent`, implement `IFocusTarget` for the main control only:

```csharp
public int TabIndex { get; set; }
public IFocusGroup FocusGroup { get; set; }
public bool IsDefaultFocusTarget { get; set; }
public bool CanReceiveFocus => Parent != null && Parent.Enabled && interactable != null;
public bool CanActivateWithKey(Keys key) { return key == Keys.Enter || key == Keys.Space; }
public void ActivateFromKey(Keys key) { if (items.Count > 0) { IsOpen = !IsOpen; } }
public void SetTargetFocused(bool isFocused) { isKeyboardFocused = isFocused; UpdateMainVisual(); }
```

Add one `isKeyboardFocused` field so `UpdateMainVisual()` can show the same thin accent outline while preserving hover and pressed fill behavior.

- [ ] **Step 4: Run the shared-control tests to verify the focus lifetime is correct**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "ButtonComponentKeyboardFocusTests|TextBoxComponentKeyboardFocusTests|ComboBoxComponentKeyboardFocusTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/2d/interactable/ButtonComponent.cs engine/helengine.core/components/2d/interactable/TextBoxComponent.cs engine/helengine.core/components/2d/interactable/ComboBoxComponent.cs engine/helengine.editor.tests/ButtonComponentKeyboardFocusTests.cs engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs engine/helengine.editor.tests/ComboBoxComponentKeyboardFocusTests.cs
git commit -m "feat: add keyboard focus to shared controls"
```

## Task 3: Make Docks Real Focus Groups And Dock Tabs Real Keyboard Targets

**Files:**
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockTabStrip.cs`
- Modify: `engine/helengine.editor/components/ui/dock/DockTabEntry.cs`
- Create: `engine/helengine.editor.tests/DockableEntityKeyboardFocusTests.cs`
- Create: `engine/helengine.editor.tests/managers/dock/DockLayoutEngineKeyboardFocusTests.cs`
- Create: `engine/helengine.editor.tests/DockTabStripKeyboardFocusTests.cs`
- Modify: `engine/helengine.editor.tests/DockTabStripTests.cs`

- [ ] **Step 1: Write failing tests for dock activation, dock traversal order, and tab focus**

Add dock tests that prove:

```csharp
[Fact]
public void SetGroupActive_WhenTrue_ShowsTheDockOutline()
```

```csharp
[Fact]
public void GetVisibleDockablesInTraversalOrder_WhenTabsAndSplitsExist_ReturnsVisibleActiveDockablesInLeafOrder()
```

```csharp
[Fact]
public void DockTabStrip_WhenUpdated_CreatesPersistentFocusTargetsForVisibleTabs()
```

```csharp
[Fact]
public void DockTabStrip_WhenEnterIsPressedOnFocusedTab_SelectsThatTab()
```

```csharp
[Fact]
public void DockLayoutEngine_WhenTabbedPanelIsRemoved_DisposesTabFocusTargets()
```

- [ ] **Step 2: Run the dock and tab tests to verify the new focus behavior is missing**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "DockableEntityKeyboardFocusTests|DockLayoutEngineKeyboardFocusTests|DockTabStripKeyboardFocusTests|DockTabStripTests"`

Expected: FAIL.

- [ ] **Step 3: Implement dock-group activation and dock-tab focus lifetime**

Make `DockableEntity` implement `IFocusGroup`:

```csharp
public class DockableEntity : EditorEntity, IFocusGroup {
    bool isKeyboardFocusActive;

    public IFocusGroup RootGroup => this;
    public int GroupOrder => 0;
    public bool CanReceiveFocus => Enabled;

    public bool ContainsScreenPoint(int2 point) {
        int left = (int)Math.Round(Position.X);
        int top = (int)Math.Round(Position.Y);
        int width = Size.X;
        int height = Size.Y + TitleBarHeight;
        return point.X >= left &&
               point.X < left + width &&
               point.Y >= top &&
               point.Y < top + height;
    }

    public void SetGroupActive(bool isActive) {
        isKeyboardFocusActive = isActive;
        panelOutline.BorderThickness = isActive ? PanelOutlineThickness : (IsDocked ? 0f : PanelOutlineThickness);
        panelOutline.BorderColor = isActive ? ThemeManager.Colors.AccentPrimary : PanelOutlineColor;
    }
}
```

Extend `DockLayoutEngine` with one public traversal method:

```csharp
public IReadOnlyList<DockableEntity> GetVisibleDockablesInTraversalOrder()
```

Implement it by walking the layout tree left-to-right / top-to-bottom and appending `PanelNode.Entity` for each leaf. Do not return hidden sibling tabs.

Update `DockTabEntry` to store one persistent focus target:

```csharp
public EditorFocusTarget FocusTarget { get; set; }
public bool IsKeyboardFocused { get; set; }
```

Update `DockTabStrip` so `EnsureTabCount` creates both the visual entry and its focus target once:

```csharp
entry.FocusTarget = new EditorFocusTarget(
    null,
    i,
    false,
    () => entry.Root.Enabled,
    point => ContainsTabPoint(entry, point),
    isFocused => { entry.IsKeyboardFocused = isFocused; UpdateTabVisual(entry, entry.Index == activeIndex); },
    key => key == Keys.Enter || key == Keys.Space,
    key => ActivateTab(entry.Index));
EditorKeyboardFocusService.RegisterTarget(entry.FocusTarget);
```

In `UpdateTabs`, rebind every visible tab target to the currently active dock group:

```csharp
DockableEntity activeDock = dockables[activeIndex];
entry.FocusTarget.FocusGroup = activeDock;
entry.FocusTarget.TabIndex = i;
```

Add these strip helpers:

```csharp
void ActivateTab(int index)
bool ContainsTabPoint(DockTabEntry entry, int2 point)
public void DisposeFocusTargets()
```

`DisposeFocusTargets()` must unregister every created tab focus target exactly once. Call it from `DockLayoutEngine.PanelNode.Remove` when the strip is being discarded because the panel collapsed.

Update `UpdateTabVisual` so the focus outline has priority over idle state but not over pressed state.

- [ ] **Step 4: Run the dock and tab tests to verify traversal and lifetime are correct**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "DockableEntityKeyboardFocusTests|DockLayoutEngineKeyboardFocusTests|DockTabStripKeyboardFocusTests|DockTabStripTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor/components/ui/dock/DockTabStrip.cs engine/helengine.editor/components/ui/dock/DockTabEntry.cs engine/helengine.editor.tests/DockableEntityKeyboardFocusTests.cs engine/helengine.editor.tests/managers/dock/DockLayoutEngineKeyboardFocusTests.cs engine/helengine.editor.tests/DockTabStripKeyboardFocusTests.cs engine/helengine.editor.tests/DockTabStripTests.cs
git commit -m "feat: add keyboard focus to docks and tab strips"
```

## Task 4: Convert Hierarchy Rows And Asset Browser Rows To Stable Focus Targets

**Files:**
- Create: `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserRow.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Create: `engine/helengine.editor.tests/SceneHierarchyPanelKeyboardFocusTests.cs`
- Create: `engine/helengine.editor.tests/AssetBrowserViewKeyboardFocusTests.cs`
- Modify: `engine/helengine.editor.tests/AssetBrowserViewGeneratedAssetTests.cs`

- [ ] **Step 1: Write failing tests for hierarchy selection and asset-browser focus**

Write tests that prove:

```csharp
[Fact]
public void SceneHierarchyPanel_WhenRowIsActivated_SelectsTheRepresentedEntity()
```

```csharp
[Fact]
public void SceneHierarchyPanel_WhenRowsAreRelaidOut_ReusesExistingFocusTargetsAndUpdatesTabIndex()
```

```csharp
[Fact]
public void AssetBrowserView_WhenDockGroupIsSupplied_RegistersTheUpButtonAndVisibleRows()
```

```csharp
[Fact]
public void AssetBrowserView_WhenEnterActivatesARow_UsesTheSameNavigationOrAssetActionAsMouseRelease()
```

```csharp
[Fact]
public void AssetBrowserView_WhenRowsShrink_KeepsPooledTargetsRegisteredButUnfocusable()
```

- [ ] **Step 2: Run the hierarchy and asset-browser tests to verify pooled focus targets are missing**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "SceneHierarchyPanelKeyboardFocusTests|AssetBrowserViewKeyboardFocusTests|AssetBrowserViewGeneratedAssetTests"`

Expected: FAIL.

- [ ] **Step 3: Replace nested row state with stable row objects and wire activation paths once**

Create `SceneHierarchyRow` as a persistent pooled row object:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Bundles one hierarchy row's visuals, interaction state, and keyboard-focus target.
    /// </summary>
    public sealed class SceneHierarchyRow {
        public SceneHierarchyRow(
            EditorEntity entity,
            SpriteComponent background,
            EditorEntity labelHost,
            TextComponent label,
            InteractableComponent interactable,
            EditorFocusTarget focusTarget) {
            Entity = entity;
            Background = background;
            LabelHost = labelHost;
            Label = label;
            Interactable = interactable;
            FocusTarget = focusTarget;
            BaseColor = ThemeManager.Colors.SurfacePrimary;
        }

        public EditorEntity Entity { get; }
        public SpriteComponent Background { get; }
        public EditorEntity LabelHost { get; }
        public TextComponent Label { get; }
        public InteractableComponent Interactable { get; }
        public EditorFocusTarget FocusTarget { get; }
        public Entity NodeEntity { get; set; }
        public byte4 BaseColor { get; set; }
        public bool IsHovering { get; set; }
        public bool IsPressed { get; set; }
        public bool IsKeyboardFocused { get; set; }
    }
}
```

Update `SceneHierarchyPanel` so `CreateRow()` creates and registers one `EditorFocusTarget` per pooled row. Reuse a single activation path for mouse and keyboard:

```csharp
void ActivateRow(SceneHierarchyRow row) {
    if (row == null || row.NodeEntity == null) {
        return;
    }

    EditorSelectionService.SetSelectedEntity(row.NodeEntity);
}
```

Use that in both the mouse handler and the focus target:

```csharp
row.FocusTarget = new EditorFocusTarget(
    this,
    0,
    false,
    () => row.Entity.Enabled && row.NodeEntity != null,
    point => ContainsHierarchyRowPoint(row, point),
    isFocused => { row.IsKeyboardFocused = isFocused; UpdateRowBackground(row, row.BaseColor); },
    key => key == Keys.Enter,
    key => ActivateRow(row));
EditorKeyboardFocusService.RegisterTarget(row.FocusTarget);
```

Then, in `LayoutRows()`, only update `row.FocusTarget.TabIndex = i;` and `row.NodeEntity = node.Entity;`. Do not recreate focus targets during refresh.

Update `UpdateRowBackground` so focus outline is visible even when the row is not hovered.

For `AssetBrowserView`, change the constructor signature to accept the owning dock group:

```csharp
public AssetBrowserView(
    FontAsset font,
    string projectPath,
    ushort layerMask,
    byte toolbarOrder,
    byte rowBackgroundOrder,
    byte iconBackgroundOrder,
    byte textOrder,
    bool includeGeneratedEntries = true,
    IFocusGroup focusGroup = null)
```

Store that `FocusGroup` and use it for:

- `UpButton.FocusGroup = focusGroup;`
- `UpButton.TabIndex = 0;`
- each pooled row `FocusTarget.FocusGroup = focusGroup;`
- each pooled row `FocusTarget.TabIndex = i + 1;`

Extend `AssetBrowserRow` with:

```csharp
public EditorFocusTarget FocusTarget { get; }
public bool IsKeyboardFocused { get; set; }
```

In `CreateRow()`, register the focus target once. In `LayoutRows()`, set `row.Entry`, `row.FocusTarget.TabIndex`, and `row.Entity.Enabled`. Keep the target registered even when a row is currently unused.

Update `AssetBrowserPanel` to pass `this` into `AssetBrowserView`.

- [ ] **Step 4: Run the hierarchy and asset-browser tests to verify pooling and activation work**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "SceneHierarchyPanelKeyboardFocusTests|AssetBrowserViewKeyboardFocusTests|AssetBrowserViewGeneratedAssetTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/SceneHierarchyRow.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/asset/AssetBrowserRow.cs engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor.tests/SceneHierarchyPanelKeyboardFocusTests.cs engine/helengine.editor.tests/AssetBrowserViewKeyboardFocusTests.cs engine/helengine.editor.tests/AssetBrowserViewGeneratedAssetTests.cs
git commit -m "feat: add keyboard focus to hierarchy and asset browser"
```

## Task 5: Add Viewport Subgroups, Toolbar Focus, And `W` / `R` / `S` Gating

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Create: `engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs`

- [ ] **Step 1: Write failing viewport focus tests**

Write tests that prove:

```csharp
[Fact]
public void EditorViewport_WhenContentTargetIsFocused_WAndRAndSChangeToolMode()
```

```csharp
[Fact]
public void EditorViewport_WhenRightMouseButtonIsPressed_SIsIgnored()
```

```csharp
[Fact]
public void EditorViewport_WhenToolbarButtonsReceiveFocus_EnterAndSpaceActivateThem()
```

```csharp
[Fact]
public void EditorViewport_WhenMouseHitsContent_TheViewportDockBecomesActiveAndContentTargetFocused()
```

- [ ] **Step 2: Run the viewport tests to verify subgroup focus and key gating are missing**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorViewportKeyboardFocusTests`

Expected: FAIL.

- [ ] **Step 3: Add persistent toolbar/content focus targets to `EditorViewport`**

Create two nested groups in the viewport constructor:

```csharp
readonly EditorFocusGroup ContentFocusGroup;
readonly EditorFocusGroup ToolbarFocusGroup;
readonly EditorFocusTarget ViewportContentFocusTarget;
readonly EditorFocusTarget[] ToolButtonFocusTargets;
readonly EditorFocusTarget[] SnapIncreaseFocusTargets;
readonly EditorFocusTarget[] SnapDecreaseFocusTargets;
readonly bool[] ToolButtonKeyboardFocusStates;
readonly bool[] SnapIncreaseKeyboardFocusStates;
readonly bool[] SnapDecreaseKeyboardFocusStates;
```

Initialize the groups once:

```csharp
ContentFocusGroup = new EditorFocusGroup(this, 0, () => Enabled, ContainsViewportContentPoint, HandleSubviewGroupActiveChanged);
ToolbarFocusGroup = new EditorFocusGroup(this, 1, () => Enabled, ContainsToolbarPoint, HandleSubviewGroupActiveChanged);
EditorKeyboardFocusService.RegisterGroup(ContentFocusGroup);
EditorKeyboardFocusService.RegisterGroup(ToolbarFocusGroup);
```

Register the content target once:

```csharp
ViewportContentFocusTarget = new EditorFocusTarget(
    ContentFocusGroup,
    0,
    true,
    () => Enabled,
    ContainsViewportContentPoint,
    isFocused => isViewportContentFocused = isFocused,
    key => {
        if (Core.Instance.InputManager.GetMouseRightButtonState() == ButtonState.Pressed) {
            return false;
        }

        return key == Keys.W || key == Keys.R || key == Keys.S;
    },
    key => {
        if (key == Keys.W) {
            ToolMode = EditorViewportToolMode.Translate;
        } else if (key == Keys.R) {
            ToolMode = EditorViewportToolMode.Rotate;
        } else if (key == Keys.S) {
            ToolMode = EditorViewportToolMode.Scale;
        }
    });
EditorKeyboardFocusService.RegisterTarget(ViewportContentFocusTarget);
```

In `CreateToolButton` and `CreateSnapToolbarButton`, create matching focus targets under `ToolbarFocusGroup`. Use `Enter` and `Space` as activation keys and update the existing visual methods with new keyboard-focus state arrays.

Add these exact helpers:

```csharp
bool ContainsViewportContentPoint(int2 point)
bool ContainsToolbarPoint(int2 point)
bool ContainsToolbarButtonPoint(int buttonIndex, int2 point)
bool ContainsSnapButtonPoint(int slotIndex, bool isIncreaseButton, int2 point)
```

Do not register or unregister these targets during layout; create them once in the constructor and only change their visual/tab state.

- [ ] **Step 4: Run the viewport tests to verify shortcut gating and toolbar focus pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorViewportKeyboardFocusTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor.tests/EditorViewportKeyboardFocusTests.cs
git commit -m "feat: add viewport keyboard focus and shortcuts"
```

## Task 6: Wire The Session Update Loop, Publish Dock Order, And Verify End To End

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`

- [ ] **Step 1: Write failing session integration tests**

Write tests that prove:

```csharp
[Fact]
public void UpdateLayout_WhenCalled_PublishesVisibleDockOrderToTheFocusService()
```

```csharp
[Fact]
public void Update_WhenCtrlTabIsPressed_MovesActivationToTheNextVisibleDock()
```

```csharp
[Fact]
public void Dispose_WhenCalled_ResetsStaticKeyboardFocusState()
```

Use the repo's existing style for reflection inside the test file instead of adding test-only APIs to production code.

- [ ] **Step 2: Run the session integration tests to verify wiring is still missing**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionKeyboardFocusIntegrationTests`

Expected: FAIL.

- [ ] **Step 3: Add the session wiring**

In the `EditorSession` constructor, reset the service before creating UI and add one internal focus-update entity:

```csharp
readonly EditorEntity keyboardFocusEntity;
```

```csharp
EditorKeyboardFocusService.Reset();
keyboardFocusEntity = new EditorEntity {
    InternalEntity = true,
    Enabled = true,
    LayerMask = EditorLayerMasks.EditorUi
};
var keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
    UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1)
};
keyboardFocusEntity.AddComponent(keyboardFocusUpdateComponent);
```

In `UpdateLayout`, publish visible dock order after the dock layout has been applied:

```csharp
dockingManager.Layout.Layout(new int2(width, availableHeight), new float3(0, titleBar.Height, 0));
EditorKeyboardFocusService.SetDockOrder(dockingManager.Layout.GetVisibleDockablesInTraversalOrder());
gizmoCameraComponent.Viewport = sceneCameraComponent.Viewport;
```

In `Dispose()`, clear static focus state after the existing handler teardown:

```csharp
EditorKeyboardFocusService.Reset();
core.Dispose();
```

- [ ] **Step 4: Run the integration slice that exercises the whole stack**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSessionKeyboardFocusIntegrationTests|EditorViewportKeyboardFocusTests|SceneHierarchyPanelKeyboardFocusTests|AssetBrowserViewKeyboardFocusTests|ButtonComponentKeyboardFocusTests|TextBoxComponentKeyboardFocusTests|ComboBoxComponentKeyboardFocusTests|DockableEntityKeyboardFocusTests|DockLayoutEngineKeyboardFocusTests|DockTabStripKeyboardFocusTests|EditorKeyboardFocusServiceTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs
git commit -m "feat: wire keyboard focus through editor session"
```

## Final Verification

- [ ] **Step 1: Run the focused regression slice**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSessionKeyboardFocusIntegrationTests|EditorViewportKeyboardFocusTests|SceneHierarchyPanelKeyboardFocusTests|AssetBrowserViewKeyboardFocusTests|ButtonComponentKeyboardFocusTests|TextBoxComponentKeyboardFocusTests|ComboBoxComponentKeyboardFocusTests|DockableEntityKeyboardFocusTests|DockLayoutEngineKeyboardFocusTests|DockTabStripKeyboardFocusTests|EditorKeyboardFocusServiceTests|AssetBrowserViewGeneratedAssetTests|DockTabStripTests|EditorSessionStartupSceneTests|InputManagerTests"`

Expected: PASS.

- [ ] **Step 2: Sanity-check the editor manually**

Run the editor and verify:

```text
1. Left click viewport content, then press W, R, and S.
2. Hold right mouse in the viewport and press S; the tool mode must not change.
3. Press Ctrl+Tab and Ctrl+Shift+Tab across the visible dock panels.
4. Press Tab and Shift+Tab inside the active dock.
5. Press Enter and Space on focused toolbar buttons, dock tabs, and shared buttons.
6. Press Enter on a hierarchy row and confirm it selects the represented entity.
7. Press Enter on an asset-browser row and confirm it navigates or opens exactly like a mouse click.
8. Confirm the active dock and the focused control both keep their outline after the mouse stops moving.
```

Expected: keyboard focus, mouse synchronization, dock traversal, and viewport shortcut gating all behave exactly as described by the design spec.

## Self-Review Notes

Spec coverage check:

- Central service: Task 1
- `TabIndex` traversal and reverse traversal: Task 1, Task 6
- `Ctrl+Tab` and `Ctrl+Shift+Tab`: Task 1, Task 3, Task 6
- Dock and control outlines: Task 2, Task 3, Task 4, Task 5
- Mouse-to-keyboard synchronization: Task 1, Task 5, Task 6
- `Enter` and `Space` activation: Task 2, Task 3, Task 5
- Viewport-local `W` / `R` / `S`: Task 5
- Right-mouse suppression for viewport shortcuts: Task 5
- Focus fallback when targets disappear or disable: Task 1, Task 2, Task 4, Task 6

Placeholder scan result:

- No placeholder markers or unnamed helper APIs remain.
- The previously missing helpers are now explicit files: `TestFocusGroup`, `TestFocusTarget`, and `SceneHierarchyRow`.
- Session tests rely on reflection inside the test file instead of invented production accessors.

Type consistency check:

- `RegisterGroup` / `UnregisterGroup` and `RegisterTarget` / `UnregisterTarget` are defined in Task 1 and referenced consistently afterward.
- `SetDockOrder(IReadOnlyList<DockableEntity>)` is defined in Task 1 and fed by `DockLayoutEngine.GetVisibleDockablesInTraversalOrder()` in Tasks 3 and 6.
- Pooled rows and tabs use persistent `EditorFocusTarget` instances everywhere in the plan; no later task reintroduces recreate-on-layout behavior.

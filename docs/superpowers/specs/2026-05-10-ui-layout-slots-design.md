# UI Layout Slots And Multi-Instance Panels

## Summary

Add one built-in `UI` top-level title-bar menu that lets the editor:

- open new panel instances through `UI -> Show`
- save the current workspace through `UI -> Save -> Slot 1..5`
- load a saved workspace through `UI -> Load -> Slot 1..5`

The workspace system must support:

- multiple instances of every panel type
- closing individual panel instances
- restoring docked and floating panel arrangements
- restoring tab groups and active tabs
- restoring panel-specific state where needed

Layout data will be stored in `user_settings/layout.json`.

## Goals

- Allow users to save and load up to five local editor UI workspaces per project.
- Allow users to open multiple instances of any panel type, including additional viewports, previews, asset browsers, properties panels, loggers, and scene hierarchy panels.
- Allow users to close any panel instance independently.
- Make `Preview` instances optionally lock to the currently shown asset or camera.
- Make every `Viewport` instance fully independent in camera, gizmo, picker, and local presentation state.
- Keep workspace persistence local to the current user and project.

## Non-Goals

- No `Exit` title-bar menu item is added as part of this work.
- No attempt is made to preserve closed panels outside saved layout slots.
- No hidden singleton panel pool is kept around after closing one panel instance.
- No cross-project shared workspace persistence is added.

## User Experience

### UI Menu

Add one new built-in `UI` top-level title-bar menu beside the existing built-in menus.

The menu contains:

- `Show`
- `Save`
- `Load`

`Show` contains one item for every registered panel type. Choosing one item creates one new undocked centered instance of that panel type.

`Save` contains:

- `Slot 1`
- `Slot 2`
- `Slot 3`
- `Slot 4`
- `Slot 5`

Choosing one save slot overwrites that slot in `user_settings/layout.json` with the current live workspace.

`Load` contains:

- `Slot 1`
- `Slot 2`
- `Slot 3`
- `Slot 4`
- `Slot 5`

Choosing one load slot replaces the current live workspace with the saved workspace from that slot.

### Panel Close

Every panel instance gets a title-bar menu strip entry with one `Close` action.

Choosing `Close`:

- removes that panel instance from the dock layout or floating workspace
- disposes that instance
- does not affect sibling instances of the same panel type

### Reopening Panels

Reopening a panel through `UI -> Show` always creates a new undocked centered instance with its default size.

It does not try to resurrect the last closed position unless that position is being restored through a saved layout slot.

## Panel Type Registry

Introduce a registry of creatable panel types. Each panel type descriptor provides:

- stable `PanelTypeId`
- user-visible menu label
- factory used to create a new panel instance
- default undocked size
- panel-state capture and restore hooks

This registry becomes the single source for:

- `UI -> Show` menu items
- layout deserialization of saved panel instances
- future panel-type expansion

## Panel Instances

Replace the current fixed-editor-shell assumption that core panels exist as one singleton instance.

Each open panel becomes one live instance record with:

- stable `InstanceId`
- `PanelTypeId`
- display title
- backing `DockableEntity`
- panel controller or state adapter

`EditorSession` will manage a live collection of panel instances instead of relying only on fixed fields for all user-visible panels.

Shared editor state remains global unless a panel type explicitly owns local state.

Examples:

- multiple `Properties` panels can all reflect the same global current selection
- multiple `Asset Browser` panels can carry different local current-folder state
- multiple `Viewport` panels can carry different local camera state
- multiple `Preview` panels can carry different local lock targets and interaction state

## Viewport Independence

`Viewport` instances are not lightweight clones of one shared editor camera.

Each viewport panel must own its own runtime editing stack:

- one scene camera entity and camera component
- one gizmo-overlay camera component paired to that scene camera
- one hidden picker camera and render target
- one viewport panel UI instance
- one viewport-local camera controller
- one viewport-local translation, rotation, and scale gizmo drag path
- one viewport-local canvas preview settings object

This ownership model is required so multiple viewports can stay open simultaneously without fighting over:

- viewport rectangles
- camera transforms
- gizmo rendering
- picker state
- toolbar state
- input focus

### Global Versus Local Viewport State

Global editor state remains shared:

- current scene contents
- current asset selection
- current entity selection
- current transform target

Viewport-local state is independent per instance:

- editor camera transform
- gizmo tool mode
- snap values
- grid visibility
- viewport settings overlay state
- canvas preview settings
- toolbar focus and input state

Changing one viewport's camera or tool mode must not change any sibling viewport instance.

### Viewport Selection Behavior

Entity selection remains global across the editor.

Any viewport may change the global selection, and all other panels continue to observe that shared selection.

Only the focused viewport handles local camera movement, local gizmo dragging, and local viewport shortcuts.

## Dock And Float Persistence

Persist the workspace as:

- the full dock split tree
- tab groups and active tab selection
- floating panels with position and size
- the full set of open panel instances

The saved model must be able to express:

- split direction
- split fraction
- leaf tab collections
- active tab index
- floating window bounds
- per-instance panel state payload

## Preview Behavior

Each `Preview` panel instance has two modes:

- unlocked
- locked

### Unlocked Preview

When unlocked, the preview follows the latest clicked target from editor selection flow.

Supported targets:

- latest clicked asset
- latest clicked camera

### Locked Preview

When locked, the preview stays bound to whatever target it was showing when the lock was enabled.

If the locked target is deleted, unloaded, or no longer resolves, the preview clears immediately.

Unlocking resumes normal follow behavior from the next eligible clicked asset or camera.

### Preview Toolbar

Add a small toolbar to `Preview`, visually consistent with the viewport toolbar direction.

The first icon is a lock icon.

Behavior:

- clicking lock while something is shown locks that current target
- clicking lock while nothing is shown does nothing
- clicking again unlocks it
- lock state is stored per preview instance

## Persistence Format

Store workspace layouts in `user_settings/layout.json`.

Suggested top-level structure:

```json
{
  "slots": {
    "slot1": {},
    "slot2": {},
    "slot3": {},
    "slot4": {},
    "slot5": {}
  }
}
```

Each slot stores:

- schema version
- open panel instances
- dock tree
- floating panel records

Each saved panel instance stores:

- `instanceId`
- `panelTypeId`
- title metadata if needed
- docked or floating state
- floating bounds when applicable
- panel-specific state payload

For `Viewport` instances, the saved panel-specific state must include:

- camera position
- camera orientation
- tool mode
- snap values
- grid visibility
- canvas preview settings

Each dock tree node stores either:

- split node data
- leaf tab-group data

Leaf tab-group data stores:

- ordered instance ids
- active tab instance id or active tab index

If a saved panel type cannot be created during load, skip that instance and continue loading the rest of the workspace.

## Save Flow

When the user chooses `UI -> Save -> Slot N`:

1. Gather the current live panel instance set.
2. Capture current dock split-tree state from the docking system.
3. Capture floating panel bounds.
4. Capture each panel instance state payload.
5. Serialize the complete workspace into `slotN`.
6. Write the updated `user_settings/layout.json` file.

## Load Flow

When the user chooses `UI -> Load -> Slot N`:

1. Read `user_settings/layout.json`.
2. Resolve `slotN`.
3. Tear down the current live workspace.
4. Recreate saved panel instances from the registry.
5. Restore floating panels first or reconstruct the dock tree using saved instance ids.
6. Restore panel-specific state payloads.
7. Rebind editor services that depend on panel instances.

Loading one slot fully replaces the current live workspace.

## Docking System Changes

The docking system needs serialization support for:

- split nodes
- panel leaf nodes
- tab groups
- active tab selection

It also needs an explicit way to rebuild a saved dock tree from existing live panel instances.

The dock model must stop assuming a leaf only contains one permanent built-in panel. It must operate on arbitrary panel instance ids supplied by the session.

## Editor Session Changes

`EditorSession` needs a panel-instance management layer responsible for:

- registering built-in panel types
- creating new panel instances
- disposing closed instances
- routing `UI` menu actions
- saving and loading workspace slots
- handling default startup layout creation when no slot is loaded

For `Viewport`, this also requires removing the current assumption that one shared session-owned viewport stack exists.

Session code that currently assumes singleton ownership of:

- the main viewport panel
- the editor scene camera
- the gizmo camera
- the hidden picker camera
- viewport-local camera controller behavior
- viewport-local gizmo drag components

must move behind one viewport-instance controller so those resources are created and destroyed per viewport instance.

Any existing code that directly references one fixed panel field for behavior must be reviewed and moved either to:

- a panel-type-specific service
- a current-primary-instance rule
- a broadcast across instances

The viewport path should not use a current-primary-instance rule for camera ownership. It needs one explicit per-instance controller.

The session may still keep references to special infrastructure services, but user-facing panel lifetime must move to instance management.

## Compatibility And Migration

- `user_settings/layout.json` is new and local-only.
- Missing layout file means use the current default startup layout.
- Missing slot means loading that slot is a no-op or user-facing warning, depending on current editor messaging conventions.
- Unknown panel types inside an existing saved layout are ignored.
- Unknown panel-state fields are ignored when possible.

## Testing Strategy

Add coverage for:

- `UI` menu creation and routing
- creating one panel instance from every registered panel type
- creating duplicate instances of the same panel type
- closing one panel instance without affecting other instances
- creating duplicate viewport instances with independent camera state
- changing one viewport tool mode without mutating sibling viewports
- restoring multiple saved viewport instances with different local state
- saving and loading dock trees with split nodes
- saving and loading tab groups with active tab restoration
- saving and loading floating panels
- skipping unknown panel types during load
- preview lock behavior for assets
- preview lock behavior for cameras
- preview clear behavior when a locked target becomes invalid

## Implementation Notes

- Start by introducing panel descriptors and live panel instance records before changing menu behavior.
- Keep persistence versioned from the first revision.
- Keep preview lock logic inside preview-specific state and selection-binding code, not in generic docking code.
- Prefer explicit serialization DTOs for layout data rather than writing dock runtime objects directly.
- Treat viewport camera, gizmo, picker, and toolbar ownership as one unit of encapsulation. Avoid partial sharing between viewport instances.

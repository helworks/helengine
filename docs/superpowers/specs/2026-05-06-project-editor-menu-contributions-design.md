# Project Editor Menu Contributions Design

## Summary

Add a reusable editor-only menu contribution system that lets project-authored editor modules add top-level menu-strip entries to the editor UI. The first concrete use is a `Demo` top-level menu with one item, `Regenerate Main Menu...`, backed by the existing project-authored editor command `menu.regenerate-demo-disc-main-menu`.

This feature must support hot reload cleanly. When project code changes and the user rebuilds scripts, contributed menus must be rebuilt from the newly loaded editor assemblies and must replace the previous contributed menu set without duplicating entries or requiring an editor restart.

## Goals

- Let project-authored editor modules contribute menu-strip entries declaratively.
- Add `Demo -> Regenerate Main Menu...` for the current city workflow.
- Reuse the existing editor-command system instead of introducing a second execution path.
- Ensure contributed menus are replaced on script reload rather than appended.
- Keep built-in editor menus (`File`, `Add`, `Build`) separate from project-contributed menus.

## Non-Goals

- General toolbar contribution support.
- Nested arbitrary multi-level menus beyond one top-level menu and one level of items.
- Automatic scene reload after regeneration.
- User-customizable menu placement beyond stable ordering metadata.

## User Experience

After project editor modules are built and loaded, the title bar shows a new top-level `Demo` menu beside the existing built-in menus. Opening `Demo` shows one item:

- `Regenerate Main Menu...`

Selecting that item executes the existing project-authored command `menu.regenerate-demo-disc-main-menu`, which regenerates `Scenes/DemoDiscMainMenu.helen` through the editor-side menu scene regeneration service.

If the project editor modules are not yet loaded, the `Demo` menu is not shown. If the user edits project code and triggers `Build Scripts`, the menu contributions are rebuilt from the freshly loaded assemblies. Removed contributions disappear, changed labels update, and existing items are not duplicated.

## Architecture

### 1. Editor menu contribution contract

Add a new editor-side discovery contract for editor modules, parallel to `IEditorCommand`.

Recommended shape:

- `IEditorMenuItemProvider`
- one method that returns `IReadOnlyList<EditorMenuItemDescriptor>`

Each descriptor contains:

- `TopLevelMenuId`
- `TopLevelMenuLabel`
- `TopLevelMenuOrder`
- `MenuItemId`
- `MenuItemLabel`
- `MenuItemOrder`
- `CommandId`

The provider contract is editor-only and is discoverable only from assemblies whose module kind is `editor`.

### 2. Editor assembly discovery

Extend `EditorGameScriptAssemblyHost` so it discovers menu providers from loaded editor assemblies in the same pass used to discover editor commands. The host should expose a catalog method such as:

- `GetAvailableEditorMenuItems()`

This returns a fully materialized descriptor list built from all loaded editor-only assemblies.

The catalog must be reconstructed from scratch after each reload. It must not incrementally append to previous state.

### 3. Session-owned contributed menu state

`EditorSession` owns the active contributed menu catalog for the running editor instance.

Flow:

1. Before scripts are loaded, the contributed catalog is empty.
2. After `Build Scripts` succeeds and assemblies are reloaded, the session queries the menu catalog from the script host.
3. The session validates the contributed menu descriptors.
4. The session pushes a complete replacement set into `EditorTitleBar`.

The session also maps menu item activation back to the existing editor-command execution service using the descriptor `CommandId`.

### 4. Title-bar rendering

`EditorTitleBar` is extended to render a variable set of contributed top-level menus in addition to its built-in menus.

The title bar must:

- keep built-in menus hardcoded and stable
- render contributed top-level menus from descriptor input
- create and destroy contributed menu button entities from a fresh descriptor set
- remove the previous contributed menu UI before applying the next set

The first result is a top-level `Demo` button containing the single `Regenerate Main Menu...` item.

## Hot Reload Behavior

Contributed menus are derived state from loaded editor assemblies.

Rules:

- No project-contributed menus are persisted in editor preferences or project settings.
- A reload completely replaces the prior contributed menu catalog and UI.
- Stable ids are used for validation and routing, but the UI is rebuilt fresh on every reload.
- Removing a provider from project code removes its menus after the next successful script build and reload.
- Changing labels or ordering in project code updates the visible menu after reload.

This ensures the editor can return to a fresh post-reload state without restarting and without duplicate menu entries.

## Validation Rules

Every contributed descriptor must provide:

- non-empty `TopLevelMenuId`
- non-empty `TopLevelMenuLabel`
- non-empty `MenuItemId`
- non-empty `MenuItemLabel`
- non-empty `CommandId`

Failure conditions:

- duplicate `MenuItemId` across all contributed descriptors
- duplicate top-level menu ids with conflicting labels
- contributed item references a command id that does not exist in the current editor-command catalog

These failures should be surfaced loudly during catalog rebuild. The editor should not silently render partial or ambiguous menu state.

## Execution Flow

When `Regenerate Main Menu...` is clicked:

1. The title bar closes its open menus.
2. `EditorSession` receives the contributed menu item activation with `CommandId = menu.regenerate-demo-disc-main-menu`.
3. The session executes that command through the existing `EditorCommandExecutionService`.
4. The command calls the existing `EditorMenuSceneRegenerationService`.
5. The service rewrites `Scenes/DemoDiscMainMenu.helen`.

If command execution fails, the error is surfaced through the same reporting path used for other script/editor command failures. No menu duplication or partial contribution state remains after failure.

## City Project First Use

The `city` project editor module `menu.tools` provides:

- top-level menu id: `demo`
- top-level label: `Demo`
- item id: `demo.regenerate-main-menu`
- item label: `Regenerate Main Menu...`
- command id: `menu.regenerate-demo-disc-main-menu`

This keeps the project-specific menu definition in project code rather than hardcoding the `Demo` menu into the engine.

## Testing

Add or update tests for:

- title bar renders no contributed menus before editor modules are loaded
- one editor module contributes `Demo -> Regenerate Main Menu...`
- clicking the contributed menu item invokes the mapped command id exactly once
- rebuilding scripts replaces contributed menus instead of duplicating them
- removing the provider removes the contributed menu after reload
- duplicate contributed item ids fail during rebuild
- missing backing command ids fail during rebuild

## Risks

- `EditorTitleBar` currently assumes only built-in top-level menus. Care is needed to keep layout and hover-switch behavior correct when contributed menus are present.
- Reload-time menu replacement must destroy old entities cleanly so stale interactables do not remain in the UI tree.
- The menu contribution system must stay constrained to editor modules only, otherwise runtime/gameplay modules could leak editor-specific UI behavior into the host.

## Recommendation

Implement the feature as a generic project-contributed menu system now, but keep the UI scope intentionally narrow: top-level contributed menus with one level of clickable items. That gives the `Demo` menu immediately while establishing a clean reusable contract for future project tooling without overbuilding the editor menu model.

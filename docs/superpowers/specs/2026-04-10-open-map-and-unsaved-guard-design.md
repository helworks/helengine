# Open Map And Unsaved Changes Guard Design

## Summary

This document defines the first scene-opening workflow for the editor. The `File` menu gains `Open Map...`, and both `Open Map...` and `New Map` are protected by an unsaved-changes guard before the current scene is discarded.

The design keeps scene navigation centralized in `EditorSession`. The session owns current-scene document state, decides when a scene is dirty, routes `Save` and `Save As`, and coordinates guarded transitions to a new empty scene or a loaded `.helen` scene.

## Goals

- Add `Open Map...` to the main `File` menu.
- Allow users to open `.helen` scene assets from inside the project `assets` folder.
- Add an editor-owned unsaved-changes prompt with `Save`, `Don't Save`, and `Cancel`.
- Use the same unsaved-changes guard for both `Open Map...` and `New Map`.
- Keep the current scene intact when opening a different scene fails.
- Keep scene navigation logic out of `EditorTitleBar`.
- Reuse the existing scene save flow instead of duplicating save logic for guarded transitions.

## Non-Goals

- No native operating-system file dialogs in this phase.
- No multi-document scene tabs.
- No recent-files menu or scene history.
- No autosave or crash recovery in this slice.
- No comparison-by-snapshot dirty detection.
- No scene-merge workflow or additive scene loading.

## Current Problem

The editor can now save `.helen` scene assets, but it still cannot open one back into the active session from the title-bar file menu.

Current gaps:

- `EditorTitleBar` exposes `New Map`, `Save Map`, and `Save Map As...`, but not `Open Map...`.
- `EditorSession` tracks `CurrentScenePath`, but it does not own a real scene-document navigation flow.
- `SceneLoadService` exists, but there is no user-facing command path that reads a `.helen` file and swaps the active scene.
- There is no unsaved-changes guard, so adding open/new behavior without one would allow silent loss of user edits.
- The editor does not currently model a dirty scene state explicitly.

## Proposed Architecture

### 1. Scene Document State Owned By EditorSession

`EditorSession` will become the owner of active-scene document state.

Tracked state:

- `CurrentScenePath`
- `IsSceneDirty`
- one pending scene-transition action when the editor is waiting for the user to answer the unsaved-changes prompt

`EditorSession` remains the coordinator for:

- save
- save as
- open
- new scene
- guarded scene transitions
- hierarchy refresh
- selection reset

This keeps scene navigation rules in one place instead of splitting them across title-bar UI, modal dialogs, and scene serialization helpers.

### 2. Open Map As A File-Menu Command

`EditorTitleBar` will add `Open Map...` to the `File` menu beside the existing scene commands.

The title bar remains presentation-only. It raises a new high-level event for the command, and `EditorSession` handles the actual behavior.

Recommended file-menu order:

- `New Map`
- `Open Map...`
- `Save Map`
- `Save Map As...`

### 3. Editor-Owned OpenFileDialog

The editor will introduce an `OpenFileDialog` that matches the overall modal style of `SaveFileDialog`.

Behavior:

- rooted under the project `assets` folder
- browses directories and files inside the project
- filters visible/selectable files to `.helen`
- returns one absolute file path when the user confirms a selection
- shows validation or load errors inside the dialog UI

This keeps scene opening consistent with the editor's existing asset-aware modal patterns instead of jumping to platform-native dialogs.

### 4. Editor-Owned UnsavedChangesDialog

The editor will introduce a small confirmation modal dedicated to scene-discard decisions.

Buttons:

- `Save`
- `Don't Save`
- `Cancel`

Meaning:

- `Save` persists the current scene first, then continues the pending `Open Map...` or `New Map` action
- `Don't Save` continues immediately without saving
- `Cancel` aborts the pending action and keeps the current scene active

This dialog is intentionally scene-specific and does not attempt to be a generic message-box system in this phase.

### 5. Guarded Scene Transitions

`New Map` and `Open Map...` both route through the same guarded transition flow.

Rules:

- If `IsSceneDirty == false`, the requested action continues immediately.
- If `IsSceneDirty == true`, the editor shows `UnsavedChangesDialog`.
- If the user chooses `Save` and the scene already has a path, the editor saves to that path and then continues.
- If the user chooses `Save` and the scene does not yet have a path, the editor opens `SaveFileDialog`; after a successful save it continues the pending action.
- If the user chooses `Don't Save`, the editor continues without saving.
- If the user chooses `Cancel`, the editor clears the pending action and does nothing else.

The pending transition must stay explicit so the editor can resume the correct operation after a successful save.

### 6. Dirty Tracking As Explicit Session State

The editor will not infer dirty state by serializing the whole scene and comparing snapshots.

Instead, `EditorSession` owns explicit dirty-state transitions through dedicated helpers such as:

- mark scene dirty
- mark scene clean after save/load/new
- request guarded transition
- continue pending transition

For the first implementation slice, the dirty flag must be updated by editor flows that already own scene mutations directly, including:

- `Add > Empty`
- `Add > Cube`
- `Add > Plane`
- transform changes made through editor gizmo interactions
- scene-property/component changes initiated by editor property editing flows
- future scene-edit commands that already pass through `EditorSession` or dedicated editor services

This is a deliberate editor-state model, not a best-effort afterthought.

### 7. Scene Loading Flow

Scene loading should preserve the current scene until the new scene has been parsed and materialized successfully.

Recommended flow:

1. Validate the selected path.
2. Read and deserialize the target `.helen` into `SceneAsset`.
3. Materialize new root entities through `SceneLoadService`.
4. Only after successful materialization, clear current user-authored scene entities.
5. Attach the loaded roots to the live scene.
6. Refresh hierarchy and clear or update selection.
7. Update `CurrentScenePath`.
8. Mark the scene clean.

If any step fails before the swap point, the current scene remains active.

### 8. Clearing A Scene Means Removing Only User Entities

`New Map` and `Open Map...` must clear only user-authored scene entities.

They must preserve:

- editor cameras
- gizmos
- dock panels
- title-bar UI
- modal dialogs
- other `InternalEntity` editor infrastructure

This matches the same user-vs-editor boundary already used by scene saving.

## Data Flow

### New Map Without Unsaved Changes

1. User selects `New Map`.
2. `EditorTitleBar` raises the new-map event.
3. `EditorSession` sees `IsSceneDirty == false`.
4. The session removes current user scene entities.
5. The session clears selection, clears `CurrentScenePath`, marks clean, and refreshes the hierarchy.

### Open Map Without Unsaved Changes

1. User selects `Open Map...`.
2. `EditorTitleBar` raises the open-map event.
3. `EditorSession` sees `IsSceneDirty == false`.
4. The session shows `OpenFileDialog`.
5. The user chooses one `.helen` file.
6. The session loads and materializes the new scene.
7. The session swaps the live scene, updates `CurrentScenePath`, marks clean, and refreshes the hierarchy.

### Guarded New Map Or Open Map

1. User selects `New Map` or `Open Map...`.
2. `EditorSession` records that requested transition as pending.
3. If the scene is dirty, the session shows `UnsavedChangesDialog`.
4. The user picks `Save`, `Don't Save`, or `Cancel`.
5. The session follows the guarded-transition rules and either continues or aborts.

### Save Then Continue

1. A pending transition exists.
2. The user selects `Save` in `UnsavedChangesDialog`.
3. If `CurrentScenePath` exists, `EditorSession` uses the normal save flow immediately.
4. If `CurrentScenePath` is empty, the session opens `SaveFileDialog`.
5. After save succeeds, the session marks the scene clean and resumes the pending transition automatically.

## Error Handling

The editor must avoid destroying the active scene when the target scene cannot be opened.

Rules:

- If the user cancels `OpenFileDialog`, no scene transition occurs.
- If the user cancels `UnsavedChangesDialog`, the pending action is cleared and the current scene stays active.
- If the user chooses `Save` but save fails, the pending transition remains unresolved and the current scene stays active.
- If the selected `.helen` path is invalid, the open dialog stays open and shows an explicit error.
- If reading, deserializing, or materializing the target scene fails, the current scene remains untouched and the editor reports the failure clearly.
- `CurrentScenePath` updates only after a successful scene swap.
- `IsSceneDirty` is cleared only after a successful save, successful load, or successful `New Map` reset.

## Testing Requirements

The implementation must include coverage for:

1. `EditorTitleBar` exposing `Open Map...` in the `File` menu.
2. `OpenFileDialog` filtering scene choices to `.helen`.
3. `EditorSession` showing `OpenFileDialog` for `Open Map...` when the scene is clean.
4. `EditorSession` clearing the scene directly for `New Map` when the scene is clean.
5. `EditorSession` showing `UnsavedChangesDialog` for `New Map` when the scene is dirty.
6. `EditorSession` showing `UnsavedChangesDialog` for `Open Map...` when the scene is dirty.
7. `Cancel` keeping the current scene and selection unchanged.
8. `Don't Save` continuing the pending `New Map` or `Open Map...` action without saving.
9. `Save` with an existing `CurrentScenePath` saving first and then continuing the pending action.
10. `Save` without a current scene path opening `SaveFileDialog`, saving successfully, and then continuing the pending action.
11. Successful open updating `CurrentScenePath`, refreshing the hierarchy, and marking the scene clean.
12. Failed open preserving the previous live scene and previous document state.

## Open Follow-Ups

These items are intentionally deferred:

- updating the window title to include dirty markers or current scene name
- recent-scenes history
- autosave and recovery
- a generalized confirmation-dialog framework
- additive scene loading
- a more comprehensive central dirty-tracking bus for every future scene mutation path

## Recommendation

Implement `Open Map...` and guarded `New Map` transitions as one scene-document workflow owned by `EditorSession`, backed by an editor `OpenFileDialog` and a dedicated `UnsavedChangesDialog`.

That keeps save/open/new behavior consistent, avoids silent data loss, preserves the current scene when loading fails, and gives the editor a clear document-state model for future scene workflows.

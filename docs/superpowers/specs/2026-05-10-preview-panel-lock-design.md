# Preview Panel Lock Design

## Goal

Add per-preview-panel lock behavior so duplicated preview panels can either:

- follow the latest clicked previewable thing globally
- stay locked to their current asset or camera target

The feature must work with the workspace panel system, survive layout save/load, and restore across editor restarts when the locked target can still be resolved.

## Scope

This design covers:

- a preview-panel toolbar with a lock toggle
- per-panel preview binding state
- latest-clicked routing for assets and cameras
- locked-target invalidation and clearing
- workspace slot persistence for locked preview panels

This design does not add any other preview toolbar controls.

## Requirements

- Every `PreviewPanel` instance is independent.
- Unlocked panels follow the latest clicked previewable thing.
- The latest clicked thing wins regardless of type:
  - last clicked asset wins over an earlier camera
  - last clicked camera wins over an earlier asset
- Locked panels ignore later previewable clicks.
- If a locked target is deleted or becomes invalid, the panel clears immediately.
- Locked bindings should restore across app restarts when possible.

## Current State

Today preview behavior is session-driven:

- `EditorSession` listens to asset and scene selection changes
- `PreviewSourceResolver` resolves one `IPreviewSource`
- `EditorSession` pushes that preview source directly into preview panels
- `PreviewPanel` is mostly a renderer with no target-binding state

That is not enough for duplicate preview panels because lock state and target ownership need to live per panel instance.

## Approaches

### 1. Per-panel binding state

Each `PreviewPanel` owns its own binding state and decides whether to accept incoming selection changes.

Pros:

- fits duplicate preview panels naturally
- keeps preview behavior localized to the preview system
- keeps `EditorSession` simpler

Cons:

- `PreviewPanel` becomes stateful instead of being only a renderer

### 2. Session-owned binding map

`EditorSession` stores one preview binding record per preview panel instance id and pushes resolved state down into passive panels.

Pros:

- keeps UI panels thinner

Cons:

- spreads preview-specific state into session orchestration
- creates tighter coupling between workspace panel tracking and preview internals

### Recommendation

Use approach 1. Preview lock is a per-panel concern, and the workspace feature already depends on panel-local state.

## Architecture

### Preview Binding Model

Each `PreviewPanel` gains a persistent binding state object with:

- `IsLocked`
- `PreviewBindingKind`
  - `None`
  - `Asset`
  - `Camera`
- asset target reference
- camera target reference

The panel no longer only stores the active `IPreviewSource`. It also stores the identity of what that source represents.

### Stable Target References

To support save/load and restart restore:

- asset locks persist the asset relative path
- camera locks persist the selected scene entity id

Camera restore succeeds only when the restored scene still contains that entity id and the entity still has a `CameraComponent`.

If target resolution fails, the preview clears instead of falling back to stale imagery.

### Session Routing

`EditorSession` remains the single observer of global selection changes, but it stops directly deciding final preview ownership per panel.

Instead it publishes a normalized preview-target event to every preview panel:

- latest clicked asset target
- latest clicked camera target
- clear when no previewable target remains

Each `PreviewPanel` handles that event locally:

- unlocked: accept and rebind
- locked: ignore

This keeps entity selection global while preview ownership stays panel-local.

### Source Resolution

`PreviewSourceResolver` stays responsible for turning a target into an `IPreviewSource`.

The resolution boundary becomes:

- `EditorSession` produces the latest target identity
- `PreviewPanel` decides whether to adopt it
- `PreviewPanel` asks the resolver path to build the new source when needed

That avoids storing disposable `IPreviewSource` instances in session-level state.

## UI

### Preview Toolbar

Add a small toolbar band at the top of `PreviewPanel`, matching the general visual treatment of the viewport toolbar.

Initial controls:

- lock toggle as the first and only control for this slice

States:

- unlocked icon state
- locked icon state

The preview content area uses the remaining body space below the toolbar.

### Interaction

- Clicking lock while content is visible freezes the current target.
- Clicking lock while the panel is empty enables locked-empty state.
- Clicking unlock removes the frozen binding state and returns the panel to follow mode.
- The next previewable click after unlock becomes the active source.

## Behavior

### Unlocked Panels

- follow the latest clicked previewable thing
- switch between assets and cameras based on click order
- update immediately when a later click is previewable

### Locked Panels

- keep their current target
- ignore later asset and camera clicks
- clear immediately when the locked target is no longer valid

### Invalidity Rules

Asset lock invalidation:

- asset entry no longer resolves
- file or imported asset no longer exists
- resolved asset is no longer previewable

Camera lock invalidation:

- scene entity id no longer resolves
- entity no longer has a camera component
- scene is replaced and the saved camera id is absent

Invalid locked targets clear the preview and keep the panel in locked state.

## Persistence

`PreviewPanel` state is saved through the workspace panel state payload.

Persist:

- `IsLocked`
- binding kind
- asset relative path when binding kind is asset
- scene entity id when binding kind is camera

Do not persist live source objects, runtime textures, or interaction-source instances.

On restore:

1. recreate the preview panel
2. restore its binding state
3. try to resolve the saved target
4. if resolution succeeds, rebuild the preview source
5. if resolution fails, clear the panel

## Error Handling

- Preview resolution failures should clear the panel and log through the existing preview error path.
- Locked invalid targets should not throw.
- Save/load should tolerate unknown or outdated preview binding payloads by clearing the preview state instead of failing the whole layout load.

## Testing

Add focused tests for:

- unlocked preview follows latest asset then latest camera
- locked preview ignores later selection changes
- locked preview clears when its asset target becomes invalid
- locked preview clears when its camera entity disappears or loses `CameraComponent`
- two preview panels diverge correctly when one is locked and one is unlocked
- workspace slot save/load restores locked asset binding
- workspace slot save/load restores locked camera binding when the entity id still exists
- restore clears when the saved asset path or camera entity id no longer resolves

## Implementation Notes

- Prefer panel-specific preview state document types rather than overloading generic workspace payloads.
- Keep `PreviewPanel` responsible for disposing and replacing its current `IPreviewSource`.
- Avoid storing direct `Entity` or `AssetBrowserEntry` references as the persistence source of truth.

## Out Of Scope

- additional preview toolbar controls
- preview history navigation
- manual pin naming
- syncing preview locks across panels

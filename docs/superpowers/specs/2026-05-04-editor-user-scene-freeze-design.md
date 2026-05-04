# Editor User Scene Freeze Design

## Summary

Freeze user-authored scene behavior while the editor is running, without modifying `helengine.core`.

The editor should:

- keep rendering user scene entities normally
- keep editor-owned tools and UI responsive
- stop user-authored `UpdateComponent`-based behavior from ticking
- stop user-authored UI and other interactables from reacting to pointer input

This specifically covers cases like:

- `FPSComponent` updating inside the editor
- authored menu/UI controls reacting to hover, click, or other pointer input
- authored gameplay update components running while editing

The implementation boundary stays entirely inside `helengine.editor`.
There will be no play-mode branch, registration flag, or update-policy change added to `helengine.core`.

## Problem

The current editor session runs the normal core update loop:

- `Core.Update()` advances registered `IUpdateable` instances
- `PointerInteractionSystem.Update()` routes pointer input to registered `IInteractable2D` instances

That means authored scene behavior still executes while the user is only editing:

- `FPSComponent` updates its counters and text
- authored UI components can receive interaction through `InteractableComponent`
- any gameplay `UpdateComponent` derived type can run during edit-time

This is the wrong boundary for the editor.
The editor should display authored content, but it should not run authored scene behavior unless a later dedicated external player or play-mode runtime is introduced.

## Goals

- Make the change entirely inside `helengine.editor`.
- Do not modify `helengine.core`.
- Keep authored scene entities visible and renderable in the editor.
- Stop all user-scene `IUpdateable` behavior from running in the editor.
- Stop all user-scene `IInteractable2D` behavior from reacting in the editor.
- Keep editor-owned update components, cameras, gizmos, dialogs, title bar controls, and docking UI working normally.
- Cover newly created scene entities, loaded scenes, and mutated scenes consistently.

## Non-Goals

- No editor play/pause/stop mode in this slice.
- No external player process in this slice.
- No attribute-based opt-in for edit-time updates in this slice.
- No changes to runtime player behavior.
- No changes to generated source code or low-performance runtime code paths.
- No attempt to disable rendering for authored scene content.

## Design

### Editor-Owned Suppression Service

Add a new editor-only service, tentatively named `EditorSceneRuntimeSuppressionService`.

Responsibilities:

- inspect live entities owned by the main editor scene
- classify which components belong to user-authored scene content
- remove user-scene `IUpdateable` instances from `ObjectManager.Updateables`
- remove user-scene `IInteractable2D` instances from `ObjectManager.Interactables`

This service does not change how components register themselves.
Core registration continues to happen as it does today.
The editor simply reconciles the live object-manager lists before each editor frame update.

That keeps the implementation local to the editor and avoids adding policy hooks into runtime code.

### Why Reconcile In The Editor Instead Of Changing Core

The simplest technically pure approach would be to add an update-policy hook to `UpdateComponent` and `InteractableComponent`.
That is explicitly out of bounds for this task.

The editor-side reconcile model is recommended here because:

- it satisfies the no-`helengine.core` constraint
- it preserves current runtime behavior unchanged
- it works for existing component types without modifying them
- it also catches authored UI interaction, not just update ticks

The cost is an editor-side scan of the live scene graph before each frame.
That is acceptable for this slice because correctness and isolation matter more than micro-optimizing edit-time bookkeeping.

## Scene Ownership Rules

### User Scene Content

A component is considered user-scene-owned when it belongs to an entity hierarchy that represents authored scene content.

The existing editor conventions already expose the boundary:

- editor infrastructure uses `EditorEntity`
- editor infrastructure marks itself with `InternalEntity = true`
- authored scene roots are the same entities the editor save/load path treats as user scene content

The suppression service should reuse the same ownership model already implied by `EditorSession.IsUserSceneEntity(...)` and related scene-root filtering.

### Important Detail: Descendant Entities

The service must not only inspect root `EditorEntity` instances.

Some authored components create child entities internally.
For example, authored UI may spawn helper child entities that are plain `Entity` instances rather than `EditorEntity`.

Because of that, suppression must walk the full descendant hierarchy under user-authored scene roots and inspect all attached components on every descendant entity.

### Editor Infrastructure

Editor-owned entities must remain active.

Examples:

- title bar buttons
- docking UI
- asset picker dialogs
- viewport camera controls
- transform gizmos
- editor preview helpers

These remain enabled because they are editor infrastructure rather than user scene content.

## Reconcile Flow

### Per-Frame Entry Point

Update the editor frame path so suppression happens immediately before the normal core update:

1. `EditorSession.Update()` calls the suppression service
2. the suppression service removes authored updateables and interactables from the live object manager
3. `core.Update()` runs
4. only editor-owned behavior continues to update or receive pointer interaction

This ordering is important.
If suppression happened after `core.Update()`, authored behavior could still tick or react once per frame.

### Reconcile Actions

For every component found under user-authored scene roots:

- if the component implements `IUpdateable`, remove it from `Core.Instance.ObjectManager.Updateables`
- if the component implements `IInteractable2D`, remove it from `Core.Instance.ObjectManager.Interactables`

No special case is needed for specific concrete types such as `FPSComponent`.
Those types are covered by interface-based suppression.

### Re-Registration Safety

Core code can still re-register components when:

- a component is added
- a parent entity becomes enabled again
- a scene is loaded

That is acceptable.
The editor suppression service runs before each frame update and removes authored registrations again before they can execute.

This keeps the design robust without needing to intercept every mutation site.

## Input And UI Behavior

### Authored UI

Authored UI should stop reacting in the editor.

Blocking `IUpdateable` alone is not enough because many UI behaviors react through `InteractableComponent` and pointer routing.

Therefore this slice must suppress both:

- user-scene updateables
- user-scene interactables

That ensures authored buttons, text boxes, menus, combo boxes, and similar UI elements do not respond to pointer input while editing.

### Editor UI

Editor UI must continue to react.

Because the suppression boundary is based on scene ownership rather than component type name, editor title bar buttons and dialogs remain interactive.

## Rendering Behavior

Rendering remains unchanged.

The service does not:

- disable user scene entities
- remove drawables
- alter cameras
- hide authored scene visuals

The result should be:

- user content is still visible in the viewport
- authored runtime behavior is frozen
- editor interaction and tooling still work

## Error Handling

The suppression service should be strict and simple.

- null entity lists should be treated as programming errors, not ignored silently
- the service should not swallow exceptions from malformed scene graphs
- ownership classification should be deterministic and explicit

This feature is editor infrastructure, not a best-effort cleanup pass.

## Testing

Add editor tests that prove the editor-only suppression boundary.

Required coverage:

- a user-authored `FPSComponent` is not present in the live update list after suppression
- a user-authored `ButtonComponent` or equivalent interactable UI does not remain registered as an interactable after suppression
- an editor-owned update component such as `EditorViewportCameraController` remains active
- an editor-owned interactable such as editor title bar or dialog UI remains active
- a loaded scene and a newly created scene entity both get suppressed correctly

Test focus should stay on list membership and editor behavior boundaries rather than implementation details.

## Implementation Notes

- Place the new service in `helengine.editor`, near other scene/editor lifecycle services.
- Keep `EditorSession` as the integration point because it already owns the main editor frame loop and scene lifecycle.
- Reuse existing authored-scene ownership rules rather than inventing a second classification model.
- Do not add any new hooks, flags, or attributes to `helengine.core`.

## Future Extensions

This design intentionally keeps future options open without implementing them now:

- attribute-based opt-in for specific user components to run in editor
- external player launch for the current scene
- true isolated play runtime in another process

Those can be layered later on top of the same authored-scene ownership boundary.

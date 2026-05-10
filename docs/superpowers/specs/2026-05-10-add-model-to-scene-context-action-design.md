# Add Model To Scene Context Action

## Goal
Add a model-only asset browser context-menu command named `Add to scene`.

When the user right-clicks a model asset and chooses `Add to scene`, the editor should create a new scene entity that uses that model as its mesh asset. The entity should spawn at the center of the most recently focused viewport. If no viewport has been focused yet, the editor should fall back to the primary viewport.

The spawned entity should behave like a normal scene object:
- it should be inserted into the live scene
- it should appear in the scene hierarchy
- it should be selected after creation
- it should mark the scene dirty

## UX
- The asset browser context menu shows `Add to scene` only for entries classified as models.
- The action is available for file-backed model assets, including imported models such as `.obj`, `.fbx`, `.dae`, `.x`, and any other registered model extension.
- The command does not appear for folders, textures, materials, or non-model files.
- Right-click selection behavior remains unchanged, so the action applies to the row under the pointer.

## Placement
- The spawn point is the current orbit target of the most recently focused viewport.
- The "most recently focused viewport" is the viewport whose content target most recently received focus.
- If no viewport content target is currently focused, the editor uses the primary viewport.
- The spawned entity should appear at that world-space orbit target, not at a fixed offset in front of the camera.

## Entity Creation
- The action should create one normal scene entity with:
  - `MeshComponent`
  - `EntitySaveComponent`
  - a name derived from the model asset display name
  - `LayerMask = SceneObjects`
- The entity should use the model asset resolved from the selected browser entry.
- If the model source has generated imported materials, the new mesh component should bind those materials in submesh order so multi-material models render correctly.
- If imported materials cannot be resolved for a model, the entity should still spawn with the model assigned and a safe fallback material binding rather than failing the command.

## Architecture
- The asset browser should not create scene entities directly.
- The context menu should raise a session-level action, and `EditorSession` should own the scene mutation.
- `EditorSession` should:
  - resolve the clicked model entry
  - resolve the target viewport orbit center
  - create the entity through the scene creation service or a dedicated model scene-creation helper
  - refresh the hierarchy
  - select the spawned entity
  - mark the scene mutated

## Data Flow
1. User right-clicks a model asset in the asset browser.
2. The context menu shows `Add to scene`.
3. The user clicks `Add to scene`.
4. The session resolves the model asset and its imported materials.
5. The session resolves the active viewport target position.
6. A new entity is created at that position with the model mesh component bound.
7. The hierarchy refreshes and the new entity becomes selected.

## Error Handling
- If the clicked asset is not a model, the menu item must not be shown.
- If the model cannot be resolved, the action should fail clearly and leave the current selection unchanged.
- If no viewport can be resolved, the session should fall back to the primary viewport before failing.
- If the model has no imported materials, the editor should still allow creation with a default material path instead of aborting.

## Testing
- Asset browser test:
  - right-clicking a model row exposes `Add to scene`
  - the command does not appear for non-model rows
- Session test:
  - invoking the action creates one new scene entity
  - the new entity is selected after creation
  - the scene is marked dirty
- Placement test:
  - with a focused viewport, the new entity spawns at that viewport's orbit target
  - with no focused viewport, the primary viewport is used
- Material test:
  - a model with imported submesh materials spawns with the matching material slots bound
  - fallback material binding still succeeds when imported materials are unavailable

## Non-Goals
- No new model import format support.
- No changes to the asset browser selection model beyond the existing right-click selection behavior.
- No changes to the viewport camera controls or orbit math.
- No new drag-and-drop import workflow in this slice.

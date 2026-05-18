# Scene Dont Unload Design

## Summary

Add a scene-level `DontUnload` setting that allows specific scenes to survive normal `SceneLoadMode.Single` transitions.

This provides the engine-level equivalent of persistent global scene content without moving entities outside scene ownership. A scene marked `DontUnload` remains loaded until code explicitly calls `UnloadScene(sceneId)`.

## Goals

- Let authored scenes opt into persistence through scene settings.
- Preserve `DontUnload` scenes during `LoadScene(sceneId, SceneLoadMode.Single)`.
- Allow explicit `UnloadScene(sceneId)` calls to unload persistent scenes.
- Keep scene ownership intact instead of moving entities into a global lifetime bucket.
- Make the feature available through the existing Scene Settings dialog.

## Non-Goals

- Do not add entity-level persistence markers.
- Do not make persistence configurable per platform in this change.
- Do not add special runtime auto-discovery for "global singleton" components.
- Do not silently reload already loaded persistent scenes.

## User-Facing Behavior

- A scene author opens `Scene Settings`.
- The author enables `Dont Unload`.
- After that scene is loaded once, later `LoadScene(otherSceneId, SceneLoadMode.Single)` calls do not unload it.
- `UnloadScene(persistentSceneId)` still unloads it immediately.
- `LoadScene(persistentSceneId, SceneLoadMode.Additive)` still throws if the scene is already loaded.
- `LoadScene(persistentSceneId, SceneLoadMode.Single)` also still throws if the scene is already loaded.

## Architecture

### Authored Data

Extend `SceneSettingsAsset` with a `DontUnload` boolean. This keeps the lifetime policy attached to the scene asset instead of scattering it into unrelated runtime systems.

### Runtime Tracking

Extend `LoadedSceneRecord` with a `DontUnload` boolean copied from `SceneAsset.SceneSettings.DontUnload` during load.

`SceneManager` should not depend on the transient `SceneAsset` after load completes, so the persistence flag must be captured into the loaded-scene bookkeeping record before the transient asset is released.

### Unload Policy

`SceneManager.LoadScene(sceneId, SceneLoadMode.Single)` should unload only currently loaded scenes whose records have `DontUnload == false`.

`SceneManager.UnloadScene(sceneId)` should continue to unload the exact target scene regardless of the flag, because explicit unload requests are authoritative.

## Detailed Runtime Flow

### Scene Load

When `SceneManager.LoadScene(sceneId, loadMode)` reaches `LoadSceneImmediate`:

1. Resolve the scene content path.
2. Reject additive duplicate loads as today.
3. For `SceneLoadMode.Single`, unload only non-persistent loaded scenes.
4. Load the `SceneAsset`.
5. Read `sceneAsset.SceneSettings.DontUnload`.
6. Materialize the runtime scene entities and owned assets.
7. Create `LoadedSceneRecord` with the persisted `dontUnload` value.
8. Track the record and owned assets.
9. Release the transient `SceneAsset`.

### Explicit Unload

`UnloadScene(sceneId)` and `UnloadSceneImmediate(sceneId)` should not special-case the persistence flag. If the user asked for unload, the scene unloads.

### Duplicate Loads

The current "already loaded" contract should remain intact. Persistent scenes are still scenes, not registries or soft handles. If a persistent scene is loaded already, another load request for the same scene id throws.

## Editor Flow

### Scene Settings Model

`SceneSettingsAsset` gains:

- `CanvasProfile`
- `DontUnload`

The default value for `DontUnload` is `false`.

### Scene Settings Dialog

`SceneSettingsDialog` gains a new checkbox labeled `Dont Unload`.

The dialog should:

- populate the checkbox from the current scene settings in `Show(...)`
- include the checkbox state in `BuildSceneSettingsFromFields()`
- preserve the existing validation flow for canvas fields

The dialog layout should be adjusted as needed so the checkbox fits cleanly without crowding the footer.

### Dirty State

`EditorSession.AreSceneSettingsEquivalent(...)` must compare `DontUnload` in addition to canvas size so changing the checkbox marks the scene dirty.

### Save/Load

The existing scene save/open flow already carries `SceneSettingsAsset` end to end. This change extends that payload rather than inventing a new persistence channel.

Affected areas:

- `SceneSaveService.CloneSceneSettings(...)`
- editor scene load/open paths that hydrate `CurrentSceneSettings`
- scene binary serializer/deserializer in editor/runtime asset readers

## Serialization

`SceneSettingsAsset` already participates in scene binary serialization.

The scene asset binary version must be incremented so the new `DontUnload` flag is serialized explicitly rather than inferred implicitly. Older payloads should deserialize with `DontUnload = false`.

That preserves backward compatibility for existing scene assets while making the new flag deterministic for newly saved scenes.

## Error Handling

- `SceneSettingsAsset` should continue to require a valid `CanvasProfile`.
- Missing `SceneSettingsAsset` on legacy scene payloads should still produce a default settings instance with `DontUnload = false`.
- Explicit unload of a persistent scene should not warn or no-op.
- Duplicate-load attempts should continue to throw.

## Testing

### Serialization Tests

- Round-trip `SceneSettingsAsset` with `DontUnload = true`.
- Verify older scene payloads deserialize with `DontUnload = false`.

### Editor Tests

- Confirm `Scene Settings` dialog can round-trip the checkbox state.
- Confirm saving a scene persists `DontUnload`.
- Confirm opening a scene restores `DontUnload`.
- Confirm changing `DontUnload` marks the scene dirty.

### Runtime Tests

- `LoadScene(..., Single)` preserves previously loaded persistent scenes.
- `LoadScene(..., Single)` unloads previously loaded non-persistent scenes.
- `UnloadScene(sceneId)` unloads a persistent scene when explicitly requested.
- Loading an already loaded persistent scene still throws.

## Alternatives Considered

### Entity-Level Component Marker

Rejected because unload policy belongs to scene lifetime, not to arbitrary entities inside the scene. It also creates unclear behavior when multiple entities disagree or when the marker entity is deleted.

### Global Persistent Entity Pool

Rejected because it breaks scene ownership and makes serialization and teardown harder to reason about. The engine already has scene boundaries; this design preserves them.

### Hardcoded Persistent Scene Registry

Rejected because it moves authored behavior into code/config and bypasses the scene settings UI.

## Recommendation

Implement persistence as a scene setting enforced by `SceneManager`, with explicit unload always winning over persistence.

This is the narrowest change that matches the desired runtime semantics, keeps the authoring model coherent, and avoids introducing fake global object lifetime outside scene ownership.

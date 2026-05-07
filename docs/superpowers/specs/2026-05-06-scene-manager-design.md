# Runtime Scene Manager

## Summary

This change adds a runtime `SceneManager` to `helengine.core` so player builds can load scenes that were included in the build manifest in either single-scene or additive mode.

The manager owns runtime scene bookkeeping, not scene memory. It tracks which built scenes are currently loaded, resolves built scene ids to cooked scene payloads, materializes root entities through the existing runtime scene loader, and emits explicit load and unload events so the player can destroy entities and free assets using its own memory model.

This ownership split is required because the runtime is intended to convert to C++ targets where garbage collection is unavailable and the player may be implemented in C or C++ with its own allocation and teardown policy.

## Goals

- Add a runtime `SceneManager` API for loading built scenes by stable `SceneId`.
- Support both single-scene replacement and additive scene loading.
- Keep a canonical registry of loaded runtime scenes inside the engine.
- Emit explicit unload notifications before scene records are removed so the player can synchronously release entities and assets.
- Reject scene ids that were not included in the build.
- Preserve existing runtime scene materialization through `RuntimeSceneLoadService`.

## Non-Goals

- Automatic entity disposal by the engine.
- Automatic asset unloading by the engine.
- Cross-thread or asynchronous scene loading.
- Streaming partial scene content.
- Backward-compatible support for loading arbitrary authoring-time scene files outside the build.

## Current Problems

### No Runtime Scene Owner

`RuntimeSceneLoadService` can materialize one packaged `SceneAsset` into live root entities, but there is no runtime service that:

- resolves a built `SceneId` to its cooked scene payload
- tracks which scenes are currently loaded
- supports additive versus single-scene behavior
- coordinates unload notifications before scene replacement

### Startup Metadata Is Too Narrow

The current managed runtime metadata in `RuntimeStartupManifest` only exposes:

- the startup scene id
- the runtime storage profile id

That is not enough for runtime scene switching because `SceneManager` also needs the full set of build-included scenes and the cooked relative path for each one.

### Memory Ownership Must Stay External

`Entity` instances register themselves with `Core.Instance.ObjectManager` during construction. If the engine were to remove scene records without giving the player a chance to synchronously destroy those entities, old scene objects would remain alive and still participate in runtime systems.

Because the player may run under manual memory management after C++ conversion, the engine must not guess how teardown should happen.

## Proposed Design

### Core Types

Add the following runtime types to `helengine.core`:

- `SceneManager`
- `SceneLoadMode`
- `LoadedSceneRecord`
- `RuntimeSceneCatalog`
- `RuntimeSceneCatalogEntry`
- `SceneLoadingEventArgs`
- `SceneLoadedEventArgs`
- `SceneUnloadingEventArgs`
- `SceneUnloadedEventArgs`

`SceneManager` is the public runtime entry point. `RuntimeSceneCatalog` is the build-scene lookup used by the manager. `LoadedSceneRecord` is the engine-owned bookkeeping record for one currently loaded scene.

### Scene Catalog Contract

`SceneManager` will consume a runtime scene catalog instead of depending directly on editor or build-graph types.

Each `RuntimeSceneCatalogEntry` will contain at least:

- `SceneId`
- `CookedRelativePath`

`RuntimeSceneCatalog` will expose:

- the ordered set of build scenes
- lookup by `SceneId`
- validation that scene ids are unique and non-blank

This keeps `helengine.core` independent from `helengine.baseplatform` while still giving runtime code enough information to load only scenes that were included in the build.

### Runtime Metadata Source

The build pipeline will need to emit a runtime scene catalog alongside the existing startup metadata.

For managed runtime use, the design is:

- add a JSON runtime scene catalog file
- add a `ReadFromFile` pattern in core similar to `RuntimeStartupManifest` and `RuntimeCodeModuleManifest`

For generated native runtime use, the design is:

- emit equivalent generated native lookup data from the editor build step, similar in spirit to the existing generated native startup and physics manifests

The `SceneManager` itself will only depend on the in-memory `RuntimeSceneCatalog`, not on how that catalog was produced.

### Scene Manager API

`SceneManager` will expose a focused runtime API:

- `LoadScene(string sceneId, SceneLoadMode loadMode)`
- `UnloadScene(string sceneId)`
- `IsSceneLoaded(string sceneId)`
- `LoadedScenes`
- `TryGetLoadedScene(string sceneId, out LoadedSceneRecord loadedScene)`

The manager constructor will require:

- a `RuntimeSceneCatalog`
- a `RuntimeSceneLoadService`
- a `ContentManager`

The `ContentManager` dependency is used to open the cooked scene asset resolved by `CookedRelativePath`.

### Loaded Scene Record

Each `LoadedSceneRecord` will store:

- `SceneId`
- `CookedRelativePath`
- `IReadOnlyList<Entity>` root entities

The manager keeps these records so additive unload is deterministic and so unload events can carry the exact entities that were created for the scene.

The record is metadata-only ownership. The manager tracks the roots but does not destroy them.

### Load Flow

For `LoadScene(sceneId, SceneLoadMode.Additive)`:

1. Validate that `sceneId` is non-blank.
2. Resolve the scene from `RuntimeSceneCatalog`.
3. Reject the request if that scene is already loaded.
4. Raise `SceneLoading`.
5. Open the cooked scene asset from `CookedRelativePath`.
6. Deserialize the `SceneAsset`.
7. Materialize root entities through `RuntimeSceneLoadService`.
8. Create and store the `LoadedSceneRecord`.
9. Raise `SceneLoaded`.

For `LoadScene(sceneId, SceneLoadMode.Single)`:

1. Unload all currently loaded scenes in load order using the same unload sequence described below.
2. Perform the normal load flow for the requested scene.

### Unload Flow

For `UnloadScene(sceneId)`:

1. Validate that `sceneId` is non-blank.
2. Resolve the currently loaded scene record.
3. Raise `SceneUnloading` with the tracked root entities.
4. Remove the loaded-scene record from the manager registry.
5. Raise `SceneUnloaded`.

The manager does not call `Dispose`, does not remove entities from `ObjectManager`, and does not unload assets.

### Synchronous Player Teardown Contract

The player must treat `SceneUnloading` as the point where teardown happens.

That contract is synchronous and explicit:

- the event provides the exact root entities belonging to the scene
- the player is responsible for deleting those entities and releasing any scene-owned assets before the event handler returns
- `SceneManager` then removes its record and continues

This requirement is especially important for `SceneLoadMode.Single`. If the player does not destroy the outgoing scene roots during `SceneUnloading`, the old entities remain registered with runtime systems while the replacement scene is loading.

`SceneUnloaded` is a post-removal notification, not a teardown callback.

### Event Semantics

The recommended event set is:

- `SceneLoading`
- `SceneLoaded`
- `SceneUnloading`
- `SceneUnloaded`

The load events should carry:

- `SceneId`
- `CookedRelativePath`
- loaded root entities for the post-load event

The unload events should carry:

- `SceneId`
- `CookedRelativePath`
- the tracked root entities that the player must destroy

### Ordering Rules

Loaded scene records remain ordered by load sequence.

For single-scene replacement:

- unload existing scenes in their current load order
- require teardown during each `SceneUnloading` event
- load the replacement scene only after prior records have been removed

For additive loads:

- preserve existing order
- append the new loaded-scene record at the end

## Error Handling

`SceneManager` should fail loudly in the following cases:

- blank scene id
- scene id not present in the runtime scene catalog
- additive load request for an already loaded scene
- unload request for a scene that is not currently loaded
- duplicate `SceneId` values inside the runtime scene catalog
- blank or missing cooked relative path in the runtime scene catalog

The manager should not catch or mask:

- file-not-found failures for cooked scene payloads
- deserialization failures
- runtime component materialization failures

Those failures indicate broken build output or broken runtime compatibility and should remain visible.

## Testing Strategy

Add test-first coverage for:

- loading one built scene in `Single` mode stores one loaded-scene record
- additive loading preserves the previously loaded scene and appends a second record
- single-scene loading unloads currently loaded scenes before loading the replacement scene
- unload events expose the exact tracked root entities for the scene being removed
- duplicate additive loads are rejected
- non-built scene ids are rejected
- unloading a scene removes the internal record without disposing the tracked entities
- runtime scene catalog lookup rejects duplicate or invalid entries

The tests should also verify event ordering:

- `SceneLoading` before `SceneLoaded`
- `SceneUnloading` before record removal completes
- `SceneUnloaded` after record removal

## Risks

- The managed runtime currently lacks a full runtime scene catalog artifact, so this change requires build metadata expansion rather than only adding a manager class.
- If player code does not delete roots during `SceneUnloading`, old entities remain active because entity registration happens at construction time.
- Native generated-runtime support will need a matching scene-catalog data source so the manual-memory contract stays identical across C# and C++ players.

## Recommendation

Implement the metadata-owning `SceneManager` with explicit synchronous unload events and no engine-side destruction.

This keeps runtime scene behavior centralized and deterministic while preserving the correct ownership boundary for manual-memory players.

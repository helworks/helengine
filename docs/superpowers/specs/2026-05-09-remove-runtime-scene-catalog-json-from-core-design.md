# Remove Runtime Scene Catalog JSON From Core

## Summary

`helengine.core` must stop reading runtime scene metadata from JSON files. `Core` should only consume an in-memory `RuntimeSceneCatalog` object that is supplied at initialization time. File formats, manifest generation, and platform-specific runtime packaging belong outside core.

This replaces the JSON-based bootstrap decision in [2026-05-06-scene-manager-design.md](/C:/dev/helworks/helengine/docs/superpowers/specs/2026-05-06-scene-manager-design.md). The earlier spec correctly introduced `SceneManager`, but it put runtime manifest I/O on the wrong side of the boundary.

## Goals

- Remove `runtime-scene-catalog.json` loading from `helengine.core`.
- Remove runtime scene-catalog file probing from `Core`.
- Keep `RuntimeSceneCatalog` as a pure in-memory runtime data model.
- Make runtime hosts or generated runtime data responsible for supplying the scene catalog to `Core`.
- Preserve `SceneManager` behavior and scene-loading semantics once a catalog is provided.

## Non-Goals

- Keep compatibility with the current JSON bootstrap path.
- Add a replacement binary manifest reader to core.
- Rework `SceneManager` scene-loading semantics.
- Solve every platform runtime manifest path in one large refactor beyond the new scene-catalog injection boundary.

## Current Problem

`Core.Initialize(...)` currently creates `SceneManager` by probing the content root for `runtime-scene-catalog.json` and then calling `RuntimeSceneCatalog.ReadFromFile(...)`.

That causes three architectural problems:

- `helengine.core` knows about one editor-owned runtime file format.
- runtime startup depends on filesystem layout instead of host-supplied runtime metadata.
- console runtimes inherit a managed JSON bootstrap path they should never have had.

The PS2 failure made this explicit, but the bug is not PS2-specific. The boundary is wrong in shared core.

## Chosen Approach

Remove scene-catalog file loading from `Core` entirely and inject a ready `RuntimeSceneCatalog` through initialization options.

This is the narrowest approach that fixes the boundary correctly:

- `Core` keeps ownership of runtime systems and `SceneManager` construction.
- `Core` stops owning manifest discovery, file probing, and JSON parsing.
- hosts and platform build pipelines become responsible for converting their own runtime metadata into a `RuntimeSceneCatalog` before core initialization.

## Rejected Approaches

### Replace JSON With Another File Format In Core

This removes JSON but keeps the wrong dependency direction. `Core` would still own runtime manifest I/O and packaged filesystem assumptions.

### Move All SceneManager Construction Out Of Core

This would also fix the boundary, but it creates unnecessary churn in every host because host code would need to reconstruct more of core's current initialization graph.

## Design

### Core Boundary

`CoreInitializationOptions` will gain one scene-catalog property used by packaged runtimes:

- `RuntimeSceneCatalog SceneCatalog`

`Core.Initialize(...)` will:

- build the default `ContentManager`
- configure the scene asset reference resolver
- create the runtime component registry
- create the scene load service
- create `SceneManager` from `InitializationOptions.SceneCatalog`

`Core` will no longer:

- resolve `runtime-scene-catalog.json`
- probe `cooked/runtime-scene-catalog.json`
- call `RuntimeSceneCatalog.ReadFromFile(...)`

If `SceneCatalog` is absent, `Core` leaves `SceneManager` unset. That preserves the existing distinction between runtimes that support built-scene switching and runtimes that do not, without giving core a file-format fallback.

### RuntimeSceneCatalog Role

`RuntimeSceneCatalog` remains in core, but only as a validated runtime data model:

- ordered `Entries`
- lookup by `SceneId`
- duplicate and blank validation

Its file-reading helper will be removed from core. Parsing helpers that exist only to support that reader should leave core as well.

### Editor And Build Boundary

The editor may still use JSON internally if that is convenient for tooling, but JSON must terminate at the editor boundary.

That means:

- editor/build code may assemble scene metadata from project/build data however it wants
- editor/build code must convert that metadata into `RuntimeSceneCatalog`
- runtime outputs must receive scene-catalog data in host-native form

For runtime builds:

- PS2 should receive generated native scene-catalog data
- other native/player targets should receive native or host-owned runtime scene-catalog data
- no runtime target should require `runtime-scene-catalog.json` to bootstrap core

### PS2 Direction

PS2 will stop trying to load `runtime-scene-catalog.json` from disc.

Instead:

- the PS2 builder will emit generated native scene-catalog data alongside the existing generated startup-scene metadata
- the PS2 host will pass or apply that catalog during `Core` initialization
- the PS2 disc no longer needs a runtime scene-catalog JSON file

### Windows And Other Runtime Hosts

Any runtime host that currently depends on `Core` probing a runtime scene-catalog file must move that responsibility outward.

The immediate contract is:

- create or obtain `RuntimeSceneCatalog`
- assign it to `CoreInitializationOptions`
- initialize core

How that catalog is produced is host-specific and intentionally outside core.

## API Changes

### CoreInitializationOptions

Add:

- `RuntimeSceneCatalog SceneCatalog`

Expectations:

- optional for runtimes that never switch built scenes
- required for runtimes that expose menu-driven or scripted built-scene switching through `SceneManager`

### Core

Update:

- `CreateSceneManager(ContentManager contentManager)` to use `InitializationOptions.SceneCatalog`

Remove:

- `ResolveRuntimeSceneCatalogPath()`
- any file probing for runtime scene catalogs

### RuntimeSceneCatalog

Remove:

- `ReadFromFile(string manifestPath)`

Any JSON parser logic used only by that path should move out of core.

## Error Handling

`Core` should fail only on runtime object validity, not file lookup.

Expected behavior:

- if `SceneCatalog` is null, `SceneManager` remains null
- if `SceneCatalog` is present but invalid, construction should fail where the invalid object is created
- `SceneManager.LoadScene(...)` continues to fail loudly when a runtime without a scene manager tries to switch scenes

This keeps core honest about its actual responsibility.

## Testing Strategy

### Core Tests

Add or update tests proving:

- `Core` initializes `SceneManager` when `CoreInitializationOptions.SceneCatalog` is supplied
- `Core` leaves `SceneManager` null when no scene catalog is supplied
- `Core` no longer probes the filesystem for `runtime-scene-catalog.json`

### Editor Tests

Add or update tests proving:

- runtime scene-catalog data is produced for targets that need `SceneManager`
- editor/runtime bootstrap paths do not depend on `runtime-scene-catalog.json` reaching core

### PS2 Tests

Add or update tests proving:

- generated native runtime metadata includes the scene catalog needed for PS2 scene switching
- PS2 runtime packaging no longer requires `runtime-scene-catalog.json`

## Migration Plan

1. Add the injected scene-catalog property to `CoreInitializationOptions`.
2. Update `Core` to create `SceneManager` only from that property.
3. Remove core-side runtime scene-catalog file probing and file readers.
4. Update tests around `Core` and `RuntimeSceneCatalog`.
5. Update editor/build pipelines to supply scene-catalog data without relying on core-side JSON reads.
6. Update PS2 generated runtime metadata and host initialization to consume native scene-catalog data.
7. Remove runtime scene-catalog JSON packaging from runtime targets that no longer need it.

## Risks

- Windows or other runtime paths that silently relied on `Core` probing JSON will break until they supply a scene catalog explicitly.
- Some editor tests currently assume runtime-scene-catalog file output exists because core reads it. Those tests will need to be rewritten around the new boundary.
- PS2 host and generated-runtime work must stay aligned with the new core initialization contract or scene switching will remain unavailable.

## Recommendation

Implement this as a strict boundary correction:

- no JSON fallback
- no alternate file reader in core
- no platform-specific branching inside core

`helengine.core` should know what a runtime scene catalog is, not where it came from or how it was serialized.

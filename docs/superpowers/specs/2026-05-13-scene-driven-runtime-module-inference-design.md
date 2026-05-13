## Summary

Platform builds should not persist or accept manual runtime code-module selection. The build graph should infer authored runtime modules directly from the scripted components referenced by the selected scenes. `selectedCodeModuleIds` should be removed from editor-local build configuration, queue snapshots, and UI summaries.

## Problem

The current editor build flow still carries `SelectedCodeModuleIds` through:

- persisted per-platform build configuration
- persisted queued build items
- build-graph code compilation

That is the wrong ownership model. Runtime authored code inclusion is not a user preference. It is a build input derived from the selected scenes. The current setting creates stale state, allows incorrect builds, and duplicates knowledge the editor already has once scenes are cooked.

## Goals

- Remove manual runtime module selection from persisted editor build state.
- Infer authored runtime code modules only from scripted components referenced by the selected scenes.
- Continue resolving transitive authored runtime-module dependencies through the authored module manifest.
- Keep build behavior strict when a scene references a component whose owning module cannot be resolved as a runtime module.
- Preserve existing platform/profile/output-directory selection behavior.

## Non-Goals

- Do not add any fallback that compiles all authored runtime modules.
- Do not preserve a hidden or deprecated user-facing code-module selection concept.
- Do not infer runtime modules from arbitrary script folders, scene file locations, or project-wide scans.
- Do not change runtime native manifest layout beyond reflecting the new compiled module set.

## Recommended Approach

Use the cooked selected scenes as the authoritative source for runtime module inference.

After the build graph cooks the selected scenes, it should inspect those cooked scene payloads, discover all assembly-qualified scripted component type ids, resolve those types through the existing scripted-component resolution path, and derive root runtime module ids from the owning assembly names. Those inferred module ids become the only roots passed into authored code compilation. The existing manifest dependency traversal remains unchanged and continues to compute the transitive closure.

This approach is preferred over pre-cook authored-scene scanning because it reuses the same cooked payload shape and scripted-type interpretation the packaging pipeline already trusts.

## Architecture

### 1. Build Graph Ownership

`EditorPlatformBuildGraphRunner` becomes the only layer that decides which authored runtime modules belong in a build.

New flow:

1. Cook selected scenes.
2. Discover referenced scripted component runtime types from those cooked scenes.
3. Map those types to owning runtime module ids using `type.Assembly.GetName().Name`.
4. Pass the inferred module ids into authored code compilation.

The queue item no longer carries runtime module selection state.

### 2. Scene-Driven Module Inference Service

The existing cooked-scene scripted-component discovery already lives in `EditorGeneratedCoreRegenerationService`. That logic should be reused or factored into a small adjacent helper so both of these build phases interpret cooked scenes the same way:

- runtime module inference
- automatic runtime component deserializer generation

The shared helper should expose a module-oriented API that returns distinct referenced runtime module ids from cooked scene asset paths, while preserving the existing type-oriented API for deserializer generation.

### 3. Code Compilation Root Selection

`EditorPlatformCodeCookService` should stop thinking in terms of user-selected module ids. It should accept inferred root runtime module ids for the current build and resolve those ids through the authored runtime module manifest.

The service should continue to:

- validate that all inferred roots exist in the manifest
- validate that inferred roots are runtime modules, not editor modules
- include transitive runtime dependencies in dependency order

If the inferred root set is empty, no authored runtime modules are compiled.

### 4. Build Config and Queue Documents

`SelectedCodeModuleIds` should be removed from:

- `EditorBuildPlatformConfigDocument`
- `EditorBuildQueueItemDocument`
- related normalization, copying, and summary code

Legacy `build_config.json` files that still contain `selectedCodeModuleIds` should continue to load successfully. When rewritten by the editor, that property should disappear from the persisted JSON.

## Data Flow

The resulting platform build flow becomes:

1. User selects scenes, output path, and platform profile options.
2. The editor queues a build item containing scene/profile/output settings only.
3. The build graph cooks the selected scenes.
4. The build graph discovers referenced scripted runtime component types from the cooked scene payloads.
5. The build graph converts those types into root runtime module ids.
6. The code cook service resolves manifest dependencies from those inferred roots and compiles only that closure.
7. The package output includes only the authored runtime modules needed by the selected scenes.

If the selected scenes reference no scripted authored runtime components, the authored runtime module set is empty.

## Error Handling

This behavior should remain strict.

### Missing Manifest Root

If a cooked scene references a scripted component whose owning assembly name does not map to an authored runtime module in the manifest, the build should fail with a concrete error that identifies:

- the cooked scene asset path
- the scripted component type id
- the inferred module id

### Editor-Only Module Reference

If a cooked scene references a scripted component whose owning module exists in the manifest but is marked editor-only, the build should fail clearly and identify the component and module.

### Existing Dependency Failures

Existing hard failures for missing or cyclic module dependencies should remain unchanged.

### No Fallbacks

There should be no fallback to:

- compile all runtime modules
- use persisted module-selection state
- silently skip unresolved scene-referenced scripted components

## Testing Strategy

### 1. Build Graph Inference Tests

Add tests that prove:

- one cooked scene referencing one scripted gameplay component infers that owning runtime module id
- multiple cooked scenes union their referenced runtime module ids
- cooked scenes with no scripted authored runtime components infer an empty root set

### 2. Code Cook Dependency Tests

Add tests that prove:

- inferred root runtime modules still pull manifest dependencies transitively
- inferred module ids that are missing from the manifest fail clearly
- inferred module ids that resolve to editor-only modules fail clearly

### 3. Build Config Regression Tests

Add tests that prove:

- `build_config.json` is written without `selectedCodeModuleIds`
- legacy config files that still contain `selectedCodeModuleIds` load successfully
- saving a loaded legacy config removes the obsolete property

### 4. UI/Queue Regression Tests

Add tests that prove:

- queue item creation no longer copies or depends on runtime module ids
- queue summary text no longer mentions runtime module counts

## Implementation Notes

- Keep the scene-driven inference rooted in cooked scene payloads, not raw authored scene files.
- Keep module inference and automatic runtime component deserializer generation aligned by reusing the same cooked-scene scripted-component discovery path.
- Preserve existing strictness around manifest correctness and dependency resolution.
- Do not retain dead compatibility shims in the editor UI after the setting is removed.

## Success Criteria

The work is complete when:

- `selectedCodeModuleIds` no longer exists in persisted build config or queue documents
- the build graph compiles authored runtime modules only when the selected scenes reference their components
- manifest dependencies are still honored transitively
- builds fail clearly when a scene references a scripted component whose owning runtime module cannot be resolved correctly
- existing scene/profile/output build workflows continue to function without a runtime-module setting

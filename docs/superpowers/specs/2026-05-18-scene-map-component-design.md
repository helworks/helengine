# Scene Map Component Design

## Goal

Replace hardcoded platform-specific scene-id remapping in core with a generic authored scene map that optional runtime callers can use when they want to translate one scene id into another.

## Problem

`PlatformMenuSceneResolver` currently hardcodes product-specific scene names and platform branching inside `helengine.core`. That creates two problems:

1. Core contains demo-disc and platform policy that does not belong there.
2. Callers that need scene-id remapping have no generic authored mechanism to opt into.

The replacement needs to stay optional. `SceneManager` should continue loading exactly the scene id it is given. Only components or systems that explicitly want mapping should consult the new authored map.

## Requirements

1. Add a generic `SceneMapComponent` in `helengine.core`.
2. `SceneMapComponent` stores a dictionary of `string -> string`.
3. `SceneMapComponent` has no platform awareness.
4. Platform-specific variations use the existing per-platform component override system outside the component schema.
5. Runtime callers can request a mapped scene id through a dedicated runtime service.
6. If no map exists, the original scene id is returned unchanged.
7. If a map exists but the requested key is missing, the original scene id is returned unchanged.
8. Only one loaded `SceneMapComponent` may exist globally across currently loaded scenes.
9. If more than one `SceneMapComponent` is loaded, runtime resolution throws.
10. The first migrated caller is `DemoDiscReturnToMenuRuntimeComponent`.
11. The old `PlatformMenuSceneResolver` should become removable after migration.

## Non-Goals

1. `SceneManager` does not automatically remap scene ids.
2. The new component does not introduce platform-name fields, default/override slots, or special fallback rules beyond returning the original key.
3. This change does not design the final menu UI workflow beyond making the generic map available.

## Proposed Architecture

### SceneMapComponent

Add a new runtime/authored component type named `SceneMapComponent`.

Responsibilities:

1. Store authored mapping entries from source scene id to target scene id.
2. Expose lookup data in a simple, generic form.

Constraints:

1. The component is passive data.
2. The component does not self-register global state.
3. The component does not inspect platform information.

## SceneMapService

Add a runtime service in `helengine.core` responsible for resolving mappings for callers that opt in.

Responsibilities:

1. Search currently loaded scenes for the singleton `SceneMapComponent`.
2. Validate the singleton rule.
3. Map requested scene ids using the component dictionary.
4. Return the original scene id unchanged when no mapping applies.

Suggested runtime contract:

```csharp
string MapSceneId(string sceneId)
```

Behavior:

1. If `sceneId` is null, empty, or whitespace, throw `ArgumentException`.
2. If no `SceneMapComponent` is loaded, return `sceneId`.
3. If exactly one `SceneMapComponent` is loaded and the dictionary contains `sceneId`, return the mapped value.
4. If exactly one `SceneMapComponent` is loaded and the dictionary does not contain `sceneId`, return `sceneId`.
5. If more than one `SceneMapComponent` is loaded, throw `InvalidOperationException`.

## Lookup Strategy

The service should discover `SceneMapComponent` by traversing the currently loaded scene roots recursively and inspecting attached components.

Reasoning:

1. It avoids introducing hidden static registration state into component lifecycle.
2. It keeps the component passive and scene-authored.
3. It matches the optional nature of the feature.

The expected usage pattern is low frequency, such as menu transitions, so a simple traversal is acceptable.

## Integration

### DemoDiscReturnToMenuRuntimeComponent

Replace the call to `PlatformMenuSceneResolver.ResolveMainMenuSceneId()` with a call through `SceneMapService`.

The component should:

1. Start from its logical target scene id.
2. Ask `SceneMapService` for the mapped scene id.
3. Load the returned value through `SceneManager`.

This removes platform branching from the runtime component while keeping the caller behavior explicit.

### Persistent Scene Authoring

Projects such as `city` can place the singleton `SceneMapComponent` in a scene marked `DontUnload`.

That gives:

1. One always-available global map scene.
2. A clean separation between generic runtime resolution and project-authored mapping policy.

## Failure Model

1. Duplicate loaded `SceneMapComponent` instances are configuration errors and should throw.
2. Missing mappings are not errors and should return the original key unchanged.
3. Missing global map scene is not an error and should return the original key unchanged.

## Testing

Add focused tests for:

1. `SceneMapService` returns the original id when no map component is loaded.
2. `SceneMapService` returns the mapped id when the key exists.
3. `SceneMapService` returns the original id when the key does not exist.
4. `SceneMapService` throws when multiple `SceneMapComponent` instances are loaded.
5. `DemoDiscReturnToMenuRuntimeComponent` uses the mapped result when a map is present.
6. `DemoDiscReturnToMenuRuntimeComponent` preserves current behavior when no mapping applies.

## Migration

1. Introduce `SceneMapComponent` and `SceneMapService`.
2. Update the demo-disc return-to-menu runtime flow to use the service.
3. Remove `PlatformMenuSceneResolver` once no callers remain.
4. Author a `SceneMapComponent` in `city` later when the menu setup is ready.

## Risks

1. If scene traversal is implemented inconsistently, duplicate singleton detection could miss nested entities.
2. If callers begin using the service in hot paths, traversal cost may eventually justify caching. That is not required for the initial implementation.
3. Existing tests that assert hardcoded resolver behavior will need to move to service-driven behavior.

## Recommendation

Implement `SceneMapComponent` as a passive dictionary component and `SceneMapService` as the only runtime lookup seam. Migrate explicit callers one by one, starting with `DemoDiscReturnToMenuRuntimeComponent`, and keep `SceneManager` free of automatic remapping behavior.

# Platform Entity And Component Existence Design

**Date:** 2026-07-05

## Goal

Add generic per-platform existence overrides for authored scene entities and components so one common scene can describe platform-specific presence without generator-owned scene variants.

The primary use case is authoring shared scenes where DS and 3DS own a second bottom-screen camera entity that should not exist on other platforms.

## User Direction

- Keep one authored scene as the source of truth.
- Add per-platform `Exists` authoring for both entities and components.
- Default `Exists` to `true` on every platform.
- Use per-platform overrides only to opt out.
- When an entity does not exist on a platform, remove its full child subtree on that platform.
- Use this generic system instead of maintaining separate platform-specific scene variants for cases like DS and 3DS bottom-screen cameras.

## Problem

The engine already has some platform-specific scene override infrastructure, but it stops short of the actual authoring model Helena wants.

Today:

- entities already support common `Enabled` state
- entities already support per-platform transform overrides
- entities already support per-platform component existence overrides
- components already support per-platform payload overrides and synthetic platform members

But:

- there is no per-platform entity existence override
- component existence is modeled through remove/add platform override data, not through an explicit `Exists` authoring checkbox
- common scenes still need generated platform variants when a whole entity subtree should exist only on handheld dual-screen targets

That is the wrong seam for shared authored content. The authoring contract should describe platform presence directly, and the packager should resolve it before runtime payload cooking.

## Non-Goals

- Add platform-only entity creation in the first pass.
- Change common entity deletion behavior outside platform editing mode.
- Reinterpret common `Enabled` as platform existence.
- Add runtime hot-switching of entity existence by platform after the scene is packaged.
- Filter the scene hierarchy or viewport live preview to hide removed entities in the first pass.
- Replace existing per-platform transform or component property override behavior.

## Current State

### Common entity state

`SceneEntityAsset` now persists a common `Enabled` flag. That controls runtime activation after the entity loads, not whether the entity exists in the packaged scene at all.

This distinction must remain:

- `Enabled = false` means the entity still exists in the scene and can still participate in authored references and hierarchy structure.
- platform `Exists = false` means the entity and its full subtree are omitted from the packaged target-platform scene.

### Existing component existence foundation

The editor already stores per-platform component existence metadata through `EntityPlatformComponentOverrideState`, and scene save/load already persists that state through `SceneEntityPlatformComponentOverrideAsset`.

The packager already applies target-platform component existence overrides before runtime scene output is written.

That means the underlying component model already exists. The missing piece is a direct inspector-facing `Exists` authoring contract and a parallel entity-level existence contract.

### Existing packaging order

`EditorWindowsBuildScenePackager` currently:

1. normalizes entity layer masks
2. applies target-platform transform overrides
3. applies target-platform component overrides
4. rewrites component payloads
5. recurses into children

Entity existence must become an earlier step so removed subtrees are pruned before any child packaging work runs.

## Decision

Adopt one generic authored-scene contract:

- entities have per-platform `Exists` overrides
- components have per-platform `Exists` overrides
- only opt-out overrides are stored
- packaging resolves existence before runtime component rewriting and cooking

This keeps authored scenes shared and makes handheld-only entities, such as bottom-screen cameras, ordinary platform overrides instead of generator policy.

## Authoring Model

### Entity existence

In platform editing mode, the selected entity exposes one `Exists` checkbox for the active non-common platform.

Rules:

- default effective value is `true`
- unchecking `Exists` on a platform marks the entity absent for that platform
- a platform-absent entity removes its entire child subtree for that platform
- checking `Exists` again reverts the platform override back to common authored behavior

The common tab does not need a platform `Exists` checkbox. Deleting the entity from the common scene remains the way to remove it from all platforms.

### Component existence

In platform editing mode, every component section exposes one `Exists` checkbox for the active non-common platform.

Rules:

- default effective value is `true`
- unchecking `Exists` for a common component uses the existing component-removal override path
- checking `Exists` again reverts that platform removal override
- for platform-only added components, unchecking `Exists` removes that platform-only component override entry

This keeps the persisted component model intact while replacing the current implicit remove/revert authoring with an explicit existence control.

## Persistence Design

### Entity save-state metadata

Add editor-owned entity existence override metadata parallel to the existing transform and component override containers stored on `EntitySaveComponent`.

Use:

- `SceneEntityPlatformExistenceOverrideAsset`
  - `PlatformId`
  - `Exists`

Only non-common overrides need to be stored. Because the default is `true`, old scene assets with no existence overrides continue to behave exactly as they do today.

### Scene asset contract

Extend `SceneEntityAsset` with:

- `PlatformExistenceOverrides : SceneEntityPlatformExistenceOverrideAsset[]`

This keeps entity existence parallel with the existing fields:

- `PlatformTransformOverrides`
- `PlatformComponentOverrides`

Scene save/load should round-trip the editor-only override metadata without mutating common authored entity state.

## Packaging Design

### Resolution order

Packaging should resolve platform-specific scene data in this order:

1. entity existence
2. entity transform overrides
3. component existence overrides
4. component payload rewriting and cook-time transforms
5. recursive packaging of remaining children

### Entity subtree pruning

When the target platform resolves `Exists = false` for an entity:

- that entity is removed from its parent `Children` array before child recursion
- no transform overrides are applied to that entity
- no component overrides are applied to that entity
- no component payload rewriting or cook-time transforms run for that entity
- none of its descendants survive into the packaged scene

This is the behavior needed for handheld-only bottom-screen camera subtrees.

### Packaged output

Packaged scenes should not carry editor-only platform existence metadata after the selected platform has been resolved, just as packaged scenes should not retain unresolved transform or component override metadata.

## Editor UX

### Entity inspector

Add a platform-specific `Exists` row to the entity-level properties UI when editing a non-common platform.

Behavior:

- common tab: no platform existence row
- platform tab: checkbox shows the effective existence for the selected platform
- toggling the checkbox updates the hidden save metadata only
- reverting clears the explicit platform override and returns to common behavior

### Component inspector

Expose the existing component existence model as an `Exists` row at the top of each component section in platform editing mode.

Behavior:

- common tab: no platform existence row
- platform tab: checkbox reflects whether the component exists on that platform
- removed common components remain inspectable enough to re-enable through the same section
- platform-only added components can be removed by unchecking `Exists`

This keeps the editor interaction symmetric between entities and components.

## Compatibility

This feature should preserve existing authored scenes.

Compatibility rules:

- scenes with no platform existence overrides continue to package exactly as before
- existing per-platform component removal/addition data continues to load and package
- common `Enabled` continues to control runtime enabled state after load
- no migration of existing scene assets is required

## Testing

Add focused tests for:

1. scene save/load roundtrip of entity platform existence overrides
2. component inspector `Exists` behavior for common and platform-only components
3. entity inspector `Exists` behavior for non-common platform editing
4. packager subtree pruning when an entity does not exist on the target platform
5. packager preservation of common `Enabled` semantics when platform existence remains `true`
6. coexistence of entity existence, transform overrides, and component overrides on the same subtree

## Success Criteria

- authors can keep one shared scene and mark entities or components absent on specific platforms
- entity absence removes the full subtree from packaged target-platform scenes
- component absence uses the existing override data but is authored through an explicit `Exists` checkbox
- packaged scenes for DS and 3DS can include handheld-only bottom-screen camera subtrees without generator-owned scene duplication
- old scenes with no existence overrides remain unchanged

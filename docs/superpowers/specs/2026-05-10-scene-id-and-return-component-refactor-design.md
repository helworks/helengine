## Summary

The current demo-disc scene flow leaks authored and packaged scene paths into gameplay code and menu definitions. `DemoDiscReturnToMenuComponent` in the city project is handling raw input polling, scene-path normalization, packaged-scene fallback rules, and editor-versus-runtime loading. That is the wrong boundary. Scene identity should be engine-owned, generic, and path-agnostic.

This refactor standardizes scene loading around stable scene ids derived from the scene asset name without extension. Runtime and editor systems will resolve those ids through shared engine/editor infrastructure. Gameplay code and menu definitions will only reference scene ids. The temporary direct-input fallback remains for now, but desktop-only keyboard checks must be explicitly gated so non-desktop builds never reference `Keys`.

## Goals

- Remove scene-path resolution logic from gameplay components.
- Make scene ids generic and engine-owned, derived from the scene asset name without extension.
- Ensure runtime scene loads use scene ids instead of authored or cooked paths.
- Ensure editor-side scene loading can also resolve a scene by scene id through shared infrastructure.
- Keep temporary direct input in `DemoDiscReturnToMenuComponent`, but restrict keyboard usage to desktop platforms only.

## Non-Goals

- Do not introduce a full input-action migration in this refactor.
- Do not redesign the complete menu/input architecture beyond the return component and scene-loading contract.
- Do not make city-specific rules part of engine/editor infrastructure.

## Current Problems

### Gameplay Code Owns Scene Path Rules

`DemoDiscReturnToMenuComponent` currently:

- polls raw keyboard and gamepad input
- resolves a scene id into an authored path
- tries packaged fallback path rewriting
- checks file existence inside the content root
- branches between editor and runtime load mechanisms

That logic is too large because it owns responsibilities that belong to shared engine/editor services.

### Menu Actions Use Paths Instead of Scene Ids

The demo-disc menu catalog currently stores values such as `scenes/rendering/cube_test.helen` inside `MenuActionDefinition(MenuActionKind.LoadScene, ...)`. That exposes authored layout details to gameplay/UI code and makes the action payload format depend on path conventions.

### Runtime Already Wants Scene Ids

`SceneManager.LoadScene(string sceneId, SceneLoadMode loadMode)` already resolves through `RuntimeSceneCatalog`. The runtime API shape is correct; the broader system is inconsistent because authored code still treats paths as public scene identity.

## Chosen Approach

Use generic scene ids everywhere above the manifest/content layer. The id is always the scene asset file name without extension. Paths remain internal implementation data owned by manifests and loaders.

This keeps the public scene-loading contract simple:

- authored/editor systems derive and index scenes by id
- runtime manifests expose scene ids plus cooked relative paths
- gameplay code asks to load `DemoDiscMainMenu`, `CubeTest`, `ColoredCubeGrid`, or `TexturedCubeGrid`
- shared infrastructure translates the id to the correct authored or cooked asset

## Scene Id Rules

### Derivation

The stable scene id is derived generically from the scene asset name:

- `Scenes/DemoDiscMainMenu.helen` -> `DemoDiscMainMenu`
- `scenes/rendering/cube_test.helen` -> `cube_test` if using file name literally

Because the user explicitly wants concise names based on the editor scene asset name, the implementation should derive the id from `Path.GetFileNameWithoutExtension(...)` and preserve the exact asset name already authored in the project. The engine should not special-case city naming.

### Ownership

- Engine/editor code owns scene-id derivation.
- Runtime manifest generation owns mapping `scene id -> cooked relative path`.
- Editor scene lookup owns mapping `scene id -> authored scene asset`.
- City and other projects should only consume scene ids, never construct them from paths manually unless authoring explicit menu content.

## Architecture Changes

### Runtime Scene Catalog

`RuntimeSceneCatalogEntry.SceneId` remains the public lookup key. The build pipeline should populate it using the derived scene id rather than an authored path identity.

`RuntimeSceneCatalog` remains a pure runtime lookup structure:

- key: scene id
- payload: cooked relative path

No gameplay code should inspect or construct cooked paths.

### Editor Scene Resolution

Add or adapt shared editor-side scene resolution so one scene id can be loaded in editor mode without gameplay code knowing the authored path.

The editor-side resolution contract should be generic:

- input: `sceneId`
- output: authored `SceneAsset`

This should be implemented in shared editor infrastructure, not in city code.

### Menu Actions

`MenuActionDefinition(MenuActionKind.LoadScene, value)` should now carry a scene id instead of a path.

That means:

- existing demo-disc scene menu entries should store ids such as `CubeTest` or whatever exact asset-name-derived ids the authored scenes expose
- runtime menu loading should continue to call `SceneManager.LoadScene(sceneId, ...)`
- editor preview or menu execution paths that currently assume path payloads must be updated to resolve by scene id instead

### Return Component

`DemoDiscReturnToMenuComponent` should be reduced to:

- read current input state
- detect whether return/back was pressed
- load `DemoDiscMainMenu` by scene id

The following methods should be removed from it:

- `ResolveSceneContentPath`
- `BuildPackagedSceneContentPath`
- `DoesContentFileExist`
- `NormalizeRelativeContentPath`

The runtime/editor split stays, but the scene lookup responsibility moves out:

- runtime: `Core.Instance.SceneManager.LoadScene("DemoDiscMainMenu", SceneLoadMode.Single)`
- editor: ask shared editor-oriented scene-id loader/resolver for `DemoDiscMainMenu`, then hand the resulting `SceneAsset` to the scene load service

## Input Rules

### Temporary Direct Input

This refactor keeps temporary direct input in `DemoDiscReturnToMenuComponent`.

Allowed for now:

- gamepad polling remains direct
- desktop keyboard polling remains direct

Required restriction:

- only desktop platforms may reference `Keys`
- non-desktop generated/player builds must not contain `Keys` references from this component

This should be enforced with explicit platform gating in the authored component so the generated native output for PSP/PS2 does not need post-generation cleanup for keyboard-only code.

### Future Direction

The longer-term direction is still input actions/contexts rather than raw `Keys` or `InputGamepadButton` checks in gameplay code. That is intentionally deferred.

## Error Handling

- Loading a scene by id should fail loudly when the id cannot be resolved.
- Editor scene-id resolution should throw a clear error if the requested authored scene is absent or ambiguous.
- Runtime scene loading should keep using `SceneManager` failure behavior when a scene id is missing from the runtime catalog.
- The return component should no longer throw path-specific "authored or packaged form" errors, because it should not know about those forms.

## Testing Strategy

### Engine and Editor Tests

- Add tests proving runtime scene catalog entries derive ids from scene asset file names.
- Add tests proving runtime manifest generation keeps cooked paths internal while exposing scene ids publicly.
- Add tests proving editor-side scene lookup can resolve an authored scene by scene id.
- Add tests proving menu scene actions execute using scene ids rather than path payloads.

### Gameplay-Facing Tests

- Add tests for `DemoDiscReturnToMenuComponent` behavior that verify:
  - it loads `DemoDiscMainMenu` by scene id
  - it no longer contains or depends on scene-path resolution logic
  - desktop keyboard checks remain available only for desktop-targeted builds
  - gamepad return input still works

### Regression Coverage

Add targeted regression coverage around the generic scene-id contract so PSP/PS2 work does not accidentally reintroduce path-based scene identity or keyboard enum leakage.

## Implementation Outline

1. Update shared scene-id derivation and manifest/catalog generation to use asset-name-derived ids.
2. Add shared editor-side scene-id resolution.
3. Update menu scene action flows to treat `LoadScene` payloads as scene ids.
4. Refactor `DemoDiscReturnToMenuComponent` to remove path logic and use scene-id loading only.
5. Gate desktop keyboard checks so non-desktop targets never touch `Keys`.
6. Verify runtime and editor menu scene transitions still work.

## Risks

- Existing menu or editor flows may implicitly assume `LoadScene` action payloads are paths.
- Scene-id collisions become more visible when ids are derived from file names alone.
- Desktop-only input gating must be done in authored code carefully so codegen does not emit invalid non-desktop references.

## Recommendation

Proceed with the refactor as a generic engine/editor scene-id cleanup with a small city gameplay surface change. The city project should only consume scene ids. Shared infrastructure should own all path mapping and scene lookup behavior.

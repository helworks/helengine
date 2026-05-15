# City Scene Generator Ownership Migration Design

## Goal

Move all remaining city/demo-disc/rendering scene generators out of `helengine` and into `city`, and require them to author live entities that are persisted through the editor scene save pipeline instead of manually constructing serialized `SceneAsset` / `SceneEntityAsset` records.

This migration is an ownership-boundary correction first. It also fixes the immediate FPS-scaling issue by ensuring project-owned scene content is authored under the same live UI hierarchy and reference-canvas rules as the rest of the project, instead of being patched into engine-side manual scene serializers.

## Problem

The current scene-generation model is split across two different boundaries:

- newer city-owned generators such as `CubeTestSceneFactory` already build live entities and save through `GeneratedAuthoringSceneWriteService`
- older menu/demo-disc/rendering generators still live inside `helengine` and manually construct serialized scene records

That older path is the wrong abstraction for project-authored scenes:

- it forces project scene code to understand engine persistence internals
- it makes project behavior depend on `SceneComponentAssetRecord` ordering and hand-authored payload details
- it produces brittle scene structure bugs, such as the recent FPS overlay placement regressions
- it hides normal runtime structure behind engine-only authoring helpers instead of letting the editor save path define the scene contract

The engine should own generic save/load/packaging infrastructure. The project should own project scenes.

## Scope

This migration covers every remaining project scene generator that still lives in `helengine` or still manually constructs serialized scene assets for city/demo-disc/rendering content.

The concrete known generators in scope are:

- `engine/helengine.editor/managers/menu/DemoMenuSceneAssetFactory.cs`
- `tools/demo-disc-scene-writer/RenderingSceneWriter.cs`
- `tools/demo-disc-scene-writer/DirectionalShadowPlazaSceneAssetFactory.cs`

The migration also includes the call sites and tests that currently depend on those engine-side generators.

This design does not change:

- the generic editor save pipeline
- scene packaging architecture
- runtime scene loading architecture
- scene ids, output paths, or high-level demo-disc feature behavior

## Ownership Boundary

After the migration:

### `helengine` owns

- `SceneSaveService`
- persistence descriptors
- scene packaging transforms
- runtime scene loading
- generic editor/entity infrastructure

### `city` owns

- demo-disc menu scene generation
- rendering showcase scene generation
- FPS overlay placement decisions for project scenes
- all project-authored scene content and hierarchy decisions

The rule becomes simple:

> Project scenes are authored in the project, using live entities and normal components, and persisted through the editor scene save boundary.

## Target Architecture

All city-owned generators should converge on the same pattern already used by the newer city rendering generators:

1. Build live `Entity` roots using normal component APIs.
2. Return a `GeneratedAuthoringSceneDefinition`.
3. Persist the scene through `GeneratedAuthoringSceneWriteService`.
4. Let `SceneSaveService` produce the final `.helen` file.

No project scene generator should manually:

- build `SceneAsset`
- build `SceneEntityAsset`
- build `SceneComponentAssetRecord`
- serialize editor asset payloads by hand
- manage artificial placeholder runtime objects purely to satisfy serializer internals

## Migration Shape

The migration will be a full ownership move now, not a staged compatibility layer.

### City additions

Add city-owned live authoring replacements for:

- the baked demo menu scene factory
- the directional shadow plaza scene factory
- the remaining rendering/demo-disc scene writer entrypoints that currently hand-build serialized scene assets

These replacements should live alongside the existing city rendering generation code and use the same live-authoring conventions already established there.

### Engine removals

Remove the corresponding project-scene generator responsibilities from `helengine` once the city-owned path is in place and verified.

`helengine` should not keep “temporary” parallel generators for the same city scenes.

## Authoring Rules

### Scene structure

The authored scene structure must preserve stable runtime behavior where existing systems depend on it.

Examples:

- demo menu scene ids must remain stable
- generated `.helen` output paths must remain stable
- entity names that are already used by tests or runtime code should remain stable unless there is a strong reason to change them

### Menu hierarchy

The baked demo menu must continue to satisfy `MenuComponent` runtime expectations.

In particular:

- the menu root must still expose exactly one generated menu subtree child
- the generated subtree must still contain the panel/item hierarchy in the expected shape

If an FPS overlay is authored for the menu, it must be placed so that it participates in the fitted UI hierarchy without altering the runtime child-layout assumptions that `MenuComponent` depends on.

The preferred placement is under the already-fitted generated UI subtree rather than on the menu root or camera root.

### FPS overlay scaling

The FPS overlay should not gain special runtime auto-attach logic.

Instead, project scenes should author the FPS component under a `ReferenceCanvasFitComponent` subtree so it scales through the same existing UI rules as the rest of the scene.

That keeps scaling policy in scene authoring instead of inside `FPSComponent`.

## Data Flow

For each migrated scene generator, the flow should be:

1. Resolve or receive the required runtime assets.
2. Build live authored root entities using normal project/engine components.
3. Package those roots into `GeneratedAuthoringSceneDefinition`.
4. Call `GeneratedAuthoringSceneWriteService.WriteScene(...)`.
5. Let `SceneSaveService` serialize the scene into the project assets folder.
6. Let existing build/package flow consume the resulting saved `.helen` scene.

This preserves one clear boundary:

- project code decides what the scene is
- editor save code decides how a scene becomes a serialized asset

## Testing Strategy

Tests must shift away from persistence-internals assertions and toward authored-scene behavior and saved-scene shape.

### Required coverage

1. City scene authoring tests
   - generated scenes deserialize successfully after save
   - expected roots and important components are present
   - menu scene shape still matches runtime expectations
   - FPS overlays appear under the intended fitted UI subtree

2. Packaging tests
   - migrated scenes still package successfully for Windows
   - cooked scene structure remains loadable
   - gameplay/menu-specific components still survive the intended packaging path

3. Runtime menu tests
   - baked menu still initializes
   - initial panel state is correct
   - scene-loading menu items still work

### Tests that should be removed or rewritten

Tests that assert engine-side manual serialization details should be replaced.

Examples of brittle assertions that should not survive:

- “FPS overlay is baked on the camera root”
- assertions tied to manual component record indices
- assertions that depend on engine-side generator implementation details rather than saved scene behavior

## Error Handling

The migration should preserve strict failure behavior.

- If required runtime assets are missing, scene generation should fail loudly.
- If the save pipeline cannot persist a generated scene, the generator should fail rather than silently degrading to a manual serialization fallback.
- No compatibility shim should silently rebuild project scenes through old engine-side record construction.

## Compatibility and Risk

The main risks are:

- breaking menu runtime hierarchy assumptions
- changing saved scene shape in a way that packaging tests do not cover
- leaving a split world where some project scenes still depend on engine-side generators

The mitigation is to migrate all remaining project scene generators in one ownership move and verify them through save-path and packaging tests before removing the old engine-side path.

## Success Criteria

The migration is complete when all of the following are true:

- city/demo-disc/rendering scene generators no longer live in `helengine`
- project scene generators no longer manually build serialized scene records
- all migrated scenes save through the editor scene save pipeline
- FPS overlay placement is authored as part of normal scene hierarchy construction
- runtime menu behavior still works
- packaging still succeeds for the migrated scenes
- old engine-side project scene generators are deleted

## Non-Goals

This migration does not attempt to:

- redesign `SceneSaveService`
- redesign packaging architecture
- redesign runtime menu behavior
- add special-case runtime logic to `FPSComponent`
- preserve old engine-side project scene generators as a long-term compatibility layer

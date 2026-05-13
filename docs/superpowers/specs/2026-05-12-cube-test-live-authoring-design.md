# Cube Test Live Authoring Design

## Goal

Replace the current `CubeTestSceneFactory` manual scene-serialization path with a live authoring path inside the `city` project.

The `cube_test` generator should:

- create a normal scene graph using live `EditorEntity` instances
- attach normal engine and gameplay components directly
- use the editor scene-save path to serialize the scene

The `city` project should no longer need to know about:

- `SceneComponentAssetRecord`
- tagged binary payload writers
- serialized component type ids
- component-specific payload layouts

This change is intentionally limited to `cube_test` so the new path can be validated before migrating any other generators.

## Problem

`CubeTestSceneFactory` currently hand-builds a `SceneAsset` and manually serializes component payloads. That forces project code to understand editor persistence internals that should remain engine-owned.

Current problems:

- the factory hard-codes serialized component type ids
- the factory hand-writes camera payload fields
- the factory hand-serializes mesh and light payloads
- the factory constructs runtime script component records explicitly
- format churn in editor serialization leaks into `city` project code

This is the wrong boundary. The generator should describe scene content, not binary scene encoding.

## Scope

This design covers only:

- `CubeTestSceneFactory`
- one new city-side writer/adapter that persists live authored scenes through editor serialization
- the `RenderingSceneGenerator` integration needed to use the new path for `cube_test`
- focused regression coverage for the new path

This design does not yet cover:

- `ColoredCubeGridSceneFactory`
- `TexturedCubeGridSceneFactory`
- axis scenes
- directional-shadow plaza
- spotlight street slice
- demo-disc main menu generation

Those generators will remain on the old path until `cube_test` proves the live authoring flow works.

## Desired Architecture

### 1. `CubeTestSceneFactory` becomes a live-scene authoring factory

`CubeTestSceneFactory` should stop returning a serialized `SceneAsset`.

Instead it should build a live authored scene using:

- `EditorEntity` roots
- normal attached components
- actual gameplay script component instances where needed

Responsibilities kept in the factory:

- entity names
- transforms
- scene hierarchy
- component selection
- authored component property values
- stable generated/file asset references needed by attached components

Responsibilities removed from the factory:

- serialized component type ids
- payload-byte generation
- direct `SceneComponentAssetRecord` creation
- direct `SceneAsset` construction

### 2. Add one city-side scene write adapter

Add a city-side service dedicated to persisting one generated live authored scene through the editor save path.

Responsibilities:

- create the editor persistence registry needed for authored scene save
- stage the live scene roots into the editor runtime in a controlled way
- invoke `SceneSaveService`
- write the final `.helen` file for `cube_test`
- isolate all editor serialization wiring away from content factories

This service should be the only city-side code that knows how to bridge from live authoring entities into the editor serializer.

### 3. `RenderingSceneGenerator` uses the new path only for `cube_test`

`RenderingSceneGenerator` should switch `cube_test` to the new live-authoring writer path while leaving every other scene on the current `SceneAsset` path.

This keeps risk localized:

- one migrated scene
- one unchanged baseline for comparison
- no large multi-scene serialization rewrite in one step

## Proposed Data Flow

The new `cube_test` flow should be:

1. `RenderingSceneGenerator.Generate(projectRootPath)` resolves the generated asset references needed by `cube_test`.
2. `CubeTestSceneFactory` builds live root `EditorEntity` instances.
3. The new city-side write service receives the scene id/path plus those root entities.
4. The write service routes them through the editor scene-save path.
5. The editor serializer emits `assets/scenes/rendering/cube_test.helen`.

Other rendering scenes continue using the current path until they are migrated in later changes.

## Live Authoring Requirements

The live authored `cube_test` scene must preserve the existing authored content shape unless intentionally changed later.

The authored scene must still contain:

- `CubeTestCamera`
- `CubeTestSun`
- `CubeTestCube`

The authored scene must still preserve the current transforms:

- camera at `(0, 0, 6)`
- sun at `(0, 4, 0)` with the same yaw/pitch orientation
- cube at origin with scale `(2, 2, 2)`

The authored scene must still attach the same logical component set that the current factory intends to author for this scene at the moment of migration.

This migration is a persistence-boundary refactor first. Behavioral cleanup, such as removing stale motion scripts from `cube_test`, should remain a separate follow-up unless explicitly included in the implementation after the new path is proven.

## Serialization Strategy

The implementation should reuse the editor serializer rather than reproducing it.

The city-side adapter should rely on the editor scene save flow built around:

- `ComponentPersistenceRegistry`
- `SceneSaveService`
- normal entity/component traversal performed by the editor serializer

The adapter should not recreate the save logic manually.

The adapter should not duplicate serializer internals such as:

- hidden component filtering
- component descriptor lookup
- asset reference harvesting
- stable entity id assignment
- platform override wrapping

Those concerns must stay owned by the editor serialization layer.

## Integration Constraints

The rework should keep generators inside the `city` project because scene formats are still changing frequently and regeneration is expected to remain city-owned for now.

The rework should not move `cube_test` generation into shared engine/editor code.

The rework should also avoid a broad “generator framework” abstraction. For this slice, a narrow city-side writer adapter is enough.

## Testing Strategy

The implementation should add focused regression tests for this migration.

### Required tests

1. A generator test that exercises the new `cube_test` live-authoring save path.
2. A scene-authoring test that deserializes the resulting `cube_test.helen` and confirms the expected entities and components are present.
3. A packaging test that proves the generated `cube_test` scene still cooks successfully after the refactor.

### What the tests should prove

- `CubeTestSceneFactory` no longer manually authors serialized component records
- the generated `.helen` remains loadable by existing editor/runtime consumers
- the build pipeline still accepts the regenerated `cube_test`

### What the tests do not need to prove in this slice

- migration of other scene generators
- removal of every stale motion-script path in the rendering pipeline
- demo-disc main menu regeneration changes

## Risks

### Editor runtime dependency during generation

The editor save path serializes the live editor scene by walking `Core.Instance.ObjectManager.Entities`. The city-side adapter therefore needs a controlled way to author temporary editor entities, save them, and clean them up without leaking state into unrelated scenes or tests.

The implementation should keep this staging logic isolated in the new writer service.

### Asset reference persistence

The current manual path explicitly sets asset references for the generated cube model, standard material, and menu font-related components. The live authoring path must ensure attached components still persist those references through normal save-state mechanisms instead of silently dropping them.

This is exactly why the migration must be validated with deserialization and packaging tests.

### Scope drift

It will be tempting to migrate other rendering scenes at the same time because they share the same smell. That should be avoided in this change. The implementation should end once `cube_test` proves the pattern.

## Recommended Implementation Shape

The preferred implementation shape is:

- refactor `CubeTestSceneFactory` into a live authoring factory
- add one narrow city-side `GeneratedAuthoringSceneWriteService`
- change `RenderingSceneGenerator` so only `cube_test` uses that new service
- leave the rest of the generator system untouched

This is the smallest useful slice that moves authoring responsibility to the right layer and gives a safe template for later migrations.

## Non-Goals

This design does not attempt to:

- redesign all rendering-scene generators at once
- remove every stale directional-shadow motion rewrite in packaging
- migrate the main menu generator in the same change
- redesign scene persistence APIs inside the engine/editor

Those can follow once `cube_test` validates the approach.

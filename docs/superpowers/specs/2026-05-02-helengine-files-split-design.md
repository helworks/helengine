# helengine.files Split Design

## Goal

Move all write-side serialization and packaging code out of `helengine.core` and into a new `helengine.files` project, while keeping runtime read-side deserialization in `helengine.core`.

This split is for memory and platform clarity:

- `helengine.core` remains the runtime foundation for player builds.
- `helengine.files` owns editor/export writing paths.
- `helengine.editor` orchestrates save/export/build and uses both projects.

## Boundary Rules

### `helengine.core`

`helengine.core` keeps only code needed to run a game and load packaged content:

- runtime asset deserialization
- runtime scene deserialization
- runtime component deserialization
- runtime asset and scene models
- runtime reader-side binary helpers

`helengine.core` must not own:

- asset writers
- scene writers
- editor export serializers
- packaging helpers that only emit files

### `helengine.files`

`helengine.files` owns all write-side serialization and export logic:

- asset serialization
- scene serialization
- packaged asset writers
- binary writer helpers
- file-format helpers used by the editor/export pipeline

`helengine.files` must not own runtime-only player loading code.

### `helengine.editor`

`helengine.editor` keeps the authoring and packaging orchestration:

- scene/component descriptors
- build and save UI
- packaging flow
- project settings

The editor calls into `helengine.files` for writes and `helengine.core` for runtime models and readers.

## Project Shape

Introduce a new project:

- `engine/helengine.files/helengine.files.csproj`

The project should follow the existing engine project conventions:

- .NET `net9.0`
- same build settings as the other engine projects
- no direct dependency on the editor UI stack

## What Moves

The following write-side code should move out of `helengine.core`:

- `AssetSerializer.Serialize(...)`
- `FontAssetBinarySerializer.Serialize(...)`
- `EditorAssetBinarySerializer.Serialize(...)`
- `EngineBinaryWriter`
- `ShaderModulePackageWriter`
- any other writer-only helper discovered during the split

The following runtime read-side code stays in `helengine.core`:

- `AssetSerializer.Deserialize(...)`
- `FontAssetBinarySerializer.Deserialize(...)`
- `RuntimeSceneLoadService`
- runtime component deserializers
- `EngineBinaryHeader`
- binary reader helpers needed by runtime loading

## Read/Write Split

The serializer boundary should be explicit:

- read side in `helengine.core`
- write side in `helengine.files`

If a class currently does both, it should be split rather than left half-and-half in core.

Expected examples:

- `AssetReader` in `helengine.core`, `AssetWriter` in `helengine.files`
- `FontAssetReader` in `helengine.core`, `FontAssetWriter` in `helengine.files`
- `EngineBinaryHeaderReader` in `helengine.core`, `EngineBinaryHeaderWriter` in `helengine.files`

## Data Flow

### Editor Export

1. The editor loads scene/component metadata.
2. The editor writes scene and asset output through `helengine.files`.
3. The write path emits packaged data.
4. The packaged output is consumed by player builds.

### Player Runtime

1. The player loads packaged assets and scenes.
2. The player deserializes through `helengine.core`.
3. No write-side code is required in the player runtime.

## Low-Memory Target Impact

The split is intended to reduce unnecessary code in low-memory player builds:

- write-only serializers are isolated from runtime code
- linker stripping becomes more effective
- runtime images should not drag in editor export helpers unless they are directly referenced

This is a code-organization improvement, not a guarantee by itself. The exact dead-stripping behavior still depends on the platform compiler/linker settings.

## Migration Plan

1. Add the new `helengine.files` project.
2. Move write-side serializer and writer code into `helengine.files`.
3. Update `helengine.editor` references to use `helengine.files` for exports.
4. Keep `helengine.core` reader paths intact.
5. Update tests so they cover both the runtime read side and the editor write side.

## Testing

The split should be verified with:

- a build of `helengine.core` after writer code is removed
- a build of `helengine.files`
- a build of `helengine.editor` referencing both projects
- runtime scene/asset deserialization tests still passing from `helengine.core`
- editor write-path tests still passing through `helengine.files`

## Non-Goals

This split does not:

- change the scene/component data model
- redesign the editor build pipeline
- move runtime deserializers out of core
- change the platform builder metadata model


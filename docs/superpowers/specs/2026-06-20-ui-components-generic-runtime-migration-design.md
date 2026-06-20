# UI Components Generic Runtime Migration Design

## Goal

Migrate the next largest safe batch of built-in pure-data components from bespoke packaged runtime deserializers and packaging rewrite paths to the shared automatic runtime component system.

This wave follows the completed lights migration and keeps `CameraComponent` deferred.

## User Direction

- Keep the migration aggressive.
- Do not preserve backward compatibility for authored or cooked payloads.
- Take the next largest safe batch.
- Asset-backed components are acceptable when they already use the shared asset-reference serialization path.
- Keep `CameraComponent` for later.

## In Scope

This wave includes:

- `RoundedRectComponent`
- `SpriteComponent`
- `TextComponent`

## Out of Scope

This wave does not include:

- `CameraComponent`
- `MeshComponent`
- `FPSComponent`
- `DebugComponent`
- built-in physics components

## Why This Batch

These three components are the largest remaining batch that still fits the current generic system without additional structural work:

- `RoundedRectComponent` is plain reflected data.
- `SpriteComponent` uses `RuntimeTexture`, which is already supported by shared automatic asset-reference persistence.
- `TextComponent` uses `FontAsset`, which is already supported by shared automatic asset-reference persistence, and its runtime-only `Texture` member is already ignored by scene persistence.

They produce more cleanup than a `RoundedRect`-only pass while still avoiding the non-reflection-friendly authored shape in `MeshComponent` and the explicit nested camera payload concerns in `CameraComponent`.

## Current State

### Runtime registration

`RuntimeComponentRegistry` still explicitly registers:

- `RuntimeTextComponentDeserializer`
- `RuntimeSpriteComponentDeserializer`
- `RuntimeRoundedRectComponentDeserializer`

### Packaging

`SceneComponentPackagingTransformService` still contains special rewrite branches and helpers for:

- `RewriteTextComponentRecord`
- `RewriteSpriteComponentRecord`
- `RewriteRoundedRectComponentRecord`

### Generated native runtime support

Generated native runtime deserializers now cover automatically persisted built-ins like `SceneMapComponent` and the four light components.

The same generated path should cover these three UI components once the explicit overlap is removed.

## Target State

After this migration:

- `RoundedRectComponent`, `SpriteComponent`, and `TextComponent` package through the shared automatic runtime payload builder
- managed runtime loading resolves them through `AutomaticScriptComponentRuntimeDeserializer`
- native player builds use generated `GeneratedRuntime...Deserializer` classes for all three
- explicit runtime deserializers are deleted
- explicit packaging rewrite branches are deleted
- explicit runtime registration is removed

## Shared Asset-Reference Constraint

This migration relies on the existing shared automatic asset-reference system already supporting:

- `RuntimeTexture`
- `FontAsset`

That means:

- `SpriteComponent.Texture` can flow through the automatic path
- `TextComponent.Font` can flow through the automatic path

No new generic asset-reference feature is required for this wave.

## Component-Specific Notes

### `RoundedRectComponent`

Safe because it is reflected data only:

- render order
- layer mask
- corners
- rotation
- color
- source rect
- size
- radius
- border thickness
- fill color
- border color

### `SpriteComponent`

Safe because:

- `Texture : RuntimeTexture` is already a supported automatic asset-backed member
- the rest of the authored state is reflected scalar/vector data

Authored members expected to participate:

- `RenderOrder2D`
- `Texture`
- `Rotation`
- `LayerMask`
- `SourceRect`
- `Size`
- `Color`

### `TextComponent`

Safe enough for this wave because:

- `Font : FontAsset` is already a supported automatic asset-backed member
- runtime-only texture backing is already marked `[ScenePersistenceIgnore]`
- authored interaction state can still flow through reflection

Important authored members expected to participate:

- `RenderOrder2D`
- `Rotation`
- `SourceRect`
- `Size`
- `Color`
- `Text`
- `WrapText`
- `Font`
- `FontScale`
- `Alignment`
- `ConvertTextToSprite`
- `LayerMask`
- `SelectionEnabled`

Important ignored/runtime-only members remain excluded:

- `Texture`

## Data Format

Packaged runtime payloads for these components will use the shared automatic runtime format:

1. automatic payload version
2. reflected member count
3. reflected member values in deterministic reflected schema order

This replaces the bespoke UI component runtime payload formats.

## Files Expected To Change

### Runtime

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- delete:
  - `engine/helengine.core/scene/runtime/RuntimeTextComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeSpriteComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeRoundedRectComponentDeserializer.cs`

### Packaging and generation

- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`

### Tests

- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

Additional UI-focused persistence or packaging tests may need expectation updates if they currently assume the bespoke runtime payload shapes.

## Testing Strategy

### Generated runtime tests

Update generated-runtime overlap tests to verify:

- `TextComponent` generated runtime deserializer is emitted
- `SpriteComponent` generated runtime deserializer is emitted
- `RoundedRectComponent` generated runtime deserializer is emitted

### Managed runtime load tests

Update or add tests to verify packaged automatic runtime payloads for:

- `TextComponent`
- `SpriteComponent`
- `RoundedRectComponent`

These tests should load through `RuntimeSceneLoadService` using the default registry and assert authored values.

### Packaging tests

Update packaging tests to verify:

- packaged record type ids use `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(...)`
- packaged payload version is `AutomaticScriptComponentRuntimeDeserializer.CurrentVersion`
- packaged scenes remain loadable through the default runtime registry

## Risks

### `TextComponent` reflected state breadth

`TextComponent` has more authored members than the rest of the batch, including selection-related authored state.

This is acceptable for the aggressive migration, but tests must assert the intended authored state explicitly.

### Native generated overlap

If any of the three explicit runtime deserializers remain registered, generated/native runtime paths can diverge or double-register.

The overlap must be removed completely.

### Old bespoke payload helpers in tests

Some tests may still create the old strict payload format directly.

Those tests should be rewritten to use the automatic runtime payload shape rather than preserved.

## Recommendation

Proceed with this exact batch:

- `RoundedRectComponent`
- `SpriteComponent`
- `TextComponent`

Keep `CameraComponent` out of this wave, then reassess whether `CameraComponent` or `MeshComponent` is the next migration candidate after the UI batch lands.

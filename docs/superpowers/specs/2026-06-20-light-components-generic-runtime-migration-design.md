# Light Components Generic Runtime Migration Design

## Goal

Migrate the built-in light components from bespoke packaged runtime deserializers and packaging rewrite paths to the shared automatic component persistence/runtime pipeline.

This is the first small-step follow-up after the generic dictionary and `SceneMapComponent` migration.

## User Direction

- Keep the migration aggressive.
- Do not preserve backward compatibility for authored or cooked payloads.
- Take small steps.
- Start with pure-data components first.

## First-Wave Scope

This wave includes only:

- `AmbientLightComponent`
- `DirectionalLightComponent`
- `PointLightComponent`
- `SpotLightComponent`

## Why These Components First

These components are the cleanest built-in candidates for the generic path:

- their authored state is simple data
- they do not rely on asset-reference resolution
- they do not expose awkward non-writable collection shapes
- their current runtime deserializers are already thin wrappers
- their packaging rewrite logic is isolated and easy to remove

They provide a low-risk migration pattern for later waves.

## Out of Scope

This wave does not migrate:

- `MeshComponent`
- `CameraComponent`
- `FPSComponent`
- `DebugComponent`
- `TextComponent`
- `SpriteComponent`
- `RoundedRectComponent`
- built-in physics components

Those remain in their current explicit paths for now.

## Current State

### Runtime registration

`RuntimeComponentRegistry` still explicitly registers:

- `RuntimeDirectionalLightComponentDeserializer`
- `RuntimeAmbientLightComponentDeserializer`
- `RuntimePointLightComponentDeserializer`
- `RuntimeSpotLightComponentDeserializer`

### Packaging

`SceneComponentPackagingTransformService` still has light-specific rewrite branches:

- `RewriteDirectionalLightComponentRecord`
- `RewriteAmbientLightComponentRecord`
- `RewritePointLightComponentRecord`
- `RewriteSpotLightComponentRecord`

These convert authored scene payloads into strict hand-authored light payload formats.

### Generated native runtime support

Generated native runtime deserializers already exist for automatic components and are now capable of dictionary support from the previous migration work.

They should also be allowed to cover the built-in light components once the explicit overlap is removed.

## Target State

After this migration:

- light components serialize through the existing authored editor path already used for built-in and automatic components
- packaged light payloads use the shared automatic runtime binary format
- managed runtime loading uses `AutomaticScriptComponentRuntimeDeserializer`
- native player builds use generated `GeneratedRuntime...Deserializer` classes for the light components
- explicit light runtime deserializers are deleted
- explicit light packaging rewrite branches are deleted
- explicit light runtime registration is removed

## Design

### Persistence shape

No special persistence format will remain for the four light components.

Packaged runtime payloads will use the shared automatic runtime format:

1. automatic payload version
2. reflected member count
3. reflected member values in deterministic schema order

This aligns the light components with the shared reflected runtime pipeline rather than the bespoke light payload serializer path.

### Runtime loading

Managed runtime:

- resolve the component through the existing automatic runtime fallback in `RuntimeComponentRegistry`
- materialize the component via `AutomaticScriptComponentRuntimeDeserializer`

Native runtime:

- generated runtime component deserializer emission must now include the four light components
- generated registration should include those generated deserializers

### Packaging

`SceneComponentPackagingTransformService` should stop branching to hand-authored light rewrite methods.

Instead:

- the packaged component record should be produced by the existing automatic runtime record builder
- light components should flow through the same generic packaging path already used by other automatic-compatible components

### Reflected schema expectations

The four light components are reflection-friendly enough for this wave:

- public writable authored members
- no asset-backed members requiring resolver work
- no get-only collection members

Inherited members from `LightComponent` remain part of the authored reflected schema:

- `LightType`
- `Color`
- `Intensity`
- `ShadowsEnabled`
- `ShadowMapMode`
- `ShadowStrength`

Concrete members remain:

- `DirectionalLightComponent.ShadowDistance`
- `PointLightComponent.Range`
- `SpotLightComponent.Range`
- `SpotLightComponent.InnerConeAngleDegrees`
- `SpotLightComponent.OuterConeAngleDegrees`

## Files Expected To Change

### Runtime

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- delete:
  - `engine/helengine.core/scene/runtime/RuntimeDirectionalLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimePointLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeSpotLightComponentDeserializer.cs`

### Packaging and generation

- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
- `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`

### Tests

- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

Additional test files may be updated if there are existing explicit-light assumptions.

## Testing Strategy

### Packaging tests

Add or update tests to verify packaged light records:

- use automatic component type ids
- use the automatic runtime payload version
- no longer require bespoke light runtime deserializers

### Managed runtime load tests

Add or update tests to verify:

- packaged directional light payloads load through the automatic runtime path
- packaged ambient light payloads load through the automatic runtime path
- packaged point light payloads load through the automatic runtime path
- packaged spot light payloads load through the automatic runtime path

### Native generation tests

Update generated runtime deserializer tests to verify:

- light components are no longer excluded from generated runtime deserializer emission
- generated registration includes the four light component deserializers

## Risks

### Reflected member ordering changes payload shape

This is acceptable because backward compatibility is intentionally dropped for this migration.

### Built-in/runtime overlap

If explicit light deserializers remain registered anywhere, native/generated and runtime-managed paths can diverge or double-register.

The migration must remove the overlap completely.

### Hidden serializer assumptions

If any tests or tooling assume the old light payload format directly, they must be updated rather than preserved.

## Recommendation

Proceed with this first wave exactly as scoped:

- lights only
- remove all bespoke runtime and packaging light paths
- use the shared automatic runtime format everywhere

After this lands, reassess the next small-step wave, with `RoundedRectComponent` as the likely next candidate.

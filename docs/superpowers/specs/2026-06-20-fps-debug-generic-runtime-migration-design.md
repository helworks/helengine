# FPS And Debug Generic Runtime Migration Design

## Goal

Migrate the remaining strict overlay built-ins that are still pure authored data at persistence boundaries from bespoke packaged runtime deserializers and packaging rewrites to the shared automatic runtime component system.

This wave is intentionally strict and includes only:

- `FPSComponent`
- `DebugComponent`

## User Direction

- Keep the batch strict.
- Do not opportunistically pull in any other components.
- Preserve the runtime overlay behavior already implemented inside the component classes.
- Do not preserve backward compatibility for authored or cooked payloads.

## Why This Batch Is Safe

Both components are runtime-behavior-heavy but persistence-simple.

Their authored persisted state is only:

- `Font : FontAsset`
- `FontScale`
- `RefreshIntervalSeconds`
- `Padding`
- `RenderOrder2D`

`FPSComponent` also persists:

- `AdditionalText`

Their non-persisted complexity stays private runtime state:

- overlay host entities
- child row entities
- child `TextComponent` instances
- cached formatted strings
- counters and sampling windows
- static active-component registries
- debug additional-line registration state

That makes them a good fit for automatic reflected persistence because the generic system only needs to restore authored public state. Runtime lifecycle behavior remains inside the component classes exactly as it does today.

## Out Of Scope

This wave does not include:

- `CameraComponent`
- `MeshComponent`
- any physics components
- any other overlay or gameplay component

## Current State

### Runtime registration

`RuntimeComponentRegistry` still explicitly registers:

- `RuntimeFPSComponentDeserializer`
- `RuntimeDebugComponentDeserializer`

### Packaging

`SceneComponentPackagingTransformService` still contains explicit rewrite branches and helpers for:

- `RewriteFPSComponentRecord`
- `RewriteDebugComponentRecord`

### Generated native runtime support

Generated native runtime deserializers currently exclude both components because they are still considered explicit built-ins.

## Target State

After this migration:

- `FPSComponent` packages through the shared automatic runtime payload builder
- `DebugComponent` packages through the shared automatic runtime payload builder
- managed runtime loading resolves both through `AutomaticScriptComponentRuntimeDeserializer`
- native player builds use generated `GeneratedRuntimeFPSComponentDeserializer`
- native player builds use generated `GeneratedRuntimeDebugComponentDeserializer`
- explicit runtime deserializers are deleted
- explicit runtime registry entries are removed
- explicit packaging rewrite branches are removed

## Runtime Behavior Constraints

This migration must not refactor or flatten the runtime overlay systems.

Specifically, the following remain class-owned behavior and should not be treated as persisted authored data:

- overlay hierarchy construction and teardown
- sampling-window bookkeeping
- update/render frame tick recording
- debug additional-line registration behavior
- private child `TextComponent` and `Entity` references

The migration only changes how public authored state is serialized into packaged runtime payloads and how the runtime registry resolves those component type ids.

## Shared Asset-Reference Constraint

This migration relies on the existing automatic asset-reference path already supporting `FontAsset`.

That means both:

- `FPSComponent.Font`
- `DebugComponent.Font`

can use the generic automatic runtime format without any new asset-reference feature work.

## Data Format

Packaged runtime payloads for both components will use the shared automatic runtime format:

1. automatic payload version
2. reflected member count
3. reflected member values in deterministic reflected schema order

This replaces the current bespoke FPS and debug runtime payload layouts.

## Files Expected To Change

### Runtime

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- delete:
  - `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs`

### Packaging and generation

- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`

### Tests

- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

## Testing Strategy

### Generated runtime tests

Update the generated-runtime overlap expectations so they verify:

- `GeneratedRuntimeFPSComponentDeserializer` is emitted
- `GeneratedRuntimeDebugComponentDeserializer` is emitted

### Managed runtime load tests

Replace the direct packaged runtime tests for FPS and debug with automatic runtime payload coverage that asserts:

- authored font references still resolve correctly
- authored scalar values still materialize correctly
- null-font payloads continue to materialize safely

### Packaging tests

Update the packaging tests to verify:

- packaged FPS and debug records use automatic component type ids
- packaged payloads begin with `AutomaticScriptComponentRuntimeDeserializer.CurrentVersion`
- packaged font references are still rewritten to the correct cooked/runtime paths
- packaged scenes remain loadable through the default runtime registry

## Risks

### Reflected member-count drift

Because both components carry substantial private runtime state, the migration depends on the reflection schema continuing to include only public writable authored members. Tests should assert the packaged payload member count rather than assuming the old bespoke layout.

### Overlay activation side effects during runtime-load tests

Loading these components can materialize private overlay helper entities and child text components after authored state is restored. Tests must target the loaded `FPSComponent` or `DebugComponent` explicitly rather than assuming each root entity owns only one live component after load.

### Packager environment coupling

Some packager tests depend on editor shader backend initialization for generated standard material setup even when the component under test is 2D-only. Tests must keep the existing backend configuration requirements satisfied while asserting the new payload contract.

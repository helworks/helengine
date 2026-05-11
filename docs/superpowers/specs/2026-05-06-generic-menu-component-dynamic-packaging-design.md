# Generic Menu Components And Dynamic Packaging Fallback

## Summary

This change fixes two related architectural problems in the Windows build pipeline:

1. The menu runtime component family is incorrectly specialized as `DemoMenu*` engine components even though the behavior is generic menu behavior.
2. The Windows scene packager currently treats missing platform compatibility metadata as a hard failure for engine components, even when a component could be packaged through the existing reflected ordinal serializer/codegen path.

The new rule is:

- Explicit compatibility entries still override behavior.
- Existing custom transforms and pass-through handlers remain supported.
- When a component has no explicit compatibility entry, the packager attempts the generic reflected ordinal packaging path.
- If a component cannot be reflected into the supported ordinal schema, the build fails with a clear unsupported-shape error.

This task also renames the current menu component family from `DemoMenu*` to generic `Menu*` names and removes the old names entirely. No old-name support is preserved.

## Goals

- Rename the current demo-specific menu component family to generic engine menu component names.
- Make reflected ordinal packaging the default fallback when compatibility metadata is missing.
- Allow engine components and non-engine components to use the same generic fallback path.
- Preserve existing handwritten transform and pass-through support for components that genuinely require custom handling.
- Generate C++ player deserialization code that matches the ordinal payload field order written by the fallback packager.

## Non-Goals

- Converting every existing custom component to the generic fallback immediately.
- Preserving old `helengine.DemoMenu*` serialized ids or old class names.
- Redesigning the menu runtime behavior beyond the rename required to generalize it.
- Replacing explicit custom transforms that are already correct and useful.

## Current Problems

### Demo-Specific Engine Naming

The current menu runtime family lives in core as:

- `DemoMenuBuildComponent`
- `DemoMenuPanelComponent`
- `DemoMenuItemComponent`
- `DemoMenuSelectedDescriptionComponent`

These names encode one demo-specific use case even though the underlying behavior is a reusable menu runtime. The editor, runtime, packaging, generated-core patching, and tests all depend on those specialized names.

### Missing Compatibility Means Hard Failure

`EditorWindowsBuildScenePackager` currently throws when a component type id has no platform compatibility entry, except for one special case: automatic scripted components outside the core assembly. That means:

- generic reflected ordinal packaging already exists conceptually
- existing engine components cannot use it by default
- new engine components fail packaging unless someone remembers to add custom compatibility metadata

This violates the intended rule that custom packaging should be exceptional rather than mandatory.

## Proposed Design

### Component Rename

The current menu family is renamed as follows:

- `DemoMenuBuildComponent` -> `MenuComponent`
- `DemoMenuPanelComponent` -> `MenuPanelComponent`
- `DemoMenuItemComponent` -> `MenuItemComponent`
- `DemoMenuSelectedDescriptionComponent` -> `MenuSelectedDescriptionComponent`

The rename includes:

- component class names
- file names
- serialized component type ids
- runtime deserializers or generated-native deserializer references
- editor persistence descriptor names
- editor scene factory usage
- generated-core regeneration special cases
- all tests and fixtures that reference the old names

The old names are deleted. Existing content using old ids is not supported.

### Packaging Decision Order

The Windows scene packaging flow becomes:

1. Resolve explicit compatibility metadata for the serialized component type id.
2. If compatibility exists:
   - `PassThrough` leaves the record unchanged.
   - `Transform` uses the existing handwritten transform path.
3. If compatibility does not exist:
   - resolve the component type from the serialized type id
   - verify that it is a supported reflected component shape
   - deserialize the editor payload through the registered persistence descriptor
   - build a reflected member schema
   - rewrite the payload into strict ordinal runtime form using member order
   - mark the component as dynamically packaged for codegen/runtime loading
4. If type resolution or schema support fails, stop with a clear unsupported-component error.

This makes explicit compatibility optional for supported reflected components and keeps custom compatibility as an override mechanism.

### Generic Reflected Ordinal Packaging

The generic fallback uses the same essential pattern already used for automatic scripted components, but it applies to any supported component type rather than only non-core assemblies.

The fallback writer must:

- read the source scene record through the registered editor persistence descriptor
- reflect supported fields/properties in deterministic schema order
- write a strict ordinal payload in that exact order
- use the same field ordering contract that the generated C++ deserializer uses

The fallback payload format remains strict and performance-oriented:

- payload version
- reflected member count
- reflected member values in schema order

Supported member types follow the existing ordinal serializer/codegen support matrix. Unsupported member types remain build errors until explicit support is added.

### Native Player Deserializer Generation

The player-side generated C++ must receive enough schema information to emit a deserialization class for each dynamically packaged component.

For each fallback-packaged component, codegen must emit:

- the component type id
- the target generated C++ component type name
- the ordered reflected members
- the read logic for each supported member type

The generated C++ deserializer must read the ordinal payload in the same order the packager wrote it. This preserves runtime performance while keeping authored component support generic.

Custom handwritten deserializers remain valid for components that stay on explicit custom transform paths.

### Explicit Custom Support Still Wins

The new fallback does not remove custom behavior. Instead, it formalizes the precedence:

- explicit custom compatibility entries override fallback
- fallback only applies when no explicit compatibility entry exists

This allows the engine to keep specialized transforms for components that need asset rewriting, normalization, or non-reflectable runtime payloads, while making ordinary components work automatically.

## Runtime And Editor Impact

### Editor Persistence

Editor persistence remains descriptor-based and tolerant. No change is required to the general scene save/load model beyond the menu type rename and any descriptor registration updates.

### Packaged Runtime

Packaged runtime payloads remain strict ordinal binary payloads. The difference is only how the packager decides whether a component can reach that payload shape.

### Generated Core

Generated-core regeneration must account for the renamed menu types and for any emitted native deserializer classes associated with fallback-packaged components.

Any current generated-core patch logic that matches `DemoMenu*` file/class names must be updated to the new generic menu names or removed if the dynamic generation path makes those patches unnecessary.

## Error Handling

Packaging failures should distinguish between:

- missing compatibility with supported reflected fallback available
  - use fallback automatically
- missing compatibility and unresolved component type id
  - fail with type-resolution error
- missing compatibility and unsupported reflected member shape
  - fail with unsupported-schema error
- explicit transform declared but transform service cannot rewrite the record
  - fail with explicit transform error

The failure text should explain whether the problem is missing type resolution, unsupported member shapes, or a broken explicit transform path.

## Implementation Areas

Expected touch points include:

- `EditorWindowsBuildScenePackager`
- `SceneComponentPackagingTransformService`
- reflected schema/ordinal serializer helpers
- native player deserializer generation
- generated-core regeneration special cases
- core menu component classes and related runtime support classes
- editor scene persistence descriptors
- editor session descriptor registration
- menu scene factory/build tooling
- tests for packager, runtime scene load, generated core, and scene persistence

## Testing Strategy

Add or update tests for:

- packaging succeeds for renamed menu components without explicit compatibility metadata by using generic reflected fallback
- explicit custom transforms still override fallback for components that declare custom compatibility
- unsupported reflected member types fail with clear errors
- generated deserializer output matches reflected field order
- editor scene persistence/load uses renamed menu components
- runtime scene loading and packaged runtime loading succeed with renamed menu components
- generated-core regeneration no longer depends on `DemoMenu*` naming

## Risks

- Renaming the menu component family touches a wide set of editor, runtime, and codegen references.
- The current generic ordinal fallback is presently scoped around non-core scripted components, so broadening it may expose assumptions tied to assembly boundaries.
- Generated C++ deserializer output must stay exactly aligned with the ordinal payload writer, or runtime loading will fail in hard-to-debug ways.

## Recommendation

Implement the hybrid default model:

- keep current custom compatibility entries working unchanged
- make reflected ordinal packaging the default when compatibility metadata is absent
- rename the menu runtime family to generic menu names now

This fixes the immediate build failure, aligns the packaging architecture with the intended default behavior, and avoids a full migration of every existing custom component in the same change.

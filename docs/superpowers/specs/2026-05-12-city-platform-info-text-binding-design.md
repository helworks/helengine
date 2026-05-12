# City Platform Info Text Binding Design

## Goal

Add a City-owned menu component that shows the runtime platform id and builder-stamped version in the bottom-right of the main menu.

The scene generator will author two text objects, and the new component will hold serialized references to those two text components so the labels stay bound across save/load and package generation.

## Decision Summary

- Add a City gameplay component named `PlatformInfoTextComponent`.
- The component will bind exactly two `TextComponent` targets:
  - the top text shows `Core.Instance.PlatformInfo.Name`
  - the bottom text shows `Core.Instance.PlatformInfo.Version`
- The binding will use hard references, not hierarchy scans or name lookups.
- The scene serializer will persist component references automatically by writing stable ids for:
  - the owning entity
  - the target component key
- `SceneEntityReference` remains entity-only.
- Add a new `SceneComponentReference` value type for component targets.
- `componentIndex` will not be used as the primary identifier because it is ordering data, not identity data.

## Why This Approach

The runtime platform id and version are immutable for a packaged build, so this should not become a polling system. The menu only needs to bind the labels once and then stay out of the way.

Using direct serialized references is the safest authoring contract for a generated scene. It avoids fragile hierarchy scans, avoids depending on entity names, and makes the scene generator responsible for wiring the exact text objects it created.

`componentIndex` is not a stable identity. It can change when components are inserted, reordered, or regenerated, so it should stay as ordering metadata only. A `SceneComponentReference` keyed by `entityId + componentKey` gives the serializer a real identity contract that survives save/load and is already aligned with the editor scene pipeline.

## Public API

Add a new serialized runtime/editor reference type:

```csharp
public class SceneComponentReference {
    public string EntityId { get; set; }
    public string ComponentKey { get; set; }
}
```

Add a new City gameplay component:

```csharp
public sealed class PlatformInfoTextComponent : UpdateComponent {
    public SceneComponentReference NameTextReference { get; set; }
    public SceneComponentReference VersionTextReference { get; set; }
}
```

The component will resolve the references to two `TextComponent` instances and update their `Text` values from `Core.Instance.PlatformInfo`.

## Serialization Contract

### Entity Targets

When a field points at an entity, the serializer writes only:

- `EntityId`

This matches the existing `SceneEntityReference` behavior.

### Component Targets

When a field points at a component, the serializer writes:

- `EntityId`
- `ComponentKey`

That pair uniquely identifies the target component inside one serialized scene.

### Runtime Resolution

During runtime scene load, the loader will build a lookup that can resolve:

- entity id -> live entity
- entity id + component key -> live component

That resolver must be available to runtime components that own serialized component references.

If a component reference cannot be resolved, scene loading should fail fast. The menu should not fall back to a partially wired state.

## Architecture

### Scene Load Path

The scene generator will keep authoring the menu scene as a normal scene asset. It will create the two bottom-right text entities and attach `PlatformInfoTextComponent` with serialized references to those two `TextComponent`s.

The editor scene serializer will persist those component references automatically.

The runtime scene loader will restore the reference ids and rebuild them into live component handles after the full scene subtree has been materialized.

### City Menu Component

`PlatformInfoTextComponent` will:

- resolve its two referenced text components
- set the first text to the platform id
- set the second text to the version string
- leave the values alone after initialization unless the scene is reloaded

The component should not scan the scene every frame. The labels are static build metadata, so the work is one-time wiring, not continuous synchronization.

### Scene Generation

The main-menu generator will author the two text objects in the bottom-right corner as part of the generated menu scene.

The component should be attached to a stable owning entity in the generated hierarchy, and the generator should populate the two serialized component references directly from the generated text objects.

## Error Handling

- If `Core.Instance.PlatformInfo` is missing, the component should throw.
- If either component reference is missing, the component should throw.
- If a reference resolves to the wrong component type, the component should throw.
- If the runtime loader cannot resolve `EntityId + ComponentKey`, loading should fail immediately.

This keeps the failure visible when the scene generator or serializer contract is broken.

## Testing

Add coverage for:

1. `SceneComponentReference` binary serialization and deserialization
2. automatic script-component persistence of component references
3. runtime scene loading resolving a component reference back to the exact live `TextComponent`
4. `PlatformInfoTextComponent` setting the two text values from `Core.Instance.PlatformInfo`
5. generated City main-menu scene containing the two bound bottom-right text objects

The tests should prove the runtime labels are wired to the correct objects, not just that some text exists somewhere in the scene.

## Files Expected To Change

- `engine/helengine.core/` reference and serialization types
- `engine/helengine.editor/serialization/scene/` persistence and runtime-deserializer support
- `engine/helengine.editor/managers/project/ScriptComponentPlayerDeserializerGenerator.cs`
- `engine/helengine.editor.tests/` serialization and runtime resolution tests
- `C:\\dev\\helprojs\\city\\assets\\codebase\\menu\\PlatformInfoTextComponent.cs`
- `C:\\dev\\helprojs\\city\\assets\\codebase\\menu.tools\\RegenerateDemoDiscMainMenuCommand.cs`
- `C:\\dev\\helprojs\\city\\assets\\scenes\\DemoDiscMainMenu.helen`

## Acceptance Criteria

- The City main menu shows the platform id in one label and the version in the label below it.
- The labels are bound through serialized references, not scene-name lookup logic.
- The serializer persists entity references and component references automatically.
- `SceneComponentReference` uses `EntityId + ComponentKey`, not component index, as the stable identity contract.
- Runtime loading fails loudly if the reference contract is broken.

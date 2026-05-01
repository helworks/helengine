# Shared Component Compatibility Model Design

## Summary

HelEngine should not require a separate per-platform serializer or loader implementation for every component. The default rule is that each component serializes once in `helengine.core`, and Windows and PS2 both consume the same component record shape unchanged.

Platform-specific behavior is allowed only through an explicit compatibility surface exposed by the active platform builder assembly. That surface can declare a component as pass-through, transform it into a platform-safe packaged form, or reject it with a clear build-time reason.

This keeps component behavior centralized in `helengine.core`, while still leaving room for real platform differences such as resource remapping, unsupported features, or binary-format constraints like endianness.

## Problem

The current packaging/runtime path is too easy to fragment into platform-specific component handling. If every new component needs separate Windows and PS2 updates, the system will quickly become unmaintainable.

That creates three concrete problems:

1. Component logic gets duplicated across platforms instead of living in one canonical place.
2. The editor build path becomes a matrix of platform-specific serializers and loaders.
3. Simple engine features turn into repeated maintenance work whenever a new platform is added.

The recent `FPSComponent` work showed that the current model needs a stronger default:

- canonical component serialization in core
- builder-defined compatibility metadata
- only exceptional transforms for true platform differences

## Goals

- Keep the canonical component definition in `helengine.core`.
- Avoid per-platform serializer duplication for normal components.
- Let platform builders declare whether a component is pass-through, transform, or unsupported.
- Keep the editor generic so it can package any component record without hardcoding a long component whitelist.
- Treat binary-format concerns such as endianness as part of the platform contract, not as special-case component code.
- Make Windows and PS2 share the same core component payloads whenever possible.

## Non-Goals

- No platform-specific component rewrite for every engine type.
- No move to a separate editor serializer per platform.
- No attempt to solve every future platform customization in this slice.
- No change to the existing dynamic builder loading model.

## Proposed Architecture

### Canonical Core Serialization

Each engine component should own one stable serialized payload in `helengine.core`. That payload is the authoritative representation of the component across platforms.

Examples:

- `MeshComponent` serializes once.
- `CameraComponent` serializes once.
- `FPSComponent` serializes once.

If a component is valid on both Windows and PS2 without special handling, then no platform-specific component code should exist for it at all.

### Platform Compatibility Surface

Each platform builder assembly should expose a compatibility table keyed by component id.

For each component id, the builder can declare one of three outcomes:

- `PassThrough`: the packaged record is valid as-is.
- `Transform`: the builder provides a small record-level rewrite before packaging.
- `Unsupported`: the editor must fail the build with a clear reason.

The compatibility table is the only platform-specific component surface the editor should consult.

### Record-Level Transforms

Transforms should operate on serialized component records, not on editor object graphs.

That keeps the contract small and predictable:

- the editor packages scene/component records generically
- the platform builder decides whether a specific serialized record needs rewriting
- the runtime only sees the packaged result

Transforms should be used only when the platform truly needs a different packaged shape, such as:

- resource path remapping
- platform-safe defaults
- a feature downgrade that still preserves runtime behavior

If a component is already valid on the platform, it should be pass-through.

### Binary Contract

Platform builders should also describe the binary contract they expect:

- endianness
- alignment expectations
- primitive layout assumptions
- payload version rules

The editor should normalize packaged data to the target platform's binary contract as part of build preparation. This is a platform-level concern, not a per-component concern.

## Packaging Flow

The editor packaging path should remain generic:

1. Gather the selected scenes and resolved runtime assets.
2. Serialize the scene records from the editor project.
3. For each component record, query the active builder compatibility table.
4. Apply a transform only when the builder says the component requires one.
5. Reject the build if the builder marks the component unsupported.
6. Write the packaged result into the platform build root.

That means the editor does not need to own a hardcoded list of component types. It only needs to understand how to iterate serialized records and ask the builder what to do.

### Runtime Loading

The runtime player should load the packaged result only.

If a component required a transform, that transform has already been applied during packaging.
If a component was pass-through, the same canonical record shape should work on both Windows and PS2.

The runtime should not need to know which components were considered exceptional during packaging.

## Failure Handling

The editor should fail early and clearly when a builder marks a component as unsupported.

The failure report should include:

- platform id
- component id
- reason from the builder
- any remediation text the builder provides

If a component is `Transform`, the build should continue and the transform should be recorded in the build log.

If a component is `PassThrough`, the editor should emit the record unchanged and avoid any platform-specific fallback behavior.

## Example: FPSComponent

`FPSComponent` should remain a canonical core component.

Expected behavior:

- Windows packages it as a normal canonical component record.
- PS2 packages the same canonical component record unless the builder declares a real transform is needed.
- If both platforms can support it directly, there should be no platform-specific serializer code for it beyond the shared core serializer and shared runtime loader.

This is the model to use for most future components as well.

## Testing

Add tests at three levels:

1. Core serialization tests
- verify a component serializes once in `helengine.core`
- verify the same payload round-trips back into the same component type
- verify payload versioning stays stable

2. Builder compatibility tests
- verify a builder reports `PassThrough` for canonical shared components
- verify a builder can reject an unsupported component with a useful message
- verify a transform rewrites only the component record payload

3. Packaging/runtime tests
- package a scene through the editor for Windows and PS2
- confirm the packaged scene contains the canonical component record shape after any platform transform
- confirm the player runtime loads the packaged scene on both targets

## Files in Scope

- `engine/helengine.core/components/*`
- `engine/helengine.core/scene/runtime/RuntimeSceneLoadService.cs`
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- `helengine-windows/builder/*`
- `helengine-ps2/builder/*`
- `engine/helengine.baseplatform/*`

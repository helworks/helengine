# Editor Scene Tagged Persistence Design

## Goal

Make editor scene component payloads tolerant to schema evolution while keeping cooked/runtime scene payloads strict and compact. Editor scenes must ignore unknown fields and use component defaults for missing fields. Cooked/player scene payloads may stay versioned and strict.

## Context

Current editor scene component payloads are positional binary blobs with per-component version checks. Adding one property forces every existing authored scene to be rewritten or migrated. This is fragile for editor-authored assets and has already broken light payloads.

The repository already has a useful architectural seam:

- editor scenes serialize through `IComponentPersistenceDescriptor`
- player builds deserialize through `IRuntimeComponentDeserializer`
- scene packaging already rewrites some editor payloads into runtime payloads through `SceneComponentPackagingTransformService`

That seam should become the real boundary:

- editor persistence is resilient and self-describing
- cook/package is the canonical translation step
- runtime only understands the strict cooked payloads it ships with

## Decision

### Editor scene payload format

All editor scene component descriptors will switch to one shared tagged field-container format.

Container shape:

1. shared editor payload format version byte
2. field count
3. repeated field records:
   - field name
   - field payload byte length
   - field payload bytes

Each component descriptor owns its field names and the field payload encoding for each property.

Behavior:

- unknown fields are skipped during editor load
- missing fields leave the target component at its constructor/default state
- save always writes the latest known field set
- old positional editor payloads are not supported going forward

This keeps editor persistence tolerant without adding reflection-heavy generic mutation logic into every load path.

### Cooked/runtime payload format

Cooked payloads remain strict, explicit, and versioned. Runtime deserializers keep failing on unsupported versions because player builds need deterministic data.

Packaging becomes the only supported bridge from editor payloads to runtime payloads for built-in runtime components:

- mesh
- camera
- FPS
- text
- rounded rect
- directional light
- point light
- spot light
- baked demo menu build
- baked demo menu panel
- baked demo menu item
- baked demo menu selected-description

### Shared strict runtime payload codecs

The repo currently duplicates strict binary payload logic across runtime deserializers and packaging transforms. That duplication should be collapsed into shared strict payload codec classes.

Those codecs will:

- serialize strict cooked payload bytes from authored values
- deserialize strict cooked payload bytes for runtime
- centralize binary field order and version checks

Editor descriptors do not use these codecs directly for editor persistence. They use the tolerant tagged container instead.

## File-level design

### New editor helpers

Add editor-only shared helpers under `engine/helengine.editor/serialization/scene`:

- tagged payload writer
- tagged payload reader
- field payload helper utilities for common primitives and scene asset references

Responsibilities:

- write stable named fields
- materialize a field lookup from one payload
- expose `TryRead...` helpers that leave defaults unchanged when fields are absent

### Editor descriptors

Every `*ComponentPersistenceDescriptor` moves to the tagged editor format.

Rules:

- instantiate the destination component first during load
- read only known fields
- never require a component-specific payload version byte
- keep save-state asset references restored for mesh/text/FPS

### Packaging transform

`SceneComponentPackagingTransformService` becomes the single translator from tagged editor payloads into strict runtime payloads.

It will:

- parse editor tagged fields without constructing runtime assets
- rewrite editor asset references into packaged file-backed references when required
- emit strict runtime payload bytes through shared strict runtime payload codecs

### Runtime deserializers

Runtime deserializers stay strict and only understand cooked payloads. Their binary logic moves behind shared strict runtime payload codecs so the cook step and runtime path cannot drift.

## Error handling

Editor load should fail only for real corruption or impossible required-state cases, not for missing future fields.

Expected editor behavior:

- unknown field name: ignore
- known field with malformed payload bytes: throw
- missing optional field: keep default
- missing required asset reference for a component that truly cannot operate without it: throw

Expected runtime behavior:

- wrong cooked payload version: throw
- malformed cooked payload: throw
- unsupported component type: throw

## Testing

Add coverage at three levels.

### Descriptor tests

Verify tolerant editor behavior:

- round-trip current fields
- unknown fields are ignored
- missing fields keep defaults

### Scene save/load integration

Verify editor scene save/load still materializes entities correctly for representative components and hidden editor visuals.

### Packaging/runtime tests

Verify tagged editor payloads package into strict runtime payloads that the runtime loader can deserialize for all supported built-in runtime components.

## Non-goals

- support for existing positional editor scene payloads
- changing the outer `.helen` asset container format
- making runtime deserializers tolerant to arbitrary editor payload evolution
- full generated serialization infrastructure in this slice

## Follow-up

After this lands, cooked/runtime serializers should move toward generated classes as the default authoring pattern. That work is adjacent but separate from making editor scenes tolerant.

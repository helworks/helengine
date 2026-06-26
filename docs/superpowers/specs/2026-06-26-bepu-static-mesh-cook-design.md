# BEPU Static Mesh Cook Design

**Date:** 2026-06-26

**Goal**

Add static world-mesh collision support to `helengine.bepu` by generating BEPU mesh data at cook time and loading that prebuilt payload at runtime, without adding moving or kinematic mesh-collider support.

**Scope**

- Static `StaticMeshCollider3DComponent` support in `helengine.bepu`.
- Cook-time generation of a BEPU-owned mesh payload from generic Helengine static-mesh collision data.
- Runtime BEPU registration of static mesh colliders from the cooked payload.
- Editor and runtime tests for the new path.

**Out of Scope**

- Dynamic or kinematic mesh colliders.
- Character-controller or capsule work.
- Replacing the generic `StaticMeshCollisionData3D` authoring surface.
- Building BEPU mesh trees in `BindScene`.
- Supporting multiple physics backends from one cooked payload in this pass.

**Current State**

The shared physics authoring surface already exposes `StaticMeshCollider3DComponent` with generic `StaticMeshCollisionData3D` triangle data. The legacy `helengine.physics3d` runtime still understands that data directly.

`helengine.bepu` does not. `BepuPhysicsFeatureGuard3D` rejects any collider that is not a box or sphere, `BepuPhysicsWorld3D.BindScene` only registers box and sphere shapes, and `BepuBodyHandle3D` has no static-mesh branch.

The important BEPU detail is that `BepuPhysics.Collidables.Mesh` already provides the right cook/runtime boundary:

- `GetSerializedByteCount()`
- `Serialize(Span<byte>)`
- `new Mesh(Span<byte>, BufferPool)`

That means the expensive tree build can happen once during cook, and runtime only needs to deserialize and register the shape.

**Decision**

Keep the shared authored source generic and add one generic cooked-runtime payload slot for static-mesh colliders. The editor cook pipeline will expose a generic static-mesh collision cook hook, and the BEPU plugin will provide the only implementation in this pass.

At cook time, the BEPU processor will:

1. read `StaticMeshCollisionData3D`,
2. convert it into BEPU `Triangle` data,
3. build a BEPU `Mesh`,
4. serialize that mesh to bytes,
5. attach the bytes to the cooked static-mesh collider payload with a BEPU format identifier.

At runtime, `helengine.bepu` will:

1. require the BEPU payload on static mesh colliders,
2. deserialize it into `BepuPhysics.Collidables.Mesh`,
3. register the result as a BEPU static collidable,
4. reject static mesh colliders that do not carry a BEPU payload.

This keeps heavy mesh preparation out of `BindScene`, preserves the existing generic authoring model, and keeps BEPU-specific details inside the BEPU integration and editor cook seam rather than leaking them across unrelated gameplay code.

## Architecture

### 1. Shared authored data remains generic

`StaticMeshCollider3DComponent.CollisionData` remains the source-of-truth authored mesh-collision representation. It continues to store generic vertex and index data and remains the only authoring-facing mesh-collision field.

The shared physics assembly gains one additional runtime-facing payload property for cooked scenes. This property is generic in structure, not BEPU-specific in type shape. It should store:

- a stable runtime format identifier string
- an opaque byte array payload

For this pass, only one payload is needed per static-mesh collider. The value is populated during scene packaging and ignored by authoring workflows.

### 2. Cooked payload format is BEPU-specific

The payload format id should be explicit and stable, for example `helengine.bepu.static-mesh`.

The payload bytes are the raw result of BEPU mesh serialization. The shared physics assembly does not interpret those bytes. It only persists them as opaque cooked runtime data.

This keeps the contract generic at the engine boundary while allowing the BEPU runtime to consume the most direct, lowest-overhead format available.

### 3. Scene packaging owns the transformation

The cook seam should live in the editor scene-packaging flow, not in runtime scene binding and not in the platform builder's file-output cook capability path.

This work is logically different from builder-owned texture cooking:

- it produces runtime physics data embedded in the cooked scene payload,
- it consumes component-authored collision data already present in scene packaging,
- it does not naturally map to a standalone cooked file artifact.

The editor should expose one generic registration surface for static-mesh collision cook processors. The processor contract should accept the authored collider data plus any required contextual transform data and return the cooked runtime payload.

`helengine.bepu` then registers one BEPU static-mesh cook processor with that generic editor-side hook.

### 4. Packaging bakes final collider-space vertices

The cook processor should operate on the final collider-space triangle data that the runtime will use directly. Do not rely on runtime scaling or mesh-tree rebuilding.

If the current scene-packaging path already bakes static-mesh collider geometry into `StaticMeshCollisionData3D`, reuse that result directly. If any authored transform scaling still exists outside the collision blob, packaging must bake that scale into the triangle vertices before the BEPU payload is built.

The BEPU mesh should therefore be serialized with unit scale. Runtime registration should only supply BEPU pose data for translation and orientation.

### 5. Runtime BEPU loading is strict

`BepuPhysicsFeatureGuard3D` should stop rejecting static mesh colliders unconditionally. Instead, it should reject only these cases:

- the entity uses a static mesh collider with a non-static rigid body
- the static mesh collider has no cooked runtime payload
- the cooked runtime payload format id is not the BEPU static-mesh format id

`BepuPhysicsWorld3D` gains a static-mesh registration path that:

- resolves `StaticMeshCollider3DComponent`
- verifies the body kind is `BodyKind3D.Static`
- deserializes the payload into `Mesh`
- adds that mesh to `SimulationValue.Shapes`
- registers the resulting `StaticDescription`
- stores the mesh-backed handle in `BepuBodyHandle3D`
- applies the same `BepuCollidableProperties3D` metadata path already used for box and sphere statics

No dynamic or kinematic body path should be added for meshes in this pass.

### 6. Runtime ownership and disposal

BEPU mesh shapes allocate pooled resources when deserialized. The runtime must own those resources explicitly.

The BEPU body-handle path therefore needs to track mesh-backed registrations so the world can return their pooled memory when the simulation resets or the world is disposed. This is different from boxes and spheres, which do not carry equivalent per-shape pooled triangle/tree buffers.

The design should keep disposal aligned with the BEPU world lifetime:

- deserialize mesh during registration
- keep the mesh reachable through the runtime handle or a dedicated mesh resource registry
- dispose the mesh back into the BEPU `BufferPool` when tearing down the world

Avoid best-effort cleanup that swallows ownership mistakes. Missing or duplicate disposal should fail during testing rather than being masked.

## Production Changes

### Shared physics contract

Modify the shared static-mesh collider contract in `engine/helengine.physics` to carry one opaque cooked runtime payload for packaged scenes.

Likely files:

- `engine/helengine.physics/StaticMeshCollider3DComponent.cs`
- one new payload type file in `engine/helengine.physics/`

The new payload type should be generic and reusable in structure:

- `string FormatId`
- `byte[] Data`

### Editor cook seam

Add one generic static-mesh collision cook registration seam in `engine/helengine.editor`.

Likely areas:

- scene packaging transform service
- editor-side processor registry or registration bootstrap
- tests covering scene packaging and reflected component persistence

This seam should let a runtime plugin contribute cooked collider payload generation without hardcoding BEPU logic into the shared editor packaging path.

### BEPU cook processor

Add one BEPU-owned processor that converts `StaticMeshCollisionData3D` into serialized BEPU mesh bytes.

Likely package:

- `engine/helengine.bepu`

Responsibilities:

- validate triangle/index data
- expand index triples into BEPU `Triangle` buffers
- build a BEPU `Mesh`
- serialize the mesh
- return the opaque payload with the BEPU format id

### BEPU runtime support

Extend `helengine.bepu` runtime registration to support static mesh statics.

Likely files:

- `engine/helengine.bepu/BepuPhysicsFeatureGuard3D.cs`
- `engine/helengine.bepu/BepuPhysicsWorld3D.cs`
- `engine/helengine.bepu/BepuBodyHandle3D.cs`
- one new helper file for mesh payload decode or shape creation

The runtime should not consult `StaticMeshCollisionData3D` for BEPU shape construction once the cooked payload is present. The generic triangle data remains for authoring persistence and legacy runtime compatibility, not for BEPU runtime mesh building.

## Error Handling

Cook-time failures should be explicit:

- missing collision vertices
- invalid index triples
- empty triangle results
- unsupported payload preconditions

Runtime failures should also be explicit:

- static mesh collider bound to a dynamic or kinematic rigid body
- missing cooked payload
- mismatched payload format id
- corrupt serialized BEPU mesh bytes

Do not silently fall back to runtime tree construction from generic collision data. If the cook pipeline failed to produce a valid BEPU payload, runtime binding should fail loudly.

## Testing Strategy

Add focused tests in three layers.

### 1. Shared contract tests

Verify the new cooked-payload type persists through the generic reflected component serialization path without disturbing the existing `CollisionData` property.

### 2. Editor packaging tests

Verify that packaging a scene with a static mesh collider and BEPU cook processor enabled produces a cooked collider payload with:

- the BEPU format id
- non-empty serialized bytes
- unchanged generic collision data

Verify that scenes without static mesh colliders are unchanged.

### 3. BEPU runtime tests

Add tests proving:

- static mesh colliders are accepted only for static rigid bodies
- a static box or sphere can interact with a cooked static mesh scene surface
- missing payloads fail during `BindScene`
- invalid format ids fail during `BindScene`

No tests for dynamic or kinematic mesh colliders should be added because that behavior remains unsupported by design.

## Risks

- The biggest integration risk is not collision math. It is choosing the wrong cook seam and ending up with BEPU-specific packaging logic scattered through unrelated editor code.
- If runtime mesh ownership is not tracked carefully, BEPU pooled mesh resources could leak across repeated world resets.
- If packaging does not bake final collider-space vertices consistently, runtime collision shapes may not align with rendered geometry.

## Success Criteria

- Static mesh world collision works in `helengine.bepu` for static rigid bodies.
- `BindScene` does not build BEPU mesh trees from generic triangle data.
- BEPU static mesh data is generated at cook time and loaded from serialized bytes at runtime.
- Dynamic and kinematic mesh colliders still fail explicitly.
- Shared editor and runtime code remain generic at the contract boundary, with BEPU-specific behavior isolated to the BEPU plugin and its cook/runtime paths.

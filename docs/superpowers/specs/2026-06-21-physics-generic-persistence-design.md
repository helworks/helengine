# Physics Generic Persistence Design

**Date:** 2026-06-21

**Goal**

Remove the remaining hand-authored physics scene serialization and deserialization paths so physics components persist, package, and load only through the shared reflected component pipeline.

**Scope**

- Runtime scene loading for physics components.
- Editor packaging of physics component scene records.
- Tests that still author or expect legacy strict physics payloads.

**Out of Scope**

- BEPU simulation behavior.
- Physics component gameplay semantics.
- Backward compatibility for previously-authored strict physics payloads.

**Current State**

Editor scene persistence already routes ordinary components through `AutomaticScriptComponentPersistenceDescriptor`. Physics still diverges in two places:

- Player/runtime registration still adds explicit physics `IRuntimeComponentDeserializer` implementations.
- Packaging still detects legacy strict physics payloads and rewrites them through manual deserializers before emitting automatic runtime payloads.

This leaves dual-format logic in production even though the desired steady state is the generic reflected contract.

**Decision**

Adopt a hard cutover with no backward compatibility.

- New and existing supported scenes must use the generic reflected component payload format.
- Manual physics runtime deserializer classes will be removed.
- The editor packaging compatibility shim for legacy physics payloads will be removed.
- Runtime scene loading will resolve physics components through `AutomaticScriptComponentRuntimeDeserializer`.

**Architecture**

The runtime registry will keep its existing behavior:

- explicit registrations when truly required
- generated registrations for cooked-scene/native codegen support
- automatic reflected fallback when the serialized component type resolves to a `Component`

After the cutover, physics uses only the third path. This aligns it with mesh, camera, lights, and other reflected components.

**Production Changes**

1. Remove explicit physics runtime deserializer registration from `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`.
2. Delete the following manual runtime deserializer classes from `engine/helengine.physics`:
   - `RuntimeRigidBody3DComponentDeserializer.cs`
   - `RuntimeBoxCollider3DComponentDeserializer.cs`
   - `RuntimeSphereCollider3DComponentDeserializer.cs`
   - `RuntimeCapsuleCollider3DComponentDeserializer.cs`
   - `RuntimeStaticMeshCollider3DComponentDeserializer.cs`
   - `RuntimeKinematicMotion3DComponentDeserializer.cs`
   - `RuntimeCharacterController3DComponentDeserializer.cs`
3. Remove the legacy strict-physics packaging branch from `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`.
4. Keep `RuntimeComponentRegistry` automatic fallback unchanged except for tests that assume physics remains explicitly registered.

**Behavioral Result**

- Physics scene records authored through the reflected descriptor continue to save, package, and load.
- Legacy strict physics payloads stop working immediately.
- Packaged scenes no longer normalize old physics payloads during packaging.

**Testing Strategy**

- Runtime scene load tests must verify generic physics payloads materialize the expected component values.
- Packager tests must author physics records through the generic reflected path and verify packaged scenes still load correctly.
- Registration tests must stop expecting explicit built-in physics runtime deserializers.
- Legacy strict-payload tests must be removed or rewritten to target the generic path.

**Risks**

- Tests may still construct strict physics payloads by hand and fail after the cutover.
- Some generated-core or registration assertions may still name the deleted runtime deserializer classes.
- If any physics component property is not actually compatible with reflected runtime serialization, runtime load tests will expose it immediately.

**Success Criteria**

- No production physics `IRuntimeComponentDeserializer` implementations remain.
- No production packaging code special-cases legacy physics scene payload layouts.
- Focused runtime and packaging tests pass using only generic reflected physics payloads.

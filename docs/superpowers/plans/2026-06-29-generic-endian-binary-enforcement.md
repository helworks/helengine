# Generic Endian Binary Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove plugin-authored raw cooked binary blob insertion from the static-mesh collision path, make Helengine own payload byte generation through endian-aware writers, and reject automatic scene component `byte[]` payload members so the same bug class cannot be reintroduced through reflected component persistence.

**Architecture:** Introduce an engine-owned serialized payload container that writes and reads HELE-header-backed payload bytes through `EngineBinaryWriter` and `EngineBinaryReader`. Change static-mesh cook processors from "return raw bytes" to "write fields into an engine writer", add a BEPU serializer that reconstructs meshes from engine-managed field reads, and block raw `byte[]` members in automatic component persistence so plugin components must use engine-managed binary payload types instead of prepacked host-native blobs.

**Tech Stack:** C#/.NET 9, xUnit, helengine editor scene packaging, helengine.core serialization, helengine.bepu

---

### Task 1: Lock The New Engine Boundary In Tests

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/StaticMeshColliderGenericPersistenceTests.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentBinaryPayloadGuardTests.cs`

- [ ] **Step 1: Write the failing packaging/runtime-shape tests**

Add assertions that packaged static-mesh colliders still round-trip through the reflected component path but no longer expose raw `byte[]` runtime payload data directly. Add a new guard test proving automatic component persistence rejects plain `byte[]` members.

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~StaticMeshColliderGenericPersistenceTests|FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~AutomaticScriptComponentBinaryPayloadGuardTests" -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests" -v minimal
```

Expected: tests fail because the current contract still uses `StaticMeshCollisionRuntimeData3D.Data` and cook processors still return raw payload bytes.

### Task 2: Add The Engine-Owned Serialized Payload Container

**Files:**
- Create: `engine/helengine.core/serialization/EngineSerializedPayload.cs`
- Modify: `engine/helengine.physics/StaticMeshCollisionRuntimeData3D.cs`

- [ ] **Step 1: Add the new engine payload type**

Create one reusable Helengine-owned payload container that:

- stores `FormatId`
- stores the cooked payload bytes privately
- writes a HELE header plus payload body through `EngineBinaryWriter`
- reopens the payload through `EngineBinaryReader`
- never accepts plugin-supplied final cooked bytes directly

- [ ] **Step 2: Refactor the static-mesh runtime data object to use the engine payload container**

Replace the public raw `Data` contract with one engine-managed payload property and helper methods used by packaging/runtime code.

- [ ] **Step 3: Run the focused tests**

Run the same tests from Task 1. Expected: still failing because the cook/runtime code has not been migrated yet.

### Task 3: Migrate Static-Mesh Cooking To Writer-Based Serialization

**Files:**
- Modify: `engine/helengine.physics/IStaticMeshCollisionCookProcessor3D.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor/managers/project/StaticMeshCollisionCookProcessorRegistry.cs`
- Modify: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`

- [ ] **Step 1: Change the cook processor contract**

Replace `Cook(...) -> StaticMeshCollisionRuntimeData3D` with an engine-owned write contract that lets plugins describe payload contents through `EngineBinaryWriter` while Helengine chooses the endianness and final byte buffer creation.

- [ ] **Step 2: Make the packaging transform resolve payload endianness from platform metadata**

Update `SceneComponentPackagingTransformService` so it resolves the selected platform serialization endianness and creates the runtime payload bytes through the new Helengine payload container before assigning them to the component.

- [ ] **Step 3: Update the packaging tests and stub processor**

Make the stub processor write deterministic values through the writer-based API and update assertions to inspect the decoded payload instead of a raw `byte[]` property.

- [ ] **Step 4: Run the packaging tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneComponentPackagingTransformServiceTests" -v minimal
```

Expected: packaging tests pass while BEPU runtime tests still fail until the runtime reader migration lands.

### Task 4: Replace BEPU Raw Mesh Serialization With Engine-Managed Field Serialization

**Files:**
- Create: `engine/helengine.bepu/BepuStaticMeshCollisionBinarySerializer.cs`
- Modify: `engine/helengine.bepu/BepuStaticMeshCollisionCookProcessor3D.cs`
- Modify: `engine/helengine.bepu/BepuShapeFactory3D.cs`
- Modify: `engine/helengine.bepu.tests/BepuStaticMeshCollisionCookProcessorTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsWorld3DTests.cs`
- Modify: `engine/helengine.bepu.tests/BepuPhysicsFeatureGuard3DTests.cs`

- [ ] **Step 1: Add the shared BEPU field serializer**

Serialize collision triangles and required mesh metadata through `EngineBinaryWriter`, and reconstruct a `Mesh` from `EngineBinaryReader` without using upstream BEPU raw serialized byte blobs as the persisted contract.

- [ ] **Step 2: Update the BEPU cook processor**

Make the cook processor implement the new writer-based contract and delegate actual field serialization to the shared serializer.

- [ ] **Step 3: Update runtime mesh creation**

Make `BepuShapeFactory3D` reopen the engine-managed payload reader and rebuild the mesh through the shared serializer.

- [ ] **Step 4: Run the focused BEPU tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests|FullyQualifiedName~BepuPhysicsWorld3DTests|FullyQualifiedName~BepuPhysicsFeatureGuard3DTests" -v minimal
```

Expected: BEPU cook/runtime tests pass without depending on BEPU native-endian raw serialized mesh bytes.

### Task 5: Block Future Raw `byte[]` Automatic Component Payload Members

**Files:**
- Modify: `engine/helengine.editor/serialization/scene/AutomaticScriptComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticScriptComponentRuntimeDeserializer.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/AutomaticScriptComponentBinaryPayloadGuardTests.cs`

- [ ] **Step 1: Add the failing guard test**

Cover one component with a public `byte[]` member and assert automatic component persistence throws with a clear message explaining that raw binary payloads must use engine-managed payload types instead.

- [ ] **Step 2: Implement the guard**

Reject `byte[]` in the automatic component persistence write/read path before the generic array logic handles it.

- [ ] **Step 3: Run the guard tests**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~AutomaticScriptComponentBinaryPayloadGuardTests" -v minimal
```

Expected: guard tests pass and the raw `byte[]` escape hatch is closed for reflected component payloads.

### Task 6: Final Focused Verification

**Files:**
- Modify: none

- [ ] **Step 1: Run the final focused verification set**

Run:

```powershell
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~StaticMeshColliderGenericPersistenceTests|FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~AutomaticScriptComponentBinaryPayloadGuardTests|FullyQualifiedName~CityStaticMeshShowcasePackagedSceneTests" -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.bepu.tests\helengine.bepu.tests.csproj --filter "FullyQualifiedName~BepuStaticMeshCollisionCookProcessorTests|FullyQualifiedName~BepuPhysicsWorld3DTests|FullyQualifiedName~BepuPhysicsFeatureGuard3DTests" -v minimal
```

Expected: all targeted suites pass.

# Physics Generic Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove manual physics scene serialization compatibility paths so physics components save, package, and load only through the generic reflected component pipeline.

**Architecture:** Runtime scene loading will rely on `RuntimeComponentRegistry` automatic reflected fallback for physics components. Editor packaging will stop detecting or rewriting legacy strict physics payloads and will only emit automatic runtime payloads. Tests will author physics component records through generic helpers and stop asserting legacy compatibility.

**Tech Stack:** C#/.NET 9, xUnit, helengine editor/runtime scene serialization, BEPU-backed physics runtime registration

---

### Task 1: Cover Generic Physics Runtime Loading

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add tests that serialize these components through the automatic runtime payload helper and assert they materialize through `RuntimeSceneLoadService`:

```csharp
[Fact]
public void Load_WhenSceneContainsGenericRigidBody3DComponent_MaterializesTheComponent() { }

[Fact]
public void Load_WhenSceneContainsGenericBoxCollider3DComponent_MaterializesTheComponent() { }

[Fact]
public void Load_WhenSceneContainsGenericKinematicMotion3DComponent_MaterializesTheComponent() { }

[Fact]
public void Load_WhenSceneContainsGenericCharacterController3DComponent_MaterializesTheComponent() { }
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests`
Expected: physics generic runtime tests fail before the production cutover is complete.

- [ ] **Step 3: Add minimal helper code in the test file**

Use existing `WriteAutomaticRuntimeComponentPayload(...)` style helpers to create automatic runtime payloads for:

```csharp
new RigidBody3DComponent { BodyKind = BodyKind3D.Dynamic, UseGravity = true, Mass = 3d, GravityScale = 2d, LinearVelocity = new float3(1f, 2f, 3f), AngularVelocity = new float3(4f, 5f, 6f) }
new BoxCollider3DComponent { Size = new float3(7f, 8f, 9f), IsTrigger = true }
new KinematicMotion3DComponent { TargetPosition = new float3(1f, 0.5f, -2f), LinearVelocity = new float3(0.25f, 0.5f, 0.75f), SmoothingTimeSeconds = 1.5d, SnapToTargetWhenBlocked = true }
new CharacterController3DComponent { DesiredVelocity = new float3(1f, 0f, 0.5f), MaxSpeed = 4.5d, Acceleration = 1d, Radius = 0.4d, StepOffset = 0.25d }
```

- [ ] **Step 4: Run the targeted tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests`
Expected: the new physics generic runtime tests pass.

### Task 2: Rewrite Packager Physics Tests To The Generic Path

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing test edits**

Replace hand-authored strict physics payload helpers in packager tests with generic reflected helpers for:

```csharp
WriteAutomaticRigidBody3DComponentPayload(...)
WriteAutomaticBoxCollider3DComponentPayload(...)
WriteAutomaticKinematicMotion3DComponentPayload(...)
WriteAutomaticCharacterController3DComponentPayload(...)
```

Update tests that currently call:

```csharp
WriteRigidBody3DComponentPayload(...)
WriteBoxCollider3DComponentPayload(...)
WriteKinematicMotion3DComponentPayload(...)
WriteCharacterController3DComponentPayload(...)
```

so they exercise only the generic path.

- [ ] **Step 2: Run the focused packager tests to verify they fail correctly**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`
Expected: any remaining strict-physics assumptions fail before production cleanup is complete.

- [ ] **Step 3: Add minimal helper and assertion updates**

Keep tests verifying packaged scene content and runtime load behavior, but remove any assertion that depends on legacy strict payload layouts or manual physics deserializer classes.

- [ ] **Step 4: Run the focused packager tests to verify they pass**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`
Expected: packager physics tests pass on generic payloads only.

### Task 3: Remove Packaging Compatibility Shim

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`

- [ ] **Step 1: Confirm the shim is covered by failing tests**

Target the logic around:

```csharp
UsesLegacyBuiltInPhysicsPayload(...)
DeserializeBuiltInPhysicsComponentForPackaging(...)
```

Expected: runtime/packager tests already cover the generic-only path.

- [ ] **Step 2: Remove the legacy strict physics branch**

Delete the branch that intercepts:

```csharp
helengine.RigidBody3DComponent
helengine.BoxCollider3DComponent
helengine.KinematicMotion3DComponent
helengine.CharacterController3DComponent
```

before the generic automatic rewrite path.

- [ ] **Step 3: Delete obsolete helper methods and constants**

Remove:

```csharp
LegacyRigidBodyPayloadLength
CurrentRigidBodyPayloadLength
LegacyBoxColliderPayloadLength
CurrentBoxColliderPayloadLength
KinematicMotionPayloadLength
CharacterControllerPayloadLength
DeserializeBuiltInPhysicsComponentForPackaging(...)
UsesLegacyBuiltInPhysicsPayload(...)
```

- [ ] **Step 4: Run focused packager and transform-service tests**

Run:
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~SceneComponentPackagingTransformServiceTests`

Expected: physics packaging remains green without the legacy branch.

### Task 4: Remove Manual Runtime Physics Deserializers

**Files:**
- Modify: `engine/helengine.bepu/BepuRuntimeComponentRegistration.cs`
- Delete: `engine/helengine.physics/RuntimeRigidBody3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeBoxCollider3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeSphereCollider3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeCapsuleCollider3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeStaticMeshCollider3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeKinematicMotion3DComponentDeserializer.cs`
- Delete: `engine/helengine.physics/RuntimeCharacterController3DComponentDeserializer.cs`

- [ ] **Step 1: Remove explicit runtime registration calls**

Delete:

```csharp
core.RegisterRuntimeComponentDeserializer(new RuntimeRigidBody3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeBoxCollider3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeSphereCollider3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeCapsuleCollider3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeStaticMeshCollider3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeKinematicMotion3DComponentDeserializer());
core.RegisterRuntimeComponentDeserializer(new RuntimeCharacterController3DComponentDeserializer());
```

- [ ] **Step 2: Delete the manual deserializer files**

Remove the seven runtime deserializer classes listed above.

- [ ] **Step 3: Run runtime scene-load tests to verify automatic fallback handles physics**

Run: `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests`
Expected: physics components still materialize through the generic automatic runtime path.

### Task 5: Update Registration and Audit Tests

**Files:**
- Modify: `engine/helengine.editor.tests/AutomaticPhysicsRuntimePayloadTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPhysics3DCodegenFeatureSymbolServiceTests.cs`
- Modify: any runtime registration tests discovered during execution

- [ ] **Step 1: Write the failing expectation updates**

Update tests so they no longer expect physics components to be backed by explicit manual runtime deserializer classes.

- [ ] **Step 2: Run the focused tests to verify they fail before production cleanup is complete**

Run:
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~AutomaticPhysicsRuntimePayloadTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPhysics3DCodegenFeatureSymbolServiceTests`

Expected: registration or expectation-only tests fail until updated.

- [ ] **Step 3: Apply minimal expectation changes**

Keep tests validating physics payload structure and codegen outcomes, but remove assumptions about manual runtime deserializer ownership.

- [ ] **Step 4: Run the focused tests to verify they pass**

Run the same commands from Step 2.
Expected: both focused suites pass.

### Task 6: Final Verification

**Files:**
- Modify: none

- [ ] **Step 1: Run the focused full verification set**

Run:
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~SceneManagerTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~SceneMapServiceTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~SceneComponentPackagingTransformServiceTests`
- `.\dotnetw.cmd test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorPlatformBuildScenePackagerTests`

Expected: all focused suites pass.

- [ ] **Step 2: Run repository searches to verify manual physics scene deserializers are gone**

Run:
- `rg -n ": IRuntimeComponentDeserializer" engine/helengine.physics`
- `rg -n "UsesLegacyBuiltInPhysicsPayload|DeserializeBuiltInPhysicsComponentForPackaging" engine/helengine.editor`

Expected:
- no manual physics runtime deserializer classes remain
- no legacy physics packaging shim remains

- [ ] **Step 3: Review the diff for scope control**

Run: `git diff -- engine/helengine.bepu engine/helengine.physics engine/helengine.editor engine/helengine.editor.tests`
Expected: changes are limited to physics persistence/runtime cutover and aligned tests.

# Console-First Physics Box Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an isolated, scalar, allocation-free HelPhysics box solver that can simulate and sleep the current four-box stack at 20 Hz, expose diagnostic metrics, bind existing box components, and compile through the C#-to-C++ generator without replacing BEPU as the default runtime.

**Architecture:** Add a new `helengine.helphysics` project containing dedicated physics math, fixed-capacity indexed pools, coherent sweep-and-prune, oriented box SAT and face clipping, persistent four-point manifolds, a sequential-impulse solver, and whole-island sleeping. Keep entity binding at the edge, keep BEPU registration unchanged during this slice, and validate generated Windows C++ as a separate gate after managed behavior is stable.

**Tech Stack:** C# 13 / .NET 9, xUnit, existing HelEngine component/runtime contracts, the existing `codegen.exe` C#-to-C++ generator, and MSVC C++20.

## Global Constraints

- This plan implements boxes only. Sphere, capsule, static mesh, character controller, joints, vehicles, CCD, and multithreading are excluded.
- Existing serialized components and scenes remain unchanged.
- BEPU remains the default registered runtime throughout this plan.
- Equivalent gameplay is required across platforms; cross-platform bit identity is not required.
- The scalar implementation must be correct and useful without SIMD.
- `System.Numerics.Vector<T>` and hardware-intrinsic APIs are prohibited from the new project.
- The simulation step must not use `List`, `Dictionary`, LINQ, delegates, virtual dispatch, exceptions for ordinary control flow, or heap allocation. Exact capacity faults are the sole diagnostic exception path.
- Every persistent and scratch capacity is supplied at world construction.
- Capacity exhaustion must produce an exact hard diagnostic and must not drop work.
- Use one class, struct, interface, or enum per file.
- Add substantive XML comments to every class, struct, field, constructor, property, and method.
- Fields use PascalCase; redundant `private` modifiers are omitted; braces remain on the same line as declarations and control statements.
- Do not use tuples, local helper functions, nullable annotations, `Mathf`, or silent default values.
- Use double-precision calculations for authoring-boundary validation and convert explicitly to `PhysicsScalar`.
- Run only the focused test project while developing each task.

## Scope Boundary

This plan ends when the box-only engine is independently usable and generated-C++ clean. It does not switch `Physics3DRuntimeComponentRegistration.Register` away from BEPU and does not modify console repositories. Follow-on plans cover additional primitive shapes and triggers, runtime backend selection and Windows packaging, then platform numeric kernels beginning with PlayStation 2 and Nintendo DS.

## File Structure

Create production code under `engine/helengine.helphysics` and tests under `engine/helengine.helphysics.tests`.

- `math/`: scalar, vector, quaternion, matrix, and math operations.
- `storage/`: handles, capacities, fixed pools, and capacity diagnostics.
- `geometry/`: box shapes, AABBs, transforms, and inertia.
- `broadphase/`: proxies, sweep endpoints, candidate pairs, and coherent sorting.
- `collision/`: SAT queries, face clipping, contacts, manifolds, and persistent cache.
- `solver/`: materials, velocity constraints, warm starting, friction, restitution, and penetration correction.
- `islands/`: dynamic connectivity, sleep qualification, and wake propagation.
- `runtime/`: world settings, world pipeline, metrics, body descriptions, scene binding, and entity synchronization.

The test project mirrors those folders. Test-only entity factories belong in `testing/HelPhysicsTestSceneFactory3D.cs`; production code must not contain test-scene construction.

---

## Task 1: Create the isolated projects and scalar math contract

**Files:**
- Create: `engine/helengine.helphysics/helengine.helphysics.csproj`
- Create: `engine/helengine.helphysics/AssemblyInfo.cs`
- Create: `engine/helengine.helphysics/math/PhysicsScalar.cs`
- Create: `engine/helengine.helphysics/math/PhysicsVector3.cs`
- Create: `engine/helengine.helphysics/math/PhysicsQuaternion.cs`
- Create: `engine/helengine.helphysics/math/PhysicsMatrix3x3.cs`
- Create: `engine/helengine.helphysics/math/PhysicsMath.cs`
- Create: `engine/helengine.helphysics.tests/helengine.helphysics.tests.csproj`
- Create: `engine/helengine.helphysics.tests/math/PhysicsMathTests.cs`

**Interfaces:**
- Consumes: `helengine.core` and `helengine.physics` project contracts.
- Produces: `PhysicsScalar`, `PhysicsVector3`, `PhysicsQuaternion`, `PhysicsMatrix3x3`, and `PhysicsMath`; later tasks use these types instead of `float3`, `float4`, or `System.Numerics` internally.

- [ ] **Step 1: Add the projects and write failing math tests**

Use these project references in `helengine.helphysics.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\helengine.core\helengine.core.csproj" SkipGetTargetFrameworkProperties="true" />
    <ProjectReference Include="..\helengine.physics\helengine.physics.csproj" SkipGetTargetFrameworkProperties="true" />
  </ItemGroup>
</Project>
```

The test project references `helengine.helphysics` and uses the same xUnit package versions as `helengine.physics3d.tests`.

```csharp
[Fact]
public void Cross_WithUnitAxes_ReturnsPositiveZAxis() {
    PhysicsVector3 result = PhysicsVector3.Cross(PhysicsVector3.UnitX, PhysicsVector3.UnitY);

    Assert.Equal(0f, result.X.ToFloat());
    Assert.Equal(0f, result.Y.ToFloat());
    Assert.Equal(1f, result.Z.ToFloat());
}

[Fact]
public void Rotate_WithQuarterTurnAroundZ_RotatesPositiveXToPositiveY() {
    PhysicsQuaternion rotation = PhysicsQuaternion.CreateFromAxisAngle(
        PhysicsVector3.UnitZ,
        PhysicsScalar.FromFloat((float)(Math.PI * 0.5d)));

    PhysicsVector3 result = rotation.Rotate(PhysicsVector3.UnitX);

    Assert.InRange(result.X.ToFloat(), -0.0001f, 0.0001f);
    Assert.InRange(result.Y.ToFloat(), 0.9999f, 1.0001f);
    Assert.InRange(result.Z.ToFloat(), -0.0001f, 0.0001f);
}
```

- [ ] **Step 2: Run the math tests and verify the missing-type failure**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~PhysicsMathTests
```

Expected: compilation fails because the dedicated physics math types do not exist.

- [ ] **Step 3: Implement the dedicated scalar and vector operations**

`PhysicsScalar` is a readonly value type with explicit construction and conversion:

```csharp
public readonly struct PhysicsScalar {
    readonly float Value;

    public PhysicsScalar(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value)) {
            throw new ArgumentOutOfRangeException(nameof(value), "Physics scalar values must be finite.");
        }

        Value = value;
    }

    public static PhysicsScalar Zero => new PhysicsScalar(0f);
    public static PhysicsScalar One => new PhysicsScalar(1f);
    public static PhysicsScalar FromFloat(float value) => new PhysicsScalar(value);
    public float ToFloat() => Value;
}
```

Add explicit arithmetic, comparison, absolute value, minimum, maximum, clamp, square root, and reciprocal-square-root operations. `PhysicsVector3` provides component arithmetic, `Dot`, `Cross`, length squared, normalization, and constants. `PhysicsQuaternion` provides normalization, conjugation, multiplication, axis-angle construction, and vector rotation. `PhysicsMatrix3x3` provides rows, transpose, vector transformation, quaternion construction, matrix multiplication, and diagonal construction.

Do not add implicit conversion operators; boundary conversion must remain visible.

- [ ] **Step 4: Run focused math tests**

Run the command from Step 2.

Expected: all math tests pass.

- [ ] **Step 5: Commit the math foundation**

```powershell
git add engine/helengine.helphysics engine/helengine.helphysics.tests
git commit -m "feat: add HelPhysics scalar math foundation"
```

---

## Task 2: Add fixed capacities, generational handles, and body storage

**Files:**
- Create: `engine/helengine.helphysics/storage/HelPhysicsCapacityExceededException.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsBodyHandle3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsShapeHandle3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsWorldCapacity3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsBodyState3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsBodyColdState3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsBodyPool3D.cs`
- Create: `engine/helengine.helphysics.tests/storage/HelPhysicsBodyPool3DTests.cs`

**Interfaces:**
- Consumes: Task 1 math types and existing `BodyKind3D`.
- Produces: `HelPhysicsBodyHandle3D`, `HelPhysicsShapeHandle3D`, `HelPhysicsWorldCapacity3D`, and `HelPhysicsBodyPool3D` with `Allocate`, `Release`, `GetRequiredState`, `GetRequiredColdState`, and `ActiveCount`.

- [ ] **Step 1: Write failing handle and capacity tests**

```csharp
[Fact]
public void ReleaseAndAllocate_ReusesIndexWithNewGeneration() {
    HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
    HelPhysicsBodyHandle3D first = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

    pool.Release(first);
    HelPhysicsBodyHandle3D second = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

    Assert.Equal(first.Index, second.Index);
    Assert.NotEqual(first.Generation, second.Generation);
    Assert.Throws<InvalidOperationException>(() => pool.GetRequiredState(first));
}

[Fact]
public void Allocate_WhenCapacityIsExhausted_ThrowsExactCapacityError() {
    HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
    pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

    HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(
        () => pool.Allocate(CreateDynamicState(), CreateDynamicColdState()));

    Assert.Equal("body", exception.PoolName);
    Assert.Equal(1, exception.Capacity);
}
```

- [ ] **Step 2: Verify the storage tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsBodyPool3DTests
```

Expected: compilation fails on missing storage types.

- [ ] **Step 3: Implement the fixed body pool**

`HelPhysicsBodyHandle3D` and `HelPhysicsShapeHandle3D` are readonly structs containing `ushort Index` and `ushort Generation`. Index `ushort.MaxValue` is reserved as invalid.

```csharp
internal struct HelPhysicsBodyState3D {
    public PhysicsVector3 Position;
    public PhysicsQuaternion Orientation;
    public PhysicsVector3 LinearVelocity;
    public PhysicsVector3 AngularVelocity;
    public PhysicsScalar InverseMass;
    public PhysicsMatrix3x3 LocalInverseInertia;
    public PhysicsScalar GravityScale;
    public PhysicsScalar LinearDamping;
    public PhysicsScalar AngularDamping;
    public ushort LowMotionStepCount;
    public bool IsAwake;
    public bool IsOccupied;
}
```

`HelPhysicsBodyColdState3D` stores shape handle, body kind, a `ushort MaterialIndex`, collision layer/mask, an `int EntityBindingId`, and authoring metadata that hot integration loops do not require. Task 2 deliberately stores material identity rather than depending on `HelPhysicsMaterial3D`, which is introduced in Task 8. `Allocate` requires both hot and cold state explicitly; it must not invent a cold-state default. Allocate parallel hot-state, cold-state, generation, and free-index arrays in the constructor. Initialize a deterministic free list in ascending allocation order. `GetRequiredState` and `GetRequiredColdState` return references only after validating index, generation, and occupancy. `Release` increments generation and rejects double release. Reject capacities below 1 or above 65,534.

- [ ] **Step 4: Run storage tests**

Run the command from Step 2.

Expected: all storage tests pass.

- [ ] **Step 5: Commit fixed body storage**

```powershell
git add engine/helengine.helphysics/storage engine/helengine.helphysics.tests/storage
git commit -m "feat: add fixed HelPhysics body storage"
```

---

## Task 3: Add box shapes, transforms, AABBs, and inertia

**Files:**
- Create: `engine/helengine.helphysics/geometry/HelPhysicsBoxShape3D.cs`
- Create: `engine/helengine.helphysics/geometry/HelPhysicsAabb3D.cs`
- Create: `engine/helengine.helphysics/geometry/HelPhysicsBoxGeometry3D.cs`
- Create: `engine/helengine.helphysics/storage/HelPhysicsShapePool3D.cs`
- Create: `engine/helengine.helphysics.tests/geometry/HelPhysicsBoxGeometry3DTests.cs`
- Create: `engine/helengine.helphysics.tests/storage/HelPhysicsShapePool3DTests.cs`

**Interfaces:**
- Consumes: Task 1 math and Task 2 shape handles.
- Produces: box shape allocation plus `ComputeWorldAabb`, `ComputeLocalInverseInertia`, `GetWorldAxis`, and `GetWorldVertex` operations.

- [ ] **Step 1: Write failing geometry tests**

```csharp
[Fact]
public void ComputeWorldAabb_WithNinetyDegreeZRotation_SwapsXYExtents() {
    HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));
    PhysicsQuaternion orientation = PhysicsQuaternion.CreateFromAxisAngle(
        PhysicsVector3.UnitZ,
        PhysicsScalar.FromFloat((float)(Math.PI * 0.5d)));

    HelPhysicsAabb3D aabb = HelPhysicsBoxGeometry3D.ComputeWorldAabb(
        box,
        PhysicsVector3.Zero,
        orientation,
        PhysicsScalar.Zero);

    Assert.InRange(aabb.Maximum.X.ToFloat(), 1.9999f, 2.0001f);
    Assert.InRange(aabb.Maximum.Y.ToFloat(), 0.9999f, 1.0001f);
    Assert.InRange(aabb.Maximum.Z.ToFloat(), 2.9999f, 3.0001f);
}
```

Add a unit-cube inertia test requiring diagonal inverse inertia `6` for unit mass, plus invalid-extent and static-zero-inertia tests.

- [ ] **Step 2: Verify geometry tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter "FullyQualifiedName~HelPhysicsBoxGeometry3DTests|FullyQualifiedName~HelPhysicsShapePool3DTests"
```

Expected: compilation fails on missing geometry types.

- [ ] **Step 3: Implement box geometry and fixed shape storage**

`HelPhysicsBoxShape3D` stores positive half extents. `HelPhysicsAabb3D` stores minimum and maximum and provides inclusive overlap testing.

Compute oriented AABB extents as:

```text
worldExtent = abs(rotationColumn0) * halfExtent.x
            + abs(rotationColumn1) * halfExtent.y
            + abs(rotationColumn2) * halfExtent.z
```

Compute box inverse inertia from full dimensions using `Ixx = mass * (height² + depth²) / 12`, with equivalent formulas for Y and Z. Static and kinematic callers receive a zero inverse-inertia matrix. `HelPhysicsShapePool3D` mirrors the generational fixed-pool behavior from Task 2 and stores only boxes in this slice.

- [ ] **Step 4: Run geometry and shape-pool tests**

Run the command from Step 2.

Expected: all selected tests pass.

- [ ] **Step 5: Commit box geometry**

```powershell
git add engine/helengine.helphysics/geometry engine/helengine.helphysics/storage/HelPhysicsShapePool3D.cs engine/helengine.helphysics.tests/geometry engine/helengine.helphysics.tests/storage/HelPhysicsShapePool3DTests.cs
git commit -m "feat: add HelPhysics box geometry"
```

---

## Task 4: Implement coherent sweep-and-prune

**Files:**
- Create: `engine/helengine.helphysics/broadphase/HelPhysicsBroadphaseProxy3D.cs`
- Create: `engine/helengine.helphysics/broadphase/HelPhysicsSweepEndpoint3D.cs`
- Create: `engine/helengine.helphysics/broadphase/HelPhysicsCandidatePair3D.cs`
- Create: `engine/helengine.helphysics/broadphase/HelPhysicsSweepAndPrune3D.cs`
- Create: `engine/helengine.helphysics.tests/broadphase/HelPhysicsSweepAndPrune3DTests.cs`

**Interfaces:**
- Consumes: body indices, body modes, collision layers/masks, awake state, and Task 3 AABBs.
- Produces: `UpdateProxy`, `RemoveProxy`, and `BuildCandidatePairs(HelPhysicsCandidatePair3D[] destination)` with deterministic ordered pairs.

- [ ] **Step 1: Write failing broadphase tests**

```csharp
[Fact]
public void BuildCandidatePairs_WithOverlappingDynamicAndStaticProxy_EmitsOneOrderedPair() {
    HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(4, 4);
    broadphase.UpdateProxy(2, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
    broadphase.UpdateProxy(1, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-2f, 2f));
    HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[4];

    int count = broadphase.BuildCandidatePairs(pairs);

    Assert.Equal(1, count);
    Assert.Equal(1, pairs[0].FirstBodyIndex);
    Assert.Equal(2, pairs[0].SecondBodyIndex);
}
```

Add static-static rejection, non-overlap, mask rejection, deterministic ordering after motion, sleeping-dynamic against static rejection, sleeping-sleeping rejection, moving-kinematic wake candidacy, and candidate-capacity exhaustion tests.

- [ ] **Step 2: Verify broadphase tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsSweepAndPrune3DTests
```

Expected: compilation fails on missing broadphase types.

- [ ] **Step 3: Implement endpoint storage and insertion sorting**

Allocate exactly two endpoints per proxy. Each endpoint stores scalar X value, body index, and minimum/maximum kind. Sort by value, then minimum before maximum, then body index. Use insertion sort so coherent frames approach linear work.

During the sweep, keep a fixed integer active-body array. Before emitting a pair, test full three-axis AABB overlap, layer/mask compatibility in both directions, body modes, and awake/kinematic-motion state. A pair enters narrow phase only when it contains an awake dynamic body or a moved kinematic body; sleeping dynamic/static and sleeping/sleeping pairs remain quiescent. Store lower body index first. If destination capacity is exhausted, throw `HelPhysicsCapacityExceededException("candidate pair", capacity)`.

- [ ] **Step 4: Run broadphase tests**

Run the command from Step 2.

Expected: all broadphase tests pass.

- [ ] **Step 5: Commit the broadphase**

```powershell
git add engine/helengine.helphysics/broadphase engine/helengine.helphysics.tests/broadphase
git commit -m "feat: add HelPhysics sweep and prune"
```

## Task 5: Implement oriented-box SAT queries

**Files:**

- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxSatAxisKind3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxSatResult3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxSat3D.cs`
- Create: `engine/helengine.helphysics.tests/collision/HelPhysicsBoxSat3DTests.cs`

- [ ] **Step 1: Write the SAT tests**

Cover separated boxes, half-unit overlap with a `+X` normal and `0.5` depth, rotated face-to-face overlap, edge-to-edge overlap, nearly parallel axes, exact touching, and swapped body order producing the opposite normal.

The public query is:

```csharp
public static bool TryFindMinimumPenetration(
    in HelPhysicsBoxShape3D ShapeA,
    in HelPhysicsBodyState3D BodyA,
    in HelPhysicsBoxShape3D ShapeB,
    in HelPhysicsBodyState3D BodyB,
    out HelPhysicsBoxSatResult3D Result)
```

`Result.Normal` always points from A toward B. It also identifies whether the minimum axis is an A face, B face, or edge pair, plus the relevant axis indices and penetration depth.

- [ ] **Step 2: Verify the SAT tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsBoxSat3DTests
```

Expected: compilation fails because the SAT types do not exist.

- [ ] **Step 3: Implement all 15 SAT axes**

Test the three A face axes, three B face axes, and nine edge cross axes using scalar dot products and the standard projected-radius formula. Skip a cross axis only when its squared length is below `1e-8f`; normalize every accepted axis before comparing depths.

Use deterministic tie-breaking: A faces in axis order, then B faces, then edge axes in nested A/B order. Orient the winning axis from A to B before returning it. Exact touching counts as contact.

- [ ] **Step 4: Run the SAT tests**

Run the command from Step 2.

Expected: all SAT tests pass.

- [ ] **Step 5: Commit SAT collision queries**

```powershell
git add engine/helengine.helphysics/collision engine/helengine.helphysics.tests/collision/HelPhysicsBoxSat3DTests.cs
git commit -m "feat: add HelPhysics box SAT"
```

## Task 6: Generate stable box contact manifolds

**Files:**

- Create: `engine/helengine.helphysics/collision/HelPhysicsContactFeature3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsContactPoint3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsContactManifold3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxClipVertex3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxCollisionScratch3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsBoxBoxCollision3D.cs`
- Create: `engine/helengine.helphysics.tests/collision/HelPhysicsBoxBoxCollision3DTests.cs`

- [ ] **Step 1: Write manifold-generation tests**

Verify a face-on-face overlap returns four contacts with approximately `0.1` penetration, a corner-on-face case returns one contact, an edge-on-edge case returns one contact, separated boxes return false, swapping bodies reverses the normal, and repeated identical queries produce identical contact order and feature identifiers.

Use this allocation-free entry point:

```csharp
public static bool TryBuildManifold(
    in HelPhysicsBoxShape3D ShapeA,
    in HelPhysicsBodyState3D BodyA,
    in HelPhysicsBoxShape3D ShapeB,
    in HelPhysicsBodyState3D BodyB,
    HelPhysicsBoxCollisionScratch3D Scratch,
    ref HelPhysicsContactManifold3D Manifold)
```

- [ ] **Step 2: Verify the manifold tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsBoxBoxCollision3DTests
```

Expected: compilation fails because manifold generation is missing.

- [ ] **Step 3: Implement contact storage and face clipping**

Store at most four points directly as `Contact0` through `Contact3`. Provide checked `GetContact(int)` and `SetContact(int, in HelPhysicsContactPoint3D)` switches; do not allocate a per-manifold array.

For face contacts, select the reference face from the SAT result, choose the most anti-parallel incident face, clip its four vertices against the four side planes of the reference face, discard points beyond the reference plane, and retain up to four deepest points. Build deterministic feature identifiers from reference face, incident face, and clipped vertex/edge provenance.

- [ ] **Step 4: Implement edge contacts**

Construct the two winning support edges and calculate their closest points with a scalar segment-to-segment query. Use their midpoint as the contact position and the SAT depth as penetration. Keep the normal oriented from body A to body B.

- [ ] **Step 5: Run manifold tests**

Run the command from Step 2.

Expected: all manifold tests pass.

- [ ] **Step 6: Commit manifold generation**

```powershell
git add engine/helengine.helphysics/collision engine/helengine.helphysics.tests/collision/HelPhysicsBoxBoxCollision3DTests.cs
git commit -m "feat: add HelPhysics box manifolds"
```

## Task 7: Persist manifolds and warm-start impulses

**Files:**

- Create: `engine/helengine.helphysics/collision/HelPhysicsPairKey3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsManifoldCacheEntry3D.cs`
- Create: `engine/helengine.helphysics/collision/HelPhysicsManifoldCache3D.cs`
- Create: `engine/helengine.helphysics.tests/collision/HelPhysicsManifoldCache3DTests.cs`

- [ ] **Step 1: Write cache tests**

Test feature-identifier persistence, local-anchor proximity fallback, impulse reset for a genuinely new contact, removal of untouched pairs, `Touch` retention for a quiescent sleeping pair, deterministic behavior across a hash collision, and exact capacity exhaustion.

The cache exposes:

```csharp
public void Update(HelPhysicsPairKey3D Pair, ref HelPhysicsContactManifold3D Manifold, int StepId)
public void Touch(HelPhysicsPairKey3D Pair, int StepId)
public bool TryGet(HelPhysicsPairKey3D Pair, out HelPhysicsContactManifold3D Manifold)
public void RemoveUntouched(int StepId)
public int Count { get; }
```

- [ ] **Step 2: Verify cache tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsManifoldCache3DTests
```

Expected: compilation fails because the cache types are missing.

- [ ] **Step 3: Implement the fixed hash table**

Use a power-of-two entry array with open addressing and linear probing. Canonicalize pair keys by lower body index first. Track empty, occupied, and tombstone states explicitly so removal does not break probe chains. Capacity exhaustion raises `HelPhysicsCapacityExceededException("manifold", capacity)`.

- [ ] **Step 4: Match and copy impulses**

For every new contact, first match an unused old contact by exact feature identifier. If no feature matches, match the nearest unused old local anchor when squared distance is below `0.0004f`. Copy accumulated normal and both tangent impulses only from the selected old contact. An old contact may be consumed once. `Touch` advances the lifetime of an existing manifold without running collision detection; the world uses it only for still-overlapping quiescent sleeping pairs.

- [ ] **Step 5: Run cache tests**

Run the command from Step 2.

Expected: all cache tests pass.

- [ ] **Step 6: Commit manifold persistence**

```powershell
git add engine/helengine.helphysics/collision engine/helengine.helphysics.tests/collision/HelPhysicsManifoldCache3DTests.cs
git commit -m "feat: persist HelPhysics manifolds"
```

## Task 8: Integrate bodies and solve contact constraints

**Files:**

- Create: `engine/helengine.helphysics/solver/HelPhysicsMaterial3D.cs`
- Create: `engine/helengine.helphysics/solver/HelPhysicsContactConstraint3D.cs`
- Create: `engine/helengine.helphysics/solver/HelPhysicsContactSolver3D.cs`
- Create: `engine/helengine.helphysics/solver/HelPhysicsBodyIntegrator3D.cs`
- Create: `engine/helengine.helphysics/solver/HelPhysicsPoseIntegrator3D.cs`
- Create: `engine/helengine.helphysics/solver/HelPhysicsPenetrationCorrector3D.cs`
- Create: `engine/helengine.helphysics.tests/solver/HelPhysicsContactSolver3DTests.cs`
- Create: `engine/helengine.helphysics.tests/solver/HelPhysicsPoseIntegrator3DTests.cs`
- Modify: `engine/helengine.helphysics/storage/HelPhysicsBodyColdState3D.cs`
- Modify: `engine/helengine.helphysics/storage/HelPhysicsBodyState3D.cs`
- Modify: `engine/helengine.helphysics/storage/HelPhysicsBodyPool3D.cs`
- Modify: `engine/helengine.helphysics/collision/HelPhysicsManifoldCache3D.cs`
- Modify: `engine/helengine.helphysics.tests/storage/HelPhysicsBodyPool3DTests.cs`
- Modify: `engine/helengine.helphysics.tests/collision/HelPhysicsManifoldCache3DTests.cs`

- [ ] **Step 1: Write solver and integration tests**

Cover a downward-moving body stopped by a static contact, restitution, static friction, dynamic friction clamping, angular response to an off-center impulse, warm starting, contacts never pulling bodies together, quaternion normalization, and penetration correction that does not inject bounce.

Expose phase-specific methods so each stage is independently testable:

```csharp
public void Prepare(PhysicsScalar StepSeconds, HelPhysicsBodyPool3D Bodies, HelPhysicsPairKey3D[] Pairs, HelPhysicsContactManifold3D[] Manifolds, int ManifoldCount)
public void WarmStart(HelPhysicsBodyPool3D Bodies)
public void SolveVelocityIteration(HelPhysicsBodyPool3D Bodies)
public void WriteBack(HelPhysicsContactManifold3D[] Manifolds)
public void CorrectPenetration(HelPhysicsBodyPool3D Bodies)
public void IntegrateVelocity(PhysicsScalar StepSeconds, in PhysicsVector3 Gravity, HelPhysicsBodyPool3D Bodies)
public void IntegratePose(PhysicsScalar StepSeconds, HelPhysicsBodyPool3D Bodies)
```

Task 8 replaces the temporary `ushort MaterialIndex` in `HelPhysicsBodyColdState3D` with an explicit `HelPhysicsMaterial3D` value; materials remain cold data and require no separate runtime-growing registry. Add accumulated force and torque vectors to `HelPhysicsBodyState3D`. Add `Capacity`, `IsOccupied(int BodyIndex)`, `GetRequiredStateByIndex(int BodyIndex)`, and `GetRequiredColdStateByIndex(int BodyIndex)` to the body pool so fixed hot loops and pair-key solving do not synthesize handles or allocate enumerators. Extend `HelPhysicsManifoldCache3D` with `StoreSolved(HelPhysicsPairKey3D Pair, ref HelPhysicsContactManifold3D Manifold, int StepId)`, which requires an existing same-step entry and replaces only its three solved impulses per contact. The world calls `Update` before solving for matching/warm start, `WriteBack` after velocity iterations, and `StoreSolved` to persist those resulting impulses without running contact matching a second time.

- [ ] **Step 2: Verify solver tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter "FullyQualifiedName~HelPhysicsContactSolver3DTests|FullyQualifiedName~HelPhysicsPoseIntegrator3DTests"
```

Expected: compilation fails because solver and integrator types are missing.

- [ ] **Step 3: Implement velocity and pose integration**

Integrate forces, gravity, and torque only for awake dynamic bodies. Apply damping as `velocity *= 1 / (1 + damping * stepSeconds)`. Integrate orientation from angular velocity and normalize the resulting quaternion. Clear accumulated forces and torques after velocity integration.

- [ ] **Step 4: Implement the sequential impulse solver**

For each contact, precompute normal effective mass, two orthogonal tangent effective masses, lever arms, restitution bias, and penetration data. Combine friction geometrically. Apply restitution only when incoming normal speed is below `-1.0f`.

Warm-start using cached normal and tangent impulses. During each velocity iteration, clamp accumulated normal impulse to zero or greater; solve static friction inside the static cone, otherwise clamp dynamic friction to the dynamic cone.

- [ ] **Step 5: Implement split penetration correction**

Use positional correction separate from velocity impulses with `0.005f` penetration slop, `0.2f` correction fraction, and `0.2f` maximum correction per step. This pass changes poses but does not add kinetic energy.

- [ ] **Step 6: Run solver tests**

Run the command from Step 2.

Expected: all solver and integration tests pass.

- [ ] **Step 7: Commit integration and solving**

```powershell
git add engine/helengine.helphysics/solver engine/helengine.helphysics.tests/solver
git commit -m "feat: solve HelPhysics box contacts"
```

## Task 9: Build contact islands and aggressively sleep them

**Files:**

- Create: `engine/helengine.helphysics/islands/HelPhysicsIsland3D.cs`
- Create: `engine/helengine.helphysics/islands/HelPhysicsIslandBuilder3D.cs`
- Create: `engine/helengine.helphysics/islands/HelPhysicsIslandSleeper3D.cs`
- Create: `engine/helengine.helphysics/islands/HelPhysicsWakeReason3D.cs`
- Create: `engine/helengine.helphysics.tests/islands/HelPhysicsIslandBuilder3DTests.cs`
- Create: `engine/helengine.helphysics.tests/islands/HelPhysicsIslandSleeper3DTests.cs`

- [ ] **Step 1: Write island and sleeping tests**

Verify that two independent boxes resting on the same static floor remain separate islands, contacting dynamic bodies join one island, the entire island sleeps after its configured quiet ticks, one fast body prevents the whole island from sleeping, and sleeping zeroes linear/angular velocities and accumulated forces.

Also verify wake propagation after explicit force, a new candidate contact, and contact with a moving kinematic body. Record the initiating `HelPhysicsWakeReason3D` for profiler diagnostics.

- [ ] **Step 2: Verify island tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter "FullyQualifiedName~HelPhysicsIslandBuilder3DTests|FullyQualifiedName~HelPhysicsIslandSleeper3DTests"
```

Expected: compilation fails because island types are missing.

- [ ] **Step 3: Implement fixed union-find island construction**

Preallocate parent, rank, body-index, and island-range arrays. Union only dynamic bodies connected by active contact manifolds. Static and kinematic bodies constrain an island but never connect two dynamic islands through themselves. Sort island members by body index to make traversal deterministic.

- [ ] **Step 4: Implement aggressive island sleeping and wake propagation**

An island gains one quiet tick only when every member remains below its configured squared linear and angular sleep thresholds. Sleep the complete island when every member reaches `SleepTicks`. Any explicit force/impulse, meaningful new candidate contact, or moving kinematic contact wakes the complete connected island and resets its counters.

- [ ] **Step 5: Run island tests**

Run the command from Step 2.

Expected: all island tests pass.

- [ ] **Step 6: Commit islands and sleeping**

```powershell
git add engine/helengine.helphysics/islands engine/helengine.helphysics.tests/islands
git commit -m "feat: add HelPhysics islands and sleeping"
```

## Task 10: Assemble the deterministic world step

**Files:**

- Create: `engine/helengine.helphysics/runtime/HelPhysicsWorldSettings3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsBodyDescription3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsBodySnapshot3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsStepMetrics3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsWorld3D.cs`
- Create: `engine/helengine.helphysics.tests/runtime/HelPhysicsWorld3DTests.cs`
- Create: `engine/helengine.helphysics.tests/runtime/HelPhysicsWorld3DAllocationTests.cs`
- Create: `engine/helengine.helphysics.tests/testing/HelPhysicsWorldFixture.cs`

- [ ] **Step 1: Write the world behavior tests**

Create a `HelPhysicsWorldFixture.CreateFourBoxStack()` helper containing one static ground box and four dynamic unit boxes. Step it 200 times at 20 Hz through `public void Step(double StepSeconds)` and assert all four dynamic bodies are sleeping, remain ordered vertically, and stay within explicit expected position tolerances.

```csharp
HelPhysicsWorldFixture Fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
HelPhysicsWorld3D World = Fixture.World;

for (int StepIndex = 0; StepIndex < 200; StepIndex++) {
    World.Step(1.0f / 20.0f);
}

for (int BoxIndex = 0; BoxIndex < Fixture.DynamicBoxes.Length; BoxIndex++) {
    HelPhysicsBodySnapshot3D Snapshot = World.GetBodySnapshot(Fixture.DynamicBoxes[BoxIndex]);
    Assert.False(Snapshot.IsAwake);
}
```

Also test deterministic replay, body removal invalidating a generation handle, invalid time steps, exact capacity diagnostics, per-step body/contact/manifold metrics, and force/impulse wake behavior.

- [ ] **Step 2: Write the steady-state allocation test**

Create and settle the same fixture, warm the `Step` path, force a collection, record `GC.GetAllocatedBytesForCurrentThread()`, run 1,000 steps, and assert the byte count is unchanged. Keep test-framework assertions outside the measured loop.

- [ ] **Step 3: Verify world tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter "FullyQualifiedName~HelPhysicsWorld3DTests|FullyQualifiedName~HelPhysicsWorld3DAllocationTests"
```

Expected: compilation fails because the world pipeline does not exist.

- [ ] **Step 4: Implement settings, descriptions, and snapshots**

Use these initial defaults, all overridable at construction:

```text
Bodies: 32
Shapes: 32
Candidate pairs: 128
Manifolds: 64
Contact points: 256
Islands: 32
Deferred commands: 128
Velocity iterations: 4
Penetration correction passes: 1
```

Reject non-positive capacities and invalid fixed step sizes at the API boundary. A body description requires an explicit shape, motion kind, pose, material, collision layer/mask, sleep thresholds, and sleep tick count; do not invent missing values.

- [ ] **Step 5: Implement `HelPhysicsWorld3D.Step` in fixed phases**

Execute this exact order, matching the approved design:

1. Validate the public `double` step, convert it once to `PhysicsScalar`, and apply deferred commands.
2. Update moved/active broadphase proxies and maintain endpoint order.
3. Produce candidate pairs and wake sleeping islands touched by active candidates.
4. Run SAT and build/persist contact manifolds.
5. Build dynamic-body islands.
6. Integrate awake dynamic forces, gravity, torque, and damping into velocities.
7. Prepare and warm-start contact constraints.
8. Run configured velocity iterations.
9. Run configured penetration-correction passes without changing kinetic velocity.
10. Integrate poses.
11. Update proxies and evaluate whole-island sleeping.
12. Touch cached manifolds whose dynamic participants remain asleep against unchanged static/sleeping participants, publish step metrics, remove genuinely untouched manifolds, and clear transient accumulators.

The scene-binding wrapper synchronizes kinematic entities before phase 2 and dynamic entities after phase 11. Trigger processing is deliberately absent from this box-only slice and is added with the later primitive/trigger plan.

The simulation loops may not allocate, box, use LINQ, dispatch delegates, or use exceptions for ordinary control flow. Exact capacity faults remain a diagnostic exception path. Interface dispatch is allowed only at the outer engine boundary, not inside body/contact loops.

- [ ] **Step 6: Expose profiler counters without timing inside the world**

Store body count, awake body count, candidate count, manifold count, contact count, island count, sleeping island count, solver iteration count, and wake counts in `HelPhysicsStepMetrics3D`. Implement `IPhysicsRuntimeProfilerMetricsProvider` as:

```csharp
public bool TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics Metrics)
```

Map HelPhysics body, contact, and manifold totals into `RuntimePhysicsProfilerMetrics`. Do not use `Stopwatch` in the allocation-sensitive world path; outer Tracy zones provide timing.

- [ ] **Step 7: Run world tests and the complete new test project**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter "FullyQualifiedName~HelPhysicsWorld3DTests|FullyQualifiedName~HelPhysicsWorld3DAllocationTests"
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj
```

Expected: the four-box stack sleeps, replay is deterministic, the measured loop allocates zero bytes, and all HelPhysics tests pass.

- [ ] **Step 8: Commit the world pipeline**

```powershell
git add engine/helengine.helphysics/runtime engine/helengine.helphysics.tests/runtime engine/helengine.helphysics.tests/testing/HelPhysicsWorldFixture.cs
git commit -m "feat: add deterministic HelPhysics world"
```

## Task 11: Bind HelPhysics to engine scenes without changing the default runtime

**Files:**

- Create: `engine/helengine.helphysics/runtime/HelPhysicsEntityBinding3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsSceneBinder3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsEntitySynchronizer3D.cs`
- Create: `engine/helengine.helphysics/runtime/HelPhysicsRuntimeFactory3D.cs`
- Create: `engine/helengine.helphysics.tests/runtime/HelPhysicsSceneBindingTests.cs`
- Create: `engine/helengine.helphysics.tests/testing/HelPhysicsTestSceneFactory3D.cs`

- [ ] **Step 1: Write scene-binding tests**

Use `HelPhysicsTestSceneFactory3D` to build real engine entities/components. Verify recursive scene traversal registers a ground and four boxes, missing rigid bodies are rejected, multiple colliders on one body are rejected, invalid box scale is rejected, and `StaticMeshCollider3DComponent` is rejected with an exception naming that exact unsupported class.

Verify dynamic transforms and velocities synchronize back to entities, kinematic poses synchronize into physics before stepping, and removing an entity invalidates its binding rather than silently reusing a stale handle.

- [ ] **Step 2: Verify binding tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsSceneBindingTests
```

Expected: compilation fails because the binding layer is missing.

- [ ] **Step 3: Implement strict scene translation**

Translate only supported rigid-body plus box-collider entities. Compute box half extents from collider dimensions and entity scale, reject zero/negative/non-finite scale, derive static mass/inertia explicitly, and store one `HelPhysicsEntityBinding3D` per registered entity. No fallback collider, implicit body, or best-effort skip is allowed.

- [ ] **Step 4: Implement bidirectional synchronization and the factory**

Before each step, copy kinematic transforms and explicit velocity updates into HelPhysics. After each step, copy dynamic transforms and velocities back into engine components. `HelPhysicsRuntimeFactory3D` constructs the world from explicit capacities/settings and provides the test/build entry point without altering global registration.

Do not change `Physics3DRuntimeComponentRegistration.Register`; BEPU remains the shipping/default Windows runtime throughout this slice.

- [ ] **Step 5: Run new and existing integration tests**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsSceneBindingTests
dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~GeneratedRuntimeModuleManifestTests|FullyQualifiedName~PhysicsWorld3DSceneLoadTests"
```

Expected: HelPhysics binding tests pass and BEPU registration/scene loading remains unchanged.

- [ ] **Step 6: Commit engine binding**

```powershell
git add engine/helengine.helphysics/runtime engine/helengine.helphysics.tests/runtime/HelPhysicsSceneBindingTests.cs engine/helengine.helphysics.tests/testing/HelPhysicsTestSceneFactory3D.cs
git commit -m "feat: bind HelPhysics box scenes"
```

## Task 12: Make generated C++ compilation a required gate

**Files:**

- Create: `scripts/validate-helphysics-generated-cpp.ps1`
- Modify only if a generator defect is proven: `C:\dev\helworks\csharpcodegen\cs2.cpp\**`
- Add a matching generator regression test only if the generator changes: `C:\dev\helworks\csharpcodegen\cs2.cpp.tests\**`

- [ ] **Step 1: Write the generated-C++ validation script**

The script takes mandatory `CodegenPath` and optional `OutputPath`. Resolve both to absolute paths and require `OutputPath` to be a unique child of repository `.validation`; never delete or reuse a broad/temp directory. Pass the repository feature catalog explicitly.

Generate with:

```powershell
& $CodegenPath --cpp --project engine\helengine.helphysics\helengine.helphysics.csproj --output $GeneratedPath --feature-catalog engine\helengine.editor\codegen\features\helengine-feature-catalog.json --platform windows --language cpp --endianness little --set include-project-defined-preprocessor-symbols=false --set write-conversion-report=true
```

Compile with the generated MSVC build script under the Visual Studio developer environment:

```powershell
cmd.exe /c "call \"C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat\" -arch=amd64 -host_arch=amd64 && call \"$GeneratedPath\build_msvc.bat\""
```

Fail on a nonzero process exit code, a missing `build\msvc\generated_unity.obj`, or conversion-report/generated-output matches for unresolved/unsupported symbols, `System.Numerics`, or generic `Vector<` usage. This gate executes generation and compilation; do not add tests that grep authored C# source text.

- [ ] **Step 2: Run the behavioral and generated-C++ gates**

```powershell
dotnet publish C:\dev\helworks\csharpcodegen\codegen\codegen.csproj -c Release -o .validation\tools\codegen
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsWorld3DAllocationTests
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate-helphysics-generated-cpp.ps1 -CodegenPath .validation\tools\codegen\codegen.exe
```

Expected: steady-state stepping allocates zero managed bytes, generation reports no unsupported dependency, MSVC exits zero, and `build\msvc\generated_unity.obj` exists.

If generation exposes a translator bug, add the smallest failing regression test in the separate `C:\dev\helworks\csharpcodegen` repository, fix and commit the C# generator there, republish the tool, and rerun this gate. Do not patch generated output.

- [ ] **Step 3: Commit the generation gate**

```powershell
git add scripts/validate-helphysics-generated-cpp.ps1
git commit -m "test: validate HelPhysics generated C++"
```

The local commit contains only the HelEngine gate. Any proven generator fix has its own preceding commit in the `csharpcodegen` repository.

## Task 13: Add a repeatable Windows comparison benchmark

**Files:**

- Create: `engine/helengine.helphysics.tests/benchmark/HelPhysicsBoxStackBenchmarkTests.cs`
- Create: `engine/helengine.helphysics.tests/benchmark/HelPhysicsBenchmarkSample3D.cs`
- Create: `engine/helengine.helphysics.tests/benchmark/HelPhysicsBenchmarkReport3D.cs`
- Create: `engine/helengine.helphysics.tests/benchmark/HelPhysicsBenchmarkRunner3D.cs`
- Modify: `engine/helengine.helphysics.tests/helengine.helphysics.tests.csproj`

- [ ] **Step 1: Write benchmark-runner contract tests**

Test that a short four-box run reports the requested sample count, positive median/P95/maximum timing, body/contact/manifold counters, and zero HelPhysics steady-state allocations. Test percentile selection with a fixed known sample array.

- [ ] **Step 2: Verify benchmark tests fail**

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj --filter FullyQualifiedName~HelPhysicsBoxStackBenchmarkTests
```

Expected: compilation fails because benchmark report types are missing.

- [ ] **Step 3: Implement a low-noise managed benchmark runner**

Add a direct test-project reference to `engine/helengine.bepu/helengine.bepu.csproj`. Benchmark the same four-box stack at 20 Hz in managed HelPhysics and managed BEPU. Preallocate `long[]` samples, warm both paths, force collection before measurement, use `Stopwatch.GetTimestamp()`, and compute median, P95, maximum, allocated bytes, and final physics counters after measurement.

The test records both engines for orientation but does not fail on their elapsed-time ratio. The eventual native generated-C++ benchmark supplies the accepted two-times-Windows-performance gate; managed results are not a proxy for PS2 performance.

- [ ] **Step 4: Run the benchmark contract tests**

Run the command from Step 2.

Expected: report contracts pass and HelPhysics reports zero steady-state allocations.

- [ ] **Step 5: Commit the benchmark harness**

```powershell
git add engine/helengine.helphysics.tests/benchmark engine/helengine.helphysics.tests/helengine.helphysics.tests.csproj
git commit -m "test: benchmark HelPhysics box stack"
```

## Final verification

- [ ] Run the complete HelPhysics suite:

```powershell
dotnet test engine\helengine.helphysics.tests\helengine.helphysics.tests.csproj
```

- [ ] Run the existing runtime-registration regression slice:

```powershell
dotnet test engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "FullyQualifiedName~GeneratedRuntimeModuleManifestTests|FullyQualifiedName~PhysicsWorld3DSceneLoadTests"
```

- [ ] Compile the generated C++:

```powershell
dotnet publish C:\dev\helworks\csharpcodegen\codegen\codegen.csproj -c Release -o .validation\tools\codegen
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate-helphysics-generated-cpp.ps1 -CodegenPath .validation\tools\codegen\codegen.exe
```

- [ ] Inspect repository hygiene:

```powershell
git diff --check
git status --short
```

Expected final state: BEPU is still the default runtime, the HelPhysics four-box stack is deterministic and asleep after the 20 Hz settling run, steady-state stepping allocates zero managed bytes, and generated C++ compiles successfully with MSVC.

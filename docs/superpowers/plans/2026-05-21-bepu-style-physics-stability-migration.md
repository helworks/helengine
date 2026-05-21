# BEPU-Style Physics Stability Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `helengine.physics3d` toward a BEPUphysics2-inspired architecture in small, testable steps that improve stability, determinism, and performance without importing BEPU as a dependency.

**Architecture:** Keep the public component API stable, but introduce internal runtime concepts that mirror BEPU's proven separation: shape storage, body/static handles, body activity, timestep configuration, contact manifolds, and solver substeps. Each task should preserve current behavior before changing algorithms, then add one stability improvement with focused regressions.

**Tech Stack:** C#/.NET 9, xUnit, `helengine.physics3d`, local reference at `C:\dev\helworks\reference\physics\bepuphysics2`.

---

## Reference Notes

BEPU concepts to borrow:
- `Simulation` owns `Bodies`, `Statics`, `Shapes`, `BroadPhase`, `NarrowPhase`, `Solver`, `PoseIntegrator`, `IslandSleeper`.
- `BodyDescription` decomposes authoring data into pose, velocity, inertia, collidable, and activity settings.
- Shapes are allocated independently and reused by many bodies.
- `DefaultTimestepper` order is sleep, predict bounds, collision detection, solve, optimize.
- Stability guidance prefers fixed timesteps, solver substeps, cached contact/constraint solutions, simple shapes, and activity-based sleeping after multiple low-motion steps.
- `CollidableDescription` includes speculative margins and CCD mode. We should add the data model first, then use it in narrow cases.

Current helengine issues to address incrementally:
- `PhysicsWorld3D` owns too many stages directly.
- `BodyState3D` mixes authoring sync, shape data, pose, velocity, inertia, contact flags, and sleep hints.
- Contact response is impulse-based but does not have an explicit contact manifold model.
- Sleep is frame-local rather than activity-count based.
- Solver iterations exist, but substeps and per-stage timing are not explicit runtime settings.

---

## Task 1: Add Runtime Handle Types

**Files:**
- Create: `engine/helengine.physics3d/runtime/PhysicsBodyHandle3D.cs`
- Create: `engine/helengine.physics3d/runtime/PhysicsShapeHandle3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DRuntimeHandleTests.cs`

- [ ] **Step 1: Write failing handle tests**

Create `engine/helengine.physics3d.tests/PhysicsWorld3DRuntimeHandleTests.cs`:

```csharp
namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies stable runtime handle value behavior used by internal physics stores.
    /// </summary>
    public class PhysicsWorld3DRuntimeHandleTests {
        /// <summary>
        /// Ensures body handles expose their numeric value and reject negative identifiers.
        /// </summary>
        [Fact]
        public void PhysicsBodyHandle3D_WithNegativeValue_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsBodyHandle3D(-1));
        }

        /// <summary>
        /// Ensures shape handles expose their numeric value and reject negative identifiers.
        /// </summary>
        [Fact]
        public void PhysicsShapeHandle3D_WithNegativeValue_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicsShapeHandle3D(-1));
        }

        /// <summary>
        /// Ensures valid body and shape handles preserve their assigned identifiers.
        /// </summary>
        [Fact]
        public void RuntimeHandles_WithValidValues_PreserveValues() {
            PhysicsBodyHandle3D bodyHandle = new PhysicsBodyHandle3D(7);
            PhysicsShapeHandle3D shapeHandle = new PhysicsShapeHandle3D(3);

            Assert.Equal(7, bodyHandle.Value);
            Assert.Equal(3, shapeHandle.Value);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PhysicsWorld3DRuntimeHandleTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: compile fails because `PhysicsBodyHandle3D` and `PhysicsShapeHandle3D` do not exist.

- [ ] **Step 3: Implement handle types**

Create `engine/helengine.physics3d/runtime/PhysicsBodyHandle3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Identifies one body entry inside the runtime physics body store.
    /// </summary>
    public readonly struct PhysicsBodyHandle3D {
        /// <summary>
        /// Initializes a new runtime body handle.
        /// </summary>
        /// <param name="value">Non-negative body slot identifier.</param>
        public PhysicsBodyHandle3D(int value) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(nameof(value), "Body handle values must be non-negative.");
            }

            Value = value;
        }

        /// <summary>
        /// Gets the non-negative body slot identifier.
        /// </summary>
        public int Value { get; }
    }
}
```

Create `engine/helengine.physics3d/runtime/PhysicsShapeHandle3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Identifies one reusable collider shape entry inside the runtime physics shape store.
    /// </summary>
    public readonly struct PhysicsShapeHandle3D {
        /// <summary>
        /// Initializes a new runtime shape handle.
        /// </summary>
        /// <param name="value">Non-negative shape slot identifier.</param>
        public PhysicsShapeHandle3D(int value) {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(nameof(value), "Shape handle values must be non-negative.");
            }

            Value = value;
        }

        /// <summary>
        /// Gets the non-negative shape slot identifier.
        /// </summary>
        public int Value { get; }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run the same `PhysicsWorld3DRuntimeHandleTests` command.

Expected: `Passed: 3`.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine/helengine.physics3d/runtime/PhysicsBodyHandle3D.cs engine/helengine.physics3d/runtime/PhysicsShapeHandle3D.cs engine/helengine.physics3d.tests/PhysicsWorld3DRuntimeHandleTests.cs
rtk git commit -m "feat: add physics runtime handles"
```

---

## Task 2: Split Reusable Shapes From Body State

**Files:**
- Create: `engine/helengine.physics3d/runtime/PhysicsShapeKind3D.cs`
- Create: `engine/helengine.physics3d/runtime/PhysicsShape3D.cs`
- Create: `engine/helengine.physics3d/runtime/PhysicsShapeStore3D.cs`
- Modify: `engine/helengine.physics3d/runtime/BodyState3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsShapeStore3DTests.cs`

- [ ] **Step 1: Write failing shape-store tests**

Create `engine/helengine.physics3d.tests/PhysicsShapeStore3DTests.cs`:

```csharp
namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies reusable runtime collider shape storage.
    /// </summary>
    public class PhysicsShapeStore3DTests {
        /// <summary>
        /// Ensures identical box shapes share one runtime shape handle.
        /// </summary>
        [Fact]
        public void GetOrAddBox_WithSameSize_ReusesShapeHandle() {
            PhysicsShapeStore3D store = new PhysicsShapeStore3D();

            PhysicsShapeHandle3D first = store.GetOrAddBox(new float3(1f, 1f, 1f));
            PhysicsShapeHandle3D second = store.GetOrAddBox(new float3(1f, 1f, 1f));

            Assert.Equal(first.Value, second.Value);
            Assert.Equal(1, store.Count);
        }

        /// <summary>
        /// Ensures different box shapes receive different runtime shape handles.
        /// </summary>
        [Fact]
        public void GetOrAddBox_WithDifferentSizes_AddsDistinctShapes() {
            PhysicsShapeStore3D store = new PhysicsShapeStore3D();

            PhysicsShapeHandle3D first = store.GetOrAddBox(new float3(1f, 1f, 1f));
            PhysicsShapeHandle3D second = store.GetOrAddBox(new float3(2f, 1f, 1f));

            Assert.NotEqual(first.Value, second.Value);
            Assert.Equal(2, store.Count);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PhysicsShapeStore3DTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: compile fails because shape-store types do not exist.

- [ ] **Step 3: Implement shape store**

Create `engine/helengine.physics3d/runtime/PhysicsShapeKind3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Identifies the primitive shape stored in one reusable physics shape record.
    /// </summary>
    public enum PhysicsShapeKind3D {
        /// <summary>
        /// Axis-aligned or oriented box collider shape.
        /// </summary>
        Box,

        /// <summary>
        /// Sphere collider shape.
        /// </summary>
        Sphere,

        /// <summary>
        /// Vertical capsule collider shape.
        /// </summary>
        Capsule
    }
}
```

Create `engine/helengine.physics3d/runtime/PhysicsShape3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores reusable collider geometry data that can be shared by many runtime bodies.
    /// </summary>
    public class PhysicsShape3D {
        /// <summary>
        /// Initializes a new reusable shape record.
        /// </summary>
        /// <param name="kind">Primitive shape kind.</param>
        /// <param name="size">Full box size or capsule diameter/height data depending on shape kind.</param>
        /// <param name="radius">Sphere or capsule radius.</param>
        public PhysicsShape3D(PhysicsShapeKind3D kind, float3 size, float radius) {
            Kind = kind;
            Size = size;
            Radius = radius;
        }

        /// <summary>
        /// Gets the primitive shape kind.
        /// </summary>
        public PhysicsShapeKind3D Kind { get; }

        /// <summary>
        /// Gets full box size or capsule size data depending on shape kind.
        /// </summary>
        public float3 Size { get; }

        /// <summary>
        /// Gets sphere or capsule radius.
        /// </summary>
        public float Radius { get; }
    }
}
```

Create `engine/helengine.physics3d/runtime/PhysicsShapeStore3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Owns reusable runtime collider shapes and returns stable handles for body records.
    /// </summary>
    public class PhysicsShapeStore3D {
        /// <summary>
        /// Stored reusable shape records.
        /// </summary>
        readonly List<PhysicsShape3D> Shapes;

        /// <summary>
        /// Initializes an empty shape store.
        /// </summary>
        public PhysicsShapeStore3D() {
            Shapes = new List<PhysicsShape3D>();
        }

        /// <summary>
        /// Gets the number of stored reusable shapes.
        /// </summary>
        public int Count {
            get { return Shapes.Count; }
        }

        /// <summary>
        /// Gets or adds one box shape with the supplied full size.
        /// </summary>
        /// <param name="size">Full box size.</param>
        /// <returns>Handle to the reusable box shape.</returns>
        public PhysicsShapeHandle3D GetOrAddBox(float3 size) {
            for (int index = 0; index < Shapes.Count; index++) {
                PhysicsShape3D shape = Shapes[index];
                if (shape.Kind == PhysicsShapeKind3D.Box && shape.Size == size) {
                    return new PhysicsShapeHandle3D(index);
                }
            }

            Shapes.Add(new PhysicsShape3D(PhysicsShapeKind3D.Box, size, 0f));
            return new PhysicsShapeHandle3D(Shapes.Count - 1);
        }

        /// <summary>
        /// Gets one shape by handle.
        /// </summary>
        /// <param name="handle">Runtime shape handle.</param>
        /// <returns>Stored reusable shape.</returns>
        public PhysicsShape3D GetShape(PhysicsShapeHandle3D handle) {
            return Shapes[handle.Value];
        }
    }
}
```

- [ ] **Step 4: Add optional body shape handle**

Modify `engine/helengine.physics3d/runtime/BodyState3D.cs` by adding this property near collider properties:

```csharp
/// <summary>
/// Gets or sets the reusable runtime shape handle assigned to this body.
/// </summary>
public PhysicsShapeHandle3D ShapeHandle { get; set; }
```

Do not remove existing `BoxCollider`, `SphereCollider`, or `CapsuleCollider` usage in this task.

- [ ] **Step 5: Run shape-store tests**

Run the same `PhysicsShapeStore3DTests` command.

Expected: `Passed: 2`.

- [ ] **Step 6: Run physics suite**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName!~Register_AttachesDefaultWorld' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: all current physics tests pass.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.physics3d/runtime/PhysicsShapeKind3D.cs engine/helengine.physics3d/runtime/PhysicsShape3D.cs engine/helengine.physics3d/runtime/PhysicsShapeStore3D.cs engine/helengine.physics3d/runtime/BodyState3D.cs engine/helengine.physics3d.tests/PhysicsShapeStore3DTests.cs
rtk git commit -m "feat: add reusable physics shape store"
```

---

## Task 3: Add Activity-Based Sleeping

**Files:**
- Create: `engine/helengine.physics3d/runtime/BodyActivity3D.cs`
- Modify: `engine/helengine.physics3d/runtime/BodyState3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Add failing stable-sleep regression**

Add this test to `PhysicsWorld3DDynamicsTests.cs` near existing sleep/damping tests:

```csharp
/// <summary>
/// Ensures a low-motion body must remain quiet for multiple contact steps before contact sleep zeroes angular velocity.
/// </summary>
[Fact]
public void Step_WithBriefLowAngularVelocityContact_DoesNotSleepImmediately() {
    Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
    groundEntity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static,
        UseGravity = false
    });
    groundEntity.AddComponent(new BoxCollider3DComponent {
        Size = new float3(8f, 1f, 8f)
    });

    RigidBody3DComponent dynamicBody = new RigidBody3DComponent {
        BodyKind = BodyKind3D.Dynamic,
        UseGravity = false,
        Mass = 1d,
        LinearVelocity = new float3(0f, -0.2f, 0f),
        AngularVelocity = new float3(0.02f, 0f, 0f)
    };
    Entity dynamicEntity = CreateDynamicBoxEntity(new float3(0f, 0.51f, 0f), dynamicBody);

    PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
    world.BindScene(new[] {
        groundEntity,
        dynamicEntity
    });

    world.Step(1.0 / 60.0);

    Assert.NotEqual(float3.Zero, dynamicBody.AngularVelocity);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~Step_WithBriefLowAngularVelocityContact_DoesNotSleepImmediately' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: fails because current sleep is frame-local and can zero angular velocity immediately.

- [ ] **Step 3: Implement `BodyActivity3D`**

Create `engine/helengine.physics3d/runtime/BodyActivity3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Tracks low-motion contact history before a body is allowed to sleep.
    /// </summary>
    public class BodyActivity3D {
        /// <summary>
        /// Initializes a new activity tracker.
        /// </summary>
        public BodyActivity3D() {
            LowMotionStepCount = 0;
            RequiredLowMotionStepCount = 16;
        }

        /// <summary>
        /// Gets or sets the number of consecutive low-motion contact steps observed.
        /// </summary>
        public int LowMotionStepCount { get; set; }

        /// <summary>
        /// Gets the required consecutive low-motion contact steps before sleep can occur.
        /// </summary>
        public int RequiredLowMotionStepCount { get; }

        /// <summary>
        /// Records whether the current contact step is low motion.
        /// </summary>
        /// <param name="isLowMotion">True when linear and angular motion are both under contact sleep thresholds.</param>
        public void RecordContactMotion(bool isLowMotion) {
            if (isLowMotion) {
                LowMotionStepCount = LowMotionStepCount + 1;
            } else {
                LowMotionStepCount = 0;
            }
        }

        /// <summary>
        /// Gets whether enough low-motion contact steps have accumulated for sleep.
        /// </summary>
        public bool CanSleep {
            get { return LowMotionStepCount >= RequiredLowMotionStepCount; }
        }
    }
}
```

- [ ] **Step 4: Attach activity to body state**

Modify every `BodyState3D` constructor to assign:

```csharp
Activity = new BodyActivity3D();
```

Add this property:

```csharp
/// <summary>
/// Gets the low-motion activity tracker used to decide when contact sleep is allowed.
/// </summary>
public BodyActivity3D Activity { get; }
```

- [ ] **Step 5: Gate contact sleep on activity**

In `PhysicsWorld3D.ApplyContactSleep`, after computing `linearCanSleep` and `angularCanSleep`, add:

```csharp
bool isLowMotionContact = linearCanSleep && angularCanSleep;
bodyState.Activity.RecordContactMotion(isLowMotionContact);
bool activityCanSleep = bodyState.Activity.CanSleep;
```

Require `activityCanSleep` in both sleep branches:

```csharp
if (linearCanSleep && angularCanSleep && activityCanSleep && contactCanAngularSleep && StabilizeRestingBoxOrientation(bodyState)) {
    bodyState.AngularVelocity = float3.Zero;
    return;
}
if (angularCanSleep && activityCanSleep && contactCanAngularSleep && isRestingUprightCandidate) {
    bodyState.AngularVelocity = float3.Zero;
}
```

- [ ] **Step 6: Run new test and existing suite**

Run the new test command. Expected: pass.

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName!~Register_AttachesDefaultWorld' 2>&1 | Select-Object -Last 180 | Out-String -Width 260 | Write-Output"
```

Expected: all current physics tests pass. If a previous test expected one-frame sleep, update that test to assert sleep after 16 contact steps.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.physics3d/runtime/BodyActivity3D.cs engine/helengine.physics3d/runtime/BodyState3D.cs engine/helengine.physics3d/PhysicsWorld3D.cs engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs
rtk git commit -m "feat: add activity-based physics sleep"
```

---

## Task 4: Introduce Explicit Contact Manifolds

**Files:**
- Create: `engine/helengine.physics3d/collision/ContactPoint3D.cs`
- Create: `engine/helengine.physics3d/collision/ContactManifold3D.cs`
- Modify: `engine/helengine.physics3d/collision/BoxBoxContactResolver3D.cs`
- Modify: `engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Test: `engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs`

- [ ] **Step 1: Write failing box-box manifold test**

Add to `PrimitiveContactResolver3DTests.cs`:

```csharp
/// <summary>
/// Ensures upright box-box contact reports the center of the horizontal overlap patch.
/// </summary>
[Fact]
public void TryResolveBoxBoxManifold_WithEdgeOverlap_ReportsOverlapPatchCenter() {
    BodyState3D first = CreateDynamicBoxBody(new float3(0.50f, 1.5f, 0.06f));
    BodyState3D second = CreateStaticBoxBody(new float3(-0.34f, 0.5f, -0.06f));

    bool resolved = BoxBoxContactResolver3D.TryResolveManifold(first, second, out ContactManifold3D manifold);

    Assert.True(resolved);
    Assert.Equal(1, manifold.ContactCount);
    Assert.InRange(manifold.Point0.Position.X, 0.00f, 0.10f);
    Assert.InRange(manifold.Point0.Position.Y, 1.00f, 1.02f);
    Assert.InRange(manifold.Point0.Position.Z, -0.20f, 0.20f);
}
```

If `PrimitiveContactResolver3DTests.cs` does not already have body factory helpers, add class-level private methods with XML comments using `Entity`, `RigidBody3DComponent`, and `BoxCollider3DComponent` exactly like `PhysicsWorld3DDynamicsTests.CreateDynamicBoxEntity`.

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~TryResolveBoxBoxManifold_WithEdgeOverlap_ReportsOverlapPatchCenter' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: compile fails because manifold types and `TryResolveManifold` do not exist.

- [ ] **Step 3: Add manifold data types**

Create `ContactPoint3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores one resolved world-space contact point and penetration depth.
    /// </summary>
    public class ContactPoint3D {
        /// <summary>
        /// Initializes one contact point.
        /// </summary>
        /// <param name="position">World-space contact position.</param>
        /// <param name="penetration">Positive penetration depth along the manifold normal.</param>
        public ContactPoint3D(float3 position, float penetration) {
            Position = position;
            Penetration = penetration;
        }

        /// <summary>
        /// Gets the world-space contact position.
        /// </summary>
        public float3 Position { get; }

        /// <summary>
        /// Gets the positive penetration depth along the manifold normal.
        /// </summary>
        public float Penetration { get; }
    }
}
```

Create `ContactManifold3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Stores contact information produced by narrow phase for one colliding body pair.
    /// </summary>
    public class ContactManifold3D {
        /// <summary>
        /// Initializes one single-point contact manifold.
        /// </summary>
        /// <param name="normal">Unit normal pointing from the second body toward the first body.</param>
        /// <param name="point0">Primary contact point.</param>
        public ContactManifold3D(float3 normal, ContactPoint3D point0) {
            Normal = normal;
            Point0 = point0 ?? throw new ArgumentNullException(nameof(point0));
            ContactCount = 1;
        }

        /// <summary>
        /// Gets the unit normal pointing from the second body toward the first body.
        /// </summary>
        public float3 Normal { get; }

        /// <summary>
        /// Gets the primary contact point.
        /// </summary>
        public ContactPoint3D Point0 { get; }

        /// <summary>
        /// Gets the number of valid contact points in this manifold.
        /// </summary>
        public int ContactCount { get; }
    }
}
```

- [ ] **Step 4: Implement box-box manifold without replacing old path**

Add `TryResolveManifold` to `BoxBoxContactResolver3D` while keeping `TryResolveContact`:

```csharp
/// <summary>
/// Finds one single-point contact manifold for an overlapping box pair.
/// </summary>
/// <param name="first">First box body state.</param>
/// <param name="second">Second box body state.</param>
/// <param name="manifold">Resolved contact manifold when boxes overlap.</param>
/// <returns>True when the boxes overlap.</returns>
public static bool TryResolveManifold(BodyState3D first, BodyState3D second, out ContactManifold3D manifold) {
    if (!TryResolveContact(first, second, out float penetration, out int axisIndex)) {
        manifold = null;
        return false;
    }

    float axisDirection = PrimitiveContactMath3D.GetAxisDirection(first, second, axisIndex);
    float3 normal = CreateAxisNormal(axisIndex, axisDirection);
    float3 position = ResolveOverlapPatchCenter(first, second, normal, axisIndex);
    manifold = new ContactManifold3D(normal, new ContactPoint3D(position, penetration));
    return true;
}
```

Because local helper functions are disallowed, add private static methods on `BoxBoxContactResolver3D` for `CreateAxisNormal` and `ResolveOverlapPatchCenter`.

- [ ] **Step 5: Use manifold in `PhysicsWorld3D` for box-box only**

In the box-box branch of `ResolveBodyPair`, call `TryResolveManifold`. Continue to call the existing `ResolveAxis` for other primitive paths until their manifolds are migrated.

Use `ContactMaterialResponse3D.ApplyManifoldResponse(first, second, manifold)` and implement that method as a wrapper around existing normal/friction response using `manifold.Normal` and `manifold.Point0.Position`.

- [ ] **Step 6: Run tests**

Run the new manifold test, the edge tipping test, and the full filtered physics suite.

Expected: all pass.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.physics3d/collision/ContactPoint3D.cs engine/helengine.physics3d/collision/ContactManifold3D.cs engine/helengine.physics3d/collision/BoxBoxContactResolver3D.cs engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs engine/helengine.physics3d/PhysicsWorld3D.cs engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs
rtk git commit -m "feat: add box contact manifolds"
```

---

## Task 5: Add Solver Substeps to Settings

**Files:**
- Modify: `engine/helengine.physics3d/PhysicsWorld3DProfile.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3DSettings.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DProfileTests.cs`
- Test: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Add failing profile/settings tests**

Add to `PhysicsWorld3DProfileTests.cs`:

```csharp
/// <summary>
/// Ensures the medium profile exposes a stable default solver substep count.
/// </summary>
[Fact]
public void CreateMedium_DefaultsToOneSolverSubstep() {
    PhysicsWorld3DProfile profile = PhysicsWorld3DProfile.CreateMedium();

    Assert.Equal(1, profile.SolverSubsteps);
}
```

- [ ] **Step 2: Add settings properties**

Add `solverSubsteps` constructor parameter to `PhysicsWorld3DProfile` and `PhysicsWorld3DSettings`, validate it is greater than zero, and expose:

```csharp
/// <summary>
/// Gets the default solver substeps executed inside each fixed world step.
/// </summary>
public int SolverSubsteps { get; }
```

Set medium default to `1`.

- [ ] **Step 3: Run profile tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PhysicsWorld3DProfileTests' 2>&1 | Select-Object -Last 140 | Out-String -Width 260 | Write-Output"
```

Expected: pass.

- [ ] **Step 4: Implement substep loop**

In `PhysicsWorld3D.Step`, keep broadphase/contact discovery once per external step for now. Inside dynamic integration and contact solve, run `Settings.SolverSubsteps` passes with `stepSeconds / Settings.SolverSubsteps`.

The first migration should be conservative:

```csharp
double substepSeconds = stepSeconds / Settings.SolverSubsteps;
for (int substepIndex = 0; substepIndex < Settings.SolverSubsteps; substepIndex++) {
    IntegrateDynamicBodies(substepSeconds);
    ResolveContacts();
    ApplyDynamicDamping(substepSeconds);
}
```

If current `Step` calls `ResolveContacts` before/after integration differently, preserve the existing order inside the loop and do not change collision algorithms in this task.

- [ ] **Step 5: Add substep stability comparison test**

Add a test that creates two worlds with the same edge-supported setup, one with `SolverSubsteps = 1` and one with `SolverSubsteps = 4`, then asserts the substepped world does not produce a larger penetration after 120 frames. Use exact positions from `Step_WithEdgeSupportedBox_StartsTippingAroundTheSupportEdge`.

- [ ] **Step 6: Run physics suite**

Run the full filtered physics test command.

Expected: all pass.

- [ ] **Step 7: Commit**

```powershell
rtk git add engine/helengine.physics3d/PhysicsWorld3DProfile.cs engine/helengine.physics3d/PhysicsWorld3DSettings.cs engine/helengine.physics3d/PhysicsWorld3D.cs engine/helengine.physics3d.tests/PhysicsWorld3DProfileTests.cs engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs
rtk git commit -m "feat: add physics solver substeps"
```

---

## Task 6: Create a Narrow Phase Boundary

**Files:**
- Create: `engine/helengine.physics3d/collision/PhysicsNarrowPhase3D.cs`
- Modify: `engine/helengine.physics3d/PhysicsWorld3D.cs`
- Test: `engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs`

- [ ] **Step 1: Create failing narrow-phase API test**

Add to `PrimitiveContactResolver3DTests.cs`:

```csharp
/// <summary>
/// Ensures the narrow phase can produce a box-box manifold without stepping a full world.
/// </summary>
[Fact]
public void TryCreateManifold_WithBoxPair_ReturnsContactManifold() {
    BodyState3D first = CreateDynamicBoxBody(new float3(0f, 1.5f, 0f));
    BodyState3D second = CreateStaticBoxBody(new float3(0f, 0.5f, 0f));
    PhysicsNarrowPhase3D narrowPhase = new PhysicsNarrowPhase3D();

    bool resolved = narrowPhase.TryCreateManifold(first, second, out ContactManifold3D manifold);

    Assert.True(resolved);
    Assert.Equal(1, manifold.ContactCount);
}
```

- [ ] **Step 2: Implement narrow phase facade**

Create `PhysicsNarrowPhase3D.cs`:

```csharp
namespace helengine {
    /// <summary>
    /// Routes body-pair collider combinations to narrow-phase contact manifold generators.
    /// </summary>
    public class PhysicsNarrowPhase3D {
        /// <summary>
        /// Attempts to create a contact manifold for one body pair.
        /// </summary>
        /// <param name="first">First body state.</param>
        /// <param name="second">Second body state.</param>
        /// <param name="manifold">Resolved manifold when the pair overlaps.</param>
        /// <returns>True when the pair overlaps and has a supported manifold.</returns>
        public bool TryCreateManifold(BodyState3D first, BodyState3D second, out ContactManifold3D manifold) {
            if (first == null) {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null) {
                throw new ArgumentNullException(nameof(second));
            }

            if (first.ColliderShapeKind == ColliderShapeKind3D.Box && second.ColliderShapeKind == ColliderShapeKind3D.Box) {
                return BoxBoxContactResolver3D.TryResolveManifold(first, second, out manifold);
            }

            manifold = null;
            return false;
        }
    }
}
```

- [ ] **Step 3: Use narrow phase in world box-box path**

Add a `readonly PhysicsNarrowPhase3D NarrowPhase;` field to `PhysicsWorld3D`, initialize it in the constructor, and route box-box body pairs through it. Keep old direct resolver calls for unsupported shape combinations.

- [ ] **Step 4: Run narrow-phase and full physics tests**

Run targeted and full filtered commands.

Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
rtk git add engine/helengine.physics3d/collision/PhysicsNarrowPhase3D.cs engine/helengine.physics3d/PhysicsWorld3D.cs engine/helengine.physics3d.tests/PrimitiveContactResolver3DTests.cs
rtk git commit -m "refactor: introduce physics narrow phase"
```

---

## Task 7: Add Deterministic Benchmark Scenarios

**Files:**
- Create: `engine/helengine.physics3d.tests/PhysicsWorld3DStabilityScenarioTests.cs`

- [ ] **Step 1: Add deterministic stability tests**

Create `PhysicsWorld3DStabilityScenarioTests.cs` with:

```csharp
namespace helengine.physics3d.tests {
    /// <summary>
    /// Runs compact deterministic physics scenarios that guard against stability regressions.
    /// </summary>
    public class PhysicsWorld3DStabilityScenarioTests {
        /// <summary>
        /// Ensures an edge-supported cube changes orientation within three seconds.
        /// </summary>
        [Fact]
        public void EdgeSupportedCube_ChangesOrientationWithinThreeSeconds() {
            PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
            RigidBody3DComponent body = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Dynamic,
                UseGravity = true,
                Mass = 1d
            };
            Entity support = CreateBox(new float3(-0.34f, 0.5f, -0.06f), BodyKind3D.Static);
            Entity dynamic = CreateBox(new float3(0.50f, 1.62f, 0.06f), body);
            world.BindScene(new[] { support, dynamic });

            for (int index = 0; index < 180; index++) {
                world.Step(1.0 / 60.0);
            }

            float3 up = float4.RotateVector(new float3(0f, 1f, 0f), dynamic.LocalOrientation);
            Assert.True(up.Y < 0.98f);
        }

        /// <summary>
        /// Creates a box entity with a new rigid body of the requested kind.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <param name="bodyKind">Rigid body kind.</param>
        /// <returns>Initialized box entity.</returns>
        static Entity CreateBox(float3 position, BodyKind3D bodyKind) {
            RigidBody3DComponent body = new RigidBody3DComponent {
                BodyKind = bodyKind,
                UseGravity = bodyKind == BodyKind3D.Dynamic,
                Mass = 1d
            };
            return CreateBox(position, body);
        }

        /// <summary>
        /// Creates a box entity with an existing rigid body component.
        /// </summary>
        /// <param name="position">Initial local position.</param>
        /// <param name="body">Rigid body component.</param>
        /// <returns>Initialized box entity.</returns>
        static Entity CreateBox(float3 position, RigidBody3DComponent body) {
            Entity entity = new Entity {
                LocalPosition = position,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            entity.InitComponents();
            entity.InitChildren();
            entity.AddComponent(body);
            entity.AddComponent(new BoxCollider3DComponent {
                Size = new float3(1f, 1f, 1f)
            });
            return entity;
        }
    }
}
```

- [ ] **Step 2: Run stability scenario tests**

Run:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName~PhysicsWorld3DStabilityScenarioTests' 2>&1 | Select-Object -Last 120 | Out-String -Width 260 | Write-Output"
```

Expected: pass.

- [ ] **Step 3: Commit**

```powershell
rtk git add engine/helengine.physics3d.tests/PhysicsWorld3DStabilityScenarioTests.cs
rtk git commit -m "test: add physics stability scenarios"
```

---

## Task 8: Document the Internal Physics Direction

**Files:**
- Create: `docs/physics3d/bepu-style-runtime-direction.md`

- [ ] **Step 1: Add design document**

Create `docs/physics3d/bepu-style-runtime-direction.md`:

```markdown
# BEPU-Style Physics Runtime Direction

The engine does not vendor BEPUphysics2 directly. The physics runtime will instead borrow stable internal ideas from `C:\dev\helworks\reference\physics\bepuphysics2` while keeping helengine component serialization and runtime feature stripping.

## Adopted Ideas

- Runtime handles identify bodies and shapes.
- Shapes are stored independently from bodies and can be reused.
- World stepping is split into clear stages: synchronize, sleep, broadphase, narrow phase, solve, integrate, synchronize back.
- Contact generation produces manifolds rather than implicit single-point response.
- Sleep uses activity history, not one-frame velocity checks.
- Stability tuning uses fixed timestep, solver iterations, and substeps.

## Non-Goals

- Do not import BEPU as a runtime dependency.
- Do not expose BEPU-specific APIs in public helengine components.
- Do not rewrite every collider pair in one change.
- Do not introduce unsafe/pinned-memory buffer pools until the simpler object-backed architecture is stable.

## Migration Order

1. Runtime handles.
2. Shape store.
3. Activity-based sleeping.
4. Box-box contact manifold.
5. Solver substeps.
6. Narrow phase boundary.
7. Stability scenario tests.
8. Optional packed storage after behavior is stable.
```

- [ ] **Step 2: Commit**

```powershell
rtk git add docs/physics3d/bepu-style-runtime-direction.md
rtk git commit -m "docs: describe physics runtime direction"
```

---

## Verification Checklist

Run before calling the migration branch complete:

```powershell
rtk proxy powershell -NoProfile -Command "dotnet test 'engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj' --filter 'FullyQualifiedName!~Register_AttachesDefaultWorld' 2>&1 | Select-Object -Last 220 | Out-String -Width 260 | Write-Output"
```

Expected: all physics tests pass.

Run a city Windows build only after the physics tests pass:

```powershell
rtk proxy powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --build windows --output 'C:\dev\helprojs\output\windows-physics-character-slope' 2>&1 | Select-Object -Last 260 | Out-String -Width 260 | Write-Output"
```

Expected: build completes and the edge-supported green cube visibly rotates/falls around the blue cube edge.

---

## Self-Review

Spec coverage:
- Uses BEPUphysics2 reference ideas without direct dependency.
- Breaks migration into small, testable steps.
- Preserves current helengine serialization and component API.
- Prioritizes stability format before packed/SIMD performance work.

Placeholder scan:
- No task depends on unspecified future code.
- Each code-bearing task includes concrete types, paths, and commands.

Type consistency:
- Handle names are `PhysicsBodyHandle3D` and `PhysicsShapeHandle3D`.
- Shape store names are `PhysicsShapeStore3D`, `PhysicsShape3D`, and `PhysicsShapeKind3D`.
- Manifold names are `ContactPoint3D` and `ContactManifold3D`.

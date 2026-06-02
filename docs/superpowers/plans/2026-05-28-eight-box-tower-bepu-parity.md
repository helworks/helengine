# Eight-Box Tower BEPU Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Helengine's dynamic box-box tower behavior materially closer to the BEPU reference by fixing persistent contact response, using the city eight-box tower as the primary parity gate.

**Architecture:** Keep the current SAT/manifold detection path and improve persistent contact response in two places: warm-started normal impulse retention across manifold changes, and manifold-level friction/twist response based on active support contacts. Use the eight-box tower as the primary regression gate, with the four-box showcase and tilted-box damping tests as non-regression guards.

**Tech Stack:** C#/.NET 9, xUnit, `helengine.physics3d`, `helengine.physics3d.tests`, local BEPU comparison harness under `artifacts/physics-comparison`.

---

## Files And Responsibilities

- `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`
  - Owns world-level tower parity regressions and non-regression guards.
- `engine/helengine.physics3d.tests/BoxBoxContactConstraint3DTests.cs`
  - New focused constraint-level tests for warm-start redistribution behavior.
- `engine/helengine.physics3d/collision/BoxBoxContactConstraint3D.cs`
  - Owns cached normal/tangent/twist impulses and manifold matching logic.
- `engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs`
  - Owns persistent normal, tangent friction, and twist friction solves.
- `artifacts/physics-comparison/PhysicsComparisonRunner.cs`
  - Existing comparison harness entry point used for BEPU-vs-Helengine tower validation.
- `artifacts/physics-comparison/output/comparison-summary.txt`
  - Existing summary artifact used as the baseline and after-change parity check.

---

### Task 1: Add The Next Eight-Box Tower Parity Regression

**Files:**
- Modify: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Add a failing eight-box tower residual-spin regression**

Add this test beside the existing eight-box tower tests:

```csharp
/// <summary>
/// Ensures the upper half of the authored eight-box tower settles with BEPU-like low residual spin.
/// </summary>
[Fact]
public void Step_WithCityEightBoxTower_SettlesUpperBoxesWithoutResidualSpin() {
    Entity groundEntity = CreateEntity(new float3(0f, -0.5f, 0f));
    groundEntity.AddComponent(new RigidBody3DComponent {
        BodyKind = BodyKind3D.Static,
        UseGravity = false
    });
    groundEntity.AddComponent(new BoxCollider3DComponent {
        Size = new float3(18f, 1f, 18f),
        StaticFriction = 1d,
        DynamicFriction = 1d
    });

    RigidBody3DComponent[] bodies = new[] {
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody(),
        CreateDynamicBody()
    };
    Entity[] boxes = new[] {
        CreateDynamicBoxEntity(new float3(0f, 1f, 0f), bodies[0]),
        CreateDynamicBoxEntity(new float3(0.9f, 3f, 0f), bodies[1]),
        CreateDynamicBoxEntity(new float3(-0.45f, 5f, 0f), bodies[2]),
        CreateDynamicBoxEntity(new float3(0.45f, 7f, 0f), bodies[3]),
        CreateDynamicBoxEntity(new float3(-0.25f, 9f, 0f), bodies[4]),
        CreateDynamicBoxEntity(new float3(0.25f, 11f, 0f), bodies[5]),
        CreateDynamicBoxEntity(new float3(-0.1f, 13f, 0f), bodies[6]),
        CreateDynamicBoxEntity(new float3(0.1f, 15f, 0f), bodies[7])
    };

    for (int index = 0; index < boxes.Length; index++) {
        SetBoxFriction(boxes[index], 1d, 1d);
    }

    Entity[] rootEntities = new Entity[boxes.Length + 1];
    rootEntities[0] = groundEntity;
    for (int index = 0; index < boxes.Length; index++) {
        rootEntities[index + 1] = boxes[index];
    }

    PhysicsWorld3D world = PhysicsWorld3D.CreateMediumDefault();
    world.BindScene(rootEntities);

    for (int stepIndex = 0; stepIndex < 1200; stepIndex++) {
        world.Step(1.0 / 60.0);
    }

    for (int bodyIndex = 4; bodyIndex < bodies.Length; bodyIndex++) {
        Assert.True(
            ResolveAngularSpeedSquared(bodies[bodyIndex]) <= 0.25d,
            $"Expected upper tower box {bodyIndex + 1} to settle near the BEPU residual-spin range, but angular velocity was {bodies[bodyIndex].AngularVelocity}.");
    }
}
```

- [ ] **Step 2: Run the new test and verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "Step_WithCityEightBoxTower_SettlesUpperBoxesWithoutResidualSpin" -v minimal
```

Expected: `FAIL` with one of the upper boxes still above the `0.25d` residual angular speed squared threshold.

- [ ] **Step 3: Commit the failing regression**

```bash
rtk git -C C:\dev\helworks\helengine add engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "Add eight-box tower residual-spin parity regression"
```

### Task 2: Preserve Support Impulses Across Stable Manifold Changes

**Files:**
- Create: `engine/helengine.physics3d.tests/BoxBoxContactConstraint3DTests.cs`
- Modify: `engine/helengine.physics3d/collision/BoxBoxContactConstraint3D.cs`

- [ ] **Step 1: Add focused failing tests for warm-start redistribution**

Create `engine/helengine.physics3d.tests/BoxBoxContactConstraint3DTests.cs` with these tests:

```csharp
namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies persistent box-box constraints preserve support impulses across BEPU-style manifold updates.
    /// </summary>
    public sealed class BoxBoxContactConstraint3DTests {
        /// <summary>
        /// Ensures aligned manifold updates redistribute cached support instead of zeroing all impulses.
        /// </summary>
        [Fact]
        public void MatchManifold_WithAlignedNormalAndChangedFeatureIds_RedistributesCachedNormalImpulses() {
            Entity firstEntity = new Entity();
            Entity secondEntity = new Entity();
            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(firstEntity, secondEntity) {
                NormalImpulse0 = 4f,
                NormalImpulse1 = 2f,
                TangentImpulse = new float3(0.5f, 0f, 0.25f),
                TwistImpulse = 0.4f,
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11,
                LastNormal = new float3(0f, 1f, 0f),
                HasLastNormal = true
            };
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = new float3(0f, 1f, 0f),
                ContactCount = 4,
                FeatureId0 = 20,
                FeatureId1 = 21,
                FeatureId2 = 22,
                FeatureId3 = 23
            };

            constraint.MatchManifold(manifold);

            Assert.Equal(1.5f, constraint.NormalImpulse0);
            Assert.Equal(1.5f, constraint.NormalImpulse1);
            Assert.Equal(1.5f, constraint.NormalImpulse2);
            Assert.Equal(1.5f, constraint.NormalImpulse3);
            Assert.Equal(new float3(0.5f, 0f, 0.25f), constraint.TangentImpulse);
            Assert.Equal(0.4f, constraint.TwistImpulse);
        }

        /// <summary>
        /// Ensures a materially different normal still clears stale cached impulses.
        /// </summary>
        [Fact]
        public void MatchManifold_WithLargeNormalChange_ResetsCachedImpulses() {
            Entity firstEntity = new Entity();
            Entity secondEntity = new Entity();
            BoxBoxContactConstraint3D constraint = new BoxBoxContactConstraint3D(firstEntity, secondEntity) {
                NormalImpulse0 = 4f,
                NormalImpulse1 = 2f,
                TangentImpulse = new float3(0.5f, 0f, 0.25f),
                TwistImpulse = 0.4f,
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11,
                LastNormal = new float3(0f, 1f, 0f),
                HasLastNormal = true
            };
            BoxBoxContactManifold3D manifold = new BoxBoxContactManifold3D {
                Normal = new float3(1f, 0f, 0f),
                ContactCount = 2,
                FeatureId0 = 10,
                FeatureId1 = 11
            };

            constraint.MatchManifold(manifold);

            Assert.Equal(0f, constraint.NormalImpulse0);
            Assert.Equal(0f, constraint.NormalImpulse1);
            Assert.Equal(float3.Zero, constraint.TangentImpulse);
            Assert.Equal(0f, constraint.TwistImpulse);
        }
    }
}
```

- [ ] **Step 2: Run the focused constraint tests and verify they fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "BoxBoxContactConstraint3DTests" -v minimal
```

Expected: `FAIL` because `MatchManifold` currently resets impulses instead of redistributing them.

- [ ] **Step 3: Implement BEPU-style impulse redistribution in `BoxBoxContactConstraint3D`**

Update `BoxBoxContactConstraint3D.cs` so `MatchManifold` redistributes cached support when the manifold normal stays aligned enough:

```csharp
/// <summary>
/// Returns the total cached normal impulse across every stored contact.
/// </summary>
/// <returns>Total cached normal impulse.</returns>
float ResolveTotalNormalImpulse() {
    return NormalImpulse0 + NormalImpulse1 + NormalImpulse2 + NormalImpulse3;
}

/// <summary>
/// Redistributes cached support across the current manifold contact count.
/// </summary>
/// <param name="contactCount">Current manifold contact count.</param>
void RedistributeNormalImpulses(int contactCount) {
    float redistributedImpulse = contactCount > 0
        ? ResolveTotalNormalImpulse() / contactCount
        : 0f;
    NormalImpulse0 = contactCount > 0 ? redistributedImpulse : 0f;
    NormalImpulse1 = contactCount > 1 ? redistributedImpulse : 0f;
    NormalImpulse2 = contactCount > 2 ? redistributedImpulse : 0f;
    NormalImpulse3 = contactCount > 3 ? redistributedImpulse : 0f;
    FrameNormalImpulse0 = 0f;
    FrameNormalImpulse1 = 0f;
    FrameNormalImpulse2 = 0f;
    FrameNormalImpulse3 = 0f;
}

public void MatchManifold(BoxBoxContactManifold3D manifold) {
    bool normalStillAligned = !HasLastNormal || float3.Dot(LastNormal, manifold.Normal) >= 0.99f;
    bool featureIdsChanged =
        ContactCount != manifold.ContactCount ||
        FeatureId0 != manifold.FeatureId0 ||
        FeatureId1 != manifold.FeatureId1 ||
        FeatureId2 != manifold.FeatureId2 ||
        FeatureId3 != manifold.FeatureId3;

    if (!normalStillAligned) {
        ResetImpulses();
    } else if (featureIdsChanged) {
        RedistributeNormalImpulses(manifold.ContactCount);
    }

    ContactCount = manifold.ContactCount;
    FeatureId0 = manifold.FeatureId0;
    FeatureId1 = manifold.FeatureId1;
    FeatureId2 = manifold.FeatureId2;
    FeatureId3 = manifold.FeatureId3;
    LastNormal = manifold.Normal;
    HasLastNormal = true;
}
```

- [ ] **Step 4: Run the focused constraint tests and verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "BoxBoxContactConstraint3DTests" -v minimal
```

Expected: `PASS`.

- [ ] **Step 5: Commit the warm-start redistribution change**

```bash
rtk git -C C:\dev\helworks\helengine add engine/helengine.physics3d.tests/BoxBoxContactConstraint3DTests.cs engine/helengine.physics3d/collision/BoxBoxContactConstraint3D.cs
rtk git -C C:\dev\helworks\helengine commit -m "Preserve box-box support across manifold updates"
```

### Task 3: Align Manifold Friction And Twist To Active Support Contacts

**Files:**
- Modify: `engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs`
- Modify: `engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs`

- [ ] **Step 1: Re-run the tower parity regression and the existing tower/stack guards**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "Step_WithCityEightBoxTower_SettlesUpperBoxesWithoutResidualSpin|Step_WithCityEightBoxTower_DoesNotCreateRunawayAngularVelocity|Step_WithCityDynamicStackBoxesScene_RemainsNearStableStackTopology|Step_WithRotatedDynamicBoxUsingDefaultMaterials_DampsRockingAfterContact" -v minimal
```

Expected: the new residual-spin regression still `FAIL`s, while the existing stack guards either pass or reveal the next contact-response mismatch.

- [ ] **Step 2: Change manifold friction and twist to use the active support patch**

Modify `ContactMaterialResponse3D.cs` so friction and twist limits are based on the active resolved support contacts from the current step rather than stale total cache:

```csharp
/// <summary>
/// Returns the normal support budget that should drive friction during the current step.
/// </summary>
/// <param name="constraint">Persistent contact constraint to inspect.</param>
/// <returns>Current-step support budget for friction.</returns>
static float ResolveFrictionImpulseBudget(BoxBoxContactConstraint3D constraint) {
    float frameTotal = constraint.FrameNormalImpulse0 +
        constraint.FrameNormalImpulse1 +
        constraint.FrameNormalImpulse2 +
        constraint.FrameNormalImpulse3;
    if (frameTotal > 0f) {
        return frameTotal;
    }

    return constraint.NormalImpulse0 +
        constraint.NormalImpulse1 +
        constraint.NormalImpulse2 +
        constraint.NormalImpulse3;
}

/// <summary>
/// Returns the number of resolved manifold contacts whose penetration is nonnegative.
/// </summary>
/// <param name="manifold">Manifold to inspect.</param>
/// <returns>Resolved contact count.</returns>
static int ResolveResolvedContactCount(BoxBoxContactManifold3D manifold) {
    int count = 0;
    for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
        if (ResolveManifoldContactPenetration(manifold, contactIndex) >= 0f) {
            count++;
        }
    }

    return count;
}

static float ResolveManifoldDynamicFriction(BodyState3D first, BodyState3D second, BoxBoxContactManifold3D manifold) {
    float friction = (float)ResolveCombinedDynamicFriction(first.Collider, second.Collider);
    int resolvedContactCount = ResolveResolvedContactCount(manifold);
    if (resolvedContactCount <= 1) {
        return friction;
    }

    return friction / resolvedContactCount;
}

static void SolveBoxBoxPersistentTangentFriction(
    BodyState3D first,
    BodyState3D second,
    BoxBoxContactManifold3D manifold,
    BoxBoxContactConstraint3D constraint) {
    float maximumImpulse = ResolveFrictionImpulseBudget(constraint) * ResolveManifoldDynamicFriction(first, second, manifold);
    // Keep the existing tangent solve below this line.
}

static float ResolveTwistFrictionLimit(
    BodyState3D first,
    BodyState3D second,
    BoxBoxContactManifold3D manifold,
    BoxBoxContactConstraint3D constraint) {
    float totalNormalImpulse = ResolveFrictionImpulseBudget(constraint);
    if (totalNormalImpulse <= 0f) {
        return 0f;
    }

    float3 center = ResolveManifoldCenter(manifold);
    float maximumRadius = 0f;
    for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
        if (ResolveManifoldContactPenetration(manifold, contactIndex) < 0f) {
            continue;
        }

        float3 offset = ResolveManifoldContactPoint(manifold, contactIndex) - center;
        maximumRadius = Math.Max(maximumRadius, (float)Math.Sqrt(float3.Dot(offset, offset)));
    }

    return totalNormalImpulse * ResolveManifoldDynamicFriction(first, second, manifold) * maximumRadius;
}
```

- [ ] **Step 3: Run the tower and stack guards and verify they pass**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj --filter "Step_WithCityEightBoxTower_SettlesUpperBoxesWithoutResidualSpin|Step_WithCityEightBoxTower_DoesNotCreateRunawayAngularVelocity|Step_WithCityEightBoxTower_CollapsesUnstableStackNearBepuTopology|Step_WithCityDynamicStackBoxesScene_KeepsTopBoxFallingAfterFirstStep|Step_WithCityDynamicStackBoxesScene_DoesNotLaunchTopBoxUpwardDuringInitialSettle|Step_WithCityDynamicStackBoxesScene_RemainsNearStableStackTopology|Step_WithCityDynamicStackBoxesScene_DoesNotOverDampTopBoxOnUncenteredSupport|Step_WithRotatedDynamicBoxUsingDefaultMaterials_DampsRockingAfterContact" -v minimal
```

Expected: `PASS`.

- [ ] **Step 4: Commit the contact-response parity change**

```bash
rtk git -C C:\dev\helworks\helengine add engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs
rtk git -C C:\dev\helworks\helengine commit -m "Align box manifold friction with BEPU support response"
```

### Task 4: Validate Against BEPU And Rebuild Windows

**Files:**
- Modify: `artifacts/physics-comparison/output/comparison-summary.txt`
- Output: `C:\dev\helprojs\output\windows\helengine_windows.exe`

- [ ] **Step 1: Run the full tower comparison harness**

Run:

```bash
rtk dotnet run --project C:\dev\helworks\helengine\artifacts\physics-comparison\PhysicsComparisonHarness.csproj --no-restore
```

Expected: `PASS` and updated comparison files under `C:\dev\helworks\helengine\artifacts\physics-comparison\output`.

- [ ] **Step 2: Inspect the updated tower summary for movement toward BEPU**

Read:

```bash
rtk powershell -Command "Get-Content 'C:\dev\helworks\helengine\artifacts\physics-comparison\output\comparison-summary.txt' -TotalCount 200 | Out-String"
```

Expected:

- final helengine box positions are materially closer to the BEPU positions than the previous baseline,
- final angular deltas are smaller for the upper boxes,
- overlap summary does not regress.

- [ ] **Step 3: Run the full physics test project**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.physics3d.tests\helengine.physics3d.tests.csproj -v minimal
```

Expected: `PASS`.

- [ ] **Step 4: Rebuild the Windows demo**

Run:

```bash
rtk dotnet C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\bin\Debug\net9.0-windows\helengine.editor.app.dll --project C:\dev\helprojs\city\project.heproj --build windows --output C:\dev\helprojs\output\windows
```

Expected: `Build completed for platform 'windows'`.

- [ ] **Step 5: Launch the rebuilt Windows demo**

Run:

```bash
rtk powershell -Command "Start-Process -FilePath 'C:\dev\helprojs\output\windows\helengine_windows.exe' -WindowStyle Hidden -PassThru | Select-Object -ExpandProperty Id | Out-String"
```

Expected: a numeric process id and a running demo process.

- [ ] **Step 6: Commit the parity pass**

```bash
rtk git -C C:\dev\helworks\helengine add engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs engine/helengine.physics3d.tests/BoxBoxContactConstraint3DTests.cs engine/helengine.physics3d/collision/BoxBoxContactConstraint3D.cs engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs artifacts/physics-comparison/output/comparison-summary.txt
rtk git -C C:\dev\helworks\helengine commit -m "Improve eight-box tower BEPU parity"
```

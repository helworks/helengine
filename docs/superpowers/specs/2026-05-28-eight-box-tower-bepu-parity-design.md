# Eight-Box Tower BEPU Parity Design

## Goal

Bring Helengine's dynamic box stacking behavior materially closer to the BEPU reference, using the city eight-box tower as the primary parity gate.

This pass is not a scene-specific visual patch. The target is solver parity for persistent box-box support behavior so that:

- the eight-box tower settles and collapses in a topology closer to BEPU,
- the four-box dynamic stack remains a useful secondary guard,
- tilted-box and smaller stack tests do not regress while tower parity improves.

## Current Problem

The earlier fixes improved the most visible early failures:

- the top box no longer immediately freezes on first support contact,
- the worst tilted-support over-damping is reduced,
- the four-box showcase is closer to the intended settle behavior.

The remaining divergence is deeper:

- long-horizon box stacks still shear laterally more than BEPU,
- residual support behavior still injects or preserves too much lateral error,
- contact cleanup and friction behavior are still not close enough to BEPU's persistent contact response.

The comparison artifacts show that Helengine now diverges later than before, but the final tower and stack topology still differ substantially from BEPU.

## Scope

This design targets only dynamic box-box parity in the 3D physics runtime.

In scope:

- persistent normal contact response for 2-4 point box manifolds,
- tangent friction behavior for supported box stacks,
- twist friction behavior for face-support contacts,
- eight-box tower regression coverage,
- Windows rebuild and manual validation on the city demo.

Out of scope:

- broad scene-specific hacks,
- non-box collider parity work,
- unrelated scheduler or packaging changes,
- hiding solver issues behind stronger sleep or damping rules.

## Reference Sources

Primary local reference:

- `C:\dev\helworks\reference\physics\bepuphysics2`

Supporting local analysis:

- [bepu-physics2-collision-analysis.md](/C:/dev/helworks/helengine/docs/physics/bepu-physics2-collision-analysis.md)

Relevant BEPU areas:

- `BepuPhysics/CollisionDetection/CollisionTasks/BoxPairTester.cs`
- `BepuPhysics/Constraints/Contact/ContactConvexCommon.cs`
- `BepuPhysics/Constraints/Contact/ContactConvexTypes.cs`
- `BepuPhysics/Constraints/Contact/TangentFriction.cs`
- `BepuPhysics/Constraints/Contact/TwistFriction.cs`

## Recommended Approach

Use constraint parity, not more manifold-only or damping-only tuning.

The narrow phase is already close enough to support useful iteration: Helengine has SAT-based box contacts, multi-point manifolds, feature ids, and persistent box-box constraints. The larger remaining gap is how those manifolds are solved across time. That makes the next best step a BEPU-guided adjustment to persistent normal, tangent, and twist response for face-support contacts.

This approach is preferred because it can improve the eight-box tower, the four-box showcase, and the smaller stack tests from the same underlying contact behavior instead of introducing another layer of special-case stabilization.

## Architecture

### Primary parity gate

The primary regression gate will be the eight-box tower tests in [PhysicsWorld3DDynamicsTests.cs](/C:/dev/helworks/helengine/engine/helengine.physics3d.tests/PhysicsWorld3DDynamicsTests.cs):

- `Step_WithCityEightBoxTower_SettlesWithoutAnyBoxBoxOverlap`
- `Step_WithCityEightBoxTower_DoesNotCreateRunawayAngularVelocity`
- `Step_WithCityEightBoxTower_CollapsesUnstableStackNearBepuTopology`

The next new failing regression should capture the next concrete parity gap after the current fixes, likely one of:

- excessive end-state lateral drift,
- too much residual angular energy in upper boxes,
- support behavior that remains too translational compared to BEPU.

### Secondary guards

The following remain required non-regression guards:

- `Step_WithCityDynamicStackBoxesScene_KeepsTopBoxFallingAfterFirstStep`
- `Step_WithCityDynamicStackBoxesScene_DoesNotLaunchTopBoxUpwardDuringInitialSettle`
- `Step_WithCityDynamicStackBoxesScene_RemainsNearStableStackTopology`
- `Step_WithCityDynamicStackBoxesScene_DoesNotOverDampTopBoxOnUncenteredSupport`
- `Step_WithRotatedDynamicBoxUsingDefaultMaterials_DampsRockingAfterContact`

### Solver focus

The next solver work should remain inside:

- [ContactMaterialResponse3D.cs](/C:/dev/helworks/helengine/engine/helengine.physics3d/collision/ContactMaterialResponse3D.cs)
- [PhysicsWorld3D.cs](/C:/dev/helworks/helengine/engine/helengine.physics3d/PhysicsWorld3D.cs)
- box-manifold helper code already used by persistent contact resolution

Expected direction:

- compare our current normal bias and accumulated impulse behavior against BEPU's convex contact solve,
- compare current tangent friction application against BEPU's manifold-level tangent friction,
- compare current twist friction support against BEPU's contact-twist response,
- change one solver behavior at a time and re-run the tower gate after each step.

### Explicit non-goals for this pass

Do not solve this by:

- increasing or broadening sleep thresholds,
- adding more generic angular damping shortcuts,
- introducing scene-specific tower logic,
- bypassing persistent contact response with one-off impulse hacks.

## Data Flow

1. Reproduce tower behavior through the existing Helengine and BEPU comparison harnesses.
2. Identify the next concrete mismatch in tower support behavior.
3. Encode that mismatch as one focused failing regression in `helengine.physics3d.tests`.
4. Adjust one solver behavior in `ContactMaterialResponse3D` or closely related box-contact code.
5. Re-run targeted parity tests and comparison traces.
6. Rebuild the Windows city demo and relaunch for manual validation.

## Error Handling And Safety

- If a solver change improves the tower gate but regresses the tilted-box or four-box guards, treat that as a failed parity step and revise.
- If a change requires broad retuning of sleep or damping to survive, reject that change as the wrong layer.
- If the full suite still has unrelated existing failures, use targeted parity evidence for this pass, but do not claim full runtime parity.

## Verification Plan

Minimum required verification for each solver iteration:

- targeted eight-box tower tests,
- targeted four-box showcase tests,
- targeted tilted-box damping test,
- targeted comparison harness run if the behavior shift is large.

Required completion verification:

- targeted tower and stack regressions pass,
- Windows build completes successfully,
- packaged Windows demo launches successfully.

## Expected Outcome

After this pass, Helengine should still not be claimed as full BEPU parity, but it should:

- stay meaningfully closer to BEPU over the eight-box tower settle,
- preserve the earlier fixes for the four-box showcase,
- reduce the remaining long-horizon shear and support drift that still make the current tower look visibly wrong.

# BEPUphysics2 Collision Analysis

This document analyzes how BEPUphysics2 handles cube, sphere, and capsule collision, with emphasis on the behavior we need for HelEngine's simple 3D runtime. It is based on the local reference checkout at `C:\dev\helworks\reference\physics\bepuphysics2`.

## Executive Summary

BEPU does not treat collision as "detect overlap, push apart, damp velocity." It builds contact manifolds, turns those manifolds into persistent contact constraints, warm-starts those constraints from previous accumulated impulses, solves penetration and friction iteratively, and lets sleeping/damping handle residual low-energy motion.

For our green-cube problem, the most important differences are:

- BEPU's box-box collision generates up to four contact points for a face patch, not one arbitrary point or a center impulse.
- BEPU preserves contact identity with feature ids and redistributes old impulses when contacts change.
- BEPU solves penetration before friction, then tangent friction and twist friction.
- BEPU has explicit accumulated impulses, warm starting, and sleeping. It is not relying on ad hoc per-frame angular damping to stop stacks.
- BEPU warns that out-of-date contacts can inject energy and sustain oscillation, especially when solver substepping updates constraints without rerunning full collision.

The direct takeaway for HelEngine is that cube stacks need a small persistent contact constraint model, not more one-frame special cases. The current nonstop rotation symptom is consistent with a solver that produces contact torque but has no equivalent warm-started friction/twist constraint and no reliable sleep condition for stable contacts.

## BEPU Simulation Pipeline

The default BEPU timestep is ordered as:

1. Sleep inactive islands.
2. Predict bounding boxes.
3. Run collision detection.
4. Solve constraints, including contact constraints.
5. Incrementally optimize internal structures.

Reference: `BepuPhysics\DefaultTimestepper.cs`, especially the class comment and `Timestep` method around lines 6 and 30-40.

That ordering matters. Collision detection is not the whole physics solve. Collision only creates or updates constraints. The solver then iteratively resolves those constraints using body velocities, inertias, accumulated impulses, material settings, and contact depth.

BEPU also distinguishes full timesteps from solver substeps. Substepping runs the solver and integrator multiple times inside one timestep, but it does not rerun full collision detection every substep. Its docs explicitly warn that approximate incremental contact updates can leave contacts out of date and sustain oscillation. Reference: `Documentation\Substepping.md`, especially lines 25-37 and 69-77.

## Shape Storage and Bounds

BEPU shapes are simple value structs stored separately from bodies:

- `Sphere` stores only `Radius`.
- `Capsule` stores `Radius` and `HalfLength`, with its internal segment along local Y.
- `Box` stores `HalfWidth`, `HalfHeight`, and `HalfLength`.

Each shape computes:

- A bounding box in local/world orientation.
- An angular expansion amount.
- A mass/inertia tensor.
- Ray tests.
- A SIMD "wide" representation used by collision batches.

References:

- `BepuPhysics\Collidables\Sphere.cs`
- `BepuPhysics\Collidables\Capsule.cs`
- `BepuPhysics\Collidables\Box.cs`

The important design point is that shape geometry and body motion are separate. A body owns pose, velocity, inertia, collidable reference, and activity state; the shape set owns reusable shape definitions.

## Narrow Phase and Contact Manifolds

BEPU's narrow phase uses pair testers to produce manifolds. A manifold is not just a normal and penetration depth. It includes:

- Contact normal, conventionally pointing from collidable B to collidable A.
- Contact offsets relative to body A.
- Per-contact depth.
- Per-contact feature id.
- Contact existence flags.
- Contact count, often shape-specific.

Reference: `BepuPhysics\CollisionDetection\ContactManifold.cs`.

Feature ids are critical. BEPU uses them to match new contacts to previous contacts so accumulated impulses can continue across frames. That prevents stable stacks from behaving like every frame is a brand-new impact.

Reference: `BepuPhysics\CollisionDetection\NarrowPhaseConstraintUpdate.cs`, especially `RedistributeImpulses` around lines 82-115 and its use around lines 176-177.

## Sphere Collision

### Sphere-Sphere

Sphere-sphere is the simplest path:

- Compute center distance from A to B.
- Normal is the normalized direction, negated to follow BEPU's B-to-A convention.
- Depth is `radiusA + radiusB - centerDistance`.
- Contact offset is halfway between the extreme points along the normal.
- One contact is emitted if depth is greater than negative speculative margin.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\SpherePairTester.cs`, especially lines 25-41.

This collision produces one contact. There is no rotational contact torque for a pure sphere-sphere normal impulse because the contact is effectively radial.

### Sphere-Box

Sphere-box clamps the sphere center into the box's local bounds:

- Transform the sphere-to-box offset into box local space.
- Clamp the offset to the box extents.
- Use the vector from clamped point to sphere center as the outside normal.
- If the sphere center is inside the box, choose the shortest exit axis.
- Transform the local normal back to world space.
- Emit one contact.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\SphereBoxTester.cs`, especially lines 20-64.

This is close to what a simple engine should implement. It is deterministic, gives one normal/depth/contact point, and cleanly handles the inside-box case.

### Sphere-Capsule

Sphere-capsule uses closest point on the capsule segment:

- Transform the capsule local Y axis to world.
- Project the sphere center onto the capsule segment and clamp to `[-HalfLength, HalfLength]`.
- Compute distance from sphere center to that closest point.
- Normal is based on that vector, with a fallback if the sphere center lies exactly on the capsule axis.
- Emit one contact.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\SphereCapsuleTester.cs`, especially lines 20-48.

This is also suitable for a lightweight implementation.

## Capsule Collision

A capsule is treated as a line segment expanded by a radius. That makes closest-segment math the natural collision basis.

### Capsule-Capsule

Capsule-capsule computes closest points between the two internal line segments:

- Compute the unbounded closest parameters on both line segments.
- Clamp through projected valid intervals instead of simply clamping both parameters independently.
- Build the normal from closest point A to closest point B.
- If the axes are coplanar, accept an interval of contact rather than one point.
- Emit up to two contacts.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\CapsulePairTester.cs`, especially lines 14-123.

The two-contact behavior is important. A capsule lying across another capsule or a flat-ish feature needs an interval, not a single point, to avoid unstable rocking.

### Capsule-Box

Capsule-box is more complex because the box has faces and edges:

- Transform capsule into box local space.
- Test likely box edges against the capsule segment.
- Test box face axes.
- Select the minimum-depth normal.
- Choose a representative box face from that normal.
- Project the capsule segment onto that face.
- Generate up to two contacts over the interval where the capsule crosses the face.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\CapsuleBoxTester.cs`, especially lines 43-123 and 177-342.

The useful idea for us is not the full SIMD/generalized implementation. The useful idea is that a capsule against a face should usually get up to two contacts when the segment overlaps a face region. A single contact makes capsules prone to tipping/rolling artifacts.

## Box Collision

Box-box is the most important case for the current stacked-cubes issue.

BEPU's box pair tester does two separate jobs:

1. Find the separating axis with minimum depth.
2. Generate a stable contact patch for the selected normal.

### Axis Selection

The tester evaluates:

- Edge-edge axes from B's three axes against A's three axes.
- Face normals of A.
- Face normals of B.

It selects the candidate with the smallest penetration depth. This is a full oriented box test, not an axis-aligned overlap test.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\BoxPairTester.cs`, especially `TestEdgeEdge` around line 15, `TestFace` around line 82, and main `Test` around lines 324-399.

### Contact Patch Generation

After choosing the normal, BEPU generates contacts by treating contact generation as face-face clipping. The source comment is explicit: other contact forms are treated as special cases of face-face clipping.

Reference: `BepuPhysics\CollisionDetection\CollisionTasks\BoxPairTester.cs`, especially lines 403-525.

The process is:

- Choose the representative face on each box by maximum dot with the collision normal.
- Build face centers, tangents, and half spans.
- Clip edges from one box against the face bounds of the other.
- Add candidate contacts from clipped edges and face vertices.
- Reduce up to eight candidates down to up to four manifold contacts.
- Store contact offsets, depths, and feature ids.

This is the key reason BEPU stacks do not behave like a one-point pivot unless the geometry really is edge/point contact. A broad face support has multiple contacts spread across the patch. Those contacts create opposing torques, and friction/twist constraints resist persistent rotation.

### What This Means for Cubes

For simple cube-cube-cube collision, the BEPU-inspired minimum we need is:

- Oriented box support, even if the first version only supports cubes.
- A contact manifold with up to four contacts for face support.
- Stable feature ids per contact.
- Persistent accumulated normal impulses per contact.
- Tangent friction over the manifold center.
- Twist friction around the contact normal.
- Sleep/activity thresholds for stable contact islands.

Without those pieces, a cube can keep receiving tiny off-center impulses forever. Damping can hide it, but it will fight correct tipping behavior.

## Contact Constraints and Solver Behavior

BEPU converts manifolds into contact constraints. Convex contact constraints have one to four contacts. For each contact count, BEPU has accumulated impulses:

- Tangent friction impulse.
- Penetration impulse per contact.
- Twist friction impulse.

Reference: `BepuPhysics\Constraints\Contact\ContactConvexTypes.cs`, especially the `Contact1AccumulatedImpulses` through `Contact4AccumulatedImpulses` structs near the top.

The solve order for convex contacts is:

1. Compute spring behavior from contact material settings.
2. Solve penetration limits for each contact.
3. Build a tangent basis from the contact normal.
4. Solve tangent friction at the weighted manifold center.
5. Solve twist friction around the normal.

Reference: `ContactConvexTypes.cs`, for example one-body contact solve around lines 312-327, two-contact one-body solve around lines 464-482, four-contact one-body solve around lines 800-822, and two-body solve paths around lines 962-978 and 1489-1512.

This is very different from our current approach if we only apply one-frame impulses and then try to damp the result. BEPU's friction and twist constraints are tied to accumulated normal impulses, which means they scale with actual support force.

## Warm Starting and Feature IDs

BEPU does not throw away contact solution state every frame. When a manifold updates, it tries to map old contact impulses to new contacts using feature ids.

If contacts match, the old impulse is retained. If contacts do not match, unmatched old impulse is distributed across unmatched new contacts.

Reference: `NarrowPhaseConstraintUpdate.cs`, `RedistributeImpulses` around lines 82-115.

This matters for stacks because stable stacks are mostly the same contacts every frame. Warm starting gives the solver a good initial guess and prevents the first iteration from behaving like impact recovery.

In HelEngine terms: a cube resting on a cube should not start every frame with zero support impulse and rediscover support from scratch. That is a direct path to jitter, slow rocking, or nonstop low-energy rotation.

## Friction, Twist Friction, and Why Rotation Stops

BEPU has two different friction ideas in convex contact constraints:

- Tangent friction resists sliding across the contact plane.
- Twist friction resists relative angular motion around the contact normal.

For multi-contact manifolds, BEPU computes a weighted center of friction using non-speculative contacts. The tangent friction constraint is applied at that center. Twist friction capacity is based on accumulated penetration impulses and distance from the friction center to each contact.

Reference: `ContactConvexTypes.cs`, `FrictionHelpers.ComputeFrictionCenter` and the solve methods for 2/3/4-contact constraints.

This is likely the missing concept behind the green cube that rotates forever. A cube resting on a face can have normal support and still retain angular velocity unless the contact model explicitly removes angular motion through friction/twist constraints or sleeps the body when motion is below threshold.

## Sleeping and Stability

BEPU sleeps inactive bodies before collision detection in the default timestepper. Its stability docs recommend:

- More solver iterations for mild convergence failures.
- More solver update rate or substeps for difficult configurations.
- Sleeping thresholds and damping for residual oscillation.
- Avoiding unstable shape/contact choices that cause contact manifolds to vary frame-to-frame.

References:

- `Documentation\StabilityTips.md`, especially lines 1-47.
- `Documentation\Substepping.md`, especially lines 69-77.

For our runtime, sleep should be an explicit part of the solver design, not a last-minute transform snap. A stable supported cube with low linear and angular velocity should become inactive until something meaningful wakes it.

## What We Should Copy

We should copy these concepts:

- Contact manifolds are first-class data, not temporary one-point response.
- Box-box support needs up to four contacts.
- Capsule-capsule and capsule-box need up to two contacts.
- Sphere contacts can stay one-contact.
- Contacts need stable ids.
- Stable contact pairs need persistent accumulated impulses.
- Penetration, tangent friction, and twist friction are separate constraints.
- Solve penetration before friction.
- Sleeping is part of stability, not a visual hack.
- Fixed timestep consistency matters.

## What We Should Not Copy Directly

We should not copy these BEPU implementation details unless we decide to build a broad generic physics engine:

- SIMD-wide pair batching.
- Generic callback-heavy API shape.
- Full broadphase tree architecture.
- Full compound/mesh/nonconvex handling.
- Every collision pair type.
- The high-complexity type processor hierarchy.

For HelEngine's current need, a simple scalar solver with the same conceptual shape is more valuable than a partial clone of BEPU internals.

## Recommended HelEngine Direction

For the current cube stack bug, the next implementation should be a small persistent contact solver for primitive bodies:

1. Build a `ContactManifold3D` abstraction with normal, offsets, depths, feature ids, and contact count.
2. Generate box-box face manifolds with up to four points using a simplified clipping approach.
3. Generate sphere one-point contacts and capsule two-point contacts.
4. Add a contact cache keyed by body pair and feature ids.
5. Store accumulated normal impulses per contact plus tangent and twist impulses per manifold.
6. Warm start before iterative solving.
7. Run penetration solves for all contacts, then tangent friction, then twist friction.
8. Add body sleep based on supported contact, low velocity, and low angular velocity over multiple frames.

This should replace the current mix of one-frame correction, special-case tilt settling, and angular damping. Those hacks conflict: one tries to keep an unstable cube tipping, another tries to damp contact rocking, and neither knows whether the contact is physically stable.

## Implications for the Green Cube

The nonstop rotation should be treated as a solver design failure, not a scene authoring issue.

The likely causes are:

- The cube receives persistent off-center contact impulses.
- The manifold/friction model does not create enough opposing angular constraint.
- There is no persistent warm-started contact cache.
- Sleeping is either missing or too weak for stable supported cubes.
- Contact normals/contact points may change frame-to-frame enough to inject or preserve angular energy.

The correct fix is to make stable cube support behave like a contact constraint island. Once the cube has a broad, stable support patch and low velocities for several frames, it should stop. If it is genuinely edge-supported or center-of-mass unsupported, it should tip and then eventually settle through the same constraint/sleep path.

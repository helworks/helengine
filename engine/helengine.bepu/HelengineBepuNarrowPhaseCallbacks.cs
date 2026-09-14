using BepuPhysics;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Collidables;
using BepuPhysics.Constraints;
using System.Runtime.CompilerServices;

namespace helengine {
    /// <summary>
    /// Implements collision filtering and contact-material blending for the BEPU-backed Helengine runtime.
    /// </summary>
    public struct HelengineBepuNarrowPhaseCallbacks : INarrowPhaseCallbacks {
        /// <summary>
        /// Maps collidable handles to authored layer, mask, and material data.
        /// </summary>
        public CollidableProperty<BepuCollidableProperties3D> CollidableProperties;

        /// <summary>
        /// Registry used to map live collidables back to the authored entities that own them.
        /// </summary>
        public BepuBodyRegistry3D BodyRegistry;

        /// <summary>
        /// Trigger overlap pairs recorded for the fixed step currently being simulated.
        /// </summary>
        public BepuTriggerPairSet3D TriggerPairs;

        /// <summary>
        /// Initializes one narrow-phase callback bundle.
        /// </summary>
        /// <param name="collidableProperties">Collidable properties aligned to the simulation handles.</param>
        /// <param name="bodyRegistry">Registry holding the runtime handles bound to the active scene.</param>
        /// <param name="triggerPairs">Set that receives trigger overlaps detected during contact generation.</param>
        public HelengineBepuNarrowPhaseCallbacks(
            CollidableProperty<BepuCollidableProperties3D> collidableProperties,
            BepuBodyRegistry3D bodyRegistry,
            BepuTriggerPairSet3D triggerPairs) : this() {
            if (collidableProperties == null) {
                throw new ArgumentNullException(nameof(collidableProperties));
            } else if (bodyRegistry == null) {
                throw new ArgumentNullException(nameof(bodyRegistry));
            } else if (triggerPairs == null) {
                throw new ArgumentNullException(nameof(triggerPairs));
            }

            CollidableProperties = collidableProperties;
            BodyRegistry = bodyRegistry;
            TriggerPairs = triggerPairs;
        }

        /// <summary>
        /// Initializes the collidable-property map against the created simulation.
        /// </summary>
        /// <param name="simulation">Simulation being initialized.</param>
        public void Initialize(Simulation simulation) {
            if (simulation == null) {
                throw new ArgumentNullException(nameof(simulation));
            }

            CollidableProperties.Initialize(simulation);
        }

        /// <summary>
        /// Filters broad collision candidates before manifold generation.
        /// </summary>
        /// <param name="workerIndex">Worker thread index executing the callback.</param>
        /// <param name="a">First collidable reference.</param>
        /// <param name="b">Second collidable reference.</param>
        /// <param name="speculativeMargin">Speculative margin proposed for the pair.</param>
        /// <returns>True when the pair should generate contacts.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) {
            ref BepuCollidableProperties3D firstProperties = ref GetCollidableProperties(a);
            ref BepuCollidableProperties3D secondProperties = ref GetCollidableProperties(b);
            bool collisionMasksCompatible = AreCollisionMasksCompatible(ref firstProperties, ref secondProperties);
            if (!collisionMasksCompatible) {
                return false;
            }

            // Trigger volumes need a real manifold to know whether the shapes actually touch, so they keep running
            // contact generation even against kinematic bodies; the constraint is suppressed in ConfigureContactManifold.
            if (firstProperties.IsTrigger || secondProperties.IsTrigger) {
                return true;
            }

            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        /// <summary>
        /// Allows child-pair generation for convex primitives.
        /// </summary>
        /// <param name="workerIndex">Worker thread index executing the callback.</param>
        /// <param name="pair">Parent collidable pair.</param>
        /// <param name="childIndexA">Child index within the first collidable.</param>
        /// <param name="childIndexB">Child index within the second collidable.</param>
        /// <returns>True when the child pair should generate contacts.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) {
            return true;
        }

        /// <summary>
        /// Configures contact materials for one generated manifold.
        /// </summary>
        /// <typeparam name="TManifold">Manifold type produced by BEPU.</typeparam>
        /// <param name="workerIndex">Worker thread index executing the callback.</param>
        /// <param name="pair">Pair whose manifold is being configured.</param>
        /// <param name="manifold">Generated manifold.</param>
        /// <param name="pairMaterial">Resolved contact material output.</param>
        /// <returns>True when the manifold should be accepted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold> {
            ref BepuCollidableProperties3D firstProperties = ref GetCollidableProperties(pair.A);
            ref BepuCollidableProperties3D secondProperties = ref GetCollidableProperties(pair.B);
            pairMaterial.FrictionCoefficient = ResolvePairFrictionCoefficient(ref firstProperties, ref secondProperties);
            pairMaterial.MaximumRecoveryVelocity = MathF.Max(firstProperties.MaximumRecoveryVelocity, secondProperties.MaximumRecoveryVelocity);
            pairMaterial.SpringSettings = ResolvePairSpringSettings(ref firstProperties, ref secondProperties);
            bool firstIsTrigger = firstProperties.IsTrigger;
            bool secondIsTrigger = secondProperties.IsTrigger;
            if (!firstIsTrigger && !secondIsTrigger) {
                return true;
            }

            if (HasTouchingContact(ref manifold)) {
                RecordTriggerPair(pair, firstIsTrigger, secondIsTrigger);
            }

            // Trigger volumes report overlaps instead of resolving them, so the pair never becomes a solid constraint.
            return false;
        }

        /// <summary>
        /// Records one detected trigger overlap against the authored entities behind the simulation pair.
        /// A pair where both colliders are triggers is recorded twice so each trigger observes the other volume.
        /// </summary>
        /// <param name="pair">Pair whose manifold reported touching contacts.</param>
        /// <param name="firstIsTrigger">True when the first collidable is authored as a trigger.</param>
        /// <param name="secondIsTrigger">True when the second collidable is authored as a trigger.</param>
        void RecordTriggerPair(CollidablePair pair, bool firstIsTrigger, bool secondIsTrigger) {
            BepuBodyHandle3D firstHandle = BodyRegistry.FindHandleByCollidable(pair.A);
            BepuBodyHandle3D secondHandle = BodyRegistry.FindHandleByCollidable(pair.B);
            if (firstHandle == null || secondHandle == null) {
                throw new InvalidOperationException("Trigger overlap detection requires both collidables to be registered runtime bodies.");
            }

            if (firstIsTrigger) {
                TriggerPairs.Add(new TriggerPairKey3D(firstHandle.Entity, secondHandle.Entity));
            }
            if (secondIsTrigger) {
                TriggerPairs.Add(new TriggerPairKey3D(secondHandle.Entity, firstHandle.Entity));
            }
        }

        /// <summary>
        /// Determines whether a generated manifold contains at least one contact that is actually touching.
        /// BEPU also emits speculative contacts for shapes that are merely close, and those carry a negative depth.
        /// </summary>
        /// <typeparam name="TManifold">Manifold type produced by BEPU.</typeparam>
        /// <param name="manifold">Generated manifold to inspect.</param>
        /// <returns>True when at least one contact has non-negative penetration depth.</returns>
        static bool HasTouchingContact<TManifold>(ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold> {
            int contactCount = manifold.Count;
            for (int index = 0; index < contactCount; index++) {
                if (manifold.GetDepth(index) >= 0f) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Allows convex child-manifold configuration to continue unchanged.
        /// </summary>
        /// <param name="workerIndex">Worker thread index executing the callback.</param>
        /// <param name="pair">Parent collidable pair.</param>
        /// <param name="childIndexA">Child index within the first collidable.</param>
        /// <param name="childIndexB">Child index within the second collidable.</param>
        /// <param name="manifold">Generated convex manifold.</param>
        /// <returns>True when the manifold should be accepted.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) {
            return true;
        }

        /// <summary>
        /// Releases callback-owned resources.
        /// </summary>
        public void Dispose() {
        }

        /// <summary>
        /// Reads authored collidable properties through the concrete body or static handle overload.
        /// This avoids forwarding a reference-return through <see cref="CollidableReference"/> in native builds.
        /// </summary>
        /// <param name="collidable">Collidable whose authored properties should be read.</param>
        /// <returns>Reference to the authored properties stored for the collidable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ref BepuCollidableProperties3D GetCollidableProperties(CollidableReference collidable) {
            if (collidable.Mobility == CollidableMobility.Static) {
                ref BepuCollidableProperties3D staticProperties = ref CollidableProperties[collidable.StaticHandle];
                return ref staticProperties;
            }

            ref BepuCollidableProperties3D bodyProperties = ref CollidableProperties[collidable.BodyHandle];
            return ref bodyProperties;
        }

        /// <summary>
        /// Determines whether two authored collider filter sets can interact.
        /// </summary>
        /// <param name="firstProperties">First collidable properties.</param>
        /// <param name="secondProperties">Second collidable properties.</param>
        /// <returns>True when both colliders accept each other's layer.</returns>
        static bool AreCollisionMasksCompatible(ref BepuCollidableProperties3D firstProperties, ref BepuCollidableProperties3D secondProperties) {
            bool firstAcceptsSecond = (firstProperties.CollisionMask & secondProperties.CollisionLayer) != 0;
            bool secondAcceptsFirst = (secondProperties.CollisionMask & firstProperties.CollisionLayer) != 0;
            return firstAcceptsSecond && secondAcceptsFirst;
        }

        /// <summary>
        /// Blends two authored dynamic-friction values into one BEPU pair coefficient.
        /// </summary>
        /// <param name="firstProperties">First collidable properties.</param>
        /// <param name="secondProperties">Second collidable properties.</param>
        /// <returns>Pair friction coefficient.</returns>
        static float ResolvePairFrictionCoefficient(ref BepuCollidableProperties3D firstProperties, ref BepuCollidableProperties3D secondProperties) {
            return (firstProperties.DynamicFriction + secondProperties.DynamicFriction) * 0.5f;
        }

        /// <summary>
        /// Selects the spring settings that should drive one contact pair.
        /// </summary>
        /// <param name="firstProperties">First collidable properties.</param>
        /// <param name="secondProperties">Second collidable properties.</param>
        /// <returns>Chosen pair spring settings.</returns>
        static SpringSettings ResolvePairSpringSettings(ref BepuCollidableProperties3D firstProperties, ref BepuCollidableProperties3D secondProperties) {
            if (secondProperties.MaximumRecoveryVelocity > firstProperties.MaximumRecoveryVelocity) {
                return secondProperties.SpringSettings;
            }

            return firstProperties.SpringSettings;
        }
    }
}

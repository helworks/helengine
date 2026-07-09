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
        /// Initializes one narrow-phase callback bundle.
        /// </summary>
        /// <param name="collidableProperties">Collidable properties aligned to the simulation handles.</param>
        public HelengineBepuNarrowPhaseCallbacks(CollidableProperty<BepuCollidableProperties3D> collidableProperties) : this() {
            CollidableProperties = collidableProperties;
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
            if (a.Mobility != CollidableMobility.Dynamic && b.Mobility != CollidableMobility.Dynamic) {
                return false;
            }

            BepuCollidableProperties3D firstProperties = CollidableProperties[a];
            BepuCollidableProperties3D secondProperties = CollidableProperties[b];
            if (!AreCollisionMasksCompatible(firstProperties, secondProperties)) {
                return false;
            }

            return !firstProperties.IsTrigger && !secondProperties.IsTrigger;
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
            BepuCollidableProperties3D firstProperties = CollidableProperties[pair.A];
            BepuCollidableProperties3D secondProperties = CollidableProperties[pair.B];
            pairMaterial.FrictionCoefficient = ResolvePairFrictionCoefficient(firstProperties, secondProperties);
            pairMaterial.MaximumRecoveryVelocity = MathF.Max(firstProperties.MaximumRecoveryVelocity, secondProperties.MaximumRecoveryVelocity);
            pairMaterial.SpringSettings = ResolvePairSpringSettings(firstProperties, secondProperties);
            return true;
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
        /// Determines whether two authored collider filter sets can interact.
        /// </summary>
        /// <param name="firstProperties">First collidable properties.</param>
        /// <param name="secondProperties">Second collidable properties.</param>
        /// <returns>True when both colliders accept each other's layer.</returns>
        static bool AreCollisionMasksCompatible(BepuCollidableProperties3D firstProperties, BepuCollidableProperties3D secondProperties) {
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
        static float ResolvePairFrictionCoefficient(BepuCollidableProperties3D firstProperties, BepuCollidableProperties3D secondProperties) {
            return (firstProperties.DynamicFriction + secondProperties.DynamicFriction) * 0.5f;
        }

        /// <summary>
        /// Selects the spring settings that should drive one contact pair.
        /// </summary>
        /// <param name="firstProperties">First collidable properties.</param>
        /// <param name="secondProperties">Second collidable properties.</param>
        /// <returns>Chosen pair spring settings.</returns>
        static SpringSettings ResolvePairSpringSettings(BepuCollidableProperties3D firstProperties, BepuCollidableProperties3D secondProperties) {
            if (secondProperties.MaximumRecoveryVelocity > firstProperties.MaximumRecoveryVelocity) {
                return secondProperties.SpringSettings;
            }

            return firstProperties.SpringSettings;
        }
    }
}

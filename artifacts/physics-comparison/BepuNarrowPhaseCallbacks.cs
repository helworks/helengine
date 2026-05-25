using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using System.Runtime.CompilerServices;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Supplies BEPU contact filtering, contact material properties, and contact tracing.
    /// </summary>
    public struct BepuNarrowPhaseCallbacks : INarrowPhaseCallbacks {
        /// <summary>
        /// Contact collector used to save manifold data.
        /// </summary>
        BepuContactTraceCollector Collector;

        /// <summary>
        /// Initializes BEPU narrow-phase callbacks.
        /// </summary>
        /// <param name="collector">Collector receiving contact records.</param>
        public BepuNarrowPhaseCallbacks(BepuContactTraceCollector collector) {
            Collector = collector;
        }

        /// <summary>
        /// Initializes callbacks after the simulation is constructed.
        /// </summary>
        /// <param name="simulation">Simulation owning the callbacks.</param>
        public void Initialize(Simulation simulation) {
        }

        /// <summary>
        /// Allows dynamic-involving pairs to generate contacts.
        /// </summary>
        /// <param name="workerIndex">Worker index processing the pair.</param>
        /// <param name="a">First collidable.</param>
        /// <param name="b">Second collidable.</param>
        /// <param name="speculativeMargin">Speculative contact margin.</param>
        /// <returns>True when contact generation should run.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b, ref float speculativeMargin) {
            return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
        }

        /// <summary>
        /// Allows all child pairs inside compound contacts.
        /// </summary>
        /// <param name="workerIndex">Worker index processing the pair.</param>
        /// <param name="pair">Parent collidable pair.</param>
        /// <param name="childIndexA">First child index.</param>
        /// <param name="childIndexB">Second child index.</param>
        /// <returns>True for every child pair.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB) {
            return true;
        }

        /// <summary>
        /// Configures one generated contact manifold and records it.
        /// </summary>
        /// <typeparam name="TManifold">Manifold type provided by BEPU.</typeparam>
        /// <param name="workerIndex">Worker index processing the manifold.</param>
        /// <param name="pair">Collidable pair that generated the manifold.</param>
        /// <param name="manifold">Generated contact manifold.</param>
        /// <param name="pairMaterial">Material values used by the solver.</param>
        /// <returns>True so BEPU creates a contact constraint.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : unmanaged, IContactManifold<TManifold> {
            pairMaterial.FrictionCoefficient = 1f;
            pairMaterial.MaximumRecoveryVelocity = 2f;
            pairMaterial.SpringSettings = new SpringSettings(30, 1);
            Collector.Record(pair, ref manifold);
            return true;
        }

        /// <summary>
        /// Allows child manifolds inside compound contacts.
        /// </summary>
        /// <param name="workerIndex">Worker index processing the manifold.</param>
        /// <param name="pair">Collidable pair that generated the manifold.</param>
        /// <param name="childIndexA">First child index.</param>
        /// <param name="childIndexB">Second child index.</param>
        /// <param name="manifold">Generated convex child manifold.</param>
        /// <returns>True so BEPU keeps the manifold.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold) {
            return true;
        }

        /// <summary>
        /// Releases callback resources.
        /// </summary>
        public void Dispose() {
        }
    }
}

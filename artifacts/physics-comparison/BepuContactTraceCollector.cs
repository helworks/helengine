using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Receives BEPU narrow-phase manifolds and records their contact data.
    /// </summary>
    public sealed class BepuContactTraceCollector {
        /// <summary>
        /// Contact CSV writer used by the collector.
        /// </summary>
        readonly ContactTraceWriter Writer;

        /// <summary>
        /// Initializes a BEPU contact collector.
        /// </summary>
        /// <param name="writer">Contact writer receiving rows.</param>
        public BepuContactTraceCollector(ContactTraceWriter writer) {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <summary>
        /// Gets or sets the simulation step currently being processed by BEPU.
        /// </summary>
        public int CurrentStepIndex { get; set; }

        /// <summary>
        /// Records every contact in one manifold.
        /// </summary>
        /// <typeparam name="TManifold">Manifold type provided by BEPU.</typeparam>
        /// <param name="pair">Pair that generated the manifold.</param>
        /// <param name="manifold">Manifold to record.</param>
        public void Record<TManifold>(CollidablePair pair, ref TManifold manifold) where TManifold : unmanaged, IContactManifold<TManifold> {
            for (int contactIndex = 0; contactIndex < manifold.Count; contactIndex++) {
                manifold.GetContact(contactIndex, out System.Numerics.Vector3 offset, out System.Numerics.Vector3 normal, out float depth, out int featureId);
                Writer.Write(CurrentStepIndex, ResolveHandleValue(pair.A), ResolveHandleValue(pair.B), contactIndex, featureId, depth, offset, normal);
            }
        }

        /// <summary>
        /// Resolves a stable numeric handle from one collidable reference.
        /// </summary>
        /// <param name="collidable">Collidable reference to identify.</param>
        /// <returns>Body or static handle value.</returns>
        static int ResolveHandleValue(CollidableReference collidable) {
            if (collidable.Mobility == CollidableMobility.Static) {
                return collidable.StaticHandle.Value;
            }

            return collidable.BodyHandle.Value;
        }
    }
}

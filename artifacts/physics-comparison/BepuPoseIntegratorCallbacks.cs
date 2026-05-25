using BepuPhysics;
using BepuUtilities;
using System.Numerics;

namespace Helengine.PhysicsComparison {
    /// <summary>
    /// Applies constant gravity to BEPU dynamic bodies.
    /// </summary>
    public struct BepuPoseIntegratorCallbacks : IPoseIntegratorCallbacks {
        /// <summary>
        /// Gravity acceleration applied to dynamic bodies.
        /// </summary>
        public Vector3 Gravity;

        /// <summary>
        /// Gravity multiplied by the current integration time step.
        /// </summary>
        Vector3Wide GravityWideDt;

        /// <summary>
        /// Initializes BEPU pose integration callbacks.
        /// </summary>
        /// <param name="gravity">Gravity acceleration.</param>
        public BepuPoseIntegratorCallbacks(Vector3 gravity) {
            Gravity = gravity;
            GravityWideDt = default;
        }

        /// <summary>
        /// Gets the angular integration mode used by the reference simulation.
        /// </summary>
        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        /// <summary>
        /// Gets whether unconstrained bodies should substep with constrained bodies.
        /// </summary>
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;

        /// <summary>
        /// Gets whether kinematic bodies should receive velocity integration callbacks.
        /// </summary>
        public readonly bool IntegrateVelocityForKinematics => false;

        /// <summary>
        /// Initializes callbacks after the simulation is constructed.
        /// </summary>
        /// <param name="simulation">Simulation owning the callbacks.</param>
        public void Initialize(Simulation simulation) {
        }

        /// <summary>
        /// Precomputes gravity multiplied by the active step duration.
        /// </summary>
        /// <param name="dt">Integration time step.</param>
        public void PrepareForIntegration(float dt) {
            GravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
        }

        /// <summary>
        /// Applies gravity to a SIMD bundle of dynamic bodies.
        /// </summary>
        /// <param name="bodyIndices">Body indices in the bundle.</param>
        /// <param name="position">Current positions.</param>
        /// <param name="orientation">Current orientations.</param>
        /// <param name="localInertia">Current local inertias.</param>
        /// <param name="integrationMask">Active SIMD lanes.</param>
        /// <param name="workerIndex">Worker index processing the bundle.</param>
        /// <param name="dt">Per-lane integration time.</param>
        /// <param name="velocity">Velocity bundle to update.</param>
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity) {
            velocity.Linear += GravityWideDt;
        }
    }
}

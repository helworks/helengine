using BepuPhysics;
using BepuUtilities;
using System.Numerics;

namespace helengine {
    /// <summary>
    /// Implements BEPU velocity integration using the authored Helengine per-body gravity settings.
    /// </summary>
    public struct HelengineBepuPoseIntegratorCallbacks : IPoseIntegratorCallbacks {
        /// <summary>
        /// Maps body handles to authored gravity accelerations in world Y units per second squared.
        /// </summary>
        public CollidableProperty<float> GravityAccelerations;

        /// <summary>
        /// Provides access to body-handle lookups for the active simulation.
        /// </summary>
        Bodies BodiesValue;

        /// <summary>
        /// Initializes one pose-integrator callback bundle.
        /// </summary>
        /// <param name="gravityAccelerations">Gravity values aligned to the simulation body handles.</param>
        public HelengineBepuPoseIntegratorCallbacks(CollidableProperty<float> gravityAccelerations) : this() {
            GravityAccelerations = gravityAccelerations;
        }

        /// <summary>
        /// Gets the angular integration mode used by this runtime.
        /// </summary>
        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

        /// <summary>
        /// Gets whether unconstrained bodies should use solver substeps.
        /// </summary>
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;

        /// <summary>
        /// Gets whether kinematic bodies should run through velocity integration.
        /// </summary>
        public readonly bool IntegrateVelocityForKinematics => false;

        /// <summary>
        /// Initializes the per-body gravity property map against the created simulation.
        /// </summary>
        /// <param name="simulation">Simulation being initialized.</param>
        public void Initialize(Simulation simulation) {
            if (simulation == null) {
                throw new ArgumentNullException(nameof(simulation));
            }

            GravityAccelerations.Initialize(simulation);
            BodiesValue = simulation.Bodies;
        }

        /// <summary>
        /// Performs any preintegration preparation required for the current timestep.
        /// </summary>
        /// <param name="dt">Current step duration.</param>
        public void PrepareForIntegration(float dt) {
        }

        /// <summary>
        /// Applies authored gravity accelerations to the active body bundle.
        /// </summary>
        /// <param name="bodyIndices">Active-set body indices for the SIMD bundle.</param>
        /// <param name="position">Current body positions.</param>
        /// <param name="orientation">Current body orientations.</param>
        /// <param name="localInertia">Current body inertias.</param>
        /// <param name="integrationMask">Mask describing active SIMD lanes.</param>
        /// <param name="workerIndex">Worker thread index executing the callback.</param>
        /// <param name="dt">Current per-lane timestep duration.</param>
        /// <param name="velocity">Velocity bundle to update.</param>
        public void IntegrateVelocity(Vector<int> bodyIndices, Vector3Wide position, QuaternionWide orientation, BodyInertiaWide localInertia, Vector<int> integrationMask, int workerIndex, Vector<float> dt, ref BodyVelocityWide velocity) {
            Span<float> gravityValues = stackalloc float[Vector<float>.Count];
            for (int bundleSlotIndex = 0; bundleSlotIndex < Vector<int>.Count; bundleSlotIndex++) {
                int bodyIndex = bodyIndices[bundleSlotIndex];
                if (bodyIndex < 0) {
                    continue;
                }

                BodyHandle bodyHandle = BodiesValue.ActiveSet.IndexToHandle[bodyIndex];
                gravityValues[bundleSlotIndex] = GravityAccelerations[bodyHandle];
            }

            velocity.Linear.Y += new Vector<float>(gravityValues) * dt;
        }
    }
}

namespace helengine {
    /// <summary>
    /// Integrates awake dynamic positions and orientations from their already solved world-space velocities.
    /// </summary>
    sealed class HelPhysicsPoseIntegrator3D {
        /// <summary>
        /// Advances occupied awake dynamic poses and normalizes every resulting orientation quaternion.
        /// </summary>
        /// <param name="stepSeconds">Positive simulation duration represented in physics scalar units.</param>
        /// <param name="bodies">Fixed body pool whose awake dynamic poses are advanced in place.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stepSeconds"/> is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void IntegratePose(PhysicsScalar stepSeconds, HelPhysicsBodyPool3D bodies) {
            if (stepSeconds <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Pose integration requires a positive simulation step.");
            }

            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            PhysicsScalar halfStep = stepSeconds * PhysicsScalar.FromFloat(0.5f);
            for (int bodyIndex = 0; bodyIndex < bodies.Capacity; bodyIndex++) {
                if (!bodies.IsOccupied(bodyIndex)) {
                    continue;
                }

                ref HelPhysicsBodyColdState3D coldState = ref bodies.GetRequiredColdStateByIndex(bodyIndex);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
                if (coldState.BodyKind != BodyKind3D.Dynamic || !state.IsAwake) {
                    continue;
                }

                state.Position += state.LinearVelocity * stepSeconds;

                PhysicsQuaternion orientation = state.Orientation;
                PhysicsVector3 angularVelocity = state.AngularVelocity;
                PhysicsQuaternion integratedOrientation = new PhysicsQuaternion(
                    orientation.X + (((angularVelocity.X * orientation.W) +
                        (angularVelocity.Y * orientation.Z) -
                        (angularVelocity.Z * orientation.Y)) * halfStep),
                    orientation.Y + ((-(angularVelocity.X * orientation.Z) +
                        (angularVelocity.Y * orientation.W) +
                        (angularVelocity.Z * orientation.X)) * halfStep),
                    orientation.Z + (((angularVelocity.X * orientation.Y) -
                        (angularVelocity.Y * orientation.X) +
                        (angularVelocity.Z * orientation.W)) * halfStep),
                    orientation.W - (((angularVelocity.X * orientation.X) +
                        (angularVelocity.Y * orientation.Y) +
                        (angularVelocity.Z * orientation.Z)) * halfStep));
                state.Orientation = integratedOrientation.Normalized();
            }
        }
    }
}

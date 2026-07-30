namespace helengine {
    /// <summary>
    /// Integrates accumulated forces, gravity, torque, and rational damping into awake dynamic body velocities.
    /// </summary>
    sealed class HelPhysicsBodyIntegrator3D {
        /// <summary>
        /// Advances velocities for occupied awake dynamic bodies and clears only their consumed force and torque accumulators.
        /// </summary>
        /// <param name="stepSeconds">Positive simulation duration represented in physics scalar units.</param>
        /// <param name="gravity">World-space gravitational acceleration before each body's gravity scale is applied.</param>
        /// <param name="bodies">Fixed body pool whose awake dynamic velocities are advanced in place.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stepSeconds"/> is not positive.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void IntegrateVelocity(PhysicsScalar stepSeconds, in PhysicsVector3 gravity, HelPhysicsBodyPool3D bodies) {
            if (stepSeconds <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Velocity integration requires a positive simulation step.");
            }

            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            for (int bodyIndex = 0; bodyIndex < bodies.Capacity; bodyIndex++) {
                if (!bodies.IsOccupied(bodyIndex)) {
                    continue;
                }

                ref HelPhysicsBodyColdState3D coldState = ref bodies.GetRequiredColdStateByIndex(bodyIndex);
                ref HelPhysicsBodyState3D state = ref bodies.GetRequiredStateByIndex(bodyIndex);
                if (coldState.BodyKind != BodyKind3D.Dynamic || !state.IsAwake) {
                    continue;
                }

                PhysicsVector3 linearAcceleration = (gravity * state.GravityScale) + (state.AccumulatedForce * state.InverseMass);
                state.LinearVelocity += linearAcceleration * stepSeconds;
                PhysicsScalar linearDampingScale =
                    PhysicsScalar.One / (PhysicsScalar.One + (state.LinearDamping * stepSeconds));
                state.LinearVelocity *= linearDampingScale;

                PhysicsMatrix3x3 rotation = PhysicsMatrix3x3.CreateFromQuaternion(state.Orientation);
                PhysicsMatrix3x3 worldInverseInertia = rotation * state.LocalInverseInertia * rotation.Transposed();
                PhysicsVector3 angularAcceleration = worldInverseInertia.Transform(state.AccumulatedTorque);
                state.AngularVelocity += angularAcceleration * stepSeconds;
                PhysicsScalar angularDampingScale =
                    PhysicsScalar.One / (PhysicsScalar.One + (state.AngularDamping * stepSeconds));
                state.AngularVelocity *= angularDampingScale;

                state.AccumulatedForce = PhysicsVector3.Zero;
                state.AccumulatedTorque = PhysicsVector3.Zero;
            }
        }
    }
}

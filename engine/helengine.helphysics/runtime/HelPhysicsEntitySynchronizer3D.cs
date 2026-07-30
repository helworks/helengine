namespace helengine {
    /// <summary>
    /// Coordinates entity-to-world input and exact fixed stepping for one HelPhysics scene binder.
    /// </summary>
    public sealed class HelPhysicsEntitySynchronizer3D {
        /// <summary>
        /// Stores the binder whose public associations are synchronized.
        /// </summary>
        readonly HelPhysicsSceneBinder3D Binder;

        /// <summary>
        /// Initializes a synchronizer for one explicit scene binder.
        /// </summary>
        /// <param name="binder">Binder whose world and current associations should be synchronized.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
        public HelPhysicsEntitySynchronizer3D(HelPhysicsSceneBinder3D binder) {
            Binder = binder ?? throw new ArgumentNullException(nameof(binder));
        }

        /// <summary>
        /// Defers current world pose and authored velocity for every valid kinematic binding.
        /// </summary>
        public void SynchronizeBeforeStep() {
            for (int bindingIndex = 0; bindingIndex < Binder.Bindings.Count; bindingIndex++) {
                HelPhysicsEntityBinding3D binding = Binder.Bindings[bindingIndex];
                if (!binding.IsValid || binding.RigidBody.BodyKind != BodyKind3D.Kinematic) {
                    continue;
                }

                Binder.World.SetKinematicState(
                    binding.BodyHandle,
                    ToPhysicsVector(binding.Entity.Position),
                    ToPhysicsQuaternion(binding.Entity.Orientation),
                    ToPhysicsVector(binding.RigidBody.LinearVelocity),
                    ToPhysicsVector(binding.RigidBody.AngularVelocity));
            }
        }

        /// <summary>
        /// Copies current dynamic world pose and velocity into unparented engine entities after a completed fixed step.
        /// </summary>
        public void SynchronizeAfterStep() {
            for (int bindingIndex = 0; bindingIndex < Binder.Bindings.Count; bindingIndex++) {
                HelPhysicsEntityBinding3D binding = Binder.Bindings[bindingIndex];
                if (!binding.IsValid || binding.RigidBody.BodyKind != BodyKind3D.Dynamic) {
                    continue;
                }

                HelPhysicsBodySnapshot3D snapshot = binding.GetBodySnapshot();
                float3 worldPosition = ToEngineVector(snapshot.Position);
                float4 worldOrientation = ToEngineQuaternion(snapshot.Orientation);
                if (binding.Entity.Parent == null) {
                    binding.Entity.LocalPosition = worldPosition;
                    binding.Entity.LocalOrientation = worldOrientation;
                } else {
                    CopyWorldPoseToParentedEntity(binding.Entity, worldPosition, worldOrientation);
                }

                binding.RigidBody.LinearVelocity = ToEngineVector(snapshot.LinearVelocity);
                binding.RigidBody.AngularVelocity = ToEngineVector(snapshot.AngularVelocity);
            }
        }

        /// <summary>
        /// Converts one simulation world pose into the local pose required beneath the entity's current transformed parent.
        /// </summary>
        /// <param name="entity">Parented entity receiving the local pose.</param>
        /// <param name="worldPosition">Simulation world-space center position.</param>
        /// <param name="worldOrientation">Simulation world-space orientation.</param>
        static void CopyWorldPoseToParentedEntity(
            Entity entity,
            float3 worldPosition,
            float4 worldOrientation) {
            float3 relativePosition = worldPosition - entity.Parent.Position;
            float4 inverseParentOrientation = float4.Inverse(entity.Parent.Orientation);
            float3 unrotatedLocalPosition = float4.RotateVector(relativePosition, inverseParentOrientation);
            entity.LocalPosition = new float3(
                unrotatedLocalPosition.X / entity.Parent.Scale.X,
                unrotatedLocalPosition.Y / entity.Parent.Scale.Y,
                unrotatedLocalPosition.Z / entity.Parent.Scale.Z);
            float4.Concatenate(ref worldOrientation, ref inverseParentOrientation, out float4 localOrientation);
            localOrientation.Normalize();
            entity.LocalOrientation = localOrientation;
        }

        /// <summary>
        /// Synchronizes kinematic input, advances exactly one configured fixed step, and publishes dynamic output.
        /// </summary>
        public void Step() {
            SynchronizeBeforeStep();
            Binder.World.Step(Binder.World.Settings.FixedStepSeconds);
            SynchronizeAfterStep();
        }

        /// <summary>
        /// Converts one HelPhysics vector into the engine numeric domain at the scene boundary.
        /// </summary>
        /// <param name="value">Physics vector to convert component by component.</param>
        /// <returns>An engine vector carrying the same values.</returns>
        static float3 ToEngineVector(PhysicsVector3 value) {
            return new float3(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat());
        }

        /// <summary>
        /// Converts one normalized HelPhysics orientation into the engine numeric domain.
        /// </summary>
        /// <param name="value">Physics quaternion to convert component by component.</param>
        /// <returns>An engine quaternion carrying the same normalized orientation.</returns>
        static float4 ToEngineQuaternion(PhysicsQuaternion value) {
            return new float4(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat(), value.W.ToFloat());
        }

        /// <summary>
        /// Converts one finite engine vector into the dedicated HelPhysics numeric domain.
        /// </summary>
        /// <param name="value">Engine vector to convert component by component.</param>
        /// <returns>A physics vector carrying the same values.</returns>
        static PhysicsVector3 ToPhysicsVector(float3 value) {
            return new PhysicsVector3(value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Converts one engine quaternion into the dedicated HelPhysics numeric domain without silent normalization.
        /// </summary>
        /// <param name="value">Authored quaternion expected to already be normalized.</param>
        /// <returns>A physics quaternion carrying the same values.</returns>
        static PhysicsQuaternion ToPhysicsQuaternion(float4 value) {
            return new PhysicsQuaternion(
                PhysicsScalar.FromFloat(value.X),
                PhysicsScalar.FromFloat(value.Y),
                PhysicsScalar.FromFloat(value.Z),
                PhysicsScalar.FromFloat(value.W));
        }
    }
}

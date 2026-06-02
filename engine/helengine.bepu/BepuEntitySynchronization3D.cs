using BepuPhysics;

namespace helengine {
    /// <summary>
    /// Synchronizes entity transforms with live BEPU body state.
    /// </summary>
    public static class BepuEntitySynchronization3D {
        /// <summary>
        /// Builds one BEPU pose from the current entity transform.
        /// </summary>
        /// <param name="entity">Entity to read from.</param>
        /// <returns>BEPU pose matching the entity transform.</returns>
        public static RigidPose CreatePose(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return new RigidPose(
                BepuNumericConversion3D.ToSystemVector(entity.LocalPosition),
                BepuNumericConversion3D.ToSystemQuaternion(entity.LocalOrientation));
        }

        /// <summary>
        /// Builds one BEPU velocity description from the authored rigid-body component.
        /// </summary>
        /// <param name="rigidBody">Authored rigid body to read from.</param>
        /// <returns>BEPU body velocity matching the authored state.</returns>
        public static BodyVelocity CreateVelocity(RigidBody3DComponent rigidBody) {
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }

            return new BodyVelocity {
                Linear = BepuNumericConversion3D.ToSystemVector(rigidBody.LinearVelocity),
                Angular = BepuNumericConversion3D.ToSystemVector(rigidBody.AngularVelocity)
            };
        }

        /// <summary>
        /// Copies one live BEPU body pose and velocity back into the authored entity graph.
        /// </summary>
        /// <param name="bodyReference">BEPU body reference to read from.</param>
        /// <param name="entity">Entity to update.</param>
        /// <param name="rigidBody">Rigid body component to update.</param>
        public static void CopyBodyToEntity(BodyReference bodyReference, Entity entity, RigidBody3DComponent rigidBody) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }

            entity.LocalPosition = BepuNumericConversion3D.ToHelengineFloat3(bodyReference.Pose.Position);
            entity.LocalOrientation = BepuNumericConversion3D.ToHelengineFloat4(bodyReference.Pose.Orientation);
            rigidBody.LinearVelocity = BepuNumericConversion3D.ToHelengineFloat3(bodyReference.Velocity.Linear);
            rigidBody.AngularVelocity = BepuNumericConversion3D.ToHelengineFloat3(bodyReference.Velocity.Angular);
        }
    }
}

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
                BepuNumericConversion3D.ToSystemVector(entity.Position),
                BepuNumericConversion3D.ToSystemQuaternion(entity.Orientation));
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

            CopyPoseToEntity(bodyReference.Pose, entity, rigidBody);
            rigidBody.LinearVelocity = BepuNumericConversion3D.ToHelengineFloat3(bodyReference.Velocity.Linear);
            rigidBody.AngularVelocity = BepuNumericConversion3D.ToHelengineFloat3(bodyReference.Velocity.Angular);
        }

        /// <summary>
        /// Copies one live BEPU pose back into the authored entity graph while preserving local transforms for parented entities.
        /// </summary>
        /// <param name="pose">World-space BEPU pose to convert.</param>
        /// <param name="entity">Entity to update.</param>
        /// <param name="rigidBody">Rigid body component whose transform is being synchronized.</param>
        public static void CopyPoseToEntity(RigidPose pose, Entity entity, RigidBody3DComponent rigidBody) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (rigidBody == null) {
                throw new ArgumentNullException(nameof(rigidBody));
            }

            float3 worldPosition = BepuNumericConversion3D.ToHelengineFloat3(pose.Position);
            float4 worldOrientation = BepuNumericConversion3D.ToHelengineFloat4(pose.Orientation);
            worldOrientation.Normalize();
            if (entity.Parent == null) {
                entity.LocalPosition = worldPosition;
                entity.LocalOrientation = worldOrientation;
            } else {
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
        }
    }
}

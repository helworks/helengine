namespace helengine {
    /// <summary>
    /// Resolves authored box-controller movement against support samples supplied by HelPhysics narrow phase queries.
    /// </summary>
    public sealed class HelPhysicsCharacterController3D {
        /// <summary>
        /// Stores the entity whose authored position is controlled by this resolver.
        /// </summary>
        readonly Entity EntityValue;

        /// <summary>
        /// Stores authored locomotion settings.
        /// </summary>
        readonly CharacterController3DComponent ControllerValue;

        /// <summary>
        /// Stores the authored box bounds used to convert support surface height into controller center height.
        /// </summary>
        readonly BoxCollider3DComponent BoxColliderValue;

        /// <summary>
        /// Initializes a controller resolver for one entity and authored controller component.
        /// </summary>
        /// <param name="entity">Entity carrying the controller and box collider.</param>
        /// <param name="controller">Authored movement, gravity, slope, step, and snap settings.</param>
        public HelPhysicsCharacterController3D(Entity entity, CharacterController3DComponent controller)
            : this(entity, controller, FindBoxCollider(entity)) {
        }

        /// <summary>
        /// Initializes a controller resolver with its authored box bounds.
        /// </summary>
        /// <param name="entity">Entity carrying the controller and box collider.</param>
        /// <param name="controller">Authored movement, gravity, slope, step, and snap settings.</param>
        /// <param name="boxCollider">Authored box bounds used for support-center conversion.</param>
        public HelPhysicsCharacterController3D(
            Entity entity,
            CharacterController3DComponent controller,
            BoxCollider3DComponent boxCollider) {
            EntityValue = entity ?? throw new ArgumentNullException(nameof(entity));
            ControllerValue = controller ?? throw new ArgumentNullException(nameof(controller));
            BoxColliderValue = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
        }
        /// <summary>
        /// Gets the authored entity advanced by this resolver.
        /// </summary>
        public Entity Entity => EntityValue;

        /// <summary>
        /// Gets the authored controller settings consumed by this resolver.
        /// </summary>
        public CharacterController3DComponent Controller => ControllerValue;

        /// <summary>
        /// Gets the authored box bounds used by this resolver.
        /// </summary>
        public BoxCollider3DComponent BoxCollider => BoxColliderValue;

        /// <summary>
        /// Computes one fixed-step controller motion from authored input and two support samples.
        /// </summary>
        /// <param name="currentPosition">Current world-space controller center.</param>
        /// <param name="currentVerticalVelocity">Vertical velocity from the previous fixed step.</param>
        /// <param name="gravityAcceleration">World-space gravity acceleration.</param>
        /// <param name="stepSeconds">Positive fixed-step duration.</param>
        /// <param name="currentSupport">Support beneath the current position.</param>
        /// <param name="targetSupport">Support beneath the authored target position.</param>
        /// <returns>Resolved position, vertical velocity, grounded state, and inherited support velocity.</returns>
        public HelPhysicsCharacterControllerMotion3D ComputeMotion(
            float3 currentPosition,
            float currentVerticalVelocity,
            float3 gravityAcceleration,
            double stepSeconds,
            HelPhysicsCharacterControllerSupport3D currentSupport,
            HelPhysicsCharacterControllerSupport3D targetSupport) {
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Controller step duration must be positive and finite.");
            }

            float step = (float)stepSeconds;
            float3 moveDirection = ControllerValue.DesiredMoveDirection;
            if (!float.IsFinite(moveDirection.X) || !float.IsFinite(moveDirection.Y) || !float.IsFinite(moveDirection.Z)) {
                throw new InvalidOperationException("Character controller desired movement must be finite.");
            }

            float halfHeight = Math.Abs(BoxColliderValue.Size.Y * EntityValue.Scale.Y) * 0.5f;
            if (!float.IsFinite(halfHeight) || halfHeight <= 0f) {
                throw new InvalidOperationException("Character controller effective box height must be finite and positive.");
            }
            double moveLengthSquared = ((double)moveDirection.X * moveDirection.X) + ((double)moveDirection.Z * moveDirection.Z);
            float3 planarVelocity = float3.Zero;
            if (moveLengthSquared > 0.0000001d && ControllerValue.MoveSpeed > 0d) {
                double inverseLength = 1d / Math.Sqrt(moveLengthSquared);
                planarVelocity = new float3(
                    (float)(moveDirection.X * inverseLength * ControllerValue.MoveSpeed),
                    0f,
                    (float)(moveDirection.Z * inverseLength * ControllerValue.MoveSpeed));
            }

            float3 supportDisplacement = currentSupport.IsValid
                ? currentSupport.Velocity * step
                : float3.Zero;
            float verticalVelocity = currentVerticalVelocity + (gravityAcceleration.Y * (float)ControllerValue.GravityScale * step);
            float3 targetPosition = new float3(
                currentPosition.X + supportDisplacement.X + (planarVelocity.X * step),
                currentPosition.Y + supportDisplacement.Y + (verticalVelocity * step),
                currentPosition.Z + supportDisplacement.Z + (planarVelocity.Z * step));
            bool grounded = false;
            float3 resolvedSupportVelocity = float3.Zero;
            if (targetSupport.IsValid && IsWalkableSupport(targetSupport)) {
                double supportDelta = targetSupport.Height - (targetPosition.Y - halfHeight);
                if (supportDelta <= ControllerValue.StepHeight && supportDelta >= -ControllerValue.GroundSnapDistance) {
                    targetPosition = new float3(targetPosition.X, targetSupport.Height + halfHeight, targetPosition.Z);
                    verticalVelocity = 0f;
                    grounded = true;
                    resolvedSupportVelocity = targetSupport.Velocity;
                }
            }

            return new HelPhysicsCharacterControllerMotion3D(
                targetPosition,
                verticalVelocity,
                grounded,
                resolvedSupportVelocity);
        }
        /// <summary>
        /// Tests one support normal against the authored maximum walkable slope.
        /// </summary>
        bool IsWalkableSupport(HelPhysicsCharacterControllerSupport3D support) {
            float3 normal = support.Normal;
            double lengthSquared =
                ((double)normal.X * normal.X) +
                ((double)normal.Y * normal.Y) +
                ((double)normal.Z * normal.Z);
            if (!double.IsFinite(lengthSquared) || lengthSquared <= 0d) {
                return false;
            }

            double cosine = normal.Y / Math.Sqrt(lengthSquared);
            double minimumCosine = Math.Cos(ControllerValue.MaximumSlopeDegrees * Math.PI / 180d);
            return cosine >= minimumCosine;
        }

        /// <summary>
        /// Finds the one authored box collider required by the default controller constructor.
        /// </summary>
        [NativeBorrowedReturn]
        static BoxCollider3DComponent FindBoxCollider(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            BoxCollider3DComponent result = null;
            if (entity.Components != null) {
                for (int index = 0; index < entity.Components.Count; index++) {
                    if (entity.Components[index] is BoxCollider3DComponent boxCollider) {
                        if (result != null) {
                            throw new InvalidOperationException("A character controller entity cannot carry multiple box colliders.");
                        }

                        result = boxCollider;
                    }
                }
            }

            return result ?? throw new InvalidOperationException("A character controller entity requires one BoxCollider3DComponent.");
        }
    }
}

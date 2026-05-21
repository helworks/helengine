#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER
namespace helengine {
    /// <summary>
    /// Advances one character controller using support sampling, grounded snap, and solid-overlap resolution.
    /// </summary>
    public static class CharacterControllerStepResolver3D {
        /// <summary>
        /// Advances one controller state forward by one fixed simulation step.
        /// </summary>
        /// <param name="controllerState">Controller state being updated.</param>
        /// <param name="bodyStates">Runtime rigid bodies that can contribute support or blocking overlaps.</param>
        /// <param name="staticMeshStates">Cooked static meshes that can contribute support surfaces.</param>
        /// <param name="gravityAcceleration">World gravity acceleration.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        public static void Advance(
            CharacterControllerState3D controllerState,
            IReadOnlyList<BodyState3D> bodyStates,
            IReadOnlyList<StaticMeshBodyState3D> staticMeshStates,
            float3 gravityAcceleration,
            double stepSeconds) {
            if (controllerState == null) {
                throw new ArgumentNullException(nameof(controllerState));
            }
            if (bodyStates == null) {
                throw new ArgumentNullException(nameof(bodyStates));
            }
            if (staticMeshStates == null) {
                throw new ArgumentNullException(nameof(staticMeshStates));
            }
            if (double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) || stepSeconds <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Step size must be a finite value greater than zero.");
            }

            float stepSecondsFloat = (float)stepSeconds;
            TryResolveSupportHeight(
                bodyStates,
                staticMeshStates,
                controllerState.Position.X,
                controllerState.Position.Y,
                controllerState.Position.Z,
                controllerState.HalfExtents,
                controllerState.Controller.MaximumSlopeDegrees,
                out float currentSupportHeight,
                out BodyState3D currentSupportBodyState);
            float3 moveDirection = new float3(
                controllerState.Controller.DesiredMoveDirection.X,
                0f,
                controllerState.Controller.DesiredMoveDirection.Z);
            float3 planarVelocity = CreatePlanarVelocity(moveDirection, controllerState.Controller.MoveSpeed);
            float verticalVelocity = controllerState.VerticalVelocity + (float)(gravityAcceleration.Y * controllerState.Controller.GravityScale * stepSeconds);
            float3 supportDisplacement = CreateSupportDisplacement(currentSupportBodyState, stepSecondsFloat);
            float3 targetPosition = new float3(
                controllerState.Position.X + supportDisplacement.X + (planarVelocity.X * stepSecondsFloat),
                controllerState.Position.Y + supportDisplacement.Y + (verticalVelocity * stepSecondsFloat),
                controllerState.Position.Z + supportDisplacement.Z + (planarVelocity.Z * stepSecondsFloat));
            BodyState3D resolvedSupportBodyState = null;

            if (TryResolveSupportHeight(
                bodyStates,
                staticMeshStates,
                targetPosition.X,
                targetPosition.Y,
                targetPosition.Z,
                controllerState.HalfExtents,
                controllerState.Controller.MaximumSlopeDegrees,
                out float supportHeight,
                out BodyState3D supportBodyState)) {
                float controllerBottom = targetPosition.Y - controllerState.HalfExtents.Y;
                double supportDelta = supportHeight - controllerBottom;
                if (supportDelta <= controllerState.Controller.StepHeight && supportDelta >= -controllerState.Controller.GroundSnapDistance) {
                    targetPosition = new float3(targetPosition.X, supportHeight + controllerState.HalfExtents.Y, targetPosition.Z);
                    verticalVelocity = 0f;
                    resolvedSupportBodyState = supportBodyState;
                }
            }

            BodyState3D overlapIgnoredSupportBodyState = resolvedSupportBodyState;
            if (overlapIgnoredSupportBodyState == null) {
                overlapIgnoredSupportBodyState = currentSupportBodyState;
            }

            CharacterControllerOverlapResolver3D.Resolve(bodyStates, controllerState.HalfExtents, overlapIgnoredSupportBodyState, ref targetPosition, ref verticalVelocity);
            controllerState.Position = targetPosition;
            controllerState.VerticalVelocity = verticalVelocity;
        }

        /// <summary>
        /// Builds planar controller velocity from one authored move direction and speed.
        /// </summary>
        /// <param name="moveDirection">Requested planar move direction.</param>
        /// <param name="moveSpeed">Requested horizontal move speed.</param>
        /// <returns>Planar controller velocity in world units per second.</returns>
        static float3 CreatePlanarVelocity(float3 moveDirection, double moveSpeed) {
            double moveLengthSquared = (moveDirection.X * moveDirection.X) + (moveDirection.Z * moveDirection.Z);
            if (moveLengthSquared <= 0.0000001d || moveSpeed <= 0d) {
                return float3.Zero;
            }

            double inverseLength = 1d / Math.Sqrt(moveLengthSquared);
            return new float3(
                (float)(moveDirection.X * inverseLength * moveSpeed),
                0f,
                (float)(moveDirection.Z * inverseLength * moveSpeed));
        }

        /// <summary>
        /// Builds the displacement inherited from one kinematic support body during the current fixed step.
        /// </summary>
        /// <param name="supportBodyState">Current support body beneath the controller footprint.</param>
        /// <param name="stepSeconds">Simulation step length in seconds.</param>
        /// <returns>Support displacement inherited by the controller during the step.</returns>
        static float3 CreateSupportDisplacement(BodyState3D supportBodyState, float stepSeconds) {
            if (supportBodyState == null) {
                return float3.Zero;
            }
            if (supportBodyState.RigidBody.BodyKind != BodyKind3D.Kinematic) {
                return float3.Zero;
            }

            return new float3(
                supportBodyState.Velocity.X * stepSeconds,
                supportBodyState.Velocity.Y * stepSeconds,
                supportBodyState.Velocity.Z * stepSeconds);
        }

        /// <summary>
        /// Resolves the highest walkable support height and contributing body beneath the supplied controller footprint.
        /// </summary>
        /// <param name="bodyStates">Runtime rigid bodies that can contribute support.</param>
        /// <param name="staticMeshStates">Cooked static meshes that can contribute support.</param>
        /// <param name="centerX">Controller footprint center X coordinate.</param>
        /// <param name="centerY">Controller center Y coordinate.</param>
        /// <param name="centerZ">Controller footprint center Z coordinate.</param>
        /// <param name="halfExtents">Controller half extents.</param>
        /// <param name="maximumSlopeDegrees">Maximum walkable slope angle in degrees.</param>
        /// <param name="supportHeight">Resolved highest support height.</param>
        /// <param name="supportBodyState">Resolved body that owns the highest support height.</param>
        /// <returns>True when at least one support surface was found.</returns>
        static bool TryResolveSupportHeight(
            IReadOnlyList<BodyState3D> bodyStates,
            IReadOnlyList<StaticMeshBodyState3D> staticMeshStates,
            float centerX,
            float centerY,
            float centerZ,
            float3 halfExtents,
            double maximumSlopeDegrees,
            out float supportHeight,
            out BodyState3D supportBodyState) {
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_BODY_SUPPORT
            bool foundRigidBodySupport = CharacterControllerBodySupportResolver3D.TryResolveSupportHeight(
                bodyStates,
                centerX,
                centerY,
                centerZ,
                halfExtents,
                maximumSlopeDegrees,
                out float rigidBodySupportHeight,
                out BodyState3D rigidBodySupportBodyState);
#else
            bool foundRigidBodySupport = false;
            float rigidBodySupportHeight = 0f;
            BodyState3D rigidBodySupportBodyState = null;
#endif
#if !HELENGINE_PHYSICS3D_STRIP_BY_SCENE_FEATURES || HELENGINE_PHYSICS3D_FEATURE_CHARACTER_CONTROLLER_STATIC_MESH_SUPPORT
            bool foundStaticMeshSupport = CharacterControllerStaticMeshSupportResolver3D.TryResolveSupportHeight(
                staticMeshStates,
                centerX,
                centerY,
                centerZ,
                halfExtents,
                maximumSlopeDegrees,
                out float staticMeshSupportHeight);
#else
            bool foundStaticMeshSupport = false;
            float staticMeshSupportHeight = 0f;
#endif

            if (!foundRigidBodySupport && !foundStaticMeshSupport) {
                supportHeight = 0f;
                supportBodyState = null;
                return false;
            }
            if (!foundStaticMeshSupport || (foundRigidBodySupport && rigidBodySupportHeight >= staticMeshSupportHeight)) {
                supportHeight = rigidBodySupportHeight;
                supportBodyState = rigidBodySupportBodyState;
                return true;
            }

            supportHeight = staticMeshSupportHeight;
            supportBodyState = null;
            return true;
        }
    }
}
#endif

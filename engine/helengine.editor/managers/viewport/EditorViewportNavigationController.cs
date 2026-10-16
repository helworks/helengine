namespace helengine.editor {
    /// <summary>
    /// Converts navigation-cube actions into camera targets, projection changes, and interruptible transitions.
    /// </summary>
    public sealed class EditorViewportNavigationController {
        /// <summary>
        /// Duration of a standard-view camera transition in seconds.
        /// </summary>
        public const double TransitionDurationSeconds = 0.2;
        /// <summary>
        /// Minimum camera distance accepted by navigation targets.
        /// </summary>
        const double MinimumNavigationDistance = 0.001;
        /// <summary>
        /// Camera forward direction before orientation is applied.
        /// </summary>
        static readonly float3 CameraForwardAxis = new float3(0f, 0f, -1f);
        /// <summary>
        /// Camera up direction before orientation is applied.
        /// </summary>
        static readonly float3 CameraUpAxis = new float3(0f, 1f, 0f);
        /// <summary>
        /// Tolerance used to notice an external change to a currently interpolated pose.
        /// </summary>
        const double PoseChangeTolerance = 0.0001;

        /// <summary>
        /// Camera controller that owns the viewport pose and orbit target.
        /// </summary>
        readonly EditorViewportCameraController CameraController;
        /// <summary>
        /// Pose captured when the current transition began.
        /// </summary>
        EditorViewportNavigationPose StartPose;
        /// <summary>
        /// Destination pose for the current transition.
        /// </summary>
        EditorViewportNavigationPose TargetPose;
        /// <summary>
        /// Last pose written by this controller for detecting external navigation.
        /// </summary>
        EditorViewportNavigationPose LastDisplayedPose;
        /// <summary>
        /// Elapsed transition time in seconds.
        /// </summary>
        double ElapsedTransitionSeconds;
        /// <summary>
        /// Projection mode last applied by the transition.
        /// </summary>
        CameraProjectionMode LastDisplayedProjectionMode;
        /// <summary>
        /// Orthographic span last applied by the transition when that projection is active.
        /// </summary>
        double LastDisplayedOrthographicSpan;

        /// <summary>
        /// Initializes one navigation controller for a viewport camera controller.
        /// </summary>
        /// <param name="cameraController">Camera controller that owns the viewport pose.</param>
        public EditorViewportNavigationController(EditorViewportCameraController cameraController) {
            CameraController = cameraController ?? throw new ArgumentNullException(nameof(cameraController));
        }

        /// <summary>
        /// Gets whether a standard camera transition is in progress.
        /// </summary>
        public bool IsTransitioning { get; private set; }

        /// <summary>
        /// Gets the camera controller whose pose is owned by this navigation controller.
        /// </summary>
        public EditorViewportCameraController ViewportCameraController => CameraController;

        /// <summary>
        /// Gets or sets the callback invoked after navigation changes the displayed camera pose or projection.
        /// </summary>
        public Action CameraStateChanged { get; set; }

        /// <summary>
        /// Selects one cube face, edge, or corner and animates the camera toward its direction.
        /// </summary>
        /// <param name="target">Signed cube direction selected by the user.</param>
        public void SelectTarget(EditorViewportNavigationTarget target) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            CancelTransition();
            if (target.IsFace) {
                CameraController.SetProjectionMode(CameraProjectionMode.Orthographic);
            }

            EditorViewportNavigationPose currentPose = CaptureCurrentPose();
            float4 targetOrientation = CreateOrientationForCameraOffset(target);
            EditorViewportNavigationPose resolvedTargetPose = new EditorViewportNavigationPose(
                currentPose.Pivot,
                targetOrientation,
                currentPose.Distance);
            BeginTransition(currentPose, resolvedTargetPose);
            NotifyCameraStateChanged();
        }

        /// <summary>
        /// Switches between perspective and orthographic projection without changing apparent scale.
        /// </summary>
        public void ToggleProjection() {
            CancelTransition();
            if (CameraController.Camera is not ICameraProjectionSettings projectionSettings) {
                throw new InvalidOperationException("The viewport camera does not expose optional projection settings.");
            }

            CameraProjectionMode requestedMode = projectionSettings.ProjectionMode == CameraProjectionMode.Perspective
                ? CameraProjectionMode.Orthographic
                : CameraProjectionMode.Perspective;
            CameraController.SetProjectionMode(requestedMode);
            NotifyCameraStateChanged();
        }

        /// <summary>
        /// Orbits the camera immediately about its current pivot using yaw and pitch deltas in radians.
        /// </summary>
        /// <param name="deltaRadians">Yaw and pitch changes in radians.</param>
        public void Orbit(float2 deltaRadians) {
            if (!float.IsFinite(deltaRadians.X) || !float.IsFinite(deltaRadians.Y)) {
                throw new ArgumentOutOfRangeException(nameof(deltaRadians), "Orbit deltas must contain only finite values.");
            }

            CancelTransition();
            EditorViewportNavigationPose currentPose = CaptureCurrentPose();
            float3 forward = NormalizeSafe(float4.RotateVector(CameraForwardAxis, currentPose.Orientation), CameraForwardAxis);
            double yaw = Math.Atan2(-forward.X, -forward.Z) - deltaRadians.X;
            double pitch = Math.Clamp(Math.Asin(Math.Clamp(forward.Y, -1f, 1f)) - deltaRadians.Y, -MaximumOrbitPitch, MaximumOrbitPitch);
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            orientation.Normalize();
            CameraController.SetViewPose(currentPose.Pivot, orientation, currentPose.Distance);
            NotifyCameraStateChanged();
        }

        /// <summary>
        /// Advances the active transition and applies its interpolated camera pose.
        /// </summary>
        /// <param name="elapsedSeconds">Elapsed frame duration in seconds.</param>
        public void Advance(double elapsedSeconds) {
            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0) {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "Elapsed time must be finite and non-negative.");
            }
            if (!IsTransitioning || StartPose == null || TargetPose == null) {
                return;
            }

            EditorViewportNavigationPose currentPose = CaptureCurrentPose();
            if (CameraController.IsNavigating ||
                !ArePosesApproximatelyEqual(LastDisplayedPose, currentPose) ||
                !IsProjectionStateEqualToLastDisplayed()) {
                CancelTransition();
                NotifyCameraStateChanged();
                return;
            }

            ElapsedTransitionSeconds = Math.Min(TransitionDurationSeconds, ElapsedTransitionSeconds + elapsedSeconds);
            double transitionAmount = ElapsedTransitionSeconds / TransitionDurationSeconds;
            if (transitionAmount >= 1.0) {
                CameraController.SetViewPose(TargetPose.Pivot, TargetPose.Orientation, TargetPose.Distance);
                CancelTransition();
                NotifyCameraStateChanged();
                return;
            }

            float interpolationAmount = (float)transitionAmount;
            float3 pivot = float3.Lerp(StartPose.Pivot, TargetPose.Pivot, interpolationAmount);
            float4 orientation = SlerpShortest(StartPose.Orientation, TargetPose.Orientation, interpolationAmount);
            double distance = StartPose.Distance + ((TargetPose.Distance - StartPose.Distance) * transitionAmount);
            CameraController.SetViewPose(pivot, orientation, distance);
            LastDisplayedPose = CaptureCurrentPose();
            CaptureLastDisplayedProjectionState();
            NotifyCameraStateChanged();
        }

        /// <summary>
        /// Cancels the current transition while leaving the displayed pose untouched.
        /// </summary>
        public void CancelTransition() {
            IsTransitioning = false;
            StartPose = null;
            TargetPose = null;
            LastDisplayedPose = null;
            ElapsedTransitionSeconds = 0.0;
        }

        /// <summary>
        /// Begins an interpolation from a captured displayed pose to one destination pose.
        /// </summary>
        /// <param name="currentPose">Pose currently displayed by the camera.</param>
        /// <param name="targetPose">Pose that the camera should reach.</param>
        void BeginTransition(EditorViewportNavigationPose currentPose, EditorViewportNavigationPose targetPose) {
            StartPose = currentPose ?? throw new ArgumentNullException(nameof(currentPose));
            TargetPose = targetPose ?? throw new ArgumentNullException(nameof(targetPose));
            LastDisplayedPose = currentPose;
            ElapsedTransitionSeconds = 0.0;
            CaptureLastDisplayedProjectionState();
            IsTransitioning = true;
        }

        /// <summary>
        /// Notifies the owning workspace that the scene, gizmo, and picker camera state must be mirrored.
        /// </summary>
        void NotifyCameraStateChanged() {
            CameraStateChanged?.Invoke();
        }

        /// <summary>
        /// Captures the current camera position, orientation, orbit target, and distance as an immutable pose.
        /// </summary>
        /// <returns>Current viewport camera pose.</returns>
        EditorViewportNavigationPose CaptureCurrentPose() {
            CameraComponent camera = CameraController.Camera;
            Entity cameraEntity = camera.Parent;
            if (cameraEntity == null) {
                throw new InvalidOperationException("Viewport navigation requires a camera attached to an entity.");
            }

            float3 pivot = CameraController.GetOrbitTarget();
            float3 offset = cameraEntity.Position - pivot;
            double distance = Math.Sqrt(
                (offset.X * offset.X) +
                (offset.Y * offset.Y) +
                (offset.Z * offset.Z));
            if (!double.IsFinite(distance) || distance < MinimumNavigationDistance) {
                distance = MinimumNavigationDistance;
            }

            return new EditorViewportNavigationPose(pivot, cameraEntity.Orientation, distance);
        }

        /// <summary>
        /// Creates a zero-roll camera orientation whose view ray looks opposite one cube direction.
        /// </summary>
        /// <param name="target">Signed camera offset from the orbit pivot.</param>
        /// <returns>Normalized camera orientation for the selected cube direction.</returns>
        static float4 CreateOrientationForCameraOffset(EditorViewportNavigationTarget target) {
            float3 cameraOffset = NormalizeSafe(new float3(target.X, target.Y, target.Z), CameraUpAxis);
            float3 forward = new float3(-cameraOffset.X, -cameraOffset.Y, -cameraOffset.Z);
            double horizontalLength = Math.Sqrt((forward.X * forward.X) + (forward.Z * forward.Z));
            double yaw = horizontalLength <= 0.000001 ? 0.0 : Math.Atan2(-forward.X, -forward.Z);
            double pitch = Math.Asin(Math.Clamp(forward.Y, -1f, 1f));
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            orientation.Normalize();
            return orientation;
        }

        /// <summary>
        /// Spherically interpolates between unit quaternions along their shortest representation arc.
        /// </summary>
        /// <param name="start">Transition start orientation.</param>
        /// <param name="end">Transition destination orientation.</param>
        /// <param name="amount">Interpolation amount in the inclusive zero-to-one range.</param>
        /// <returns>Normalized interpolated orientation.</returns>
        static float4 SlerpShortest(float4 start, float4 end, float amount) {
            double dot = float4.Dot(start, end);
            float4 adjustedEnd = end;
            if (dot < 0.0) {
                adjustedEnd = new float4(-end.X, -end.Y, -end.Z, -end.W);
                dot = -dot;
            }

            dot = Math.Clamp(dot, -1.0, 1.0);
            if (dot > 0.9995) {
                return float4.Lerp(start, adjustedEnd, amount);
            }

            double angle = Math.Acos(dot);
            double sineOfAngle = Math.Sin(angle);
            double startWeight = Math.Sin((1.0 - amount) * angle) / sineOfAngle;
            double endWeight = Math.Sin(amount * angle) / sineOfAngle;
            float4 result = new float4(
                (float)((start.X * startWeight) + (adjustedEnd.X * endWeight)),
                (float)((start.Y * startWeight) + (adjustedEnd.Y * endWeight)),
                (float)((start.Z * startWeight) + (adjustedEnd.Z * endWeight)),
                (float)((start.W * startWeight) + (adjustedEnd.W * endWeight)));
            result.Normalize();
            return result;
        }

        /// <summary>
        /// Checks whether the camera still displays the pose last written by this transition.
        /// </summary>
        /// <param name="expectedPose">Most recent transition output.</param>
        /// <param name="actualPose">Current camera pose.</param>
        /// <returns>True when the poses differ by no more than the transition tolerance.</returns>
        static bool ArePosesApproximatelyEqual(EditorViewportNavigationPose expectedPose, EditorViewportNavigationPose actualPose) {
            if (expectedPose == null || actualPose == null) {
                return false;
            }

            return AreVectorsApproximatelyEqual(expectedPose.Pivot, actualPose.Pivot) &&
                   AreVectorsApproximatelyEqual(expectedPose.Orientation, actualPose.Orientation) &&
                   Math.Abs(expectedPose.Distance - actualPose.Distance) <= PoseChangeTolerance;
        }

        /// <summary>
        /// Checks whether the selected projection inputs still match the last applied transition state.
        /// </summary>
        /// <returns>True when no external projection change has occurred.</returns>
        bool IsProjectionStateEqualToLastDisplayed() {
            CameraComponent camera = CameraController.Camera;
            ICameraProjectionSettings projectionSettings = camera as ICameraProjectionSettings;
            CameraProjectionMode mode = projectionSettings == null ? CameraProjectionMode.Perspective : projectionSettings.ProjectionMode;
            if (mode != LastDisplayedProjectionMode) {
                return false;
            }
            if (mode == CameraProjectionMode.Orthographic &&
                Math.Abs(projectionSettings.OrthographicVerticalSpan - LastDisplayedOrthographicSpan) > PoseChangeTolerance) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Records the active projection mode and span for future external-change detection.
        /// </summary>
        void CaptureLastDisplayedProjectionState() {
            CameraComponent camera = CameraController.Camera;
            ICameraProjectionSettings projectionSettings = camera as ICameraProjectionSettings;
            LastDisplayedProjectionMode = projectionSettings == null ? CameraProjectionMode.Perspective : projectionSettings.ProjectionMode;
            LastDisplayedOrthographicSpan = projectionSettings == null ? 0.0 : projectionSettings.OrthographicVerticalSpan;
        }

        /// <summary>
        /// Compares two vectors using the configured external-camera-change tolerance.
        /// </summary>
        /// <param name="left">First vector.</param>
        /// <param name="right">Second vector.</param>
        /// <returns>True when each component is within the configured tolerance.</returns>
        static bool AreVectorsApproximatelyEqual(float3 left, float3 right) {
            return Math.Abs(left.X - right.X) <= PoseChangeTolerance &&
                   Math.Abs(left.Y - right.Y) <= PoseChangeTolerance &&
                   Math.Abs(left.Z - right.Z) <= PoseChangeTolerance;
        }

        /// <summary>
        /// Compares two quaternions while accepting the negated representation of the same rotation.
        /// </summary>
        /// <param name="left">First orientation.</param>
        /// <param name="right">Second orientation.</param>
        /// <returns>True when the orientation components represent the same rotation.</returns>
        static bool AreVectorsApproximatelyEqual(float4 left, float4 right) {
            double directError = Math.Max(
                Math.Max(Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y)),
                Math.Max(Math.Abs(left.Z - right.Z), Math.Abs(left.W - right.W)));
            double negatedError = Math.Max(
                Math.Max(Math.Abs(left.X + right.X), Math.Abs(left.Y + right.Y)),
                Math.Max(Math.Abs(left.Z + right.Z), Math.Abs(left.W + right.W)));
            return Math.Min(directError, negatedError) <= PoseChangeTolerance;
        }

        /// <summary>
        /// Normalizes a vector or returns a fallback when its magnitude is too small.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <param name="fallback">Fallback direction returned for near-zero vectors.</param>
        /// <returns>Normalized vector when valid; otherwise the fallback value.</returns>
        static float3 NormalizeSafe(float3 value, float3 fallback) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Y * value.Y) +
                (value.Z * value.Z);
            if (lengthSquared <= 0.000000000001) {
                return fallback;
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                (float)(value.Y * inverseLength),
                (float)(value.Z * inverseLength));
        }

        /// <summary>
        /// Maximum absolute pitch that an interactive orbit may reach before clamping away from a pole.
        /// </summary>
        const double MaximumOrbitPitch = (Math.PI * 0.5) - 0.001;
    }
}

namespace helengine.editor {
    /// <summary>
    /// Moves a camera entity when the viewport is right-click active and WASDQE input is pressed.
    /// </summary>
    public class EditorViewportCameraController : UpdateComponent {
        /// <summary>
        /// Default movement speed applied per update tick.
        /// </summary>
        public const float DefaultMoveSpeed = 0.15f;
        /// <summary>
        /// Mouse-look sensitivity in radians per pixel.
        /// </summary>
        public const double DefaultLookSensitivity = 0.003;
        /// <summary>
        /// Pan speed in world units per pixel.
        /// </summary>
        public const double DefaultPanSpeed = 0.01;
        /// <summary>
        /// Default wheel zoom speed in world units per scroll-wheel notch.
        /// </summary>
        public const double DefaultWheelZoomSpeed = 1.0;
        /// <summary>
        /// Default orbit distance used to derive a virtual target before the user selects one.
        /// </summary>
        public const double DefaultOrbitDistance = 10.0;

        /// <summary>
        /// Minimum length squared used to avoid normalizing a zero vector.
        /// </summary>
        const double MinLengthSquared = 0.000001;
        /// <summary>
        /// Minimum orbit distance allowed between the camera and its orbit pivot.
        /// </summary>
        const double MinOrbitDistance = 0.1;
        /// <summary>
        /// Maximum pitch angle in radians to avoid gimbal lock.
        /// </summary>
        const double MaxPitch = (Math.PI * 0.5) - 0.001;
        /// <summary>
        /// Standard scroll-wheel delta emitted for one wheel notch on Windows-compatible mice.
        /// </summary>
        const double WheelDeltaPerNotch = 120.0;

        /// <summary>
        /// World up axis used for vertical movement.
        /// </summary>
        static readonly float3 WorldUp = new float3(0f, 1f, 0f);

        /// <summary>
        /// Forward axis used as the camera basis before rotation.
        /// </summary>
        static readonly float3 ForwardAxis = new float3(0f, 0f, -1f);

        /// <summary>
        /// Camera supplying viewport bounds for activation checks.
        /// </summary>
        readonly CameraComponent camera;

        /// <summary>
        /// Tracks whether a right-click started inside the viewport.
        /// </summary>
        bool isActive;
        /// <summary>
        /// Tracks whether a middle-click pan started inside the viewport.
        /// </summary>
        bool isPanning;
        /// <summary>
        /// Tracks whether an Alt plus middle-click orbit started inside the viewport.
        /// </summary>
        bool isOrbiting;
        /// <summary>
        /// Tracks whether the first look delta after activation should be ignored.
        /// </summary>
        bool ignoreNextLookDelta;
        /// <summary>
        /// Tracks whether the first pan delta after activation should be ignored.
        /// </summary>
        bool ignoreNextPanDelta;
        /// <summary>
        /// Tracks whether the first orbit delta after activation should be ignored.
        /// </summary>
        bool ignoreNextOrbitDelta;
        /// <summary>
        /// Tracks whether yaw and pitch have been initialized from the current orientation.
        /// </summary>
        bool hasOrientationState;
        /// <summary>
        /// Tracks whether the controller already has a virtual orbit target.
        /// </summary>
        bool hasVirtualTargetState;
        /// <summary>
        /// Current yaw angle in radians.
        /// </summary>
        double yaw;
        /// <summary>
        /// Current pitch angle in radians.
        /// </summary>
        double pitch;
        /// <summary>
        /// Current orbit distance from the camera to the orbit target.
        /// </summary>
        double orbitDistance;
        /// <summary>
        /// Current virtual target used when no scene entity is selected.
        /// </summary>
        float3 virtualTarget;

        /// <summary>
        /// Initializes a new controller for the specified camera.
        /// </summary>
        /// <param name="camera">Camera to move.</param>
        public EditorViewportCameraController(CameraComponent camera) {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            MoveSpeed = DefaultMoveSpeed;
            LookSensitivity = DefaultLookSensitivity;
            PanSpeed = DefaultPanSpeed;
            WheelZoomSpeed = DefaultWheelZoomSpeed;
            orbitDistance = DefaultOrbitDistance;
        }

        /// <summary>
        /// Gets the camera managed by this controller.
        /// </summary>
        public CameraComponent Camera => camera;

        /// <summary>
        /// Gets or sets the movement speed applied per update tick.
        /// </summary>
        public float MoveSpeed { get; set; }
        /// <summary>
        /// Gets or sets the mouse-look sensitivity in radians per pixel.
        /// </summary>
        public double LookSensitivity { get; set; }
        /// <summary>
        /// Gets or sets the pan speed in world units per pixel.
        /// </summary>
        public double PanSpeed { get; set; }
        /// <summary>
        /// Gets or sets the zoom speed in world units per scroll-wheel notch.
        /// </summary>
        public double WheelZoomSpeed { get; set; }

        /// <summary>
        /// Updates camera position based on right-click state and keyboard input.
        /// </summary>
        public override void Update() {
            InputManager input = Core.Instance.InputManager;
            bool isPointerBlocked = EditorInputCaptureService.IsPointerBlocked(input.GetMousePosition());

            if (!hasOrientationState) {
                InitializeYawPitchFromOrientation();
                hasOrientationState = true;
            }

            if (!hasVirtualTargetState) {
                UpdateVirtualTargetFromCamera();
                hasVirtualTargetState = true;
            }

            if (input.WasMouseRightButtonPressed()) {
                if (isPointerBlocked) {
                    isActive = false;
                    ignoreNextLookDelta = false;
                } else {
                    isActive = IsPointerInsideViewport(input);
                    if (isActive) {
                        ignoreNextLookDelta = true;
                    }
                }
            }

            if (input.WasMouseRightButtonReleased() || input.GetMouseRightButtonState() == ButtonState.Released) {
                isActive = false;
                ignoreNextLookDelta = false;
            }

            if (input.WasMouseMiddleButtonPressed()) {
                if (isPointerBlocked) {
                    isPanning = false;
                    isOrbiting = false;
                    ignoreNextPanDelta = false;
                    ignoreNextOrbitDelta = false;
                } else {
                    bool isPointerInsideViewport = IsPointerInsideViewport(input);
                    if (IsOrbitModifierDown(input)) {
                        isPanning = false;
                        ignoreNextPanDelta = false;
                        isOrbiting = isPointerInsideViewport;
                        if (isOrbiting) {
                            ResolveOrbitTarget();
                            ignoreNextOrbitDelta = true;
                        }
                    } else {
                        isOrbiting = false;
                        ignoreNextOrbitDelta = false;
                        isPanning = isPointerInsideViewport;
                        if (isPanning) {
                            ignoreNextPanDelta = true;
                        }
                    }
                }
            }

            if (input.WasMouseMiddleButtonReleased() || input.GetMouseMiddleButtonState() == ButtonState.Released) {
                isPanning = false;
                isOrbiting = false;
                ignoreNextPanDelta = false;
                ignoreNextOrbitDelta = false;
            }

            UpdatePointerWrapState(input);

            if (isActive) {
                ApplyMouseLook(input);
            }

            float3 forward = GetForward(Parent.Orientation);
            float3 right = NormalizeSafe(float3.Cross(forward, WorldUp), new float3(1f, 0f, 0f));
            float3 up = NormalizeSafe(float3.Cross(right, forward), WorldUp);

            ApplyWheelZoom(input, isPointerBlocked, forward);

            if (isOrbiting) {
                ApplyOrbit(input);
                forward = GetForward(Parent.Orientation);
                right = NormalizeSafe(float3.Cross(forward, WorldUp), new float3(1f, 0f, 0f));
                up = NormalizeSafe(float3.Cross(right, forward), WorldUp);
            }

            if (!isActive && !isPanning && !isOrbiting) {
                return;
            }

            if (isPanning) {
                if (ignoreNextPanDelta) {
                    ignoreNextPanDelta = false;
                } else {
                    int2 delta = input.GetMouseDelta();
                    if (delta.X != 0 || delta.Y != 0) {
                        double panScale = PanSpeed;
                        float3 panMove =
                            right * (float)(-delta.X * panScale) +
                            up * (float)(delta.Y * panScale);
                        Parent.Position += panMove;
                        virtualTarget += panMove;
                    }
                }
            }

            float3 move = BuildMovement(input, forward, right);
            double lengthSquared = (move.X * move.X) + (move.Y * move.Y) + (move.Z * move.Z);
            if (lengthSquared <= MinLengthSquared) {
                return;
            }

            move = NormalizeSafe(move, forward);
            Parent.Position += move * MoveSpeed;
            if (isActive) {
                UpdateVirtualTargetFromCamera();
            }
        }

        /// <summary>
        /// Applies scroll-wheel movement as a forward or backward camera zoom.
        /// </summary>
        /// <param name="input">Input manager providing scroll-wheel delta and pointer position.</param>
        /// <param name="isPointerBlocked">True when UI is currently blocking viewport input.</param>
        /// <param name="forward">Current camera forward axis.</param>
        void ApplyWheelZoom(InputManager input, bool isPointerBlocked, float3 forward) {
            int wheelDelta = input.GetMouseScrollWheelDelta();
            if (wheelDelta == 0) {
                return;
            }
            if (isPointerBlocked) {
                return;
            }
            if (!IsPointerInsideViewport(input)) {
                return;
            }

            double notchDelta = wheelDelta / WheelDeltaPerNotch;
            double zoomDistance = notchDelta * WheelZoomSpeed;
            Parent.Position += forward * (float)zoomDistance;
            UpdateOrbitDistanceFromTarget();
        }

        /// <summary>
        /// Applies mouse delta to camera yaw and pitch while right click is held.
        /// </summary>
        /// <param name="input">Input manager providing mouse delta.</param>
        void ApplyMouseLook(InputManager input) {
            if (ignoreNextLookDelta) {
                ignoreNextLookDelta = false;
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            yaw -= delta.X * LookSensitivity;
            pitch -= delta.Y * LookSensitivity;
            pitch = Math.Clamp(pitch, -MaxPitch, MaxPitch);

            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            orientation.Normalize();
            Parent.Orientation = orientation;
            UpdateVirtualTargetFromCamera();
        }

        /// <summary>
        /// Enables client-edge pointer wrapping while camera navigation is active.
        /// </summary>
        /// <param name="input">Input manager receiving the desired pointer-wrap state.</param>
        void UpdatePointerWrapState(InputManager input) {
            if (isActive || isPanning || isOrbiting) {
                input.RequestPointerWrapEnabled();
            }
        }

        /// <summary>
        /// Synchronizes yaw and pitch values from the current camera orientation.
        /// </summary>
        void InitializeYawPitchFromOrientation() {
            float3 forward = GetForward(Parent.Orientation);
            yaw = Math.Atan2(forward.X, -forward.Z);
            pitch = Math.Asin(forward.Y);
            pitch = Math.Clamp(pitch, -MaxPitch, MaxPitch);
        }

        /// <summary>
        /// Applies orbit deltas to the camera while keeping the orbit pivot fixed.
        /// </summary>
        /// <param name="input">Input manager providing mouse delta.</param>
        void ApplyOrbit(InputManager input) {
            if (ignoreNextOrbitDelta) {
                ignoreNextOrbitDelta = false;
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            yaw -= delta.X * LookSensitivity;
            pitch -= delta.Y * LookSensitivity;
            pitch = Math.Clamp(pitch, -MaxPitch, MaxPitch);

            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            orientation.Normalize();
            Parent.Orientation = orientation;

            float3 forward = GetForward(orientation);
            float3 target = ResolveOrbitTarget();
            Parent.Position = target - (forward * (float)orbitDistance);
        }

        /// <summary>
        /// Determines whether the current input state requests orbit instead of pan.
        /// </summary>
        /// <param name="input">Input manager used to query modifier keys.</param>
        /// <returns>True when either Alt key is pressed.</returns>
        bool IsOrbitModifierDown(InputManager input) {
            return input.IsKeyDown(Keys.LeftAlt) || input.IsKeyDown(Keys.RightAlt);
        }

        /// <summary>
        /// Resolves the active orbit target from the selection or the stored virtual target.
        /// </summary>
        /// <returns>World-space target position used for orbit interactions.</returns>
        float3 ResolveOrbitTarget() {
            Entity selectedEntity = EditorSelectionService.SelectedEntity;
            if (selectedEntity != null) {
                virtualTarget = selectedEntity.Position;
                orbitDistance = GetDistance(Parent.Position, virtualTarget);
                if (orbitDistance < MinOrbitDistance) {
                    orbitDistance = MinOrbitDistance;
                }
            }

            hasVirtualTargetState = true;
            return virtualTarget;
        }

        /// <summary>
        /// Updates the stored virtual target so it remains in front of the camera at the current orbit distance.
        /// </summary>
        void UpdateVirtualTargetFromCamera() {
            float3 forward = GetForward(Parent.Orientation);
            virtualTarget = Parent.Position + (forward * (float)orbitDistance);
            hasVirtualTargetState = true;
        }

        /// <summary>
        /// Recomputes orbit distance against the current virtual target after the camera position changes.
        /// </summary>
        void UpdateOrbitDistanceFromTarget() {
            float3 target = ResolveOrbitTarget();
            orbitDistance = GetDistance(Parent.Position, target);
            if (orbitDistance < MinOrbitDistance) {
                orbitDistance = MinOrbitDistance;
            }
        }

        /// <summary>
        /// Computes the world-space distance between two positions.
        /// </summary>
        /// <param name="left">First world position.</param>
        /// <param name="right">Second world position.</param>
        /// <returns>Distance between the supplied positions.</returns>
        double GetDistance(float3 left, float3 right) {
            double deltaX = left.X - right.X;
            double deltaY = left.Y - right.Y;
            double deltaZ = left.Z - right.Z;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }

        /// <summary>
        /// Determines whether the cursor is within the camera viewport.
        /// </summary>
        /// <param name="input">Input manager used for cursor position.</param>
        /// <returns>True when the cursor is inside the viewport rectangle.</returns>
        bool IsPointerInsideViewport(InputManager input) {
            int2 mouse = input.GetMousePosition();
            float4 vp = camera.Viewport;

            if (vp.Z <= 1f || vp.W <= 1f) {
                return false;
            }

            float x = mouse.X;
            float y = mouse.Y;

            return x >= vp.X && x <= vp.X + vp.Z &&
                   y >= vp.Y && y <= vp.Y + vp.W;
        }

        /// <summary>
        /// Computes a forward vector based on the camera orientation.
        /// </summary>
        /// <param name="orientation">Camera orientation quaternion.</param>
        /// <returns>Normalized forward vector.</returns>
        float3 GetForward(float4 orientation) {
            float3 direction = float4.RotateVector(ForwardAxis, orientation);
            return NormalizeSafe(direction, ForwardAxis);
        }

        /// <summary>
        /// Normalizes a vector or returns a fallback when its length is too small.
        /// </summary>
        /// <param name="value">Vector to normalize.</param>
        /// <param name="fallback">Fallback direction.</param>
        /// <returns>Normalized vector or fallback.</returns>
        float3 NormalizeSafe(float3 value, float3 fallback) {
            double lengthSquared = (value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z);
            if (lengthSquared <= MinLengthSquared) {
                return fallback;
            }

            double invLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3((float)(value.X * invLength), (float)(value.Y * invLength), (float)(value.Z * invLength));
        }

        /// <summary>
        /// Builds a movement direction vector based on keyboard input.
        /// </summary>
        /// <param name="input">Input manager used to query keys.</param>
        /// <param name="forward">Forward axis.</param>
        /// <param name="right">Right axis.</param>
        /// <returns>Combined movement direction.</returns>
        float3 BuildMovement(InputManager input, float3 forward, float3 right) {
            float3 move = float3.Zero;

            if (input.IsKeyDown(Keys.W)) {
                move += forward;
            }
            if (input.IsKeyDown(Keys.S)) {
                move -= forward;
            }
            if (input.IsKeyDown(Keys.D)) {
                move += right;
            }
            if (input.IsKeyDown(Keys.A)) {
                move -= right;
            }
            if (input.IsKeyDown(Keys.E)) {
                move += WorldUp;
            }
            if (input.IsKeyDown(Keys.Q)) {
                move -= WorldUp;
            }

            return move;
        }
    }
}

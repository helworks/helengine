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
        /// Minimum length squared used to avoid normalizing a zero vector.
        /// </summary>
        const double MinLengthSquared = 0.000001;
        /// <summary>
        /// Maximum pitch angle in radians to avoid gimbal lock.
        /// </summary>
        const double MaxPitch = (Math.PI * 0.5) - 0.001;

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
        /// Tracks whether the first look delta after activation should be ignored.
        /// </summary>
        bool ignoreNextLookDelta;
        /// <summary>
        /// Tracks whether the first pan delta after activation should be ignored.
        /// </summary>
        bool ignoreNextPanDelta;
        /// <summary>
        /// Last mouse position recorded for look deltas.
        /// </summary>
        int2 lastLookPosition;
        /// <summary>
        /// Last mouse position recorded for pan deltas.
        /// </summary>
        int2 lastPanPosition;
        /// <summary>
        /// Tracks whether yaw and pitch have been initialized from the current orientation.
        /// </summary>
        bool hasOrientationState;
        /// <summary>
        /// Current yaw angle in radians.
        /// </summary>
        double yaw;
        /// <summary>
        /// Current pitch angle in radians.
        /// </summary>
        double pitch;

        /// <summary>
        /// Initializes a new controller for the specified camera.
        /// </summary>
        /// <param name="camera">Camera to move.</param>
        public EditorViewportCameraController(CameraComponent camera) {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            MoveSpeed = DefaultMoveSpeed;
            LookSensitivity = DefaultLookSensitivity;
            PanSpeed = DefaultPanSpeed;
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
        /// Updates camera position based on right-click state and keyboard input.
        /// </summary>
        public override void Update() {
            InputManager input = Core.Instance.InputManager;
            bool isPointerBlocked = EditorInputCaptureService.IsPointerBlocked(input.GetMousePosition());

            if (!hasOrientationState) {
                InitializeYawPitchFromOrientation();
                hasOrientationState = true;
            }

            if (input.WasMouseRightButtonPressed()) {
                if (isPointerBlocked) {
                    isActive = false;
                    ignoreNextLookDelta = false;
                } else {
                    isActive = IsPointerInsideViewport(input);
                    if (isActive) {
                        lastLookPosition = input.GetMousePosition();
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
                    ignoreNextPanDelta = false;
                } else {
                    isPanning = IsPointerInsideViewport(input);
                    if (isPanning) {
                        lastPanPosition = input.GetMousePosition();
                        ignoreNextPanDelta = true;
                    }
                }
            }

            if (input.WasMouseMiddleButtonReleased() || input.GetMouseMiddleButtonState() == ButtonState.Released) {
                isPanning = false;
                ignoreNextPanDelta = false;
            }

            if (!isActive) {
                if (!isPanning) {
                    return;
                }
            } else {
                ApplyMouseLook(input);
            }

            float3 forward = GetForward(Parent.Orientation);
            float3 right = NormalizeSafe(float3.Cross(forward, WorldUp), new float3(1f, 0f, 0f));
            float3 up = NormalizeSafe(float3.Cross(right, forward), WorldUp);

            if (isPanning) {
                if (ignoreNextPanDelta) {
                    lastPanPosition = input.GetMousePosition();
                    ignoreNextPanDelta = false;
                } else {
                    int2 current = input.GetMousePosition();
                    int2 delta = new int2(current.X - lastPanPosition.X, current.Y - lastPanPosition.Y);
                    lastPanPosition = current;
                    if (delta.X != 0 || delta.Y != 0) {
                        double panScale = PanSpeed;
                        float3 panMove =
                            right * (float)(-delta.X * panScale) +
                            up * (float)(delta.Y * panScale);
                        Parent.Position += panMove;
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
        }

        /// <summary>
        /// Applies mouse delta to camera yaw and pitch while right click is held.
        /// </summary>
        /// <param name="input">Input manager providing mouse delta.</param>
        void ApplyMouseLook(InputManager input) {
            if (ignoreNextLookDelta) {
                lastLookPosition = input.GetMousePosition();
                ignoreNextLookDelta = false;
                return;
            }

            int2 current = input.GetMousePosition();
            int2 delta = new int2(current.X - lastLookPosition.X, current.Y - lastLookPosition.Y);
            lastLookPosition = current;
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

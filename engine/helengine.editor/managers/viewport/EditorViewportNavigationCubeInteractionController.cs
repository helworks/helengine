namespace helengine.editor {
    /// <summary>
    /// Owns pointer classification, click-versus-orbit behavior, and viewport input capture for one navigation cube.
    /// </summary>
    public sealed class EditorViewportNavigationCubeInteractionController : IDisposable {
        /// <summary>Gets the logical edge length of the cube before UI scaling.</summary>
        public const int LogicalCubeSize = 96;
        /// <summary>Gets the logical gap between the viewport edge and the cube.</summary>
        public const int LogicalContentMargin = 8;
        /// <summary>Gets the logical height of the projection control.</summary>
        public const int LogicalProjectionControlHeight = 24;
        /// <summary>Gets the logical width of the projection control.</summary>
        public const int LogicalProjectionControlWidth = 72;
        /// <summary>Gets the logical pointer distance required to begin an orbit drag.</summary>
        public const int LogicalOrbitThreshold = 4;
        /// <summary>Gets the radius of the central projection-toggle hotspot in logical cube pixels.</summary>
        public const int LogicalCenterToggleRadius = 11;
        /// <summary>Logical spacing between the cube and the projection row.</summary>
        const int LogicalProjectionControlGap = 8;
        /// <summary>Full camera-orbit turn in radians across one cube edge.</summary>
        const double OrbitRadiansPerCube = Math.PI * 2.0;
        /// <summary>Camera navigation state changed by clicks and drags.</summary>
        readonly EditorViewportNavigationController NavigationController;
        /// <summary>Session input blockers used to give the cube priority over the scene and gizmos.</summary>
        readonly EditorInputCaptureService InputCapture;
        /// <summary>Blocker owner for the visible cube area.</summary>
        readonly object CubeInputBlockerOwner = new object();
        /// <summary>Blocker owner for the projection control area.</summary>
        readonly object ProjectionControlInputBlockerOwner = new object();
        /// <summary>Blocker owner for the non-interactive destination-preview pill.</summary>
        readonly object HoverPreviewInputBlockerOwner = new object();
        /// <summary>Blocker owner for the viewport while a cube drag is active.</summary>
        readonly object CapturedViewportInputBlockerOwner = new object();
        /// <summary>Pure geometry shared with the overlay renderer.</summary>
        readonly EditorViewportNavigationCubeGeometry Geometry = new EditorViewportNavigationCubeGeometry();
        /// <summary>Window-space origin of the scene-camera content rectangle.</summary>
        int2 ViewportPosition;
        /// <summary>Device-pixel size of the scene-camera content rectangle.</summary>
        int2 ContentSize;
        /// <summary>Window-space origin of the cube bounds.</summary>
        int2 CubePosition;
        /// <summary>Device-pixel size of the cube bounds.</summary>
        int2 CubeSize;
        /// <summary>Window-space origin of the projection control.</summary>
        int2 ProjectionControlPosition;
        /// <summary>Device-pixel size of the projection control.</summary>
        int2 ProjectionControlSize;
        /// <summary>Current multiplier applied to logical UI coordinates.</summary>
        float UiScale;
        /// <summary>Window-space pointer position at the beginning of the current gesture.</summary>
        int2 PressStartPosition;
        /// <summary>Window-space pointer position from the previous captured update.</summary>
        int2 PreviousPointerPosition;
        /// <summary>Cube target pressed at the beginning of the current gesture.</summary>
        EditorViewportNavigationTarget PressedTarget;
        /// <summary>Tracks whether the current gesture began on the projection control.</summary>
        bool PressedProjectionControl;
        /// <summary>Tracks whether the current gesture began on the central projection-toggle hotspot.</summary>
        bool PressedCenterToggle;
        /// <summary>Tracks whether the current gesture crossed the movement threshold.</summary>
        bool HasDragged;
        /// <summary>Tracks whether the controller has been disposed.</summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes pointer interaction for one viewport navigation cube.
        /// </summary>
        /// <param name="navigationController">Viewport-local camera navigation state.</param>
        /// <param name="inputCapture">Editor session's pointer blocker service.</param>
        public EditorViewportNavigationCubeInteractionController(EditorViewportNavigationController navigationController, EditorInputCaptureService inputCapture) {
            NavigationController = navigationController ?? throw new ArgumentNullException(nameof(navigationController));
            InputCapture = inputCapture ?? throw new ArgumentNullException(nameof(inputCapture));
        }

        /// <summary>Projection used to classify pointer positions on the rendered cube.</summary>
        public CameraProjectionMode ProjectionMode { get => Geometry.ProjectionMode; set => Geometry.ProjectionMode = value; }

        /// <summary>Gets whether the cube bounds fit in the current viewport.</summary>
        public bool IsVisible { get; private set; }
        /// <summary>Gets whether the projection row fits beneath the cube.</summary>
        public bool IsProjectionControlVisible { get; private set; }
        /// <summary>Gets whether the cube owns the pointer through release or cancellation.</summary>
        public bool IsPointerCaptured { get; private set; }
        /// <summary>Gets whether an active cube drag currently orbits the camera.</summary>
        public bool IsOrbiting { get; private set; }
        /// <summary>Gets whether the pointer is over the projection control.</summary>
        public bool IsProjectionControlHovered { get; private set; }
        /// <summary>Gets whether the pointer is over the center hotspot that toggles projection.</summary>
        public bool IsCenterToggleHovered { get; private set; }
        /// <summary>Gets the currently hovered visible face, edge, or corner target.</summary>
        public EditorViewportNavigationTarget HoveredTarget { get; private set; }
        /// <summary>Gets the cube origin in window-space device pixels.</summary>
        public int2 CubeScreenPosition => CubePosition;
        /// <summary>Gets the cube size in device pixels.</summary>
        public int2 CubeScreenSize => CubeSize;
        /// <summary>Gets the projection control origin in window-space device pixels.</summary>
        public int2 ProjectionControlScreenPosition => ProjectionControlPosition;
        /// <summary>Gets the projection control size in device pixels.</summary>
        public int2 ProjectionControlScreenSize => ProjectionControlSize;

        /// <summary>
        /// Recomputes the overlay bounds from viewport content size, screen origin, and UI scale.
        /// </summary>
        /// <param name="viewportPosition">Window-space origin of the camera content viewport.</param>
        /// <param name="contentSize">Device-pixel dimensions available to viewport content.</param>
        /// <param name="uiScale">Effective editor UI scale multiplier.</param>
        public void Resize(int2 viewportPosition, int2 contentSize, float uiScale) {
            EnsureNotDisposed();
            if (contentSize.X < 0 || contentSize.Y < 0) {
                throw new ArgumentOutOfRangeException(nameof(contentSize), "Viewport content dimensions must be non-negative.");
            }
            if (!float.IsFinite(uiScale) || uiScale <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(uiScale), "UI scale must be finite and positive.");
            }
            bool layoutChanged = ViewportPosition.X != viewportPosition.X ||
                                 ViewportPosition.Y != viewportPosition.Y ||
                                 ContentSize.X != contentSize.X ||
                                 ContentSize.Y != contentSize.Y ||
                                 UiScale != uiScale;
            if (IsPointerCaptured && layoutChanged) {
                CancelInteraction();
            }

            ViewportPosition = viewportPosition;
            ContentSize = contentSize;
            UiScale = uiScale;
            int margin = ScaleLogicalPixels(LogicalContentMargin);
            CubeSize = new int2(ScaleLogicalPixels(LogicalCubeSize), ScaleLogicalPixels(LogicalCubeSize));
            ProjectionControlSize = new int2(
                ScaleLogicalPixels(LogicalProjectionControlWidth),
                ScaleLogicalPixels(LogicalProjectionControlHeight));
            IsVisible = contentSize.X >= CubeSize.X + (margin * 2) &&
                        contentSize.Y >= CubeSize.Y + (margin * 2);
            int controlGap = ScaleLogicalPixels(LogicalProjectionControlGap);
            IsProjectionControlVisible = IsVisible &&
                contentSize.Y >= margin + CubeSize.Y + controlGap + ProjectionControlSize.Y + margin;

            if (!IsVisible) {
                IsCenterToggleHovered = false;
                HoveredTarget = null;
                IsProjectionControlHovered = false;
                ClearLayoutBlockers();
                return;
            }

            CubePosition = new int2(
                viewportPosition.X + contentSize.X - margin - CubeSize.X,
                viewportPosition.Y + margin);
            ProjectionControlPosition = new int2(
                CubePosition.X + ((CubeSize.X - ProjectionControlSize.X) / 2),
                CubePosition.Y + CubeSize.Y + controlGap);
            UpdateLayoutBlockers();
        }

        /// <summary>
        /// Blocks scene and gizmo input under the visible destination preview without claiming it as a control.
        /// </summary>
        /// <param name="position">Window-space origin of the preview bounds.</param>
        /// <param name="size">Device-pixel dimensions of the preview bounds, or zero to clear it.</param>
        public void SetHoverPreviewBounds(int2 position, int2 size) {
            EnsureNotDisposed();
            if (size.X < 0 || size.Y < 0) {
                throw new ArgumentOutOfRangeException(nameof(size), "Preview bounds must be non-negative.");
            }

            if (!IsVisible || size.X == 0 || size.Y == 0) {
                InputCapture.ClearBlocker(HoverPreviewInputBlockerOwner);
                return;
            }

            InputCapture.SetBlocker(HoverPreviewInputBlockerOwner, position, size);
        }

        /// <summary>
        /// Advances view transitions and routes one pointer frame through click, orbit, and projection behavior.
        /// </summary>
        /// <param name="pointerPosition">Pointer position in window coordinates.</param>
        /// <param name="wasPrimaryPressed">True when the primary pointer button went down this frame.</param>
        /// <param name="isPrimaryPressed">True while the primary pointer button remains down.</param>
        /// <param name="hasForegroundFocus">True while the editor host retains pointer focus.</param>
        /// <param name="cameraOrientation">Current scene-camera orientation used for hit testing.</param>
        /// <param name="elapsedSeconds">Elapsed frame duration used by camera transitions.</param>
        public void UpdatePointer(int2 pointerPosition, bool wasPrimaryPressed, bool isPrimaryPressed, bool hasForegroundFocus, float4 cameraOrientation, double elapsedSeconds) {
            EnsureNotDisposed();
            if (!hasForegroundFocus) {
                CancelInteraction();
                IsCenterToggleHovered = false;
                HoveredTarget = null;
                IsProjectionControlHovered = false;
                return;
            }

            NavigationController.Advance(elapsedSeconds);
            if (!IsVisible) {
                return;
            }

            UpdateHover(pointerPosition, cameraOrientation);
            if (IsPointerCaptured) {
                if (!isPrimaryPressed) {
                    CompletePointerGesture(pointerPosition, cameraOrientation);
                    ReleaseViewportCapture();
                    UpdateLayoutBlockers();
                    UpdateHover(pointerPosition, cameraOrientation);
                    return;
                }

                AdvanceCapturedGesture(pointerPosition);
                return;
            }

            if (!wasPrimaryPressed || IsPointerBlockedByAnotherOwner(pointerPosition)) {
                return;
            }

            BeginPointerGesture(pointerPosition, cameraOrientation);
        }

        /// <summary>
        /// Cancels a pending click or orbit and clears its temporary full-viewport capture region.
        /// </summary>
        public void CancelInteraction() {
            if (IsDisposed) {
                return;
            }

            ReleaseViewportCapture();
        }

        /// <summary>
        /// Releases every registered blocker and prevents future pointer updates.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            CancelInteraction();
            ClearLayoutBlockers();
            IsDisposed = true;
        }

        /// <summary>
        /// Captures a press on a visible cube target or on the projection control.
        /// </summary>
        /// <param name="pointerPosition">Window-space press position.</param>
        /// <param name="cameraOrientation">Camera orientation used to resolve the target.</param>
        void BeginPointerGesture(int2 pointerPosition, float4 cameraOrientation) {
            bool isProjectionControl = IsProjectionControlVisible &&
                ContainsPoint(pointerPosition, ProjectionControlPosition, ProjectionControlSize);
            EditorViewportNavigationTarget target = null;
            bool isCenterToggle = false;
            if (!isProjectionControl && ContainsPoint(pointerPosition, CubePosition, CubeSize)) {
                float2 localPointer = GetCubeLocalPointer(pointerPosition);
                isCenterToggle = IsCenterTogglePoint(localPointer);
                if (!isCenterToggle && Geometry.TryHit(localPointer, cameraOrientation, LogicalCubeSize, out EditorViewportNavigationCubeHit hit)) {
                    target = hit.Target;
                }
            }
            if (!isProjectionControl && !isCenterToggle && target == null) {
                return;
            }

            IsPointerCaptured = true;
            PressStartPosition = pointerPosition;
            PreviousPointerPosition = pointerPosition;
            PressedProjectionControl = isProjectionControl;
            PressedCenterToggle = isCenterToggle;
            PressedTarget = target;
            HasDragged = false;
            IsOrbiting = false;
            InputCapture.SetBlocker(CapturedViewportInputBlockerOwner, ViewportPosition, ContentSize);
        }

        /// <summary>
        /// Starts orbit movement when the pointer leaves its four-logical-pixel click threshold.
        /// </summary>
        /// <param name="pointerPosition">Current pointer position in window coordinates.</param>
        void AdvanceCapturedGesture(int2 pointerPosition) {
            double deltaXFromPress = pointerPosition.X - PressStartPosition.X;
            double deltaYFromPress = pointerPosition.Y - PressStartPosition.Y;
            double threshold = LogicalOrbitThreshold * UiScale;
            double distanceFromPressSquared = (deltaXFromPress * deltaXFromPress) + (deltaYFromPress * deltaYFromPress);
            if (!HasDragged && distanceFromPressSquared >= threshold * threshold) {
                HasDragged = true;
                IsOrbiting = !PressedProjectionControl;
            }
            if (IsOrbiting) {
                double radiansPerPixel = OrbitRadiansPerCube / CubeSize.X;
                float2 orbitDelta = new float2(
                    (float)((pointerPosition.X - PreviousPointerPosition.X) * radiansPerPixel),
                    (float)((pointerPosition.Y - PreviousPointerPosition.Y) * radiansPerPixel));
                NavigationController.Orbit(orbitDelta);
            }

            PreviousPointerPosition = pointerPosition;
        }

        /// <summary>
        /// Applies a click only when release resolves back to the originally pressed target.
        /// </summary>
        /// <param name="pointerPosition">Window-space release position.</param>
        /// <param name="cameraOrientation">Current camera orientation at release.</param>
        void CompletePointerGesture(int2 pointerPosition, float4 cameraOrientation) {
            if (HasDragged) {
                return;
            }
            if (PressedProjectionControl) {
                if (IsProjectionControlVisible && ContainsPoint(pointerPosition, ProjectionControlPosition, ProjectionControlSize)) {
                    NavigationController.ToggleProjection();
                }
                return;
            }
            if (PressedCenterToggle) {
                if (ContainsPoint(pointerPosition, CubePosition, CubeSize) &&
                    IsCenterTogglePoint(GetCubeLocalPointer(pointerPosition))) {
                    NavigationController.ToggleProjection();
                }
                return;
            }
            if (PressedTarget == null || !ContainsPoint(pointerPosition, CubePosition, CubeSize)) {
                return;
            }

            float2 localPointer = GetCubeLocalPointer(pointerPosition);
            if (IsCenterTogglePoint(localPointer)) {
                return;
            }
            if (Geometry.TryHit(localPointer, cameraOrientation, LogicalCubeSize, out EditorViewportNavigationCubeHit releaseHit) &&
                PressedTarget.Equals(releaseHit.Target)) {
                NavigationController.SelectTarget(releaseHit.Target);
            }
        }

        /// <summary>
        /// Refreshes hovered target state from the same geometry used for click resolution.
        /// </summary>
        /// <param name="pointerPosition">Window-space pointer position.</param>
        /// <param name="cameraOrientation">Current camera orientation.</param>
        void UpdateHover(int2 pointerPosition, float4 cameraOrientation) {
            IsProjectionControlHovered = IsProjectionControlVisible &&
                ContainsPoint(pointerPosition, ProjectionControlPosition, ProjectionControlSize);
            IsCenterToggleHovered = false;
            HoveredTarget = null;
            if (!ContainsPoint(pointerPosition, CubePosition, CubeSize)) {
                return;
            }

            float2 localPointer = GetCubeLocalPointer(pointerPosition);
            if (IsCenterTogglePoint(localPointer)) {
                IsCenterToggleHovered = true;
                return;
            }
            if (Geometry.TryHit(localPointer, cameraOrientation, LogicalCubeSize, out EditorViewportNavigationCubeHit hit)) {
                HoveredTarget = hit.Target;
            }
        }

        /// <summary>Determines whether a logical cube point lies in the central projection-toggle hotspot.</summary>
        /// <param name="localPointer">Pointer coordinate in logical cube pixels.</param>
        /// <returns>True when the point is within the center hotspot radius.</returns>
        static bool IsCenterTogglePoint(float2 localPointer) {
            double center = LogicalCubeSize * 0.5;
            double deltaX = localPointer.X - center;
            double deltaY = localPointer.Y - center;
            double radius = LogicalCenterToggleRadius;
            return (deltaX * deltaX) + (deltaY * deltaY) <= radius * radius;
        }

        /// <summary>
        /// Converts a window-space pointer coordinate into logical cube coordinates.
        /// </summary>
        /// <param name="pointerPosition">Window-space pointer coordinate.</param>
        /// <returns>Logical cube-local coordinate.</returns>
        float2 GetCubeLocalPointer(int2 pointerPosition) {
            return new float2(
                (float)((pointerPosition.X - CubePosition.X) / UiScale),
                (float)((pointerPosition.Y - CubePosition.Y) / UiScale));
        }

        /// <summary>
        /// Updates persistent blockers that match the cube and projection button bounds.
        /// </summary>
        void UpdateLayoutBlockers() {
            if (IsVisible) {
                InputCapture.SetBlocker(CubeInputBlockerOwner, CubePosition, CubeSize);
            } else {
                InputCapture.ClearBlocker(CubeInputBlockerOwner);
            }
            if (IsProjectionControlVisible) {
                InputCapture.SetBlocker(ProjectionControlInputBlockerOwner, ProjectionControlPosition, ProjectionControlSize);
            } else {
                InputCapture.ClearBlocker(ProjectionControlInputBlockerOwner);
            }
        }

        /// <summary>
        /// Clears persistent blockers when the cube does not fit or is disposed.
        /// </summary>
        void ClearLayoutBlockers() {
            InputCapture.ClearBlocker(CubeInputBlockerOwner);
            InputCapture.ClearBlocker(ProjectionControlInputBlockerOwner);
            InputCapture.ClearBlocker(HoverPreviewInputBlockerOwner);
        }

        /// <summary>
        /// Clears temporary pointer capture and all pending gesture state.
        /// </summary>
        void ReleaseViewportCapture() {
            if (IsPointerCaptured) {
                InputCapture.ClearBlocker(CapturedViewportInputBlockerOwner);
            }
            IsPointerCaptured = false;
            PressedTarget = null;
            PressedProjectionControl = false;
            PressedCenterToggle = false;
            HasDragged = false;
            IsOrbiting = false;
        }

        /// <summary>
        /// Checks whether another modal, panel, or toolbar already blocks the pressed point.
        /// </summary>
        /// <param name="pointerPosition">Window-space pointer position.</param>
        /// <returns>True when a different blocker owns the pointer.</returns>
        bool IsPointerBlockedByAnotherOwner(int2 pointerPosition) {
            return InputCapture.IsPointerBlocked(pointerPosition, owner =>
                !ReferenceEquals(owner, CubeInputBlockerOwner) &&
                !ReferenceEquals(owner, ProjectionControlInputBlockerOwner) &&
                !ReferenceEquals(owner, HoverPreviewInputBlockerOwner) &&
                !ReferenceEquals(owner, CapturedViewportInputBlockerOwner));
        }

        /// <summary>
        /// Checks whether a point lies inside the half-open bounds of a screen-space rectangle.
        /// </summary>
        /// <param name="point">Point in window coordinates.</param>
        /// <param name="position">Top-left rectangle coordinate.</param>
        /// <param name="size">Rectangle dimensions.</param>
        /// <returns>True when the point is within the rectangle.</returns>
        static bool ContainsPoint(int2 point, int2 position, int2 size) {
            return point.X >= position.X && point.X < position.X + size.X &&
                   point.Y >= position.Y && point.Y < position.Y + size.Y;
        }

        /// <summary>
        /// Converts a logical UI length into at least one integral device pixel.
        /// </summary>
        /// <param name="logicalPixels">Positive base pixel count.</param>
        /// <returns>Device-pixel length rounded upward.</returns>
        int ScaleLogicalPixels(int logicalPixels) {
            return Math.Max(1, (int)Math.Ceiling(logicalPixels * (double)UiScale));
        }

        /// <summary>
        /// Rejects further use after resources have been disposed.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorViewportNavigationCubeInteractionController));
            }
        }
    }
}

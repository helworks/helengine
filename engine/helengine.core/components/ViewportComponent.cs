namespace helengine {
    /// <summary>
    /// Exposes a reusable logical viewport that can follow the current screen or an ancestor camera.
    /// </summary>
    public class ViewportComponent : Component, IAnchorBoundsProvider {
        /// <summary>
        /// Binding mode that resolves the viewport from the current screen size.
        /// </summary>
        public const byte ScreenBindingMode = 0;

        /// <summary>
        /// Binding mode that resolves the viewport from the nearest ancestor camera component.
        /// </summary>
        public const byte AncestorCameraBindingMode = 1;

        /// <summary>
        /// Binding mode that resolves the viewport from the authored fixed size.
        /// </summary>
        public const byte FixedBindingMode = 2;

        /// <summary>
        /// Stores the selected viewport binding mode.
        /// </summary>
        byte BindingModeValue;

        /// <summary>
        /// Stores the authored fixed viewport size used by fixed and fallback bindings.
        /// </summary>
        int2 FixedSizeValue;

        /// <summary>
        /// Tracks the nearest ancestor camera currently driving this viewport.
        /// </summary>
        CameraComponent BoundCameraComponentValue;

        /// <summary>
        /// Tracks whether the component is subscribed to screen resize events.
        /// </summary>
        bool IsSubscribedToWindowResizeValue;

        /// <summary>
        /// Raises when the resolved viewport bounds change.
        /// </summary>
        public event Action AnchorBoundsChanged;

        /// <summary>
        /// Initializes a new viewport component with screen binding and a 720p authored fallback size.
        /// </summary>
        public ViewportComponent() {
            BindingModeValue = ScreenBindingMode;
            FixedSizeValue = new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight);
        }

        /// <summary>
        /// Gets or sets the active viewport binding mode.
        /// </summary>
        public byte BindingMode {
            get { return BindingModeValue; }
            set {
                if (BindingModeValue != value) {
                    BindingModeValue = value;
                    RefreshSubscriptions();
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the fixed viewport size used by fixed bindings and fallbacks.
        /// </summary>
        public int2 FixedSize {
            get { return FixedSizeValue; }
            set {
                if (FixedSizeValue.X != value.X || FixedSizeValue.Y != value.Y) {
                    FixedSizeValue = value;
                    RaiseAnchorBoundsChanged();
                }
            }
        }

        /// <summary>
        /// Gets the resolved viewport bounds in local pixels.
        /// </summary>
        public int2 AnchorBounds {
            get {
                RefreshSubscriptions();
                return ResolveAnchorBounds();
            }
        }

        /// <summary>
        /// Rebinds the viewport listeners when the component is attached to an entity.
        /// </summary>
        /// <param name="entity">Entity receiving the component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            RefreshSubscriptions();
        }

        /// <summary>
        /// Releases viewport subscriptions when the component is removed from its entity.
        /// </summary>
        /// <param name="entity">Entity losing the component.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            DetachFromCamera();
            DetachFromWindowResize();
        }

        /// <summary>
        /// Rebinds the viewport listeners when the parent entity changes enabled state.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                RefreshSubscriptions();
                RaiseAnchorBoundsChanged();
            } else {
                DetachFromCamera();
                DetachFromWindowResize();
            }
        }

        /// <summary>
        /// Updates event subscriptions for the current binding mode.
        /// </summary>
        void RefreshSubscriptions() {
            if (BindingModeValue == ScreenBindingMode) {
                DetachFromCamera();
                AttachToWindowResize();
            } else if (BindingModeValue == AncestorCameraBindingMode) {
                DetachFromWindowResize();
                CameraComponent nextCameraComponent = ResolveAncestorCameraComponent();
                if (!ReferenceEquals(BoundCameraComponentValue, nextCameraComponent)) {
                    DetachFromCamera();
                    BoundCameraComponentValue = nextCameraComponent;
                    AttachToCamera();
                }
            } else {
                DetachFromCamera();
                DetachFromWindowResize();
            }
        }

        /// <summary>
        /// Attaches to the current ancestor camera viewport changes when one exists.
        /// </summary>
        void AttachToCamera() {
            if (BoundCameraComponentValue == null) {
                return;
            }

            BoundCameraComponentValue.ViewportChanged += HandleCameraViewportChanged;
        }

        /// <summary>
        /// Detaches from the current ancestor camera viewport changes when one is active.
        /// </summary>
        void DetachFromCamera() {
            if (BoundCameraComponentValue != null) {
                BoundCameraComponentValue.ViewportChanged -= HandleCameraViewportChanged;
                BoundCameraComponentValue = null;
            }
        }

        /// <summary>
        /// Attaches the screen resize fallback when it is not already active.
        /// </summary>
        void AttachToWindowResize() {
            if (IsSubscribedToWindowResizeValue) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized += HandleWindowResized;
            IsSubscribedToWindowResizeValue = true;
        }

        /// <summary>
        /// Detaches the screen resize fallback when it is active.
        /// </summary>
        void DetachFromWindowResize() {
            if (!IsSubscribedToWindowResizeValue) {
                return;
            }

            Core.Instance.RenderManager3D.WindowResized -= HandleWindowResized;
            IsSubscribedToWindowResizeValue = false;
        }

        /// <summary>
        /// Handles ancestor camera viewport changes.
        /// </summary>
        void HandleCameraViewportChanged() {
            RaiseAnchorBoundsChanged();
        }

        /// <summary>
        /// Handles window resize notifications for the screen binding mode.
        /// </summary>
        /// <param name="handle">Window handle reported by the render manager.</param>
        /// <param name="newWidth">Updated window width.</param>
        /// <param name="newHeight">Updated window height.</param>
        void HandleWindowResized(IntPtr handle, int newWidth, int newHeight) {
            RaiseAnchorBoundsChanged();
        }

        /// <summary>
        /// Resolves the active viewport bounds without forcing another subscription refresh.
        /// </summary>
        /// <returns>Resolved viewport bounds in local pixels.</returns>
        int2 ResolveAnchorBounds() {
            if (BindingModeValue == ScreenBindingMode) {
                int2 screenSize = Core.Instance.RenderManager3D.MainWindowSize;
                if (screenSize.X > 0 && screenSize.Y > 0) {
                    return screenSize;
                }

                return FixedSizeValue;
            }

            if (BindingModeValue == AncestorCameraBindingMode) {
                CameraComponent cameraComponent = ResolveAncestorCameraComponent();
                if (cameraComponent != null) {
                    float4 viewport = cameraComponent.Viewport;
                    int viewportWidth = Math.Max(1, (int)Math.Round(viewport.Z));
                    int viewportHeight = Math.Max(1, (int)Math.Round(viewport.W));
                    return new int2(viewportWidth, viewportHeight);
                }

                return FixedSizeValue;
            }

            return FixedSizeValue;
        }

        /// <summary>
        /// Resolves the nearest ancestor camera component in the current entity chain.
        /// </summary>
        /// <returns>Nearest ancestor camera component when one exists; otherwise null.</returns>
        CameraComponent ResolveAncestorCameraComponent() {
            Entity current = Parent;

            while (current != null) {
                if (current.Components != null) {
                    for (int index = 0; index < current.Components.Count; index++) {
                        if (current.Components[index] is CameraComponent cameraComponent) {
                            return cameraComponent;
                        }
                    }
                }

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Raises the viewport changed event when listeners are present.
        /// </summary>
        void RaiseAnchorBoundsChanged() {
            if (AnchorBoundsChanged != null) {
                AnchorBoundsChanged();
            }
        }
    }
}

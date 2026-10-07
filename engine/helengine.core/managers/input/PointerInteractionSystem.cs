namespace helengine {
    /// <summary>
    /// Routes pointer hover and press interactions to 2D interactables using the current raw input frame.
    /// </summary>
    public sealed class PointerInteractionSystem {
        /// <summary>
        /// Initializes a new pointer interaction router for one core instance.
        /// </summary>
        /// <param name="core">Core instance that owns the current object graph.</param>
        /// <param name="inputSystem">Input system that supplies raw pointer state.</param>
        public PointerInteractionSystem(Core core, InputSystem inputSystem) {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            Input = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
        }

        /// <summary>
        /// Gets the core instance that owns the routed pointer targets.
        /// </summary>
        public Core Core { get; private set; }

        /// <summary>
        /// Gets the input system that supplies raw pointer state.
        /// </summary>
        public InputSystem Input { get; private set; }

        /// <summary>
        /// Component that currently owns a global pointer-cursor override, when one exists.
        /// </summary>
        Component CursorOverrideOwner;

        /// <summary>
        /// Fixed screen position associated with the current cursor override.
        /// </summary>
        int2 CursorOverridePositionValue;

        /// <summary>
        /// Cursor kind requested by the current global override owner.
        /// </summary>
        PointerCursorKind CursorOverrideKind;

        /// <summary>
        /// Gets the interactable currently captured by a press.
        /// </summary>
        public IInteractable2D Highlighted { get; private set; }

        /// <summary>
        /// Gets the interactable currently hovered by the pointer.
        /// </summary>
        public IInteractable2D Hovering { get; private set; }

        /// <summary>
        /// Gets the cursor requested by the currently hovered interactable.
        /// </summary>
        public PointerCursorKind HoverCursor {
            get {
                if (HasAttachedCursorOverride) {
                    return CursorOverrideKind;
                }

                if (Hovering == null) {
                    return PointerCursorKind.Default;
                }

                return Hovering.HoverCursor;
            }
        }

        /// <summary>
        /// Gets the screen position where the current global cursor override was activated.
        /// </summary>
        public int2 CursorOverridePosition {
            get {
                return HasAttachedCursorOverride ? CursorOverridePositionValue : new int2(0, 0);
            }
        }

        /// <summary>Gets the available autoscroll direction from the component that owns the fixed cursor marker.</summary>
        public int CursorOverrideScrollDirection {
            get {
                return HasAttachedCursorOverride && CursorOverrideOwner is ScrollComponent scroll
                    ? scroll.AutoScrollDirection
                    : 0;
            }
        }

        /// <summary>
        /// Attempts to set a global cursor override owned by the supplied component.
        /// </summary>
        /// <param name="owner">Component that owns the override.</param>
        /// <param name="cursor">Cursor to expose while the component remains attached.</param>
        /// <returns>True when the override was assigned to the owner.</returns>
        public bool TrySetCursorOverride(Component owner, PointerCursorKind cursor) {
            return TrySetCursorOverride(owner, cursor, new int2(Input.GetPointerX(), Input.GetPointerY()));
        }

        /// <summary>
        /// Attempts to set a global cursor override and its fixed screen-space anchor.
        /// </summary>
        /// <param name="owner">Component that owns the override.</param>
        /// <param name="cursor">Cursor state to expose while the component remains attached.</param>
        /// <param name="position">Screen position where the override was activated.</param>
        /// <returns>True when the override was assigned to the owner.</returns>
        public bool TrySetCursorOverride(Component owner, PointerCursorKind cursor, int2 position) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            if (HasAttachedCursorOverride &&
                CursorOverrideOwner.Parent != null &&
                !ReferenceEquals(CursorOverrideOwner, owner)) {
                return false;
            }

            CursorOverrideOwner = owner;
            CursorOverrideKind = cursor;
            CursorOverridePositionValue = position;
            return true;
        }

        /// <summary>
        /// Clears a cursor override only when the supplied component currently owns it.
        /// </summary>
        /// <param name="owner">Component whose override should be cleared.</param>
        public void ClearCursorOverride(Component owner) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!ReferenceEquals(CursorOverrideOwner, owner)) {
                return;
            }

            CursorOverrideOwner = null;
            CursorOverrideKind = PointerCursorKind.Default;
            CursorOverridePositionValue = new int2(0, 0);
        }

        /// <summary>
        /// Clears a stale override when its owning component is no longer attached.
        /// </summary>
        bool HasAttachedCursorOverride {
            get {
                if (CursorOverrideOwner == null) {
                    return false;
                }

                if (CursorOverrideOwner.Parent != null) {
                    return true;
                }

                CursorOverrideOwner = null;
                CursorOverrideKind = PointerCursorKind.Default;
                CursorOverridePositionValue = new int2(0, 0);
                return false;
            }
        }

        /// <summary>
        /// Clears cached hover or capture state when one interactable is removed or disabled.
        /// </summary>
        /// <param name="interactable">Interactable that is leaving the active hit-test set.</param>
        public void ClearInteractionFor(IInteractable2D interactable) {
            if (interactable == null) {
                throw new ArgumentNullException(nameof(interactable));
            }

            if (ReferenceEquals(Hovering, interactable)) {
                Hovering = null;
            }

            if (ReferenceEquals(Highlighted, interactable)) {
                Highlighted = null;
                CapturedCamera = null;
            }
        }

        /// <summary>
        /// Updates hover and capture routing for the current pointer frame.
        /// </summary>
        public void Update() {
            ObjectManager objectManager = Core.ObjectManager;
            List<IInteractable2D> interactables = objectManager.Interactables;
            List<IDrawable2D> drawables2D = objectManager.Drawables2D;

            PointerInteraction interaction = PointerInteraction.None;
            if (Input.WasPointerPrimaryReleased()) {
                interaction = PointerInteraction.Release;
            } else if (Input.WasPointerPrimaryPressed()) {
                interaction = PointerInteraction.Press;
            }

            if (Highlighted != null) {
                int pointerX;
                int pointerY;
                PointerInteractableHitResolver.GetRelativePointerForInteractable(Highlighted, Input.GetPointerX(), Input.GetPointerY(), CapturedCamera, out pointerX, out pointerY);
                int deltaX = Input.GetPointerDeltaX();
                int deltaY = Input.GetPointerDeltaY();
                if (interaction == PointerInteraction.None && (deltaX != 0 || deltaY != 0)) {
                    interaction = PointerInteraction.Hover;
                }

                int2 pointer = new int2(pointerX, pointerY);
                int2 delta = new int2(deltaX, deltaY);
                Highlighted.OnCursor(pointer, delta, interaction);
                if (interaction == PointerInteraction.Release) {
                    Highlighted = null;
                    CapturedCamera = null;
                }

                return;
            }

            ResolveTopInteractableAt(
                interactables,
                drawables2D,
                Input.GetPointerX(),
                Input.GetPointerY(),
                out IInteractable2D hit,
                out ICamera hitCamera);

            bool hoveringChanged = hit != Hovering;
            if (hoveringChanged && Hovering != null) {
                int prevPointerX;
                int prevPointerY;
                ICamera hoverCamera = FindCameraForInteractableAt(Hovering, Input.GetPointerX(), Input.GetPointerY());
                PointerInteractableHitResolver.GetRelativePointerForInteractable(Hovering, Input.GetPointerX(), Input.GetPointerY(), hoverCamera, out prevPointerX, out prevPointerY);
                int2 previousPointer = new int2(prevPointerX, prevPointerY);
                int2 zeroDelta = new int2(0, 0);
                Hovering.OnCursor(previousPointer, zeroDelta, PointerInteraction.Leave);
            }

            Hovering = hit;
            if (Hovering == null) {
                return;
            }

            int currentPointerX;
            int currentPointerY;
            PointerInteractableHitResolver.GetRelativePointerForInteractable(Hovering, Input.GetPointerX(), Input.GetPointerY(), hitCamera, out currentPointerX, out currentPointerY);
            int currentDeltaX = Input.GetPointerDeltaX();
            int currentDeltaY = Input.GetPointerDeltaY();
            if (interaction == PointerInteraction.Press) {
                if (hoveringChanged) {
                    int2 hoverPointer = new int2(currentPointerX, currentPointerY);
                    int2 hoverDelta = new int2(currentDeltaX, currentDeltaY);
                    Hovering.OnCursor(hoverPointer, hoverDelta, PointerInteraction.Hover);
                }

                Highlighted = Hovering;
                CapturedCamera = hitCamera;
                int2 pressPointer = new int2(currentPointerX, currentPointerY);
                int2 pressDelta = new int2(currentDeltaX, currentDeltaY);
                Hovering.OnCursor(pressPointer, pressDelta, PointerInteraction.Press);
            } else if (hoveringChanged || currentDeltaX != 0 || currentDeltaY != 0) {
                int2 hoverPointer = new int2(currentPointerX, currentPointerY);
                int2 hoverDelta = new int2(currentDeltaX, currentDeltaY);
                Hovering.OnCursor(hoverPointer, hoverDelta, PointerInteraction.Hover);
            }
        }

        /// <summary>
        /// Resolves the top-most interactable across all cameras that cover one pointer coordinate.
        /// </summary>
        /// <param name="interactables">Registered interactables considered for the hit test.</param>
        /// <param name="drawables2D">Registered drawables used to evaluate per-camera visual order.</param>
        /// <param name="x">Pointer X coordinate in window space.</param>
        /// <param name="y">Pointer Y coordinate in window space.</param>
        /// <param name="hitInteractable">Receives the top-most interactable, or null when nothing matches.</param>
        /// <param name="hitCamera">Receives the camera that owns the winning hit, or null when nothing matches.</param>
        void ResolveTopInteractableAt(
            List<IInteractable2D> interactables,
            List<IDrawable2D> drawables2D,
            int x,
            int y,
            out IInteractable2D hitInteractable,
            out ICamera hitCamera) {
            List<ICamera> cameras = Core.ObjectManager.Cameras;
            hitInteractable = null;
            hitCamera = null;
            byte winningDrawOrder = 0;
            int winningCameraIndex = -1;

            for (int i = 0; i < cameras.Count; i++) {
                ICamera camera = cameras[i];
                if (!ResolveViewportInWindowSpace(camera).Contains(x, y)) {
                    continue;
                }

                IInteractable2D candidateInteractable = PointerInteractableHitResolver.ResolveTopInteractableAt(
                    interactables,
                    drawables2D,
                    camera,
                    x,
                    y);
                if (candidateInteractable == null) {
                    continue;
                }

                byte candidateDrawOrder = camera.CameraDrawOrder;
                if (hitInteractable == null ||
                    candidateDrawOrder > winningDrawOrder ||
                    (candidateDrawOrder == winningDrawOrder && i > winningCameraIndex)) {
                    hitInteractable = candidateInteractable;
                    hitCamera = camera;
                    winningDrawOrder = candidateDrawOrder;
                    winningCameraIndex = i;
                }
            }
        }

        /// <summary>
        /// Finds the camera that should be used to compute relative pointer coordinates for one interactable.
        /// </summary>
        /// <param name="interactable">Interactable being evaluated.</param>
        /// <param name="x">Pointer X coordinate in window space.</param>
        /// <param name="y">Pointer Y coordinate in window space.</param>
        /// <returns>An object-manager-owned camera borrowed for pointer routing, or null when no camera covers the point.</returns>
        [NativeBorrowedReturn]
        ICamera FindCameraForInteractableAt(IInteractable2D interactable, int x, int y) {
            if (interactable == null) {
                return null;
            }

            List<ICamera> cameras = Core.ObjectManager.Cameras;
            ICamera matchedCamera = null;
            byte winningDrawOrder = 0;
            int winningCameraIndex = -1;

            for (int i = 0; i < cameras.Count; i++) {
                ICamera camera = cameras[i];
                if (!ResolveViewportInWindowSpace(camera).Contains(x, y)) {
                    continue;
                }
                if ((interactable.Parent.LayerMask & camera.LayerMask) == 0) {
                    continue;
                }

                byte candidateDrawOrder = camera.CameraDrawOrder;
                if (matchedCamera == null ||
                    candidateDrawOrder > winningDrawOrder ||
                    (candidateDrawOrder == winningDrawOrder && i > winningCameraIndex)) {
                    matchedCamera = camera;
                    winningDrawOrder = candidateDrawOrder;
                    winningCameraIndex = i;
                }
            }

            return matchedCamera;
        }

        /// <summary>
        /// Resolves one camera viewport into window-space pixels so pointer routing matches the active render target dimensions.
        /// </summary>
        /// <param name="camera">Camera whose viewport should be resolved.</param>
        /// <returns>Viewport rectangle in window-space pixels.</returns>
        float4 ResolveViewportInWindowSpace(ICamera camera) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (Core.RenderManager3D == null) {
                return camera.Viewport;
            }

            int2 mainWindowSize = Core.RenderManager3D.InputWindowSize;
            if (mainWindowSize.X <= 0 || mainWindowSize.Y <= 0) {
                return camera.Viewport;
            }

            return CameraViewportResolver.ResolveViewport(camera.Viewport, mainWindowSize.X, mainWindowSize.Y);
        }

        /// <summary>
        /// Cached camera captured at the start of a press interaction.
        /// </summary>
        ICamera CapturedCamera;
    }
}

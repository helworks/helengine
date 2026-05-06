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
                if (Hovering == null) {
                    return PointerCursorKind.Default;
                }

                return Hovering.HoverCursor;
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
            if (Input.WasMouseLeftButtonReleased()) {
                interaction = PointerInteraction.Release;
            } else if (Input.WasMouseLeftButtonPressed()) {
                interaction = PointerInteraction.Press;
            }

            int mouseX = Input.GetMouseX();
            int mouseY = Input.GetMouseY();

            if (Highlighted != null) {
                int pointerX;
                int pointerY;
                PointerInteractableHitResolver.GetRelativePointerForInteractable(Highlighted, Input.GetMouseX(), Input.GetMouseY(), capturedCamera, out pointerX, out pointerY);
                int deltaX = Input.GetMouseDeltaX();
                int deltaY = Input.GetMouseDeltaY();
                if (interaction == PointerInteraction.None && (deltaX != 0 || deltaY != 0)) {
                    interaction = PointerInteraction.Hover;
                }

                int2 pointer = new int2(pointerX, pointerY);
                int2 delta = new int2(deltaX, deltaY);
                Highlighted.OnCursor(pointer, delta, interaction);
                if (interaction == PointerInteraction.Release) {
                    Highlighted = null;
                    capturedCamera = null;
                }

                return;
            }

            ResolveTopInteractableAt(
                interactables,
                drawables2D,
                Input.GetMouseX(),
                Input.GetMouseY(),
                out IInteractable2D hit,
                out ICamera hitCamera);

            bool hoveringChanged = hit != Hovering;
            if (hoveringChanged && Hovering != null) {
                int prevPointerX;
                int prevPointerY;
                ICamera hoverCamera = FindCameraForInteractableAt(Hovering, Input.GetMouseX(), Input.GetMouseY());
                PointerInteractableHitResolver.GetRelativePointerForInteractable(Hovering, Input.GetMouseX(), Input.GetMouseY(), hoverCamera, out prevPointerX, out prevPointerY);
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
            PointerInteractableHitResolver.GetRelativePointerForInteractable(Hovering, Input.GetMouseX(), Input.GetMouseY(), hitCamera, out currentPointerX, out currentPointerY);
            int currentDeltaX = Input.GetMouseDeltaX();
            int currentDeltaY = Input.GetMouseDeltaY();
            if (interaction == PointerInteraction.Press) {
                if (hoveringChanged) {
                    int2 hoverPointer = new int2(currentPointerX, currentPointerY);
                    int2 hoverDelta = new int2(currentDeltaX, currentDeltaY);
                    Hovering.OnCursor(hoverPointer, hoverDelta, PointerInteraction.Hover);
                }

                Highlighted = Hovering;
                capturedCamera = hitCamera;
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
                if (!camera.Viewport.Contains(x, y)) {
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
        /// <returns>Matching camera, or null when no camera covers the point.</returns>
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
                if (!camera.Viewport.Contains(x, y)) {
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
        /// Cached camera captured at the start of a press interaction.
        /// </summary>
        ICamera capturedCamera;
    }
}

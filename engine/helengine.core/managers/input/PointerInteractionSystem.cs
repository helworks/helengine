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
            ICamera topCamera = GetTopmostCameraAt(mouseX, mouseY);

            if (topCamera != null) {
                float4 viewport = topCamera.Viewport;
                mouseX -= (int)viewport.X;
                mouseY -= (int)viewport.Y;
            }

            if (Highlighted != null) {
                int pointerX;
                int pointerY;
                GetRelativePointerForInteractable(Highlighted, Input.GetMouseX(), Input.GetMouseY(), capturedCamera, out pointerX, out pointerY);
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

            IInteractable2D hit = null;
            byte hitRenderOrder = 0;
            int hitDrawableIndex = -1;
            int hitInteractableIndex = -1;
            if (topCamera != null) {
                ushort camMask = topCamera.LayerMask;
                for (int i = 0; i < interactables.Count; i++) {
                    IInteractable2D interactable = interactables[i];
                    if ((interactable.Parent.LayerMask & camMask) == 0) {
                        continue;
                    }

                    float3 position = interactable.Parent.Position;
                    float4 rect = new float4(position.X, position.Y, interactable.Size.X, interactable.Size.Y);
                    if (!rect.Contains(mouseX, mouseY)) {
                        continue;
                    }

                    byte candidateRenderOrder = GetTopDrawableRenderOrder(drawables2D, interactable, camMask, out int candidateDrawableIndex);
                    if (hit == null ||
                        CandidateIsInFront(candidateRenderOrder, candidateDrawableIndex, i, hitRenderOrder, hitDrawableIndex, hitInteractableIndex)) {
                        hit = interactable;
                        hitRenderOrder = candidateRenderOrder;
                        hitDrawableIndex = candidateDrawableIndex;
                        hitInteractableIndex = i;
                    }
                }
            }

            bool hoveringChanged = hit != Hovering;
            if (hoveringChanged && Hovering != null) {
                int prevPointerX;
                int prevPointerY;
                ICamera hoverCamera = FindCameraForInteractableAt(Hovering, Input.GetMouseX(), Input.GetMouseY());
                GetRelativePointerForInteractable(Hovering, Input.GetMouseX(), Input.GetMouseY(), hoverCamera, out prevPointerX, out prevPointerY);
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
            GetRelativePointerForInteractable(Hovering, Input.GetMouseX(), Input.GetMouseY(), topCamera, out currentPointerX, out currentPointerY);
            int currentDeltaX = Input.GetMouseDeltaX();
            int currentDeltaY = Input.GetMouseDeltaY();
            if (interaction == PointerInteraction.Press) {
                if (hoveringChanged) {
                    int2 hoverPointer = new int2(currentPointerX, currentPointerY);
                    int2 hoverDelta = new int2(currentDeltaX, currentDeltaY);
                    Hovering.OnCursor(hoverPointer, hoverDelta, PointerInteraction.Hover);
                }

                Highlighted = Hovering;
                capturedCamera = topCamera;
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
        /// Finds the topmost camera that contains one pointer coordinate.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window space.</param>
        /// <param name="y">Pointer Y coordinate in window space.</param>
        /// <returns>Topmost matching camera, or null when none covers the point.</returns>
        ICamera GetTopmostCameraAt(int x, int y) {
            List<ICamera> cameras = Core.ObjectManager.Cameras;
            for (int i = cameras.Count - 1; i >= 0; i--) {
                ICamera camera = cameras[i];
                if (camera.Viewport.Contains(x, y)) {
                    return camera;
                }
            }

            return null;
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

            return GetTopmostCameraAt(x, y);
        }

        /// <summary>
        /// Computes a pointer position relative to one interactable.
        /// </summary>
        /// <param name="interactable">Interactable to measure against.</param>
        /// <param name="x">Pointer X coordinate in window space.</param>
        /// <param name="y">Pointer Y coordinate in window space.</param>
        /// <param name="camera">Camera used to interpret the pointer position.</param>
        /// <returns>Pointer position relative to the interactable.</returns>
        void GetRelativePointerForInteractable(IInteractable2D interactable, int x, int y, ICamera camera, out int relativeX, out int relativeY) {
            float2 local = new float2(x, y);
            if (camera != null) {
                float4 viewport = camera.Viewport;
                local.X -= viewport.X;
                local.Y -= viewport.Y;
            }

            float3 position = interactable.Parent.Position;
            relativeX = (int)Math.Round(local.X - position.X);
            relativeY = (int)Math.Round(local.Y - position.Y);
        }

        /// <summary>
        /// Chooses the strongest drawable order associated with one interactable and camera mask.
        /// </summary>
        /// <param name="drawables2D">Registered 2D drawables.</param>
        /// <param name="interactable">Interactable being evaluated.</param>
        /// <param name="camMask">Camera layer mask.</param>
        /// <param name="candidateDrawableIndex">Receives the drawable index used for tie-breaking.</param>
        /// <returns>Highest drawable render order for the interactable.</returns>
        byte GetTopDrawableRenderOrder(List<IDrawable2D> drawables2D, IInteractable2D interactable, ushort camMask, out int candidateDrawableIndex) {
            candidateDrawableIndex = -1;
            byte renderOrder = 0;
            if (drawables2D == null || interactable == null) {
                return renderOrder;
            }

            for (int i = 0; i < drawables2D.Count; i++) {
                IDrawable2D drawable = drawables2D[i];
                if (drawable.Parent != interactable.Parent) {
                    continue;
                }
                if ((drawable.Parent.LayerMask & camMask) == 0) {
                    continue;
                }

                if (candidateDrawableIndex < 0 || drawable.RenderOrder2D >= renderOrder) {
                    renderOrder = drawable.RenderOrder2D;
                    candidateDrawableIndex = i;
                }
            }

            return renderOrder;
        }

        /// <summary>
        /// Determines whether one candidate is in front of another candidate using render order and registration order.
        /// </summary>
        /// <param name="candidateRenderOrder">Candidate render order.</param>
        /// <param name="candidateDrawableIndex">Candidate drawable index.</param>
        /// <param name="candidateInteractableIndex">Candidate interactable index.</param>
        /// <param name="currentRenderOrder">Current best render order.</param>
        /// <param name="currentDrawableIndex">Current best drawable index.</param>
        /// <param name="currentInteractableIndex">Current best interactable index.</param>
        /// <returns>True when the candidate should replace the current best hit.</returns>
        bool CandidateIsInFront(
            byte candidateRenderOrder,
            int candidateDrawableIndex,
            int candidateInteractableIndex,
            byte currentRenderOrder,
            int currentDrawableIndex,
            int currentInteractableIndex) {
            if (candidateRenderOrder != currentRenderOrder) {
                return candidateRenderOrder > currentRenderOrder;
            }

            if (candidateDrawableIndex != currentDrawableIndex) {
                return candidateDrawableIndex > currentDrawableIndex;
            }

            return candidateInteractableIndex > currentInteractableIndex;
        }

        /// <summary>
        /// Cached camera captured at the start of a press interaction.
        /// </summary>
        ICamera capturedCamera;
    }
}

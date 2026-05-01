namespace helengine;

/// <summary>
/// Routes mouse input to interactable components and tracks hover/press state.
/// </summary>
public abstract class InputManager {
    /// <summary>
    /// Cached core instance for input routing.
    /// </summary>
    protected Core core;

    /// <summary>
    /// Camera that captured the pointer on press.
    /// </summary>
    ICamera capturedCamera;
    /// <summary>
    /// Mouse state snapshot from the previous update.
    /// </summary>
    MouseState lastMouseState;
    /// <summary>
    /// Mouse state snapshot captured during the current update.
    /// </summary>
    MouseState mouseState;
    /// <summary>
    /// Keyboard state snapshot from the previous update.
    /// </summary>
    KeyboardState lastKeyboardState;
    /// <summary>
    /// Keyboard state snapshot captured during the current update.
    /// </summary>
    KeyboardState keyboardState;
    /// <summary>
    /// Tracks whether input has been captured for the current frame.
    /// </summary>
    bool hasCapturedInput;
    /// <summary>
    /// Mouse movement delta cached for the current frame.
    /// </summary>
    int2 mouseDelta;
    /// <summary>
    /// Tracks whether client-edge pointer wrapping is currently active on the mouse backend.
    /// </summary>
    bool ActivePointerWrapEnabled;
    /// <summary>
    /// Tracks whether any editor interaction requested pointer wrapping for the next frame.
    /// </summary>
    bool RequestedPointerWrapEnabled;

    /// <summary>
    /// Initializes the input manager and caches the core instance.
    /// </summary>
    public InputManager() {
        core = Core.Instance;
    }

    /// <summary>
    /// Gets or sets the keyboard input device used by platform-specific managers.
    /// </summary>
    protected Keyboard Keyboard { get; set; }

    /// <summary>
    /// Gets or sets the mouse input device used by platform-specific managers.
    /// </summary>
    protected Mouse Mouse { get; set; }

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
    /// Enables or disables keyboard input capture for the active backend.
    /// </summary>
    /// <param name="isActive">True to capture key state; false to ignore input.</param>
    public void SetKeyboardActive(bool isActive) {
        if (Keyboard == null) {
            return;
        }

        Keyboard.SetActive(isActive);
    }

    /// <summary>
    /// Enables or disables client-edge pointer wrapping for the active mouse backend.
    /// </summary>
    /// <param name="isEnabled">True when active interactions should wrap across the client bounds.</param>
    public void SetPointerWrapEnabled(bool isEnabled) {
        ActivePointerWrapEnabled = isEnabled;
        RequestedPointerWrapEnabled = isEnabled;

        if (Mouse == null) {
            return;
        }

        Mouse.SetPointerWrapEnabled(isEnabled);
    }

    /// <summary>
    /// Requests client-edge pointer wrapping for the next input frame.
    /// </summary>
    public void RequestPointerWrapEnabled() {
        RequestedPointerWrapEnabled = true;
    }

    /// <summary>
    /// Captures keyboard and mouse input at the start of a frame.
    /// </summary>
    public virtual void EarlyUpdate() {
        ApplyPointerWrapState();
        EnsureInputStateCaptured();
    }

    /// <summary>
    /// Gets the cached mouse position in window coordinates.
    /// </summary>
    /// <returns>Mouse position in pixels.</returns>
    public int2 GetMousePosition() {
        return new int2(mouseState.X, mouseState.Y);
    }

    /// <summary>
    /// Gets the mouse movement delta between the current and previous frame.
    /// </summary>
    /// <returns>Mouse movement delta in pixels.</returns>
    public int2 GetMouseDelta() {
        return mouseDelta;
    }

    /// <summary>
    /// Gets the current scroll wheel value.
    /// </summary>
    /// <returns>Scroll wheel value.</returns>
    public int GetMouseScrollWheelValue() {
        return mouseState.ScrollWheelValue;
    }

    /// <summary>
    /// Gets the scroll wheel delta between the current and previous frame.
    /// </summary>
    /// <returns>Scroll wheel delta.</returns>
    public int GetMouseScrollWheelDelta() {
        return mouseState.ScrollWheelValue - lastMouseState.ScrollWheelValue;
    }

    /// <summary>
    /// Gets the current left mouse button state.
    /// </summary>
    /// <returns>Left button state.</returns>
    public ButtonState GetMouseLeftButtonState() {
        return mouseState.LeftButton;
    }

    /// <summary>
    /// Gets the current right mouse button state.
    /// </summary>
    /// <returns>Right button state.</returns>
    public ButtonState GetMouseRightButtonState() {
        return mouseState.RightButton;
    }

    /// <summary>
    /// Gets the current middle mouse button state.
    /// </summary>
    /// <returns>Middle button state.</returns>
    public ButtonState GetMouseMiddleButtonState() {
        return mouseState.MiddleButton;
    }

    /// <summary>
    /// Gets the current XButton1 state.
    /// </summary>
    /// <returns>XButton1 state.</returns>
    public ButtonState GetMouseXButton1State() {
        return mouseState.XButton1;
    }

    /// <summary>
    /// Gets the current XButton2 state.
    /// </summary>
    /// <returns>XButton2 state.</returns>
    public ButtonState GetMouseXButton2State() {
        return mouseState.XButton2;
    }

    /// <summary>
    /// Gets a value indicating whether the left mouse button was pressed this frame.
    /// </summary>
    /// <returns>True when the left button transitioned to pressed.</returns>
    public bool WasMouseLeftButtonPressed() {
        return WasMouseButtonPressed(mouseState.LeftButton, lastMouseState.LeftButton);
    }

    /// <summary>
    /// Gets a value indicating whether the left mouse button was released this frame.
    /// </summary>
    /// <returns>True when the left button transitioned to released.</returns>
    public bool WasMouseLeftButtonReleased() {
        return WasMouseButtonReleased(mouseState.LeftButton, lastMouseState.LeftButton);
    }

    /// <summary>
    /// Gets a value indicating whether the right mouse button was pressed this frame.
    /// </summary>
    /// <returns>True when the right button transitioned to pressed.</returns>
    public bool WasMouseRightButtonPressed() {
        return WasMouseButtonPressed(mouseState.RightButton, lastMouseState.RightButton);
    }

    /// <summary>
    /// Gets a value indicating whether the right mouse button was released this frame.
    /// </summary>
    /// <returns>True when the right button transitioned to released.</returns>
    public bool WasMouseRightButtonReleased() {
        return WasMouseButtonReleased(mouseState.RightButton, lastMouseState.RightButton);
    }

    /// <summary>
    /// Gets a value indicating whether the middle mouse button was pressed this frame.
    /// </summary>
    /// <returns>True when the middle button transitioned to pressed.</returns>
    public bool WasMouseMiddleButtonPressed() {
        return WasMouseButtonPressed(mouseState.MiddleButton, lastMouseState.MiddleButton);
    }

    /// <summary>
    /// Gets a value indicating whether the middle mouse button was released this frame.
    /// </summary>
    /// <returns>True when the middle button transitioned to released.</returns>
    public bool WasMouseMiddleButtonReleased() {
        return WasMouseButtonReleased(mouseState.MiddleButton, lastMouseState.MiddleButton);
    }

    /// <summary>
    /// Gets a value indicating whether the XButton1 was pressed this frame.
    /// </summary>
    /// <returns>True when the XButton1 transitioned to pressed.</returns>
    public bool WasMouseXButton1Pressed() {
        return WasMouseButtonPressed(mouseState.XButton1, lastMouseState.XButton1);
    }

    /// <summary>
    /// Gets a value indicating whether the XButton1 was released this frame.
    /// </summary>
    /// <returns>True when the XButton1 transitioned to released.</returns>
    public bool WasMouseXButton1Released() {
        return WasMouseButtonReleased(mouseState.XButton1, lastMouseState.XButton1);
    }

    /// <summary>
    /// Gets a value indicating whether the XButton2 was pressed this frame.
    /// </summary>
    /// <returns>True when the XButton2 transitioned to pressed.</returns>
    public bool WasMouseXButton2Pressed() {
        return WasMouseButtonPressed(mouseState.XButton2, lastMouseState.XButton2);
    }

    /// <summary>
    /// Gets a value indicating whether the XButton2 was released this frame.
    /// </summary>
    /// <returns>True when the XButton2 transitioned to released.</returns>
    public bool WasMouseXButton2Released() {
        return WasMouseButtonReleased(mouseState.XButton2, lastMouseState.XButton2);
    }

    /// <summary>
    /// Gets a value indicating whether the provided key is currently pressed.
    /// </summary>
    /// <param name="key">Key to query.</param>
    /// <returns>True when the key is down.</returns>
    public bool IsKeyDown(Keys key) {
        return keyboardState.IsKeyDown(key);
    }

    /// <summary>
    /// Gets a value indicating whether the provided key is currently released.
    /// </summary>
    /// <param name="key">Key to query.</param>
    /// <returns>True when the key is up.</returns>
    public bool IsKeyUp(Keys key) {
        return keyboardState.IsKeyUp(key);
    }

    /// <summary>
    /// Gets a value indicating whether the provided key was pressed this frame.
    /// </summary>
    /// <param name="key">Key to query.</param>
    /// <returns>True when the key transitioned to down.</returns>
    public bool WasKeyPressed(Keys key) {
        return keyboardState.IsKeyDown(key) && lastKeyboardState.IsKeyUp(key);
    }

    /// <summary>
    /// Gets a value indicating whether the provided key was released this frame.
    /// </summary>
    /// <param name="key">Key to query.</param>
    /// <returns>True when the key transitioned to up.</returns>
    public bool WasKeyReleased(Keys key) {
        return keyboardState.IsKeyUp(key) && lastKeyboardState.IsKeyDown(key);
    }

    /// <summary>
    /// Processes mouse state, performs hit tests, and dispatches pointer events.
    /// </summary>
    public virtual void Update() {
        EnsureInputStateCaptured();

        try {
            var objectManager = core.ObjectManager;
            List<IInteractable2D> interactables = objectManager.Interactables;
            List<IDrawable2D> drawables2D = objectManager.Drawables2D;

            PointerInteraction interaction = PointerInteraction.None;
            if (mouseState.LeftButton == ButtonState.Released &&
                lastMouseState.LeftButton == ButtonState.Pressed) {
                interaction = PointerInteraction.Release;
            } else if (mouseState.LeftButton == ButtonState.Pressed &&
                lastMouseState.LeftButton == ButtonState.Released) {
                interaction = PointerInteraction.Press;
            }

            // Determine the topmost camera under the cursor
            ICamera topCamera = GetTopmostCameraAt(mouseState.X, mouseState.Y);

            // Compute local mouse coordinates within that camera's viewport
            float2 localMouse = new float2(mouseState.X, mouseState.Y);
            if (topCamera != null) {
                float4 vp = topCamera.Viewport;
                localMouse.X -= vp.X;
                localMouse.Y -= vp.Y;
            }

            // If we have a captured interactable (pressed), send events to it regardless
            if (Highlighted != null) {
                // Use the camera captured at press time for relative coords
                float2 capturedLocal = new float2(mouseState.X, mouseState.Y);
                if (capturedCamera != null) {
                    float4 cvp = capturedCamera.Viewport;
                    capturedLocal.X -= cvp.X;
                    capturedLocal.Y -= cvp.Y;
                }

                int2 rel = new int2(
                    (int)MathF.Round(capturedLocal.X - Highlighted.Parent.Position.X),
                    (int)MathF.Round(capturedLocal.Y - Highlighted.Parent.Position.Y)
                );

                int deltaX = mouseDelta.X;
                int deltaY = mouseDelta.Y;
                if (interaction == PointerInteraction.None && (deltaX != 0 || deltaY != 0)) {
                    interaction = PointerInteraction.Hover;
                }

                Highlighted.OnCursor(rel, new int2(deltaX, deltaY), interaction);

                if (interaction == PointerInteraction.Release) {
                    Highlighted = null;
                    capturedCamera = null;
                }

                return; // Captured; do not process hover routing
            }

            // No captured interactable; hit test within the topmost camera
            IInteractable2D hit = null;
            byte hitRenderOrder = 0;
            int hitDrawableIndex = -1;
            int hitInteractableIndex = -1;
            if (topCamera != null) {
                ushort camMask = topCamera.LayerMask;
                for (int i = 0; i < interactables.Count; i++) {
                    IInteractable2D interactable = interactables[i];

                    // Filter by camera layer mask
                    if ((interactable.Parent.LayerMask & camMask) == 0) continue;

                    float3 pos = interactable.Parent.Position;
                    int2 size = interactable.Size;
                    float4 rect = new float4(pos.X, pos.Y, size.X, size.Y);

                    if (rect.Contains(localMouse.X, localMouse.Y)) {
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
            }

            // Handle hover/leave transitions and press
            bool hoveringChanged = hit != Hovering;
            if (hoveringChanged) {
                if (Hovering != null) {
                    // Compute relative coords for the previous hovered for a clean leave event
                    float2 prevLocal = new float2(mouseState.X, mouseState.Y);
                    ICamera prevCam = FindCameraForInteractableAt(Hovering, mouseState.X, mouseState.Y);
                    if (prevCam != null) {
                        float4 vp = prevCam.Viewport;
                        prevLocal.X -= vp.X;
                        prevLocal.Y -= vp.Y;
                    }
                    int2 prevRel = new int2(
                        (int)MathF.Round(prevLocal.X - Hovering.Parent.Position.X),
                        (int)MathF.Round(prevLocal.Y - Hovering.Parent.Position.Y)
                    );
                    Hovering.OnCursor(prevRel, new int2(0, 0), PointerInteraction.Leave);
                }
                Hovering = hit;
            }

            if (Hovering != null) {
                // Calculate relative position within the hit interactable
                int2 rel = new int2(
                    (int)MathF.Round(localMouse.X - Hovering.Parent.Position.X),
                    (int)MathF.Round(localMouse.Y - Hovering.Parent.Position.Y)
                );

                int deltaX = mouseDelta.X;
                int deltaY = mouseDelta.Y;

                // Click started on this interactable
                if (interaction == PointerInteraction.Press) {
                    if (hoveringChanged) {
                        Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Hover);
                    }

                    Highlighted = Hovering;
                    capturedCamera = topCamera;
                    Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Press);
                } else {
                    // Entering a newly visible interactable must also raise hover even without pointer movement.
                    if (hoveringChanged || deltaX != 0 || deltaY != 0) {
                        Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Hover);
                    }
                }
            } else {
                // Not over any interactable
                if (Hovering != null) {
                    Hovering.OnCursor(new int2(0, 0), new int2(0, 0), PointerInteraction.Leave);
                    Hovering = null;
                }
            }
        } finally {
            CommitPointerWrapState();
            hasCapturedInput = false;
        }
    }

    /// <summary>
    /// Finds the topmost back-buffer camera whose viewport contains the given screen coordinates.
    /// </summary>
    private ICamera GetTopmostCameraAt(int x, int y) {
        var cameras = core.ObjectManager.Cameras;
        for (int i = cameras.Count - 1; i >= 0; i--) {
            var cam = cameras[i];
            if (cam.RenderTarget != null) {
                continue;
            }

            var vp = cam.Viewport;
            if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W) {
                return cam;
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the topmost back-buffer camera containing the specified interactable at the given mouse position.
    /// </summary>
    private ICamera FindCameraForInteractableAt(IInteractable2D interactable, int mouseX, int mouseY) {
        var cameras = core.ObjectManager.Cameras;
        for (int i = cameras.Count - 1; i >= 0; i--) {
            var cam = cameras[i];
            if (cam.RenderTarget != null) {
                continue;
            }

            if ((interactable.Parent.LayerMask & cam.LayerMask) == 0) {
                continue;
            }
            var vp = cam.Viewport;
            if (mouseX >= vp.X && mouseX < vp.X + vp.Z && mouseY >= vp.Y && mouseY < vp.Y + vp.W) {
                return cam;
            }
        }
        return null;
    }

    /// <summary>
    /// Ensures input state is captured once per frame.
    /// </summary>
    void EnsureInputStateCaptured() {
        if (hasCapturedInput) {
            return;
        }

        CaptureInputState();
    }

    /// <summary>
    /// Captures current keyboard and mouse states, updating cached values.
    /// </summary>
    void CaptureInputState() {
        lastMouseState = mouseState;
        mouseState = Mouse.GetState();
        int2 pointerWrapDeltaOffset = Mouse.ConsumePointerWrapDeltaOffset();
        mouseDelta = new int2(
            mouseState.X - lastMouseState.X + pointerWrapDeltaOffset.X,
            mouseState.Y - lastMouseState.Y + pointerWrapDeltaOffset.Y
        );

        lastKeyboardState = keyboardState;
        keyboardState = Keyboard.GetState();

        hasCapturedInput = true;
    }

    /// <summary>
    /// Applies the currently active pointer-wrap state to the mouse backend before input capture begins.
    /// </summary>
    void ApplyPointerWrapState() {
        if (Mouse == null) {
            return;
        }

        Mouse.SetPointerWrapEnabled(ActivePointerWrapEnabled);
    }

    /// <summary>
    /// Commits all pointer-wrap requests gathered during the current frame and updates the backend for the next frame.
    /// </summary>
    void CommitPointerWrapState() {
        ActivePointerWrapEnabled = RequestedPointerWrapEnabled;
        RequestedPointerWrapEnabled = false;

        if (Mouse == null) {
            return;
        }

        Mouse.SetPointerWrapEnabled(ActivePointerWrapEnabled);
    }

    /// <summary>
    /// Returns true when a mouse button transitioned to pressed this frame.
    /// </summary>
    /// <param name="current">Current button state.</param>
    /// <param name="previous">Previous button state.</param>
    /// <returns>True when the button transitioned to pressed.</returns>
    bool WasMouseButtonPressed(ButtonState current, ButtonState previous) {
        return current == ButtonState.Pressed && previous == ButtonState.Released;
    }

    /// <summary>
    /// Returns true when a mouse button transitioned to released this frame.
    /// </summary>
    /// <param name="current">Current button state.</param>
    /// <param name="previous">Previous button state.</param>
    /// <returns>True when the button transitioned to released.</returns>
    bool WasMouseButtonReleased(ButtonState current, ButtonState previous) {
        return current == ButtonState.Released && previous == ButtonState.Pressed;
    }

    /// <summary>
    /// Resolves the highest render order used by drawables owned by the interactable hierarchy.
    /// </summary>
    /// <param name="drawables">Registered 2D drawables.</param>
    /// <param name="interactable">Interactable being evaluated.</param>
    /// <param name="cameraLayerMask">Layer mask used by the active camera.</param>
    /// <param name="highestDrawableIndex">Receives the most recent matching drawable index.</param>
    /// <returns>Highest render order found for the interactable hierarchy.</returns>
    byte GetTopDrawableRenderOrder(
        List<IDrawable2D> drawables,
        IInteractable2D interactable,
        ushort cameraLayerMask,
        out int highestDrawableIndex) {
        if (drawables == null) {
            throw new ArgumentNullException(nameof(drawables));
        }
        if (interactable == null) {
            throw new ArgumentNullException(nameof(interactable));
        }

        byte highestRenderOrder = 0;
        highestDrawableIndex = -1;
        Entity interactableRoot = interactable.Parent;
        for (int i = 0; i < drawables.Count; i++) {
            IDrawable2D drawable = drawables[i];
            if (drawable == null || drawable.Parent == null) {
                continue;
            }
            if ((drawable.Parent.LayerMask & cameraLayerMask) == 0) {
                continue;
            }
            if (!IsSameEntityOrDescendant(drawable.Parent, interactableRoot)) {
                continue;
            }

            if (highestDrawableIndex < 0 ||
                drawable.RenderOrder2D > highestRenderOrder ||
                (drawable.RenderOrder2D == highestRenderOrder && i > highestDrawableIndex)) {
                highestRenderOrder = drawable.RenderOrder2D;
                highestDrawableIndex = i;
            }
        }

        return highestRenderOrder;
    }

    /// <summary>
    /// Returns true when the candidate interactable should be treated as visually in front of the current hit.
    /// </summary>
    /// <param name="candidateRenderOrder">Highest render order for the candidate.</param>
    /// <param name="candidateDrawableIndex">Latest drawable index for the candidate.</param>
    /// <param name="candidateInteractableIndex">Registration index for the candidate interactable.</param>
    /// <param name="currentRenderOrder">Highest render order for the current hit.</param>
    /// <param name="currentDrawableIndex">Latest drawable index for the current hit.</param>
    /// <param name="currentInteractableIndex">Registration index for the current hit.</param>
    /// <returns>True when the candidate should replace the current hit.</returns>
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
    /// Returns true when the candidate entity is the same as the root entity or is parented under it.
    /// </summary>
    /// <param name="candidate">Entity to inspect.</param>
    /// <param name="root">Root entity that owns the interactable.</param>
    /// <returns>True when the candidate belongs to the interactable hierarchy.</returns>
    bool IsSameEntityOrDescendant(Entity candidate, Entity root) {
        if (candidate == null) {
            throw new ArgumentNullException(nameof(candidate));
        }
        if (root == null) {
            throw new ArgumentNullException(nameof(root));
        }

        Entity current = candidate;
        while (current != null) {
            if (ReferenceEquals(current, root)) {
                return true;
            }
            current = current.Parent;
        }

        return false;
    }
}

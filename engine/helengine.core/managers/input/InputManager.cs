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
    /// Captures keyboard and mouse input at the start of a frame.
    /// </summary>
    public virtual void EarlyUpdate() {
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
        return new int2(mouseState.X - lastMouseState.X, mouseState.Y - lastMouseState.Y);
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

                int deltaX = mouseState.X - lastMouseState.X;
                int deltaY = mouseState.Y - lastMouseState.Y;
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
                        hit = interactable;
                        // We do not break to allow later interactables to override earlier ones if overlapping
                    }
                }
            }

            // Handle hover/leave transitions and press
            if (hit != Hovering) {
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

                int deltaX = mouseState.X - lastMouseState.X;
                int deltaY = mouseState.Y - lastMouseState.Y;

                // Click started on this interactable
                if (interaction == PointerInteraction.Press) {
                    Highlighted = Hovering;
                    capturedCamera = topCamera;
                    Hovering.OnCursor(rel, new int2(deltaX, deltaY), PointerInteraction.Press);
                } else {
                    // Hover movement
                    if (deltaX != 0 || deltaY != 0) {
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
            hasCapturedInput = false;
        }
    }

    /// <summary>
    /// Finds the topmost camera whose viewport contains the given screen coordinates.
    /// </summary>
    private ICamera GetTopmostCameraAt(int x, int y) {
        var cameraBuckets = core.ObjectManager.Cameras;
        for (int b = cameraBuckets.Length - 1; b >= 0; b--) {
            var list = cameraBuckets[b];
            for (int i = list.Count - 1; i >= 0; i--) {
                var cam = list[i];
                var vp = cam.Viewport;
                if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W) {
                    return cam;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds the camera containing the specified interactable at the given mouse position.
    /// </summary>
    private ICamera FindCameraForInteractableAt(IInteractable2D interactable, int mouseX, int mouseY) {
        var cameraBuckets = core.ObjectManager.Cameras;
        for (int b = cameraBuckets.Length - 1; b >= 0; b--) {
            var list = cameraBuckets[b];
            for (int i = list.Count - 1; i >= 0; i--) {
                var cam = list[i];
                if ((interactable.Parent.LayerMask & cam.LayerMask) == 0) continue;
                var vp = cam.Viewport;
                if (mouseX >= vp.X && mouseX < vp.X + vp.Z && mouseY >= vp.Y && mouseY < vp.Y + vp.W) {
                    return cam;
                }
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

        lastKeyboardState = keyboardState;
        keyboardState = Keyboard.GetState();

        hasCapturedInput = true;
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
}

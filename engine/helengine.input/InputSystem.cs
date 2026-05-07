namespace helengine;

/// <summary>
/// Resolves logical actions from raw frame input and active contexts.
/// </summary>
public sealed class InputSystem {
    /// <summary>
    /// Binding records owned by this input system.
    /// </summary>
    readonly List<InputBinding> Bindings;
    /// <summary>
    /// Stack of currently active input contexts.
    /// </summary>
    readonly List<int> ActiveContextStack;
    /// <summary>
    /// Resolved action state for the current frame.
    /// </summary>
    readonly Dictionary<int, InputActionState> CurrentActionStates;
    /// <summary>
    /// Resolved action state for the previous frame.
    /// </summary>
    readonly Dictionary<int, InputActionState> PreviousActionStates;
    /// <summary>
    /// Tracks the actions that appeared in the active context stack during the current frame.
    /// </summary>
    readonly List<int> SeenActionIds;
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
    /// Tracks whether client-edge pointer wrapping is currently active.
    /// </summary>
    bool ActivePointerWrapEnabled;
    /// <summary>
    /// Tracks whether any interaction requested pointer wrapping for the next frame.
    /// </summary>
    bool RequestedPointerWrapEnabled;
    /// <summary>
    /// Cached mouse client bounds used for wrap calculations.
    /// </summary>
    int2 MouseClientBounds;
    /// <summary>
    /// Delta offset introduced by the most recent pointer wrap.
    /// </summary>
    int2 PointerWrapDeltaOffset;
    /// <summary>
    /// Tracks whether keyboard capture is active for the current backend.
    /// </summary>
    bool KeyboardIsActive;
    /// <summary>
    /// Optional frame callback invoked after input capture and before pointer-wrap state is committed.
    /// </summary>
    Action FrameUpdateHandler;

    /// <summary>
    /// Initializes a new input system with no backend and no bindings.
    /// </summary>
    public InputSystem() {
        Bindings = new List<InputBinding>();
        ActiveContextStack = new List<int>();
        CurrentActionStates = new Dictionary<int, InputActionState>();
        PreviousActionStates = new Dictionary<int, InputActionState>();
        SeenActionIds = new List<int>();
        keyboardState = new KeyboardState();
        mouseState = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        KeyboardIsActive = true;
        CurrentFrame = new InputFrameState {
            Keyboard = keyboardState,
            Mouse = mouseState
        };
    }

    /// <summary>
    /// Gets or sets the backend used to capture raw frame input.
    /// </summary>
    public IInputBackend Backend { get; private set; }

    /// <summary>
    /// Gets the most recently captured raw frame.
    /// </summary>
    public InputFrameState CurrentFrame { get; private set; }

    /// <summary>
    /// Gets a value indicating whether client-edge pointer wrapping is currently active.
    /// </summary>
    public bool IsPointerWrapEnabled {
        get { return ActivePointerWrapEnabled; }
    }

    /// <summary>
    /// Binds a backend that will be queried on the next frame capture.
    /// </summary>
    /// <param name="backend">Backend that captures raw input.</param>
    public void SetBackend(IInputBackend backend) {
        Backend = backend;
    }

    /// <summary>
    /// Sets the optional frame callback that should process the captured input state.
    /// </summary>
    /// <param name="handler">Callback invoked during the update phase.</param>
    public void SetFrameUpdateHandler(Action handler) {
        FrameUpdateHandler = handler;
    }

    /// <summary>
    /// Enables or disables keyboard input capture for the current backend.
    /// </summary>
    /// <param name="isActive">True to capture key state; false to ignore input.</param>
    public void SetKeyboardActive(bool isActive) {
        KeyboardIsActive = isActive;
    }

    /// <summary>
    /// Replaces the keyboard state exposed by this system when deterministic test input is needed.
    /// </summary>
    /// <param name="state">Keyboard state to expose on the next capture.</param>
    public void SetKeyboardState(KeyboardState state) {
        keyboardState = state;
        InputFrameState currentFrame = CurrentFrame;
        currentFrame.Keyboard = state;
        CurrentFrame = currentFrame;
    }

    /// <summary>
    /// Replaces the mouse state exposed by this system when deterministic test input is needed.
    /// </summary>
    /// <param name="state">Mouse state to expose on the next capture.</param>
    public void SetMouseState(MouseState state) {
        mouseState = state;
        InputFrameState currentFrame = CurrentFrame;
        currentFrame.Mouse = state;
        CurrentFrame = currentFrame;
    }

    /// <summary>
    /// Configures the simulated client bounds used by pointer wrapping.
    /// </summary>
    /// <param name="clientBounds">Client width and height for the simulated target.</param>
    public void SetMouseClientBounds(int2 clientBounds) {
        MouseClientBounds = clientBounds;
    }

    /// <summary>
    /// Enables or disables client-edge pointer wrapping for the active mouse backend.
    /// </summary>
    /// <param name="isEnabled">True when active interactions should wrap across the client bounds.</param>
    public void SetPointerWrapEnabled(bool isEnabled) {
        ActivePointerWrapEnabled = isEnabled;
        RequestedPointerWrapEnabled = isEnabled;
    }

    /// <summary>
    /// Requests client-edge pointer wrapping for the next input frame.
    /// </summary>
    public void RequestPointerWrapEnabled() {
        RequestedPointerWrapEnabled = true;
    }

    /// <summary>
    /// Registers one action binding in the input system.
    /// </summary>
    /// <param name="binding">Binding to add.</param>
    public void RegisterBinding(InputBinding binding) {
        Bindings.Add(binding);
    }

    /// <summary>
    /// Removes every binding that belongs to the supplied context.
    /// </summary>
    /// <param name="contextId">Context whose bindings should be removed.</param>
    public void ClearBindings(InputContextId contextId) {
        for (int i = Bindings.Count - 1; i >= 0; i--) {
            if (Bindings[i].ContextId != contextId) {
                continue;
            }

            Bindings.RemoveAt(i);
        }
    }

    /// <summary>
    /// Pushes a context onto the active stack so its bindings become eligible.
    /// </summary>
    /// <param name="contextId">Context to activate.</param>
    public void PushContext(InputContextId contextId) {
        ActiveContextStack.Add(contextId.Value);
    }

    /// <summary>
    /// Removes the most recent matching context from the active stack.
    /// </summary>
    /// <param name="contextId">Context to deactivate.</param>
    public void PopContext(InputContextId contextId) {
        for (int i = ActiveContextStack.Count - 1; i >= 0; i--) {
            if (ActiveContextStack[i] != contextId.Value) {
                continue;
            }

            ActiveContextStack.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// Clears every active context from the stack.
    /// </summary>
    public void ClearContexts() {
        ActiveContextStack.Clear();
    }

    /// <summary>
    /// Captures keyboard and mouse input at the start of a frame.
    /// </summary>
    public void EarlyUpdate() {
        ApplyPointerWrapState();
        EnsureInputStateCaptured();
        ResolveBindings();
    }

    /// <summary>
    /// Completes the frame by committing pointer-wrap state and clearing the capture latch.
    /// </summary>
    public void Update() {
        EnsureInputStateCaptured();
        try {
            if (FrameUpdateHandler != null) {
                FrameUpdateHandler();
            }
        } finally {
            CommitPointerWrapState();
            hasCapturedInput = false;
        }
    }

    /// <summary>
    /// Returns the current resolved state for one action.
    /// </summary>
    /// <param name="actionId">Action to query.</param>
    /// <returns>Resolved action state for the current frame.</returns>
    public InputActionState GetActionState(InputActionId actionId) {
        if (CurrentActionStates.TryGetValue(actionId.Value, out InputActionState currentState)) {
            return currentState;
        }

        return new InputActionState();
    }

    /// <summary>
    /// Returns whether the supplied action is currently active.
    /// </summary>
    /// <param name="actionId">Action to query.</param>
    /// <returns>True when the action is active.</returns>
    public bool IsActionDown(InputActionId actionId) {
        return GetActionState(actionId).IsDown;
    }

    /// <summary>
    /// Returns whether the supplied action transitioned to active on this frame.
    /// </summary>
    /// <param name="actionId">Action to query.</param>
    /// <returns>True when the action was pressed this frame.</returns>
    public bool WasActionPressed(InputActionId actionId) {
        return GetActionState(actionId).WasPressed;
    }

    /// <summary>
    /// Returns whether the supplied action transitioned to inactive on this frame.
    /// </summary>
    /// <param name="actionId">Action to query.</param>
    /// <returns>True when the action was released this frame.</returns>
    public bool WasActionReleased(InputActionId actionId) {
        return GetActionState(actionId).WasReleased;
    }

    /// <summary>
    /// Returns the current analog value for the supplied action.
    /// </summary>
    /// <param name="actionId">Action to query.</param>
    /// <returns>Resolved action value for the current frame.</returns>
    public float GetActionValue(InputActionId actionId) {
        return GetActionState(actionId).Value;
    }

    /// <summary>
    /// Gets the current pointer state.
    /// </summary>
    /// <returns>Current pointer state snapshot.</returns>
    public InputPointerState GetPointerState() {
        return CurrentFrame.Pointer;
    }

    /// <summary>
    /// Gets the number of captured gamepads in the current frame.
    /// </summary>
    /// <returns>Number of valid gamepad entries.</returns>
    public int GetGamepadCount() {
        return CurrentFrame.GamepadCount;
    }

    /// <summary>
    /// Gets one captured gamepad state by index.
    /// </summary>
    /// <param name="index">Zero-based gamepad index.</param>
    /// <returns>Captured gamepad state, or a default state when the index is invalid.</returns>
    public InputGamepadState GetGamepadState(int index) {
        if (CurrentFrame.Gamepads == null) {
            return new InputGamepadState();
        }
        if (index < 0 || index >= CurrentFrame.GamepadCount || index >= CurrentFrame.Gamepads.Length) {
            return new InputGamepadState();
        }

        return CurrentFrame.Gamepads[index];
    }

    /// <summary>
    /// Gets the captured text input state.
    /// </summary>
    /// <returns>Text state captured for the current frame.</returns>
    public InputTextState GetTextState() {
        return CurrentFrame.Text;
    }

    /// <summary>
    /// Gets the cached mouse position in window coordinates.
    /// </summary>
    /// <returns>Mouse position in pixels.</returns>
    public int2 GetMousePosition() {
        return new int2(mouseState.X, mouseState.Y);
    }

    /// <summary>
    /// Gets the cached mouse X coordinate in window space.
    /// </summary>
    /// <returns>Mouse X position in pixels.</returns>
    public int GetMouseX() {
        return mouseState.X;
    }

    /// <summary>
    /// Gets the cached mouse Y coordinate in window space.
    /// </summary>
    /// <returns>Mouse Y position in pixels.</returns>
    public int GetMouseY() {
        return mouseState.Y;
    }

    /// <summary>
    /// Gets the mouse movement delta between the current and previous frame.
    /// </summary>
    /// <returns>Mouse movement delta in pixels.</returns>
    public int2 GetMouseDelta() {
        return mouseDelta;
    }

    /// <summary>
    /// Gets the cached mouse movement delta on the X axis.
    /// </summary>
    /// <returns>Mouse movement delta in pixels for X.</returns>
    public int GetMouseDeltaX() {
        return mouseDelta.X;
    }

    /// <summary>
    /// Gets the cached mouse movement delta on the Y axis.
    /// </summary>
    /// <returns>Mouse movement delta in pixels for Y.</returns>
    public int GetMouseDeltaY() {
        return mouseDelta.Y;
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
    /// Gets a value indicating whether one mouse button transitioned from released to pressed.
    /// </summary>
    /// <param name="currentState">Current button state.</param>
    /// <param name="previousState">Previous button state.</param>
    /// <returns>True when the button pressed this frame.</returns>
    bool WasMouseButtonPressed(ButtonState currentState, ButtonState previousState) {
        return currentState == ButtonState.Pressed && previousState == ButtonState.Released;
    }

    /// <summary>
    /// Gets a value indicating whether one mouse button transitioned from pressed to released.
    /// </summary>
    /// <param name="currentState">Current button state.</param>
    /// <param name="previousState">Previous button state.</param>
    /// <returns>True when the button released this frame.</returns>
    bool WasMouseButtonReleased(ButtonState currentState, ButtonState previousState) {
        return currentState == ButtonState.Released && previousState == ButtonState.Pressed;
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
        lastKeyboardState = keyboardState;

        if (Backend != null) {
            InputFrameState backendFrame = Backend.CaptureFrame();
            if (KeyboardIsActive) {
                keyboardState = backendFrame.Keyboard;
            }
            mouseState = backendFrame.Mouse;
            CurrentFrame = backendFrame;
        } else {
            InputFrameState currentFrame = CurrentFrame;
            currentFrame.Keyboard = keyboardState;
            currentFrame.Mouse = mouseState;
            CurrentFrame = currentFrame;
        }

        int2 pointerWrapDeltaOffset = ConsumePointerWrapDeltaOffset();
        mouseDelta = new int2(
            mouseState.X - lastMouseState.X + pointerWrapDeltaOffset.X,
            mouseState.Y - lastMouseState.Y + pointerWrapDeltaOffset.Y
        );

        InputFrameState updatedFrame = CurrentFrame;
        updatedFrame.Keyboard = keyboardState;
        updatedFrame.Mouse = mouseState;
        updatedFrame.Pointer = new InputPointerState {
            Connected = true,
            X = mouseState.X,
            Y = mouseState.Y,
            DeltaX = mouseDelta.X,
            DeltaY = mouseDelta.Y,
            ScrollDelta = GetMouseScrollWheelDelta(),
            Buttons = BuildPointerButtonMask(mouseState)
        };
        CurrentFrame = updatedFrame;

        hasCapturedInput = true;
    }

    /// <summary>
    /// Applies the currently active pointer-wrap state to the cached mouse state before input capture begins.
    /// </summary>
    void ApplyPointerWrapState() {
        if (!ActivePointerWrapEnabled) {
            return;
        }

        ApplyPointerWrap();
    }

    /// <summary>
    /// Commits all pointer-wrap requests gathered during the current frame and updates the cached state for the next frame.
    /// </summary>
    void CommitPointerWrapState() {
        ActivePointerWrapEnabled = RequestedPointerWrapEnabled;
        RequestedPointerWrapEnabled = false;
    }

    /// <summary>
    /// Applies client-edge wrapping to the cached mouse state when wrapping is active.
    /// </summary>
    void ApplyPointerWrap() {
        if (!ActivePointerWrapEnabled) {
            return;
        }

        if (MouseClientBounds.X <= 1 || MouseClientBounds.Y <= 1) {
            return;
        }

        int wrappedX = mouseState.X;
        int wrappedY = mouseState.Y;
        int deltaOffsetX = 0;
        int deltaOffsetY = 0;

        if (mouseState.X <= 0) {
            wrappedX = MouseClientBounds.X - 2;
            deltaOffsetX = -(MouseClientBounds.X - 2);
        } else if (mouseState.X >= MouseClientBounds.X - 1) {
            wrappedX = 1;
            deltaOffsetX = MouseClientBounds.X - 2;
        }

        if (mouseState.Y <= 0) {
            wrappedY = MouseClientBounds.Y - 2;
            deltaOffsetY = -(MouseClientBounds.Y - 2);
        } else if (mouseState.Y >= MouseClientBounds.Y - 1) {
            wrappedY = 1;
            deltaOffsetY = MouseClientBounds.Y - 2;
        }

        if (deltaOffsetX == 0 && deltaOffsetY == 0) {
            return;
        }

        mouseState.X = wrappedX;
        mouseState.Y = wrappedY;
        PointerWrapDeltaOffset = new int2(deltaOffsetX, deltaOffsetY);
        InputFrameState currentFrame = CurrentFrame;
        currentFrame.Mouse = mouseState;
        InputPointerState pointer = currentFrame.Pointer;
        pointer.X = wrappedX;
        pointer.Y = wrappedY;
        int pointerDeltaX = pointer.DeltaX + deltaOffsetX;
        int pointerDeltaY = pointer.DeltaY + deltaOffsetY;
        pointer.DeltaX = pointerDeltaX;
        pointer.DeltaY = pointerDeltaY;
        currentFrame.Pointer = pointer;
        CurrentFrame = currentFrame;
    }

    /// <summary>
    /// Returns and clears the delta offset produced by the most recent pointer wrap.
    /// </summary>
    /// <returns>Offset that must be added to preserve continuous mouse movement.</returns>
    int2 ConsumePointerWrapDeltaOffset() {
        int2 pointerWrapDeltaOffset = PointerWrapDeltaOffset;
        PointerWrapDeltaOffset = new int2(0, 0);
        return pointerWrapDeltaOffset;
    }

    /// <summary>
    /// Builds a pointer button mask from the cached mouse state.
    /// </summary>
    /// <param name="state">Mouse state to inspect.</param>
    /// <returns>Pointer button bitmask.</returns>
    ulong BuildPointerButtonMask(MouseState state) {
        ulong buttons = 0;
        if (state.LeftButton == ButtonState.Pressed) {
            buttons |= 1UL << (int)InputPointerButton.Primary;
        }
        if (state.RightButton == ButtonState.Pressed) {
            buttons |= 1UL << (int)InputPointerButton.Secondary;
        }
        if (state.MiddleButton == ButtonState.Pressed) {
            buttons |= 1UL << (int)InputPointerButton.Middle;
        }
        if (state.XButton1 == ButtonState.Pressed) {
            buttons |= 1UL << (int)InputPointerButton.Back;
        }
        if (state.XButton2 == ButtonState.Pressed) {
            buttons |= 1UL << (int)InputPointerButton.Forward;
        }

        return buttons;
    }

    /// <summary>
    /// Resolves the registered bindings against the current frame and active contexts.
    /// </summary>
    void ResolveBindings() {
        List<int> previousActionKeys = new List<int>();
        foreach (int actionKey in PreviousActionStates.Keys) {
            previousActionKeys.Add(actionKey);
        }

        for (int index = 0; index < previousActionKeys.Count; index++) {
            int actionKey = previousActionKeys[index];
            PreviousActionStates.Remove(actionKey);
        }

        List<int> currentActionKeys = new List<int>();
        foreach (int actionKey in CurrentActionStates.Keys) {
            currentActionKeys.Add(actionKey);
        }

        for (int index = 0; index < currentActionKeys.Count; index++) {
            int actionKey = currentActionKeys[index];
            PreviousActionStates[actionKey] = CurrentActionStates[actionKey];
            CurrentActionStates.Remove(actionKey);
        }

        SeenActionIds.Clear();

        for (int contextIndex = ActiveContextStack.Count - 1; contextIndex >= 0; contextIndex--) {
            int activeContextValue = ActiveContextStack[contextIndex];
            Dictionary<int, float> contextActionValues = new Dictionary<int, float>();
            List<int> contextActionKeys = new List<int>();
            for (int bindingIndex = 0; bindingIndex < Bindings.Count; bindingIndex++) {
                InputBinding binding = Bindings[bindingIndex];
                if (binding.ContextId.Value != activeContextValue) {
                    continue;
                }

                float value = ResolveBindingValue(binding);
                if (!SeenActionIds.Contains(binding.ActionId.Value)) {
                    SeenActionIds.Add(binding.ActionId.Value);
                }
                if (value == 0f) {
                    continue;
                }

                int actionKey = binding.ActionId.Value;
                if (contextActionValues.TryGetValue(actionKey, out float currentContextValue)
                    && Math.Abs(value) <= Math.Abs(currentContextValue)) {
                    continue;
                }

                contextActionValues[actionKey] = value;
                if (!contextActionKeys.Contains(actionKey)) {
                    contextActionKeys.Add(actionKey);
                }
            }

            foreach (int actionKey in contextActionKeys) {
                if (CurrentActionStates.ContainsKey(actionKey)) {
                    continue;
                }

                StoreActionValue(new InputActionId(actionKey), contextActionValues[actionKey]);
            }
        }

        foreach (int actionKey in SeenActionIds) {
            if (CurrentActionStates.ContainsKey(actionKey)) {
                continue;
            }

            InputActionState previousState;
            if (!PreviousActionStates.TryGetValue(actionKey, out previousState)) {
                previousState = new InputActionState();
            }

            CurrentActionStates[actionKey] = new InputActionState {
                Value = 0f,
                IsDown = false,
                WasPressed = false,
                WasReleased = previousState.IsDown
            };
        }

        ApplyActionTransitions();
    }

    /// <summary>
    /// Stores the strongest value seen for an action during this frame.
    /// </summary>
    /// <param name="actionId">Action identifier.</param>
    /// <param name="value">Resolved control value.</param>
    void StoreActionValue(InputActionId actionId, float value) {
        int key = actionId.Value;
        if (!CurrentActionStates.TryGetValue(key, out InputActionState currentState)) {
            currentState = new InputActionState();
        }

        if (Math.Abs(value) <= Math.Abs(currentState.Value)) {
            return;
        }

        currentState.Value = value;
        currentState.IsDown = value != 0f;
        CurrentActionStates[key] = currentState;
    }

    /// <summary>
    /// Computes pressed and released transitions for all resolved actions.
    /// </summary>
    void ApplyActionTransitions() {
        List<int> resolvedActionKeys = new List<int>();
        foreach (int actionKey in CurrentActionStates.Keys) {
            resolvedActionKeys.Add(actionKey);
        }
        for (int i = 0; i < resolvedActionKeys.Count; i++) {
            int actionKey = resolvedActionKeys[i];
            InputActionState currentState = CurrentActionStates[actionKey];
            InputActionState previousState;
            if (!PreviousActionStates.TryGetValue(actionKey, out previousState)) {
                previousState = new InputActionState();
            }

            currentState.IsDown = currentState.Value != 0f;
            currentState.WasPressed = currentState.IsDown && !previousState.IsDown;
            currentState.WasReleased = !currentState.IsDown && previousState.IsDown;
            CurrentActionStates[actionKey] = currentState;
        }
    }

    /// <summary>
    /// Resolves one binding against the current raw frame state.
    /// </summary>
    /// <param name="binding">Binding to resolve.</param>
    /// <returns>Normalized control value.</returns>
    float ResolveBindingValue(InputBinding binding) {
        switch (binding.Control.DeviceKind) {
            case InputDeviceKind.Gamepad:
                return ResolveGamepadBindingValue(binding);
            case InputDeviceKind.Pointer:
                return ResolvePointerBindingValue(binding);
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Resolves one gamepad binding against the current raw frame state.
    /// </summary>
    /// <param name="binding">Binding to resolve.</param>
    /// <returns>Normalized control value.</returns>
    float ResolveGamepadBindingValue(InputBinding binding) {
        if (!TryGetGamepad(binding.Control.DeviceIndex, out InputGamepadState gamepad)) {
            return 0f;
        }

        switch (binding.Control.ControlKind) {
            case InputControlKind.Button:
                return IsGamepadButtonDown(gamepad, binding.Control.ControlIndex) ? binding.Scale : 0f;
            case InputControlKind.Axis:
                return GetGamepadAxisValue(gamepad, binding.Control.ControlIndex) * binding.Scale;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Resolves one pointer binding against the current raw frame state.
    /// </summary>
    /// <param name="binding">Binding to resolve.</param>
    /// <returns>Normalized control value.</returns>
    float ResolvePointerBindingValue(InputBinding binding) {
        switch (binding.Control.ControlKind) {
            case InputControlKind.Button:
                return IsPointerButtonDown(CurrentFrame.Pointer, binding.Control.ControlIndex) ? binding.Scale : 0f;
            case InputControlKind.PointerDelta:
                if (binding.Control.ControlIndex == 0) {
                    return CurrentFrame.Pointer.DeltaX * binding.Scale;
                }

                return CurrentFrame.Pointer.DeltaY * binding.Scale;
            case InputControlKind.ScrollWheel:
                return CurrentFrame.Pointer.ScrollDelta * binding.Scale;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Returns whether the current frame contains the requested gamepad.
    /// </summary>
    /// <param name="deviceIndex">Zero-based gamepad index.</param>
    /// <param name="gamepad">Receives the current gamepad state when available.</param>
    /// <returns>True when the gamepad exists in the current frame.</returns>
    bool TryGetGamepad(int deviceIndex, out InputGamepadState gamepad) {
        gamepad = new InputGamepadState();
        if (CurrentFrame.Gamepads == null) {
            return false;
        }
        if (deviceIndex < 0 || deviceIndex >= CurrentFrame.GamepadCount || deviceIndex >= CurrentFrame.Gamepads.Length) {
            return false;
        }

        gamepad = CurrentFrame.Gamepads[deviceIndex];
        return gamepad.Connected;
    }

    /// <summary>
    /// Returns whether one abstract gamepad button is down.
    /// </summary>
    /// <param name="gamepad">Gamepad state to inspect.</param>
    /// <param name="buttonIndex">Button index on the gamepad.</param>
    /// <returns>True when the button bit is active.</returns>
    bool IsGamepadButtonDown(InputGamepadState gamepad, int buttonIndex) {
        if (buttonIndex < 0 || buttonIndex >= 64) {
            return false;
        }

        return (gamepad.Buttons & (1UL << buttonIndex)) != 0;
    }

    /// <summary>
    /// Returns the value of one abstract gamepad axis.
    /// </summary>
    /// <param name="gamepad">Gamepad state to inspect.</param>
    /// <param name="axisIndex">Axis index on the gamepad.</param>
    /// <returns>Signed axis value.</returns>
    float GetGamepadAxisValue(InputGamepadState gamepad, int axisIndex) {
        switch (axisIndex) {
            case 0:
                return gamepad.LeftStickX;
            case 1:
                return gamepad.LeftStickY;
            case 2:
                return gamepad.RightStickX;
            case 3:
                return gamepad.RightStickY;
            case 4:
                return gamepad.LeftTrigger;
            case 5:
                return gamepad.RightTrigger;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Returns whether one abstract pointer button is down.
    /// </summary>
    /// <param name="pointer">Pointer state to inspect.</param>
    /// <param name="buttonIndex">Button index on the pointer.</param>
    /// <returns>True when the button bit is active.</returns>
    bool IsPointerButtonDown(InputPointerState pointer, int buttonIndex) {
        if (buttonIndex < 0 || buttonIndex >= 64) {
            return false;
        }

        return (pointer.Buttons & (1UL << buttonIndex)) != 0;
    }
}

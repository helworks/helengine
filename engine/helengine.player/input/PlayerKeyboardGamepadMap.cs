namespace helengine.player {
    /// <summary>
    /// Turns desktop keyboard state into an emulated engine pad, so a gamepad-only platform can be played without a
    /// physical controller. The default layout mirrors the PS2 pad: arrows are the D-pad, Z/X/C/V are
    /// Cross/Circle/Square/Triangle, WASD and IJKL are the sticks, Q/E are L1/R1, 1/3 are L2/R2, F/H are L3/R3,
    /// Enter is Start and Backspace is Select.
    /// </summary>
    public sealed class PlayerKeyboardGamepadMap {
        /// <summary>Full stick deflection produced by one held direction key.</summary>
        public const short FullDeflection = short.MaxValue;

        /// <summary>Key-to-button bindings, in the order they are described at startup.</summary>
        readonly PlayerKeyboardGamepadButtonBinding[] ButtonBindings;

        /// <summary>Key-pair-to-axis bindings, in the order they are described at startup.</summary>
        readonly PlayerKeyboardGamepadAxisBinding[] AxisBindings;

        /// <summary>
        /// Initializes a keyboard pad from explicit bindings.
        /// </summary>
        /// <param name="buttonBindings">Key-to-button bindings.</param>
        /// <param name="axisBindings">Key-pair-to-axis bindings.</param>
        /// <exception cref="ArgumentNullException">Either binding list is null.</exception>
        public PlayerKeyboardGamepadMap(IReadOnlyList<PlayerKeyboardGamepadButtonBinding> buttonBindings, IReadOnlyList<PlayerKeyboardGamepadAxisBinding> axisBindings) {
            if (buttonBindings == null) {
                throw new ArgumentNullException(nameof(buttonBindings));
            } else if (axisBindings == null) {
                throw new ArgumentNullException(nameof(axisBindings));
            }

            ButtonBindings = buttonBindings.ToArray();
            AxisBindings = axisBindings.ToArray();
        }

        /// <summary>
        /// Creates the default PS2-style keyboard pad layout.
        /// </summary>
        /// <returns>The default keyboard pad.</returns>
        public static PlayerKeyboardGamepadMap CreateDefault() {
            PlayerKeyboardGamepadButtonBinding[] buttons = {
                new PlayerKeyboardGamepadButtonBinding(Keys.Up, InputGamepadButton.DPadUp),
                new PlayerKeyboardGamepadButtonBinding(Keys.Down, InputGamepadButton.DPadDown),
                new PlayerKeyboardGamepadButtonBinding(Keys.Left, InputGamepadButton.DPadLeft),
                new PlayerKeyboardGamepadButtonBinding(Keys.Right, InputGamepadButton.DPadRight),
                new PlayerKeyboardGamepadButtonBinding(Keys.Z, InputGamepadButton.South),
                new PlayerKeyboardGamepadButtonBinding(Keys.X, InputGamepadButton.East),
                new PlayerKeyboardGamepadButtonBinding(Keys.C, InputGamepadButton.West),
                new PlayerKeyboardGamepadButtonBinding(Keys.V, InputGamepadButton.North),
                new PlayerKeyboardGamepadButtonBinding(Keys.Q, InputGamepadButton.LeftShoulder),
                new PlayerKeyboardGamepadButtonBinding(Keys.E, InputGamepadButton.RightShoulder),
                new PlayerKeyboardGamepadButtonBinding(Keys.D1, InputGamepadButton.LeftTrigger),
                new PlayerKeyboardGamepadButtonBinding(Keys.D3, InputGamepadButton.RightTrigger),
                new PlayerKeyboardGamepadButtonBinding(Keys.F, InputGamepadButton.LeftStick),
                new PlayerKeyboardGamepadButtonBinding(Keys.H, InputGamepadButton.RightStick),
                new PlayerKeyboardGamepadButtonBinding(Keys.Enter, InputGamepadButton.Start),
                new PlayerKeyboardGamepadButtonBinding(Keys.Back, InputGamepadButton.Select)
            };
            PlayerKeyboardGamepadAxisBinding[] axes = {
                new PlayerKeyboardGamepadAxisBinding(Keys.A, Keys.D, PlayerGamepadAxis.LeftStickX),
                new PlayerKeyboardGamepadAxisBinding(Keys.W, Keys.S, PlayerGamepadAxis.LeftStickY),
                new PlayerKeyboardGamepadAxisBinding(Keys.J, Keys.L, PlayerGamepadAxis.RightStickX),
                new PlayerKeyboardGamepadAxisBinding(Keys.I, Keys.K, PlayerGamepadAxis.RightStickY)
            };
            return new PlayerKeyboardGamepadMap(buttons, axes);
        }

        /// <summary>
        /// Builds the emulated pad for one frame of keyboard state. Held L2/R2 keys also report a fully pulled analog
        /// trigger, matching how the PS2 native host reports its digital L2/R2.
        /// </summary>
        /// <param name="keyboard">Desktop keyboard state for the frame.</param>
        /// <returns>The emulated pad, marked connected.</returns>
        public InputGamepadState Map(KeyboardState keyboard) {
            InputGamepadState pad = new InputGamepadState { Connected = true };
            for (int i = 0; i < ButtonBindings.Length; i++) {
                PlayerKeyboardGamepadButtonBinding binding = ButtonBindings[i];
                if (keyboard.IsKeyDown(binding.Key)) {
                    pad.SetButtonDown(binding.Button, true);
                }
            }

            for (int i = 0; i < AxisBindings.Length; i++) {
                PlayerKeyboardGamepadAxisBinding binding = AxisBindings[i];
                int value = 0;
                if (keyboard.IsKeyDown(binding.NegativeKey)) {
                    value -= FullDeflection;
                }
                if (keyboard.IsKeyDown(binding.PositiveKey)) {
                    value += FullDeflection;
                }

                SetAxis(ref pad, binding.Axis, (short)value);
            }

            pad.LeftTrigger = pad.IsButtonDown(InputGamepadButton.LeftTrigger) ? short.MaxValue : (short)0;
            pad.RightTrigger = pad.IsButtonDown(InputGamepadButton.RightTrigger) ? short.MaxValue : (short)0;
            return pad;
        }

        /// <summary>
        /// Describes every binding as one line (<c>Z -> South</c>, <c>W/S -> LeftStickY -/+</c>) for the player's
        /// startup log.
        /// </summary>
        /// <returns>One line per binding, buttons first.</returns>
        public IReadOnlyList<string> Describe() {
            List<string> lines = new List<string>(ButtonBindings.Length + AxisBindings.Length);
            for (int i = 0; i < ButtonBindings.Length; i++) {
                lines.Add(ButtonBindings[i].Key + " -> " + ButtonBindings[i].Button);
            }
            for (int i = 0; i < AxisBindings.Length; i++) {
                PlayerKeyboardGamepadAxisBinding binding = AxisBindings[i];
                lines.Add(binding.NegativeKey + "/" + binding.PositiveKey + " -> " + binding.Axis + " -/+");
            }

            return lines;
        }

        /// <summary>
        /// Writes one stick axis of a pad.
        /// </summary>
        /// <param name="pad">Pad to update.</param>
        /// <param name="axis">Axis to write.</param>
        /// <param name="value">Axis value.</param>
        /// <exception cref="ArgumentOutOfRangeException">The axis is not a known stick axis.</exception>
        static void SetAxis(ref InputGamepadState pad, PlayerGamepadAxis axis, short value) {
            if (axis == PlayerGamepadAxis.LeftStickX) {
                pad.LeftStickX = value;
            } else if (axis == PlayerGamepadAxis.LeftStickY) {
                pad.LeftStickY = value;
            } else if (axis == PlayerGamepadAxis.RightStickX) {
                pad.RightStickX = value;
            } else if (axis == PlayerGamepadAxis.RightStickY) {
                pad.RightStickY = value;
            } else {
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unknown keyboard pad stick axis.");
            }
        }
    }
}

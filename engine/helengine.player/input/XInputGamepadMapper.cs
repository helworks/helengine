namespace helengine.player {
    /// <summary>
    /// Converts a raw XInput controller report into the engine pad so a desktop controller drives the same engine
    /// buttons as the PS2 pad on real hardware: A/B/X/Y sit where Cross/Circle/Square/Triangle sit (South/East/West/North),
    /// LB/RB are L1/R1, the triggers are L2/R2, Back is Select. Stick Y is inverted because XInput reports up as positive
    /// while the PS2 native host reports up as negative.
    /// </summary>
    public static class XInputGamepadMapper {
        /// <summary><c>XINPUT_GAMEPAD_DPAD_UP</c>.</summary>
        public const ushort DPadUpMask = 0x0001;

        /// <summary><c>XINPUT_GAMEPAD_DPAD_DOWN</c>.</summary>
        public const ushort DPadDownMask = 0x0002;

        /// <summary><c>XINPUT_GAMEPAD_DPAD_LEFT</c>.</summary>
        public const ushort DPadLeftMask = 0x0004;

        /// <summary><c>XINPUT_GAMEPAD_DPAD_RIGHT</c>.</summary>
        public const ushort DPadRightMask = 0x0008;

        /// <summary><c>XINPUT_GAMEPAD_START</c>.</summary>
        public const ushort StartMask = 0x0010;

        /// <summary><c>XINPUT_GAMEPAD_BACK</c>.</summary>
        public const ushort BackMask = 0x0020;

        /// <summary><c>XINPUT_GAMEPAD_LEFT_THUMB</c> (stick click).</summary>
        public const ushort LeftThumbMask = 0x0040;

        /// <summary><c>XINPUT_GAMEPAD_RIGHT_THUMB</c> (stick click).</summary>
        public const ushort RightThumbMask = 0x0080;

        /// <summary><c>XINPUT_GAMEPAD_LEFT_SHOULDER</c>.</summary>
        public const ushort LeftShoulderMask = 0x0100;

        /// <summary><c>XINPUT_GAMEPAD_RIGHT_SHOULDER</c>.</summary>
        public const ushort RightShoulderMask = 0x0200;

        /// <summary><c>XINPUT_GAMEPAD_A</c>.</summary>
        public const ushort AMask = 0x1000;

        /// <summary><c>XINPUT_GAMEPAD_B</c>.</summary>
        public const ushort BMask = 0x2000;

        /// <summary><c>XINPUT_GAMEPAD_X</c>.</summary>
        public const ushort XMask = 0x4000;

        /// <summary><c>XINPUT_GAMEPAD_Y</c>.</summary>
        public const ushort YMask = 0x8000;

        /// <summary><c>XINPUT_GAMEPAD_LEFT_THUMB_DEADZONE</c>: left stick values at or below this magnitude read as centered.</summary>
        public const short LeftThumbDeadzone = 7849;

        /// <summary><c>XINPUT_GAMEPAD_RIGHT_THUMB_DEADZONE</c>: right stick values at or below this magnitude read as centered.</summary>
        public const short RightThumbDeadzone = 8689;

        /// <summary><c>XINPUT_GAMEPAD_TRIGGER_THRESHOLD</c>: trigger values at or below this read as released.</summary>
        public const byte TriggerThreshold = 30;

        /// <summary>Largest raw XInput trigger value.</summary>
        const double MaxRawTrigger = 255d;

        /// <summary>
        /// Maps one raw report to a connected engine pad.
        /// </summary>
        /// <param name="raw">Raw XInput controller report.</param>
        /// <returns>The engine pad, marked connected.</returns>
        public static InputGamepadState Map(XInputGamepad raw) {
            InputGamepadState pad = new InputGamepadState { Connected = true };
            pad.SetButtonDown(InputGamepadButton.DPadUp, (raw.Buttons & DPadUpMask) != 0);
            pad.SetButtonDown(InputGamepadButton.DPadDown, (raw.Buttons & DPadDownMask) != 0);
            pad.SetButtonDown(InputGamepadButton.DPadLeft, (raw.Buttons & DPadLeftMask) != 0);
            pad.SetButtonDown(InputGamepadButton.DPadRight, (raw.Buttons & DPadRightMask) != 0);
            pad.SetButtonDown(InputGamepadButton.Start, (raw.Buttons & StartMask) != 0);
            pad.SetButtonDown(InputGamepadButton.Select, (raw.Buttons & BackMask) != 0);
            pad.SetButtonDown(InputGamepadButton.LeftStick, (raw.Buttons & LeftThumbMask) != 0);
            pad.SetButtonDown(InputGamepadButton.RightStick, (raw.Buttons & RightThumbMask) != 0);
            pad.SetButtonDown(InputGamepadButton.LeftShoulder, (raw.Buttons & LeftShoulderMask) != 0);
            pad.SetButtonDown(InputGamepadButton.RightShoulder, (raw.Buttons & RightShoulderMask) != 0);
            pad.SetButtonDown(InputGamepadButton.South, (raw.Buttons & AMask) != 0);
            pad.SetButtonDown(InputGamepadButton.East, (raw.Buttons & BMask) != 0);
            pad.SetButtonDown(InputGamepadButton.West, (raw.Buttons & XMask) != 0);
            pad.SetButtonDown(InputGamepadButton.North, (raw.Buttons & YMask) != 0);
            pad.SetButtonDown(InputGamepadButton.LeftTrigger, raw.LeftTrigger > TriggerThreshold);
            pad.SetButtonDown(InputGamepadButton.RightTrigger, raw.RightTrigger > TriggerThreshold);

            pad.LeftStickX = ApplyDeadzone(raw.LeftThumbX, LeftThumbDeadzone);
            pad.LeftStickY = InvertAxis(ApplyDeadzone(raw.LeftThumbY, LeftThumbDeadzone));
            pad.RightStickX = ApplyDeadzone(raw.RightThumbX, RightThumbDeadzone);
            pad.RightStickY = InvertAxis(ApplyDeadzone(raw.RightThumbY, RightThumbDeadzone));
            pad.LeftTrigger = ScaleTrigger(raw.LeftTrigger);
            pad.RightTrigger = ScaleTrigger(raw.RightTrigger);
            return pad;
        }

        /// <summary>
        /// Centers a stick axis whose magnitude does not exceed the deadzone, as the PS2 host does for its own sticks.
        /// </summary>
        /// <param name="value">Raw axis value.</param>
        /// <param name="deadzone">Largest magnitude still treated as centered.</param>
        /// <returns>The raw value, or zero inside the deadzone.</returns>
        static short ApplyDeadzone(short value, short deadzone) {
            return Math.Abs((int)value) <= deadzone ? (short)0 : value;
        }

        /// <summary>
        /// Flips an axis direction, clamping the one value (<c>-32768</c>) whose negation does not fit a short.
        /// </summary>
        /// <param name="value">Axis value to flip.</param>
        /// <returns>The negated value.</returns>
        static short InvertAxis(short value) {
            return (short)Math.Min(-(int)value, short.MaxValue);
        }

        /// <summary>
        /// Scales a raw trigger byte to the engine's 0..32767 trigger range; a trigger at or below the XInput threshold
        /// reads as released, so the analog value agrees with the digital L2/R2 button.
        /// </summary>
        /// <param name="value">Raw trigger value.</param>
        /// <returns>The scaled trigger value.</returns>
        static short ScaleTrigger(byte value) {
            if (value <= TriggerThreshold) {
                return 0;
            }

            return (short)Math.Round(value * (short.MaxValue / MaxRawTrigger));
        }
    }
}

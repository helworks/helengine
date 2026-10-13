namespace helengine.player.tests.input {
    /// <summary>
    /// Verifies the default keyboard-emulated pad: every mapped key presses the engine button or deflects the stick axis
    /// the real PS2 pad would, and an idle keyboard yields a neutral pad.
    /// </summary>
    public sealed class PlayerKeyboardGamepadMapTests {
        /// <summary>Each mapped key presses exactly its engine gamepad button and nothing else.</summary>
        /// <param name="key">Keyboard key held down.</param>
        /// <param name="button">Engine button the key must press.</param>
        [Theory]
        [InlineData(Keys.Up, InputGamepadButton.DPadUp)]
        [InlineData(Keys.Down, InputGamepadButton.DPadDown)]
        [InlineData(Keys.Left, InputGamepadButton.DPadLeft)]
        [InlineData(Keys.Right, InputGamepadButton.DPadRight)]
        [InlineData(Keys.Z, InputGamepadButton.South)]
        [InlineData(Keys.X, InputGamepadButton.East)]
        [InlineData(Keys.C, InputGamepadButton.West)]
        [InlineData(Keys.V, InputGamepadButton.North)]
        [InlineData(Keys.Q, InputGamepadButton.LeftShoulder)]
        [InlineData(Keys.E, InputGamepadButton.RightShoulder)]
        [InlineData(Keys.D1, InputGamepadButton.LeftTrigger)]
        [InlineData(Keys.D3, InputGamepadButton.RightTrigger)]
        [InlineData(Keys.F, InputGamepadButton.LeftStick)]
        [InlineData(Keys.H, InputGamepadButton.RightStick)]
        [InlineData(Keys.Enter, InputGamepadButton.Start)]
        [InlineData(Keys.Back, InputGamepadButton.Select)]
        public void Map_WhenButtonKeyDown_PressesOnlyThatButton(Keys key, InputGamepadButton button) {
            InputGamepadState pad = PlayerKeyboardGamepadMap.CreateDefault().Map(new KeyboardState(key));

            Assert.Equal(1UL << (int)button, pad.Buttons);
        }

        /// <summary>Each stick key deflects exactly its axis fully, with up and left negative like the PS2 pad.</summary>
        /// <param name="key">Keyboard key held down.</param>
        /// <param name="axis">Stick axis the key must deflect.</param>
        /// <param name="expected">Expected axis value.</param>
        [Theory]
        [InlineData(Keys.A, PlayerGamepadAxis.LeftStickX, -32767)]
        [InlineData(Keys.D, PlayerGamepadAxis.LeftStickX, 32767)]
        [InlineData(Keys.W, PlayerGamepadAxis.LeftStickY, -32767)]
        [InlineData(Keys.S, PlayerGamepadAxis.LeftStickY, 32767)]
        [InlineData(Keys.J, PlayerGamepadAxis.RightStickX, -32767)]
        [InlineData(Keys.L, PlayerGamepadAxis.RightStickX, 32767)]
        [InlineData(Keys.I, PlayerGamepadAxis.RightStickY, -32767)]
        [InlineData(Keys.K, PlayerGamepadAxis.RightStickY, 32767)]
        public void Map_WhenStickKeyDown_DeflectsOnlyThatAxis(Keys key, PlayerGamepadAxis axis, short expected) {
            InputGamepadState pad = PlayerKeyboardGamepadMap.CreateDefault().Map(new KeyboardState(key));

            Assert.Equal(axis == PlayerGamepadAxis.LeftStickX ? expected : (short)0, pad.LeftStickX);
            Assert.Equal(axis == PlayerGamepadAxis.LeftStickY ? expected : (short)0, pad.LeftStickY);
            Assert.Equal(axis == PlayerGamepadAxis.RightStickX ? expected : (short)0, pad.RightStickX);
            Assert.Equal(axis == PlayerGamepadAxis.RightStickY ? expected : (short)0, pad.RightStickY);
            Assert.Equal(0UL, pad.Buttons);
        }

        /// <summary>Holding both keys of one axis cancels out instead of picking one direction.</summary>
        [Fact]
        public void Map_WhenOpposingStickKeysDown_LeavesAxisCentered() {
            InputGamepadState pad = PlayerKeyboardGamepadMap.CreateDefault().Map(new KeyboardState(Keys.A, Keys.D));

            Assert.Equal(0, pad.LeftStickX);
        }

        /// <summary>The trigger keys also report a fully pulled analog trigger, as the PS2 native host does for L2 and R2.</summary>
        [Fact]
        public void Map_WhenTriggerKeysDown_ReportsFullAnalogTriggers() {
            InputGamepadState pad = PlayerKeyboardGamepadMap.CreateDefault().Map(new KeyboardState(Keys.D1, Keys.D3));

            Assert.Equal(short.MaxValue, pad.LeftTrigger);
            Assert.Equal(short.MaxValue, pad.RightTrigger);
        }

        /// <summary>An idle keyboard produces a pad with no buttons, centered sticks and released triggers.</summary>
        [Fact]
        public void Map_WhenNoKeysDown_ReturnsNeutralPad() {
            InputGamepadState pad = PlayerKeyboardGamepadMap.CreateDefault().Map(new KeyboardState());

            Assert.Equal(0UL, pad.Buttons);
            Assert.Equal(0, pad.LeftStickX);
            Assert.Equal(0, pad.LeftStickY);
            Assert.Equal(0, pad.RightStickX);
            Assert.Equal(0, pad.RightStickY);
            Assert.Equal(0, pad.LeftTrigger);
            Assert.Equal(0, pad.RightTrigger);
        }

        /// <summary>The startup description lists one line per binding, naming the key and the engine control.</summary>
        [Fact]
        public void Describe_ListsEveryBinding() {
            IReadOnlyList<string> lines = PlayerKeyboardGamepadMap.CreateDefault().Describe();

            Assert.Equal(20, lines.Count);
            Assert.Contains("Z -> South", lines);
            Assert.Contains("W/S -> LeftStickY -/+", lines);
        }
    }
}

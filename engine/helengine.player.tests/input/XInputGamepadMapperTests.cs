namespace helengine.player.tests.input {
    /// <summary>
    /// Verifies the pure mapping from a raw XInput gamepad report to the engine pad, laid out so a desktop controller
    /// drives the same engine buttons and axis directions as the real PS2 pad.
    /// </summary>
    public sealed class XInputGamepadMapperTests {
        /// <summary>Each XInput button bit presses exactly its engine button.</summary>
        /// <param name="xinputMask">Raw <c>XINPUT_GAMEPAD</c> button mask.</param>
        /// <param name="button">Engine button the mask must press.</param>
        [Theory]
        [InlineData(0x0001, InputGamepadButton.DPadUp)]
        [InlineData(0x0002, InputGamepadButton.DPadDown)]
        [InlineData(0x0004, InputGamepadButton.DPadLeft)]
        [InlineData(0x0008, InputGamepadButton.DPadRight)]
        [InlineData(0x0010, InputGamepadButton.Start)]
        [InlineData(0x0020, InputGamepadButton.Select)]
        [InlineData(0x0040, InputGamepadButton.LeftStick)]
        [InlineData(0x0080, InputGamepadButton.RightStick)]
        [InlineData(0x0100, InputGamepadButton.LeftShoulder)]
        [InlineData(0x0200, InputGamepadButton.RightShoulder)]
        [InlineData(0x1000, InputGamepadButton.South)]
        [InlineData(0x2000, InputGamepadButton.East)]
        [InlineData(0x4000, InputGamepadButton.West)]
        [InlineData(0x8000, InputGamepadButton.North)]
        public void Map_WhenButtonBitSet_PressesOnlyThatButton(int xinputMask, InputGamepadButton button) {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad { Buttons = (ushort)xinputMask });

            Assert.True(pad.Connected);
            Assert.Equal(1UL << (int)button, pad.Buttons);
        }

        /// <summary>X axes pass through; Y axes are inverted because XInput reports up as positive and the PS2 pad as negative.</summary>
        [Fact]
        public void Map_SticksOutsideDeadzone_KeepXAndInvertY() {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad {
                LeftThumbX = 20000,
                LeftThumbY = 20000,
                RightThumbX = -20000,
                RightThumbY = -20000
            });

            Assert.Equal(20000, pad.LeftStickX);
            Assert.Equal(-20000, pad.LeftStickY);
            Assert.Equal(-20000, pad.RightStickX);
            Assert.Equal(20000, pad.RightStickY);
        }

        /// <summary>A fully pushed-down stick (-32768) inverts to the largest positive value instead of overflowing.</summary>
        [Fact]
        public void Map_WhenStickFullyDown_ClampsInvertedValue() {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad { LeftThumbY = short.MinValue });

            Assert.Equal(short.MaxValue, pad.LeftStickY);
        }

        /// <summary>Small stick drift inside the XInput recommended deadzones reads as centered.</summary>
        [Fact]
        public void Map_SticksInsideDeadzone_ReadAsCentered() {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad {
                LeftThumbX = 7849,
                LeftThumbY = -7849,
                RightThumbX = 8689,
                RightThumbY = -8689
            });

            Assert.Equal(0, pad.LeftStickX);
            Assert.Equal(0, pad.LeftStickY);
            Assert.Equal(0, pad.RightStickX);
            Assert.Equal(0, pad.RightStickY);
        }

        /// <summary>A pulled trigger presses the L2/R2 engine button and reports a scaled analog value.</summary>
        [Fact]
        public void Map_WhenTriggersPulled_PressButtonsAndScaleAnalog() {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad { LeftTrigger = 255, RightTrigger = 128 });

            Assert.True(pad.IsButtonDown(InputGamepadButton.LeftTrigger));
            Assert.True(pad.IsButtonDown(InputGamepadButton.RightTrigger));
            Assert.Equal(short.MaxValue, pad.LeftTrigger);
            Assert.Equal(16448, pad.RightTrigger);
        }

        /// <summary>A trigger resting at or below the XInput threshold is released, both digitally and in analog.</summary>
        [Fact]
        public void Map_WhenTriggersAtThreshold_ReportReleased() {
            InputGamepadState pad = XInputGamepadMapper.Map(new XInputGamepad { LeftTrigger = 30, RightTrigger = 30 });

            Assert.Equal(0UL, pad.Buttons);
            Assert.Equal(0, pad.LeftTrigger);
            Assert.Equal(0, pad.RightTrigger);
        }
    }
}

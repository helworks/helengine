namespace helengine.player.tests.input {
    /// <summary>
    /// Verifies how the desktop controller and the keyboard-emulated pad combine into gamepad slot 0.
    /// </summary>
    public sealed class PlayerGamepadMergerTests {
        /// <summary>Buttons held on either pad are held on the merged pad.</summary>
        [Fact]
        public void Merge_OrsButtons() {
            InputGamepadState physical = new InputGamepadState();
            physical.SetButtonDown(InputGamepadButton.South, true);
            InputGamepadState keyboard = new InputGamepadState();
            keyboard.SetButtonDown(InputGamepadButton.Start, true);

            InputGamepadState merged = PlayerGamepadMerger.Merge(physical, keyboard);

            Assert.True(merged.IsButtonDown(InputGamepadButton.South));
            Assert.True(merged.IsButtonDown(InputGamepadButton.Start));
            Assert.False(merged.IsButtonDown(InputGamepadButton.East));
        }

        /// <summary>Each axis takes the value with the larger magnitude, whichever pad it comes from and whatever its sign.</summary>
        [Fact]
        public void Merge_TakesLargerMagnitudePerAxis() {
            InputGamepadState physical = new InputGamepadState {
                LeftStickX = 12000,
                LeftStickY = -30000,
                RightStickX = 0,
                RightStickY = 5000,
                LeftTrigger = 20000,
                RightTrigger = 0
            };
            InputGamepadState keyboard = new InputGamepadState {
                LeftStickX = -32767,
                LeftStickY = 1000,
                RightStickX = 32767,
                RightStickY = 0,
                LeftTrigger = 0,
                RightTrigger = 32767
            };

            InputGamepadState merged = PlayerGamepadMerger.Merge(physical, keyboard);

            Assert.Equal(-32767, merged.LeftStickX);
            Assert.Equal(-30000, merged.LeftStickY);
            Assert.Equal(32767, merged.RightStickX);
            Assert.Equal(5000, merged.RightStickY);
            Assert.Equal(20000, merged.LeftTrigger);
            Assert.Equal(32767, merged.RightTrigger);
        }

        /// <summary>The merged pad is connected when either source is connected.</summary>
        [Fact]
        public void Merge_ConnectedWhenEitherSourceConnected() {
            Assert.True(PlayerGamepadMerger.Merge(new InputGamepadState(), new InputGamepadState { Connected = true }).Connected);
            Assert.False(PlayerGamepadMerger.Merge(new InputGamepadState(), new InputGamepadState()).Connected);
        }
    }
}

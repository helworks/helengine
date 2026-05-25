using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the engine-owned standard platform action layer resolves configured accept and return inputs through the existing input system.
    /// </summary>
    public sealed class StandardPlatformInputTests {
        /// <summary>
        /// Ensures one configured accept binding resolves through the standard platform action helper.
        /// </summary>
        [Fact]
        public void Initialize_WhenStandardPlatformAcceptBindingIsConfigured_ResolvesPressedAcceptAction() {
            using Core core = CreateCore(CreateConfiguration(new StandardPlatformActionBinding(
                StandardPlatformAction.Accept,
                CreateGamepadButtonControl(InputGamepadButton.South))));
            TestInputBackend input = (TestInputBackend)core.Input.Backend;
            input.SetGamepadStates(new[] { CreatePressedGamepadState(InputGamepadButton.South) });

            input.EarlyUpdate();

            Assert.True(core.StandardPlatformInput.IsActionDown(StandardPlatformAction.Accept));
            Assert.True(core.StandardPlatformInput.WasActionPressed(StandardPlatformAction.Accept));
        }

        /// <summary>
        /// Ensures an unconfigured return binding remains inactive even when another gamepad button is pressed.
        /// </summary>
        [Fact]
        public void Initialize_WhenStandardPlatformReturnBindingIsMissing_ReturnActionRemainsInactive() {
            using Core core = CreateCore(CreateConfiguration(new StandardPlatformActionBinding(
                StandardPlatformAction.Accept,
                CreateGamepadButtonControl(InputGamepadButton.South))));
            TestInputBackend input = (TestInputBackend)core.Input.Backend;
            input.SetGamepadStates(new[] { CreatePressedGamepadState(InputGamepadButton.East) });

            input.EarlyUpdate();

            Assert.False(core.StandardPlatformInput.IsActionDown(StandardPlatformAction.Return));
            Assert.False(core.StandardPlatformInput.WasActionPressed(StandardPlatformAction.Return));
        }

        /// <summary>
        /// Creates one initialized core configured with the supplied standard platform input configuration.
        /// </summary>
        /// <param name="configuration">Standard platform action configuration that should be registered during startup.</param>
        /// <returns>Initialized core ready for input tests.</returns>
        Core CreateCore(StandardPlatformInputConfiguration configuration) {
            Core core = new Core(new CoreInitializationOptions {
                StandardPlatformInputConfiguration = configuration
            });
            TestInputBackend input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), input, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Creates one immutable standard platform input configuration from the supplied bindings.
        /// </summary>
        /// <param name="bindings">Bindings that should be registered for the current runtime.</param>
        /// <returns>Configuration that contains the supplied standard platform action bindings.</returns>
        StandardPlatformInputConfiguration CreateConfiguration(params StandardPlatformActionBinding[] bindings) {
            return new StandardPlatformInputConfiguration(bindings);
        }

        /// <summary>
        /// Creates one physical gamepad-button control reference for the supplied abstract button.
        /// </summary>
        /// <param name="button">Abstract gamepad button that should be referenced.</param>
        /// <returns>Physical control reference for the supplied abstract button.</returns>
        InputControlId CreateGamepadButtonControl(InputGamepadButton button) {
            return new InputControlId(InputDeviceKind.Gamepad, InputControlKind.Button, 0, (int)button);
        }

        /// <summary>
        /// Creates one connected gamepad state with the supplied button pressed.
        /// </summary>
        /// <param name="button">Abstract button that should be marked active.</param>
        /// <returns>Connected gamepad state with the requested button set.</returns>
        InputGamepadState CreatePressedGamepadState(InputGamepadButton button) {
            InputGamepadState gamepadState = new InputGamepadState {
                Connected = true
            };
            gamepadState.SetButtonDown(button, true);
            return gamepadState;
        }
    }
}

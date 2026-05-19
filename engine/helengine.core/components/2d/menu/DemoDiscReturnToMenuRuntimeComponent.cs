namespace helengine {
    /// <summary>
    /// Provides one player-safe compatibility component that returns the active demo-disc scene to the main menu.
    /// </summary>
    public sealed class DemoDiscReturnToMenuRuntimeComponent : UpdateComponent {
        /// <summary>
        /// Stable logical scene id requested by the return-to-menu flow before optional scene-map remapping.
        /// </summary>
        const string MainMenuSceneId = "DemoDiscMainMenu";

        /// <summary>
        /// Performs per-frame input polling for the temporary return-to-menu bind.
        /// </summary>
        public override void Update() {
            InputSystem inputSystem = Core.Instance.Input;
            bool wasReturnPressed = WasKeyboardReturnPressed(inputSystem)
                || WasGamepadReturnPressed(inputSystem);

            if (wasReturnPressed) {
                string resolvedSceneId = SceneMapComponent.ResolveSceneId(MainMenuSceneId);
                Core.Instance.SceneManager.LoadScene(resolvedSceneId, SceneLoadMode.Single);
            }
        }

        /// <summary>
        /// Returns whether one supported desktop return key was pressed during the current frame.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        /// <returns>True when a supported desktop return key pressed during the current frame.</returns>
        bool WasKeyboardReturnPressed(InputSystem inputSystem) {
#if DESKTOP_PLATFORM
            return inputSystem.WasKeyPressed(Keys.Escape) || inputSystem.WasKeyPressed(Keys.Back);
#else
            return false;
#endif
        }

        /// <summary>
        /// Returns whether one supported gamepad return button was pressed during the current frame.
        /// </summary>
        /// <param name="inputSystem">Input system supplying the current frame state.</param>
        /// <returns>True when a supported gamepad return button pressed during the current frame.</returns>
        bool WasGamepadReturnPressed(InputSystem inputSystem) {
            return inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.East)
                || inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.North)
                || inputSystem.WasGamepadButtonPressed(0, InputGamepadButton.Select);
        }
    }
}

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
        /// Interactable host used by pointer-enabled return buttons.
        /// </summary>
        InteractableComponent BoundInteractable;

        /// <summary>
        /// Tracks whether the active pointer press began inside the bound interactable.
        /// </summary>
        bool PointerPressStartedInside;

        /// <summary>
        /// Gets or sets whether keyboard return bindings may trigger the menu load.
        /// </summary>
        public bool AllowKeyboardReturn { get; set; } = true;

        /// <summary>
        /// Gets or sets whether gamepad return bindings may trigger the menu load.
        /// </summary>
        public bool AllowGamepadReturn { get; set; } = true;

        /// <summary>
        /// Gets or sets whether pointer interaction on a sibling interactable may trigger the menu load.
        /// </summary>
        public bool AllowPointerReturn { get; set; } = true;

        /// <summary>
        /// Tracks whether this component already requested a return transition during its current lifetime.
        /// </summary>
        bool SceneLoadWasRequested;

        /// <summary>
        /// Binds one sibling interactable when the component is attached to an authored clickable host.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);
            if (AllowPointerReturn) {
                TryBindInteractable();
            }
        }

        /// <summary>
        /// Performs per-frame input polling for the temporary return-to-menu bind.
        /// </summary>
        public override void Update() {
            if (AllowPointerReturn) {
                TryBindInteractable();
            }

            InputSystem inputSystem = Core.Instance.Input;
            bool wasReturnPressed = (AllowKeyboardReturn && WasKeyboardReturnPressed(inputSystem))
                || (AllowGamepadReturn && WasGamepadReturnPressed(inputSystem));

            if (wasReturnPressed) {
                LoadResolvedMainMenuScene();
            }
        }

        /// <summary>
        /// Releases any bound pointer interactable subscription before the component instance is deleted.
        /// </summary>
        public override void Dispose() {
            UnbindInteractable();
            base.Dispose();
        }

        /// <summary>
        /// Releases any bound pointer interactable subscription when the component detaches from its owner.
        /// </summary>
        /// <param name="entity">Owning entity.</param>
        public override void ComponentRemoved(Entity entity) {
            UnbindInteractable();
            base.ComponentRemoved(entity);
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

        /// <summary>
        /// Binds one sibling interactable so pointer-enabled hosts can trigger return-to-menu clicks.
        /// </summary>
        void TryBindInteractable() {
            if (!AllowPointerReturn) {
                return;
            } else if (BoundInteractable != null || Parent == null || Parent.Components == null) {
                return;
            }

            for (int componentIndex = 0; componentIndex < Parent.Components.Count; componentIndex++) {
                if (Parent.Components[componentIndex] is InteractableComponent interactable) {
                    BoundInteractable = interactable;
                    BoundInteractable.CursorEvent += HandleCursorEvent;
                    return;
                }
            }
        }

        /// <summary>
        /// Releases the current pointer-interactable binding when the host no longer owns this component.
        /// </summary>
        void UnbindInteractable() {
            if (BoundInteractable == null) {
                return;
            }

            BoundInteractable.CursorEvent -= HandleCursorEvent;
            BoundInteractable = null;
            PointerPressStartedInside = false;
        }

        /// <summary>
        /// Handles pointer press and release events routed from one bound clickable host.
        /// </summary>
        /// <param name="relativePosition">Pointer position relative to the interactable.</param>
        /// <param name="delta">Pointer delta reported by the shared interaction router.</param>
        /// <param name="interaction">Current pointer interaction state.</param>
        void HandleCursorEvent(int2 relativePosition, int2 delta, PointerInteraction interaction) {
            if (!AllowPointerReturn) {
                return;
            }

            if (interaction == PointerInteraction.Press) {
                PointerPressStartedInside = true;
                return;
            }
            if (interaction == PointerInteraction.Release) {
                bool shouldReturnToMenu = PointerPressStartedInside;
                PointerPressStartedInside = false;
                if (shouldReturnToMenu) {
                    LoadResolvedMainMenuScene();
                }
                return;
            }
            if (interaction == PointerInteraction.Leave) {
                PointerPressStartedInside = false;
            }
        }

        /// <summary>
        /// Loads the logical main menu scene after resolving any active scene-map override.
        /// </summary>
        void LoadResolvedMainMenuScene() {
            if (SceneLoadWasRequested) {
                return;
            }

            string resolvedSceneId = SceneMapComponent.ResolveSceneId(MainMenuSceneId);
            SceneLoadWasRequested = true;
            Core.Instance.SceneManager.LoadScene(resolvedSceneId, SceneLoadMode.Single);
        }
    }
}

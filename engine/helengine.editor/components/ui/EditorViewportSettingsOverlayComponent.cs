namespace helengine.editor {
    /// <summary>
    /// Non-modal viewport overlay that exposes grid visibility and clip-plane settings for one editor camera.
    /// </summary>
    public class EditorViewportSettingsOverlayComponent : UpdateComponent {
        /// <summary>
        /// Fixed overlay width in pixels.
        /// </summary>
        const int PanelWidth = 260;
        /// <summary>
        /// Fixed overlay height in pixels.
        /// </summary>
        const int PanelHeight = 174;
        /// <summary>
        /// Horizontal panel padding in pixels.
        /// </summary>
        const int PanelPadding = 12;
        /// <summary>
        /// Vertical spacing inserted between stacked control groups.
        /// </summary>
        const int SectionSpacing = 10;
        /// <summary>
        /// Spacing between a slider track and its numeric value readout.
        /// </summary>
        const int SliderValueSpacing = 8;
        /// <summary>
        /// Height of the grid-toggle row, which matches the standard overlay control height.
        /// </summary>
        const int GridToggleRowHeight = EditorPlatformSettingsSection.RowHeight;
        /// <summary>
        /// Height reserved for one section label.
        /// </summary>
        const int SectionLabelHeight = 18;
        /// <summary>
        /// Vertical gap between a section label and its slider row.
        /// </summary>
        const int SectionLabelSpacing = 4;
        /// <summary>
        /// Width of each slider track.
        /// </summary>
        const int SliderWidth = 156;
        /// <summary>
        /// Height of each slider control.
        /// </summary>
        const int SliderHeight = 16;
        /// <summary>
        /// Width reserved for numeric clip-plane value labels.
        /// </summary>
        const int SliderValueWidth = 60;
        /// <summary>
        /// Width of the close button.
        /// </summary>
        const int CloseButtonWidth = 88;
        /// <summary>
        /// Height of the close button.
        /// </summary>
        const int CloseButtonHeight = 24;
        /// <summary>
        /// Minimum authored keyboard step used by the near-plane slider.
        /// </summary>
        const double NearPlaneKeyboardStep = 0.01;
        /// <summary>
        /// Minimum authored keyboard step used by the far-plane slider.
        /// </summary>
        const double FarPlaneKeyboardStep = 1.0;

        /// <summary>
        /// Camera whose viewport settings are edited by this overlay.
        /// </summary>
        readonly CameraComponent Camera;
        /// <summary>
        /// Font used for overlay labels and button text.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Layer mask applied to overlay visuals.
        /// </summary>
        readonly ushort OverlayLayerMask;
        /// <summary>
        /// Delegate that applies viewport grid visibility.
        /// </summary>
        readonly Action<bool> SetGridVisibleAction;
        /// <summary>
        /// Delegate that resolves whether the viewport grid is currently visible.
        /// </summary>
        readonly Func<bool> IsGridVisibleResolver;
        /// <summary>
        /// Owner key used by the input-capture service for the overlay rectangle.
        /// </summary>
        readonly object InputBlockerOwner;

        /// <summary>
        /// Viewport entity that owns this overlay component.
        /// </summary>
        EditorViewport OwnerViewport;
        /// <summary>
        /// Root entity for the overlay panel.
        /// </summary>
        EditorEntity OverlayRoot;
        /// <summary>
        /// Rounded panel background that keeps overlay controls readable over scene content.
        /// </summary>
        RoundedRectComponent OverlayBackground;
        /// <summary>
        /// Transparent hit area that lets the full overlay panel receive pointer input without closing.
        /// </summary>
        InteractableComponent OverlayBackgroundInteractable;
        /// <summary>
        /// Label text for the grid row.
        /// </summary>
        TextComponent GridToggleLabelText;
        /// <summary>
        /// Host entity for the grid-toggle checkbox.
        /// </summary>
        EditorEntity GridToggleCheckBoxHost;
        /// <summary>
        /// Checkbox used to render and toggle the grid visibility state.
        /// </summary>
        CheckBoxComponent GridToggleCheckBox;
        /// <summary>
        /// Label text for the near-plane slider row.
        /// </summary>
        TextComponent NearPlaneLabelText;
        /// <summary>
        /// Value text for the near-plane slider row.
        /// </summary>
        TextComponent NearPlaneValueText;
        /// <summary>
        /// Label text for the far-plane slider row.
        /// </summary>
        TextComponent FarPlaneLabelText;
        /// <summary>
        /// Value text for the far-plane slider row.
        /// </summary>
        TextComponent FarPlaneValueText;
        /// <summary>
        /// Root entity for the close button.
        /// </summary>
        EditorEntity CloseButtonRoot;
        /// <summary>
        /// Background surface for the close button.
        /// </summary>
        RoundedRectComponent CloseButtonBackground;
        /// <summary>
        /// Text shown inside the close button.
        /// </summary>
        TextComponent CloseButtonText;
        /// <summary>
        /// Interactable region for the close button.
        /// </summary>
        InteractableComponent CloseButtonInteractable;
        /// <summary>
        /// Overlay-local focus group used for keyboard traversal.
        /// </summary>
        EditorFocusGroup OverlayFocusGroup;
        /// <summary>
        /// Focus target for the grid-toggle checkbox.
        /// </summary>
        EditorFocusTarget GridToggleFocusTargetInternal;
        /// <summary>
        /// Focus target for the near-plane slider.
        /// </summary>
        EditorFocusTarget NearPlaneFocusTargetInternal;
        /// <summary>
        /// Focus target for the far-plane slider.
        /// </summary>
        EditorFocusTarget FarPlaneFocusTargetInternal;
        /// <summary>
        /// Focus target for the close button.
        /// </summary>
        EditorFocusTarget CloseButtonFocusTargetInternal;
        /// <summary>
        /// Slider used to edit the camera near plane.
        /// </summary>
        EditorSlider NearPlaneSliderInternal;
        /// <summary>
        /// Slider used to edit the camera far plane.
        /// </summary>
        EditorSlider FarPlaneSliderInternal;
        /// <summary>
        /// Settings button focus target that should regain focus when the overlay closes.
        /// </summary>
        EditorFocusTarget SettingsButtonFocusTarget;
        /// <summary>
        /// Local X coordinate of the settings button anchor.
        /// </summary>
        float AnchorX;
        /// <summary>
        /// Local Y coordinate of the overlay top edge.
        /// </summary>
        float AnchorY;
        /// <summary>
        /// Width of the settings button used to align the overlay to the right edge.
        /// </summary>
        int AnchorWidth;
        /// <summary>
        /// Tracks whether the pointer is hovering the close button.
        /// </summary>
        bool CloseButtonHoverState;
        /// <summary>
        /// Tracks whether the close button is currently pressed.
        /// </summary>
        bool CloseButtonPressedState;
        /// <summary>
        /// Tracks whether keyboard focus currently targets the close button.
        /// </summary>
        bool CloseButtonKeyboardFocusState;
        /// <summary>
        /// Prevents slider synchronization from recursively re-entering camera update handlers.
        /// </summary>
        bool IsSynchronizingState;
        /// <summary>
        /// Tracks whether the overlay hierarchy and focus targets were created.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Initializes one viewport settings overlay bound to a specific camera and grid-visibility delegates.
        /// </summary>
        /// <param name="camera">Viewport camera whose settings are edited by the overlay.</param>
        /// <param name="font">Font used for overlay labels and button text.</param>
        /// <param name="overlayLayerMask">Layer mask applied to overlay visuals.</param>
        /// <param name="setGridVisibleAction">Delegate that applies viewport grid visibility.</param>
        /// <param name="isGridVisibleResolver">Delegate that resolves whether the viewport grid is visible.</param>
        public EditorViewportSettingsOverlayComponent(
            CameraComponent camera,
            FontAsset font,
            ushort overlayLayerMask,
            Action<bool> setGridVisibleAction,
            Func<bool> isGridVisibleResolver) {
            Camera = camera ?? throw new ArgumentNullException(nameof(camera));
            Font = font ?? throw new ArgumentNullException(nameof(font));
            OverlayLayerMask = overlayLayerMask;
            SetGridVisibleAction = setGridVisibleAction ?? throw new ArgumentNullException(nameof(setGridVisibleAction));
            IsGridVisibleResolver = isGridVisibleResolver ?? throw new ArgumentNullException(nameof(isGridVisibleResolver));
            InputBlockerOwner = new object();
        }

        /// <summary>
        /// Raised when the overlay open state changes so the owning viewport can refresh button visuals.
        /// </summary>
        public event Action<bool> OpenStateChanged;

        /// <summary>
        /// Gets whether the overlay is currently open and visible.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets the focus target used by the grid-toggle row.
        /// </summary>
        public EditorFocusTarget GridToggleFocusTarget => GridToggleFocusTargetInternal;

        /// <summary>
        /// Gets the focus target used by the near-plane slider row.
        /// </summary>
        public EditorFocusTarget NearPlaneFocusTarget => NearPlaneFocusTargetInternal;

        /// <summary>
        /// Gets the focus target used by the far-plane slider row.
        /// </summary>
        public EditorFocusTarget FarPlaneFocusTarget => FarPlaneFocusTargetInternal;

        /// <summary>
        /// Gets the focus target used by the close button row.
        /// </summary>
        public EditorFocusTarget CloseButtonFocusTarget => CloseButtonFocusTargetInternal;

        /// <summary>
        /// Gets the near-plane slider entity.
        /// </summary>
        public EditorSlider NearPlaneSlider => NearPlaneSliderInternal;

        /// <summary>
        /// Gets the far-plane slider entity.
        /// </summary>
        public EditorSlider FarPlaneSlider => FarPlaneSliderInternal;

        /// <summary>
        /// Creates the overlay hierarchy and registers focus targets when attached to one viewport.
        /// </summary>
        /// <param name="entity">Owning viewport entity.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (IsInitialized) {
                return;
            }

            OwnerViewport = entity as EditorViewport;
            if (OwnerViewport == null) {
                throw new InvalidOperationException("Viewport settings overlay must be attached to an EditorViewport.");
            }

            CreateOverlayRoot();
            CreateGridToggleRow();
            CreateNearPlaneRow();
            CreateFarPlaneRow();
            CreateCloseButtonRow();
            CreateFocusTargets();
            LayoutOverlay();
            OverlayRoot.Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Unregisters focus targets and clears any active overlay input blocker when removed.
        /// </summary>
        /// <param name="entity">Owning viewport entity.</param>
        public override void ComponentRemoved(Entity entity) {
            base.ComponentRemoved(entity);
            ClearInputBlocker();
            UnregisterFocusTargets();
            IsOpen = false;
        }

        /// <summary>
        /// Polls dismissal input and keeps overlay capture bounds synchronized with the viewport layout.
        /// </summary>
        public override void Update() {
            if (!IsInitialized) {
                return;
            }

            if (!IsOpen) {
                ClearInputBlocker();
                return;
            }

            if (OwnerViewport == null || !OwnerViewport.Enabled) {
                Close(null);
                return;
            }

            UpdateInputBlocker();

            InputSystem input = Core.Instance.Input;
            if (input == null) {
                return;
            }

            if (input.WasKeyPressed(Keys.Escape)) {
                HandleEscapeKey(SettingsButtonFocusTarget);
                return;
            }

            if (input.WasMouseLeftButtonPressed() || input.WasMouseRightButtonPressed()) {
                HandleOutsidePointerPressed(input.GetMousePosition(), SettingsButtonFocusTarget);
            }
        }

        /// <summary>
        /// Stores the settings button focus target used for focus restoration when the overlay closes.
        /// </summary>
        /// <param name="settingsButtonFocusTarget">Settings button focus target owned by the viewport toolbar.</param>
        public void SetSettingsButtonFocusTarget(EditorFocusTarget settingsButtonFocusTarget) {
            SettingsButtonFocusTarget = settingsButtonFocusTarget;
        }

        /// <summary>
        /// Repositions the overlay anchor to remain aligned under the viewport settings button.
        /// </summary>
        /// <param name="anchorX">Local X coordinate of the settings button.</param>
        /// <param name="anchorY">Local Y coordinate where the overlay should open.</param>
        /// <param name="anchorWidth">Width of the settings button.</param>
        public void SetAnchorPosition(float anchorX, float anchorY, int anchorWidth) {
            AnchorX = anchorX;
            AnchorY = anchorY;
            AnchorWidth = anchorWidth;
            LayoutOverlay();
            UpdateInputBlocker();
        }

        /// <summary>
        /// Opens the overlay, synchronizes control state from the camera, and focuses the first control.
        /// </summary>
        public void Open() {
            EnsureInitialized();
            SynchronizeFromCamera();
            IsOpen = true;
            OverlayRoot.Enabled = true;
            LayoutOverlay();
            UpdateInputBlocker();
            RaiseOpenStateChanged();
            if (GridToggleFocusTargetInternal != null) {
                EditorKeyboardFocusService.SetFocusedTarget(GridToggleFocusTargetInternal);
            }
        }

        /// <summary>
        /// Closes the overlay and optionally restores focus to the supplied settings button target.
        /// </summary>
        /// <param name="settingsButtonFocusTarget">Settings button target that should regain focus after closing.</param>
        public void Close(EditorFocusTarget settingsButtonFocusTarget) {
            EnsureInitialized();
            if (!IsOpen) {
                return;
            }

            IsOpen = false;
            OverlayRoot.Enabled = false;
            ClearInputBlocker();
            RaiseOpenStateChanged();
            if (settingsButtonFocusTarget != null && settingsButtonFocusTarget.CanReceiveFocus) {
                EditorKeyboardFocusService.SetFocusedTarget(settingsButtonFocusTarget);
            }
        }

        /// <summary>
        /// Closes the overlay when a pointer press occurs outside both the panel and the settings button.
        /// </summary>
        /// <param name="screenPoint">Pointer position in screen coordinates.</param>
        /// <param name="settingsButtonFocusTarget">Settings button target used to avoid premature close while toggling.</param>
        public void HandleOutsidePointerPressed(int2 screenPoint, EditorFocusTarget settingsButtonFocusTarget) {
            EnsureInitialized();
            if (!IsOpen) {
                return;
            }

            if (EditorInputCaptureService.IsPointerBlocked(screenPoint, owner => ReferenceEquals(owner, InputBlockerOwner))) {
                return;
            }

            if (ContainsOverlayPoint(screenPoint)) {
                return;
            }
            if (settingsButtonFocusTarget != null && settingsButtonFocusTarget.ContainsScreenPoint(screenPoint.X, screenPoint.Y)) {
                return;
            }

            Close(settingsButtonFocusTarget);
        }

        /// <summary>
        /// Closes the overlay from the keyboard escape path and restores focus to the settings button.
        /// </summary>
        /// <param name="settingsButtonFocusTarget">Settings button target that should regain focus.</param>
        public void HandleEscapeKey(EditorFocusTarget settingsButtonFocusTarget) {
            Close(settingsButtonFocusTarget);
        }

        /// <summary>
        /// Removes the overlay input blocker if one is currently registered.
        /// </summary>
        public void ClearInputBlocker() {
            EditorInputCaptureService.ClearBlocker(InputBlockerOwner);
        }

        /// <summary>
        /// Registers or refreshes the overlay input blocker while the panel is open.
        /// </summary>
        public void UpdateInputBlocker() {
            if (!IsInitialized || !IsOpen || OverlayRoot == null || OwnerViewport == null) {
                return;
            }

            int2 overlayPosition = GetOverlayScreenPosition();
            EditorInputCaptureService.SetBlocker(InputBlockerOwner, overlayPosition, new int2(PanelWidth, PanelHeight));
        }

        /// <summary>
        /// Creates the shared overlay root and background chrome.
        /// </summary>
        void CreateOverlayRoot() {
            OverlayRoot = new EditorEntity {
                InternalEntity = true,
                LayerMask = OverlayLayerMask,
                Position = new float3(0f, 0f, 0.45f),
                Enabled = false
            };
            OwnerViewport.AddChild(OverlayRoot);

            OverlayBackground = new RoundedRectComponent {
                Size = new int2(PanelWidth, PanelHeight),
                Radius = 6f,
                BorderThickness = 1f,
                FillColor = new byte4(20, 24, 30, 240),
                BorderColor = new byte4(255, 255, 255, 52),
                RenderOrder2D = RenderOrder2D.OverlayBackground
            };
            OverlayRoot.AddComponent(OverlayBackground);

            OverlayBackgroundInteractable = new InteractableComponent {
                Size = new int2(PanelWidth, PanelHeight),
                HoverCursor = PointerCursorKind.Default
            };
            OverlayRoot.AddComponent(OverlayBackgroundInteractable);
        }

        /// <summary>
        /// Creates the grid-toggle label and default checkbox control.
        /// </summary>
        void CreateGridToggleRow() {
            EditorEntity labelRoot = CreateChildRoot();
            OverlayRoot.AddChild(labelRoot);

            GridToggleLabelText = new TextComponent {
                Font = Font,
                Text = "Grid",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(120, GridToggleRowHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            labelRoot.AddComponent(GridToggleLabelText);

            GridToggleCheckBoxHost = CreateChildRoot();
            OverlayRoot.AddChild(GridToggleCheckBoxHost);

            GridToggleCheckBox = new CheckBoxComponent(EditorPlatformSettingsSection.CheckBoxSize, Font);
            GridToggleCheckBox.CheckedChanged += (component, isChecked) => HandleGridToggleCheckedChanged(isChecked);
            GridToggleCheckBox.SetRenderOrders(RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            GridToggleCheckBoxHost.AddComponent(GridToggleCheckBox);
        }

        /// <summary>
        /// Creates the near-plane slider row and binds live camera updates.
        /// </summary>
        void CreateNearPlaneRow() {
            EditorEntity labelRoot = CreateChildRoot();
            OverlayRoot.AddChild(labelRoot);

            NearPlaneLabelText = new TextComponent {
                Font = Font,
                Text = "Near Plane",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(PanelWidth - PanelPadding * 2, SectionLabelHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            labelRoot.AddComponent(NearPlaneLabelText);

            NearPlaneSliderInternal = new EditorSlider(0.01, 10.0, Camera.NearPlaneDistance, EditorSliderScaleMode.Logarithmic, SliderWidth, SliderHeight) {
                InternalEntity = true
            };
            NearPlaneSliderInternal.ApplyLayerMask(OverlayLayerMask);
            NearPlaneSliderInternal.SetRenderOrders(RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            NearPlaneSliderInternal.KeyboardStep = NearPlaneKeyboardStep;
            NearPlaneSliderInternal.ValueChanged += HandleNearPlaneSliderChanged;
            OverlayRoot.AddChild(NearPlaneSliderInternal);

            EditorEntity valueRoot = CreateChildRoot();
            OverlayRoot.AddChild(valueRoot);

            NearPlaneValueText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(SliderValueWidth, SliderHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            valueRoot.AddComponent(NearPlaneValueText);
        }

        /// <summary>
        /// Creates the far-plane slider row and binds live camera updates.
        /// </summary>
        void CreateFarPlaneRow() {
            EditorEntity labelRoot = CreateChildRoot();
            OverlayRoot.AddChild(labelRoot);

            FarPlaneLabelText = new TextComponent {
                Font = Font,
                Text = "Far Plane",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(PanelWidth - PanelPadding * 2, SectionLabelHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            labelRoot.AddComponent(FarPlaneLabelText);

            FarPlaneSliderInternal = new EditorSlider(1.0, 5000.0, Camera.FarPlaneDistance, EditorSliderScaleMode.Logarithmic, SliderWidth, SliderHeight) {
                InternalEntity = true
            };
            FarPlaneSliderInternal.ApplyLayerMask(OverlayLayerMask);
            FarPlaneSliderInternal.SetRenderOrders(RenderOrder2D.OverlayBackground, RenderOrder2D.OverlayForeground);
            FarPlaneSliderInternal.KeyboardStep = FarPlaneKeyboardStep;
            FarPlaneSliderInternal.ValueChanged += HandleFarPlaneSliderChanged;
            OverlayRoot.AddChild(FarPlaneSliderInternal);

            EditorEntity valueRoot = CreateChildRoot();
            OverlayRoot.AddChild(valueRoot);

            FarPlaneValueText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(SliderValueWidth, SliderHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            valueRoot.AddComponent(FarPlaneValueText);
        }

        /// <summary>
        /// Creates the close button row and pointer wiring.
        /// </summary>
        void CreateCloseButtonRow() {
            CloseButtonRoot = CreateChildRoot();
            OverlayRoot.AddChild(CloseButtonRoot);

            CloseButtonBackground = new RoundedRectComponent {
                Size = new int2(CloseButtonWidth, CloseButtonHeight),
                Radius = 4f,
                BorderThickness = 1f,
                FillColor = ThemeManager.Colors.SurfaceInput,
                BorderColor = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = RenderOrder2D.OverlayBackground
            };
            CloseButtonRoot.AddComponent(CloseButtonBackground);

            EditorEntity textRoot = CreateChildRoot();
            CloseButtonRoot.AddChild(textRoot);

            CloseButtonText = new TextComponent {
                Font = Font,
                Text = "Close",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(CloseButtonWidth, CloseButtonHeight),
                RenderOrder2D = RenderOrder2D.OverlayForeground
            };
            textRoot.AddComponent(CloseButtonText);

            CloseButtonInteractable = new InteractableComponent {
                Size = new int2(CloseButtonWidth, CloseButtonHeight)
            };
            CloseButtonInteractable.CursorEvent += (position, delta, interaction) => HandleCloseButtonCursor(interaction);
            CloseButtonRoot.AddComponent(CloseButtonInteractable);
        }

        /// <summary>
        /// Creates and registers the focus targets used by the overlay controls.
        /// </summary>
        void CreateFocusTargets() {
            OverlayFocusGroup = new EditorFocusGroup(
                OwnerViewport,
                2,
                () => OwnerViewport.Enabled && IsOpen,
                ContainsOverlayPoint,
                HandleOverlayGroupActiveChanged);
            EditorKeyboardFocusService.RegisterGroup(OverlayFocusGroup);

            GridToggleFocusTargetInternal = new EditorFocusTarget(
                OverlayFocusGroup,
                0,
                false,
                () => IsOpen && GridToggleCheckBoxHost != null && GridToggleCheckBoxHost.Enabled,
                ContainsGridTogglePoint,
                HandleGridToggleFocusedChanged,
                CanActivateButtonWithKey,
                ActivateGridToggleFromKey);
            EditorKeyboardFocusService.RegisterTarget(GridToggleFocusTargetInternal);

            NearPlaneFocusTargetInternal = new EditorFocusTarget(
                OverlayFocusGroup,
                1,
                false,
                () => IsOpen && NearPlaneSliderInternal.Enabled,
                ContainsNearPlaneSliderPoint,
                HandleNearPlaneFocusedChanged,
                CanAdjustSliderWithKey,
                ActivateNearPlaneFromKey);
            EditorKeyboardFocusService.RegisterTarget(NearPlaneFocusTargetInternal);

            FarPlaneFocusTargetInternal = new EditorFocusTarget(
                OverlayFocusGroup,
                2,
                false,
                () => IsOpen && FarPlaneSliderInternal.Enabled,
                ContainsFarPlaneSliderPoint,
                HandleFarPlaneFocusedChanged,
                CanAdjustSliderWithKey,
                ActivateFarPlaneFromKey);
            EditorKeyboardFocusService.RegisterTarget(FarPlaneFocusTargetInternal);

            CloseButtonFocusTargetInternal = new EditorFocusTarget(
                OverlayFocusGroup,
                3,
                false,
                () => IsOpen && CloseButtonRoot.Enabled,
                ContainsCloseButtonPoint,
                HandleCloseButtonFocusedChanged,
                CanActivateButtonWithKey,
                ActivateCloseButtonFromKey);
            EditorKeyboardFocusService.RegisterTarget(CloseButtonFocusTargetInternal);
        }

        /// <summary>
        /// Unregisters overlay focus targets and group from the shared keyboard-focus service.
        /// </summary>
        void UnregisterFocusTargets() {
            if (GridToggleFocusTargetInternal != null) {
                EditorKeyboardFocusService.UnregisterTarget(GridToggleFocusTargetInternal);
            }
            if (NearPlaneFocusTargetInternal != null) {
                EditorKeyboardFocusService.UnregisterTarget(NearPlaneFocusTargetInternal);
            }
            if (FarPlaneFocusTargetInternal != null) {
                EditorKeyboardFocusService.UnregisterTarget(FarPlaneFocusTargetInternal);
            }
            if (CloseButtonFocusTargetInternal != null) {
                EditorKeyboardFocusService.UnregisterTarget(CloseButtonFocusTargetInternal);
            }
            if (OverlayFocusGroup != null) {
                EditorKeyboardFocusService.UnregisterGroup(OverlayFocusGroup);
            }
        }

        /// <summary>
        /// Recomputes the panel and row positions from the current settings-button anchor.
        /// </summary>
        void LayoutOverlay() {
            if (OverlayRoot == null || OverlayBackground == null || OwnerViewport == null) {
                return;
            }

            float panelX = ResolvePanelLeft();
            OverlayRoot.Position = new float3(panelX, AnchorY, 0.45f);
            OverlayBackground.Size = new int2(PanelWidth, PanelHeight);
            if (OverlayBackgroundInteractable != null) {
                OverlayBackgroundInteractable.Size = new int2(PanelWidth, PanelHeight);
            }

            int gridRowY = PanelPadding;
            int nearLabelY = gridRowY + GridToggleRowHeight + SectionSpacing;
            int nearSliderY = nearLabelY + SectionLabelHeight + SectionLabelSpacing;
            int farLabelY = nearSliderY + SliderHeight + SectionSpacing;
            int farSliderY = farLabelY + SectionLabelHeight + SectionLabelSpacing;
            int closeY = PanelHeight - PanelPadding - CloseButtonHeight;

            LayoutGridRow(gridRowY);
            LayoutSliderRow(NearPlaneSliderInternal, NearPlaneValueText, nearLabelY, nearSliderY);
            LayoutSliderRow(FarPlaneSliderInternal, FarPlaneValueText, farLabelY, farSliderY);
            LayoutCloseButton(closeY);
        }

        /// <summary>
        /// Positions the grid label and checkbox on the first row.
        /// </summary>
        /// <param name="gridRowY">Top coordinate of the grid row inside the overlay.</param>
        void LayoutGridRow(int gridRowY) {
            if (GridToggleLabelText == null || GridToggleCheckBoxHost == null || GridToggleCheckBox == null) {
                return;
            }

            float rowInsetY = (GridToggleRowHeight - EditorPlatformSettingsSection.CheckBoxSize.Y) / 2f;

            if (GridToggleLabelText.Parent != null) {
                GridToggleLabelText.Parent.Position = new float3(PanelPadding, gridRowY + rowInsetY, 0.1f);
            }

            int checkboxX = PanelWidth - PanelPadding - EditorPlatformSettingsSection.CheckBoxSize.X;
            GridToggleCheckBoxHost.Position = new float3(checkboxX, gridRowY + rowInsetY, 0.1f);
        }

        /// <summary>
        /// Positions one label, slider, and numeric value row.
        /// </summary>
        /// <param name="slider">Slider entity to position.</param>
        /// <param name="valueText">Numeric value text paired with the slider.</param>
        /// <param name="labelY">Top coordinate of the section label.</param>
        /// <param name="sliderY">Top coordinate of the slider row.</param>
        void LayoutSliderRow(EditorSlider slider, TextComponent valueText, int labelY, int sliderY) {
            if (slider == null || valueText == null) {
                return;
            }

            TextComponent labelText = null;
            if (ReferenceEquals(slider, NearPlaneSliderInternal)) {
                labelText = NearPlaneLabelText;
            } else if (ReferenceEquals(slider, FarPlaneSliderInternal)) {
                labelText = FarPlaneLabelText;
            }

            if (labelText != null && labelText.Parent != null) {
                labelText.Parent.Position = new float3(PanelPadding, labelY, 0.1f);
            }

            slider.Position = new float3(PanelPadding, sliderY, 0.1f);
            if (valueText.Parent != null) {
                int valueX = PanelPadding + SliderWidth + SliderValueSpacing;
                valueText.Parent.Position = new float3(valueX, sliderY + 1f, 0.1f);
            }
        }

        /// <summary>
        /// Positions the close button at the bottom of the overlay.
        /// </summary>
        /// <param name="closeY">Top coordinate of the close button.</param>
        void LayoutCloseButton(int closeY) {
            if (CloseButtonRoot == null || CloseButtonBackground == null || CloseButtonInteractable == null) {
                return;
            }

            int closeX = (PanelWidth - CloseButtonWidth) / 2;
            CloseButtonRoot.Position = new float3(closeX, closeY, 0.1f);
            CloseButtonBackground.Size = new int2(CloseButtonWidth, CloseButtonHeight);
            CloseButtonInteractable.Size = new int2(CloseButtonWidth, CloseButtonHeight);
            if (CloseButtonText != null && CloseButtonText.Parent != null) {
                CloseButtonText.Parent.Position = new float3(0f, 3f, 0.1f);
            }
        }

        /// <summary>
        /// Handles pointer interaction updates for the close button.
        /// </summary>
        /// <param name="interaction">Pointer interaction state.</param>
        void HandleCloseButtonCursor(PointerInteraction interaction) {
            if (interaction == PointerInteraction.Hover) {
                CloseButtonHoverState = true;
            } else if (interaction == PointerInteraction.Press) {
                CloseButtonHoverState = true;
                CloseButtonPressedState = true;
            } else if (interaction == PointerInteraction.Release) {
                bool shouldClose = CloseButtonPressedState && CloseButtonHoverState;
                CloseButtonPressedState = false;
                if (shouldClose) {
                    Close(SettingsButtonFocusTarget);
                }
            } else if (interaction == PointerInteraction.Leave) {
                CloseButtonHoverState = false;
                CloseButtonPressedState = false;
            } else if (interaction == PointerInteraction.None) {
                return;
            } else {
                throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateCloseButtonVisuals();
        }

        /// <summary>
        /// Preserves the grid-toggle focus target wiring without changing checkbox visuals.
        /// </summary>
        /// <param name="isFocused">True when keyboard focus currently targets the grid-toggle checkbox.</param>
        void HandleGridToggleFocusedChanged(bool isFocused) {
        }

        /// <summary>
        /// Applies a checked-state change from the grid checkbox to the viewport grid visibility.
        /// </summary>
        /// <param name="isChecked">True when the checkbox should show the grid.</param>
        void HandleGridToggleCheckedChanged(bool isChecked) {
            SetGridVisibleAction(isChecked);
        }

        /// <summary>
        /// Applies keyboard-focus styling to the near-plane slider target.
        /// </summary>
        /// <param name="isFocused">True when keyboard focus currently targets the near-plane slider.</param>
        void HandleNearPlaneFocusedChanged(bool isFocused) {
            NearPlaneSliderInternal.SetKeyboardFocused(isFocused);
        }

        /// <summary>
        /// Applies keyboard-focus styling to the far-plane slider target.
        /// </summary>
        /// <param name="isFocused">True when keyboard focus currently targets the far-plane slider.</param>
        void HandleFarPlaneFocusedChanged(bool isFocused) {
            FarPlaneSliderInternal.SetKeyboardFocused(isFocused);
        }

        /// <summary>
        /// Applies keyboard-focus styling to the close button target.
        /// </summary>
        /// <param name="isFocused">True when keyboard focus currently targets the close button.</param>
        void HandleCloseButtonFocusedChanged(bool isFocused) {
            CloseButtonKeyboardFocusState = isFocused;
            UpdateCloseButtonVisuals();
        }

        /// <summary>
        /// Exists so the overlay focus group can participate in activation changes without changing visuals.
        /// </summary>
        /// <param name="isActive">True when the overlay group becomes active.</param>
        void HandleOverlayGroupActiveChanged(bool isActive) {
        }

        /// <summary>
        /// Returns true when Enter or Space should activate a button-like overlay control.
        /// </summary>
        /// <param name="key">Key to evaluate.</param>
        /// <returns>True when the key should activate the control.</returns>
        bool CanActivateButtonWithKey(Keys key) {
            return key == Keys.Enter || key == Keys.Space;
        }

        /// <summary>
        /// Returns true when the supplied key should adjust a slider.
        /// </summary>
        /// <param name="key">Key to evaluate.</param>
        /// <returns>True when the key should adjust a slider.</returns>
        bool CanAdjustSliderWithKey(Keys key) {
            return key == Keys.Left || key == Keys.Right;
        }

        /// <summary>
        /// Toggles viewport grid visibility from keyboard activation of the checkbox row.
        /// </summary>
        /// <param name="key">Activation key routed by the focus service.</param>
        void ActivateGridToggleFromKey(Keys key) {
            if (!CanActivateButtonWithKey(key)) {
                return;
            }

            ToggleGridVisibility();
        }

        /// <summary>
        /// Applies one keyboard adjustment to the near-plane slider.
        /// </summary>
        /// <param name="key">Adjustment key routed by the focus service.</param>
        void ActivateNearPlaneFromKey(Keys key) {
            if (!CanAdjustSliderWithKey(key)) {
                return;
            }

            NearPlaneSliderInternal.AdjustFromKey(key);
        }

        /// <summary>
        /// Applies one keyboard adjustment to the far-plane slider.
        /// </summary>
        /// <param name="key">Adjustment key routed by the focus service.</param>
        void ActivateFarPlaneFromKey(Keys key) {
            if (!CanAdjustSliderWithKey(key)) {
                return;
            }

            FarPlaneSliderInternal.AdjustFromKey(key);
        }

        /// <summary>
        /// Closes the overlay from keyboard activation of the close button.
        /// </summary>
        /// <param name="key">Activation key routed by the focus service.</param>
        void ActivateCloseButtonFromKey(Keys key) {
            if (!CanActivateButtonWithKey(key)) {
                return;
            }

            Close(SettingsButtonFocusTarget);
        }

        /// <summary>
        /// Applies live camera updates from the near-plane slider while preserving valid clip-plane separation.
        /// </summary>
        /// <param name="value">New authored near-plane value from the slider.</param>
        void HandleNearPlaneSliderChanged(double value) {
            if (IsSynchronizingState) {
                return;
            }

            double maximumNear = Math.Max(
                CameraProjectionUtils.MinimumNearPlaneDistance,
                Camera.FarPlaneDistance - CameraProjectionUtils.MinimumPlaneSeparation);
            double clampedValue = Math.Clamp(value, CameraProjectionUtils.MinimumNearPlaneDistance, maximumNear);
            Camera.NearPlaneDistance = (float)clampedValue;
            SynchronizeFromCamera();
        }

        /// <summary>
        /// Applies live camera updates from the far-plane slider while preserving valid clip-plane separation.
        /// </summary>
        /// <param name="value">New authored far-plane value from the slider.</param>
        void HandleFarPlaneSliderChanged(double value) {
            if (IsSynchronizingState) {
                return;
            }

            double minimumFar = Math.Max(1.0, Camera.NearPlaneDistance + CameraProjectionUtils.MinimumPlaneSeparation);
            double clampedValue = Math.Clamp(value, minimumFar, 5000.0);
            Camera.FarPlaneDistance = (float)clampedValue;
            SynchronizeFromCamera();
        }

        /// <summary>
        /// Synchronizes overlay control state, slider positions, and numeric readouts from the camera state.
        /// </summary>
        void SynchronizeFromCamera() {
            IsSynchronizingState = true;
            try {
                NearPlaneSliderInternal.SetValue(Camera.NearPlaneDistance);
                FarPlaneSliderInternal.SetValue(Camera.FarPlaneDistance);
                UpdateGridToggleVisuals();
                UpdateClipPlaneValueTexts();
            } finally {
                IsSynchronizingState = false;
            }
        }

        /// <summary>
        /// Synchronizes the grid-toggle checkbox state from the current viewport grid visibility.
        /// </summary>
        void UpdateGridToggleVisuals() {
            if (GridToggleCheckBox == null) {
                return;
            }

            GridToggleCheckBox.IsChecked = IsGridVisibleResolver();
        }

        /// <summary>
        /// Updates the close button colors from current interaction state.
        /// </summary>
        void UpdateCloseButtonVisuals() {
            if (CloseButtonBackground == null || CloseButtonText == null) {
                return;
            }

            if (CloseButtonPressedState) {
                CloseButtonBackground.FillColor = ThemeManager.Colors.AccentTertiary;
                CloseButtonBackground.BorderColor = ThemeManager.Colors.AccentTertiary;
            } else if (CloseButtonKeyboardFocusState || CloseButtonHoverState) {
                CloseButtonBackground.FillColor = ThemeManager.Colors.AccentSecondary;
                CloseButtonBackground.BorderColor = ThemeManager.Colors.AccentSecondary;
            } else {
                CloseButtonBackground.FillColor = ThemeManager.Colors.SurfaceInput;
                CloseButtonBackground.BorderColor = ThemeManager.Colors.SurfacePrimary;
            }

            if (CloseButtonHoverState || CloseButtonPressedState || CloseButtonKeyboardFocusState) {
                CloseButtonText.Color = new byte4(255, 255, 255, 255);
            } else {
                CloseButtonText.Color = ThemeManager.Colors.InputForegroundPrimary;
            }
        }

        /// <summary>
        /// Updates the near and far numeric value readouts from the camera state.
        /// </summary>
        void UpdateClipPlaneValueTexts() {
            if (NearPlaneValueText != null) {
                NearPlaneValueText.Text = FormatDistance(Camera.NearPlaneDistance);
            }
            if (FarPlaneValueText != null) {
                FarPlaneValueText.Text = FormatDistance(Camera.FarPlaneDistance);
            }
        }

        /// <summary>
        /// Flips the grid checkbox state and forwards the new value to the viewport grid handler.
        /// </summary>
        void ToggleGridVisibility() {
            if (GridToggleCheckBox == null) {
                return;
            }

            bool isChecked = !IsGridVisibleResolver();
            SetGridVisibleAction(isChecked);
            GridToggleCheckBox.IsChecked = isChecked;
        }

        /// <summary>
        /// Returns true when the supplied point lies inside the overlay panel bounds.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the panel rectangle.</returns>
        bool ContainsOverlayPoint(int2 point) {
            if (OverlayRoot == null || OwnerViewport == null) {
                return false;
            }

            int2 overlayPosition = GetOverlayScreenPosition();
            return point.X >= overlayPosition.X &&
                   point.X < overlayPosition.X + PanelWidth &&
                   point.Y >= overlayPosition.Y &&
                   point.Y < overlayPosition.Y + PanelHeight;
        }

        /// <summary>
        /// Returns true when the supplied point lies inside the grid-toggle checkbox.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the grid-toggle checkbox.</returns>
        bool ContainsGridTogglePoint(int2 point) {
            return ContainsChildPoint(GridToggleCheckBoxHost, EditorPlatformSettingsSection.CheckBoxSize.X, EditorPlatformSettingsSection.CheckBoxSize.Y, point);
        }

        /// <summary>
        /// Returns true when the supplied point lies inside the near-plane slider.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the near-plane slider.</returns>
        bool ContainsNearPlaneSliderPoint(int2 point) {
            return ContainsChildPoint(NearPlaneSliderInternal, SliderWidth, SliderHeight, point);
        }

        /// <summary>
        /// Returns true when the supplied point lies inside the far-plane slider.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the far-plane slider.</returns>
        bool ContainsFarPlaneSliderPoint(int2 point) {
            return ContainsChildPoint(FarPlaneSliderInternal, SliderWidth, SliderHeight, point);
        }

        /// <summary>
        /// Returns true when the supplied point lies inside the close button.
        /// </summary>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the close button.</returns>
        bool ContainsCloseButtonPoint(int2 point) {
            return ContainsChildPoint(CloseButtonRoot, CloseButtonWidth, CloseButtonHeight, point);
        }

        /// <summary>
        /// Returns true when the supplied point lies inside one overlay child rectangle.
        /// </summary>
        /// <param name="child">Overlay child entity whose rectangle should be evaluated.</param>
        /// <param name="width">Rectangle width in pixels.</param>
        /// <param name="height">Rectangle height in pixels.</param>
        /// <param name="point">Screen point to evaluate.</param>
        /// <returns>True when the point lies inside the child rectangle.</returns>
        bool ContainsChildPoint(Entity child, int width, int height, int2 point) {
            if (child == null || OwnerViewport == null || OverlayRoot == null) {
                return false;
            }

            int left = (int)Math.Round(child.Position.X);
            int top = (int)Math.Round(child.Position.Y);
            return point.X >= left &&
                   point.X < left + width &&
                   point.Y >= top &&
                   point.Y < top + height;
        }

        /// <summary>
        /// Computes the current overlay screen position from the owning viewport and root entity.
        /// </summary>
        /// <returns>Screen-space top-left panel position.</returns>
        int2 GetOverlayScreenPosition() {
            if (OwnerViewport == null || OverlayRoot == null) {
                return int2.Zero;
            }

            return new int2(
                (int)Math.Round(OverlayRoot.Position.X),
                (int)Math.Round(OverlayRoot.Position.Y));
        }

        /// <summary>
        /// Resolves the local X position used to right-align the overlay beneath the settings button.
        /// </summary>
        /// <returns>Panel left position in viewport-local coordinates.</returns>
        float ResolvePanelLeft() {
            if (OwnerViewport == null) {
                return 0f;
            }

            double desiredLeft = AnchorX + AnchorWidth - PanelWidth;
            double maximumLeft = Math.Max(0.0, OwnerViewport.Size.X - PanelWidth);
            return (float)Math.Round(Math.Clamp(desiredLeft, 0.0, maximumLeft));
        }

        /// <summary>
        /// Creates one internal child root that inherits the overlay layer mask.
        /// </summary>
        /// <returns>Internal child entity ready to host components.</returns>
        EditorEntity CreateChildRoot() {
            return new EditorEntity {
                InternalEntity = true,
                LayerMask = OverlayLayerMask,
                Position = float3.Zero
            };
        }

        /// <summary>
        /// Formats one clip-plane distance using compact fractional precision rules.
        /// </summary>
        /// <param name="value">Distance to format.</param>
        /// <returns>Formatted distance string.</returns>
        string FormatDistance(double value) {
            if (value < 10.0) {
                return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value < 100.0) {
                return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }

            return value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Ensures the overlay has been attached to one viewport before stateful methods proceed.
        /// </summary>
        void EnsureInitialized() {
            if (!IsInitialized || OwnerViewport == null || OverlayRoot == null) {
                throw new InvalidOperationException("Viewport settings overlay must be attached to an EditorViewport before use.");
            }
        }

        /// <summary>
        /// Raises the overlay open-state event when listeners are present.
        /// </summary>
        void RaiseOpenStateChanged() {
            if (OpenStateChanged != null) {
                OpenStateChanged(IsOpen);
            }
        }
    }
}

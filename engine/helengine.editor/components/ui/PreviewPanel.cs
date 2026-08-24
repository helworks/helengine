using System.Text.Json;

namespace helengine.editor {
    /// <summary>
    /// Dockable panel that hosts the active preview source and renders it inside the preview area.
    /// </summary>
    public class PreviewPanel : DockableEntity {
        /// <summary>
        /// Shared JSON options used to deserialize persisted preview-panel state payloads written with camelCase names.
        /// </summary>
        static JsonSerializerOptions PreviewStateJsonSerializerOptions { get; } = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        static PreviewPanel() {
            PreviewStateJsonSerializerOptions.Converters.Add(new SceneAssetReferenceJsonConverter());
        }
        /// <summary>
        /// Padding applied around the preview image.
        /// </summary>
        const int ContentPadding = 0;
        /// <summary>
        /// Vertical spacing reserved between the preview image and the resolution caption.
        /// </summary>
        const int ResolutionLabelGap = 6;
        /// <summary>
        /// Minimum zoom multiplier allowed for texture previews.
        /// </summary>
        const double MinimumZoomScale = 0.25d;
        /// <summary>
        /// Maximum zoom multiplier allowed for texture previews.
        /// </summary>
        const double MaximumZoomScale = 16.0d;
        /// <summary>
        /// Multiplier applied for each scroll-wheel notch while zooming a texture preview.
        /// </summary>
        const double ZoomStepFactor = 1.1d;
        /// <summary>
        /// Height of the compact toolbar displayed for model previews.
        /// </summary>
        const int ModelToolbarHeight = 24;
        /// <summary>
        /// Horizontal padding around the model-grid button.
        /// </summary>
        const int ModelToolbarPadding = 6;
        /// <summary>
        /// Width of the model-grid button.
        /// </summary>
        const int ModelToolbarButtonWidth = 22;
        /// <summary>
        /// Height of the model-grid button.
        /// </summary>
        const int ModelToolbarButtonHeight = 18;
        /// <summary>
        /// Horizontal space separating adjacent model-preview toolbar buttons.
        /// </summary>
        const int ModelToolbarButtonGap = 4;
        /// <summary>
        /// Square size of the model-grid button icon.
        /// </summary>
        const int ModelToolbarIconSize = 14;

        /// <summary>
        /// Render order used for the preview sprite.
        /// </summary>
        readonly byte spriteOrder;
        /// <summary>
        /// Runtime texture used by the model-grid toolbar button.
        /// </summary>
        readonly RuntimeTexture modelGridIcon;
        /// <summary>
        /// Root entity hosting preview content.
        /// </summary>
        readonly EditorEntity contentRoot;
        /// <summary>
        /// Root entity hosting controls specific to model previews.
        /// </summary>
        readonly EditorEntity modelToolbarRoot;
        /// <summary>
        /// Background sprite for the model-preview toolbar.
        /// </summary>
        readonly SpriteComponent modelToolbarBackground;
        /// <summary>
        /// Root entity of the model-preview grid toggle button.
        /// </summary>
        readonly EditorEntity gridButtonRoot;
        /// <summary>
        /// Background sprite for the model-preview grid toggle button.
        /// </summary>
        readonly SpriteComponent gridButtonBackground;
        /// <summary>
        /// Icon sprite for the model-preview grid toggle button.
        /// </summary>
        readonly SpriteComponent gridButtonIcon;
        /// <summary>
        /// Hit-testable region used to activate the model-preview grid button.
        /// </summary>
        readonly InteractableComponent gridButtonInteractable;
        /// <summary>
        /// Keyboard focus target assigned to the model-preview grid button.
        /// </summary>
        readonly EditorFocusTarget gridButtonFocusTarget;
        /// <summary>
        /// Root entity of the model-preview bounds display cycle button.
        /// </summary>
        readonly EditorEntity boundsButtonRoot;
        /// <summary>
        /// Background sprite for the model-preview bounds display cycle button.
        /// </summary>
        readonly SpriteComponent boundsButtonBackground;
        /// <summary>
        /// Entity that positions the model-preview bounds button glyph.
        /// </summary>
        readonly EditorEntity boundsButtonLabelHost;
        /// <summary>
        /// Text glyph that identifies the model-preview bounds display cycle button.
        /// </summary>
        readonly TextComponent boundsButtonText;
        /// <summary>
        /// Hit-testable region used to activate the model-preview bounds display cycle button.
        /// </summary>
        readonly InteractableComponent boundsButtonInteractable;
        /// <summary>
        /// Keyboard focus target assigned to the model-preview bounds display cycle button.
        /// </summary>
        readonly EditorFocusTarget boundsButtonFocusTarget;
        /// <summary>
        /// Entity that positions the preview sprite.
        /// </summary>
        readonly EditorEntity textureHost;
        /// <summary>
        /// Sprite used to draw the preview texture.
        /// </summary>
        readonly SpriteComponent textureSprite;
        /// <summary>
        /// Entity that positions the resolution caption beneath texture previews.
        /// </summary>
        readonly EditorEntity resolutionLabelHost;
        /// <summary>
        /// Text component that renders the texture resolution caption.
        /// </summary>
        readonly TextComponent resolutionLabelText;
        /// <summary>
        /// Currently active preview source.
        /// </summary>
        IPreviewSource ActivePreviewSourceValue;
        /// <summary>
        /// Current binding kind owned by the preview panel.
        /// </summary>
        PreviewPanelBindingKind BindingKindValue;
        /// <summary>
        /// Relative asset path used when the preview panel is bound to one asset.
        /// </summary>
        string BoundAssetRelativePath = string.Empty;
        /// <summary>Canonical reference for the currently bound authored asset.</summary>
        SceneAssetReference BoundAssetReferenceValue;
        /// <summary>
        /// Stable scene entity id used when the preview panel is bound to one camera.
        /// </summary>
        uint BoundSceneEntityId;
        /// <summary>
        /// True when the preview panel should ignore later latest-click updates.
        /// </summary>
        bool IsLockedValue;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;
        /// <summary>
        /// Current zoom factor applied to texture previews, relative to the fitted size.
        /// </summary>
        double TextureZoomScale;
        /// <summary>
        /// Additional translation applied to texture previews after cursor-centered zooming.
        /// </summary>
        float2 TexturePanOffset;
        /// <summary>
        /// Tracks whether a middle-mouse drag is currently active on the texture preview.
        /// </summary>
        bool IsMiddleMouseDragging;
        /// <summary>
        /// Tracks whether a left-mouse drag is currently active on an interactive non-texture preview.
        /// </summary>
        bool IsLeftMouseDragging;
        /// <summary>
        /// Tracks the grid-button hover state used by toolbar visuals.
        /// </summary>
        bool IsGridButtonHovered;
        /// <summary>
        /// Tracks the grid-button press state until the pointer is released.
        /// </summary>
        bool IsGridButtonPressed;
        /// <summary>
        /// Tracks whether keyboard navigation currently targets the grid button.
        /// </summary>
        bool IsGridButtonKeyboardFocused;
        /// <summary>
        /// Tracks the bounds-button hover state used by toolbar visuals.
        /// </summary>
        bool IsBoundsButtonHovered;
        /// <summary>
        /// Tracks the bounds-button press state until the pointer is released.
        /// </summary>
        bool IsBoundsButtonPressed;
        /// <summary>
        /// Tracks whether keyboard navigation currently targets the bounds button.
        /// </summary>
        bool IsBoundsButtonKeyboardFocused;
        /// <summary>
        /// Stores the grid visibility preference owned by this preview-panel instance.
        /// </summary>
        bool IsModelGridVisibleValue;
        /// <summary>
        /// Stores the bounds display preference owned by this preview-panel instance.
        /// </summary>
        ModelPreviewBoundsDisplayMode ModelBoundsDisplayModeValue;

        /// <summary>
        /// Initializes a new preview panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        public PreviewPanel(FontAsset font) : this(font, TextureUtils.PixelTexture, EditorUiMetrics.Default) {
        }

        /// <summary>
        /// Initializes a new preview panel with the provided font and shared metrics source.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome and padding.</param>
        public PreviewPanel(FontAsset font, EditorUiMetrics metrics) : this(font, TextureUtils.PixelTexture, metrics) {
        }

        /// <summary>
        /// Initializes a new preview panel with a model-grid toolbar icon and shared metrics source.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="gridIcon">Icon drawn by the model-preview grid toggle button.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock chrome and padding.</param>
        public PreviewPanel(FontAsset font, RuntimeTexture gridIcon, EditorUiMetrics metrics) : base(font, metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (gridIcon == null) {
                throw new ArgumentNullException(nameof(gridIcon));
            }

            Title = "Preview";
            MinSize = new int2(metrics.ScalePixels(220), metrics.ScalePixels(160));

            spriteOrder = RenderOrder2D.PanelForeground;
            modelGridIcon = gridIcon;
            IsModelGridVisibleValue = true;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);
            AddChild(contentRoot);

            modelToolbarRoot = new EditorEntity {
                LayerMask = LayerMask,
                Enabled = false,
                Position = new float3(0f, 0f, 0.4f)
            };
            contentRoot.AddChild(modelToolbarRoot);

            modelToolbarBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = RenderOrder2D.PanelSurface
            };
            modelToolbarRoot.AddComponent(modelToolbarBackground);

            gridButtonRoot = new EditorEntity {
                LayerMask = LayerMask
            };
            modelToolbarRoot.AddChild(gridButtonRoot);

            gridButtonBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                RenderOrder2D = RenderOrder2D.PanelSurface
            };
            gridButtonRoot.AddComponent(gridButtonBackground);

            EditorEntity gridIconHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            gridButtonRoot.AddChild(gridIconHost);

            gridButtonIcon = new SpriteComponent {
                Texture = modelGridIcon,
                Color = new byte4(255, 255, 255, 224),
                Size = new int2(ModelToolbarIconSize, ModelToolbarIconSize),
                RenderOrder2D = spriteOrder
            };
            gridIconHost.AddComponent(gridButtonIcon);

            gridButtonInteractable = new InteractableComponent {
                Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight)
            };
            gridButtonInteractable.CursorEvent += HandleGridButtonCursor;
            gridButtonRoot.AddComponent(gridButtonInteractable);
            gridButtonFocusTarget = new EditorFocusTarget(
                this,
                0,
                false,
                () => Enabled && modelToolbarRoot.Enabled,
                ContainsGridButtonPoint,
                isFocused => {
                    IsGridButtonKeyboardFocused = isFocused;
                    UpdateGridButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => ToggleModelGrid());
            EditorKeyboardFocusService.RegisterTarget(gridButtonFocusTarget);

            boundsButtonRoot = new EditorEntity {
                LayerMask = LayerMask
            };
            modelToolbarRoot.AddChild(boundsButtonRoot);

            boundsButtonBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                RenderOrder2D = RenderOrder2D.PanelSurface
            };
            boundsButtonRoot.AddComponent(boundsButtonBackground);

            boundsButtonLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = new float3(0f, 0f, 0.1f)
            };
            boundsButtonRoot.AddChild(boundsButtonLabelHost);

            boundsButtonText = new TextComponent {
                Font = TitleFont,
                Text = "B",
                Color = new byte4(255, 255, 255, 224),
                RenderOrder2D = spriteOrder
            };
            boundsButtonLabelHost.AddComponent(boundsButtonText);

            boundsButtonInteractable = new InteractableComponent {
                Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight)
            };
            boundsButtonInteractable.CursorEvent += HandleBoundsButtonCursor;
            boundsButtonRoot.AddComponent(boundsButtonInteractable);
            boundsButtonFocusTarget = new EditorFocusTarget(
                this,
                1,
                false,
                () => Enabled && modelToolbarRoot.Enabled,
                ContainsBoundsButtonPoint,
                isFocused => {
                    IsBoundsButtonKeyboardFocused = isFocused;
                    UpdateBoundsButtonVisuals();
                },
                key => key == Keys.Enter || key == Keys.Space,
                key => CycleModelBoundsDisplayMode());
            EditorKeyboardFocusService.RegisterTarget(boundsButtonFocusTarget);

            textureHost = new EditorEntity();
            textureHost.LayerMask = LayerMask;
            textureHost.Position = new float3(GetContentPaddingPixels(), GetContentPaddingPixels(), 0.2f);
            contentRoot.AddChild(textureHost);

            textureSprite = new SpriteComponent();
            textureSprite.RenderOrder2D = spriteOrder;
            textureSprite.Color = new byte4(255, 255, 255, 255);
            textureSprite.Size = new int2(1, 1);
            textureHost.AddComponent(textureSprite);

            resolutionLabelHost = new EditorEntity();
            resolutionLabelHost.LayerMask = LayerMask;
            resolutionLabelHost.Enabled = false;
            contentRoot.AddChild(resolutionLabelHost);

            resolutionLabelText = new TextComponent();
            resolutionLabelText.Font = TitleFont;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            resolutionLabelText.RenderOrder2D = spriteOrder;
            resolutionLabelHost.AddComponent(resolutionLabelText);

            ClearPreview();
            LayoutModelToolbar();
            UpdateGridButtonVisuals();
            UpdateBoundsButtonVisuals();
            AddComponent(new PreviewPanelUpdater(this));
            isInitialized = true;
            InitializeHierarchy();
        }

        /// <summary>
        /// Gets the current preview source, when one is bound.
        /// </summary>
        public IPreviewSource ActivePreviewSource => ActivePreviewSourceValue;
        /// <summary>
        /// Gets whether the preview panel is currently locked to its bound target.
        /// </summary>
        public bool IsLocked => IsLockedValue;

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            base.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Displays one texture asset through a texture preview source.
        /// </summary>
        /// <param name="asset">Texture asset to preview.</param>
        public void ShowTexture(TextureAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(asset);
            SetPreviewSource(new TexturePreviewSource(runtimeTexture));
        }

        /// <summary>
        /// Binds one active preview source to the panel.
        /// </summary>
        /// <param name="previewSource">Preview source to bind, or null to clear the panel.</param>
        public void SetPreviewSource(IPreviewSource previewSource) {
            if (ReferenceEquals(ActivePreviewSourceValue, previewSource)) {
                return;
            }

            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Dispose();
            }

            ActivePreviewSourceValue = previewSource;
            if (ActivePreviewSourceValue == null) {
                UpdateModelToolbarVisibility();
                ClearPreviewVisuals();
                return;
            }

            ResetTexturePreviewLayout();
            UpdateModelToolbarVisibility();
            ActivePreviewSourceValue.Resize(GetPreviewContentSize());
            textureSprite.Texture = ActivePreviewSourceValue.Texture;
            LayoutPreview();
        }

        /// <summary>
        /// Captures one serializable preview-panel state payload.
        /// </summary>
        /// <returns>Serializable preview-panel state payload.</returns>
        public PreviewPanelStateDocument CaptureState() {
            return new PreviewPanelStateDocument {
                IsLocked = IsLockedValue,
                BindingKind = BindingKindValue,
                AssetReference = BoundAssetReferenceValue,
                AssetRelativePath = BoundAssetRelativePath,
                SceneEntityId = BoundSceneEntityId
            };
        }

        /// <summary>
        /// Restores one previously captured preview-panel state payload.
        /// </summary>
        /// <param name="state">Preview-panel state payload to restore.</param>
        public void RestoreState(PreviewPanelStateDocument state) {
            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }

            IsLockedValue = state.IsLocked;
            BindingKindValue = state.BindingKind;
            BoundAssetReferenceValue = state.AssetReference;
            BoundAssetRelativePath = BoundAssetReferenceValue != null
                ? BoundAssetReferenceValue.RelativePath ?? string.Empty
                : state.AssetRelativePath ?? string.Empty;
            BoundSceneEntityId = state.SceneEntityId;
        }

        /// <summary>
        /// Restores one previously captured preview-panel state payload from the workspace persistence pipeline.
        /// </summary>
        /// <param name="state">Serialized state payload to reapply.</param>
        public void RestoreState(object state) {
            if (state == null) {
                RestoreState(new PreviewPanelStateDocument());
                return;
            }
            if (state is PreviewPanelStateDocument document) {
                RestoreState(document);
                return;
            }
            if (state is JsonElement jsonElement) {
                PreviewPanelStateDocument deserialized = jsonElement.Deserialize<PreviewPanelStateDocument>(PreviewStateJsonSerializerOptions);
                if (deserialized == null) {
                    throw new InvalidOperationException("Preview panel state could not be deserialized.");
                }

                RestoreState(deserialized);
                return;
            }

            throw new InvalidOperationException("Preview panel state payload has an unsupported type.");
        }

        /// <summary>
        /// Toggles whether the preview panel should ignore later latest-click updates.
        /// </summary>
        public void ToggleLock() {
            IsLockedValue = !IsLockedValue;
        }

        /// <summary>
        /// Toggles the persistent floor-grid preference for model previews shown by this panel.
        /// </summary>
        public void ToggleModelGrid() {
            IsModelGridVisibleValue = !IsModelGridVisibleValue;
            if (ActivePreviewSourceValue is ModelPreviewSource modelPreviewSource) {
                modelPreviewSource.SetGridVisible(IsModelGridVisibleValue);
            }

            UpdateGridButtonVisuals();
        }

        /// <summary>
        /// Advances the persistent model-preview bounds display through box, sphere, and no overlay.
        /// </summary>
        public void CycleModelBoundsDisplayMode() {
            if (ModelBoundsDisplayModeValue == ModelPreviewBoundsDisplayMode.None) {
                ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.Box;
            } else if (ModelBoundsDisplayModeValue == ModelPreviewBoundsDisplayMode.Box) {
                ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.Sphere;
            } else if (ModelBoundsDisplayModeValue == ModelPreviewBoundsDisplayMode.Sphere) {
                ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.None;
            } else {
                throw new InvalidOperationException("Model preview bounds display mode is not supported.");
            }

            if (ActivePreviewSourceValue is ModelPreviewSource modelPreviewSource) {
                modelPreviewSource.SetBoundsDisplayMode(ModelBoundsDisplayModeValue);
            }

            UpdateBoundsButtonVisuals();
        }

        /// <summary>
        /// Applies the latest asset selection when the preview panel is unlocked.
        /// </summary>
        /// <param name="assetEntry">Latest asset selection.</param>
        /// <param name="previewSourceResolver">Resolver used to build the next preview source.</param>
        /// <returns>True when the latest asset selection was adopted; otherwise false.</returns>
        public bool ApplyLatestAssetSelection(AssetBrowserEntry assetEntry, PreviewSourceResolver previewSourceResolver) {
            if (IsLockedValue) {
                return false;
            }
            if (assetEntry == null) {
                ApplyLatestSelectionCleared();
                return false;
            }
            if (previewSourceResolver == null) {
                throw new ArgumentNullException(nameof(previewSourceResolver));
            }

            if (!previewSourceResolver.TryResolveAssetPreview(assetEntry, out IPreviewSource previewSource)) {
                ApplyLatestSelectionCleared();
                return false;
            }

            SetPreviewSource(previewSource);
            BindingKindValue = PreviewPanelBindingKind.Asset;
            BoundAssetRelativePath = assetEntry.RelativePath ?? string.Empty;
            BoundAssetReferenceValue = CreateAuthoredReference(assetEntry);
            BoundSceneEntityId = 0u;
            return true;
        }

        /// <summary>
        /// Applies the latest camera selection when the preview panel is unlocked.
        /// </summary>
        /// <param name="selectedEntity">Latest scene selection.</param>
        /// <param name="previewSourceResolver">Resolver used to build the next preview source.</param>
        /// <returns>True when the latest camera selection was adopted; otherwise false.</returns>
        public bool ApplyLatestCameraSelection(Entity selectedEntity, PreviewSourceResolver previewSourceResolver) {
            if (IsLockedValue) {
                return false;
            }
            if (selectedEntity == null) {
                ApplyLatestSelectionCleared();
                return false;
            }
            if (previewSourceResolver == null) {
                throw new ArgumentNullException(nameof(previewSourceResolver));
            }

            if (!previewSourceResolver.TryResolveCameraPreview(selectedEntity, out IPreviewSource previewSource)) {
                ApplyLatestSelectionCleared();
                return false;
            }

            SetPreviewSource(previewSource);
            BindingKindValue = PreviewPanelBindingKind.Camera;
            BoundAssetRelativePath = string.Empty;
            BoundSceneEntityId = ResolveSceneEntityId(selectedEntity);
            return true;
        }

        /// <summary>
        /// Rebuilds the locked asset preview source from persisted state when the asset still resolves.
        /// </summary>
        /// <param name="assetEntry">Resolved asset entry that matches the stored relative path.</param>
        /// <param name="previewSourceResolver">Resolver used to rebuild the preview source.</param>
        /// <returns>True when the locked asset preview was restored; otherwise false.</returns>
        public bool RestoreLockedAssetSelection(AssetBrowserEntry assetEntry, PreviewSourceResolver previewSourceResolver) {
            if (!IsLockedValue) {
                return false;
            }
            if (BindingKindValue != PreviewPanelBindingKind.Asset) {
                return false;
            }
            if (assetEntry == null) {
                return false;
            }
            if (previewSourceResolver == null) {
                throw new ArgumentNullException(nameof(previewSourceResolver));
            }

            if (!previewSourceResolver.TryResolveAssetPreview(assetEntry, out IPreviewSource previewSource)) {
                return false;
            }

            SetPreviewSource(previewSource);
            BoundAssetRelativePath = assetEntry.RelativePath ?? string.Empty;
            BoundAssetReferenceValue = CreateAuthoredReference(assetEntry);
            BoundSceneEntityId = 0u;
            return true;
        }

        /// <summary>
        /// Rebuilds the locked camera preview source from persisted state when the camera entity still resolves.
        /// </summary>
        /// <param name="selectedEntity">Resolved scene entity that still owns a previewable camera.</param>
        /// <param name="previewSourceResolver">Resolver used to rebuild the preview source.</param>
        /// <returns>True when the locked camera preview was restored; otherwise false.</returns>
        public bool RestoreLockedCameraSelection(Entity selectedEntity, PreviewSourceResolver previewSourceResolver) {
            if (!IsLockedValue) {
                return false;
            }
            if (BindingKindValue != PreviewPanelBindingKind.Camera) {
                return false;
            }
            if (selectedEntity == null) {
                return false;
            }
            if (previewSourceResolver == null) {
                throw new ArgumentNullException(nameof(previewSourceResolver));
            }

            if (!previewSourceResolver.TryResolveCameraPreview(selectedEntity, out IPreviewSource previewSource)) {
                return false;
            }

            SetPreviewSource(previewSource);
            BoundAssetRelativePath = string.Empty;
            BoundSceneEntityId = ResolveSceneEntityId(selectedEntity);
            return true;
        }

        /// <summary>
        /// Clears the current locked target when the persisted asset or camera can no longer be resolved.
        /// </summary>
        public void ClearLockedTarget() {
            if (!IsLockedValue) {
                return;
            }

            ClearPreview();
            BindingKindValue = PreviewPanelBindingKind.None;
            BoundAssetRelativePath = string.Empty;
            BoundSceneEntityId = 0u;
        }

        /// <summary>
        /// Clears the unlocked preview panel when no latest previewable target remains.
        /// </summary>
        public void ApplyLatestSelectionCleared() {
            if (IsLockedValue) {
                return;
            }

            ClearPreview();
            BindingKindValue = PreviewPanelBindingKind.None;
            BoundAssetRelativePath = string.Empty;
            BoundSceneEntityId = 0u;
        }

        /// <summary>
        /// Clears the current preview.
        /// </summary>
        public void ClearPreview() {
            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Dispose();
            }

            ActivePreviewSourceValue = null;
            ResetTexturePreviewLayout();
            UpdateModelToolbarVisibility();
            ClearPreviewVisuals();
        }

        /// <summary>
        /// Updates the active preview source for the current frame.
        /// </summary>
        internal void UpdatePreviewSource() {
            if (ActivePreviewSourceValue == null) {
                return;
            }

            if (IsTexturePreviewSource()) {
                HandlePreviewWheelInput();
                HandlePreviewPanInput();
            } else if (ActivePreviewSourceValue is IPreviewInteractionSource interactionSource) {
                HandlePreviewInteractionInput(interactionSource);
            }

            ActivePreviewSourceValue.Update();
            textureSprite.Texture = ActivePreviewSourceValue.Texture;
            LayoutPreview();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            if (ActivePreviewSourceValue != null) {
                ActivePreviewSourceValue.Resize(GetPreviewContentSize());
            }

            LayoutModelToolbar();
            LayoutPreview();
        }

        /// <summary>
        /// Updates scaled preview content offsets after the shared dock chrome metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));
            contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
            textureHost.Position = new float3(GetContentPaddingPixels(), GetContentPaddingPixels(), 0.2f);
            resolutionLabelText.Font = TitleFont;
            boundsButtonText.Font = TitleFont;
            LayoutModelToolbar();
            LayoutPreview();
        }

        /// <summary>
        /// Lays out the preview sprite within the panel.
        /// </summary>
        void LayoutPreview() {
            if (ActivePreviewSourceValue == null || ActivePreviewSourceValue.Texture == null) {
                textureHost.Enabled = false;
                resolutionLabelHost.Enabled = false;
                return;
            }

            RuntimeTexture texture = ActivePreviewSourceValue.Texture;
            if (IsTexturePreviewSource()) {
                LayoutTexturePreview(texture);
                return;
            }

            LayoutGenericPreview(texture);
        }

        /// <summary>
        /// Lays out a texture preview using the current zoom factor and caption state.
        /// </summary>
        /// <param name="texture">Texture currently exposed by the active preview source.</param>
        void LayoutTexturePreview(RuntimeTexture texture) {
            textureHost.Enabled = true;
            resolutionLabelHost.Enabled = true;

            int2 contentSize = GetContentSize();
            string labelText = BuildResolutionLabelText(texture);
            int2 labelSize = GetResolutionLabelSize(labelText);
            resolutionLabelText.Text = labelText;
            resolutionLabelText.Size = labelSize;

            int2 textureViewportSize = GetTextureViewportSize(contentSize, labelSize);
            int2 targetSize = GetTextureDisplaySize(texture, textureViewportSize, TextureZoomScale);
            float2 centeredPosition = GetCenteredTexturePosition(textureViewportSize, targetSize);

            textureHost.Position = new float3(
                centeredPosition.X + TexturePanOffset.X,
                centeredPosition.Y + TexturePanOffset.Y,
                0.2f);
            textureSprite.Size = targetSize;
            resolutionLabelHost.Position = new float3(
                GetContentPaddingPixels() + Math.Max(0, (textureViewportSize.X - labelSize.X) / 2),
                GetContentPaddingPixels() + textureViewportSize.Y + ResolutionLabelGap,
                0.2f);
        }

        /// <summary>
        /// Lays out a non-texture preview without the resolution caption or zoom offset.
        /// </summary>
        /// <param name="texture">Texture currently exposed by the active preview source.</param>
        void LayoutGenericPreview(RuntimeTexture texture) {
            textureHost.Enabled = true;
            resolutionLabelHost.Enabled = false;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Size = new int2(1, 1);

            int2 contentSize = GetPreviewContentSize();
            int toolbarOffset = IsModelPreviewSource() ? ModelToolbarHeight : 0;
            textureHost.Position = new float3(GetContentPaddingPixels(), GetContentPaddingPixels() + toolbarOffset, 0.2f);
            textureSprite.Size = contentSize;
        }

        /// <summary>
        /// Clears the displayed texture and disables the preview host.
        /// </summary>
        void ClearPreviewVisuals() {
            textureSprite.Texture = null;
            textureSprite.Size = new int2(1, 1);
            textureHost.Enabled = false;
            resolutionLabelHost.Enabled = false;
            resolutionLabelText.Text = string.Empty;
            resolutionLabelText.Size = new int2(1, 1);
        }

        /// <summary>
        /// Resets the interaction state used by the currently active preview source.
        /// </summary>
        void ResetTexturePreviewLayout() {
            TextureZoomScale = 1d;
            TexturePanOffset = new float2(0f, 0f);
            IsMiddleMouseDragging = false;
            IsLeftMouseDragging = false;
        }

        /// <summary>
        /// Handles wheel zoom input for the active texture preview.
        /// </summary>
        void HandlePreviewWheelInput() {
            if (!IsTexturePreviewSource() || ActivePreviewSourceValue.Texture == null) {
                return;
            }

            InputSystem input = Core.Instance.Input;
            int wheelDelta = input.GetMouseScrollWheelDelta();
            if (wheelDelta == 0) {
                return;
            }

            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                return;
            }

            if (!IsPointerInsideContent(pointer)) {
                return;
            }

            int2 contentSize = GetContentSize();
            string labelText = BuildResolutionLabelText(ActivePreviewSourceValue.Texture);
            int2 labelSize = GetResolutionLabelSize(labelText);
            int2 textureViewportSize = GetTextureViewportSize(contentSize, labelSize);
            int2 currentSize = textureSprite.Size;
            if (currentSize.X <= 0 || currentSize.Y <= 0) {
                return;
            }

            double zoomNotches = wheelDelta / 120.0d;
            double nextZoomScale = TextureZoomScale * Math.Pow(ZoomStepFactor, zoomNotches);
            nextZoomScale = Math.Max(MinimumZoomScale, Math.Min(MaximumZoomScale, nextZoomScale));
            if (Math.Abs(nextZoomScale - TextureZoomScale) < 0.000001d) {
                return;
            }

            float3 contentOrigin = contentRoot.Position;
            float pointerLocalX = pointer.X - contentOrigin.X;
            float pointerLocalY = pointer.Y - contentOrigin.Y;
            float currentLeft = textureHost.LocalPosition.X;
            float currentTop = textureHost.LocalPosition.Y;
            double anchorX = (pointerLocalX - currentLeft) / (double)currentSize.X;
            double anchorY = (pointerLocalY - currentTop) / (double)currentSize.Y;

            TextureZoomScale = nextZoomScale;

            int2 nextSize = GetTextureDisplaySize(ActivePreviewSourceValue.Texture, textureViewportSize, TextureZoomScale);
            float2 centeredPosition = GetCenteredTexturePosition(textureViewportSize, nextSize);
            float desiredLeft = pointerLocalX - (float)(anchorX * nextSize.X);
            float desiredTop = pointerLocalY - (float)(anchorY * nextSize.Y);
            TexturePanOffset = new float2(desiredLeft - centeredPosition.X, desiredTop - centeredPosition.Y);
        }

        /// <summary>
        /// Handles middle-mouse drag input for texture previews.
        /// </summary>
        void HandlePreviewPanInput() {
            if (!IsTexturePreviewSource() || ActivePreviewSourceValue.Texture == null) {
                IsMiddleMouseDragging = false;
                return;
            }

            InputSystem input = Core.Instance.Input;
            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                IsMiddleMouseDragging = false;
                return;
            }

            if (input.WasMouseMiddleButtonPressed()) {
                IsMiddleMouseDragging = IsPointerInsideContent(pointer);
            }

            if (!IsMiddleMouseDragging) {
                if (input.GetMouseMiddleButtonState() == ButtonState.Released) {
                    IsMiddleMouseDragging = false;
                }

                return;
            }

            if (input.GetMouseMiddleButtonState() == ButtonState.Released) {
                IsMiddleMouseDragging = false;
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            TexturePanOffset = new float2(
                TexturePanOffset.X + delta.X,
                TexturePanOffset.Y + delta.Y);
        }

        /// <summary>
        /// Handles wheel and left-drag input for interactive preview sources.
        /// </summary>
        /// <param name="interactionSource">Active preview source that accepts pointer interaction.</param>
        void HandlePreviewInteractionInput(IPreviewInteractionSource interactionSource) {
            if (interactionSource == null) {
                throw new ArgumentNullException(nameof(interactionSource));
            }

            InputSystem input = Core.Instance.Input;
            int2 pointer = input.GetMousePosition();
            if (EditorInputCaptureService.IsPointerBlocked(pointer, owner => !ReferenceEquals(owner, this))) {
                IsLeftMouseDragging = false;
                return;
            }

            if (IsPointerInsideModelToolbar(pointer)) {
                IsLeftMouseDragging = false;
                IsMiddleMouseDragging = false;
                return;
            }

            if (!IsPointerInsideContent(pointer)) {
                IsLeftMouseDragging = false;
                return;
            }

            int wheelDelta = input.GetMouseScrollWheelDelta();
            if (wheelDelta != 0) {
                interactionSource.HandleMouseWheel(wheelDelta);
            }

            if (input.WasMouseLeftButtonPressed()) {
                IsLeftMouseDragging = true;
            }

            if (input.WasMouseMiddleButtonPressed()) {
                IsMiddleMouseDragging = true;
            }

            if (!IsLeftMouseDragging) {
                if (input.GetMouseLeftButtonState() == ButtonState.Released) {
                    IsLeftMouseDragging = false;
                }
            }

            if (input.GetMouseMiddleButtonState() == ButtonState.Released) {
                IsMiddleMouseDragging = false;
            }

            if (!IsLeftMouseDragging && !IsMiddleMouseDragging) {
                return;
            }

            if (IsLeftMouseDragging && input.GetMouseLeftButtonState() == ButtonState.Released) {
                IsLeftMouseDragging = false;
            }

            if (IsMiddleMouseDragging && input.GetMouseMiddleButtonState() == ButtonState.Released) {
                IsMiddleMouseDragging = false;
            }

            if (!IsLeftMouseDragging && !IsMiddleMouseDragging) {
                return;
            }

            int2 delta = input.GetMouseDelta();
            if (delta.X == 0 && delta.Y == 0) {
                return;
            }

            if (IsLeftMouseDragging) {
            if (input.GetMouseMiddleButtonState() == ButtonState.Pressed && input.GetMouseLeftButtonState() != ButtonState.Pressed) {
                interactionSource.HandleMouseMiddleDrag(delta);
            } else {
                interactionSource.HandleMouseDrag(delta);
            }
        }

            if (IsMiddleMouseDragging) {
                interactionSource.HandleMouseMiddleDrag(delta);
            }
        }

        /// <summary>
        /// Returns true when the active preview source exposes a texture preview.
        /// </summary>
        /// <returns>True when the panel is currently showing a texture preview.</returns>
        bool IsTexturePreviewSource() {
            return ActivePreviewSourceValue is TexturePreviewSource;
        }

        /// <summary>
        /// Returns true when the active source is a model preview that should show model-specific controls.
        /// </summary>
        /// <returns>True when model-preview controls apply to the active source.</returns>
        bool IsModelPreviewSource() {
            return ActivePreviewSourceValue is ModelPreviewSource;
        }

        /// <summary>
        /// Synchronizes toolbar visibility and the panel-owned grid preference with the active source.
        /// </summary>
        void UpdateModelToolbarVisibility() {
            if (ActivePreviewSourceValue is ModelPreviewSource modelPreviewSource) {
                modelToolbarRoot.Enabled = true;
                modelPreviewSource.SetGridVisible(IsModelGridVisibleValue);
                modelPreviewSource.ConfigureBoundsDimensionLabels(TitleFont);
                modelPreviewSource.SetBoundsDisplayMode(ModelBoundsDisplayModeValue);
            } else {
                modelToolbarRoot.Enabled = false;
            }

            LayoutModelToolbar();
            UpdateGridButtonVisuals();
            UpdateBoundsButtonVisuals();
        }

        /// <summary>
        /// Lays out the model-preview toolbar and its grid and bounds controls.
        /// </summary>
        void LayoutModelToolbar() {
            int toolbarWidth = ModelToolbarPadding * 2 + ModelToolbarButtonWidth * 2 + ModelToolbarButtonGap;
            modelToolbarRoot.Position = new float3(0f, 0f, 0.4f);
            modelToolbarBackground.Size = new int2(toolbarWidth, ModelToolbarHeight);

            float buttonY = (float)Math.Round((ModelToolbarHeight - ModelToolbarButtonHeight) * 0.5d);
            gridButtonRoot.Position = new float3(ModelToolbarPadding, buttonY, 0.1f);
            gridButtonBackground.Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight);
            gridButtonInteractable.Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight);

            if (gridButtonIcon.Parent != null) {
                float iconX = (float)Math.Round((ModelToolbarButtonWidth - ModelToolbarIconSize) * 0.5d);
                float iconY = (float)Math.Round((ModelToolbarButtonHeight - ModelToolbarIconSize) * 0.5d);
                gridButtonIcon.Parent.Position = new float3(iconX, iconY, 0.1f);
            }

            gridButtonIcon.Size = new int2(ModelToolbarIconSize, ModelToolbarIconSize);

            boundsButtonRoot.Position = new float3(ModelToolbarPadding + ModelToolbarButtonWidth + ModelToolbarButtonGap, buttonY, 0.1f);
            boundsButtonBackground.Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight);
            boundsButtonInteractable.Size = new int2(ModelToolbarButtonWidth, ModelToolbarButtonHeight);
            float2 boundsGlyphSize = TitleFont.MeasureString(boundsButtonText.Text);
            int boundsGlyphWidth = Math.Max(1, (int)Math.Ceiling(boundsGlyphSize.X));
            int boundsGlyphHeight = Math.Max(1, (int)Math.Ceiling(TitleFont.LineHeight));
            float glyphX = (float)Math.Round((ModelToolbarButtonWidth - boundsGlyphWidth) * 0.5d);
            float glyphY = (float)Math.Round((ModelToolbarButtonHeight - boundsGlyphHeight) * 0.5d);
            boundsButtonLabelHost.Position = new float3(glyphX, glyphY, 0.1f);
            boundsButtonText.Size = new int2(boundsGlyphWidth, boundsGlyphHeight);
        }

        /// <summary>
        /// Handles pointer interaction state updates for the model-preview grid button.
        /// </summary>
        /// <param name="position">Pointer position relative to the button.</param>
        /// <param name="delta">Pointer movement delta since the previous interaction event.</param>
        /// <param name="interaction">Pointer interaction state reported by the input system.</param>
        void HandleGridButtonCursor(int2 position, int2 delta, PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    IsGridButtonHovered = true;
                    break;
                case PointerInteraction.Press:
                    IsGridButtonPressed = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldToggle = IsGridButtonPressed && IsGridButtonHovered;
                    IsGridButtonPressed = false;
                    if (shouldToggle) {
                        ToggleModelGrid();
                    }
                    break;
                case PointerInteraction.Leave:
                    IsGridButtonHovered = false;
                    IsGridButtonPressed = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateGridButtonVisuals();
        }

        /// <summary>
        /// Applies active, hover, press, and keyboard-focus colors to the model-preview grid button.
        /// </summary>
        void UpdateGridButtonVisuals() {
            if (IsGridButtonPressed) {
                gridButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (IsModelGridVisibleValue && modelToolbarRoot.Enabled) {
                gridButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (IsGridButtonHovered || IsGridButtonKeyboardFocused) {
                gridButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                gridButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (IsModelGridVisibleValue || IsGridButtonHovered || IsGridButtonPressed || IsGridButtonKeyboardFocused) {
                gridButtonIcon.Color = new byte4(255, 255, 255, 255);
            } else {
                gridButtonIcon.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Handles pointer interaction state updates for the model-preview bounds display cycle button.
        /// </summary>
        /// <param name="position">Pointer position relative to the button.</param>
        /// <param name="delta">Pointer movement delta since the previous interaction event.</param>
        /// <param name="interaction">Pointer interaction state reported by the input system.</param>
        void HandleBoundsButtonCursor(int2 position, int2 delta, PointerInteraction interaction) {
            switch (interaction) {
                case PointerInteraction.Hover:
                    IsBoundsButtonHovered = true;
                    break;
                case PointerInteraction.Press:
                    IsBoundsButtonPressed = true;
                    break;
                case PointerInteraction.Release:
                    bool shouldCycle = IsBoundsButtonPressed && IsBoundsButtonHovered;
                    IsBoundsButtonPressed = false;
                    if (shouldCycle) {
                        CycleModelBoundsDisplayMode();
                    }
                    break;
                case PointerInteraction.Leave:
                    IsBoundsButtonHovered = false;
                    IsBoundsButtonPressed = false;
                    break;
                case PointerInteraction.None:
                    break;
                default:
                    throw new InvalidOperationException("Pointer interaction state is not supported.");
            }

            UpdateBoundsButtonVisuals();
        }

        /// <summary>
        /// Applies active, hover, press, and keyboard-focus colors to the model-preview bounds display button.
        /// </summary>
        void UpdateBoundsButtonVisuals() {
            if (IsBoundsButtonPressed) {
                boundsButtonBackground.Color = ThemeManager.Colors.AccentTertiary;
            } else if (ModelBoundsDisplayModeValue != ModelPreviewBoundsDisplayMode.None && modelToolbarRoot.Enabled) {
                boundsButtonBackground.Color = ThemeManager.Colors.AccentPrimary;
            } else if (IsBoundsButtonHovered || IsBoundsButtonKeyboardFocused) {
                boundsButtonBackground.Color = ThemeManager.Colors.AccentSecondary;
            } else {
                boundsButtonBackground.Color = ThemeManager.Colors.SurfaceInput;
            }

            if (ModelBoundsDisplayModeValue != ModelPreviewBoundsDisplayMode.None || IsBoundsButtonHovered || IsBoundsButtonPressed || IsBoundsButtonKeyboardFocused) {
                boundsButtonText.Color = new byte4(255, 255, 255, 255);
            } else {
                boundsButtonText.Color = new byte4(255, 255, 255, 224);
            }
        }

        /// <summary>
        /// Builds the caption text used beneath a texture preview.
        /// </summary>
        /// <param name="texture">Texture to describe.</param>
        /// <returns>Human-readable resolution string.</returns>
        string BuildResolutionLabelText(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            return texture.Width + " x " + texture.Height;
        }

        /// <summary>
        /// Measures the caption text used beneath a texture preview.
        /// </summary>
        /// <param name="labelText">Caption text to measure.</param>
        /// <returns>Measured label size in pixels.</returns>
        int2 GetResolutionLabelSize(string labelText) {
            if (TitleFont == null) {
                return new int2(1, 1);
            }

            float2 measured = TitleFont.MeasureString(labelText);
            int width = Math.Max(1, (int)Math.Ceiling(measured.X));
            int height = Math.Max(1, (int)Math.Ceiling(TitleFont.LineHeight));
            return new int2(width, height);
        }

        /// <summary>
        /// Computes the usable preview viewport after subtracting the caption space when needed.
        /// </summary>
        /// <param name="contentSize">Panel content size available before caption layout.</param>
        /// <param name="labelSize">Measured caption size.</param>
        /// <returns>Usable image viewport size in pixels.</returns>
        int2 GetTextureViewportSize(int2 contentSize, int2 labelSize) {
            int viewportHeight = contentSize.Y;
            if (labelSize.Y > 0) {
                viewportHeight = Math.Max(1, viewportHeight - labelSize.Y - ResolutionLabelGap);
            }

            return new int2(Math.Max(1, contentSize.X), Math.Max(1, viewportHeight));
        }

        /// <summary>
        /// Computes the displayed size of one texture for the supplied viewport and zoom scale.
        /// </summary>
        /// <param name="texture">Texture being laid out.</param>
        /// <param name="viewportSize">Available viewport size in pixels.</param>
        /// <param name="zoomScale">Additional zoom multiplier to apply.</param>
        /// <returns>Scaled texture size in pixels.</returns>
        int2 GetTextureDisplaySize(RuntimeTexture texture, int2 viewportSize, double zoomScale) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            int sourceWidth = Math.Max(1, texture.Width);
            int sourceHeight = Math.Max(1, texture.Height);
            double widthScale = viewportSize.X / (double)sourceWidth;
            double heightScale = viewportSize.Y / (double)sourceHeight;
            double scale = Math.Min(widthScale, heightScale) * zoomScale;

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return new int2(targetWidth, targetHeight);
        }

        /// <summary>
        /// Computes the top-left position used to center one texture inside a viewport.
        /// </summary>
        /// <param name="viewportSize">Available viewport size in pixels.</param>
        /// <param name="displaySize">Current texture size in pixels.</param>
        /// <returns>Centered top-left position relative to the preview content root.</returns>
        float2 GetCenteredTexturePosition(int2 viewportSize, int2 displaySize) {
            float left = GetContentPaddingPixels() + (viewportSize.X - displaySize.X) * 0.5f;
            float top = GetContentPaddingPixels() + (viewportSize.Y - displaySize.Y) * 0.5f;
            return new float2(left, top);
        }

        /// <summary>
        /// Gets the usable content size for the current panel dimensions.
        /// </summary>
        /// <returns>Usable preview content size in pixels.</returns>
    int2 GetContentSize() {
        return new int2(
            Math.Max(1, Size.X - GetContentPaddingPixels() * 2),
            Math.Max(1, Size.Y - GetContentPaddingPixels() * 2));
    }

        /// <summary>
        /// Gets the usable render-target size after reserving toolbar height for model previews.
        /// </summary>
        /// <returns>Size available to the active preview source.</returns>
        int2 GetPreviewContentSize() {
            int2 contentSize = GetContentSize();
            if (!IsModelPreviewSource()) {
                return contentSize;
            }

            return new int2(contentSize.X, Math.Max(1, contentSize.Y - ModelToolbarHeight));
        }

        /// <summary>
        /// Gets the scaled content padding used around the preview texture.
        /// </summary>
        /// <returns>Scaled preview content padding in pixels.</returns>
        int GetContentPaddingPixels() {
            if (ContentPadding <= 0) {
                return 0;
            }

            return UiMetrics.ScalePixels(ContentPadding);
        }

        /// <summary>
        /// Returns true when the pointer lies inside the preview content area below the title bar.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer is inside the preview body.</returns>
        bool IsPointerInsideContent(int2 pointer) {
            int panelLeft = (int)Math.Round(Position.X);
            int panelTop = (int)Math.Round(Position.Y) + TitleBarHeightPixels;
            int panelWidth = Size.X;
            int panelHeight = Size.Y;

            return pointer.X >= panelLeft &&
                   pointer.X < panelLeft + panelWidth &&
                   pointer.Y >= panelTop &&
                   pointer.Y < panelTop + panelHeight;
        }

        /// <summary>
        /// Returns true when the pointer lies inside the visible model-preview toolbar.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer is inside the model toolbar bounds.</returns>
        bool IsPointerInsideModelToolbar(int2 pointer) {
            if (!modelToolbarRoot.Enabled) {
                return false;
            }

            int toolbarLeft = (int)Math.Round(modelToolbarRoot.Position.X);
            int toolbarTop = (int)Math.Round(modelToolbarRoot.Position.Y);
            return pointer.X >= toolbarLeft &&
                   pointer.X < toolbarLeft + modelToolbarBackground.Size.X &&
                   pointer.Y >= toolbarTop &&
                   pointer.Y < toolbarTop + modelToolbarBackground.Size.Y;
        }

        /// <summary>
        /// Returns true when the pointer lies inside the model-preview grid button.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer is inside the grid-button bounds.</returns>
        bool ContainsGridButtonPoint(int2 pointer) {
            if (!gridButtonRoot.Enabled || !modelToolbarRoot.Enabled) {
                return false;
            }

            int buttonLeft = (int)Math.Round(gridButtonRoot.Position.X);
            int buttonTop = (int)Math.Round(gridButtonRoot.Position.Y);
            return pointer.X >= buttonLeft &&
                   pointer.X < buttonLeft + gridButtonInteractable.Size.X &&
                   pointer.Y >= buttonTop &&
                   pointer.Y < buttonTop + gridButtonInteractable.Size.Y;
        }

        /// <summary>
        /// Returns true when the pointer lies inside the model-preview bounds display cycle button.
        /// </summary>
        /// <param name="pointer">Pointer position in screen coordinates.</param>
        /// <returns>True when the pointer is inside the bounds-button bounds.</returns>
        bool ContainsBoundsButtonPoint(int2 pointer) {
            if (!boundsButtonRoot.Enabled || !modelToolbarRoot.Enabled) {
                return false;
            }

            int buttonLeft = (int)Math.Round(boundsButtonRoot.Position.X);
            int buttonTop = (int)Math.Round(boundsButtonRoot.Position.Y);
            return pointer.X >= buttonLeft &&
                   pointer.X < buttonLeft + boundsButtonInteractable.Size.X &&
                   pointer.Y >= buttonTop &&
                   pointer.Y < buttonTop + boundsButtonInteractable.Size.Y;
        }

        /// <summary>Creates a canonical authored reference from a file-backed browser entry.</summary>
        /// <param name="entry">Browser entry to convert.</param>
        /// <returns>Canonical reference, or null for generated entries.</returns>
        static SceneAssetReference CreateAuthoredReference(AssetBrowserEntry entry) {
            if (entry == null || entry.IsGenerated || string.IsNullOrWhiteSpace(entry.AssetId) || string.IsNullOrWhiteSpace(entry.ContentHash)) {
                return null;
            }
            return global::helengine.SceneAssetReferenceFactory.CreateFileSystemReference(entry.AssetId, entry.RelativePath, entry.ContentHash);
        }

        /// <summary>
        /// Resolves one stable scene entity id for the provided scene selection.
        /// </summary>
        /// <param name="selectedEntity">Scene selection whose id should be captured.</param>
        /// <returns>Stable scene entity id when one is available; otherwise an empty string.</returns>
        uint ResolveSceneEntityId(Entity selectedEntity) {
            EntitySaveComponent saveComponent = FindSaveComponent(selectedEntity);
            if (saveComponent != null && saveComponent.EntityId != 0u) {
                return saveComponent.EntityId;
            }

            return 0u;
        }

        /// <summary>
        /// Returns the hidden persistence component attached to the supplied entity when one exists.
        /// </summary>
        /// <param name="selectedEntity">Entity whose persistence metadata should be inspected.</param>
        /// <returns>Attached save component when present; otherwise null.</returns>
        static EntitySaveComponent FindSaveComponent(Entity selectedEntity) {
            if (selectedEntity == null || selectedEntity.Components == null) {
                return null;
            }

            for (int index = 0; index < selectedEntity.Components.Count; index++) {
                if (selectedEntity.Components[index] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }
    }
}

using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Presents schema-driven per-platform material authoring inside the properties panel.
    /// </summary>
    public class MaterialAssetView {
        /// <summary>
        /// Height of each visible row.
        /// </summary>
        const int RowHeight = 24;

        /// <summary>
        /// Spacing between stacked rows.
        /// </summary>
        const int RowSpacing = 6;

        /// <summary>
        /// Spacing between label, value, and button controls.
        /// </summary>
        const int ControlSpacing = 8;

        /// <summary>
        /// Width reserved for row labels.
        /// </summary>
        const int LabelWidth = 96;

        /// <summary>
        /// Width reserved for the optional field action button.
        /// </summary>
        const int ButtonWidth = 80;

        /// <summary>
        /// Field id used by shader-backed schemas for shader assignment.
        /// </summary>
        const string ShaderAssetIdFieldId = "shader-asset-id";

        /// <summary>
        /// Field id used by shader-backed schemas for texture assignment.
        /// </summary>
        const string TextureAssetIdFieldId = "texture-id";

        /// <summary>
        /// Field id used to toggle custom shader overrides.
        /// </summary>
        const string UseCustomShaderFieldId = "use-custom-shader";

        /// <summary>
        /// Field id used by shader-backed schemas to toggle shadow casting.
        /// </summary>
        const string CastsShadowFieldId = "casts-shadow";

        /// <summary>
        /// Field id used by shader-backed schemas to toggle shadow receiving.
        /// </summary>
        const string ReceivesShadowFieldId = "receives-shadow";

        /// <summary>
        /// Field id used by shader-backed schemas for vertex program assignment.
        /// </summary>
        const string VertexProgramFieldId = "vertex-program";

        /// <summary>
        /// Field id used by shader-backed schemas for pixel program assignment.
        /// </summary>
        const string PixelProgramFieldId = "pixel-program";

        /// <summary>
        /// Font used for text elements.
        /// </summary>
        readonly FontAsset Font;

        /// <summary>
        /// Render order used for text labels and values.
        /// </summary>
        readonly byte TextOrder;

        /// <summary>
        /// Root entity that owns view visuals.
        /// </summary>
        readonly EditorEntity RootEntity;

        /// <summary>
        /// Shared platform tab strip used to switch the active platform panel.
        /// </summary>
        readonly PlatformTabStripView PlatformTabStrip;

        /// <summary>
        /// Supported platform identifiers shown in the platform picker.
        /// </summary>
        readonly List<string> SupportedPlatformIds;

        /// <summary>
        /// Material panels created for each supported platform.
        /// </summary>
        readonly Dictionary<string, MaterialAssetPlatformPanel> PlatformPanels;

        /// <summary>
        /// Shared host entity used to keep the color picker outside the scrollable inspector content.
        /// </summary>
        readonly EditorEntity ColorPickerHost;

        /// <summary>
        /// Layer mask used by the shared color picker overlay when it renders outside the clipped inspector content.
        /// </summary>
        readonly ushort ColorPickerOverlayLayerMask;

        /// <summary>
        /// Shared color picker overlay reused by all material color fields.
        /// </summary>
        readonly EditorColorPickerOverlayComponent ColorPickerOverlay;

        /// <summary>
        /// Service used to load and save material settings sidecars.
        /// </summary>
        readonly MaterialAssetSettingsService SettingsService;

        /// <summary>
        /// Service used to normalize and switch builder-defined schema selections.
        /// </summary>
        readonly MaterialAssetSchemaSettingsService SchemaSettingsService;

        /// <summary>
        /// Currently selected asset entry.
        /// </summary>
        AssetBrowserEntry CurrentEntry;

        /// <summary>
        /// Currently loaded material asset.
        /// </summary>
        MaterialAsset CurrentAsset;

        /// <summary>
        /// Currently loaded material sidecar settings.
        /// </summary>
        AssetImportSettings CurrentSettings;

        /// <summary>
        /// Current platform metadata resolver.
        /// </summary>
        Func<string, EditorPlatformBuildSelectionModel> SelectionModelResolver;

        /// <summary>
        /// Currently selected platform identifier.
        /// </summary>
        string CurrentPlatformId;

        /// <summary>
        /// Active color field currently being edited by the shared color picker.
        /// </summary>
        EditorColorFieldControl ActiveColorFieldControl;

        /// <summary>
        /// Platform identifier that owns the active color field.
        /// </summary>
        string ActiveColorFieldPlatformId;

        /// <summary>
        /// Field identifier owned by the active color field.
        /// </summary>
        string ActiveColorFieldId;

        /// <summary>
        /// Cached layout height.
        /// </summary>
        int LayoutHeight;

        /// <summary>
        /// Tracks whether the view is visible.
        /// </summary>
        bool IsViewVisible;

        /// <summary>
        /// Initializes a new view for material asset editing.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        public MaterialAssetView(FontAsset font, ushort layerMask) : this(font, layerMask, null) {
        }

        /// <summary>
        /// Initializes a new view for material asset editing with a separate host for shared overlay UI.
        /// </summary>
        /// <param name="font">Font used for text rendering.</param>
        /// <param name="layerMask">Layer mask applied to the view entities.</param>
        /// <param name="overlayHost">Entity that should host non-scrolling overlay UI such as the color picker.</param>
        public MaterialAssetView(FontAsset font, ushort layerMask, EditorEntity overlayHost) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            TextOrder = RenderOrder2D.PanelForeground;
            SupportedPlatformIds = new List<string>(4);
            PlatformPanels = new Dictionary<string, MaterialAssetPlatformPanel>(StringComparer.OrdinalIgnoreCase);
            SettingsService = new MaterialAssetSettingsService();
            SchemaSettingsService = new MaterialAssetSchemaSettingsService();

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;

            PlatformTabStrip = new PlatformTabStripView(font, layerMask, 88, RowHeight, 0, RowHeight);
            PlatformTabStrip.Root.Enabled = false;
            PlatformTabStrip.SetRenderOrders(RenderOrder2D.PanelSurface, TextOrder);
            PlatformTabStrip.Root.Position = float3.Zero;
            RootEntity.AddChild(PlatformTabStrip.Root);

            if (overlayHost != null) {
                ColorPickerHost = overlayHost;
                ColorPickerOverlayLayerMask = EditorLayerMasks.EditorModalUi;
            } else {
                ColorPickerHost = RootEntity;
                ColorPickerOverlayLayerMask = layerMask;
            }

            ColorPickerOverlay = new EditorColorPickerOverlayComponent(font, ColorPickerOverlayLayerMask);
            ColorPickerOverlay.ColorChanged += HandleSharedColorPickerChanged;
            ColorPickerOverlay.Closed += HandleSharedColorPickerClosed;
            ColorPickerHost.AddChild(ColorPickerOverlay);

            Hide();
        }

        /// <summary>
        /// Gets the root entity to attach into the properties panel.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets the current height of the view layout.
        /// </summary>
        public int Height => LayoutHeight;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => IsViewVisible;

        /// <summary>
        /// Shows the view for the specified material asset and per-platform settings payload.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="materialAsset">Material asset to edit.</param>
        /// <param name="settings">Per-platform settings sidecar for the material asset.</param>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        /// <param name="activePlatformId">Currently active project platform identifier.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform.</param>
        public void Show(
            AssetBrowserEntry entry,
            MaterialAsset materialAsset,
            AssetImportSettings settings,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (supportedPlatforms.Count == 0) {
                throw new ArgumentException("At least one supported platform must be provided.", nameof(supportedPlatforms));
            } else if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            } else if (selectionModelResolver == null) {
                throw new ArgumentNullException(nameof(selectionModelResolver));
            }

            CurrentEntry = entry;
            CurrentAsset = materialAsset;
            CurrentSettings = settings;
            SelectionModelResolver = selectionModelResolver;

            SetSupportedPlatforms(supportedPlatforms);
            CurrentPlatformId = ResolveSelectedPlatformId(activePlatformId);
            BuildPlatformPanels();
            PlatformTabStrip.SetPlatforms(SupportedPlatformIds, CurrentPlatformId, HandlePlatformSelectionChanged);
            PlatformTabStrip.SetSelectedPlatform(CurrentPlatformId);
            UpdatePlatformVisibility();
            IsViewVisible = true;
            RootEntity.Enabled = true;
        }

        /// <summary>
        /// Hides the view and clears the current material state.
        /// </summary>
        public void Hide() {
            if (CurrentEntry != null && CurrentSettings != null) {
                SyncCurrentFieldValues(CurrentPlatformId, saveToDisk: true);
            }

            if (ColorPickerOverlay != null && ColorPickerOverlay.IsOpen) {
                ColorPickerOverlay.Close();
            }

            ClearPlatformPanels();
            PlatformTabStrip.Root.Enabled = false;
            IsViewVisible = false;
            RootEntity.Enabled = false;
            CurrentEntry = null;
            CurrentAsset = null;
            CurrentSettings = null;
            SelectionModelResolver = null;
            CurrentPlatformId = string.Empty;
            SupportedPlatformIds.Clear();
        }

        /// <summary>
        /// Updates the view layout within the properties panel.
        /// </summary>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="width">Available width in pixels.</param>
        public void UpdateLayout(int left, int top, int width) {
            if (!IsViewVisible) {
                LayoutHeight = 0;
                return;
            }

            int safeWidth = Math.Max(0, width);
            int currentTop = top;

            PlatformTabStrip.UpdateLayout(left, currentTop, Math.Max(1, safeWidth));
            currentTop += RowHeight + RowSpacing;

            MaterialAssetPlatformPanel activePanel = GetActivePlatformPanel();
            if (activePanel != null) {
                activePanel.SetVisible(true);
                activePanel.UpdateLayout(left, currentTop, safeWidth);
                currentTop += activePanel.Height + RowSpacing;
            }

            if (ColorPickerOverlay != null && ColorPickerOverlay.IsOpen) {
                ColorPickerOverlay.UpdateLayout();
            }

            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
                if (panel == null || panel == activePanel) {
                    continue;
                }

                panel.SetVisible(false);
            }

            LayoutHeight = currentTop - top;
        }

        /// <summary>
        /// Handles platform tab selection changes.
        /// </summary>
        /// <param name="value">Selected platform identifier.</param>
        void HandlePlatformSelectionChanged(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new InvalidOperationException("Platform selection was not provided.");
            }

            SyncCurrentFieldValues(CurrentPlatformId, saveToDisk: true);
            CurrentPlatformId = value;
            UpdatePlatformVisibility();
        }

        /// <summary>
        /// Handles schema picker selection changes.
        /// </summary>
        /// <param name="index">Selected schema index.</param>
        /// <param name="value">Selected schema display label.</param>
        void HandleSchemaSelectionChanged(string platformId, int index, string value) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (index < 0) {
                throw new InvalidOperationException("Schema selection index is out of range.");
            }

            MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
            if (panel == null || panel.IsUpdatingSchemaSelectionValue) {
                return;
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            PlatformMaterialSchemaDefinition[] availableSchemas = ResolveAvailableSchemas(platformId);
            if (index >= availableSchemas.Length) {
                throw new InvalidOperationException("Schema selection index is out of range.");
            }

            string schemaId = availableSchemas[index].SchemaId;
            SchemaSettingsService.SelectSchema(materialSettings, availableSchemas, schemaId);
            RebuildPlatformPanel(platformId);
            SaveCurrentMaterialState(platformId);
        }

        /// <summary>
        /// Handles text-field changes for one schema-driven field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose text changed.</param>
        /// <param name="textBox">Text box whose value changed.</param>
        void HandleTextFieldChanged(string platformId, string fieldId, TextBoxComponent textBox) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (textBox == null) {
                throw new ArgumentNullException(nameof(textBox));
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = textBox.Text ?? string.Empty;
        }

        /// <summary>
        /// Handles text-field submission for one schema-driven field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose text was submitted.</param>
        /// <param name="textBox">Text box whose value was submitted.</param>
        void HandleTextFieldSubmitted(string platformId, string fieldId, TextBoxComponent textBox) {
            HandleTextFieldChanged(platformId, fieldId, textBox);
            SaveCurrentMaterialState(platformId);
        }

        /// <summary>
        /// Handles combo-box selection changes for one schema-driven choice field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose selection changed.</param>
        /// <param name="value">Selected serialized value.</param>
        void HandleChoiceFieldChanged(string platformId, string fieldId, string value) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = value;
            SaveCurrentMaterialState(platformId);
        }

        /// <summary>
        /// Handles check-box state changes for one schema-driven boolean field.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier whose value changed.</param>
        /// <param name="isChecked">Current checked state.</param>
        void HandleBooleanFieldChanged(string platformId, string fieldId, bool isChecked) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = isChecked ? "true" : "false";
            SaveCurrentMaterialState(platformId);
            if (string.Equals(fieldId, UseCustomShaderFieldId, StringComparison.OrdinalIgnoreCase)) {
                RebuildPlatformPanel(platformId);
            }
        }

        /// <summary>
        /// Handles live color updates from one schema-driven color field.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier whose value changed.</param>
        /// <param name="color">Current color value.</param>
        void HandleColorFieldChanged(string platformId, string fieldId, byte4 color) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            materialSettings.FieldValues[fieldId] = EditorColorUtils.FormatHtmlColor(color);
        }

        /// <summary>
        /// Handles color-field submission and persists the committed value.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier whose value changed.</param>
        /// <param name="color">Committed color value.</param>
        void HandleColorFieldSubmitted(string platformId, string fieldId, byte4 color) {
            HandleColorFieldChanged(platformId, fieldId, color);
            SaveCurrentMaterialState(platformId);
        }

        /// <summary>
        /// Opens the shared color picker for one field control.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier being edited.</param>
        /// <param name="colorControl">Color field control that requested the picker.</param>
        void HandleColorFieldPickerRequested(string platformId, string fieldId, EditorColorFieldControl colorControl) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (colorControl == null) {
                throw new ArgumentNullException(nameof(colorControl));
            }

            if (ColorPickerOverlay.IsOpen &&
                ActiveColorFieldControl == colorControl &&
                string.Equals(ActiveColorFieldPlatformId, platformId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ActiveColorFieldId, fieldId, StringComparison.OrdinalIgnoreCase)) {
                ColorPickerOverlay.Close();
                return;
            }

            ActiveColorFieldControl = colorControl;
            ActiveColorFieldPlatformId = platformId;
            ActiveColorFieldId = fieldId;
            if (colorControl.SwatchButtonControl.Parent != null) {
                float3 anchorPosition = colorControl.SwatchButtonControl.Parent.Position;
                ColorPickerOverlay.SetAnchorPosition(anchorPosition.X, anchorPosition.Y, colorControl.SwatchButtonControl.Size.Y);
            }
            ColorPickerOverlay.Open(colorControl.Value);
        }

        /// <summary>
        /// Applies shared picker updates to the active color field.
        /// </summary>
        /// <param name="color">Current color from the shared picker.</param>
        void HandleSharedColorPickerChanged(byte4 color) {
            if (ActiveColorFieldControl == null) {
                return;
            }

            ActiveColorFieldControl.SetValue(color);
            HandleColorFieldChanged(ActiveColorFieldPlatformId, ActiveColorFieldId, color);
        }

        /// <summary>
        /// Persists the active color field when the shared picker closes.
        /// </summary>
        void HandleSharedColorPickerClosed() {
            if (ActiveColorFieldControl == null) {
                return;
            }

            SaveCurrentMaterialState(ActiveColorFieldPlatformId);
            ActiveColorFieldControl = null;
            ActiveColorFieldPlatformId = null;
            ActiveColorFieldId = null;
        }

        /// <summary>
        /// Requests an asset pick from the asset picker service for one asset-reference field.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier that should receive the selected asset id.</param>
        void RequestAssetPick(string platformId, string fieldId) {
            if (CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                return;
            }

            if (string.Equals(fieldId, TextureAssetIdFieldId, StringComparison.OrdinalIgnoreCase)) {
                string textureExtensionFilter = string.Join(";", TextureImportFormatCatalog.AllTextureExtensions);
                EditorAssetPickerService.RequestPick(entry => HandleTexturePicked(platformId, fieldId, entry), textureExtensionFilter);
            } else {
                EditorAssetPickerService.RequestPick(entry => HandleShaderPicked(platformId, fieldId, entry), EditorFileTemplateRegistry.ShaderExtension);
            }
        }

        /// <summary>
        /// Handles shader selections from the asset picker.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier that should receive the shader asset id.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleShaderPicked(string platformId, string fieldId, AssetBrowserEntry entry) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (entry == null || CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                return;
            }
            if (entry.IsDirectory || !IsShaderEntry(entry)) {
                return;
            }

            try {
                string shaderId = ShaderAssetIdUtils.BuildShaderAssetId(entry.FullPath);
                ApplyShaderIdToActivePlatform(platformId, fieldId, shaderId);
                UpdateFieldControlsFromSettings(platformId);
                SaveCurrentMaterialState(platformId);
            } catch (Exception ex) {
                Logger.WriteError($"Failed to assign platform shader: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles texture selections from the asset picker.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier that should receive the texture asset id.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleTexturePicked(string platformId, string fieldId, AssetBrowserEntry entry) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (entry == null || CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                return;
            }
            if (entry.IsDirectory) {
                return;
            }

            try {
                string textureId = ShaderAssetIdUtils.BuildShaderAssetId(entry.FullPath);
                ApplyTextureIdToActivePlatform(platformId, fieldId, textureId);
                UpdateFieldControlsFromSettings(platformId);
                SaveCurrentMaterialState(platformId);
            } catch (Exception ex) {
                Logger.WriteError($"Failed to assign platform texture: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies one shader assignment to the active platform settings payload.
        /// </summary>
        /// <param name="fieldId">Builder-defined field identifier that stores the shader asset id.</param>
        /// <param name="shaderId">Shader identifier to apply.</param>
        void ApplyShaderIdToActivePlatform(string platformId, string fieldId, string shaderId) {
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                throw new InvalidOperationException("Active platform material settings are not available.");
            }

            materialSettings.FieldValues[fieldId] = shaderId ?? string.Empty;
            materialSettings.FieldValues[VertexProgramFieldId] = string.IsNullOrWhiteSpace(shaderId) ? string.Empty : string.Concat(shaderId, ".vs");
            materialSettings.FieldValues[PixelProgramFieldId] = string.IsNullOrWhiteSpace(shaderId) ? string.Empty : string.Concat(shaderId, ".ps");
            materialSettings.FieldValues[UseCustomShaderFieldId] = "true";
        }

        /// <summary>
        /// Applies one texture assignment to the active platform settings payload.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the field.</param>
        /// <param name="fieldId">Builder-defined field identifier that stores the texture asset id.</param>
        /// <param name="textureId">Texture identifier to apply.</param>
        void ApplyTextureIdToActivePlatform(string platformId, string fieldId, string textureId) {
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                throw new InvalidOperationException("Active platform material settings are not available.");
            }

            materialSettings.FieldValues[fieldId] = textureId ?? string.Empty;
        }

        /// <summary>
        /// Saves the material sidecar and mirrors the active platform fields back into the serialized material asset used by preview consumers.
        /// </summary>
        void SaveCurrentMaterialState(string platformId) {
            if (CurrentEntry == null || CurrentAsset == null || CurrentSettings == null) {
                throw new InvalidOperationException("Cannot save a material view that is not bound to an asset.");
            }

            SettingsService.ApplyPlatformMaterialFields(CurrentAsset, CurrentSettings, platformId);
            SaveMaterialAsset(CurrentEntry.FullPath, CurrentAsset);
            SettingsService.Save(CurrentEntry.FullPath, CurrentSettings);
            RefreshShaderResources(CurrentAsset.ShaderAssetId);
        }

        /// <summary>
        /// Forces a shader reload so runtime materials update immediately.
        /// </summary>
        /// <param name="shaderId">Shader identifier to reload.</param>
        void RefreshShaderResources(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                return;
            }

            try {
                ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(shaderId);
                Core.Instance.RenderManager3D.InvalidateShaderResources(shaderId, shaderAsset);
            } catch (Exception ex) {
                Logger.WriteError($"Shader refresh failed for '{shaderId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the material asset back to disk.
        /// </summary>
        /// <param name="path">Material asset file path.</param>
        /// <param name="materialAsset">Material asset to serialize.</param>
        void SaveMaterialAsset(string path, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Rebuilds the visible field-editor rows for the active schema.
        /// </summary>
        void RebuildPlatformPanel(string platformId) {
            MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
            if (panel == null) {
                return;
            }

            panel.ClearFieldRows();

            PlatformMaterialSchemaDefinition materialSchema = EnsurePlatformSchema(platformId);
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSchema == null) {
                return;
            }

            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                if (!ShouldRenderField(field, materialSettings)) {
                    continue;
                }

                MaterialAssetFieldEditorRow row = CreateFieldRow(platformId, field);
                panel.AddFieldRow(row);
            }

            UpdateFieldControlsFromSettings(platformId);
        }

        /// <summary>
        /// Creates one field-editor row for the supplied material field definition.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the row.</param>
        /// <param name="field">Builder-defined field to render.</param>
        /// <returns>Created field-editor row.</returns>
        MaterialAssetFieldEditorRow CreateFieldRow(string platformId, PlatformMaterialFieldDefinition field) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            EditorEntity labelHost = CreateTextHost(RootEntity.LayerMask, out TextComponent labelText, field.DisplayName);
            EditorEntity valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;

            if (field.FieldKind == PlatformMaterialFieldKind.Boolean) {
                CheckBoxComponent checkBox = new CheckBoxComponent(new int2(RowHeight, RowHeight), Font);
                checkBox.CheckedChanged += (component, isChecked) => HandleBooleanFieldChanged(platformId, field.FieldId, isChecked);
                valueHost.AddComponent(checkBox);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, null, null, checkBox, null, null, null);
            }

            if (field.FieldKind == PlatformMaterialFieldKind.Choice) {
                ComboBoxComponent comboBox = new ComboBoxComponent(new int2(180, RowHeight), Font, field.AllowedValues, -1);
                comboBox.SelectionChanged += (index, value) => HandleChoiceFieldChanged(platformId, field.FieldId, value);
                valueHost.AddComponent(comboBox);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, null, comboBox, null, null, null, null);
            }

            if (field.FieldKind == PlatformMaterialFieldKind.Color) {
                EditorColorFieldControl colorControl = new EditorColorFieldControl(Font, RootEntity.LayerMask);
                colorControl.ColorChanged += color => HandleColorFieldChanged(platformId, field.FieldId, color);
                colorControl.Submitted += color => HandleColorFieldSubmitted(platformId, field.FieldId, color);
                colorControl.PickerRequested += () => HandleColorFieldPickerRequested(platformId, field.FieldId, colorControl);
                valueHost.AddChild(colorControl);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, null, null, null, null, null, colorControl);
            }

            TextBoxComponent textBox = new TextBoxComponent(new int2(180, RowHeight), Font);
            textBox.TextChanged += currentTextBox => HandleTextFieldChanged(platformId, field.FieldId, currentTextBox);
            textBox.Submitted += currentTextBox => HandleTextFieldSubmitted(platformId, field.FieldId, currentTextBox);
            valueHost.AddComponent(textBox);

            if (IsAssetPickerField(field)) {
                EditorEntity buttonHost = new EditorEntity();
                buttonHost.LayerMask = RootEntity.LayerMask;
                ButtonComponent button = new ButtonComponent("Pick", new int2(ButtonWidth, RowHeight), Font, () => RequestAssetPick(platformId, field.FieldId));
                buttonHost.AddComponent(button);
                return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, textBox, null, null, buttonHost, button, null);
            }

            return new MaterialAssetFieldEditorRow(field.FieldId, field.FieldKind, labelHost, labelText, valueHost, textBox, null, null, null, null, null);
        }

        /// <summary>
        /// Updates field controls to reflect the active platform field-value payload.
        /// </summary>
        void UpdateFieldControlsFromSettings(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
            if (panel == null) {
                return;
            }

            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            PlatformMaterialSchemaDefinition materialSchema = FindActiveSchema(platformId);
            if (materialSettings == null || materialSchema == null) {
                return;
            }

            for (int index = 0; index < panel.FieldRows.Count; index++) {
                MaterialAssetFieldEditorRow row = panel.FieldRows[index];
                PlatformMaterialFieldDefinition field = FindFieldDefinition(materialSchema, row.FieldId);
                string value = ResolveFieldValue(materialSettings, field);
                ApplyFieldValueToControl(row, field, value);
            }
        }

        /// <summary>
        /// Synchronizes the current field controls back into the active platform settings payload.
        /// </summary>
        /// <param name="saveToDisk">True when the material asset and sidecar should be persisted after syncing.</param>
        void SyncCurrentFieldValues(string platformId, bool saveToDisk) {
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            if (materialSettings == null) {
                return;
            }

            MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
            if (panel == null) {
                return;
            }

            for (int index = 0; index < panel.FieldRows.Count; index++) {
                MaterialAssetFieldEditorRow row = panel.FieldRows[index];
                if (row.TextBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.TextBox.Text ?? string.Empty;
                } else if (row.ComboBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.ComboBox.HasSelection ? row.ComboBox.SelectedItem ?? string.Empty : string.Empty;
                } else if (row.CheckBox != null) {
                    materialSettings.FieldValues[row.FieldId] = row.CheckBox.IsChecked ? "true" : "false";
                } else if (row.ColorControl != null) {
                    materialSettings.FieldValues[row.FieldId] = EditorColorUtils.FormatHtmlColor(row.ColorControl.Value);
                }
            }

            if (saveToDisk) {
                SaveCurrentMaterialState(platformId);
            }
        }

        /// <summary>
        /// Ensures the active platform points at one valid schema and updates the schema combo-box selection.
        /// </summary>
        /// <returns>Resolved active schema or null when no schema is available.</returns>
        PlatformMaterialSchemaDefinition EnsurePlatformSchema(string platformId) {
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            PlatformMaterialSchemaDefinition[] materialSchemas = ResolveAvailableSchemas(platformId);
            MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
            if (materialSettings == null) {
                if (panel != null) {
                    panel.UpdateSchemaPicker(materialSchemas, string.Empty);
                }
                return null;
            }

            PlatformMaterialSchemaDefinition materialSchema = SchemaSettingsService.EnsureSelectedSchema(materialSettings, materialSchemas);
            string schemaId = materialSchema?.SchemaId ?? string.Empty;
            if (panel != null) {
                panel.UpdateSchemaPicker(materialSchemas, schemaId);
            }
            return materialSchema;
        }

        /// <summary>
        /// Finds the active material schema for the selected platform.
        /// </summary>
        /// <returns>Active schema or null when none was published.</returns>
        PlatformMaterialSchemaDefinition FindActiveSchema(string platformId) {
            MaterialAssetProcessorSettings materialSettings = GetMaterialSettings(platformId);
            string schemaId = materialSettings?.SchemaId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(schemaId)) {
                return null;
            }

            return FindSchemaDefinition(ResolveAvailableSchemas(platformId), schemaId);
        }

        /// <summary>
        /// Updates which platform panel is currently visible.
        /// </summary>
        void UpdatePlatformVisibility() {
            PlatformTabStrip.Root.Enabled = SupportedPlatformIds.Count > 0;

            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                MaterialAssetPlatformPanel panel = GetPlatformPanel(platformId);
                if (panel == null) {
                    continue;
                }

                panel.SetVisible(string.Equals(platformId, CurrentPlatformId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Returns the platform panel for one platform identifier.
        /// </summary>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>Matching panel or null when none exists.</returns>
        MaterialAssetPlatformPanel GetPlatformPanel(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return null;
            }

            MaterialAssetPlatformPanel panel;
            if (PlatformPanels.TryGetValue(platformId, out panel)) {
                return panel;
            }

            return null;
        }

        /// <summary>
        /// Returns the platform panel that is currently active.
        /// </summary>
        /// <returns>Active platform panel or null when no platform is selected.</returns>
        MaterialAssetPlatformPanel GetActivePlatformPanel() {
            return GetPlatformPanel(CurrentPlatformId);
        }

        /// <summary>
        /// Resolves the material schemas published for the selected platform.
        /// </summary>
        /// <returns>Published material schemas for the selected platform.</returns>
        PlatformMaterialSchemaDefinition[] ResolveAvailableSchemas(string platformId) {
            EditorPlatformBuildSelectionModel selectionModel = SelectionModelResolver?.Invoke(platformId);
            if (selectionModel == null || selectionModel.MaterialSchemas == null) {
                return Array.Empty<PlatformMaterialSchemaDefinition>();
            }

            return selectionModel.MaterialSchemas;
        }

        /// <summary>
        /// Finds one schema definition by identifier.
        /// </summary>
        /// <param name="materialSchemas">Schemas to search.</param>
        /// <param name="schemaId">Schema identifier to locate.</param>
        /// <returns>Matching schema or null when no schema matches the identifier.</returns>
        PlatformMaterialSchemaDefinition FindSchemaDefinition(
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
            string schemaId) {
            if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas));
            } else if (string.IsNullOrWhiteSpace(schemaId)) {
                return null;
            }

            for (int index = 0; index < materialSchemas.Count; index++) {
                PlatformMaterialSchemaDefinition materialSchema = materialSchemas[index];
                if (string.Equals(materialSchema.SchemaId, schemaId, StringComparison.OrdinalIgnoreCase)) {
                    return materialSchema;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds one field definition inside a material schema.
        /// </summary>
        /// <param name="materialSchema">Schema whose fields should be searched.</param>
        /// <param name="fieldId">Field identifier to locate.</param>
        /// <returns>Matching field definition.</returns>
        PlatformMaterialFieldDefinition FindFieldDefinition(PlatformMaterialSchemaDefinition materialSchema, string fieldId) {
            if (materialSchema == null) {
                throw new ArgumentNullException(nameof(materialSchema));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                if (string.Equals(field.FieldId, fieldId, StringComparison.OrdinalIgnoreCase)) {
                    return field;
                }
            }

            throw new InvalidOperationException($"Field '{fieldId}' was not found on schema '{materialSchema.SchemaId}'.");
        }

        /// <summary>
        /// Resolves the active platform material settings payload.
        /// </summary>
        /// <returns>Material settings for the active platform, or null when unavailable.</returns>
        MaterialAssetProcessorSettings GetMaterialSettings(string platformId) {
            if (CurrentSettings == null || CurrentSettings.Processor == null || CurrentSettings.Processor.Platforms == null) {
                return null;
            }

            AssetPlatformProcessorSettings platformSettings;
            if (!CurrentSettings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                return null;
            }

            return platformSettings.Material;
        }

        /// <summary>
        /// Resolves the active platform material settings payload.
        /// </summary>
        /// <returns>Material settings for the active platform, or null when unavailable.</returns>
        MaterialAssetProcessorSettings GetActiveMaterialSettings() {
            return GetMaterialSettings(CurrentPlatformId);
        }

        /// <summary>
        /// Builds one platform panel for every supported platform identifier.
        /// </summary>
        void BuildPlatformPanels() {
            ClearPlatformPanels();

            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                string platformId = SupportedPlatformIds[index];
                MaterialAssetPlatformPanel panel = CreatePlatformPanel(platformId);
                PlatformPanels[platformId] = panel;
                RootEntity.AddChild(panel.Root);
                panel.SetVisible(string.Equals(platformId, CurrentPlatformId, StringComparison.OrdinalIgnoreCase));
                RebuildPlatformPanel(platformId);
            }
        }

        /// <summary>
        /// Releases all platform panels and their generated child entities.
        /// </summary>
        void ClearPlatformPanels() {
            foreach (KeyValuePair<string, MaterialAssetPlatformPanel> pair in PlatformPanels) {
                pair.Value.Dispose();
            }

            PlatformPanels.Clear();
        }

        /// <summary>
        /// Creates one platform panel for the supplied platform identifier.
        /// </summary>
        /// <param name="platformId">Platform identifier represented by the new panel.</param>
        /// <returns>Created platform panel.</returns>
        MaterialAssetPlatformPanel CreatePlatformPanel(string platformId) {
            MaterialAssetPlatformPanel panel = new MaterialAssetPlatformPanel(platformId, Font, RootEntity.LayerMask, TextOrder);
            panel.SchemaComboBoxControl.SelectionChanged += (index, value) => HandleSchemaSelectionChanged(platformId, index, value);
            return panel;
        }

        /// <summary>
        /// Resolves the serialized value that should initialize one field editor.
        /// </summary>
        /// <param name="materialSettings">Active platform material settings payload.</param>
        /// <param name="field">Builder-defined field being resolved.</param>
        /// <returns>Serialized field value.</returns>
        string ResolveFieldValue(MaterialAssetProcessorSettings materialSettings, PlatformMaterialFieldDefinition field) {
            if (materialSettings.FieldValues != null && materialSettings.FieldValues.TryGetValue(field.FieldId, out string value)) {
                return value ?? string.Empty;
            }

            return field.DefaultValue ?? string.Empty;
        }

        /// <summary>
        /// Applies one serialized field value to the corresponding editor control.
        /// </summary>
        /// <param name="row">Editor row whose control should be updated.</param>
        /// <param name="field">Field definition that owns the control.</param>
        /// <param name="value">Serialized field value to display.</param>
        void ApplyFieldValueToControl(MaterialAssetFieldEditorRow row, PlatformMaterialFieldDefinition field, string value) {
            if (row.TextBox != null) {
                row.TextBox.Text = value ?? string.Empty;
                return;
            }

            if (row.ComboBox != null) {
                int selectedIndex = FindAllowedValueIndex(field.AllowedValues, value);
                row.ComboBox.SetItems(field.AllowedValues, selectedIndex);
                return;
            }

            if (row.CheckBox != null) {
                row.CheckBox.IsChecked = ParseBooleanValue(value);
                return;
            }

            if (row.ColorControl != null) {
                byte4 color;
                if (!EditorColorUtils.TryParseHtmlColor(value, out color) && !EditorColorUtils.TryParseHtmlColor(field.DefaultValue, out color)) {
                    color = new byte4(255, 255, 255, 255);
                }

                row.ColorControl.SetValue(color);
            }
        }

        /// <summary>
        /// Finds the selected index of one serialized choice value.
        /// </summary>
        /// <param name="allowedValues">Allowed choice values defined by the schema.</param>
        /// <param name="value">Serialized value to locate.</param>
        /// <returns>Matching choice index or zero when the value is unavailable.</returns>
        int FindAllowedValueIndex(string[] allowedValues, string value) {
            if (allowedValues == null || allowedValues.Length == 0) {
                return -1;
            }

            for (int index = 0; index < allowedValues.Length; index++) {
                if (string.Equals(allowedValues[index], value, StringComparison.OrdinalIgnoreCase)) {
                    return index;
                }
            }

            return 0;
        }

        /// <summary>
        /// Parses one serialized boolean value.
        /// </summary>
        /// <param name="value">Serialized value to parse.</param>
        /// <returns>Parsed boolean state.</returns>
        bool ParseBooleanValue(string value) {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        /// <summary>
        /// Populates the supported-platform list from the supplied project platform identifiers.
        /// </summary>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        void SetSupportedPlatforms(IReadOnlyList<string> supportedPlatforms) {
            SupportedPlatformIds.Clear();
            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                SupportedPlatformIds.Add(platformId);
            }
        }

        /// <summary>
        /// Resolves the platform id that should be selected initially.
        /// </summary>
        /// <param name="activePlatformId">Current project platform identifier.</param>
        /// <returns>Selected platform identifier guaranteed to exist in the view.</returns>
        string ResolveSelectedPlatformId(string activePlatformId) {
            for (int index = 0; index < SupportedPlatformIds.Count; index++) {
                if (string.Equals(SupportedPlatformIds[index], activePlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return SupportedPlatformIds[index];
                }
            }

            return SupportedPlatformIds[0];
        }

        /// <summary>
        /// Creates one text-host entity and text component pair.
        /// </summary>
        /// <param name="layerMask">Layer mask applied to the host entity.</param>
        /// <param name="textComponent">Created text component.</param>
        /// <param name="text">Initial text content.</param>
        /// <returns>Host entity that owns the text component.</returns>
        EditorEntity CreateTextHost(ushort layerMask, out TextComponent textComponent, string text) {
            EditorEntity host = new EditorEntity();
            host.LayerMask = layerMask;

            textComponent = new TextComponent();
            textComponent.Font = Font;
            textComponent.Text = text;
            textComponent.Color = ThemeManager.Colors.InputForegroundPrimary;
            textComponent.RenderOrder2D = TextOrder;
            host.AddComponent(textComponent);
            return host;
        }

        /// <summary>
        /// Lays out one label and combo-box row.
        /// </summary>
        /// <param name="labelHost">Label host entity.</param>
        /// <param name="labelText">Label text component.</param>
        /// <param name="comboHost">Combo-box host entity.</param>
        /// <param name="labelWidth">Reserved label width.</param>
        /// <param name="comboWidth">Available combo-box width.</param>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        void LayoutLabelAndCombo(
            EditorEntity labelHost,
            TextComponent labelText,
            EditorEntity comboHost,
            int labelWidth,
            int comboWidth,
            int left,
            int top) {
            labelHost.Position = new float3(left, top, 0.2f);
            labelText.Size = new int2(labelWidth, RowHeight);

            int comboLeft = left + labelWidth + ControlSpacing;
            comboHost.Position = new float3(comboLeft, top, 0.2f);
        }

        /// <summary>
        /// Lays out one schema-driven field row.
        /// </summary>
        /// <param name="row">Row to lay out.</param>
        /// <param name="left">Left offset in pixels.</param>
        /// <param name="top">Top offset in pixels.</param>
        /// <param name="safeWidth">Available width in pixels.</param>
        /// <param name="labelWidth">Reserved label width.</param>
        void LayoutFieldRow(MaterialAssetFieldEditorRow row, int left, int top, int safeWidth, int labelWidth) {
            int buttonWidth = row.ButtonHost != null ? Math.Min(ButtonWidth, safeWidth) : 0;
            int valueWidth = Math.Max(0, safeWidth - labelWidth - (row.ButtonHost != null ? buttonWidth + (ControlSpacing * 2) : ControlSpacing));

            row.LabelHost.Position = new float3(left, top, 0.2f);
            row.LabelText.Size = new int2(labelWidth, RowHeight);

            int valueLeft = left + labelWidth + ControlSpacing;
            row.ValueHost.Position = new float3(valueLeft, top, 0.2f);

            if (row.TextBox != null) {
                row.TextBox.Size = new int2(valueWidth, RowHeight);
            } else if (row.ComboBox != null) {
                row.ComboBox.Size = new int2(valueWidth, RowHeight);
            } else if (row.CheckBox != null) {
                row.CheckBox.Size = new int2(RowHeight, RowHeight);
            } else if (row.ColorControl != null) {
                row.ColorControl.Size = new int2(valueWidth, RowHeight);
            }

            if (row.ButtonHost != null) {
                int buttonLeft = valueLeft + valueWidth + ControlSpacing;
                row.ButtonHost.Position = new float3(buttonLeft, top, 0.2f);
            }
        }

        /// <summary>
        /// Determines whether one field should expose the asset picker convenience button.
        /// </summary>
        /// <param name="field">Field definition to evaluate.</param>
        /// <returns>True when the field should expose the asset picker.</returns>
        bool IsAssetPickerField(PlatformMaterialFieldDefinition field) {
            return field.FieldKind == PlatformMaterialFieldKind.AssetReference &&
                (string.Equals(field.FieldId, ShaderAssetIdFieldId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.FieldId, TextureAssetIdFieldId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether one field should be shown for the active material settings.
        /// </summary>
        /// <param name="field">Field definition to evaluate.</param>
        /// <param name="materialSettings">Active material settings that control conditional visibility.</param>
        /// <returns>True when the field should be shown in the editor.</returns>
        bool ShouldRenderField(PlatformMaterialFieldDefinition field, MaterialAssetProcessorSettings materialSettings) {
            if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            if (string.Equals(field.FieldId, UseCustomShaderFieldId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (IsCustomShaderEnabled(materialSettings)) {
                return true;
            }

            return !IsShaderOverrideField(field);
        }

        /// <summary>
        /// Determines whether custom-shader mode is enabled in the active material settings.
        /// </summary>
        /// <param name="materialSettings">Active material settings to inspect.</param>
        /// <returns>True when custom shader mode is enabled.</returns>
        bool IsCustomShaderEnabled(MaterialAssetProcessorSettings materialSettings) {
            if (materialSettings == null || materialSettings.FieldValues == null) {
                return false;
            }

            string customShaderValue;
            if (!materialSettings.FieldValues.TryGetValue(UseCustomShaderFieldId, out customShaderValue)) {
                return false;
            }

            return string.Equals(customShaderValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether one field belongs to the shader override block that stays hidden in standard mode.
        /// </summary>
        /// <param name="field">Field definition to evaluate.</param>
        /// <returns>True when the field should only be shown while custom shader mode is enabled.</returns>
        bool IsShaderOverrideField(PlatformMaterialFieldDefinition field) {
            return string.Equals(field.FieldId, ShaderAssetIdFieldId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.FieldId, VertexProgramFieldId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.FieldId, PixelProgramFieldId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the entry represents a shader source file.
        /// </summary>
        /// <param name="entry">Asset entry to evaluate.</param>
        /// <returns>True when the entry is a shader source file.</returns>
        bool IsShaderEntry(AssetBrowserEntry entry) {
            if (entry == null) {
                return false;
            }

            return string.Equals(entry.Extension, EditorFileTemplateRegistry.ShaderExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Presents editable material asset options inside the properties panel.
    /// </summary>
    public class MaterialAssetView {
        /// <summary>
        /// Height of the shader picker row.
        /// </summary>
        const int RowHeight = 24;
        /// <summary>
        /// Spacing between label, value, and button elements.
        /// </summary>
        const int ControlSpacing = 8;
        /// <summary>
        /// Width reserved for the shader label.
        /// </summary>
        const int LabelWidth = 72;
        /// <summary>
        /// Width reserved for the pick button.
        /// </summary>
        const int ButtonWidth = 80;
        /// <summary>
        /// Text shown when no shader is assigned.
        /// </summary>
        const string EmptyShaderLabel = "None";
        /// <summary>
        /// Label text for the shader picker row.
        /// </summary>
        const string ShaderLabelText = "Shader";

        /// <summary>
        /// Font used for text elements.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Root entity that owns view visuals.
        /// </summary>
        readonly EditorEntity RootEntity;
        /// <summary>
        /// Host entity for the shader label.
        /// </summary>
        readonly EditorEntity LabelHost;
        /// <summary>
        /// Text component for the shader label.
        /// </summary>
        readonly TextComponent LabelText;
        /// <summary>
        /// Host entity for the shader value text.
        /// </summary>
        readonly EditorEntity ValueHost;
        /// <summary>
        /// Text component for the shader value.
        /// </summary>
        readonly TextComponent ValueText;
        /// <summary>
        /// Host entity for the shader pick button.
        /// </summary>
        readonly EditorEntity PickHost;
        /// <summary>
        /// Button used to open the shader picker.
        /// </summary>
        readonly ButtonComponent PickButton;
        /// <summary>
        /// Currently selected asset entry.
        /// </summary>
        AssetBrowserEntry CurrentEntry;
        /// <summary>
        /// Currently loaded material asset.
        /// </summary>
        MaterialAsset CurrentAsset;
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
        public MaterialAssetView(FontAsset font, ushort layerMask) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            Font = font;
            TextOrder = RenderOrder2D.PanelForeground;

            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.InternalEntity = true;

            LabelHost = new EditorEntity();
            LabelHost.LayerMask = layerMask;
            RootEntity.AddChild(LabelHost);

            LabelText = new TextComponent();
            LabelText.Font = font;
            LabelText.Text = ShaderLabelText;
            LabelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            LabelText.RenderOrder2D = TextOrder;
            LabelHost.AddComponent(LabelText);

            ValueHost = new EditorEntity();
            ValueHost.LayerMask = layerMask;
            RootEntity.AddChild(ValueHost);

            ValueText = new TextComponent();
            ValueText.Font = font;
            ValueText.Text = string.Empty;
            ValueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            ValueText.RenderOrder2D = TextOrder;
            ValueHost.AddComponent(ValueText);

            PickHost = new EditorEntity();
            PickHost.LayerMask = layerMask;
            RootEntity.AddChild(PickHost);

            PickButton = new ButtonComponent("Pick", new int2(ButtonWidth, RowHeight), font, RequestShaderPick);
            PickHost.AddComponent(PickButton);

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
        /// Shows the view for the specified material asset.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="materialAsset">Material asset to edit.</param>
        public void Show(AssetBrowserEntry entry, MaterialAsset materialAsset) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            CurrentEntry = entry;
            CurrentAsset = materialAsset;
            UpdateShaderLabel();
            IsViewVisible = true;
            RootEntity.Enabled = true;
        }

        /// <summary>
        /// Hides the view and clears the current material state.
        /// </summary>
        public void Hide() {
            IsViewVisible = false;
            RootEntity.Enabled = false;
            CurrentEntry = null;
            CurrentAsset = null;
            ValueText.Text = string.Empty;
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
            int labelWidth = Math.Min(LabelWidth, safeWidth);
            int buttonWidth = Math.Min(ButtonWidth, safeWidth);
            int valueWidth = Math.Max(0, safeWidth - labelWidth - buttonWidth - (ControlSpacing * 2));

            LabelHost.Position = new float3(left, top, 0.2f);
            LabelText.Size = new int2(labelWidth, RowHeight);

            int valueLeft = left + labelWidth + ControlSpacing;
            ValueHost.Position = new float3(valueLeft, top, 0.2f);
            ValueText.Size = new int2(valueWidth, RowHeight);

            int buttonLeft = left + safeWidth - buttonWidth;
            PickHost.Position = new float3(buttonLeft, top, 0.2f);

            LayoutHeight = RowHeight;
        }

        /// <summary>
        /// Requests a shader pick from the asset picker service.
        /// </summary>
        void RequestShaderPick() {
            if (CurrentEntry == null || CurrentAsset == null) {
                return;
            }

            EditorAssetPickerService.RequestPick(HandleShaderPicked, EditorFileTemplateRegistry.ShaderExtension);
        }

        /// <summary>
        /// Handles shader selections from the asset picker.
        /// </summary>
        /// <param name="entry">Picked asset entry.</param>
        void HandleShaderPicked(AssetBrowserEntry entry) {
            if (entry == null || CurrentEntry == null || CurrentAsset == null) {
                return;
            }

            if (entry.IsDirectory) {
                return;
            }

            if (!IsShaderEntry(entry)) {
                return;
            }

            try {
                string shaderId = ShaderAssetIdUtils.BuildShaderAssetId(entry.FullPath);
                ApplyShaderId(shaderId);
                SaveMaterialAsset(CurrentEntry.FullPath, CurrentAsset);
                UpdateShaderLabel();
                RefreshShaderResources(shaderId);
            } catch (Exception ex) {
                Logger.WriteError($"Failed to assign shader: {ex.Message}");
            }
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
        /// Updates the material asset fields based on a shader identifier.
        /// </summary>
        /// <param name="shaderId">Shader identifier to apply.</param>
        void ApplyShaderId(string shaderId) {
            if (CurrentAsset == null) {
                return;
            }

            CurrentAsset.ShaderAssetId = shaderId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(shaderId)) {
                CurrentAsset.VertexProgram = string.Empty;
                CurrentAsset.PixelProgram = string.Empty;
                CurrentAsset.Variant = string.Empty;
                return;
            }

            CurrentAsset.VertexProgram = string.Concat(shaderId, ".vs");
            CurrentAsset.PixelProgram = string.Concat(shaderId, ".ps");
            CurrentAsset.Variant = "default";
        }

        /// <summary>
        /// Saves the material asset back to disk.
        /// </summary>
        /// <param name="path">Material asset file path.</param>
        /// <param name="materialAsset">Material asset to serialize.</param>
        void SaveMaterialAsset(string path, MaterialAsset materialAsset) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, materialAsset);
            }
        }

        /// <summary>
        /// Updates the shader label text for the current material asset.
        /// </summary>
        void UpdateShaderLabel() {
            if (CurrentAsset == null) {
                ValueText.Text = string.Empty;
                return;
            }

            string shaderId = CurrentAsset.ShaderAssetId;
            ValueText.Text = string.IsNullOrWhiteSpace(shaderId) ? EmptyShaderLabel : shaderId;
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

            string extension = entry.Extension;
            return string.Equals(extension, EditorFileTemplateRegistry.ShaderExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Loads the default viewport toolbar PNGs and converts them into runtime textures.
    /// </summary>
    public static class EditorToolbarIconLoader {
        /// <summary>
        /// Output-relative directory that stores the toolbar icon PNGs.
        /// </summary>
        static readonly string ToolbarIconDirectory = Path.Combine(AppContext.BaseDirectory, "content", "icons", "toolbar");
        /// <summary>
        /// File path for the translate toolbar icon.
        /// </summary>
        static readonly string TranslateIconPath = Path.Combine(ToolbarIconDirectory, "transform.png");
        /// <summary>
        /// File path for the rotate toolbar icon.
        /// </summary>
        static readonly string RotateIconPath = Path.Combine(ToolbarIconDirectory, "rotate.png");
        /// <summary>
        /// File path for the scale toolbar icon.
        /// </summary>
        static readonly string ScaleIconPath = Path.Combine(ToolbarIconDirectory, "scale.png");
        /// <summary>
        /// File path for the snap increase toolbar icon.
        /// </summary>
        static readonly string SnapIncreaseIconPath = Path.Combine(ToolbarIconDirectory, "snap-increase.png");
        /// <summary>
        /// File path for the snap decrease toolbar icon.
        /// </summary>
        static readonly string SnapDecreaseIconPath = Path.Combine(ToolbarIconDirectory, "snap-decrease.png");
        /// <summary>
        /// File path for the snap magnet toolbar label icon.
        /// </summary>
        static readonly string MagnetIconPath = Path.Combine(ToolbarIconDirectory, "magnet.png");
        /// <summary>
        /// File path for the control-key toolbar label icon.
        /// </summary>
        static readonly string CtrlKeyIconPath = Path.Combine(ToolbarIconDirectory, "key-ctrl.png");
        /// <summary>
        /// File path for the shift-key toolbar label icon.
        /// </summary>
        static readonly string ShiftKeyIconPath = Path.Combine(ToolbarIconDirectory, "key-shift.png");

        /// <summary>
        /// Loads the default toolbar icon set used by the editor viewport.
        /// </summary>
        /// <returns>Runtime texture set that can be assigned to the viewport toolbar.</returns>
        public static EditorViewportToolbarIconSet LoadDefaultToolbarIcons() {
            GDITextureImporter importer = new GDITextureImporter();
            RuntimeTexture translateIcon = LoadTexture(importer, TranslateIconPath);
            RuntimeTexture rotateIcon = LoadTexture(importer, RotateIconPath);
            RuntimeTexture scaleIcon = LoadTexture(importer, ScaleIconPath);
            RuntimeTexture snapIncreaseIcon = LoadTexture(importer, SnapIncreaseIconPath);
            RuntimeTexture snapDecreaseIcon = LoadTexture(importer, SnapDecreaseIconPath);
            RuntimeTexture magnetIcon = LoadTexture(importer, MagnetIconPath);
            RuntimeTexture ctrlKeyIcon = LoadTexture(importer, CtrlKeyIconPath);
            RuntimeTexture shiftKeyIcon = LoadTexture(importer, ShiftKeyIconPath);
            return new EditorViewportToolbarIconSet(
                translateIcon,
                rotateIcon,
                scaleIcon,
                snapIncreaseIcon,
                snapDecreaseIcon,
                magnetIcon,
                ctrlKeyIcon,
                shiftKeyIcon);
        }

        /// <summary>
        /// Loads one PNG file from disk and uploads it into the active 2D renderer.
        /// </summary>
        /// <param name="importer">Texture importer used to decode PNG data.</param>
        /// <param name="filePath">Absolute file path to the PNG file.</param>
        /// <returns>Renderer-owned runtime texture built from the decoded image.</returns>
        static RuntimeTexture LoadTexture(GDITextureImporter importer, string filePath) {
            if (importer == null) {
                throw new ArgumentNullException(nameof(importer));
            }
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Toolbar icon path must be provided.", nameof(filePath));
            }
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException("Toolbar icon PNG was not found.", filePath);
            }

            using FileStream stream = File.OpenRead(filePath);
            TextureAsset textureAsset = importer.ImportTexture(stream);
            return Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
        }
    }
}

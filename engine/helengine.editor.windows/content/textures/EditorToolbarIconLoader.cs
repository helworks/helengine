namespace helengine.editor {
    /// <summary>
    /// Loads the default viewport toolbar PNGs and converts them into runtime textures.
    /// </summary>
    public static class EditorToolbarIconLoader {
        /// <summary>
        /// Shared PNG content processor used to decode built-in editor toolbar icons without mutating the shared content manager configuration.
        /// </summary>
        static readonly TextureImporterContentProcessor ToolbarIconContentProcessor = new TextureImporterContentProcessor(new GDITextureImporter());

        /// <summary>
        /// Root-relative path for the translate toolbar icon.
        /// </summary>
        static readonly string TranslateIconPath = Path.Combine("content", "icons", "toolbar", "transform.png");
        /// <summary>
        /// Root-relative path for the rotate toolbar icon.
        /// </summary>
        static readonly string RotateIconPath = Path.Combine("content", "icons", "toolbar", "rotate.png");
        /// <summary>
        /// Root-relative path for the scale toolbar icon.
        /// </summary>
        static readonly string ScaleIconPath = Path.Combine("content", "icons", "toolbar", "scale.png");
        /// <summary>
        /// Root-relative path for the viewport grid toolbar icon.
        /// </summary>
        static readonly string GridIconPath = Path.Combine("content", "icons", "toolbar", "grid.png");
        /// <summary>
        /// Root-relative path for the snap increase toolbar icon.
        /// </summary>
        static readonly string SnapIncreaseIconPath = Path.Combine("content", "icons", "toolbar", "snap-increase.png");
        /// <summary>
        /// Root-relative path for the snap decrease toolbar icon.
        /// </summary>
        static readonly string SnapDecreaseIconPath = Path.Combine("content", "icons", "toolbar", "snap-decrease.png");
        /// <summary>
        /// Root-relative path for the snap magnet toolbar label icon.
        /// </summary>
        static readonly string MagnetIconPath = Path.Combine("content", "icons", "toolbar", "magnet.png");
        /// <summary>
        /// Root-relative path for the control-key toolbar label icon.
        /// </summary>
        static readonly string CtrlKeyIconPath = Path.Combine("content", "icons", "toolbar", "key-ctrl.png");
        /// <summary>
        /// Root-relative path for the shift-key toolbar label icon.
        /// </summary>
        static readonly string ShiftKeyIconPath = Path.Combine("content", "icons", "toolbar", "key-shift.png");
        /// <summary>
        /// Root-relative path for the editor title-bar icon.
        /// </summary>
        static readonly string TitleBarIconPath = Path.Combine("content", "icons", "titlebar", "helengine_icon.png");

        /// <summary>
        /// Loads the default toolbar icon set used by the editor viewport.
        /// </summary>
        /// <param name="content">Content manager used to resolve and parse the icon files.</param>
        /// <param name="applicationRootPath">Absolute application root path used to resolve built-in editor content.</param>
        /// <returns>Runtime texture set that can be assigned to the viewport toolbar.</returns>
        public static EditorViewportToolbarIconSet LoadDefaultToolbarIcons(ContentManager content, string applicationRootPath) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }
            if (string.IsNullOrWhiteSpace(applicationRootPath)) {
                throw new ArgumentException("Application root path must be provided.", nameof(applicationRootPath));
            }

            RuntimeTexture translateIcon = LoadTexture(content, applicationRootPath, TranslateIconPath);
            RuntimeTexture rotateIcon = LoadTexture(content, applicationRootPath, RotateIconPath);
            RuntimeTexture scaleIcon = LoadTexture(content, applicationRootPath, ScaleIconPath);
            RuntimeTexture gridIcon = LoadTexture(content, applicationRootPath, GridIconPath);
            RuntimeTexture snapIncreaseIcon = LoadTexture(content, applicationRootPath, SnapIncreaseIconPath);
            RuntimeTexture snapDecreaseIcon = LoadTexture(content, applicationRootPath, SnapDecreaseIconPath);
            RuntimeTexture magnetIcon = LoadTexture(content, applicationRootPath, MagnetIconPath);
            RuntimeTexture ctrlKeyIcon = LoadTexture(content, applicationRootPath, CtrlKeyIconPath);
            RuntimeTexture shiftKeyIcon = LoadTexture(content, applicationRootPath, ShiftKeyIconPath);
            return new EditorViewportToolbarIconSet(
                translateIcon,
                rotateIcon,
                scaleIcon,
                gridIcon,
                snapIncreaseIcon,
                snapDecreaseIcon,
                magnetIcon,
                ctrlKeyIcon,
                shiftKeyIcon);
        }

        /// <summary>
        /// Loads the editor application icon used in the top-left title-bar slot.
        /// </summary>
        /// <param name="content">Content manager used to resolve and parse the icon file.</param>
        /// <param name="applicationRootPath">Absolute application root path used to resolve built-in editor content.</param>
        /// <returns>Runtime texture that can be assigned to the title-bar icon slot.</returns>
        public static RuntimeTexture LoadTitleBarIcon(ContentManager content, string applicationRootPath) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }
            if (string.IsNullOrWhiteSpace(applicationRootPath)) {
                throw new ArgumentException("Application root path must be provided.", nameof(applicationRootPath));
            }

            return LoadTexture(content, applicationRootPath, TitleBarIconPath);
        }

        /// <summary>
        /// Loads one PNG file from disk and uploads it into the active 2D renderer.
        /// </summary>
        /// <param name="content">Content manager used to decode the PNG data.</param>
        /// <param name="applicationRootPath">Absolute application root path used to resolve built-in editor content.</param>
        /// <param name="filePath">Application-relative file path to the PNG file.</param>
        /// <returns>Renderer-owned runtime texture built from the decoded image.</returns>
        static RuntimeTexture LoadTexture(ContentManager content, string applicationRootPath, string filePath) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }
            if (string.IsNullOrWhiteSpace(applicationRootPath)) {
                throw new ArgumentException("Application root path must be provided.", nameof(applicationRootPath));
            }
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Toolbar icon path must be provided.", nameof(filePath));
            }

            string absoluteFilePath = ResolveApplicationContentPath(applicationRootPath, filePath);
            TextureAsset textureAsset = content.Load(absoluteFilePath, ToolbarIconContentProcessor);
            return Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
        }

        /// <summary>
        /// Resolves one built-in editor content path relative to the application root.
        /// </summary>
        /// <param name="applicationRootPath">Absolute application root path used to resolve built-in editor content.</param>
        /// <param name="filePath">Application-relative file path.</param>
        /// <returns>Absolute file path for the requested built-in content.</returns>
        static string ResolveApplicationContentPath(string applicationRootPath, string filePath) {
            if (string.IsNullOrWhiteSpace(applicationRootPath)) {
                throw new ArgumentException("Application root path must be provided.", nameof(applicationRootPath));
            }
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("Toolbar icon path must be provided.", nameof(filePath));
            }

            return Path.GetFullPath(Path.Combine(applicationRootPath, filePath));
        }
    }
}

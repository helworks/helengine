namespace helengine.editor {
    /// <summary>
    /// Loads the default viewport toolbar PNGs and converts them into runtime textures.
    /// </summary>
    public static class EditorToolbarIconLoader {
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
        /// Root-relative path for the viewport settings toolbar icon.
        /// </summary>
        static readonly string SettingsIconPath = Path.Combine("content", "icons", "toolbar", "settings.png");
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
            RuntimeTexture settingsIcon = LoadTexture(content, applicationRootPath, SettingsIconPath);
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
                settingsIcon,
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
            TextureAsset textureAsset = LoadToolbarTextureAsset(absoluteFilePath);
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

        /// <summary>
        /// Loads one built-in toolbar texture from disk without routing through the asset importer pipeline.
        /// </summary>
        /// <param name="absoluteFilePath">Absolute file path to the built-in PNG file.</param>
        /// <returns>Decoded texture asset in RGBA byte order.</returns>
        static TextureAsset LoadToolbarTextureAsset(string absoluteFilePath) {
            if (string.IsNullOrWhiteSpace(absoluteFilePath)) {
                throw new ArgumentException("Toolbar icon path must be provided.", nameof(absoluteFilePath));
            }

            using System.Drawing.Bitmap sourceBitmap = new System.Drawing.Bitmap(absoluteFilePath);
            int width = sourceBitmap.Width;
            int height = sourceBitmap.Height;
            if (width > ushort.MaxValue || height > ushort.MaxValue) {
                throw new InvalidOperationException("Texture dimensions exceed supported limits.");
            }

            System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(0, 0, width, height);
            using System.Drawing.Bitmap bitmap = sourceBitmap.Clone(bounds, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(bounds, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try {
                int bytesPerPixel = 4;
                int rowLength = width * bytesPerPixel;
                byte[] colors = new byte[width * height * bytesPerPixel];
                byte[] rowData = new byte[rowLength];

                for (int y = 0; y < height; y++) {
                    IntPtr rowPointer = IntPtr.Add(data.Scan0, y * data.Stride);
                    System.Runtime.InteropServices.Marshal.Copy(rowPointer, rowData, 0, rowLength);

                    int rowOffset = y * rowLength;
                    for (int x = 0; x < width; x++) {
                        int sourceIndex = x * bytesPerPixel;
                        int destinationIndex = rowOffset + sourceIndex;
                        colors[destinationIndex] = rowData[sourceIndex + 2];
                        colors[destinationIndex + 1] = rowData[sourceIndex + 1];
                        colors[destinationIndex + 2] = rowData[sourceIndex];
                        colors[destinationIndex + 3] = rowData[sourceIndex + 3];
                    }
                }

                return new TextureAsset {
                    Width = (ushort)width,
                    Height = (ushort)height,
                    Colors = colors
                };
            } finally {
                bitmap.UnlockBits(data);
            }
        }
    }
}

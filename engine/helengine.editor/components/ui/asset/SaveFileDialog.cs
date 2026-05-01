namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to select a filesystem folder and save one scene file under the project assets folder.
    /// </summary>
    public class SaveFileDialog : EditorEntity {
        /// <summary>
        /// Default minimum width for the dialog panel.
        /// </summary>
        public const int MinPanelWidth = 420;
        /// <summary>
        /// Default minimum height for the dialog panel.
        /// </summary>
        public const int MinPanelHeight = 360;
        /// <summary>
        /// Default maximum width for the dialog panel.
        /// </summary>
        public const int MaxPanelWidth = 920;
        /// <summary>
        /// Default maximum height for the dialog panel.
        /// </summary>
        public const int MaxPanelHeight = 760;
        /// <summary>
        /// Padding applied inside the dialog panel.
        /// </summary>
        public const int PanelPadding = 16;
        /// <summary>
        /// Height of the draggable header area.
        /// </summary>
        public const int HeaderHeight = 32;
        /// <summary>
        /// Height of the file-name label row.
        /// </summary>
        public const int FileNameLabelHeight = 18;
        /// <summary>
        /// Height of the file-name input row.
        /// </summary>
        public const int FileNameFieldHeight = 22;
        /// <summary>
        /// Height of the footer button row.
        /// </summary>
        public const int FooterHeight = 28;
        /// <summary>
        /// Vertical spacing between dialog sections.
        /// </summary>
        public const int SectionSpacing = 10;
        /// <summary>
        /// Radius used for the dialog background.
        /// </summary>
        const float PanelRadius = 6f;
        /// <summary>
        /// Border thickness used for the dialog background.
        /// </summary>
        const float PanelBorderThickness = 2f;
        /// <summary>
        /// Render order used by the fullscreen modal backdrop behind the panel.
        /// </summary>
        const byte BackdropOrder = RenderOrder2D.ModalBackground - 1;
        /// <summary>
        /// Width reserved on the right side of the host title bar so the window buttons stay interactive.
        /// </summary>
        const int HostTitleBarButtonGapWidth = EditorTitleBar.HeightPixels * 4;
        /// <summary>
        /// Horizontal padding inside the header.
        /// </summary>
        const int HeaderPadding = 8;
        /// <summary>
        /// Fixed size used for the cancel button.
        /// </summary>
        static readonly int2 CancelButtonSize = new int2(88, 22);
        /// <summary>
        /// Fixed size used for the save button.
        /// </summary>
        static readonly int2 SaveButtonSize = new int2(88, 22);

        /// <summary>
        /// Font used for dialog labels and buttons.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Path resolver used to validate scene save destinations.
        /// </summary>
        readonly SceneSavePathResolver PathResolver;
        /// <summary>
        /// Root entity hosting the fullscreen modal backdrop.
        /// </summary>
        readonly EditorEntity BackdropRoot;
        /// <summary>
        /// Root entity hosting the title-bar backdrop strip.
        /// </summary>
        readonly EditorEntity BackdropTopRoot;
        /// <summary>
        /// Dimming surface rendered across the title-bar area while leaving the window buttons free.
        /// </summary>
        readonly SpriteComponent BackdropTopSurface;
        /// <summary>
        /// Interactable that absorbs pointer input over the title-bar backdrop strip.
        /// </summary>
        readonly InteractableComponent BackdropTopInteractable;
        /// <summary>
        /// Root entity hosting the editor-content backdrop block.
        /// </summary>
        readonly EditorEntity BackdropBodyRoot;
        /// <summary>
        /// Fullscreen dimming surface rendered behind the dialog panel.
        /// </summary>
        readonly SpriteComponent BackdropBodySurface;
        /// <summary>
        /// Fullscreen interactable that absorbs pointer input outside the panel.
        /// </summary>
        readonly InteractableComponent BackdropBodyInteractable;
        /// <summary>
        /// Root entity hosting the panel content.
        /// </summary>
        readonly EditorEntity PanelRoot;
        /// <summary>
        /// Panel background shape.
        /// </summary>
        readonly RoundedRectComponent PanelBackground;
        /// <summary>
        /// Root entity for the draggable header area.
        /// </summary>
        readonly EditorEntity HeaderRoot;
        /// <summary>
        /// Header background sprite.
        /// </summary>
        readonly SpriteComponent HeaderBackground;
        /// <summary>
        /// Header drag interactable.
        /// </summary>
        readonly InteractableComponent HeaderInteractable;
        /// <summary>
        /// Header text host entity.
        /// </summary>
        readonly EditorEntity HeaderHost;
        /// <summary>
        /// Header title text.
        /// </summary>
        readonly TextComponent HeaderText;
        /// <summary>
        /// Shared asset browser view used in filesystem-only mode.
        /// </summary>
        readonly AssetBrowserView BrowserView;
        /// <summary>
        /// Host entity for the file name label.
        /// </summary>
        readonly EditorEntity FileNameLabelHost;
        /// <summary>
        /// File name label text.
        /// </summary>
        readonly TextComponent FileNameLabel;
        /// <summary>
        /// Host entity for the file name input box.
        /// </summary>
        readonly EditorEntity FileNameFieldHost;
        /// <summary>
        /// File name input box used to type the target scene name.
        /// </summary>
        readonly TextBoxComponent FileNameField;
        /// <summary>
        /// Host entity for the validation/error text.
        /// </summary>
        readonly EditorEntity StatusHost;
        /// <summary>
        /// Status or validation error text.
        /// </summary>
        readonly TextComponent StatusText;
        /// <summary>
        /// Host entity for the cancel button.
        /// </summary>
        readonly EditorEntity CancelButtonHost;
        /// <summary>
        /// Cancel button component.
        /// </summary>
        readonly ButtonComponent CancelButton;
        /// <summary>
        /// Host entity for the save button.
        /// </summary>
        readonly EditorEntity SaveButtonHost;
        /// <summary>
        /// Save button component.
        /// </summary>
        readonly ButtonComponent SaveButton;
        /// <summary>
        /// Render order used for panel backgrounds.
        /// </summary>
        readonly byte PanelOrder;
        /// <summary>
        /// Render order used for text labels.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Cached panel size used for layout.
        /// </summary>
        int2 PanelSize;
        /// <summary>
        /// Cached panel position relative to the host window.
        /// </summary>
        int2 PanelPosition;
        /// <summary>
        /// Cached host size for clamping.
        /// </summary>
        int2 HostSize;
        /// <summary>
        /// True when the user has positioned the dialog manually.
        /// </summary>
        bool IsUserPositioned;
        /// <summary>
        /// True when the header is actively being dragged.
        /// </summary>
        bool IsDragging;
        /// <summary>
        /// Tracks whether the dialog has completed initialization.
        /// </summary>
        bool IsInitialized;

        /// <summary>
        /// Raised when the user confirms a valid save path.
        /// </summary>
        public event Action<string> SaveRequested;

        /// <summary>
        /// Initializes a new save-file dialog rooted at the project assets folder.
        /// </summary>
        /// <param name="font">Font used for labels and buttons.</param>
        /// <param name="projectPath">Project root that owns the assets folder.</param>
        public SaveFileDialog(FontAsset font, string projectPath) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            Font = font;
            PathResolver = new SceneSavePathResolver(projectPath);
            PanelSize = new int2(MinPanelWidth, MinPanelHeight);

            LayerMask = 0b1000000000000000;
            InternalEntity = true;
            Name = "SaveFileDialog";

            PanelOrder = RenderOrder2D.ModalBackground;
            byte toolbarOrder = RenderOrder2D.ModalBackground;
            byte rowBackgroundOrder = RenderOrder2D.ModalBackground;
            byte iconBackgroundOrder = RenderOrder2D.ModalBackground;
            TextOrder = RenderOrder2D.ModalForeground;

            BackdropRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            AddChild(BackdropRoot);

            BackdropTopRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BackdropRoot.AddChild(BackdropTopRoot);

            BackdropTopSurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 144),
                RenderOrder2D = BackdropOrder,
                Size = new int2(0, 0)
            };
            BackdropTopRoot.AddComponent(BackdropTopSurface);

            BackdropTopInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackdropTopRoot.AddComponent(BackdropTopInteractable);

            BackdropBodyRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            BackdropRoot.AddChild(BackdropBodyRoot);

            BackdropBodySurface = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = new byte4(0, 0, 0, 144),
                RenderOrder2D = BackdropOrder,
                Size = new int2(0, 0)
            };
            BackdropBodyRoot.AddComponent(BackdropBodySurface);

            BackdropBodyInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            BackdropBodyRoot.AddComponent(BackdropBodyInteractable);

            PanelRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            AddChild(PanelRoot);

            PanelBackground = new RoundedRectComponent {
                FillColor = ThemeManager.Colors.SurfacePrimary,
                BorderColor = ThemeManager.Colors.AccentTertiary,
                BorderThickness = PanelBorderThickness,
                Radius = PanelRadius,
                RenderOrder2D = PanelOrder,
                Size = new int2(0, 0)
            };
            PanelRoot.AddComponent(PanelBackground);

            HeaderRoot = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(HeaderRoot);

            HeaderBackground = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.SurfacePrimary,
                RenderOrder2D = PanelOrder,
                Size = new int2(0, 0)
            };
            HeaderRoot.AddComponent(HeaderBackground);

            HeaderInteractable = new InteractableComponent {
                Size = new int2(0, 0)
            };
            HeaderInteractable.CursorEvent += HandleHeaderCursor;
            HeaderRoot.AddComponent(HeaderInteractable);

            HeaderHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            HeaderRoot.AddChild(HeaderHost);

            HeaderText = new TextComponent {
                Font = font,
                Text = "Save Scene",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            HeaderHost.AddComponent(HeaderText);

            BrowserView = new AssetBrowserView(
                Font,
                projectPath,
                LayerMask,
                toolbarOrder,
                rowBackgroundOrder,
                iconBackgroundOrder,
                TextOrder,
                false);
            BrowserView.SetToolbarButtonRenderOrders(TextOrder, TextOrder);
            BrowserView.AssetActivated += HandleAssetActivated;
            PanelRoot.AddChild(BrowserView.Entity);

            FileNameLabelHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(FileNameLabelHost);

            FileNameLabel = new TextComponent {
                Font = font,
                Text = "File Name",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            FileNameLabelHost.AddComponent(FileNameLabel);

            FileNameFieldHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(FileNameFieldHost);

            FileNameField = new TextBoxComponent(new int2(220, FileNameFieldHeight), font, "Scene Name");
            FileNameFieldHost.AddComponent(FileNameField);

            StatusHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(StatusHost);

            StatusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                Size = new int2(1, Math.Max(1, (int)Math.Ceiling(Math.Max(font.LineHeight, 1f)))),
                RenderOrder2D = TextOrder
            };
            StatusHost.AddComponent(StatusText);

            CancelButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(CancelButtonHost);

            CancelButton = new ButtonComponent("Cancel", CancelButtonSize, font, Hide, 0f);
            CancelButtonHost.AddComponent(CancelButton);
            CancelButton.SetRenderOrders(TextOrder, TextOrder);

            SaveButtonHost = new EditorEntity {
                LayerMask = LayerMask,
                Position = float3.Zero
            };
            PanelRoot.AddChild(SaveButtonHost);

            SaveButton = new ButtonComponent("Save", SaveButtonSize, font, HandleSaveClicked, 0f);
            SaveButtonHost.AddComponent(SaveButton);
            SaveButton.SetRenderOrders(TextOrder, TextOrder);

            Enabled = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Gets a value indicating whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => Enabled;

        /// <summary>
        /// Shows the dialog and prepares its initial directory and suggested file name.
        /// </summary>
        /// <param name="initialRelativeDirectory">Relative directory to navigate to initially.</param>
        /// <param name="suggestedFileName">Suggested file name to prefill in the text box.</param>
        public void Show(string initialRelativeDirectory, string suggestedFileName) {
            IsUserPositioned = false;
            IsDragging = false;
            Enabled = true;
            StatusText.Text = string.Empty;
            if (!BrowserView.TryNavigateTo(initialRelativeDirectory)) {
                BrowserView.TryNavigateTo(SceneSavePathResolver.DefaultSceneDirectory);
            }
            FileNameField.Text = suggestedFileName ?? string.Empty;
            BrowserView.RefreshEntries();
        }

        /// <summary>
        /// Hides the dialog and clears its input-capture blocker.
        /// </summary>
        public void Hide() {
            IsUserPositioned = false;
            IsDragging = false;
            HideBackdrop();
            Enabled = false;
        }

        /// <summary>
        /// Shows one validation or save error in the dialog footer.
        /// </summary>
        /// <param name="message">Validation or save error to display.</param>
        public void ShowError(string message) {
            StatusText.Text = message ?? string.Empty;
        }

        /// <summary>
        /// Updates dialog sizing and layout to fit the provided window dimensions.
        /// </summary>
        /// <param name="windowWidth">Current window width.</param>
        /// <param name="windowHeight">Current window height.</param>
        public void UpdateLayout(int windowWidth, int windowHeight) {
            if (!IsInitialized) {
                return;
            }
            if (!Enabled) {
                HideBackdrop();
                return;
            }

            int width = Math.Max(1, windowWidth);
            int height = Math.Max(1, windowHeight);
            HostSize = new int2(width, height);

            int maxWidth = Math.Max(MinPanelWidth, width - PanelPadding * 2);
            int maxHeight = Math.Max(MinPanelHeight, height - PanelPadding * 2);
            int panelWidth = Math.Min(MaxPanelWidth, maxWidth);
            int panelHeight = Math.Min(MaxPanelHeight, maxHeight);
            panelWidth = Math.Min(panelWidth, width);
            panelHeight = Math.Min(panelHeight, height);

            PanelSize = new int2(panelWidth, panelHeight);
            PanelBackground.Size = PanelSize;
            UpdateBackdrop();
            if (!IsUserPositioned) {
                PanelPosition = new int2(Math.Max(0, (width - panelWidth) / 2), Math.Max(0, (height - panelHeight) / 2));
            }

            ClampPanelPosition();
            ApplyPanelPosition();

            LayoutHeader(panelWidth);
            LayoutBrowser(panelWidth, panelHeight);
            LayoutFileName(panelWidth, panelHeight);
            LayoutFooter(panelWidth, panelHeight);
        }

        /// <summary>
        /// Hides the backdrop geometry when the dialog is not visible.
        /// </summary>
        void HideBackdrop() {
            BackdropTopSurface.Size = new int2(0, 0);
            BackdropTopInteractable.Size = new int2(0, 0);
            BackdropBodySurface.Size = new int2(0, 0);
            BackdropBodyInteractable.Size = new int2(0, 0);
        }

        /// <summary>
        /// Updates the backdrop geometry so the host title-bar buttons remain clickable.
        /// </summary>
        void UpdateBackdrop() {
            int topWidth = Math.Max(0, HostSize.X - HostTitleBarButtonGapWidth);
            BackdropTopRoot.Position = float3.Zero;
            BackdropTopSurface.Size = new int2(topWidth, EditorTitleBar.HeightPixels);
            BackdropTopInteractable.Size = new int2(topWidth, EditorTitleBar.HeightPixels);
            BackdropBodyRoot.Position = new float3(0f, EditorTitleBar.HeightPixels, 0f);
            int bodyHeight = Math.Max(0, HostSize.Y - EditorTitleBar.HeightPixels);
            BackdropBodySurface.Size = new int2(HostSize.X, bodyHeight);
            BackdropBodyInteractable.Size = new int2(HostSize.X, bodyHeight);
        }

        /// <summary>
        /// Handles the save button by validating the path and raising the save request when successful.
        /// </summary>
        void HandleSaveClicked() {
            try {
                string fullPath = PathResolver.BuildFullPath(BrowserView.CurrentDirectoryPath, FileNameField.Text);
                StatusText.Text = string.Empty;
                SaveRequested?.Invoke(fullPath);
            } catch (Exception ex) {
                StatusText.Text = ex.Message;
            }
        }

        /// <summary>
        /// Handles file activation from the browser by prefilling the file name text box.
        /// </summary>
        /// <param name="entry">Activated browser entry.</param>
        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null || entry.IsDirectory) {
                return;
            }

            FileNameField.Text = Path.GetFileNameWithoutExtension(entry.Name);
        }

        /// <summary>
        /// Handles pointer interaction on the dialog header to allow dragging.
        /// </summary>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="delta">Pointer movement delta.</param>
        /// <param name="state">Pointer interaction state.</param>
        void HandleHeaderCursor(int2 pos, int2 delta, PointerInteraction state) {
            switch (state) {
                case PointerInteraction.Press:
                    IsDragging = true;
                    IsUserPositioned = true;
                    break;
                case PointerInteraction.Hover:
                    if (IsDragging) {
                        PanelPosition = new int2(PanelPosition.X + delta.X, PanelPosition.Y + delta.Y);
                        ClampPanelPosition();
                        ApplyPanelPosition();
                    }
                    break;
                case PointerInteraction.Release:
                case PointerInteraction.Leave:
                    IsDragging = false;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Updates header placement within the dialog panel.
        /// </summary>
        /// <param name="panelWidth">Panel width used for layout.</param>
        void LayoutHeader(int panelWidth) {
            int headerWidth = Math.Max(0, panelWidth - PanelPadding * 2);
            HeaderRoot.Position = new float3(PanelPadding, PanelPadding, 0.2f);
            HeaderBackground.Size = new int2(headerWidth, HeaderHeight);
            HeaderInteractable.Size = new int2(headerWidth, HeaderHeight);

            FontTightMetrics headerMetrics = Font.MeasureTight(HeaderText.Text);
            HeaderHost.Position = new float3(HeaderPadding, GetTextTopOffset(HeaderHeight, headerMetrics), 0.2f);
            HeaderText.Size = new int2(Math.Max(1, headerWidth - HeaderPadding * 2), (int)Math.Ceiling(headerMetrics.Height));
        }

        /// <summary>
        /// Updates browser placement within the dialog panel.
        /// </summary>
        /// <param name="panelWidth">Panel width used for layout.</param>
        /// <param name="panelHeight">Panel height used for layout.</param>
        void LayoutBrowser(int panelWidth, int panelHeight) {
            int browserWidth = Math.Max(0, panelWidth - PanelPadding * 2);
            int browserTop = PanelPadding + HeaderHeight + SectionSpacing;
            int footerTop = panelHeight - PanelPadding - FooterHeight;
            int browserBottom = footerTop - SectionSpacing - FileNameLabelHeight - FileNameFieldHeight - SectionSpacing - Math.Max(FileNameLabelHeight, (int)Math.Ceiling(Font.LineHeight));
            int browserHeight = Math.Max(120, browserBottom - browserTop);

            BrowserView.Entity.Position = new float3(PanelPadding, browserTop, 0.2f);
            BrowserView.UpdateLayout(browserWidth, browserHeight);
        }

        /// <summary>
        /// Updates the file name label, input, and status text placement.
        /// </summary>
        /// <param name="panelWidth">Panel width used for layout.</param>
        /// <param name="panelHeight">Panel height used for layout.</param>
        void LayoutFileName(int panelWidth, int panelHeight) {
            int footerTop = panelHeight - PanelPadding - FooterHeight;
            int statusHeight = Math.Max(FileNameLabelHeight, (int)Math.Ceiling(Font.LineHeight));
            int fieldTop = footerTop - SectionSpacing - statusHeight - SectionSpacing - FileNameFieldHeight;
            int labelTop = fieldTop - FileNameLabelHeight;
            int contentWidth = Math.Max(0, panelWidth - PanelPadding * 2);

            FileNameLabelHost.Position = new float3(PanelPadding, labelTop, 0.2f);
            FontTightMetrics labelMetrics = Font.MeasureTight(FileNameLabel.Text);
            FileNameLabel.Size = new int2(contentWidth, (int)Math.Ceiling(Math.Max(labelMetrics.Height, FileNameLabelHeight)));

            FileNameFieldHost.Position = new float3(PanelPadding, fieldTop, 0.2f);
            FileNameField.Size = new int2(contentWidth, FileNameFieldHeight);

            StatusHost.Position = new float3(PanelPadding, fieldTop + FileNameFieldHeight + SectionSpacing, 0.2f);
            FontTightMetrics statusMetrics = Font.MeasureTight(StatusText.Text ?? string.Empty);
            StatusText.Size = new int2(contentWidth, (int)Math.Ceiling(Math.Max(statusMetrics.Height, Font.LineHeight)));
        }

        /// <summary>
        /// Updates footer button placement within the dialog panel.
        /// </summary>
        /// <param name="panelWidth">Panel width used for layout.</param>
        /// <param name="panelHeight">Panel height used for layout.</param>
        void LayoutFooter(int panelWidth, int panelHeight) {
            int footerTop = panelHeight - PanelPadding - FooterHeight;
            int saveButtonX = panelWidth - PanelPadding - SaveButtonSize.X;
            int cancelButtonX = saveButtonX - 8 - CancelButtonSize.X;
            int buttonY = footerTop + Math.Max(0, (FooterHeight - SaveButtonSize.Y) / 2);

            CancelButtonHost.Position = new float3(cancelButtonX, buttonY, 0.2f);
            SaveButtonHost.Position = new float3(saveButtonX, buttonY, 0.2f);
        }

        /// <summary>
        /// Applies the current panel position to the panel root entity.
        /// </summary>
        void ApplyPanelPosition() {
            PanelRoot.Position = new float3(PanelPosition.X, PanelPosition.Y, 0.1f);
        }

        /// <summary>
        /// Clamps the dialog panel inside the host window bounds.
        /// </summary>
        void ClampPanelPosition() {
            int maxX = Math.Max(0, HostSize.X - PanelSize.X);
            int maxY = Math.Max(0, HostSize.Y - PanelSize.Y);

            int clampedX = PanelPosition.X;
            if (clampedX < 0) {
                clampedX = 0;
            } else if (clampedX > maxX) {
                clampedX = maxX;
            }

            int clampedY = PanelPosition.Y;
            if (clampedY < 0) {
                clampedY = 0;
            } else if (clampedY > maxY) {
                clampedY = maxY;
            }

            PanelPosition = new int2(clampedX, clampedY);
        }

        /// <summary>
        /// Computes the vertical offset needed to center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container in pixels.</param>
        /// <param name="metrics">Tight font metrics for the text.</param>
        /// <returns>Top offset to position the text.</returns>
        float GetTextTopOffset(float containerHeight, FontTightMetrics metrics) {
            return (float)Math.Round(containerHeight * 0.5 - metrics.Height * 0.5 - metrics.MinTop);
        }
    }
}

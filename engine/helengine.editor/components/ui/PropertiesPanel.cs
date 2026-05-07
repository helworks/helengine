namespace helengine.editor {
    /// <summary>
    /// Dockable panel intended to show editable properties for the current selection.
    /// </summary>
    public class PropertiesPanel : DockableEntity {
        /// <summary>
        /// Padding applied to the content area.
        /// </summary>
        const int ContentPadding = 8;
        /// <summary>
        /// Margin inserted before the first visible content element.
        /// </summary>
        const int ContentTopMargin = 6;
        /// <summary>
        /// Spacing between stacked text lines.
        /// </summary>
        const int LineSpacing = 6;
        /// <summary>
        /// Height of each transform row.
        /// </summary>
        const int TransformRowHeight = 24;
        /// <summary>
        /// Width reserved for transform labels.
        /// </summary>
        const int TransformLabelWidth = 96;
        /// <summary>
        /// Spacing between transform fields.
        /// </summary>
        const int TransformFieldSpacing = 6;
        /// <summary>
        /// Height of each transform input field.
        /// </summary>
        const int TransformFieldHeight = 22;
        /// <summary>
        /// Spacing between transform controls and component properties.
        /// </summary>
        const int ComponentSectionSpacing = 10;
        /// <summary>
        /// Height of the add-component button.
        /// </summary>
        const int AddComponentButtonHeight = 24;
        /// <summary>
        /// Vertical spacing inserted after the add-component button before the component list.
        /// </summary>
        const int AddComponentListSpacing = 8;
        /// <summary>
        /// Horizontal padding added to the computed add-component button width.
        /// </summary>
        const int AddComponentButtonPadding = 16;

        /// <summary>
        /// Font used for property text.
        /// </summary>
        readonly FontAsset font;
        /// <summary>
        /// Render order for property text.
        /// </summary>
        readonly byte textOrder;
        /// <summary>
        /// Root entity hosting the scroll viewport directly below the title bar.
        /// </summary>
        readonly EditorEntity contentRoot;
        /// <summary>
        /// Camera owner that clips scrollable property content to the panel body.
        /// </summary>
        readonly EditorEntity ContentCameraEntity;
        /// <summary>
        /// Camera that renders the scrollable properties body inside the panel viewport only.
        /// </summary>
        readonly CameraComponent ContentCameraComponent;
        /// <summary>
        /// Root entity hosting all scrollable property content.
        /// </summary>
        readonly EditorEntity ScrollContentRoot;
        /// <summary>
        /// Scroll controller that tracks pixel-based vertical offset for the panel body.
        /// </summary>
        readonly ScrollComponent ContentScrollComponent;
        /// <summary>
        /// Clip owner attached to the fixed panel body host so overflow content clips against the visible viewport instead of the scrolling child.
        /// </summary>
        readonly ClipRectComponent ContentClipComponent;
        /// <summary>
        /// Hosts for each text line.
        /// </summary>
        readonly List<EditorEntity> lineHosts;
        /// <summary>
        /// Text components for each line.
        /// </summary>
        readonly List<TextComponent> lineTexts;
        /// <summary>
        /// Header text line.
        /// </summary>
        readonly TextComponent headerText;
        /// <summary>
        /// Asset path text line.
        /// </summary>
        readonly TextComponent pathText;
        /// <summary>
        /// Importer identifier text line.
        /// </summary>
        readonly TextComponent importerText;
        /// <summary>
        /// Source checksum text line.
        /// </summary>
        readonly TextComponent checksumText;
        /// <summary>
        /// Asset identifier text line.
        /// </summary>
        readonly TextComponent assetIdText;
        /// <summary>
        /// Status or error message line.
        /// </summary>
        readonly TextComponent statusText;
        /// <summary>
        /// View that renders asset import settings controls.
        /// </summary>
        readonly AssetImportSettingsView importSettingsView;
        /// <summary>
        /// View that renders material asset options.
        /// </summary>
        readonly MaterialAssetView MaterialView;
        /// <summary>
        /// Root entity hosting transform editing controls.
        /// </summary>
        readonly EditorEntity TransformRoot;
        /// <summary>
        /// View used to display component-specific properties.
        /// </summary>
        readonly ComponentPropertiesView ComponentView;
        /// <summary>
        /// Root entity hosting the add-component button.
        /// </summary>
        readonly EditorEntity AddComponentButtonRoot;
        /// <summary>
        /// Button used to open the add-component modal.
        /// </summary>
        readonly ButtonComponent AddComponentButton;
        /// <summary>
        /// Modal picker listing the components that can currently be added.
        /// </summary>
        readonly ComponentAddDialog AddComponentDialog;
        /// <summary>
        /// Provider that exposes components discovered from the currently loaded game script assembly.
        /// </summary>
        readonly IEditorScriptComponentCatalogProvider ScriptComponentCatalogProvider;
        /// <summary>
        /// Confirmation dialog shown before removing one component from the selected entity.
        /// </summary>
        readonly RemoveComponentDialog RemoveComponentDialog;
        /// <summary>
        /// Shared root entity that keeps screen-wide dialogs outside the docked panel tree.
        /// </summary>
        readonly EditorEntity ModalHost;
        /// <summary>
        /// Row entity for the entity name field.
        /// </summary>
        readonly EditorEntity NameRow;
        /// <summary>
        /// Label component for the name row.
        /// </summary>
        readonly TextComponent NameLabel;
        /// <summary>
        /// Host entity for the name input field.
        /// </summary>
        readonly EditorEntity NameFieldHost;
        /// <summary>
        /// Text box used to edit the entity name.
        /// </summary>
        readonly TextBoxComponent NameField;
        /// <summary>
        /// Row entity for position fields.
        /// </summary>
        readonly EditorEntity PositionRow;
        /// <summary>
        /// Row entity for rotation fields.
        /// </summary>
        readonly EditorEntity RotationRow;
        /// <summary>
        /// Row entity for scale fields.
        /// </summary>
        readonly EditorEntity ScaleRow;
        /// <summary>
        /// Label component for the position row.
        /// </summary>
        readonly TextComponent PositionLabel;
        /// <summary>
        /// Label component for the rotation row.
        /// </summary>
        readonly TextComponent RotationLabel;
        /// <summary>
        /// Label component for the scale row.
        /// </summary>
        readonly TextComponent ScaleLabel;
        /// <summary>
        /// Host entities for the position input fields.
        /// </summary>
        readonly EditorEntity[] PositionFieldHosts;
        /// <summary>
        /// Host entities for the rotation input fields.
        /// </summary>
        readonly EditorEntity[] RotationFieldHosts;
        /// <summary>
        /// Host entities for the scale input fields.
        /// </summary>
        readonly EditorEntity[] ScaleFieldHosts;
        /// <summary>
        /// Position input fields in X, Y, Z order.
        /// </summary>
        readonly TextBoxComponent[] PositionFields;
        /// <summary>
        /// Rotation input fields in X, Y, Z order (degrees).
        /// </summary>
        readonly TextBoxComponent[] RotationFields;
        /// <summary>
        /// Scale input fields in X, Y, Z order.
        /// </summary>
        readonly TextBoxComponent[] ScaleFields;
        /// <summary>
        /// Cached text values for position fields.
        /// </summary>
        readonly string[] PositionTextCache;
        /// <summary>
        /// Cached text values for rotation fields.
        /// </summary>
        readonly string[] RotationTextCache;
        /// <summary>
        /// Cached text values for scale fields.
        /// </summary>
        readonly string[] ScaleTextCache;
        /// <summary>
        /// Cached text value for the name field.
        /// </summary>
        string NameTextCache;
        /// <summary>
        /// Currently selected entity, if any.
        /// </summary>
        Entity SelectedEntity;
        /// <summary>
        /// Component currently pending removal confirmation.
        /// </summary>
        Component PendingRemovalComponent;
        /// <summary>
        /// True when transform controls should be visible.
        /// </summary>
        bool ShowTransformControls;
        /// <summary>
        /// True when text fields are being synchronized from the entity.
        /// </summary>
        bool IsSynchronizingInputs;
        /// <summary>
        /// True when a transform apply has been requested.
        /// </summary>
        bool ApplyTransformRequested;
        /// <summary>
        /// Cached host width used when laying out the screen-wide modal dialogs.
        /// </summary>
        int ModalHostWidth;
        /// <summary>
        /// Cached host height used when laying out the screen-wide modal dialogs.
        /// </summary>
        int ModalHostHeight;
        /// <summary>
        /// Cached width used by the add-component button.
        /// </summary>
        readonly int AddComponentButtonWidth;
        /// <summary>
        /// Currently selected asset entry, if any.
        /// </summary>
        AssetBrowserEntry currentEntry;
        /// <summary>
        /// Tracks whether the panel finished initialization.
        /// </summary>
        bool isInitialized;
        /// <summary>
        /// Last X coordinate used to position the clipped content camera viewport.
        /// </summary>
        float LastContentViewportX;
        /// <summary>
        /// Last Y coordinate used to position the clipped content camera viewport.
        /// </summary>
        float LastContentViewportY;
        /// <summary>
        /// Last width used by the clipped content camera viewport.
        /// </summary>
        float LastContentViewportWidth;
        /// <summary>
        /// Last height used by the clipped content camera viewport.
        /// </summary>
        float LastContentViewportHeight;

        /// <summary>
        /// Raised when the user applies a pending import setting change.
        /// </summary>
        public event Action<AssetBrowserEntry, AssetImportSettingsApplyRequest> ImportSettingsApplyRequested;

        /// <summary>
        /// Initializes a new properties panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="contentManager">Content manager used by nested asset-editing views.</param>
        public PropertiesPanel(FontAsset font, ContentManager contentManager) : this(font, contentManager, null, new EditorEntity(), null, EditorUiMetrics.Default, null) { }

        /// <summary>
        /// Initializes a new properties panel with the provided font.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="contentManager">Content manager used by nested asset-editing views.</param>
        /// <param name="fileSystemModelResolver">Resolver that loads processed runtime models for file-system model source entries.</param>
        public PropertiesPanel(FontAsset font, ContentManager contentManager, EditorFileSystemModelResolver fileSystemModelResolver) : this(font, contentManager, fileSystemModelResolver, new EditorEntity(), null, EditorUiMetrics.Default, null) { }

        /// <summary>
        /// Initializes a new properties panel with the provided font and modal host.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="contentManager">Content manager used by nested asset-editing views.</param>
        /// <param name="fileSystemModelResolver">Resolver that loads processed runtime models for file-system model source entries.</param>
        /// <param name="modalHost">Shared root entity used to host screen-wide dialogs.</param>
        public PropertiesPanel(FontAsset font, ContentManager contentManager, EditorFileSystemModelResolver fileSystemModelResolver, EditorEntity modalHost) : this(font, contentManager, fileSystemModelResolver, modalHost, null, EditorUiMetrics.Default, null) { }

        /// <summary>
        /// Initializes a new properties panel with the provided font, modal host, and script component provider.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="contentManager">Content manager used by nested asset-editing views.</param>
        /// <param name="fileSystemModelResolver">Resolver that loads processed runtime models for file-system model source entries.</param>
        /// <param name="modalHost">Shared root entity used to host screen-wide dialogs.</param>
        /// <param name="scriptComponentCatalogProvider">Provider used to discover components from the current game script assembly.</param>
        public PropertiesPanel(FontAsset font, ContentManager contentManager, EditorFileSystemModelResolver fileSystemModelResolver, EditorEntity modalHost, IEditorScriptComponentCatalogProvider scriptComponentCatalogProvider)
            : this(font, contentManager, fileSystemModelResolver, modalHost, scriptComponentCatalogProvider, EditorUiMetrics.Default, null) {
        }

        /// <summary>
        /// Initializes a new properties panel with the provided font, modal host, script component provider, and shared dock metrics.
        /// </summary>
        /// <param name="font">Font used for the title bar.</param>
        /// <param name="contentManager">Content manager used by nested asset-editing views.</param>
        /// <param name="fileSystemModelResolver">Resolver that loads processed runtime models for file-system model source entries.</param>
        /// <param name="modalHost">Shared root entity used to host screen-wide dialogs.</param>
        /// <param name="scriptComponentCatalogProvider">Provider used to discover components from the current game script assembly.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the dock title bar.</param>
        public PropertiesPanel(
            FontAsset font,
            ContentManager contentManager,
            EditorFileSystemModelResolver fileSystemModelResolver,
            EditorEntity modalHost,
            IEditorScriptComponentCatalogProvider scriptComponentCatalogProvider,
            EditorUiMetrics metrics,
            EditorFileSystemFontResolver fileSystemFontResolver = null) : base(font, metrics) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }
            if (modalHost == null) {
                throw new ArgumentNullException(nameof(modalHost));
            }

            this.font = font;
            ModalHost = modalHost;
            ScriptComponentCatalogProvider = scriptComponentCatalogProvider;
            AddComponentButtonWidth = Math.Max(128, (int)Math.Ceiling(font.MeasureTight("Add Component").Width) + AddComponentButtonPadding);
            Title = "Properties";
            MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));

            textOrder = RenderOrder2D.PanelForeground;

            contentRoot = new EditorEntity();
            contentRoot.LayerMask = LayerMask;
            contentRoot.Position = new float3(0, TitleBarHeightPixels, 0.05f);
            AddChild(contentRoot);

            ContentClipComponent = new ClipRectComponent();
            contentRoot.AddComponent(ContentClipComponent);

            ContentCameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = EditorLayerMasks.PropertiesPanelContent
            };
            ContentCameraComponent = new CameraComponent {
                LayerMask = EditorLayerMasks.PropertiesPanelContent,
                CameraDrawOrder = EditorUiCameraDrawOrders.PanelContent,
                ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0)
            };
            ContentCameraEntity.AddComponent(ContentCameraComponent);

            ScrollContentRoot = new EditorEntity();
            ScrollContentRoot.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            ScrollContentRoot.Position = float3.Zero;
            contentRoot.AddChild(ScrollContentRoot);

            ContentScrollComponent = new ScrollComponent();
            ContentScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
            ContentScrollComponent.ScrollOffsetChanged += HandleContentScrollOffsetChanged;
            contentRoot.AddComponent(ContentScrollComponent);

            lineHosts = new List<EditorEntity>(6);
            lineTexts = new List<TextComponent>(6);

            headerText = AddLine();
            pathText = AddLine();
            importerText = AddLine();
            checksumText = AddLine();
            assetIdText = AddLine();
            statusText = AddLine();

            importSettingsView = new AssetImportSettingsView(font, EditorLayerMasks.PropertiesPanelContent);
            importSettingsView.ApplyRequested += HandleImportSettingsApplyRequested;
            ScrollContentRoot.AddChild(importSettingsView.Root);

            MaterialView = new MaterialAssetView(font, EditorLayerMasks.PropertiesPanelContent);
            ScrollContentRoot.AddChild(MaterialView.Root);

            TransformRoot = new EditorEntity();
            TransformRoot.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            TransformRoot.Position = new float3(0, 0, 0.2f);
            ScrollContentRoot.AddChild(TransformRoot);

            if (fileSystemModelResolver == null && fileSystemFontResolver == null) {
                ComponentView = new ComponentPropertiesView(font, contentManager, null, null, EditorLayerMasks.PropertiesPanelContent);
            } else {
                ComponentView = new ComponentPropertiesView(font, contentManager, fileSystemModelResolver, fileSystemFontResolver, EditorLayerMasks.PropertiesPanelContent);
            }
            ComponentView.RemoveRequested += HandleComponentRemoveRequested;
            ScrollContentRoot.AddChild(ComponentView.Root);

            AddComponentButtonRoot = new EditorEntity();
            AddComponentButtonRoot.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            AddComponentButtonRoot.Position = float3.Zero;
            AddComponentButtonRoot.Enabled = true;
            ScrollContentRoot.AddChild(AddComponentButtonRoot);

            AddComponentButton = new ButtonComponent("Add Component", new int2(AddComponentButtonWidth, AddComponentButtonHeight), font, HandleAddComponentClicked, 0f);
            AddComponentButton.UseSquareCorners();
            AddComponentButton.SetHoverCursor(PointerCursorKind.Hand);
            AddComponentButtonRoot.AddComponent(AddComponentButton);
            AddComponentButtonRoot.Enabled = false;

            AddComponentDialog = new ComponentAddDialog(font);
            AddComponentDialog.ComponentSelected += HandleAddComponentSelected;
            ModalHost.LayerMask = LayerMask;
            ModalHost.Position = float3.Zero;
            ModalHost.InternalEntity = true;
            ModalHost.Enabled = true;
            ModalHost.AddChild(AddComponentDialog);

            RemoveComponentDialog = new RemoveComponentDialog(font);
            RemoveComponentDialog.ConfirmRequested += HandleRemoveComponentConfirmed;
            RemoveComponentDialog.CancelRequested += HandleRemoveComponentCanceled;
            ModalHost.AddChild(RemoveComponentDialog);

            CreateNameRow(out NameRow, out NameLabel, out NameFieldHost, out NameField);
            CreateTransformRow("Position", out PositionRow, out PositionLabel, out PositionFieldHosts, out PositionFields);
            CreateTransformRow("Rotation", out RotationRow, out RotationLabel, out RotationFieldHosts, out RotationFields);
            CreateTransformRow("Scale", out ScaleRow, out ScaleLabel, out ScaleFieldHosts, out ScaleFields);

            PositionTextCache = new string[3];
            RotationTextCache = new string[3];
            ScaleTextCache = new string[3];
            NameTextCache = string.Empty;

            HookNameEvents(NameField);
            HookTransformEvents(PositionFields);
            HookTransformEvents(RotationFields);
            HookTransformEvents(ScaleFields);

            AddComponent(new PropertiesPanelUpdater(this));

            ShowEmpty();
            isInitialized = true;
            UpdateContentViewportFromCurrentBounds();
            LayoutLines();
        }

        /// <summary>
        /// Shows import settings for the specified asset entry.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="settings">Import settings to display.</param>
        /// <param name="importerIds">Registered importer identifiers.</param>
        public void ShowImportSettings(
            AssetBrowserEntry entry,
            AssetImportSettings settings,
            IReadOnlyList<string> importerIds,
            IReadOnlyList<string> supportedPlatforms,
            string activePlatformId) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (importerIds == null) {
                throw new ArgumentNullException(nameof(importerIds));
            }
            if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            }
            if (string.IsNullOrWhiteSpace(activePlatformId)) {
                throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
            }

            currentEntry = entry;
            HideRemoveComponentDialog();
            importSettingsView.Show(importerIds, settings, supportedPlatforms, activePlatformId, entry.EntryKind);
            MaterialView.Hide();
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(Array.Empty<string>());
            LayoutLines();
        }

        /// <summary>
        /// Shows an error message for an asset when import settings cannot be resolved.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="message">Error message to display.</param>
        public void ShowImportError(AssetBrowserEntry entry, string message) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Message must be provided.", nameof(message));
            }

            currentEntry = null;
            HideRemoveComponentDialog();
            importSettingsView.Hide();
            MaterialView.Hide();
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(new[] {
                "Properties",
                $"Asset: {BuildAssetLabel(entry)}",
                $"Status: {message}"
            });
            LayoutLines();
        }

        /// <summary>
        /// Resets the panel to its empty selection state.
        /// </summary>
        public void ShowEmpty() {
            currentEntry = null;
            HideRemoveComponentDialog();
            importSettingsView.Hide();
            MaterialView.Hide();
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(Array.Empty<string>());
            LayoutLines();
        }

        /// <summary>
        /// Shows material options for a selected material asset.
        /// </summary>
        /// <param name="entry">Selected asset entry.</param>
        /// <param name="materialAsset">Material asset to edit.</param>
        /// <param name="settings">Per-platform settings sidecar for the selected material asset.</param>
        /// <param name="supportedPlatforms">Supported project platform identifiers.</param>
        /// <param name="activePlatformId">Currently active project platform identifier.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform.</param>
        public void ShowMaterialSettings(
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
            }

            currentEntry = entry;
            HideRemoveComponentDialog();
            importSettingsView.Hide();
            MaterialView.Show(entry, materialAsset, settings, supportedPlatforms, activePlatformId, selectionModelResolver);
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(Array.Empty<string>());
            LayoutLines();
        }

        /// <summary>
        /// Shows a read-only summary for one generated asset entry.
        /// </summary>
        /// <param name="entry">Generated asset entry selected in the browser.</param>
        public void ShowGeneratedAssetSummary(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            currentEntry = null;
            importSettingsView.Hide();
            MaterialView.Hide();
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(new[] {
                "Properties",
                $"Asset: {BuildAssetLabel(entry)}",
                $"Provider: {entry.ProviderId}",
                $"Asset Id: {entry.AssetId}",
                $"Kind: {entry.EntryKind}",
                "Source: Generated"
            });
            LayoutLines();
        }

        /// <summary>
        /// Shows a read-only summary for one serialized scene asset entry.
        /// </summary>
        /// <param name="entry">Scene asset entry selected in the browser.</param>
        public void ShowSceneAssetSummary(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            currentEntry = null;
            importSettingsView.Hide();
            MaterialView.Hide();
            SetTransformVisible(false);
            ComponentView.Hide();
            ApplyLines(new[] {
                "Properties",
                $"Asset: {BuildAssetLabel(entry)}",
                $"Path: {entry.RelativePath}",
                string.Empty,
                string.Empty,
                "Kind: Scene"
            });
            LayoutLines();
        }

        /// <summary>
        /// Shows transform and component details for a selected entity.
        /// </summary>
        /// <param name="entity">Selected entity to display.</param>
        public void ShowEntityProperties(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            currentEntry = null;
            HideRemoveComponentDialog();
            importSettingsView.Hide();
            MaterialView.Hide();
            SelectedEntity = entity;
            ApplyLines(Array.Empty<string>());
            SyncTransformFields(entity);
            ComponentView.ShowComponents(entity);
            SetTransformVisible(true);
            LayoutLines();
        }

        /// <summary>
        /// Updates the screen-wide modal dialogs using the current host window size.
        /// </summary>
        /// <param name="windowWidth">Current host window width.</param>
        /// <param name="windowHeight">Current host window height.</param>
        public void UpdateModalLayout(int windowWidth, int windowHeight) {
            ModalHostWidth = Math.Max(1, windowWidth);
            ModalHostHeight = Math.Max(1, windowHeight);
            AddComponentDialog.UpdateLayout(ModalHostWidth, ModalHostHeight);
            RemoveComponentDialog.UpdateLayout(ModalHostWidth, ModalHostHeight);
        }

        /// <summary>
        /// Opens the remove-component confirmation dialog for the supplied component.
        /// </summary>
        /// <param name="component">Component pending removal.</param>
        void HandleComponentRemoveRequested(Component component) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (SelectedEntity == null) {
                return;
            }

            PendingRemovalComponent = component;
            string entityName = SelectedEntity is EditorEntity editorEntity ? editorEntity.Name : SelectedEntity.GetType().Name;
            RemoveComponentDialog.Show(entityName, FormatComponentTitle(component.GetType().Name));
            if (ModalHostWidth > 0 && ModalHostHeight > 0) {
                RemoveComponentDialog.UpdateLayout(ModalHostWidth, ModalHostHeight);
            }
        }

        /// <summary>
        /// Removes the pending component from the selected entity after confirmation.
        /// </summary>
        void HandleRemoveComponentConfirmed() {
            if (SelectedEntity == null || PendingRemovalComponent == null) {
                HideRemoveComponentDialog();
                return;
            }
            if (SelectedEntity.Components == null || !SelectedEntity.Components.Contains(PendingRemovalComponent)) {
                HideRemoveComponentDialog();
                return;
            }

            SelectedEntity.RemoveComponent(PendingRemovalComponent);
            EditorSceneMutationService.MarkSceneMutated();
            HideRemoveComponentDialog();
            ShowEntityProperties(SelectedEntity);
        }

        /// <summary>
        /// Cancels the pending component removal.
        /// </summary>
        void HandleRemoveComponentCanceled() {
            HideRemoveComponentDialog();
        }

        /// <summary>
        /// Handles layout updates when the dockable size changes.
        /// </summary>
        protected override void OnSizeChanged() {
            base.OnSizeChanged();
            if (!isInitialized) {
                return;
            }

            LayoutLines();
            UpdateContentViewportFromCurrentBounds();
        }

        /// <summary>
        /// Reapplies scaled dock metrics after one live UI scale change.
        /// </summary>
        /// <param name="font">Updated dock title font.</param>
        /// <param name="metrics">Updated scaled editor UI metrics.</param>
        public override void ApplyUiMetrics(FontAsset font, EditorUiMetrics metrics) {
            base.ApplyUiMetrics(font, metrics);
        }

        /// <summary>
        /// Updates scaled content offsets after the shared dock title-bar metrics change.
        /// </summary>
        protected override void HandleUiMetricsApplied() {
            MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));
            contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
            UpdateContentViewportFromCurrentBounds();
            LayoutLines();
        }

        /// <summary>
        /// Reapplies the clipped content camera viewport when the panel moves or resizes.
        /// </summary>
        internal void UpdateContentViewportFromCurrentBounds() {
            if (!isInitialized) {
                return;
            }

            float viewportX = Position.X;
            float viewportY = Position.Y + TitleBarHeightPixels;
            float viewportWidth = Math.Max(1, GetContentViewportWidthPixels());
            float viewportHeight = Math.Max(1, GetContentViewportHeightPixels());
            if (LastContentViewportX == viewportX &&
                LastContentViewportY == viewportY &&
                LastContentViewportWidth == viewportWidth &&
                LastContentViewportHeight == viewportHeight) {
                return;
            }

            LastContentViewportX = viewportX;
            LastContentViewportY = viewportY;
            LastContentViewportWidth = viewportWidth;
            LastContentViewportHeight = viewportHeight;
            ContentCameraComponent.Viewport = new float4(viewportX, viewportY, viewportWidth, viewportHeight);
            ContentClipComponent.Size = new int2((int)Math.Round(viewportWidth), (int)Math.Round(viewportHeight));
            ContentScrollComponent.Size = new int2((int)Math.Round(viewportWidth), (int)Math.Round(viewportHeight));
        }

        /// <summary>
        /// Updates the scrollable content root when the vertical offset changes.
        /// </summary>
        /// <param name="scrollComponent">Scroll controller that raised the change notification.</param>
        /// <param name="scrollOffset">Current vertical scroll offset in pixels.</param>
        void HandleContentScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
            UpdateScrollContentPosition();
        }

        /// <summary>
        /// Gets the current width of the visible properties body.
        /// </summary>
        /// <returns>Viewport width in pixels.</returns>
        int GetContentViewportWidthPixels() {
            return Math.Max(Size.X, MinSize.X);
        }

        /// <summary>
        /// Gets the current height of the visible properties body.
        /// </summary>
        /// <returns>Viewport height in pixels.</returns>
        int GetContentViewportHeightPixels() {
            return Math.Max(Size.Y, MinSize.Y);
        }

        /// <summary>
        /// Updates scroll metrics to reflect the current document height and viewport size.
        /// </summary>
        /// <param name="contentHeightPixels">Measured total content height in pixels.</param>
        void UpdateContentScrollMetrics(int contentHeightPixels) {
            int viewportWidth = GetContentViewportWidthPixels();
            int viewportHeight = GetContentViewportHeightPixels();
            ContentScrollComponent.Size = new int2(viewportWidth, viewportHeight);
            ContentScrollComponent.VisibleItemCount = viewportHeight;
            ContentScrollComponent.ItemCount = Math.Max(0, contentHeightPixels);
            ContentScrollComponent.ClampScrollOffset();
            UpdateScrollContentPosition();
        }

        /// <summary>
        /// Applies the current scroll offset to the scrollable content root.
        /// </summary>
        void UpdateScrollContentPosition() {
            ScrollContentRoot.Position = new float3(0f, -ContentScrollComponent.ScrollOffset, 0.1f);
        }

        /// <summary>
        /// Creates a new line host with a text component.
        /// </summary>
        /// <returns>The created text component.</returns>
        TextComponent AddLine() {
            var host = new EditorEntity();
            host.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            host.Position = float3.Zero;
            ScrollContentRoot.AddChild(host);

            var text = new TextComponent();
            text.Font = font;
            text.Text = string.Empty;
            text.Color = ThemeManager.Colors.InputForegroundPrimary;
            text.Size = new int2(1, 1);
            text.RenderOrder2D = textOrder;
            host.AddComponent(text);

            lineHosts.Add(host);
            lineTexts.Add(text);
            return text;
        }

        /// <summary>
        /// Ensures enough text lines are available to display the requested count.
        /// </summary>
        /// <param name="count">Number of lines required.</param>
        void EnsureLineCount(int count) {
            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count), "Line count must be non-negative.");
            }

            for (int i = lineTexts.Count; i < count; i++) {
                AddLine();
            }
        }

        /// <summary>
        /// Applies the provided lines to the visible text rows.
        /// </summary>
        /// <param name="lines">Lines to display.</param>
        void ApplyLines(IReadOnlyList<string> lines) {
            if (lines == null) {
                throw new ArgumentNullException(nameof(lines));
            }

            EnsureLineCount(lines.Count);

            for (int i = 0; i < lineTexts.Count; i++) {
                TextComponent text = lineTexts[i];
                text.Text = i < lines.Count ? lines[i] ?? string.Empty : string.Empty;
            }
        }

        /// <summary>
        /// Creates a transform row with a label and three axis inputs.
        /// </summary>
        /// <param name="label">Label text for the row.</param>
        /// <param name="row">Created row entity.</param>
        /// <param name="labelText">Label text component.</param>
        /// <param name="fieldHosts">Axis host entities.</param>
        /// <param name="fields">Axis text box components.</param>
        void CreateTransformRow(
            string label,
            out EditorEntity row,
            out TextComponent labelText,
            out EditorEntity[] fieldHosts,
            out TextBoxComponent[] fields) {
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Label must be provided.", nameof(label));
            }

            row = new EditorEntity();
            row.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            row.Position = float3.Zero;
            TransformRoot.AddChild(row);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            labelHost.Position = float3.Zero;
            row.AddChild(labelHost);

            labelText = new TextComponent();
            labelText.Font = font;
            labelText.Text = label;
            labelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            labelText.Size = new int2(TransformLabelWidth, TransformFieldHeight);
            labelText.RenderOrder2D = textOrder;
            labelHost.AddComponent(labelText);

            fieldHosts = new EditorEntity[3];
            fields = new TextBoxComponent[3];
            string[] placeholders = new[] { "X", "Y", "Z" };
            for (int i = 0; i < fieldHosts.Length; i++) {
                var fieldHost = new EditorEntity();
                fieldHost.LayerMask = EditorLayerMasks.PropertiesPanelContent;
                fieldHost.Position = float3.Zero;
                row.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(60, TransformFieldHeight), font, placeholders[i]);
                fieldHost.AddComponent(field);

                fieldHosts[i] = fieldHost;
                fields[i] = field;
            }
        }

        /// <summary>
        /// Creates a single-line name row with a label and text input.
        /// </summary>
        /// <param name="row">Created row entity.</param>
        /// <param name="labelText">Label text component.</param>
        /// <param name="fieldHost">Host entity for the text field.</param>
        /// <param name="field">Text field component.</param>
        void CreateNameRow(
            out EditorEntity row,
            out TextComponent labelText,
            out EditorEntity fieldHost,
            out TextBoxComponent field) {
            row = new EditorEntity();
            row.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            row.Position = float3.Zero;
            TransformRoot.AddChild(row);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            labelHost.Position = float3.Zero;
            row.AddChild(labelHost);

            labelText = new TextComponent();
            labelText.Font = font;
            labelText.Text = "Name";
            labelText.Color = ThemeManager.Colors.InputForegroundPrimary;
            labelText.Size = new int2(TransformLabelWidth, TransformFieldHeight);
            labelText.RenderOrder2D = textOrder;
            labelHost.AddComponent(labelText);

            fieldHost = new EditorEntity();
            fieldHost.LayerMask = EditorLayerMasks.PropertiesPanelContent;
            fieldHost.Position = float3.Zero;
            row.AddChild(fieldHost);

            field = new TextBoxComponent(new int2(120, TransformFieldHeight), font);
            fieldHost.AddComponent(field);
        }

        /// <summary>
        /// Sets whether transform controls are visible.
        /// </summary>
        /// <param name="visible">True to show transform controls.</param>
        void SetTransformVisible(bool visible) {
            ShowTransformControls = visible;
            if (!visible) {
                SelectedEntity = null;
                ApplyTransformRequested = false;
                AddComponentButtonRoot.Enabled = false;
            }
        }

        /// <summary>
        /// Updates layout for transform rows and fields.
        /// </summary>
        /// <param name="top">Top offset within the content root.</param>
        /// <param name="maxWidth">Maximum available width.</param>
        void UpdateTransformLayout(int top, int maxWidth) {
            TransformRoot.Enabled = true;
            TransformRoot.Position = new float3(0, top, 0.2f);

            int labelWidth = Math.Min(TransformLabelWidth, maxWidth);
            int nameFieldWidth = Math.Max(48, maxWidth - labelWidth - TransformFieldSpacing);
            int availableFieldWidth = Math.Max(0, maxWidth - labelWidth - (TransformFieldSpacing * 2));
            int fieldWidth = Math.Max(48, availableFieldWidth / 3);
            int rowSpacing = LineSpacing + 2;

            int rowTop = 0;
            LayoutNameRow(labelWidth, nameFieldWidth, rowTop);
            rowTop += TransformRowHeight + rowSpacing;
            LayoutTransformRow(PositionRow, PositionLabel, PositionFieldHosts, PositionFields, labelWidth, fieldWidth, rowTop);
            rowTop += TransformRowHeight + rowSpacing;
            LayoutTransformRow(RotationRow, RotationLabel, RotationFieldHosts, RotationFields, labelWidth, fieldWidth, rowTop);
            rowTop += TransformRowHeight + rowSpacing;
            LayoutTransformRow(ScaleRow, ScaleLabel, ScaleFieldHosts, ScaleFields, labelWidth, fieldWidth, rowTop);
        }

        /// <summary>
        /// Updates the layout for a single transform row.
        /// </summary>
        /// <param name="row">Row entity to layout.</param>
        /// <param name="label">Label component for the row.</param>
        /// <param name="fieldHosts">Field host entities.</param>
        /// <param name="fields">Field text box components.</param>
        /// <param name="labelWidth">Width of the label region.</param>
        /// <param name="fieldWidth">Width of each field.</param>
        /// <param name="top">Top offset within the transform root.</param>
        void LayoutTransformRow(
            EditorEntity row,
            TextComponent label,
            EditorEntity[] fieldHosts,
            TextBoxComponent[] fields,
            int labelWidth,
            int fieldWidth,
            int top) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (label == null) {
                throw new ArgumentNullException(nameof(label));
            }
            if (fieldHosts == null) {
                throw new ArgumentNullException(nameof(fieldHosts));
            }
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }

            row.Position = new float3(ContentPadding, top, 0.2f);
            label.Size = new int2(labelWidth, TransformFieldHeight);

            int labelYOffset = Math.Max(0, (TransformRowHeight - TransformFieldHeight) / 2);
            if (label.Parent is EditorEntity labelHost) {
                labelHost.Position = new float3(0, labelYOffset, 0.2f);
            }

            int fieldX = labelWidth + TransformFieldSpacing;
            for (int i = 0; i < fieldHosts.Length; i++) {
                EditorEntity host = fieldHosts[i];
                host.Position = new float3(fieldX, labelYOffset, 0.2f);
                if (i < fields.Length) {
                    fields[i].Size = new int2(fieldWidth, TransformFieldHeight);
                }
                fieldX += fieldWidth + TransformFieldSpacing;
            }
        }

        /// <summary>
        /// Updates layout for the name row label and field.
        /// </summary>
        /// <param name="labelWidth">Width of the label region.</param>
        /// <param name="fieldWidth">Width of the name field.</param>
        /// <param name="top">Top offset within the transform root.</param>
        void LayoutNameRow(int labelWidth, int fieldWidth, int top) {
            NameRow.Position = new float3(ContentPadding, top, 0.2f);
            NameLabel.Size = new int2(labelWidth, TransformFieldHeight);

            int labelYOffset = Math.Max(0, (TransformRowHeight - TransformFieldHeight) / 2);
            if (NameLabel.Parent is EditorEntity labelHost) {
                labelHost.Position = new float3(0, labelYOffset, 0.2f);
            }

            NameFieldHost.Position = new float3(labelWidth + TransformFieldSpacing, labelYOffset, 0.2f);
            NameField.Size = new int2(fieldWidth, TransformFieldHeight);
        }

        /// <summary>
        /// Syncs the name and transform field text with the selected entity.
        /// </summary>
        /// <param name="entity">Entity to read transform values from.</param>
        void SyncTransformFields(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            IsSynchronizingInputs = true;
            try {
                SyncNameField(entity);
                float3 position = entity.Position;
                float3 scale = entity.Scale;
                double pitch;
                double yaw;
                double roll;
                GetOrientationDegrees(entity.Orientation, out pitch, out yaw, out roll);

                SetVectorFields(PositionFields, PositionTextCache, position.X, position.Y, position.Z);
                SetVectorFields(RotationFields, RotationTextCache, pitch, yaw, roll);
                SetVectorFields(ScaleFields, ScaleTextCache, scale.X, scale.Y, scale.Z);
            } finally {
                IsSynchronizingInputs = false;
            }
        }

        /// <summary>
        /// Syncs the name field text with the selected entity.
        /// </summary>
        /// <param name="entity">Entity to read the name from.</param>
        void SyncNameField(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            string nameText;
            if (entity is EditorEntity editorEntity) {
                nameText = editorEntity.Name;
                if (nameText == null) {
                    throw new InvalidOperationException("Entity name was not initialized.");
                }
            } else {
                nameText = entity.GetType().Name;
            }

            NameField.Text = nameText;
            NameTextCache = nameText;
        }

        /// <summary>
        /// Writes vector values to text fields and caches.
        /// </summary>
        /// <param name="fields">Fields to update.</param>
        /// <param name="cache">Cache to update.</param>
        /// <param name="x">X value.</param>
        /// <param name="y">Y value.</param>
        /// <param name="z">Z value.</param>
        void SetVectorFields(TextBoxComponent[] fields, string[] cache, double x, double y, double z) {
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }
            if (cache == null) {
                throw new ArgumentNullException(nameof(cache));
            }
            if (fields.Length < 3 || cache.Length < 3) {
                throw new InvalidOperationException("Transform fields are not initialized.");
            }

            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);

            fields[0].Text = xText;
            fields[1].Text = yText;
            fields[2].Text = zText;

            cache[0] = xText;
            cache[1] = yText;
            cache[2] = zText;
        }

        /// <summary>
        /// Applies transform edits if input fields have changed.
        /// </summary>
        internal void UpdateTransformEdits() {
            if (!ShowTransformControls || SelectedEntity == null) {
                return;
            }

            if (IsSynchronizingInputs) {
                return;
            }

            if (!ApplyTransformRequested) {
                return;
            }

            ApplyTransformRequested = false;
            bool sceneMutated = false;

            bool nameChanged = CacheFieldText(NameField, ref NameTextCache);
            bool positionChanged = CacheFieldText(PositionFields, PositionTextCache);
            bool rotationChanged = CacheFieldText(RotationFields, RotationTextCache);
            bool scaleChanged = CacheFieldText(ScaleFields, ScaleTextCache);

            if (nameChanged) {
                if (SelectedEntity is EditorEntity editorEntity) {
                    string text = NameField.Text;
                    if (text == null) {
                        throw new InvalidOperationException("Name field text was not initialized.");
                    }

                    if (!string.Equals(editorEntity.Name, text, StringComparison.Ordinal)) {
                        editorEntity.Name = text;
                        sceneMutated = true;
                    }
                    NameTextCache = text;
                } else {
                    SyncNameField(SelectedEntity);
                }
            }

            if (positionChanged) {
                double x;
                double y;
                double z;
                if (TryReadVector(PositionFields, out x, out y, out z)) {
                    float3 newPosition = new float3((float)x, (float)y, (float)z);
                    if (SelectedEntity.Position != newPosition) {
                        SelectedEntity.Position = newPosition;
                        sceneMutated = true;
                    }
                    SetVectorFields(PositionFields, PositionTextCache, x, y, z);
                }
            }

            if (rotationChanged) {
                double pitch;
                double yaw;
                double roll;
                if (TryReadVector(RotationFields, out pitch, out yaw, out roll)) {
                    float4 rotation;
                    double yawRad = yaw * (Math.PI / 180.0);
                    double pitchRad = pitch * (Math.PI / 180.0);
                    double rollRad = roll * (Math.PI / 180.0);
                    float4.CreateFromYawPitchRoll((float)yawRad, (float)pitchRad, (float)rollRad, out rotation);
                    if (!SelectedEntity.Orientation.Equals(rotation)) {
                        SelectedEntity.Orientation = rotation;
                        sceneMutated = true;
                    }
                    SetVectorFields(RotationFields, RotationTextCache, pitch, yaw, roll);
                }
            }

            if (scaleChanged) {
                double x;
                double y;
                double z;
                if (TryReadVector(ScaleFields, out x, out y, out z)) {
                    float3 newScale = new float3((float)x, (float)y, (float)z);
                    if (SelectedEntity.Scale != newScale) {
                        SelectedEntity.Scale = newScale;
                        sceneMutated = true;
                    }
                    SetVectorFields(ScaleFields, ScaleTextCache, x, y, z);
                }
            }

            if (sceneMutated) {
                EditorSceneMutationService.MarkSceneMutated();
            }
        }

        /// <summary>
        /// Hooks submit events for a set of transform fields.
        /// </summary>
        /// <param name="fields">Fields to subscribe to.</param>
        void HookTransformEvents(TextBoxComponent[] fields) {
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }

            for (int i = 0; i < fields.Length; i++) {
                TextBoxComponent field = fields[i];
                if (field == null) {
                    continue;
                }

                field.Submitted += HandleTransformSubmitted;
            }
        }

        /// <summary>
        /// Hooks submit events for the name field.
        /// </summary>
        /// <param name="field">Name field to subscribe to.</param>
        void HookNameEvents(TextBoxComponent field) {
            if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            field.Submitted += HandleTransformSubmitted;
        }

        /// <summary>
        /// Marks the transform as needing validation and apply.
        /// </summary>
        /// <param name="field">Field that was submitted.</param>
        void HandleTransformSubmitted(TextBoxComponent field) {
            if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            ApplyTransformRequested = true;
        }

        /// <summary>
        /// Caches field text and reports whether any value changed.
        /// </summary>
        /// <param name="fields">Text fields to inspect.</param>
        /// <param name="cache">Cache to update.</param>
        /// <returns>True when any field text changed.</returns>
        bool CacheFieldText(TextBoxComponent[] fields, string[] cache) {
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }
            if (cache == null) {
                throw new ArgumentNullException(nameof(cache));
            }
            if (fields.Length < 3 || cache.Length < 3) {
                throw new InvalidOperationException("Transform fields are not initialized.");
            }

            bool changed = false;
            for (int i = 0; i < 3; i++) {
                string text = fields[i].Text ?? string.Empty;
                if (!string.Equals(cache[i], text, StringComparison.Ordinal)) {
                    changed = true;
                    cache[i] = text;
                }
            }

            return changed;
        }

        /// <summary>
        /// Caches a single text field and reports whether its value changed.
        /// </summary>
        /// <param name="field">Text field to inspect.</param>
        /// <param name="cache">Cache to update.</param>
        /// <returns>True when the field text changed.</returns>
        bool CacheFieldText(TextBoxComponent field, ref string cache) {
            if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            string text = field.Text;
            if (text == null) {
                throw new InvalidOperationException("Text field was not initialized.");
            }

            string cacheValue = cache ?? string.Empty;
            if (string.Equals(cacheValue, text, StringComparison.Ordinal)) {
                return false;
            }

            cache = text;
            return true;
        }

        /// <summary>
        /// Reads a vector from three text fields.
        /// </summary>
        /// <param name="fields">Text fields to parse.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <returns>True when all fields parsed successfully.</returns>
        bool TryReadVector(TextBoxComponent[] fields, out double x, out double y, out double z) {
            x = 0;
            y = 0;
            z = 0;
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }
            if (fields.Length < 3) {
                throw new InvalidOperationException("Transform fields are not initialized.");
            }

            if (!TryReadNumber(fields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(fields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(fields[2].Text, out z)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Parses a numeric string with invariant culture.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="value">Parsed value.</param>
        /// <returns>True when parsing succeeds.</returns>
        bool TryReadNumber(string text, out double value) {
            if (string.IsNullOrWhiteSpace(text)) {
                value = 0;
                return false;
            }

            return double.TryParse(
                text,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// Converts a quaternion to pitch/yaw/roll in degrees.
        /// </summary>
        /// <param name="orientation">Quaternion orientation.</param>
        /// <param name="pitch">Pitch angle in degrees.</param>
        /// <param name="yaw">Yaw angle in degrees.</param>
        /// <param name="roll">Roll angle in degrees.</param>
        void GetOrientationDegrees(float4 orientation, out double pitch, out double yaw, out double roll) {
            double x = orientation.X;
            double y = orientation.Y;
            double z = orientation.Z;
            double w = orientation.W;

            double sinPitch = 2.0 * (w * x - y * z);
            if (Math.Abs(sinPitch) >= 1.0) {
                pitch = Math.CopySign(Math.PI / 2.0, sinPitch);
            } else {
                pitch = Math.Asin(sinPitch);
            }

            double sinYaw = 2.0 * (w * y + x * z);
            double cosYaw = 1.0 - 2.0 * (x * x + y * y);
            yaw = Math.Atan2(sinYaw, cosYaw);

            double sinRoll = 2.0 * (w * z + x * y);
            double cosRoll = 1.0 - 2.0 * (y * y + z * z);
            roll = Math.Atan2(sinRoll, cosRoll);

            pitch = pitch * (180.0 / Math.PI);
            yaw = yaw * (180.0 / Math.PI);
            roll = roll * (180.0 / Math.PI);
        }

        /// <summary>
        /// Formats a double value for display.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted string.</returns>
        string FormatDouble(double value) {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }


        /// <summary>
        /// Updates line positions and sizes based on the current text content.
        /// </summary>
        void LayoutLines() {
            UpdateContentViewportFromCurrentBounds();
            int rowWidth = GetContentViewportWidthPixels();
            int maxWidth = Math.Max(0, rowWidth - ContentPadding * 2);
            float lineHeight = (float)Math.Max((double)font.LineHeight, 1.0);

            float offsetY = ContentTopMargin;
            for (int i = 0; i < lineTexts.Count; i++) {
                TextComponent text = lineTexts[i];
                EditorEntity host = lineHosts[i];
                if (string.IsNullOrWhiteSpace(text.Text)) {
                    host.Enabled = false;
                    continue;
                }

                host.Enabled = true;
                host.Position = new float3(ContentPadding, (float)Math.Round(offsetY), 0.2f);
                text.Size = new int2(maxWidth, (int)Math.Ceiling(lineHeight));
                offsetY += lineHeight + LineSpacing;
            }

            if (importSettingsView.IsVisible) {
                int viewTop = (int)Math.Round(offsetY);
                importSettingsView.UpdateLayout(ContentPadding, viewTop, maxWidth);
                offsetY = viewTop + importSettingsView.Height + LineSpacing;
            }

            if (MaterialView.IsVisible) {
                int viewTop = (int)Math.Round(offsetY);
                MaterialView.UpdateLayout(ContentPadding, viewTop, maxWidth);
                offsetY = viewTop + MaterialView.Height + LineSpacing;
            }

            if (ShowTransformControls) {
                int transformTop = (int)Math.Round(offsetY);
                UpdateTransformLayout(transformTop, maxWidth);
                int addComponentTop = transformTop + GetTransformSectionHeight() + ComponentSectionSpacing;
                LayoutAddComponentButton(addComponentTop, maxWidth);
                int componentTop = addComponentTop + AddComponentButtonHeight + AddComponentListSpacing;
                ComponentView.UpdateLayout(0, componentTop, rowWidth);
                offsetY = Math.Max(addComponentTop + AddComponentButtonHeight, componentTop + ComponentView.Height);
            } else {
                TransformRoot.Enabled = false;
                AddComponentButtonRoot.Enabled = false;
                ComponentView.Hide();
            }

            int contentHeight = (int)Math.Ceiling(offsetY) + ContentPadding;
            UpdateContentScrollMetrics(contentHeight);
        }

        /// <summary>
        /// Calculates the total height consumed by the transform controls.
        /// </summary>
        /// <returns>Height in pixels.</returns>
        int GetTransformSectionHeight() {
            int rowSpacing = LineSpacing + 2;
            return (TransformRowHeight * 4) + (rowSpacing * 3);
        }

        /// <summary>
        /// Hides the remove-component confirmation dialog and clears the pending component.
        /// </summary>
        void HideRemoveComponentDialog() {
            PendingRemovalComponent = null;
            RemoveComponentDialog.Hide();
        }

        /// <summary>
        /// Handles activation of the add-component button.
        /// </summary>
        void HandleAddComponentClicked() {
            if (SelectedEntity == null || SelectedEntity is not EditorEntity) {
                return;
            }

            IReadOnlyList<EditorComponentAddDescriptor> scriptDescriptors = null;
            if (ScriptComponentCatalogProvider != null) {
                scriptDescriptors = ScriptComponentCatalogProvider.GetAvailableScriptComponents(SelectedEntity);
            }

            AddComponentDialog.Show((EditorEntity)SelectedEntity, scriptDescriptors);
            if (ModalHostWidth > 0 && ModalHostHeight > 0) {
                AddComponentDialog.UpdateLayout(ModalHostWidth, ModalHostHeight);
            }
        }

        /// <summary>
        /// Adds one selected component to the current entity and refreshes the properties view.
        /// </summary>
        /// <param name="descriptor">Descriptor that defines the component to add.</param>
        void HandleAddComponentSelected(EditorComponentAddDescriptor descriptor) {
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (SelectedEntity == null) {
                return;
            }

            descriptor.AddAction(SelectedEntity);
            EditorSceneMutationService.MarkSceneMutated();
            ShowEntityProperties(SelectedEntity);
        }

        /// <summary>
        /// Positions the add-component button above the component list.
        /// </summary>
        /// <param name="top">Top offset within the content root.</param>
        void LayoutAddComponentButton(int top, int width) {
            if (SelectedEntity == null || SelectedEntity is not EditorEntity) {
                AddComponentButtonRoot.Enabled = false;
                return;
            }

            AddComponentButtonRoot.Enabled = true;
            AddComponentButtonRoot.Position = new float3(ContentPadding, top, 0.2f);
            AddComponentButton.SetSize(new int2(Math.Max(0, width), AddComponentButtonHeight));
        }

        /// <summary>
        /// Formats one component type name into a readable title.
        /// </summary>
        /// <param name="componentTypeName">Raw component type name.</param>
        /// <returns>Readable component title.</returns>
        string FormatComponentTitle(string componentTypeName) {
            if (string.IsNullOrWhiteSpace(componentTypeName)) {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(componentTypeName.Length + 8);
            for (int i = 0; i < componentTypeName.Length; i++) {
                char current = componentTypeName[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(componentTypeName[i - 1])) {
                    builder.Append(' ');
                }
                builder.Append(current);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Forwards apply requests from the import settings view.
        /// </summary>
        /// <param name="request">Pending importer and processor settings to apply.</param>
        void HandleImportSettingsApplyRequested(AssetImportSettingsApplyRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (currentEntry == null) {
                throw new InvalidOperationException("No asset is currently selected.");
            }

            if (ImportSettingsApplyRequested != null) {
                ImportSettingsApplyRequested(currentEntry, request);
            }
        }

        /// <summary>
        /// Builds a display label for an asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to describe.</param>
        /// <returns>Display label for the asset path.</returns>
        string BuildAssetLabel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!string.IsNullOrWhiteSpace(entry.RelativePath)) {
                return entry.RelativePath;
            }

            return entry.Name;
        }
    }
}

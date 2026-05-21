using System.Globalization;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Builds editable property rows for each component on a selected entity.
    /// </summary>
    public class ComponentPropertiesView {
        /// <summary>
        /// Height of header rows in pixels.
        /// </summary>
        const int HeaderHeight = 22;
        /// <summary>
        /// Height of one component title bar in pixels.
        /// </summary>
        const int SectionHeaderHeight = 26;
        /// <summary>
        /// Vertical spacing preserved between one component title bar and its first row.
        /// </summary>
        const int SectionHeaderSpacing = 6;
        /// <summary>
        /// Vertical spacing preserved between component sections.
        /// </summary>
        const int SectionSpacing = 10;
        /// <summary>
        /// Horizontal padding applied inside the body of each component section.
        /// </summary>
        const int SectionBodyPadding = 8;
        /// <summary>
        /// Padding applied before the title text inside one component section header.
        /// </summary>
        const int SectionHeaderPadding = 8;
        /// <summary>
        /// Width reserved for the fixed remove button in one component section header.
        /// </summary>
        const int SectionRemoveButtonWidth = 32;
        /// <summary>
        /// Width reserved for the section-level revert button.
        /// </summary>
        const int SectionRevertButtonWidth = 60;
        /// <summary>
        /// Height reserved for the section-level revert button.
        /// </summary>
        const int SectionRevertButtonHeight = 22;
        /// <summary>
        /// Horizontal spacing preserved between the title and the active header action.
        /// </summary>
        const int SectionHeaderButtonSpacing = 6;
        /// <summary>
        /// Height of property rows in pixels.
        /// </summary>
        const int RowHeight = 24;
        /// <summary>
        /// Spacing between rows in pixels.
        /// </summary>
        const int RowSpacing = 6;
        /// <summary>
        /// Fraction of each row reserved for the label column.
        /// </summary>
        const double LabelWidthRatio = 0.4;
        /// <summary>
        /// Height of text fields.
        /// </summary>
        const int FieldHeight = 22;
        /// <summary>
        /// Spacing between fields in vector rows.
        /// </summary>
        const int FieldSpacing = 6;
        /// <summary>
        /// Width of the material pick button.
        /// </summary>
        const int PickButtonWidth = 80;
        /// <summary>
        /// Width reserved for the row-level revert button.
        /// </summary>
        const int RevertButtonWidth = 60;
        /// <summary>
        /// Height of the material pick button.
        /// </summary>
        const int PickButtonHeight = 22;
        /// <summary>
        /// Height of the row-level revert button.
        /// </summary>
        const int RevertButtonHeight = 22;
        /// <summary>
        /// Border thickness used for row-level override markers.
        /// </summary>
        const float OverrideOutlineThickness = 1f;
        /// <summary>
        /// Placeholder text for empty asset values.
        /// </summary>
        const string EmptyAssetLabel = "None";
        /// <summary>
        /// Placeholder text for assigned assets without a known label.
        /// </summary>
        const string AssignedAssetLabel = "Assigned";
        /// <summary>
        /// Extension used for material assets.
        /// </summary>
        const string MaterialExtension = EditorFileTemplateRegistry.MaterialExtension;
        /// <summary>
        /// Nested member identifier used by scene-map source-entry rows.
        /// </summary>
        const string SceneMapSourceEntryMemberName = "SourceSceneId";
        /// <summary>
        /// Nested member identifier used by scene-map target-entry rows.
        /// </summary>
        const string SceneMapTargetEntryMemberName = "TargetSceneId";
        /// <summary>
        /// Nested member identifier used by scene-map draft source rows.
        /// </summary>
        const string SceneMapDraftSourceMemberName = "DraftSourceSceneId";
        /// <summary>
        /// Nested member identifier used by scene-map draft target rows.
        /// </summary>
        const string SceneMapDraftTargetMemberName = "DraftTargetSceneId";
        /// <summary>
        /// Preferred preview platform used when file-backed materials need one shader-backed editor runtime path.
        /// </summary>
        const string PreferredEditorPreviewPlatformId = "windows";
        /// <summary>
        /// Extension used for font assets.
        /// </summary>
        static readonly string[] FontExtensions = new[] { ".ttf", ".otf" };

        /// <summary>
        /// Font used for labels and values.
        /// </summary>
        readonly FontAsset Font;
        /// <summary>
        /// Content manager used to load serialized editor assets.
        /// </summary>
        readonly ContentManager AssetContentManager;
        /// <summary>
        /// Converts picked browser entries into stable scene asset references.
        /// </summary>
        readonly SceneAssetReferenceFactory AssetReferenceFactory;
        /// <summary>
        /// Resolves file-system model source files through the processed model cache.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;
        /// <summary>
        /// Resolves file-system font source files through the imported font cache.
        /// </summary>
        readonly EditorFileSystemFontResolver FileSystemFontResolver;
        /// <summary>
        /// Root entity that hosts the component property rows.
        /// </summary>
        readonly EditorEntity RootEntity;
        /// <summary>
        /// Pool of inactive rows that can be reused.
        /// </summary>
        readonly List<ComponentPropertyRow> RowPool;
        /// <summary>
        /// Active rows currently displayed.
        /// </summary>
        readonly List<ComponentPropertyRow> ActiveRows;
        /// <summary>
        /// Pool of inactive component sections that can be reused.
        /// </summary>
        readonly List<ComponentSectionView> SectionPool;
        /// <summary>
        /// Active component sections currently displayed.
        /// </summary>
        readonly List<ComponentSectionView> ActiveSections;
        /// <summary>
        /// Map of vector text fields to their owning row.
        /// </summary>
        readonly Dictionary<TextBoxComponent, ComponentPropertyRow> VectorFieldRows;
        /// <summary>
        /// Map of Vector4 text fields to their owning row.
        /// </summary>
        readonly Dictionary<TextBoxComponent, ComponentPropertyRow> Vector4FieldRows;
        /// <summary>
        /// Map of scalar text fields to their owning row.
        /// </summary>
        readonly Dictionary<TextBoxComponent, ComponentPropertyRow> ScalarFieldRows;
        /// <summary>
        /// Tracks display labels for runtime models assigned via the picker.
        /// </summary>
        readonly Dictionary<RuntimeModel, string> ModelLabels;
        /// <summary>
        /// Tracks display labels for runtime materials assigned via the picker.
        /// </summary>
        readonly Dictionary<RuntimeMaterial, string> MaterialLabels;
        /// <summary>
        /// Tracks display labels for font assets assigned via the picker.
        /// </summary>
        readonly Dictionary<FontAsset, string> FontLabels;
        /// <summary>
        /// Builds reflected property descriptors for the default inspector path.
        /// </summary>
        readonly ReflectedComponentPropertyDescriptorBuilder DescriptorBuilder;
        /// <summary>
        /// Builds and persists detached component overrides for non-common platform tabs.
        /// </summary>
        readonly ComponentPlatformEditingService PlatformEditingService;
        /// <summary>
        /// Render order used for label text.
        /// </summary>
        readonly byte TextOrder;
        /// <summary>
        /// Tracks the collapsed state currently chosen for visible components.
        /// </summary>
        readonly Dictionary<Component, bool> CollapsedStates;
        /// <summary>
        /// Tracks nested provider-backed section expansion state for visible components.
        /// </summary>
        readonly Dictionary<string, bool> CustomEditorExpandedStates;
        /// <summary>
        /// Tracks pending draft source scene ids for scene-map custom editors.
        /// </summary>
        readonly Dictionary<Component, string> SceneMapDraftSourcesByComponent;
        /// <summary>
        /// Tracks pending draft target scene ids for scene-map custom editors.
        /// </summary>
        readonly Dictionary<Component, string> SceneMapDraftTargetsByComponent;
        /// <summary>
        /// Tracks whether the view is updating text fields internally.
        /// </summary>
        bool IsSynchronizing;
        /// <summary>
        /// Entity currently being inspected.
        /// </summary>
        Entity CurrentEntity;
        /// <summary>
        /// Platform id currently being edited by the component inspector.
        /// </summary>
        string CurrentPlatformId;
        /// <summary>
        /// Last left offset used during layout.
        /// </summary>
        int LastLayoutLeft;
        /// <summary>
        /// Last top offset used during layout.
        /// </summary>
        int LastLayoutTop;
        /// <summary>
        /// Last available width used during layout.
        /// </summary>
        int LastLayoutWidth;
        /// <summary>
        /// Tracks whether one layout pass has already established the current row geometry.
        /// </summary>
        bool HasLayoutState;
        /// <summary>
        /// Height consumed by the most recent visible layout pass.
        /// </summary>
        int LayoutHeightValue;

        /// <summary>
        /// Raised when the user requests to remove one component section.
        /// </summary>
        public event Action<ComponentSectionView> RemoveRequested;

        /// <summary>
        /// Initializes a new component properties view.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        public ComponentPropertiesView(FontAsset font, ContentManager contentManager) : this(font, contentManager, null, null, EditorLayerMasks.EditorUi) { }

        /// <summary>
        /// Initializes a new component properties view with support for file-system model source resolution.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        public ComponentPropertiesView(FontAsset font, ContentManager contentManager, EditorFileSystemModelResolver fileSystemModelResolver)
            : this(font, contentManager, fileSystemModelResolver, null, EditorLayerMasks.EditorUi) {
        }

        /// <summary>
        /// Initializes a new component properties view with support for file-system model and font source resolution.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        /// <param name="fileSystemFontResolver">Resolver that imports or loads processed font assets for file-system font sources.</param>
        public ComponentPropertiesView(
            FontAsset font,
            ContentManager contentManager,
            EditorFileSystemModelResolver fileSystemModelResolver,
            EditorFileSystemFontResolver fileSystemFontResolver)
            : this(font, contentManager, fileSystemModelResolver, fileSystemFontResolver, EditorLayerMasks.EditorUi) {
        }

        /// <summary>
        /// Initializes a new component properties view with support for file-system model and font source resolution on the requested UI layer.
        /// </summary>
        /// <param name="font">Font used for row labels.</param>
        /// <param name="contentManager">Content manager used to load serialized assets.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        /// <param name="fileSystemFontResolver">Resolver that imports or loads processed font assets for file-system font sources.</param>
        /// <param name="layerMask">Layer used by the view root and all generated rows.</param>
        public ComponentPropertiesView(
            FontAsset font,
            ContentManager contentManager,
            EditorFileSystemModelResolver fileSystemModelResolver,
            EditorFileSystemFontResolver fileSystemFontResolver,
            ushort layerMask) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            Font = font;
            AssetContentManager = contentManager;
            AssetReferenceFactory = new SceneAssetReferenceFactory();
            FileSystemModelResolver = fileSystemModelResolver;
            FileSystemFontResolver = fileSystemFontResolver;
            RootEntity = new EditorEntity();
            RootEntity.LayerMask = layerMask;
            RootEntity.Position = float3.Zero;
            RootEntity.Enabled = false;

            RowPool = new List<ComponentPropertyRow>(16);
            ActiveRows = new List<ComponentPropertyRow>(16);
            SectionPool = new List<ComponentSectionView>(8);
            ActiveSections = new List<ComponentSectionView>(8);
            VectorFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            Vector4FieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ScalarFieldRows = new Dictionary<TextBoxComponent, ComponentPropertyRow>();
            ModelLabels = new Dictionary<RuntimeModel, string>();
            MaterialLabels = new Dictionary<RuntimeMaterial, string>();
            FontLabels = new Dictionary<FontAsset, string>();
            DescriptorBuilder = new ReflectedComponentPropertyDescriptorBuilder();
            PlatformEditingService = new ComponentPlatformEditingService();
            CollapsedStates = new Dictionary<Component, bool>();
            CustomEditorExpandedStates = new Dictionary<string, bool>();
            SceneMapDraftSourcesByComponent = new Dictionary<Component, string>();
            SceneMapDraftTargetsByComponent = new Dictionary<Component, string>();
            TextOrder = RenderOrder2D.PanelForeground;
            CurrentPlatformId = ComponentPlatformEditingService.CommonPlatformId;
        }

        /// <summary>
        /// Gets the root entity for the view.
        /// </summary>
        public EditorEntity Root => RootEntity;

        /// <summary>
        /// Gets a value indicating whether the view is visible.
        /// </summary>
        public bool IsVisible => RootEntity.Enabled;

        /// <summary>
        /// Gets the height consumed by the current visible section layout.
        /// </summary>
        public int Height => LayoutHeightValue;

        /// <summary>
        /// Shows component properties for the specified entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        public void ShowComponents(Entity entity) {
            ShowComponents(entity, ComponentPlatformEditingService.CommonPlatformId);
        }

        /// <summary>
        /// Shows component properties for the specified entity in one selected platform context.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <param name="platformId">Platform context displayed by the inspector.</param>
        public void ShowComponents(Entity entity, string platformId) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            CurrentEntity = entity;
            CurrentPlatformId = platformId;
            ClearActiveRows();
            ClearActiveSections();
            EntitySaveComponent saveComponent = ResolveEntitySaveComponent(entity);
            bool hasCommonComponents = entity.Components != null && entity.Components.Count > 0;
            IReadOnlyList<EntityPlatformAddedComponentState> addedComponents = saveComponent != null
                ? PlatformEditingService.GetAddedComponents(saveComponent, platformId)
                : Array.Empty<EntityPlatformAddedComponentState>();
            if (!hasCommonComponents && addedComponents.Count < 1) {
                RootEntity.Enabled = false;
                LayoutHeightValue = 0;
                return;
            }

            RootEntity.Enabled = true;

            if (hasCommonComponents) {
                for (int i = 0; i < entity.Components.Count; i++) {
                    Component commonComponent = entity.Components[i];
                    if (commonComponent == null) {
                        continue;
                    }
                    if (commonComponent is IEditorHiddenComponent) {
                        continue;
                    }

                    if (PlatformEditingService.IsComponentRemoved(commonComponent, saveComponent, platformId)) {
                        ComponentSectionView removedSection = AcquireSection(commonComponent, commonComponent, saveComponent, platformId, false, true);
                        ActiveSections.Add(removedSection);
                        continue;
                    }

                    Component editableComponent = ResolveEditableComponent(commonComponent, saveComponent, platformId);
                    ComponentSectionView section = AcquireSection(commonComponent, commonComponent, saveComponent, platformId, false, false);
                    AddPropertyRows(section, commonComponent, editableComponent, saveComponent, platformId);
                    ActiveSections.Add(section);
                }
            }

            for (int index = 0; index < addedComponents.Count; index++) {
                EntityPlatformAddedComponentState addedComponentState = addedComponents[index];
                ComponentSectionView addedSection = AcquireSection(
                    addedComponentState.Component,
                    null,
                    saveComponent,
                    platformId,
                    true,
                    false);
                AddPropertyRows(addedSection, null, addedComponentState.Component, saveComponent, platformId);
                ActiveSections.Add(addedSection);
            }

            if (ActiveSections.Count == 0) {
                RootEntity.Enabled = false;
                LayoutHeightValue = 0;
            }
        }

        /// <summary>
        /// Hides the view and clears active rows.
        /// </summary>
        public void Hide() {
            ClearActiveRows();
            ClearActiveSections();
            RootEntity.Enabled = false;
            LayoutHeightValue = 0;
            CurrentEntity = null;
            CurrentPlatformId = ComponentPlatformEditingService.CommonPlatformId;
            HasLayoutState = false;
        }

        /// <summary>
        /// Resolves the effective editable component for the requested platform.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="saveComponent">Hidden save component attached to the entity.</param>
        /// <param name="platformId">Platform context displayed by the inspector.</param>
        /// <returns>Effective editable component for the supplied platform.</returns>
        Component ResolveEditableComponent(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (saveComponent == null) {
                return commonComponent;
            }

            return PlatformEditingService.ResolveEditableComponent(commonComponent, saveComponent, platformId);
        }

        /// <summary>
        /// Updates the layout of the rows using the provided bounds.
        /// </summary>
        /// <param name="left">Left offset relative to the parent panel.</param>
        /// <param name="top">Top offset relative to the parent panel.</param>
        /// <param name="maxWidth">Maximum width available for the rows.</param>
        public void UpdateLayout(int left, int top, int maxWidth) {
            if (!RootEntity.Enabled) {
                LayoutHeightValue = 0;
                return;
            }

            LastLayoutLeft = left;
            LastLayoutTop = top;
            LastLayoutWidth = maxWidth;
            HasLayoutState = true;
            RootEntity.Position = new float3(left, top, 0.2f);
            int width = Math.Max(0, maxWidth);
            int y = 0;
            for (int i = 0; i < ActiveSections.Count; i++) {
                ComponentSectionView section = ActiveSections[i];
                LayoutSectionHeader(section, width, y);
                y += SectionHeaderHeight;

                if (!section.IsCollapsed) {
                    y += SectionHeaderSpacing;
                    for (int rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++) {
                        ComponentPropertyRow row = section.Rows[rowIndex];
                        int rowHeight = row.Kind == ComponentPropertyRowKind.Header ? HeaderHeight : RowHeight;
                        row.Entity.Enabled = true;
                        LayoutRow(row, width, y, rowHeight);
                        y += rowHeight + RowSpacing;
                    }
                } else {
                    SetSectionRowsEnabled(section, false);
                }

                y += SectionSpacing;
            }

            LayoutHeightValue = y > 0 ? y - SectionSpacing : 0;
        }

        /// <summary>
        /// Clears active rows and returns them to the pool.
        /// </summary>
        void ClearActiveRows() {
            for (int i = 0; i < ActiveRows.Count; i++) {
                ComponentPropertyRow row = ActiveRows[i];
                row.Entity.Enabled = false;
                row.CommonComponent = null;
                row.TargetComponent = null;
                row.SaveComponent = null;
                row.EditingPlatformId = null;
                row.Property = null;
                row.ValueType = null;
                row.CustomEditorTypeId = null;
                row.NestedMemberName = null;
                row.CustomEditorEntryKey = null;
                row.IndentLevel = 0;
                row.IsExpanded = false;
                row.IsOverrideActive = false;
                if (row.ActionButtonHost != null) {
                    row.ActionButtonHost.Enabled = false;
                }
                if (row.RevertButtonHost != null) {
                    row.RevertButtonHost.Enabled = false;
                }
                RowPool.Add(row);
            }

            ActiveRows.Clear();
        }

        /// <summary>
        /// Clears active component sections and returns them to the pool.
        /// </summary>
        void ClearActiveSections() {
            for (int i = 0; i < ActiveSections.Count; i++) {
                ComponentSectionView section = ActiveSections[i];
                section.Root.Enabled = false;
                section.TargetComponent = null;
                section.CommonComponent = null;
                section.SaveComponent = null;
                section.EditingPlatformId = null;
                section.IsPlatformOnlyComponent = false;
                section.IsRemovedOnPlatform = false;
                section.IsExistenceOverrideActive = false;
                if (section.HeaderOverrideOutline != null) {
                    section.HeaderOverrideOutline.BorderThickness = 0f;
                }
                if (section.RevertButtonHost != null) {
                    section.RevertButtonHost.Enabled = false;
                }
                section.RemoveButtonHost.Enabled = true;
                section.Rows.Clear();
                section.IsCollapsed = false;
                SectionPool.Add(section);
            }

            ActiveSections.Clear();
        }

        /// <summary>
        /// Adds editable rows for the properties of a component.
        /// </summary>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        void AddPropertyRows(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId) {
            List<ReflectedComponentPropertyDescriptor> descriptors = DescriptorBuilder.Build(editableComponent.GetType());
            for (int index = 0; index < descriptors.Count; index++) {
                ReflectedComponentPropertyDescriptor descriptor = descriptors[index];
                if (descriptor.IsCustomEditor) {
                    AddCustomEditorRows(section, commonComponent, editableComponent, saveComponent, platformId, descriptor);
                    continue;
                }

                ComponentPropertyRow row = AcquireRow(descriptor.RowKind);
                BindPropertyRow(row, commonComponent, editableComponent, saveComponent, platformId, descriptor);
                UpdateRowValue(row);
                section.Rows.Add(row);
                ActiveRows.Add(row);
            }
        }

        /// <summary>
        /// Adds the rows required by one provider-backed custom property editor.
        /// </summary>
        /// <param name="section">Section receiving the rows.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        void AddCustomEditorRows(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (!descriptor.IsCustomEditor) {
                throw new InvalidOperationException("Custom editor rows require a provider-backed descriptor.");
            }

            ComponentPropertyRow sectionRow = AcquireRow(ComponentPropertyRowKind.CustomSection);
            BindCustomSectionRow(sectionRow, commonComponent, editableComponent, saveComponent, platformId, descriptor);
            section.Rows.Add(sectionRow);
            ActiveRows.Add(sectionRow);
            if (!sectionRow.IsExpanded) {
                return;
            }

            if (string.Equals(sectionRow.CustomEditorTypeId, CameraClearSettingsPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                AddCameraClearSettingsRows(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow);
                return;
            }
            if (string.Equals(sectionRow.CustomEditorTypeId, SceneMapPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                AddSceneMapRows(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow);
            }
        }

        /// <summary>
        /// Associates a row with a component property and resets the label text.
        /// </summary>
        /// <param name="row">Row to bind.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Reflected property descriptor.</param>
        void BindPropertyRow(
            ComponentPropertyRow row,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor) {
            row.CommonComponent = commonComponent;
            row.TargetComponent = editableComponent;
            row.SaveComponent = saveComponent;
            row.EditingPlatformId = platformId;
            row.Property = descriptor.Property;
            row.ValueType = descriptor.Property.PropertyType;
            row.Label.Text = descriptor.DisplayName;
            row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            row.Entity.Enabled = true;
        }

        /// <summary>
        /// Associates one custom section row with its provider-backed property descriptor.
        /// </summary>
        /// <param name="row">Row to bind.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        void BindCustomSectionRow(
            ComponentPropertyRow row,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            row.CommonComponent = commonComponent;
            row.TargetComponent = editableComponent;
            row.SaveComponent = saveComponent;
            row.EditingPlatformId = platformId;
            row.Property = descriptor.Property;
            row.ValueType = descriptor.Property.PropertyType;
            row.CustomEditorTypeId = descriptor.CustomEditor.EditorTypeId;
            row.NestedMemberName = null;
            row.IndentLevel = 0;
            row.Label.Text = descriptor.DisplayName;
            row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            row.Entity.Enabled = true;
            row.IsExpanded = CustomEditorExpandedStates.TryGetValue(BuildCustomEditorStateKey(editableComponent, descriptor.Property), out bool isExpanded) && isExpanded;
            UpdateCustomSectionVisual(row, false);
        }

        /// <summary>
        /// Adds the nested rows used by the camera clear settings custom editor.
        /// </summary>
        /// <param name="section">Section receiving the nested rows.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        /// <param name="sectionRow">Top-level custom section row.</param>
        void AddCameraClearSettingsRows(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow) {
            AddNestedBooleanRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Color Enabled", nameof(CameraClearSettings.ClearColorEnabled));
            AddNestedVector4Row(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Color", nameof(CameraClearSettings.ClearColor));
            AddNestedBooleanRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Depth Enabled", nameof(CameraClearSettings.ClearDepthEnabled));
            AddNestedScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Depth", nameof(CameraClearSettings.ClearDepth), typeof(float));
            AddNestedBooleanRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Stencil Enabled", nameof(CameraClearSettings.ClearStencilEnabled));
            AddNestedScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Clear Stencil", nameof(CameraClearSettings.ClearStencil), typeof(byte));
        }

        /// <summary>
        /// Adds the nested rows used by the scene-map custom editor.
        /// </summary>
        /// <param name="section">Section receiving the rows.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        /// <param name="sectionRow">Top-level custom section row.</param>
        void AddSceneMapRows(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow) {
            if (editableComponent is not SceneMapComponent sceneMapComponent) {
                throw new InvalidOperationException("Scene map rows require a SceneMapComponent target.");
            }

            List<KeyValuePair<string, string>> mappings = sceneMapComponent.Mappings.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();
            for (int index = 0; index < mappings.Count; index++) {
                KeyValuePair<string, string> mapping = mappings[index];
                string displayIndex = (index + 1).ToString(CultureInfo.InvariantCulture);
                AddSceneMapScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Source " + displayIndex, SceneMapSourceEntryMemberName, mapping.Key, false);
                AddSceneMapScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "Target " + displayIndex, SceneMapTargetEntryMemberName, mapping.Key, true);
            }

            AddSceneMapScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "New Source", SceneMapDraftSourceMemberName, string.Empty, false);
            AddSceneMapScalarRow(section, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, "New Target", SceneMapDraftTargetMemberName, string.Empty, true);
        }

        /// <summary>
        /// Adds one scalar row inside the scene-map custom section.
        /// </summary>
        /// <param name="section">Section receiving the row.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        /// <param name="sectionRow">Owning scene-map section row.</param>
        /// <param name="label">Visible row label.</param>
        /// <param name="nestedMemberName">Stable nested member identifier.</param>
        /// <param name="entryKey">Stable dictionary key associated with the row when it edits an existing entry.</param>
        /// <param name="usesActionButton">True when the row should expose an action button.</param>
        void AddSceneMapScalarRow(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName,
            string entryKey,
            bool usesActionButton) {
            ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Scalar);
            BindSceneMapScalarRow(row, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, label, nestedMemberName, entryKey);
            if (usesActionButton) {
                EnsureSceneMapActionButton(row);
                ConfigureSceneMapActionButton(row);
            }

            UpdateRowValue(row);
            section.Rows.Add(row);
            ActiveRows.Add(row);
        }

        /// <summary>
        /// Associates one scalar row with the scene-map custom editor metadata.
        /// </summary>
        /// <param name="row">Row to bind.</param>
        /// <param name="commonComponent">Common live component attached to the entity.</param>
        /// <param name="editableComponent">Effective editable component shown for the current platform.</param>
        /// <param name="saveComponent">Hidden save component attached to the owning entity.</param>
        /// <param name="platformId">Platform context currently shown by the inspector.</param>
        /// <param name="descriptor">Provider-backed property descriptor.</param>
        /// <param name="sectionRow">Owning scene-map section row.</param>
        /// <param name="label">Visible row label.</param>
        /// <param name="nestedMemberName">Stable nested member identifier.</param>
        /// <param name="entryKey">Stable dictionary key associated with the row when it edits an existing entry.</param>
        void BindSceneMapScalarRow(
            ComponentPropertyRow row,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName,
            string entryKey) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (sectionRow == null) {
                throw new ArgumentNullException(nameof(sectionRow));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Label must be provided.", nameof(label));
            }
            if (string.IsNullOrWhiteSpace(nestedMemberName)) {
                throw new ArgumentException("Nested member name must be provided.", nameof(nestedMemberName));
            }

            row.CommonComponent = commonComponent;
            row.TargetComponent = editableComponent;
            row.SaveComponent = saveComponent;
            row.EditingPlatformId = platformId;
            row.Property = descriptor.Property;
            row.ValueType = typeof(string);
            row.CustomEditorTypeId = sectionRow.CustomEditorTypeId;
            row.NestedMemberName = nestedMemberName;
            row.CustomEditorEntryKey = entryKey ?? string.Empty;
            row.IndentLevel = 1;
            row.Label.Text = label;
            row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            row.Entity.Enabled = true;
        }

        /// <summary>
        /// Adds one nested boolean row inside the camera clear settings section.
        /// </summary>
        void AddNestedBooleanRow(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName) {
            ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Boolean);
            BindNestedPropertyRow(row, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, label, nestedMemberName, typeof(bool));
            UpdateRowValue(row);
            section.Rows.Add(row);
            ActiveRows.Add(row);
        }

        /// <summary>
        /// Adds one nested scalar row inside the camera clear settings section.
        /// </summary>
        void AddNestedScalarRow(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName,
            Type valueType) {
            ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Scalar);
            BindNestedPropertyRow(row, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, label, nestedMemberName, valueType);
            UpdateRowValue(row);
            section.Rows.Add(row);
            ActiveRows.Add(row);
        }

        /// <summary>
        /// Adds one nested Vector4 row inside the camera clear settings section.
        /// </summary>
        void AddNestedVector4Row(
            ComponentSectionView section,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName) {
            ComponentPropertyRow row = AcquireRow(ComponentPropertyRowKind.Vector4);
            BindNestedPropertyRow(row, commonComponent, editableComponent, saveComponent, platformId, descriptor, sectionRow, label, nestedMemberName, typeof(float4));
            UpdateRowValue(row);
            section.Rows.Add(row);
            ActiveRows.Add(row);
        }

        /// <summary>
        /// Associates one nested provider-backed row with the owning property and nested member metadata.
        /// </summary>
        void BindNestedPropertyRow(
            ComponentPropertyRow row,
            Component commonComponent,
            Component editableComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            ReflectedComponentPropertyDescriptor descriptor,
            ComponentPropertyRow sectionRow,
            string label,
            string nestedMemberName,
            Type valueType) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }
            if (sectionRow == null) {
                throw new ArgumentNullException(nameof(sectionRow));
            }
            if (string.IsNullOrWhiteSpace(label)) {
                throw new ArgumentException("Label must be provided.", nameof(label));
            }
            if (string.IsNullOrWhiteSpace(nestedMemberName)) {
                throw new ArgumentException("Nested member name must be provided.", nameof(nestedMemberName));
            }
            if (valueType == null) {
                throw new ArgumentNullException(nameof(valueType));
            }

            row.CommonComponent = commonComponent;
            row.TargetComponent = editableComponent;
            row.SaveComponent = saveComponent;
            row.EditingPlatformId = platformId;
            row.Property = descriptor.Property;
            row.ValueType = valueType;
            row.CustomEditorTypeId = sectionRow.CustomEditorTypeId;
            row.NestedMemberName = nestedMemberName;
            row.IndentLevel = 1;
            row.Label.Text = label;
            row.Label.Color = ThemeManager.Colors.InputForegroundPrimary;
            row.Entity.Enabled = true;
        }

        /// <summary>
        /// Updates one row according to its configured row kind.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateRowValue(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            switch (row.Kind) {
                case ComponentPropertyRowKind.CustomSection:
                    UpdateCustomSectionRow(row);
                    break;
                case ComponentPropertyRowKind.Vector3:
                    UpdateVectorRow(row);
                    break;
                case ComponentPropertyRowKind.Vector4:
                    UpdateVector4Row(row);
                    break;
                case ComponentPropertyRowKind.Material:
                    UpdateMaterialRow(row);
                    break;
                case ComponentPropertyRowKind.Font:
                    UpdateFontRow(row);
                    break;
                case ComponentPropertyRowKind.Model:
                    UpdateModelRow(row);
                    break;
                case ComponentPropertyRowKind.Boolean:
                    UpdateBooleanRow(row);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    UpdateScalarRow(row);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    UpdateReadOnlyRow(row);
                    break;
            }

            RefreshRowOverrideChrome(row);
        }

        /// <summary>
        /// Refreshes the row-level override marker and revert button for the supplied row.
        /// </summary>
        /// <param name="row">Row whose override chrome should be updated.</param>
        void RefreshRowOverrideChrome(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            bool isOverrideActive = false;
            if (CanRowShowOverrideChrome(row)) {
                string propertyPath = BuildRowPropertyPath(row);
                if (!string.IsNullOrWhiteSpace(propertyPath)) {
                    isOverrideActive = PlatformEditingService.IsPropertyOverrideActive(
                        row.CommonComponent,
                        row.TargetComponent,
                        row.SaveComponent,
                        row.EditingPlatformId,
                        propertyPath);
                }
            }

            row.IsOverrideActive = isOverrideActive;
            if (row.RevertButtonHost != null) {
                row.RevertButtonHost.Enabled = isOverrideActive;
            }
            if (row.OverrideOutline != null) {
                row.OverrideOutline.BorderThickness = isOverrideActive ? OverrideOutlineThickness : 0f;
            }
        }

        /// <summary>
        /// Returns the width reserved for a row-level revert button when the row is currently overridden.
        /// </summary>
        /// <param name="row">Row whose revert button width should be resolved.</param>
        /// <returns>Reserved width in pixels, or zero when the button should remain hidden.</returns>
        int GetVisibleRevertButtonWidth(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            return row.IsOverrideActive ? RevertButtonWidth : 0;
        }

        /// <summary>
        /// Returns whether the supplied row can show row-level override chrome.
        /// </summary>
        /// <param name="row">Row to classify.</param>
        /// <returns>True when the row represents an editable property on a non-common platform tab.</returns>
        bool CanRowShowOverrideChrome(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            if (row.CommonComponent == null || row.TargetComponent == null || row.SaveComponent == null) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(row.EditingPlatformId)
                || string.Equals(row.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            if (row.Kind == ComponentPropertyRowKind.CustomSection || row.Kind == ComponentPropertyRowKind.Header || row.Kind == ComponentPropertyRowKind.ReadOnly) {
                return false;
            }

            return row.Property != null;
        }

        /// <summary>
        /// Builds the stable property path represented by one visible row.
        /// </summary>
        /// <param name="row">Row whose stable property path should be returned.</param>
        /// <returns>Stable property path for the row, or null when the row does not represent a revertible property.</returns>
        string BuildRowPropertyPath(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.Property == null) {
                return null;
            }
            if (string.IsNullOrWhiteSpace(row.NestedMemberName)) {
                return row.Property.Name;
            }
            if (string.Equals(row.CustomEditorTypeId, SceneMapPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                return row.Property.Name;
            }

            return string.Concat(row.Property.Name, ".", row.NestedMemberName);
        }

        /// <summary>
        /// Handles one row-level revert request by clearing the explicit platform override marker and rebuilding the visible platform view.
        /// </summary>
        /// <param name="row">Row whose value should return to common behavior.</param>
        void HandleRowRevertClicked(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (!CanRowShowOverrideChrome(row)) {
                return;
            }

            string propertyPath = BuildRowPropertyPath(row);
            if (string.IsNullOrWhiteSpace(propertyPath)) {
                return;
            }

            PlatformEditingService.ClearPropertyOverride(row.CommonComponent, row.SaveComponent, row.EditingPlatformId, propertyPath);
            RebuildCurrentComponentView();
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Rebuilds the current component inspector view while preserving the last known layout bounds.
        /// </summary>
        void RebuildCurrentComponentView() {
            if (CurrentEntity == null) {
                return;
            }

            ShowComponents(CurrentEntity, CurrentPlatformId);
            if (HasLayoutState) {
                UpdateLayout(LastLayoutLeft, LastLayoutTop, LastLayoutWidth);
            }
        }

        /// <summary>
        /// Updates one custom section row to reflect its current expansion state.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateCustomSectionRow(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            UpdateCustomSectionVisual(row, false);
        }

        /// <summary>
        /// Updates a Vector3 row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateVectorRow(ComponentPropertyRow row) {
            if (!TryGetVectorValue(row, out float3 value)) {
                SetVectorFields(row, 0.0, 0.0, 0.0);
                return;
            }

            SetVectorFields(row, value.X, value.Y, value.Z);
        }

        /// <summary>
        /// Updates a scalar row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateScalarRow(ComponentPropertyRow row) {
            object rawValue = GetRowValue(row);
            string text = FormatScalarValue(rawValue);
            UpdateScalarField(row, text);
        }

        /// <summary>
        /// Updates a boolean row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateBooleanRow(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            bool isChecked = false;
            object rawValue = GetRowValue(row);
            if (rawValue is bool boolValue) {
                isChecked = boolValue;
            }

            UpdateBooleanField(row, isChecked);
        }

        /// <summary>
        /// Updates a read-only row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateReadOnlyRow(ComponentPropertyRow row) {
            object rawValue = GetRowValue(row);
            row.ValueText.Text = rawValue == null ? string.Empty : rawValue.ToString();
        }

        /// <summary>
        /// Updates a material row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateMaterialRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is RuntimeMaterial material) {
                if (MaterialLabels.TryGetValue(material, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(material.Id) ? EmptyAssetLabel : material.Id;
            } else {
                row.ValueText.Text = EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Updates a font row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateFontRow(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is FontAsset font) {
                if (FontLabels.TryGetValue(font, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(font.FontInfo?.Name) ? EmptyAssetLabel : font.FontInfo.Name;
            } else {
                row.ValueText.Text = EmptyAssetLabel;
            }
        }

        /// <summary>
        /// Updates a model row with the component property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateModelRow(ComponentPropertyRow row) {
            object rawValue = GetRowValue(row);
            if (rawValue is RuntimeModel model) {
                if (ModelLabels.TryGetValue(model, out string label) && !string.IsNullOrWhiteSpace(label)) {
                    row.ValueText.Text = label;
                    return;
                }

                row.ValueText.Text = string.IsNullOrWhiteSpace(model.Id) ? AssignedAssetLabel : model.Id;
                return;
            }

            row.ValueText.Text = EmptyAssetLabel;
        }

        /// <summary>
        /// Updates a Vector4 row with the current nested property value.
        /// </summary>
        /// <param name="row">Row to update.</param>
        void UpdateVector4Row(ComponentPropertyRow row) {
            if (!TryGetVector4Value(row, out float4 value)) {
                SetVector4Fields(row, 0.0, 0.0, 0.0, 0.0);
                return;
            }

            SetVector4Fields(row, value.X, value.Y, value.Z, value.W);
        }

        /// <summary>
        /// Retrieves the current property value from a row.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <returns>Property value or null.</returns>
        object GetPropertyValue(ComponentPropertyRow row) {
            if (row.TargetComponent == null || row.Property == null) {
                return null;
            }

            if (TryGetSuppressedCameraPropertyValue(row, out object suppressedValue)) {
                return suppressedValue;
            }

            return row.Property.GetValue(row.TargetComponent);
        }

        /// <summary>
        /// Retrieves the effective editable value represented by one row.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <returns>Effective row value or null.</returns>
        object GetRowValue(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            if (!string.IsNullOrWhiteSpace(row.NestedMemberName)
                && string.Equals(row.CustomEditorTypeId, CameraClearSettingsPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                CameraClearSettings settings = ReadCameraClearSettings(row);
                return ReadCameraClearSettingsNestedValue(row, settings);
            }
            if (!string.IsNullOrWhiteSpace(row.NestedMemberName)
                && string.Equals(row.CustomEditorTypeId, SceneMapPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                return ReadSceneMapRowValue(row);
            }

            return GetPropertyValue(row);
        }

        /// <summary>
        /// Reads the effective scalar value represented by one scene-map custom-editor row.
        /// </summary>
        /// <param name="row">Row being queried.</param>
        /// <returns>Effective scene-map row value.</returns>
        object ReadSceneMapRowValue(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            Component componentKey = ResolveSceneMapDraftComponentKey(row);
            if (string.Equals(row.NestedMemberName, SceneMapSourceEntryMemberName, StringComparison.Ordinal)) {
                return row.CustomEditorEntryKey ?? string.Empty;
            }
            if (string.Equals(row.NestedMemberName, SceneMapTargetEntryMemberName, StringComparison.Ordinal)) {
                if (row.TargetComponent is SceneMapComponent sceneMapComponent
                    && !string.IsNullOrWhiteSpace(row.CustomEditorEntryKey)
                    && sceneMapComponent.Mappings.TryGetValue(row.CustomEditorEntryKey, out string targetSceneId)) {
                    return targetSceneId;
                }

                return string.Empty;
            }
            if (string.Equals(row.NestedMemberName, SceneMapDraftSourceMemberName, StringComparison.Ordinal)) {
                if (componentKey != null && SceneMapDraftSourcesByComponent.TryGetValue(componentKey, out string draftSourceSceneId)) {
                    return draftSourceSceneId;
                }

                return string.Empty;
            }
            if (string.Equals(row.NestedMemberName, SceneMapDraftTargetMemberName, StringComparison.Ordinal)) {
                if (componentKey != null && SceneMapDraftTargetsByComponent.TryGetValue(componentKey, out string draftTargetSceneId)) {
                    return draftTargetSceneId;
                }

                return string.Empty;
            }

            throw new InvalidOperationException($"Unsupported scene map row member '{row.NestedMemberName}'.");
        }

        /// <summary>
        /// Attempts to read a Vector3 value from the row property.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <param name="value">Vector value when available.</param>
        /// <returns>True when a Vector3 value was read.</returns>
        bool TryGetVectorValue(ComponentPropertyRow row, out float3 value) {
            value = float3.Zero;
            object rawValue = GetRowValue(row);
            if (rawValue is float3 vector) {
                value = vector;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to read a Vector4 value from the row property.
        /// </summary>
        /// <param name="row">Row to query.</param>
        /// <param name="value">Vector4 value when available.</param>
        /// <returns>True when a Vector4 value was read.</returns>
        bool TryGetVector4Value(ComponentPropertyRow row, out float4 value) {
            value = float4.Identity;
            object rawValue = GetRowValue(row);
            if (rawValue is float4 vector) {
                value = vector;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the scalar field text and cache for a row.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="text">Text to apply.</param>
        void UpdateScalarField(ComponentPropertyRow row, string text) {
            if (row.ScalarField == null) {
                return;
            }

            IsSynchronizing = true;
            row.ScalarField.Text = text ?? string.Empty;
            row.ScalarCache = text ?? string.Empty;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Updates the checkbox state for a boolean row.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="isChecked">Checked state to apply.</param>
        void UpdateBooleanField(ComponentPropertyRow row, bool isChecked) {
            if (row.CheckBoxField == null) {
                return;
            }

            IsSynchronizing = true;
            row.CheckBoxField.IsChecked = isChecked;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Applies vector field text to the row and cache.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="x">X value.</param>
        /// <param name="y">Y value.</param>
        /// <param name="z">Z value.</param>
        void SetVectorFields(ComponentPropertyRow row, double x, double y, double z) {
            if (row.VectorFields == null || row.VectorCache == null) {
                return;
            }

            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);

            IsSynchronizing = true;
            row.VectorFields[0].Text = xText;
            row.VectorFields[1].Text = yText;
            row.VectorFields[2].Text = zText;
            row.VectorCache[0] = xText;
            row.VectorCache[1] = yText;
            row.VectorCache[2] = zText;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Applies Vector4 field text to the row and cache.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="x">X value.</param>
        /// <param name="y">Y value.</param>
        /// <param name="z">Z value.</param>
        /// <param name="w">W value.</param>
        void SetVector4Fields(ComponentPropertyRow row, double x, double y, double z, double w) {
            if (row.Vector4Fields == null || row.Vector4Cache == null) {
                return;
            }

            string xText = FormatDouble(x);
            string yText = FormatDouble(y);
            string zText = FormatDouble(z);
            string wText = FormatDouble(w);

            IsSynchronizing = true;
            row.Vector4Fields[0].Text = xText;
            row.Vector4Fields[1].Text = yText;
            row.Vector4Fields[2].Text = zText;
            row.Vector4Fields[3].Text = wText;
            row.Vector4Cache[0] = xText;
            row.Vector4Cache[1] = yText;
            row.Vector4Cache[2] = zText;
            row.Vector4Cache[3] = wText;
            IsSynchronizing = false;
        }

        /// <summary>
        /// Formats a scalar value for display.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted text.</returns>
        string FormatScalarValue(object value) {
            if (value == null) {
                return string.Empty;
            }

            if (value is float floatValue) {
                return FormatDouble(floatValue);
            }
            if (value is double doubleValue) {
                return FormatDouble(doubleValue);
            }
            if (value is bool boolValue) {
                return boolValue ? "True" : "False";
            }
            if (value is IFormattable formattable) {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        /// <summary>
        /// Formats a double value using invariant culture.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted string.</returns>
        string FormatDouble(double value) {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Attempts to parse a Vector3 from the row fields.
        /// </summary>
        /// <param name="row">Row to parse.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <returns>True when all fields parse successfully.</returns>
        bool TryReadVector(ComponentPropertyRow row, out double x, out double y, out double z) {
            x = 0.0;
            y = 0.0;
            z = 0.0;

            if (row.VectorFields == null || row.VectorFields.Length < 3) {
                return false;
            }

            if (!TryReadNumber(row.VectorFields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(row.VectorFields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(row.VectorFields[2].Text, out z)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to parse a Vector4 from the row fields.
        /// </summary>
        /// <param name="row">Row to parse.</param>
        /// <param name="x">Parsed X value.</param>
        /// <param name="y">Parsed Y value.</param>
        /// <param name="z">Parsed Z value.</param>
        /// <param name="w">Parsed W value.</param>
        /// <returns>True when all fields parse successfully.</returns>
        bool TryReadVector4(ComponentPropertyRow row, out double x, out double y, out double z, out double w) {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            w = 0.0;

            if (row.Vector4Fields == null || row.Vector4Fields.Length < 4) {
                return false;
            }

            if (!TryReadNumber(row.Vector4Fields[0].Text, out x)) {
                return false;
            }
            if (!TryReadNumber(row.Vector4Fields[1].Text, out y)) {
                return false;
            }
            if (!TryReadNumber(row.Vector4Fields[2].Text, out z)) {
                return false;
            }
            if (!TryReadNumber(row.Vector4Fields[3].Text, out w)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether two Vector4 values contain the same components.
        /// </summary>
        /// <param name="left">Left value.</param>
        /// <param name="right">Right value.</param>
        /// <returns>True when all four components match exactly.</returns>
        bool AreFloat4ValuesEqual(float4 left, float4 right) {
            return left.X == right.X
                && left.Y == right.Y
                && left.Z == right.Z
                && left.W == right.W;
        }

        /// <summary>
        /// Parses a numeric value using invariant culture.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="value">Parsed numeric value.</param>
        /// <returns>True when parsing succeeds.</returns>
        bool TryReadNumber(string text, out double value) {
            if (string.IsNullOrWhiteSpace(text)) {
                value = 0.0;
                return false;
            }

            return double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// Handles submit events for vector fields.
        /// </summary>
        /// <param name="field">Submitted text box.</param>
        void HandleVectorSubmitted(TextBoxComponent field) {
            if (IsSynchronizing) {
                return;
            }

            if (!VectorFieldRows.TryGetValue(field, out ComponentPropertyRow row)) {
                return;
            }

            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            if (!TryReadVector(row, out double x, out double y, out double z)) {
                return;
            }

            var value = new float3((float)x, (float)y, (float)z);
            if (GetRowValue(row) is float3 currentValue && currentValue == value) {
                SetVectorFields(row, x, y, z);
                return;
            }

            SetRowValue(row, value);
            SetVectorFields(row, x, y, z);
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Handles submit events for Vector4 fields.
        /// </summary>
        /// <param name="field">Submitted text box.</param>
        void HandleVector4Submitted(TextBoxComponent field) {
            if (IsSynchronizing) {
                return;
            }

            if (!Vector4FieldRows.TryGetValue(field, out ComponentPropertyRow row)) {
                return;
            }

            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            if (!TryReadVector4(row, out double x, out double y, out double z, out double w)) {
                return;
            }

            var value = new float4((float)x, (float)y, (float)z, (float)w);
            if (GetRowValue(row) is float4 currentValue && AreFloat4ValuesEqual(currentValue, value)) {
                SetVector4Fields(row, x, y, z, w);
                return;
            }

            SetRowValue(row, value);
            SetVector4Fields(row, x, y, z, w);
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Handles submit events for scalar fields.
        /// </summary>
        /// <param name="field">Submitted text box.</param>
        void HandleScalarSubmitted(TextBoxComponent field) {
            if (IsSynchronizing) {
                return;
            }

            if (!ScalarFieldRows.TryGetValue(field, out ComponentPropertyRow row)) {
                return;
            }

            if (row.TargetComponent == null || row.Property == null) {
                return;
            }
            if (string.Equals(row.CustomEditorTypeId, SceneMapPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                HandleSceneMapScalarSubmitted(row, field.Text);
                return;
            }

            Type targetType = row.ValueType ?? row.Property.PropertyType;
            if (!TryParseScalar(field.Text, targetType, out object parsed)) {
                UpdateScalarField(row, row.ScalarCache);
                return;
            }

            object currentValue = GetRowValue(row);
            if (Equals(currentValue, parsed)) {
                UpdateScalarField(row, FormatScalarValue(parsed));
                return;
            }

            SetRowValue(row, parsed);
            UpdateScalarField(row, FormatScalarValue(parsed));
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Handles submit events for scene-map scalar rows.
        /// </summary>
        /// <param name="row">Row that owns the submitted scalar field.</param>
        /// <param name="text">Submitted text value.</param>
        void HandleSceneMapScalarSubmitted(ComponentPropertyRow row, string text) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            EnsureEditableComponentForRow(row);
            if (row.TargetComponent is not SceneMapComponent sceneMapComponent) {
                return;
            }

            string submittedText = text ?? string.Empty;
            if (string.Equals(row.NestedMemberName, SceneMapDraftSourceMemberName, StringComparison.Ordinal)) {
                SetSceneMapDraftValue(SceneMapDraftSourcesByComponent, ResolveSceneMapDraftComponentKey(row), submittedText);
                UpdateScalarField(row, submittedText);
                return;
            }
            if (string.Equals(row.NestedMemberName, SceneMapDraftTargetMemberName, StringComparison.Ordinal)) {
                SetSceneMapDraftValue(SceneMapDraftTargetsByComponent, ResolveSceneMapDraftComponentKey(row), submittedText);
                UpdateScalarField(row, submittedText);
                return;
            }
            if (string.IsNullOrWhiteSpace(submittedText)) {
                UpdateScalarField(row, row.ScalarCache);
                return;
            }
            if (string.Equals(row.NestedMemberName, SceneMapSourceEntryMemberName, StringComparison.Ordinal)) {
                if (string.Equals(row.CustomEditorEntryKey, submittedText, StringComparison.Ordinal)) {
                    UpdateScalarField(row, submittedText);
                    return;
                }
                if (sceneMapComponent.Mappings.ContainsKey(submittedText)) {
                    UpdateScalarField(row, row.CustomEditorEntryKey);
                    return;
                }
                if (!sceneMapComponent.Mappings.TryGetValue(row.CustomEditorEntryKey, out string targetSceneId)) {
                    UpdateScalarField(row, row.CustomEditorEntryKey);
                    return;
                }

                sceneMapComponent.Mappings.Remove(row.CustomEditorEntryKey);
                sceneMapComponent.Mappings.Add(submittedText, targetSceneId);
                row.CustomEditorEntryKey = submittedText;
                PersistPlatformOverrideIfNeeded(row);
                EditorSceneMutationService.MarkSceneMutated();
                RefreshCurrentView();
                return;
            }
            if (string.Equals(row.NestedMemberName, SceneMapTargetEntryMemberName, StringComparison.Ordinal)) {
                if (!sceneMapComponent.Mappings.TryGetValue(row.CustomEditorEntryKey, out string existingTargetSceneId)) {
                    UpdateScalarField(row, row.ScalarCache);
                    return;
                }
                if (string.Equals(existingTargetSceneId, submittedText, StringComparison.Ordinal)) {
                    UpdateScalarField(row, submittedText);
                    return;
                }

                sceneMapComponent.Mappings[row.CustomEditorEntryKey] = submittedText;
                PersistPlatformOverrideIfNeeded(row);
                UpdateScalarField(row, submittedText);
                EditorSceneMutationService.MarkSceneMutated();
                return;
            }

            UpdateScalarField(row, row.ScalarCache);
        }

        /// <summary>
        /// Handles change events for boolean checkbox rows.
        /// </summary>
        /// <param name="checkBox">Checkbox that raised the change event.</param>
        /// <param name="isChecked">New checked state.</param>
        void HandleBooleanCheckedChanged(CheckBoxComponent checkBox, bool isChecked) {
            if (IsSynchronizing) {
                return;
            }
            if (checkBox == null) {
                return;
            }

            ComponentPropertyRow row = FindBooleanRow(checkBox);
            if (row == null || row.TargetComponent == null || row.Property == null) {
                return;
            }

            object currentValue = GetRowValue(row);
            if (currentValue is bool currentBoolValue && currentBoolValue == isChecked) {
                UpdateBooleanField(row, isChecked);
                return;
            }

            SetRowValue(row, isChecked);
            UpdateBooleanField(row, isChecked);
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Finds the boolean property row that owns one checkbox.
        /// </summary>
        /// <param name="checkBox">Checkbox to resolve.</param>
        /// <returns>Owning row when found.</returns>
        ComponentPropertyRow FindBooleanRow(CheckBoxComponent checkBox) {
            if (checkBox == null) {
                throw new ArgumentNullException(nameof(checkBox));
            }

            for (int index = 0; index < ActiveRows.Count; index++) {
                ComponentPropertyRow row = ActiveRows[index];
                if (ReferenceEquals(row.CheckBoxField, checkBox)) {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to parse a scalar value from text.
        /// </summary>
        /// <param name="text">Text to parse.</param>
        /// <param name="targetType">Target property type.</param>
        /// <param name="value">Parsed value when successful.</param>
        /// <returns>True when parsing succeeds.</returns>
        bool TryParseScalar(string text, Type targetType, out object value) {
            value = null;
            if (targetType == typeof(string)) {
                value = text ?? string.Empty;
                return true;
            }

            if (targetType == typeof(bool)) {
                bool boolValue;
                if (bool.TryParse(text, out boolValue)) {
                    value = boolValue;
                    return true;
                }
                return false;
            }

            double numeric;
            if (!TryReadNumber(text, out numeric)) {
                return false;
            }

            if (targetType == typeof(float)) {
                value = (float)numeric;
                return true;
            }
            if (targetType == typeof(double)) {
                value = numeric;
                return true;
            }
            if (targetType == typeof(int)) {
                value = (int)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(long)) {
                value = (long)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(short)) {
                value = (short)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(byte)) {
                value = (byte)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(uint)) {
                value = (uint)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(ulong)) {
                value = (ulong)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(ushort)) {
                value = (ushort)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }
            if (targetType == typeof(sbyte)) {
                value = (sbyte)Math.Round(numeric, MidpointRounding.AwayFromZero);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Ensures the supplied row targets the effective editable component for its current platform context.
        /// </summary>
        /// <param name="row">Row whose editable component should be resolved.</param>
        void EnsureEditableComponentForRow(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.CommonComponent == null || row.TargetComponent == null) {
                return;
            }
            if (row.SaveComponent == null || string.IsNullOrWhiteSpace(row.EditingPlatformId)) {
                return;
            }
            if (string.Equals(row.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            if (!ReferenceEquals(row.TargetComponent, row.CommonComponent)) {
                return;
            }

            Component editableOverrideComponent = PlatformEditingService.EnsurePlatformOverrideComponent(row.CommonComponent, row.SaveComponent, row.EditingPlatformId);
            RetargetRowsForEditableComponent(row.CommonComponent, row.EditingPlatformId, editableOverrideComponent);
        }

        /// <summary>
        /// Retargets every visible row that edits the same common component and platform context.
        /// </summary>
        /// <param name="commonComponent">Common live component whose rows should be retargeted.</param>
        /// <param name="platformId">Platform context whose rows should be retargeted.</param>
        /// <param name="editableComponent">Effective editable component that should back those rows.</param>
        void RetargetRowsForEditableComponent(Component commonComponent, string platformId, Component editableComponent) {
            if (commonComponent == null) {
                throw new ArgumentNullException(nameof(commonComponent));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (editableComponent == null) {
                throw new ArgumentNullException(nameof(editableComponent));
            }

            for (int index = 0; index < ActiveRows.Count; index++) {
                ComponentPropertyRow activeRow = ActiveRows[index];
                if (!ReferenceEquals(activeRow.CommonComponent, commonComponent)) {
                    continue;
                }
                if (!string.Equals(activeRow.EditingPlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                activeRow.TargetComponent = editableComponent;
            }
        }

        /// <summary>
        /// Applies one effective row value back to the owning component property.
        /// </summary>
        /// <param name="row">Row being updated.</param>
        /// <param name="value">Value to apply.</param>
        void SetRowValue(ComponentPropertyRow row, object value) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            EnsureEditableComponentForRow(row);

            if (!string.IsNullOrWhiteSpace(row.NestedMemberName)
                && string.Equals(row.CustomEditorTypeId, CameraClearSettingsPropertyEditorProvider.EditorTypeId, StringComparison.Ordinal)) {
                CameraClearSettings settings = ReadCameraClearSettings(row);
                settings = WriteCameraClearSettingsNestedValue(row, settings, value);
                if (!TrySetSuppressedCameraPropertyValue(row, settings)) {
                    row.Property.SetValue(row.TargetComponent, settings);
                }
                PersistPlatformOverrideIfNeeded(row);
                return;
            }

            if (TrySetSuppressedCameraPropertyValue(row, value)) {
                PersistPlatformOverrideIfNeeded(row);
                return;
            }

            row.Property.SetValue(row.TargetComponent, value);
            PersistPlatformOverrideIfNeeded(row);
        }

        /// <summary>
        /// Persists the current row target back into the editor-only platform override metadata when required.
        /// </summary>
        /// <param name="row">Row whose editable component should be persisted.</param>
        void PersistPlatformOverrideIfNeeded(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.CommonComponent == null || row.TargetComponent == null) {
                return;
            }
            if (row.SaveComponent == null || string.IsNullOrWhiteSpace(row.EditingPlatformId)) {
                return;
            }
            if (string.Equals(row.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            string propertyPath = BuildRowPropertyPath(row);
            if (!string.IsNullOrWhiteSpace(propertyPath)) {
                PlatformEditingService.MarkPropertyOverride(row.CommonComponent, row.SaveComponent, row.EditingPlatformId, propertyPath);
            }
            PlatformEditingService.PersistPlatformOverride(row.CommonComponent, row.TargetComponent, row.SaveComponent, row.EditingPlatformId);
            RefreshRowOverrideChrome(row);
        }

        /// <summary>
        /// Attempts to resolve one authored suppressed-camera property value for a row.
        /// </summary>
        /// <param name="row">Row being read.</param>
        /// <param name="value">Authored property value when suppression metadata owns the property.</param>
        /// <returns>True when the row resolves through suppression metadata; otherwise false.</returns>
        bool TryGetSuppressedCameraPropertyValue(ComponentPropertyRow row, out object value) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.TargetComponent is CameraComponent cameraComponent) {
                return EditorSceneCameraSuppressionService.TryGetAuthoredPropertyValue(cameraComponent, row.Property.Name, out value);
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Attempts to write one authored suppressed-camera property value for a row.
        /// </summary>
        /// <param name="row">Row being updated.</param>
        /// <param name="value">Authored property value to store.</param>
        /// <returns>True when the row writes through suppression metadata; otherwise false.</returns>
        bool TrySetSuppressedCameraPropertyValue(ComponentPropertyRow row, object value) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.TargetComponent is CameraComponent cameraComponent) {
                return EditorSceneCameraSuppressionService.TrySetAuthoredPropertyValue(cameraComponent, row.Property.Name, value);
            }

            return false;
        }

        /// <summary>
        /// Reads the camera clear settings value represented by one provider-backed row.
        /// </summary>
        /// <param name="row">Row whose owning property should be read.</param>
        /// <returns>Current camera clear settings value.</returns>
        CameraClearSettings ReadCameraClearSettings(ComponentPropertyRow row) {
            object rawValue = GetPropertyValue(row);
            if (rawValue is CameraClearSettings settings) {
                return settings;
            }

            throw new InvalidOperationException("Camera clear settings row requires a CameraClearSettings value.");
        }

        /// <summary>
        /// Reads one nested camera clear settings member from the supplied struct value.
        /// </summary>
        /// <param name="row">Row whose nested member should be read.</param>
        /// <param name="settings">Current camera clear settings value.</param>
        /// <returns>Nested member value.</returns>
        object ReadCameraClearSettingsNestedValue(ComponentPropertyRow row, CameraClearSettings settings) {
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearColorEnabled), StringComparison.Ordinal)) {
                return settings.ClearColorEnabled;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearColor), StringComparison.Ordinal)) {
                return settings.ClearColor;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearDepthEnabled), StringComparison.Ordinal)) {
                return settings.ClearDepthEnabled;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearDepth), StringComparison.Ordinal)) {
                return settings.ClearDepth;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearStencilEnabled), StringComparison.Ordinal)) {
                return settings.ClearStencilEnabled;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearStencil), StringComparison.Ordinal)) {
                return settings.ClearStencil;
            }

            throw new InvalidOperationException($"Unsupported camera clear settings member '{row.NestedMemberName}'.");
        }

        /// <summary>
        /// Writes one nested camera clear settings member into the supplied struct value.
        /// </summary>
        /// <param name="row">Row whose nested member should be written.</param>
        /// <param name="settings">Current camera clear settings value.</param>
        /// <param name="value">Nested value to assign.</param>
        /// <returns>Updated camera clear settings value.</returns>
        CameraClearSettings WriteCameraClearSettingsNestedValue(ComponentPropertyRow row, CameraClearSettings settings, object value) {
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearColorEnabled), StringComparison.Ordinal)) {
                settings.ClearColorEnabled = (bool)value;
                return settings;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearColor), StringComparison.Ordinal)) {
                settings.ClearColor = (float4)value;
                return settings;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearDepthEnabled), StringComparison.Ordinal)) {
                settings.ClearDepthEnabled = (bool)value;
                return settings;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearDepth), StringComparison.Ordinal)) {
                settings.ClearDepth = (float)value;
                return settings;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearStencilEnabled), StringComparison.Ordinal)) {
                settings.ClearStencilEnabled = (bool)value;
                return settings;
            }
            if (string.Equals(row.NestedMemberName, nameof(CameraClearSettings.ClearStencil), StringComparison.Ordinal)) {
                settings.ClearStencil = (byte)value;
                return settings;
            }

            throw new InvalidOperationException($"Unsupported camera clear settings member '{row.NestedMemberName}'.");
        }

        /// <summary>
        /// Requests the asset picker for a material field.
        /// </summary>
        /// <param name="row">Material row to update.</param>
        void RequestMaterialPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleMaterialPicked(row, entry), MaterialExtension);
        }

        /// <summary>
        /// Requests the asset picker for a font field.
        /// </summary>
        /// <param name="row">Font row to update.</param>
        void RequestFontPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleFontPicked(row, entry));
        }

        /// <summary>
        /// Applies a picked material asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleMaterialPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                EnsureEditableComponentForRow(row);
                RuntimeMaterial material = LoadMaterial(entry);
                row.Property.SetValue(row.TargetComponent, material);
                if (entry != null && material != null) {
                    MaterialLabels[material] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                PersistPlatformOverrideIfNeeded(row);
                UpdateMaterialRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Material pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies a picked font asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleFontPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                EnsureEditableComponentForRow(row);
                FontAsset font = LoadFont(entry);
                row.Property.SetValue(row.TargetComponent, font);
                if (entry != null && font != null) {
                    FontLabels[font] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                PersistPlatformOverrideIfNeeded(row);
                UpdateFontRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Font pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a runtime material from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Runtime material instance.</returns>
        RuntimeMaterial LoadMaterial(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
            }

            string extension = entry.Extension;
            if (!IsMaterialExtension(extension)) {
                throw new InvalidOperationException("Selected asset is not a material.");
            }

            MaterialAsset materialAsset = LoadMaterialAsset(entry.FullPath);
            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                return null;
            }

            ShaderAsset shaderAsset = LoadShaderAsset(materialAsset.ShaderAssetId);
            return Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
        }

        /// <summary>
        /// Loads a font asset from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Font asset instance.</returns>
        FontAsset LoadFont(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!IsFontExtension(entry.Extension)) {
                throw new InvalidOperationException("Selected asset is not a font.");
            }

            if (FileSystemFontResolver != null) {
                return FileSystemFontResolver.ResolveFontAsset(entry.FullPath);
            }

            return AssetContentManager.Load<FontAsset>(entry.FullPath, RuntimeContentProcessorIds.FontAsset);
        }

        /// <summary>
        /// Loads a material asset from disk.
        /// </summary>
        /// <param name="path">Path to the material asset.</param>
        /// <returns>Material asset instance.</returns>
        MaterialAsset LoadMaterialAsset(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Material path must be provided.", nameof(path));
            }

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            return settingsService.LoadMaterialAsset(path, ResolveActiveProjectPlatformId());
        }

        /// <summary>
        /// Resolves the preview platform used when file-backed material assets are previewed from the component inspector.
        /// </summary>
        /// <returns>Preferred preview platform identifier, or the active/first supported platform when Windows preview is unavailable.</returns>
        string ResolveActiveProjectPlatformId() {
            string projectRootPath = EditorProjectPaths.ProjectRoot;
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("The current project root must be initialized before file-backed materials can be loaded.");
            }

            EditorProjectPlatformsDocument platformsDocument = new EditorProjectPlatformsService(projectRootPath).Load();
            if (platformsDocument.SupportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported project platform must exist before file-backed materials can be loaded.");
            }

            for (int index = 0; index < platformsDocument.SupportedPlatforms.Count; index++) {
                if (string.Equals(platformsDocument.SupportedPlatforms[index], PreferredEditorPreviewPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return platformsDocument.SupportedPlatforms[index];
                }
            }

            string activePlatformId = new EditorProjectLocalSettingsService(projectRootPath, platformsDocument.SupportedPlatforms).LoadActivePlatform();
            if (!string.IsNullOrWhiteSpace(activePlatformId)) {
                return activePlatformId;
            }

            return platformsDocument.SupportedPlatforms[0];
        }

        /// <summary>
        /// Loads a shader asset from the shader cache.
        /// </summary>
        /// <param name="shaderId">Shader asset identifier.</param>
        /// <returns>Shader asset instance.</returns>
        ShaderAsset LoadShaderAsset(string shaderId) {
            if (string.IsNullOrWhiteSpace(shaderId)) {
                throw new InvalidOperationException("Material does not specify a shader asset id.");
            }

            return EditorShaderPackageService.LoadShaderAsset(shaderId);
        }

        /// <summary>
        /// Determines whether an extension represents a material asset.
        /// </summary>
        /// <param name="extension">Extension to test.</param>
        /// <returns>True when the extension is material-like.</returns>
        bool IsMaterialExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            return string.Equals(extension, MaterialExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an extension represents a font asset.
        /// </summary>
        /// <param name="extension">Extension to test.</param>
        /// <returns>True when the extension is font-like.</returns>
        bool IsFontExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            for (int index = 0; index < FontExtensions.Length; index++) {
                if (string.Equals(extension, FontExtensions[index], StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the vertical offset needed to center text using tight font metrics.
        /// </summary>
        /// <param name="containerHeight">Height of the container.</param>
        /// <param name="metrics">Tight font metrics.</param>
        /// <returns>Top offset for centered text.</returns>
        float GetTextTopOffset(int containerHeight, FontTightMetrics metrics) {
            double height = Math.Max(containerHeight, 1);
            double offset = (height - metrics.Height) * 0.5 - metrics.MinTop;
            return (float)Math.Round(offset);
        }

        /// <summary>
        /// Lays out a row using the provided width and height.
        /// </summary>
        /// <param name="row">Row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="top">Top offset for the row.</param>
        /// <param name="height">Row height.</param>
        void LayoutRow(ComponentPropertyRow row, int width, int top, int height) {
            int bodyWidth = Math.Max(0, width - SectionBodyPadding * 2);
            int indentOffset = row.IndentLevel * (SectionBodyPadding * 2);
            bodyWidth = Math.Max(0, bodyWidth - indentOffset);
            row.Entity.Position = new float3(SectionBodyPadding + indentOffset, top, 0.2f);
            if (row.OverrideOutline != null) {
                row.OverrideOutline.Size = new int2(Math.Max(1, bodyWidth), height);
            }

            int revertButtonWidth = GetVisibleRevertButtonWidth(row);
            int contentWidth = bodyWidth;
            if (revertButtonWidth > 0) {
                contentWidth = Math.Max(0, bodyWidth - revertButtonWidth - FieldSpacing);
                if (row.RevertButtonHost != null) {
                    float buttonY = (float)Math.Round((height - RevertButtonHeight) * 0.5);
                    row.RevertButtonHost.Position = new float3(contentWidth + FieldSpacing, buttonY, 0.2f);
                }
                if (row.RevertButton != null) {
                    row.RevertButton.SetSize(new int2(revertButtonWidth, RevertButtonHeight));
                }
            }

            int labelWidth = (int)Math.Round(contentWidth * LabelWidthRatio, MidpointRounding.AwayFromZero);
            if (labelWidth > contentWidth) {
                labelWidth = contentWidth;
            }

            var labelMetrics = Font.MeasureTight(row.Label.Text ?? string.Empty);
            float labelY = GetTextTopOffset(height, labelMetrics);
            row.LabelHost.Position = new float3(0, labelY, 0.2f);
            row.Label.Size = new int2(labelWidth, (int)Math.Ceiling(labelMetrics.Height));

            switch (row.Kind) {
                case ComponentPropertyRowKind.Header:
                    LayoutHeaderRow(row, contentWidth, height);
                    break;
                case ComponentPropertyRowKind.Vector3:
                    LayoutVectorRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Vector4:
                    LayoutVector4Row(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.CustomSection:
                    LayoutCustomSectionRow(row, contentWidth, height);
                    break;
                case ComponentPropertyRowKind.Material:
                    LayoutMaterialRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Font:
                    LayoutMaterialRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Model:
                    LayoutMaterialRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Boolean:
                    LayoutBooleanRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    LayoutScalarRow(row, contentWidth, height, labelWidth);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    LayoutReadOnlyRow(row, contentWidth, height, labelWidth);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Layouts one custom section row so it spans the available width like a nested header.
        /// </summary>
        /// <param name="row">Custom section row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        void LayoutCustomSectionRow(ComponentPropertyRow row, int width, int height) {
            if (row.HeaderBackground == null || row.HeaderInteractable == null) {
                return;
            }

            int safeWidth = Math.Max(1, width);
            row.HeaderBackground.Size = new int2(safeWidth, height);
            row.HeaderInteractable.Size = new int2(safeWidth, height);
            row.LabelHost.Position = new float3(SectionHeaderPadding, row.LabelHost.Position.Y, 0.2f);
            row.Label.Size = new int2(Math.Max(1, safeWidth - SectionHeaderPadding * 2), row.Label.Size.Y);
        }

        /// <summary>
        /// Layouts a header row to span the full width.
        /// </summary>
        /// <param name="row">Header row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        void LayoutHeaderRow(ComponentPropertyRow row, int width, int height) {
            row.Label.Size = new int2(Math.Max(0, width), row.Label.Size.Y);
        }

        /// <summary>
        /// Updates the placement of one component section title bar.
        /// </summary>
        /// <param name="section">Section whose title bar should be laid out.</param>
        /// <param name="width">Available content width.</param>
        /// <param name="top">Top offset for the section title bar.</param>
        void LayoutSectionHeader(ComponentSectionView section, int width, int top) {
            int safeWidth = Math.Max(1, width);
            section.Root.Enabled = true;
            section.Root.Position = new float3(0f, top, 0.2f);
            section.Background.Size = new int2(safeWidth, SectionHeaderHeight);
            if (section.HeaderOverrideOutline != null) {
                section.HeaderOverrideOutline.Size = new int2(safeWidth, SectionHeaderHeight);
            }
            section.HeaderInteractable.Size = new int2(safeWidth, SectionHeaderHeight);

            FontTightMetrics titleMetrics = Font.MeasureTight(section.TitleText.Text ?? string.Empty);
            float titleY = GetTextTopOffset(SectionHeaderHeight, titleMetrics);
            section.TitleHost.Position = new float3(SectionHeaderPadding, titleY, 0.2f);

            int visibleActionWidth = 0;
            if (section.RemoveButtonHost != null && section.RemoveButtonHost.Enabled) {
                visibleActionWidth = SectionRemoveButtonWidth + SectionHeaderButtonSpacing;
            } else if (section.RevertButtonHost != null && section.RevertButtonHost.Enabled) {
                visibleActionWidth = SectionRevertButtonWidth + SectionHeaderButtonSpacing;
            }
            int titleWidth = Math.Max(1, safeWidth - SectionHeaderPadding - visibleActionWidth - SectionHeaderPadding);
            section.TitleText.Size = new int2(titleWidth, Math.Max(1, (int)Math.Ceiling(Math.Max(titleMetrics.Height, Font.LineHeight))));

            if (section.RemoveButtonHost != null && section.RemoveButtonHost.Enabled) {
                int removeButtonX = Math.Max(0, safeWidth - SectionRemoveButtonWidth);
                section.RemoveButtonHost.Position = new float3(removeButtonX, 0f, 0.2f);
            }
            if (section.RevertButtonHost != null && section.RevertButtonHost.Enabled) {
                float revertButtonY = (float)Math.Round((SectionHeaderHeight - SectionRevertButtonHeight) * 0.5);
                int revertButtonX = Math.Max(0, safeWidth - SectionRevertButtonWidth);
                section.RevertButtonHost.Position = new float3(revertButtonX, revertButtonY, 0.2f);
            }
        }

        /// <summary>
        /// Layouts a Vector3 row with three text fields.
        /// </summary>
        /// <param name="row">Vector3 row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutVectorRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.VectorFieldHosts == null || row.VectorFields == null) {
                return;
            }

            int available = Math.Max(0, width - labelWidth - (FieldSpacing * 2));
            int fieldWidth = Math.Max(48, available / 3);
            float fieldY = (float)Math.Round((height - FieldHeight) * 0.5);

            int fieldX = labelWidth + FieldSpacing;
            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                row.VectorFieldHosts[i].Position = new float3(fieldX, fieldY, 0.2f);
                row.VectorFields[i].Size = new int2(fieldWidth, FieldHeight);
                fieldX += fieldWidth + FieldSpacing;
            }
        }

        /// <summary>
        /// Layouts a Vector4 row with four text fields.
        /// </summary>
        /// <param name="row">Vector4 row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutVector4Row(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.Vector4FieldHosts == null || row.Vector4Fields == null) {
                return;
            }

            int available = Math.Max(0, width - labelWidth - (FieldSpacing * 3));
            int fieldWidth = Math.Max(40, available / 4);
            float fieldY = (float)Math.Round((height - FieldHeight) * 0.5);

            int fieldX = labelWidth + FieldSpacing;
            for (int index = 0; index < row.Vector4FieldHosts.Length; index++) {
                row.Vector4FieldHosts[index].Position = new float3(fieldX, fieldY, 0.2f);
                row.Vector4Fields[index].Size = new int2(fieldWidth, FieldHeight);
                fieldX += fieldWidth + FieldSpacing;
            }
        }

        /// <summary>
        /// Layouts a material row with a value label and pick button.
        /// </summary>
        /// <param name="row">Material row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutMaterialRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null || row.ActionButtonHost == null) {
                return;
            }

            int buttonWidth = PickButtonWidth;
            int valueWidth = Math.Max(0, width - labelWidth - FieldSpacing - buttonWidth);
            var valueMetrics = Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));

            float buttonY = (float)Math.Round((height - PickButtonHeight) * 0.5);
            row.ActionButtonHost.Position = new float3(width - buttonWidth, buttonY, 0.2f);
        }

        /// <summary>
        /// Layouts a scalar row with a single text field.
        /// </summary>
        /// <param name="row">Scalar row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutScalarRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ScalarField == null) {
                return;
            }

            int actionButtonWidth = row.ActionButtonHost != null && row.ActionButton != null && row.ActionButtonHost.Enabled
                ? PickButtonWidth + FieldSpacing
                : 0;
            int fieldWidth = Math.Max(48, width - labelWidth - FieldSpacing - actionButtonWidth);
            float fieldY = (float)Math.Round((height - FieldHeight) * 0.5);
            row.ScalarField.Parent.Position = new float3(labelWidth + FieldSpacing, fieldY, 0.2f);
            row.ScalarField.Size = new int2(fieldWidth, FieldHeight);
            if (row.ActionButtonHost != null && row.ActionButton != null && row.ActionButtonHost.Enabled) {
                float buttonY = (float)Math.Round((height - PickButtonHeight) * 0.5);
                row.ActionButtonHost.Position = new float3(labelWidth + FieldSpacing + fieldWidth + FieldSpacing, buttonY, 0.2f);
            }
        }

        /// <summary>
        /// Layouts a boolean row with one checkbox field.
        /// </summary>
        /// <param name="row">Boolean row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutBooleanRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.CheckBoxHost == null || row.CheckBoxField == null) {
                return;
            }

            int checkBoxSize = Math.Max(16, FieldHeight);
            float checkBoxY = (float)Math.Round((height - checkBoxSize) * 0.5);
            row.CheckBoxHost.Position = new float3(labelWidth + FieldSpacing, checkBoxY, 0.2f);
            row.CheckBoxField.Size = new int2(checkBoxSize, checkBoxSize);
        }

        /// <summary>
        /// Layouts a read-only row with a value label.
        /// </summary>
        /// <param name="row">Read-only row to layout.</param>
        /// <param name="width">Available width.</param>
        /// <param name="height">Row height.</param>
        /// <param name="labelWidth">Width reserved for labels.</param>
        void LayoutReadOnlyRow(ComponentPropertyRow row, int width, int height, int labelWidth) {
            if (row.ValueHost == null || row.ValueText == null) {
                return;
            }

            int valueWidth = Math.Max(0, width - labelWidth - FieldSpacing);
            var valueMetrics = Font.MeasureTight(row.ValueText.Text ?? string.Empty);
            float valueY = GetTextTopOffset(height, valueMetrics);
            row.ValueHost.Position = new float3(labelWidth + FieldSpacing, valueY, 0.2f);
            row.ValueText.Size = new int2(valueWidth, (int)Math.Ceiling(valueMetrics.Height));
        }

        /// <summary>
        /// Sets the enabled state for all rows owned by one component section.
        /// </summary>
        /// <param name="section">Section whose rows should be toggled.</param>
        /// <param name="enabled">True when the rows should be visible.</param>
        void SetSectionRowsEnabled(ComponentSectionView section, bool enabled) {
            for (int i = 0; i < section.Rows.Count; i++) {
                section.Rows[i].Entity.Enabled = enabled;
            }
        }

        /// <summary>
        /// Retrieves or creates a component section for the supplied component.
        /// </summary>
        /// <param name="component">Component that will own the section.</param>
        /// <returns>Prepared component section.</returns>
        ComponentSectionView AcquireSection(
            Component component,
            Component commonComponent,
            EntitySaveComponent saveComponent,
            string platformId,
            bool isPlatformOnlyComponent,
            bool isRemovedOnPlatform) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ComponentSectionView section;
            if (SectionPool.Count > 0) {
                int index = SectionPool.Count - 1;
                section = SectionPool[index];
                SectionPool.RemoveAt(index);
            } else {
                section = CreateSection();
            }

            section.TargetComponent = component;
            section.CommonComponent = commonComponent;
            section.SaveComponent = saveComponent;
            section.EditingPlatformId = platformId;
            section.IsPlatformOnlyComponent = isPlatformOnlyComponent;
            section.IsRemovedOnPlatform = isRemovedOnPlatform;
            section.TitleText.Text = FormatComponentTitle(component.GetType().Name);
            section.IsCollapsed = !isRemovedOnPlatform
                && CollapsedStates.TryGetValue(component, out bool isCollapsed)
                && isCollapsed;
            section.Root.Enabled = true;
            section.Rows.Clear();
            RefreshSectionOverrideChrome(section);
            return section;
        }

        /// <summary>
        /// Creates one reusable component section with header chrome and remove button wiring.
        /// </summary>
        /// <returns>Prepared component section instance.</returns>
        ComponentSectionView CreateSection() {
            EditorEntity root = new EditorEntity();
            root.LayerMask = RootEntity.LayerMask;
            root.Position = float3.Zero;
            root.Enabled = false;
            RootEntity.AddChild(root);

            SpriteComponent background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, SectionHeaderHeight)
            };
            root.AddComponent(background);

            RoundedRectComponent headerOverrideOutline = new RoundedRectComponent {
                Radius = 0f,
                Corners = RoundedRectCorners.None,
                FillColor = new byte4(255, 255, 255, 0),
                BorderThickness = 0f,
                BorderColor = ResolveOverrideOutlineColor(),
                RenderOrder2D = RenderOrder2D.PanelForeground,
                Size = new int2(1, SectionHeaderHeight)
            };
            root.AddComponent(headerOverrideOutline);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(1, SectionHeaderHeight),
                HoverCursor = PointerCursorKind.Hand
            };
            root.AddComponent(interactable);

            EditorEntity titleHost = new EditorEntity {
                LayerMask = RootEntity.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            root.AddChild(titleHost);

            TextComponent titleText = new TextComponent {
                Font = Font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                Size = new int2(1, 1),
                RenderOrder2D = TextOrder
            };
            titleHost.AddComponent(titleText);

            EditorEntity removeButtonHost = new EditorEntity {
                LayerMask = RootEntity.LayerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            root.AddChild(removeButtonHost);

            EditorEntity revertButtonHost = new EditorEntity {
                LayerMask = RootEntity.LayerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };
            root.AddChild(revertButtonHost);

            ComponentSectionView section = null;
            section = new ComponentSectionView(
                root,
                background,
                interactable,
                titleHost,
                titleText,
                removeButtonHost,
                new ButtonComponent("X", new int2(SectionRemoveButtonWidth, SectionHeaderHeight), Font, () => HandleSectionRemoveClicked(section), 0f));

            removeButtonHost.AddComponent(section.RemoveButton);
            section.RemoveButton.SetRenderOrders(TextOrder, TextOrder);
            section.RemoveButton.UseHoverOnlyBackground();
            section.RemoveButton.UseSquareCorners();
            section.RemoveButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            ButtonComponent revertButton = new ButtonComponent("Revert", new int2(SectionRevertButtonWidth, SectionRevertButtonHeight), Font, () => HandleSectionRevertClicked(section), 1f);
            revertButton.SetRenderOrders(RenderOrder2D.PanelSurface, TextOrder);
            revertButton.UseHoverOnlyBackground();
            revertButton.UseSquareCorners();
            revertButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            revertButtonHost.AddComponent(revertButton);
            section.HeaderOverrideOutline = headerOverrideOutline;
            section.RevertButtonHost = revertButtonHost;
            section.RevertButton = revertButton;
            UpdateSectionHeaderVisual(section);
            interactable.CursorEvent += (pos, delta, state) => HandleSectionHeaderCursor(section, pos, state);

            return section;
        }

        /// <summary>
        /// Handles pointer interactions for one nested provider-backed section row.
        /// </summary>
        /// <param name="row">Row receiving the pointer interaction.</param>
        /// <param name="state">Current pointer state.</param>
        void HandleCustomSectionCursor(ComponentPropertyRow row, PointerInteraction state) {
            if (row == null) {
                return;
            }

            if (state == PointerInteraction.Hover) {
                UpdateCustomSectionVisual(row, true);
                return;
            }

            if (state == PointerInteraction.Leave) {
                UpdateCustomSectionVisual(row, false);
                return;
            }

            if (state != PointerInteraction.Press) {
                return;
            }

            HandleCustomSectionPressed(row);
        }

        /// <summary>
        /// Toggles one nested provider-backed section row and rebuilds the current inspected entity.
        /// </summary>
        /// <param name="row">Row to toggle.</param>
        void HandleCustomSectionPressed(ComponentPropertyRow row) {
            if (row == null || row.TargetComponent == null || row.Property == null) {
                return;
            }

            string stateKey = BuildCustomEditorStateKey(row.TargetComponent, row.Property);
            bool nextExpanded = !row.IsExpanded;
            row.IsExpanded = nextExpanded;
            CustomEditorExpandedStates[stateKey] = nextExpanded;
            if (CurrentEntity == null) {
                return;
            }

            ShowComponents(CurrentEntity);
            if (HasLayoutState) {
                UpdateLayout(LastLayoutLeft, LastLayoutTop, LastLayoutWidth);
            }
        }

        /// <summary>
        /// Handles the shared scene-map action button for one scalar row.
        /// </summary>
        /// <param name="row">Row that owns the pressed action button.</param>
        void HandleSceneMapActionButtonPressed(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            if (string.Equals(row.NestedMemberName, SceneMapTargetEntryMemberName, StringComparison.Ordinal)) {
                HandleSceneMapRemoveRequested(row);
                return;
            }
            if (string.Equals(row.NestedMemberName, SceneMapDraftTargetMemberName, StringComparison.Ordinal)) {
                HandleSceneMapAddRequested(row);
            }
        }

        /// <summary>
        /// Commits the current scene-map draft rows into the component dictionary when both values are valid.
        /// </summary>
        /// <param name="row">Row that supplied the add request context.</param>
        void HandleSceneMapAddRequested(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            EnsureEditableComponentForRow(row);
            if (row.TargetComponent is not SceneMapComponent sceneMapComponent) {
                return;
            }

            Component componentKey = ResolveSceneMapDraftComponentKey(row);
            if (componentKey == null) {
                return;
            }
            if (!SceneMapDraftSourcesByComponent.TryGetValue(componentKey, out string sourceSceneId) || string.IsNullOrWhiteSpace(sourceSceneId)) {
                return;
            }
            if (!SceneMapDraftTargetsByComponent.TryGetValue(componentKey, out string targetSceneId) || string.IsNullOrWhiteSpace(targetSceneId)) {
                return;
            }
            if (sceneMapComponent.Mappings.ContainsKey(sourceSceneId)) {
                return;
            }

            sceneMapComponent.Mappings.Add(sourceSceneId, targetSceneId);
            SceneMapDraftSourcesByComponent.Remove(componentKey);
            SceneMapDraftTargetsByComponent.Remove(componentKey);
            PersistPlatformOverrideIfNeeded(row);
            EditorSceneMutationService.MarkSceneMutated();
            RefreshCurrentView();
        }

        /// <summary>
        /// Removes one existing scene-map entry from the component dictionary.
        /// </summary>
        /// <param name="row">Row that supplied the remove request context.</param>
        void HandleSceneMapRemoveRequested(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            EnsureEditableComponentForRow(row);
            if (row.TargetComponent is not SceneMapComponent sceneMapComponent) {
                return;
            }
            if (string.IsNullOrWhiteSpace(row.CustomEditorEntryKey)) {
                return;
            }
            if (!sceneMapComponent.Mappings.Remove(row.CustomEditorEntryKey)) {
                return;
            }

            PersistPlatformOverrideIfNeeded(row);
            EditorSceneMutationService.MarkSceneMutated();
            RefreshCurrentView();
        }

        /// <summary>
        /// Rebuilds the current inspected view while preserving the last-known layout bounds.
        /// </summary>
        void RefreshCurrentView() {
            if (CurrentEntity == null) {
                return;
            }

            ShowComponents(CurrentEntity);
            if (HasLayoutState) {
                UpdateLayout(LastLayoutLeft, LastLayoutTop, LastLayoutWidth);
            }
        }

        /// <summary>
        /// Builds the stable expansion-state key for one provider-backed property editor.
        /// </summary>
        /// <param name="component">Component that owns the custom editor property.</param>
        /// <param name="property">Owning property metadata.</param>
        /// <returns>Stable expansion-state key.</returns>
        string BuildCustomEditorStateKey(Component component, PropertyInfo property) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (property == null) {
                throw new ArgumentNullException(nameof(property));
            }

            return component.GetHashCode().ToString(CultureInfo.InvariantCulture) + "::" + property.Name;
        }

        /// <summary>
        /// Updates one nested provider-backed section header to reflect hover and expansion state.
        /// </summary>
        /// <param name="row">Row whose visual state should be refreshed.</param>
        /// <param name="isHovered">True when the row is currently hovered.</param>
        void UpdateCustomSectionVisual(ComponentPropertyRow row, bool isHovered) {
            if (row == null || row.HeaderBackground == null || row.Label == null) {
                return;
            }

            row.HeaderBackground.Color = isHovered || row.IsExpanded
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentSecondary;
            row.Label.Color = isHovered || row.IsExpanded
                ? ThemeManager.Colors.TextOnAccent
                : ThemeManager.Colors.InputForegroundPrimary;
        }

        /// <summary>
        /// Handles header pointer interactions so one section can be collapsed or expanded.
        /// </summary>
        /// <param name="section">Section receiving the interaction.</param>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="state">Current pointer state.</param>
        void HandleSectionHeaderCursor(ComponentSectionView section, int2 pos, PointerInteraction state) {
            if (section == null) {
                return;
            }

            if (state == PointerInteraction.Hover) {
                section.IsHeaderHovered = true;
                UpdateSectionHeaderVisual(section);
                return;
            }

            if (state == PointerInteraction.Leave) {
                section.IsHeaderHovered = false;
                UpdateSectionHeaderVisual(section);
                return;
            }

            if (state != PointerInteraction.Press) {
                return;
            }

            if (IsPointerOverSectionActionButton(section, pos, section.Background.Size.X)) {
                return;
            }

            section.IsCollapsed = !section.IsCollapsed;
            if (section.TargetComponent != null) {
                CollapsedStates[section.TargetComponent] = section.IsCollapsed;
            }
            SetSectionRowsEnabled(section, !section.IsCollapsed);
        }

        /// <summary>
        /// Raises one remove request for the supplied component section.
        /// </summary>
        /// <param name="section">Section whose component should be removed.</param>
        void HandleSectionRemoveClicked(ComponentSectionView section) {
            if (section == null || section.TargetComponent == null) {
                return;
            }

            if (RemoveRequested != null) {
                RemoveRequested(section);
            }
        }

        /// <summary>
        /// Reverts one section-level existence override back to common behavior and rebuilds the current view.
        /// </summary>
        /// <param name="section">Section whose existence override should be cleared.</param>
        void HandleSectionRevertClicked(ComponentSectionView section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }
            if (!CanSectionShowExistenceOverrideChrome(section)) {
                return;
            }

            PlatformEditingService.RevertComponentExistenceOverride(section.TargetComponent, section.SaveComponent, section.EditingPlatformId);
            RebuildCurrentComponentView();
            EditorSceneMutationService.MarkSceneMutated();
        }

        /// <summary>
        /// Determines whether a pointer position overlaps one visible section action button.
        /// </summary>
        /// <param name="section">Section whose button bounds should be queried.</param>
        /// <param name="pos">Pointer position relative to the header.</param>
        /// <param name="headerWidth">Current width of the header.</param>
        /// <returns>True when the pointer overlaps one visible header action button.</returns>
        bool IsPointerOverSectionActionButton(ComponentSectionView section, int2 pos, int headerWidth) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }

            if (section.RemoveButtonHost != null && section.RemoveButtonHost.Enabled) {
                int removeButtonX = Math.Max(0, headerWidth - SectionRemoveButtonWidth);
                if (pos.X >= removeButtonX &&
                    pos.X <= headerWidth &&
                    pos.Y >= 0 &&
                    pos.Y <= SectionHeaderHeight) {
                    return true;
                }
            }

            if (section.RevertButtonHost != null && section.RevertButtonHost.Enabled) {
                int revertButtonX = Math.Max(0, headerWidth - SectionRevertButtonWidth);
                if (pos.X >= revertButtonX &&
                    pos.X <= headerWidth &&
                    pos.Y >= 0 &&
                    pos.Y <= SectionHeaderHeight) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates one component section header to reflect the current hover state.
        /// </summary>
        /// <param name="section">Section whose header chrome should be refreshed.</param>
        void UpdateSectionHeaderVisual(ComponentSectionView section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }

            section.Background.Color = section.IsHeaderHovered
                ? ThemeManager.Colors.AccentPrimary
                : ThemeManager.Colors.AccentSecondary;
            section.TitleText.Color = section.IsHeaderHovered
                ? ThemeManager.Colors.TextOnAccent
                : ThemeManager.Colors.InputForegroundPrimary;
        }

        /// <summary>
        /// Refreshes the section-level override chrome and action visibility for one component section.
        /// </summary>
        /// <param name="section">Section whose override chrome should be refreshed.</param>
        void RefreshSectionOverrideChrome(ComponentSectionView section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }

            bool isExistenceOverrideActive = CanSectionShowExistenceOverrideChrome(section);
            section.IsExistenceOverrideActive = isExistenceOverrideActive;
            if (section.HeaderOverrideOutline != null) {
                section.HeaderOverrideOutline.BorderThickness = isExistenceOverrideActive ? OverrideOutlineThickness : 0f;
            }
            if (section.RevertButtonHost != null) {
                section.RevertButtonHost.Enabled = isExistenceOverrideActive;
            }
            if (section.RemoveButtonHost != null) {
                section.RemoveButtonHost.Enabled = !isExistenceOverrideActive && section.TargetComponent != null;
            }
        }

        /// <summary>
        /// Returns whether the supplied section should show existence-override chrome on its header.
        /// </summary>
        /// <param name="section">Section to classify.</param>
        /// <returns>True when the section represents a non-common platform-specific existence override.</returns>
        bool CanSectionShowExistenceOverrideChrome(ComponentSectionView section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }
            if (section.TargetComponent == null || section.SaveComponent == null) {
                return false;
            }
            if (string.IsNullOrWhiteSpace(section.EditingPlatformId)
                || string.Equals(section.EditingPlatformId, ComponentPlatformEditingService.CommonPlatformId, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            return section.IsPlatformOnlyComponent || section.IsRemovedOnPlatform;
        }

        /// <summary>
        /// Formats one component type name into a readable editor title.
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
        /// Builds the subtle border color used to mark overridden rows in the inspector.
        /// </summary>
        /// <returns>Border color used by row-level override chrome.</returns>
        byte4 ResolveOverrideOutlineColor() {
            byte4 accentColor = ThemeManager.Colors.AccentPrimary;
            return new byte4(accentColor.X, accentColor.Y, accentColor.Z, 160);
        }

        /// <summary>
        /// Retrieves or creates a row of the requested kind.
        /// </summary>
        /// <param name="kind">Row layout kind.</param>
        /// <returns>Prepared row instance.</returns>
        ComponentPropertyRow AcquireRow(ComponentPropertyRowKind kind) {
            for (int i = 0; i < RowPool.Count; i++) {
                ComponentPropertyRow row = RowPool[i];
                if (row.Kind == kind) {
                    RowPool.RemoveAt(i);
                    return row;
                }
            }

            return CreateRow(kind);
        }

        /// <summary>
        /// Creates a new row of the requested kind.
        /// </summary>
        /// <param name="kind">Row layout kind.</param>
        /// <returns>New row instance.</returns>
        ComponentPropertyRow CreateRow(ComponentPropertyRowKind kind) {
            var rowEntity = new EditorEntity();
            rowEntity.LayerMask = RootEntity.LayerMask;
            rowEntity.Position = float3.Zero;
            RootEntity.AddChild(rowEntity);

            RoundedRectComponent overrideOutline = new RoundedRectComponent {
                Radius = 0f,
                Corners = RoundedRectCorners.None,
                FillColor = new byte4(255, 255, 255, 0),
                BorderThickness = OverrideOutlineThickness,
                BorderColor = ResolveOverrideOutlineColor(),
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, RowHeight)
            };
            rowEntity.AddComponent(overrideOutline);

            var labelHost = new EditorEntity();
            labelHost.LayerMask = RootEntity.LayerMask;
            labelHost.Position = float3.Zero;
            rowEntity.AddChild(labelHost);

            var label = new TextComponent();
            label.Font = Font;
            label.Text = string.Empty;
            label.Color = ThemeManager.Colors.InputForegroundPrimary;
            label.Size = new int2(1, 1);
            label.RenderOrder2D = TextOrder;
            labelHost.AddComponent(label);

            var row = new ComponentPropertyRow(kind, rowEntity, labelHost, label);
            row.OverrideOutline = overrideOutline;

            var revertButtonHost = new EditorEntity();
            revertButtonHost.LayerMask = RootEntity.LayerMask;
            revertButtonHost.Position = float3.Zero;
            revertButtonHost.Enabled = false;
            rowEntity.AddChild(revertButtonHost);

            ButtonComponent revertButton = new ButtonComponent("Revert", new int2(RevertButtonWidth, RevertButtonHeight), Font, () => HandleRowRevertClicked(row), 1f);
            revertButton.SetRenderOrders(RenderOrder2D.PanelSurface, TextOrder);
            revertButton.UseHoverOnlyBackground();
            revertButton.UseSquareCorners();
            revertButton.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            revertButtonHost.AddComponent(revertButton);
            row.RevertButtonHost = revertButtonHost;
            row.RevertButton = revertButton;

            switch (kind) {
                case ComponentPropertyRowKind.Vector3:
                    BuildVectorRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Vector4:
                    BuildVector4Row(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.CustomSection:
                    BuildCustomSectionRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Material:
                    BuildMaterialRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Font:
                    BuildFontRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Model:
                    BuildModelRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Boolean:
                    BuildBooleanRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.Scalar:
                    BuildScalarRow(row, rowEntity);
                    break;
                case ComponentPropertyRowKind.ReadOnly:
                    BuildReadOnlyRow(row, rowEntity);
                    break;
                default:
                    break;
            }

            return row;
        }

        /// <summary>
        /// Builds the Vector4 field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildVector4Row(ComponentPropertyRow row, EditorEntity rowEntity) {
            row.Vector4FieldHosts = new EditorEntity[4];
            row.Vector4Fields = new TextBoxComponent[4];
            row.Vector4Cache = new string[4];

            string[] placeholders = new[] { "R", "G", "B", "A" };
            for (int index = 0; index < row.Vector4FieldHosts.Length; index++) {
                var fieldHost = new EditorEntity();
                fieldHost.LayerMask = RootEntity.LayerMask;
                fieldHost.Position = float3.Zero;
                rowEntity.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(48, FieldHeight), Font, placeholders[index]);
                field.Submitted += HandleVector4Submitted;
                fieldHost.AddComponent(field);

                row.Vector4FieldHosts[index] = fieldHost;
                row.Vector4Fields[index] = field;
                Vector4FieldRows[field] = row;
            }
        }

        /// <summary>
        /// Builds the chrome used by one provider-backed nested section row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildCustomSectionRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            SpriteComponent background = new SpriteComponent {
                Texture = TextureUtils.PixelTexture,
                Color = ThemeManager.Colors.AccentSecondary,
                RenderOrder2D = RenderOrder2D.PanelSurface,
                Size = new int2(1, RowHeight)
            };
            rowEntity.AddComponent(background);

            InteractableComponent interactable = new InteractableComponent {
                Size = new int2(1, RowHeight),
                HoverCursor = PointerCursorKind.Hand
            };
            rowEntity.AddComponent(interactable);

            row.HeaderBackground = background;
            row.HeaderInteractable = interactable;
            interactable.CursorEvent += (pos, delta, state) => HandleCustomSectionCursor(row, state);
        }

        /// <summary>
        /// Builds the Vector3 field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildVectorRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            row.VectorFieldHosts = new EditorEntity[3];
            row.VectorFields = new TextBoxComponent[3];
            row.VectorCache = new string[3];

            string[] placeholders = new[] { "X", "Y", "Z" };
            for (int i = 0; i < row.VectorFieldHosts.Length; i++) {
                var fieldHost = new EditorEntity();
                fieldHost.LayerMask = RootEntity.LayerMask;
                fieldHost.Position = float3.Zero;
                rowEntity.AddChild(fieldHost);

                var field = new TextBoxComponent(new int2(60, FieldHeight), Font, placeholders[i]);
                field.Submitted += HandleVectorSubmitted;
                fieldHost.AddComponent(field);

                row.VectorFieldHosts[i] = fieldHost;
                row.VectorFields[i] = field;
                VectorFieldRows[field] = row;
            }
        }

        /// <summary>
        /// Builds the material field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildMaterialRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestMaterialPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Builds the font field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildFontRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestFontPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Builds the model field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildModelRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = EmptyAssetLabel;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            var buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            rowEntity.AddChild(buttonHost);

            var button = new ButtonComponent("Pick", new int2(PickButtonWidth, PickButtonHeight), Font, () => RequestModelPick(row), 0f);
            buttonHost.AddComponent(button);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Requests the asset picker for a model field.
        /// </summary>
        /// <param name="row">Model row to update.</param>
        void RequestModelPick(ComponentPropertyRow row) {
            EditorAssetPickerService.RequestPick(entry => HandleModelPicked(row, entry));
        }

        /// <summary>
        /// Applies a picked model asset to the row property.
        /// </summary>
        /// <param name="row">Row to update.</param>
        /// <param name="entry">Picked asset entry.</param>
        void HandleModelPicked(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }

            try {
                EnsureEditableComponentForRow(row);
                RuntimeModel model = LoadModel(entry);
                row.Property.SetValue(row.TargetComponent, model);
                if (entry != null) {
                    ModelLabels[model] = entry.Name ?? string.Empty;
                }
                StorePickedAssetReference(row, entry);
                PersistPlatformOverrideIfNeeded(row);
                UpdateModelRow(row);
                EditorSceneMutationService.MarkSceneMutated();
            } catch (Exception ex) {
                Logger.WriteError($"Model pick failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Stores a stable scene asset reference for one picked asset-backed property row.
        /// </summary>
        /// <param name="row">Row whose property received the picked asset.</param>
        /// <param name="entry">Picked asset entry used to build the stable reference.</param>
        void StorePickedAssetReference(ComponentPropertyRow row, AssetBrowserEntry entry) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (row.TargetComponent == null || row.Property == null) {
                return;
            }
            if (row.SaveComponent == null) {
                return;
            }

            SceneAssetReference assetReference = AssetReferenceFactory.CreateFromEntry(entry);
            if (row.CommonComponent == null) {
                PlatformEditingService.StoreAddedComponentAssetReference(
                    row.TargetComponent,
                    row.SaveComponent,
                    row.EditingPlatformId ?? ComponentPlatformEditingService.CommonPlatformId,
                    row.Property.Name,
                    assetReference);
                return;
            }

            PlatformEditingService.StoreAssetReference(
                row.CommonComponent,
                row.TargetComponent,
                row.SaveComponent,
                row.EditingPlatformId ?? ComponentPlatformEditingService.CommonPlatformId,
                row.Property.Name,
                assetReference);
        }

        /// <summary>
        /// Resolves the hidden save component attached to one inspected entity.
        /// </summary>
        /// <param name="entity">Entity whose hidden save component should be returned.</param>
        /// <returns>Hidden save component when one is attached; otherwise null.</returns>
        EntitySaveComponent ResolveEntitySaveComponent(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity == null || entity.Components == null) {
                return null;
            }

            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent entitySaveComponent) {
                    return entitySaveComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads a runtime model from the selected asset entry.
        /// </summary>
        /// <param name="entry">Asset entry to load.</param>
        /// <returns>Runtime model instance.</returns>
        RuntimeModel LoadModel(AssetBrowserEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsGenerated) {
                return GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
            }

            if (FileSystemModelResolver == null) {
                ModelAsset modelAsset = LoadModelAsset(entry.FullPath);
                return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            }

            return FileSystemModelResolver.ResolveRuntimeModel(entry.FullPath);
        }

        /// <summary>
        /// Loads a model asset from disk.
        /// </summary>
        /// <param name="path">Path to the model asset.</param>
        /// <returns>Model asset instance.</returns>
        ModelAsset LoadModelAsset(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Model path must be provided.", nameof(path));
            }

            return AssetContentManager.Load<ModelAsset>(path, EditorContentProcessorIds.ModelAsset);
        }


        /// <summary>
        /// Builds the scalar field controls for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildScalarRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var fieldHost = new EditorEntity();
            fieldHost.LayerMask = RootEntity.LayerMask;
            fieldHost.Position = float3.Zero;
            rowEntity.AddChild(fieldHost);

            var field = new TextBoxComponent(new int2(120, FieldHeight), Font, string.Empty);
            field.Submitted += HandleScalarSubmitted;
            fieldHost.AddComponent(field);

            row.ScalarField = field;
            row.ScalarCache = string.Empty;
            ScalarFieldRows[field] = row;
        }

        /// <summary>
        /// Ensures one scalar row owns a reusable scene-map action button.
        /// </summary>
        /// <param name="row">Row that should expose the action button.</param>
        void EnsureSceneMapActionButton(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.ActionButtonHost != null && row.ActionButton != null) {
                row.ActionButtonHost.Enabled = true;
                return;
            }

            EditorEntity buttonHost = new EditorEntity();
            buttonHost.LayerMask = RootEntity.LayerMask;
            buttonHost.Position = float3.Zero;
            row.Entity.AddChild(buttonHost);

            ButtonComponent button = new ButtonComponent("Action", new int2(PickButtonWidth, PickButtonHeight), Font, () => HandleSceneMapActionButtonPressed(row), 0f);
            button.SetRenderOrders(RenderOrder2D.PanelSurface, TextOrder);
            button.UseHoverOnlyBackground();
            button.UseSquareCorners();
            button.SetTextColor(ThemeManager.Colors.AccentQuaternary);
            buttonHost.AddComponent(button);

            row.ActionButtonHost = buttonHost;
            row.ActionButton = button;
        }

        /// <summary>
        /// Updates one scalar row scene-map action button to match its current role.
        /// </summary>
        /// <param name="row">Row whose action button should be refreshed.</param>
        void ConfigureSceneMapActionButton(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }
            if (row.ActionButtonHost == null || row.ActionButton == null) {
                return;
            }

            row.ActionButtonHost.Enabled = true;
            if (string.Equals(row.NestedMemberName, SceneMapDraftTargetMemberName, StringComparison.Ordinal)) {
                row.ActionButton.SetText("Add");
                return;
            }
            if (string.Equals(row.NestedMemberName, SceneMapTargetEntryMemberName, StringComparison.Ordinal)) {
                row.ActionButton.SetText("Remove");
                return;
            }

            row.ActionButton.SetText("Action");
        }

        /// <summary>
        /// Resolves the component identity used to store scene-map draft values for one row.
        /// </summary>
        /// <param name="row">Row whose draft component identity should be resolved.</param>
        /// <returns>Component identity used for draft storage, or null when unavailable.</returns>
        Component ResolveSceneMapDraftComponentKey(ComponentPropertyRow row) {
            if (row == null) {
                throw new ArgumentNullException(nameof(row));
            }

            if (row.TargetComponent != null) {
                return row.TargetComponent;
            }

            return row.CommonComponent;
        }

        /// <summary>
        /// Stores one scene-map draft scalar value for the supplied component identity.
        /// </summary>
        /// <param name="draftValues">Dictionary that stores the draft values.</param>
        /// <param name="componentKey">Component identity that owns the draft value.</param>
        /// <param name="value">Draft text to store.</param>
        void SetSceneMapDraftValue(Dictionary<Component, string> draftValues, Component componentKey, string value) {
            if (draftValues == null) {
                throw new ArgumentNullException(nameof(draftValues));
            }
            if (componentKey == null) {
                return;
            }

            draftValues[componentKey] = value ?? string.Empty;
        }

        /// <summary>
        /// Builds the checkbox controls for a boolean row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildBooleanRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var checkBoxHost = new EditorEntity();
            checkBoxHost.LayerMask = RootEntity.LayerMask;
            checkBoxHost.Position = float3.Zero;
            rowEntity.AddChild(checkBoxHost);

            var checkBox = new CheckBoxComponent(new int2(FieldHeight, FieldHeight), Font);
            checkBox.SetRenderOrders(RenderOrder2D.PanelSurface, TextOrder);
            checkBox.CheckedChanged += HandleBooleanCheckedChanged;
            checkBoxHost.AddComponent(checkBox);

            row.CheckBoxHost = checkBoxHost;
            row.CheckBoxField = checkBox;
        }

        /// <summary>
        /// Builds the read-only value text for a row.
        /// </summary>
        /// <param name="row">Row to populate.</param>
        /// <param name="rowEntity">Row root entity.</param>
        void BuildReadOnlyRow(ComponentPropertyRow row, EditorEntity rowEntity) {
            var valueHost = new EditorEntity();
            valueHost.LayerMask = RootEntity.LayerMask;
            valueHost.Position = float3.Zero;
            rowEntity.AddChild(valueHost);

            var valueText = new TextComponent();
            valueText.Font = Font;
            valueText.Text = string.Empty;
            valueText.Color = ThemeManager.Colors.InputForegroundPrimary;
            valueText.Size = new int2(1, 1);
            valueText.RenderOrder2D = TextOrder;
            valueHost.AddComponent(valueText);

            row.ValueHost = valueHost;
            row.ValueText = valueText;
        }
    }
}

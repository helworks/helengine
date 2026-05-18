using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies component-section chrome, collapse behavior, and remove confirmation wiring in the properties panel.
    /// </summary>
    public class PropertiesPanelComponentShellTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the panel tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the properties panel and nested views.
        /// </summary>
        public PropertiesPanelComponentShellTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-properties-panel-component-shell-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();
            EditorSceneMutationService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content and clears shared editor services after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one visible component section is created per removable scene component.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenEntityHasVisibleComponents_CreatesOneSectionPerComponent() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            view.ShowComponents(entity);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");

            Assert.Equal(2, sections.Count);
            Assert.Equal("Mesh Component", sections[0].TitleText.Text);
            Assert.Equal("Camera Component", sections[1].TitleText.Text);
        }

        /// <summary>
        /// Ensures clicking a component header collapses only that section body.
        /// </summary>
        [Fact]
        public void HeaderPressed_WhenSectionIsExpanded_CollapsesOnlyThatSection() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            view.ShowComponents(entity);
            view.UpdateLayout(0, 0, 420);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");
            ComponentSectionView firstSection = sections[0];
            ComponentSectionView secondSection = sections[1];

            firstSection.HeaderInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Press);

            Assert.True(firstSection.IsCollapsed);
            Assert.All(firstSection.Rows, row => Assert.False(row.Entity.Enabled));
            Assert.False(secondSection.IsCollapsed);
            Assert.Contains(secondSection.Rows, row => row.Entity.Enabled);
        }

        /// <summary>
        /// Ensures component section headers use full-width title layout and tint on hover.
        /// </summary>
        [Fact]
        public void HeaderHovered_WhenSectionIsVisible_UsesFlushTitleLayoutAndHoverTint() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            view.ShowComponents(entity);
            view.UpdateLayout(0, 0, 420);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");
            ComponentSectionView firstSection = sections[0];
            int removeButtonWidth = firstSection.RemoveButton.Size.X;

            Assert.Equal(0f, firstSection.Root.Position.X);
            Assert.Equal(8f, firstSection.TitleHost.Position.X);
            Assert.Equal(420 - removeButtonWidth - 16 - 6, firstSection.TitleText.Size.X);
            Assert.Equal(420, firstSection.Background.Size.X);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, firstSection.Background.Color);
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, firstSection.TitleText.Color);
            Assert.Equal(8f, firstSection.Rows[0].Entity.Position.X);

            firstSection.HeaderInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Hover);

            Assert.Equal(ThemeManager.Colors.AccentPrimary, firstSection.Background.Color);
            Assert.Equal(ThemeManager.Colors.TextOnAccent, firstSection.TitleText.Color);

            firstSection.HeaderInteractable.OnCursor(new int2(8, 8), new int2(0, 0), PointerInteraction.Leave);

            Assert.Equal(ThemeManager.Colors.AccentSecondary, firstSection.Background.Color);
            Assert.Equal(ThemeManager.Colors.InputForegroundPrimary, firstSection.TitleText.Color);
        }

        /// <summary>
        /// Ensures the add-component modal hides single-instance options that already exist on the selected entity.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenEntityAlreadyHasCamera_ShowsModalWithoutCamera() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            panel.ShowEntityProperties(entity);
            EditorEntity buttonRoot = GetPrivateField<EditorEntity>(panel, "AddComponentButtonRoot");
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<EditorComponentAddDescriptor> descriptors = GetPrivateField<List<EditorComponentAddDescriptor>>(dialog, "FilteredDescriptors");

            Assert.True(buttonRoot.Enabled);
            Assert.True(dialog.IsVisible);
            Assert.DoesNotContain(descriptors, descriptor => string.Equals(descriptor.DisplayName, "Camera", StringComparison.Ordinal));
            Assert.Contains(descriptors, descriptor => string.Equals(descriptor.DisplayName, "Mesh", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the add-component modal merges reflected script components from the current build result.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenScriptProviderReturnsComponents_ShowsScriptDescriptors() {
            TestScriptComponentCatalogProvider scriptProvider = new TestScriptComponentCatalogProvider();
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath), null, new EditorEntity(), scriptProvider);
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<EditorComponentAddDescriptor> descriptors = GetPrivateField<List<EditorComponentAddDescriptor>>(dialog, "FilteredDescriptors");

            Assert.Contains(descriptors, descriptor => string.Equals(descriptor.DisplayName, "Script Tool", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the add-component modal includes the runtime FPS overlay component.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenDialogOpens_IncludesFpsDescriptor() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<EditorComponentAddDescriptor> descriptors = GetPrivateField<List<EditorComponentAddDescriptor>>(dialog, "FilteredDescriptors");

            Assert.Contains(descriptors, descriptor => string.Equals(descriptor.DisplayName, "FPS", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the add-component modal search field live-filters the visible component list.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenSearchTextChanges_FiltersTheComponentList() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new MeshComponent());

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            TextBoxComponent searchField = GetPrivateField<TextBoxComponent>(dialog, "SearchField");

            searchField.Text = "mesh";

            List<EditorComponentAddDescriptor> descriptors = GetPrivateField<List<EditorComponentAddDescriptor>>(dialog, "FilteredDescriptors");

            Assert.Single(descriptors);
            Assert.Equal("Mesh", descriptors[0].DisplayName);
        }

        /// <summary>
        /// Ensures the add-component modal blocks viewport selection while visible.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenDialogIsVisible_BlocksViewportInputUntilHidden() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.UpdateModalLayout(1280, 720);
            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            Assert.True(EditorInputCaptureService.IsPointerBlocked(new int2(100, 100)));

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            dialog.Hide();

            FieldInfo blockersField = typeof(EditorInputCaptureService).GetField("Blockers", BindingFlags.Static | BindingFlags.NonPublic);
            System.Collections.IDictionary blockers = Assert.IsAssignableFrom<System.Collections.IDictionary>(blockersField.GetValue(null));
            Assert.True(blockers.Count == 0, "Remaining blockers: " + string.Join(", ", blockers.Keys.Cast<object>().Select(value => value.GetType().FullName)));
            Assert.False(EditorInputCaptureService.IsPointerBlocked(new int2(100, 100)));
        }

        /// <summary>
        /// Ensures hovering a component row updates its background tint.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenRowIsHovered_ChangesTheRowBackground() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.UpdateModalLayout(1280, 720);
            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            TextBoxComponent searchField = GetPrivateField<TextBoxComponent>(dialog, "SearchField");
            searchField.Text = "mesh";
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Mesh", StringComparison.Ordinal));
            byte4 beforeHover = row.Background.Color;

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            dialog.UpdateLayout(1280, 720);

            Assert.NotEqual(beforeHover, row.Background.Color);
            Assert.Equal(row.HoverColor, row.Background.Color);
        }

        /// <summary>
        /// Ensures clicking a component row selects it without immediately adding the component.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenRowIsActivated_SelectsItWithoutClosing() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            bool componentSelected = false;

            panel.UpdateModalLayout(1280, 720);
            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            dialog.ComponentSelected += _ => componentSelected = true;
            TextBoxComponent searchField = GetPrivateField<TextBoxComponent>(dialog, "SearchField");
            searchField.Text = "mesh";
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Mesh", StringComparison.Ordinal));
            byte4 beforeClick = row.Background.Color;

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

            Assert.True(dialog.IsVisible);
            Assert.Same(row, GetPrivateField<ContextMenuRow>(dialog, "SelectedRow"));
            Assert.NotNull(GetPrivateField<EditorComponentAddDescriptor>(dialog, "SelectedDescriptor"));
            Assert.Equal("Mesh", GetPrivateField<EditorComponentAddDescriptor>(dialog, "SelectedDescriptor").DisplayName);
            Assert.False(componentSelected);
            Assert.NotEqual(beforeClick, row.Background.Color);
            Assert.Equal(ThemeManager.Colors.AccentPrimary, row.Background.Color);
        }

        /// <summary>
        /// Ensures double-clicking a component row adds it and closes the modal.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenRowIsDoubleActivated_AddsTheComponentAndCloses() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.UpdateModalLayout(1280, 720);
            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            TextBoxComponent searchField = GetPrivateField<TextBoxComponent>(dialog, "SearchField");
            searchField.Text = "mesh";
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Mesh", StringComparison.Ordinal));

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);
            FindPrivateField(dialog.GetType(), "LastActivatedTicks").SetValue(dialog, Environment.TickCount64 - 1);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

            Assert.False(dialog.IsVisible);
            Assert.Contains(entity.Components, value => value is MeshComponent);
        }

        /// <summary>
        /// Ensures the footer Add button commits the currently selected component and closes the modal.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenSelectionIsConfirmedWithAddButton_AddsTheComponentAndCloses() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.UpdateModalLayout(1280, 720);
            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            TextBoxComponent searchField = GetPrivateField<TextBoxComponent>(dialog, "SearchField");
            searchField.Text = "mesh";
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Mesh", StringComparison.Ordinal));

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);
            InvokePrivate(dialog, "HandleAddClicked");

            Assert.False(dialog.IsVisible);
            Assert.Contains(entity.Components, value => value is MeshComponent);
        }

        /// <summary>
        /// Ensures the footer Add button is created and spans the component picker width.
        /// </summary>
        [Fact]
        public void HandleAddComponentClicked_WhenDialogIsVisible_CreatesTheFooterAddButton() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            ButtonComponent addButton = GetPrivateField<ButtonComponent>(dialog, "AddButton");

            Assert.Equal(ComponentAddDialog.PanelWidth - (ComponentAddDialog.PanelPadding * 2), addButton.Size.X);
            Assert.Equal(ComponentAddDialog.FooterButtonHeight, addButton.Size.Y);
        }

        /// <summary>
        /// Ensures entity inspection shows one shared platform tab strip directly under the entity identity controls.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_ShowsPlatformTabsDirectlyUnderEntityName() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            panel.ShowEntityProperties(entity);

            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");
            EditorEntity nameRow = GetPrivateField<EditorEntity>(panel, "NameRow");

            Assert.True(tabStrip.Root.Enabled);
            Assert.Equal("common", tabStrip.SelectedPlatformId);
            Assert.True(tabStrip.Root.Position.Y >= nameRow.Position.Y);
        }

        /// <summary>
        /// Ensures the shared platform tab strip is parented into the transform section and uses local transform-section coordinates.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_ParentsPlatformTabsIntoTransformSection() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            panel.Position = new float3(160f, 120f, 0f);
            panel.Size = new int2(320, 420);

            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            panel.ShowEntityProperties(entity);

            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");
            EditorEntity transformRoot = GetPrivateField<EditorEntity>(panel, "TransformRoot");

            Assert.Same(transformRoot, tabStrip.Root.Parent);
            Assert.True(tabStrip.Root.LocalPosition.Y >= 0f);
            Assert.True(tabStrip.Root.LocalPosition.Y < panel.Size.Y);
        }

        /// <summary>
        /// Ensures the entity platform tab strip creates visible tab hosts with a non-zero clipped viewport after one full entity layout pass.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_CreatesVisiblePlatformTabHosts() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            panel.Size = new int2(320, 420);

            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            panel.ShowEntityProperties(entity);

            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");
            List<EditorEntity> tabHosts = GetPrivateField<List<EditorEntity>>(tabStrip, "TabHosts");
            ClipRectComponent viewportClipRect = GetPrivateField<ClipRectComponent>(tabStrip, "ViewportClipRect");

            Assert.NotEmpty(tabHosts);
            Assert.All(tabHosts, host => Assert.True(host.Enabled));
            Assert.True(viewportClipRect.Size.X > 0);
            Assert.True(viewportClipRect.Size.Y > 0);
        }

        /// <summary>
        /// Ensures the laid-out properties panel emits visible rounded-rectangle and glyph commands for the shared platform tabs.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_EmitsVisiblePlatformTabRenderCommands() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            panel.Position = new float3(160f, 120f, 0f);
            panel.Size = new int2(320, 420);

            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());

            panel.ShowEntityProperties(entity);

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "ContentCameraComponent");
            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");
            List<EditorEntity> tabHosts = GetPrivateField<List<EditorEntity>>(tabStrip, "TabHosts");
            EditorEntity firstTabHost = Assert.IsType<EditorEntity>(tabHosts[0]);
            RenderCommandList2D commands = new RenderCommandListBuilder2D().Build(contentCamera.RenderQueue2D);
            float expectedLeft = firstTabHost.Position.X;
            float expectedTop = firstTabHost.Position.Y;
            bool foundRoundedRect = false;
            bool foundGlyph = false;
            bool foundPositiveClipForTab = false;
            Stack<float4> activeClipStack = new Stack<float4>();

            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++) {
                if (commands.GetCommandType(commandIndex) == RenderCommand2DType.ClipPush) {
                    int payloadIndex = commands.GetClipPushPayloadIndex(commandIndex);
                    activeClipStack.Push(commands.GetClipPushRect(payloadIndex));
                } else if (commands.GetCommandType(commandIndex) == RenderCommand2DType.ClipPop) {
                    activeClipStack.Pop();
                } else if (commands.GetCommandType(commandIndex) == RenderCommand2DType.RoundedRect) {
                    int payloadIndex = commands.GetRoundedRectPayloadIndex(commandIndex);
                    float4 bounds = commands.GetRoundedRectBounds(payloadIndex);
                    if (Math.Abs(bounds.X - expectedLeft) < 0.5f
                        && Math.Abs(bounds.Y - expectedTop) < 0.5f
                        && Math.Abs(bounds.Z - 96f) < 0.5f
                        && Math.Abs(bounds.W - 24f) < 0.5f) {
                        foundRoundedRect = true;
                        if (activeClipStack.Count > 0) {
                            float4 clipRect = activeClipStack.Peek();
                            float clippedRight = Math.Min(bounds.X + bounds.Z, clipRect.X + clipRect.Z);
                            float clippedBottom = Math.Min(bounds.Y + bounds.W, clipRect.Y + clipRect.W);
                            float clippedLeft = Math.Max(bounds.X, clipRect.X);
                            float clippedTop = Math.Max(bounds.Y, clipRect.Y);
                            foundPositiveClipForTab = clippedRight > clippedLeft && clippedBottom > clippedTop;
                        }
                    }
                } else if (commands.GetCommandType(commandIndex) == RenderCommand2DType.GlyphQuad) {
                    int payloadIndex = commands.GetGlyphQuadPayloadIndex(commandIndex);
                    float4 bounds = commands.GetGlyphQuadBounds(payloadIndex);
                    if (bounds.X >= expectedLeft
                        && bounds.X < expectedLeft + 96f
                        && bounds.Y >= expectedTop
                        && bounds.Y < expectedTop + 24f) {
                        foundGlyph = true;
                    }
                }
            }

            Assert.True(foundRoundedRect);
            Assert.True(foundGlyph);
            Assert.True(foundPositiveClipForTab);
        }

        /// <summary>
        /// Ensures switching from entity inspection to a scene-asset summary hides the shared component platform strip instead of leaving stale tab-strip state behind.
        /// </summary>
        [Fact]
        public void ShowSceneAssetSummary_AfterEntityProperties_HidesComponentPlatformTabStrip() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new CameraComponent());
            AssetBrowserEntry sceneEntry = AssetBrowserEntry.CreateFileSystemFile(
                "test.scene",
                "scenes/test.scene",
                Path.Combine(TempRootPath, "scenes", "test.scene"),
                ".scene",
                AssetEntryKind.Scene);

            panel.ShowEntityProperties(entity);
            panel.ShowSceneAssetSummary(sceneEntry);

            PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");

            Assert.False(tabStrip.Root.Enabled);
        }

        /// <summary>
        /// Ensures one properties panel content camera does not render the shared platform tabs that belong to a different properties panel instance.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenTwoPropertiesPanelsExist_DoesNotRenderSiblingPanelPlatformTabs() {
            PropertiesPanel firstPanel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            PropertiesPanel secondPanel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            firstPanel.Position = new float3(40f, 60f, 0f);
            firstPanel.Size = new int2(320, 420);
            secondPanel.Position = new float3(420f, 60f, 0f);
            secondPanel.Size = new int2(320, 420);

            EditorEntity firstEntity = new EditorEntity {
                Name = "First"
            };
            firstEntity.AddComponent(new CameraComponent());
            EditorEntity secondEntity = new EditorEntity {
                Name = "Second"
            };
            secondEntity.AddComponent(new CameraComponent());

            firstPanel.ShowEntityProperties(firstEntity);
            secondPanel.ShowEntityProperties(secondEntity);
            Core.Instance.ObjectManager.Update();

            PlatformTabStripView secondTabStrip = GetPrivateField<PlatformTabStripView>(secondPanel, "ComponentPlatformTabStrip");
            List<EditorEntity> secondTabHosts = GetPrivateField<List<EditorEntity>>(secondTabStrip, "TabHosts");
            EditorEntity secondFirstTabHost = Assert.IsType<EditorEntity>(secondTabHosts[0]);
            CameraComponent firstContentCamera = GetPrivateField<CameraComponent>(firstPanel, "ContentCameraComponent");
            RenderCommandList2D commands = new RenderCommandListBuilder2D().Build(firstContentCamera.RenderQueue2D);
            bool foundSiblingTabRoundedRect = false;

            for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++) {
                if (commands.GetCommandType(commandIndex) != RenderCommand2DType.RoundedRect) {
                    continue;
                }

                int payloadIndex = commands.GetRoundedRectPayloadIndex(commandIndex);
                float4 bounds = commands.GetRoundedRectBounds(payloadIndex);
                if (Math.Abs(bounds.X - secondFirstTabHost.Position.X) < 0.5f
                    && Math.Abs(bounds.Y - secondFirstTabHost.Position.Y) < 0.5f
                    && Math.Abs(bounds.Z - 96f) < 0.5f
                    && Math.Abs(bounds.W - 24f) < 0.5f) {
                    foundSiblingTabRoundedRect = true;
                    break;
                }
            }

            Assert.False(foundSiblingTabRoundedRect);
        }

        /// <summary>
        /// Ensures the component modals can be attached to a shared top-level host instead of the docked panel.
        /// </summary>
        [Fact]
        public void PropertiesPanel_WhenProvidedSharedModalHost_AttachesComponentDialogsToThatHost() {
            EditorEntity modalHost = new EditorEntity {
                LayerMask = EditorLayerMasks.EditorModalUi
            };
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath), null, modalHost);

            ComponentAddDialog addDialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            RemoveComponentDialog removeDialog = GetPrivateField<RemoveComponentDialog>(panel, "RemoveComponentDialog");

            Assert.Same(modalHost, addDialog.Parent);
            Assert.Same(modalHost, removeDialog.Parent);
        }

        /// <summary>
        /// Ensures the add-component button builds its interactive visuals when an entity is selected.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_BuildsAddComponentButtonVisuals() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };

            panel.ShowEntityProperties(entity);

            ButtonComponent button = GetPrivateField<ButtonComponent>(panel, "AddComponentButton");
            Entity textEntity = GetPrivateField<Entity>(button, "textEntity");
            InteractableComponent interactable = GetPrivateField<InteractableComponent>(button, "interactableComponent");
            RoundedRectComponent background = GetPrivateField<RoundedRectComponent>(button, "roundedRect");

            Assert.Equal(panel.Size.X - 16, button.Size.X);
            Assert.Equal(PointerCursorKind.Hand, interactable.HoverCursor);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, background.FillColor);
            Assert.NotNull(textEntity);
            Assert.NotNull(interactable);
        }

        /// <summary>
        /// Ensures tall property content produces a positive scroll range instead of overflowing the panel body.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenPropertyContentExceedsPanelBody_ExposesPositiveScrollRange() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath)) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = CreateEntityWithTallPropertyComponent();

            panel.ShowEntityProperties(entity);

            ScrollComponent scrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");
            int expectedViewportHeight = Math.Max(panel.Size.Y, panel.MinSize.Y);

            Assert.True(scrollComponent.MaximumScrollOffset > 0);
            Assert.Equal(320, scrollComponent.Size.X);
            Assert.Equal(expectedViewportHeight, scrollComponent.Size.Y);
            Assert.Equal(expectedViewportHeight, scrollComponent.VisibleItemCount);
        }

        /// <summary>
        /// Ensures one mouse-wheel notch advances the properties panel by one content row instead of one raw pixel.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenPropertyContentExceedsPanelBody_UsesRowSizedWheelScrollSteps() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath)) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = CreateEntityWithTallPropertyComponent();

            panel.ShowEntityProperties(entity);

            ScrollComponent scrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");

            Assert.Equal(24, scrollComponent.ScrollStepCount);
        }

        /// <summary>
        /// Ensures scrollable property content is parented to the clipped content root and rendered on its dedicated layer.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenScrollableBodyIsBuilt_ParentsChildContentToTheClippedViewportLayer() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath)) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = CreateEntityWithTallPropertyComponent();

            panel.ShowEntityProperties(entity);

            EditorEntity scrollContentRoot = GetPrivateField<EditorEntity>(panel, "ScrollContentRoot");
            EditorEntity transformRoot = GetPrivateField<EditorEntity>(panel, "TransformRoot");
            EditorEntity addComponentButtonRoot = GetPrivateField<EditorEntity>(panel, "AddComponentButtonRoot");
            ComponentPropertiesView componentView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "ContentCameraComponent");
            float expectedViewportHeight = Math.Max(panel.Size.Y, panel.MinSize.Y);

            Assert.Equal(EditorLayerMasks.PropertiesPanelContent, scrollContentRoot.LayerMask);
            Assert.Equal(EditorLayerMasks.PropertiesPanelContent, transformRoot.LayerMask);
            Assert.Equal(EditorLayerMasks.PropertiesPanelContent, addComponentButtonRoot.LayerMask);
            Assert.Equal(EditorLayerMasks.PropertiesPanelContent, componentView.Root.LayerMask);
            Assert.Same(scrollContentRoot, transformRoot.Parent);
            Assert.Same(scrollContentRoot, addComponentButtonRoot.Parent);
            Assert.Same(scrollContentRoot, componentView.Root.Parent);
            Assert.Equal(EditorUiCameraDrawOrders.PanelContent, contentCamera.CameraDrawOrder);
            Assert.Equal(32f, contentCamera.Viewport.X);
            Assert.Equal(40f + DockableEntity.TitleBarHeight, contentCamera.Viewport.Y);
            Assert.Equal(320f, contentCamera.Viewport.Z);
            Assert.Equal(expectedViewportHeight, contentCamera.Viewport.W);
        }

        /// <summary>
        /// Ensures the fixed properties-body viewport host owns a clip component sized to the visible panel body.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenScrollableBodyIsBuilt_AttachesClipOwnerToTheFixedViewportHost() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath)) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = CreateEntityWithTallPropertyComponent();

            panel.ShowEntityProperties(entity);

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            EditorEntity scrollContentRoot = GetPrivateField<EditorEntity>(panel, "ScrollContentRoot");
            ClipRectComponent clipComponent = GetRequiredComponent<ClipRectComponent>(contentRoot);
            int expectedViewportHeight = Math.Max(panel.Size.Y, panel.MinSize.Y);

            Assert.NotNull(clipComponent);
            Assert.Equal(new int2(320, expectedViewportHeight), clipComponent.Size);
            Assert.Equal(new float4(32f, 40f + DockableEntity.TitleBarHeight, 320f, expectedViewportHeight), clipComponent.GetClipRect());
            Assert.Equal(0f, scrollContentRoot.LocalPosition.X);
            Assert.Equal(0f, scrollContentRoot.LocalPosition.Y);
            Assert.Equal(0.1f, scrollContentRoot.LocalPosition.Z);
        }

        /// <summary>
        /// Ensures controls that overflow below the panel body are not resolved by pointer hit testing until scrolled into view.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenPointerTargetsAddButtonOutsideViewport_DoesNotResolveTheClippedOverflowButton() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath)) {
                Position = new float3(32f, 40f, 0f),
                Size = new int2(320, 120)
            };
            EditorEntity entity = CreateEntityWithTallPropertyComponent();

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView componentView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(componentView, "ActiveSections");
            ComponentSectionView firstSection = Assert.Single(sections);
            int pointerX = (int)Math.Round(firstSection.HeaderInteractable.Parent.Position.X + 8f);
            int pointerY = (int)Math.Round(firstSection.HeaderInteractable.Parent.Position.Y + 8f);
            ICamera topCamera = FindTopCameraAt(pointerX, pointerY);
            Assert.Null(topCamera);

            IInteractable2D hit = null;
            if (topCamera != null) {
                hit = PointerInteractableHitResolver.ResolveTopInteractableAt(
                    Core.Instance.ObjectManager.Interactables,
                    Core.Instance.ObjectManager.Drawables2D,
                    topCamera,
                    pointerX,
                    pointerY);
            }

            Assert.NotSame(firstSection.HeaderInteractable, hit);
            Assert.Null(hit);
        }

        /// <summary>
        /// Ensures the remove button wiring raises a remove request for the correct component.
        /// </summary>
        [Fact]
        public void HandleSectionRemoveClicked_RaisesRemoveRequestedForTheSectionComponent() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();
            ComponentSectionView removedSection = null;
            view.RemoveRequested += value => removedSection = value;

            view.ShowComponents(entity);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");

            InvokePrivate(view, "HandleSectionRemoveClicked", sections[0]);

            Assert.Same(sections[0], removedSection);
        }

        /// <summary>
        /// Ensures boolean component properties are rendered as checkbox rows and update the target property.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenLightContainsBooleanShadowProperty_UsesCheckboxRowAndUpdatesTheLight() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Light"
            };
            DirectionalLightComponent light = new DirectionalLightComponent();
            entity.AddComponent(light);

            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetPrivateField<List<ComponentPropertyRow>>(view, "ActiveRows");
            ComponentPropertyRow shadowRow = Assert.Single(rows, row => string.Equals(row.Property?.Name, nameof(LightComponent.ShadowsEnabled), StringComparison.Ordinal));

            Assert.Equal(ComponentPropertyRowKind.Boolean, shadowRow.Kind);
            Assert.NotNull(shadowRow.CheckBoxField);
            Assert.True(shadowRow.CheckBoxField.IsChecked);

            InvokePrivate(view, "HandleBooleanCheckedChanged", shadowRow.CheckBoxField, false);

            Assert.False(light.ShadowsEnabled);
            Assert.False(shadowRow.CheckBoxField.IsChecked);
        }

        /// <summary>
        /// Ensures directional light shadow distance is surfaced as an editable scalar row and updates the target light.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenDirectionalLightContainsShadowDistance_UsesScalarRowAndUpdatesTheLight() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Light"
            };
            DirectionalLightComponent light = new DirectionalLightComponent {
                ShadowDistance = 64f
            };
            entity.AddComponent(light);

            view.ShowComponents(entity);

            ComponentPropertyRow shadowDistanceRow = FindScalarRow(view, nameof(DirectionalLightComponent.ShadowDistance));

            Assert.Equal(ComponentPropertyRowKind.Scalar, shadowDistanceRow.Kind);
            Assert.Equal("64", shadowDistanceRow.ScalarField.Text);

            shadowDistanceRow.ScalarField.Text = "96";
            InvokePrivate(view, "HandleScalarSubmitted", shadowDistanceRow.ScalarField);

            Assert.Equal(96f, light.ShadowDistance);
            Assert.Equal("96", shadowDistanceRow.ScalarField.Text);
        }

        /// <summary>
        /// Ensures scalar property rows use a 40/60 label and value split when the inspector is laid out.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenScalarPropertyRowsAreVisible_UsesFortySixtyLabelSplit() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Light"
            };
            entity.AddComponent(new DirectionalLightComponent {
                ShadowDistance = 64f
            });

            view.ShowComponents(entity);
            view.UpdateLayout(0, 0, 420);

            ComponentPropertyRow shadowDistanceRow = FindScalarRow(view, nameof(DirectionalLightComponent.ShadowDistance));

            Assert.Equal(162, shadowDistanceRow.Label.Size.X);
            Assert.Equal(176f, shadowDistanceRow.ScalarField.Parent.Position.X);
            Assert.Equal(236, shadowDistanceRow.ScalarField.Size.X);
        }

        /// <summary>
        /// Ensures submitting invalid scalar text keeps the authored component value and restores the last valid field text.
        /// </summary>
        [Fact]
        public void HandleScalarSubmitted_WhenTextIsInvalid_RestoresLastValidTextWithoutChangingTheValue() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Light"
            };
            DirectionalLightComponent light = new DirectionalLightComponent {
                Intensity = 2f
            };
            entity.AddComponent(light);

            view.ShowComponents(entity);

            ComponentPropertyRow intensityRow = FindScalarRow(view, nameof(LightComponent.Intensity));
            intensityRow.ScalarField.Text = string.Empty;

            InvokePrivate(view, "HandleScalarSubmitted", intensityRow.ScalarField);

            Assert.Equal(2f, light.Intensity);
            Assert.Equal("2", intensityRow.ScalarField.Text);
            Assert.Equal("2", intensityRow.ScalarCache);
        }

        /// <summary>
        /// Ensures losing focus with invalid scalar text rolls the field back to the last valid value instead of leaving invalid text visible.
        /// </summary>
        [Fact]
        public void ScalarField_WhenBlurredWithInvalidText_RestoresLastValidTextWithoutChangingTheValue() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Light"
            };
            DirectionalLightComponent light = new DirectionalLightComponent {
                Intensity = 2f
            };
            entity.AddComponent(light);

            view.ShowComponents(entity);

            ComponentPropertyRow intensityRow = FindScalarRow(view, nameof(LightComponent.Intensity));
            intensityRow.ScalarField.IsFocused = true;
            intensityRow.ScalarField.Text = "-";
            intensityRow.ScalarField.IsFocused = false;

            Assert.Equal(2f, light.Intensity);
            Assert.Equal("2", intensityRow.ScalarField.Text);
            Assert.Equal("2", intensityRow.ScalarCache);
        }

        /// <summary>
        /// Ensures confirming removal deletes the component and keeps the entity selected.
        /// </summary>
        [Fact]
        public void HandleRemoveComponentConfirmed_WhenDialogWasOpened_RemovesTheComponentAndKeepsTheSelection() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");
            ComponentSectionView meshSection = Assert.Single(sections, value => value.TargetComponent is MeshComponent);

            InvokePrivate(view, "HandleSectionRemoveClicked", meshSection);
            InvokePrivate(panel, "HandleRemoveComponentConfirmed");

            Assert.DoesNotContain(entity.Components, value => value is MeshComponent);
            Assert.Same(entity, GetPrivateField<Entity>(panel, "SelectedEntity"));
        }

        /// <summary>
        /// Ensures canceling removal leaves the component attached.
        /// </summary>
        [Fact]
        public void HandleRemoveComponentCanceled_WhenDialogWasOpened_LeavesTheComponentAttached() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");
            ComponentSectionView meshSection = Assert.Single(sections, value => value.TargetComponent is MeshComponent);

            InvokePrivate(view, "HandleSectionRemoveClicked", meshSection);
            InvokePrivate(panel, "HandleRemoveComponentCanceled");

            Assert.Contains(entity.Components, value => value is MeshComponent);
            Assert.Same(entity, GetPrivateField<Entity>(panel, "SelectedEntity"));
        }

        /// <summary>
        /// Ensures adding a component from a non-common platform tab creates a platform-only section instead of mutating the live common entity component list.
        /// </summary>
        [Fact]
        public void HandleAddComponentSelected_WhenWindowsTabAddsMesh_KeepsTheLiveEntityCommonAndShowsTheComponentOnlyOnWindows() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            EditorComponentAddDescriptor descriptor = new EditorComponentAddDescriptor(
                "Mesh",
                typeof(MeshComponent),
                true,
                target => target.AddComponent(new MeshComponent()));

            panel.ShowEntityProperties(entity, new[] { "windows" });
            SelectInspectorPlatform(panel, "windows");

            InvokePrivate(panel, "HandleAddComponentSelected", descriptor);

            Assert.DoesNotContain(entity.Components, value => value is MeshComponent);

            SelectInspectorPlatform(panel, "common");
            ComponentPropertiesView commonView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> commonSections = GetPrivateField<List<ComponentSectionView>>(commonView, "ActiveSections");
            Assert.DoesNotContain(commonSections, value => value.TargetComponent is MeshComponent);

            SelectInspectorPlatform(panel, "windows");
            ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            Assert.Contains(windowsSections, value => value.TargetComponent is MeshComponent);
        }

        /// <summary>
        /// Ensures platform-only component sections show header-level revert chrome and can be reverted back to common behavior.
        /// </summary>
        [Fact]
        public void HandleAddComponentSelected_WhenWindowsTabAddsMesh_ShowsHeaderRevertChromeAndCanRevertThePlatformOnlySection() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            EditorComponentAddDescriptor descriptor = new EditorComponentAddDescriptor(
                "Mesh",
                typeof(MeshComponent),
                true,
                target => target.AddComponent(new MeshComponent()));

            panel.ShowEntityProperties(entity, new[] { "windows" });
            SelectInspectorPlatform(panel, "windows");
            InvokePrivate(panel, "HandleAddComponentSelected", descriptor);
            SelectInspectorPlatform(panel, "windows");

            ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            ComponentSectionView meshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

            Assert.True(meshSection.IsPlatformOnlyComponent);
            Assert.False(meshSection.IsRemovedOnPlatform);
            Assert.NotNull(meshSection.RevertButtonHost);
            Assert.NotNull(meshSection.HeaderOverrideOutline);
            Assert.True(meshSection.RevertButtonHost.Enabled);
            Assert.False(meshSection.RemoveButtonHost.Enabled);
            Assert.True(meshSection.HeaderOverrideOutline.BorderThickness > 0f);

            InvokePrivate(windowsView, "HandleSectionRevertClicked", meshSection);

            windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            Assert.DoesNotContain(windowsSections, value => value.TargetComponent is MeshComponent);
        }

        /// <summary>
        /// Ensures removing a common component from a non-common platform tab creates a platform removal override instead of deleting the live common component.
        /// </summary>
        [Fact]
        public void HandleRemoveComponentConfirmed_WhenWindowsTabRemovesMesh_KeepsTheLiveMeshAndHidesItOnlyOnWindows() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            panel.ShowEntityProperties(entity, new[] { "windows" });
            SelectInspectorPlatform(panel, "windows");

            ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            ComponentSectionView meshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

            InvokePrivate(windowsView, "HandleSectionRemoveClicked", meshSection);
            InvokePrivate(panel, "HandleRemoveComponentConfirmed");

            Assert.Contains(entity.Components, value => value is MeshComponent);

            SelectInspectorPlatform(panel, "common");
            ComponentPropertiesView commonView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> commonSections = GetPrivateField<List<ComponentSectionView>>(commonView, "ActiveSections");
            Assert.Contains(commonSections, value => value.TargetComponent is MeshComponent);

            SelectInspectorPlatform(panel, "windows");
            windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            Assert.DoesNotContain(windowsSections, value => value.TargetComponent is MeshComponent && value.Rows.Count > 0);
        }

        /// <summary>
        /// Ensures platform removal placeholders show header-level revert chrome and restore the common component when reverted.
        /// </summary>
        [Fact]
        public void HandleRemoveComponentConfirmed_WhenWindowsTabRemovesMesh_ShowsHeaderRevertChromeAndRestoresTheSectionWhenReverted() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();

            panel.ShowEntityProperties(entity, new[] { "windows" });
            SelectInspectorPlatform(panel, "windows");

            ComponentPropertiesView windowsView = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            List<ComponentSectionView> windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            ComponentSectionView meshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

            InvokePrivate(windowsView, "HandleSectionRemoveClicked", meshSection);
            InvokePrivate(panel, "HandleRemoveComponentConfirmed");
            SelectInspectorPlatform(panel, "windows");

            windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            ComponentSectionView removedMeshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);

            Assert.False(removedMeshSection.IsPlatformOnlyComponent);
            Assert.True(removedMeshSection.IsRemovedOnPlatform);
            Assert.Empty(removedMeshSection.Rows);
            Assert.NotNull(removedMeshSection.RevertButtonHost);
            Assert.NotNull(removedMeshSection.HeaderOverrideOutline);
            Assert.True(removedMeshSection.RevertButtonHost.Enabled);
            Assert.False(removedMeshSection.RemoveButtonHost.Enabled);
            Assert.True(removedMeshSection.HeaderOverrideOutline.BorderThickness > 0f);

            InvokePrivate(windowsView, "HandleSectionRevertClicked", removedMeshSection);

            windowsSections = GetPrivateField<List<ComponentSectionView>>(windowsView, "ActiveSections");
            ComponentSectionView restoredMeshSection = Assert.Single(windowsSections, value => value.TargetComponent is MeshComponent);
            Assert.False(restoredMeshSection.IsRemovedOnPlatform);
            Assert.NotEmpty(restoredMeshSection.Rows);
            Assert.False(restoredMeshSection.RevertButtonHost.Enabled);
            Assert.True(restoredMeshSection.RemoveButtonHost.Enabled);
            Assert.Equal(0f, restoredMeshSection.HeaderOverrideOutline.BorderThickness);
        }

        /// <summary>
        /// Creates one editor entity with two visible scene components.
        /// </summary>
        /// <returns>Entity used by the component-shell tests.</returns>
        EditorEntity CreateEntityWithVisibleComponents() {
            EditorEntity entity = new EditorEntity {
                Name = "Cube"
            };
            entity.AddComponent(new MeshComponent());
            entity.AddComponent(new CameraComponent());
            return entity;
        }

        /// <summary>
        /// Creates one editor entity whose property rows overflow a small properties panel body.
        /// </summary>
        /// <returns>Entity used to verify scrolling and clipping.</returns>
        EditorEntity CreateEntityWithTallPropertyComponent() {
            EditorEntity entity = new EditorEntity {
                Name = "Tall"
            };
            entity.AddComponent(new TallPropertyTestComponent());
            return entity;
        }

        /// <summary>
        /// Finds one scalar property row by bound property name.
        /// </summary>
        /// <param name="view">View that owns the active property rows.</param>
        /// <param name="propertyName">Property name to locate.</param>
        /// <returns>Matching scalar row.</returns>
        ComponentPropertyRow FindScalarRow(ComponentPropertiesView view, string propertyName) {
            List<ComponentPropertyRow> rows = GetPrivateField<List<ComponentPropertyRow>>(view, "ActiveRows");
            return Assert.Single(
                rows,
                row => row.Kind == ComponentPropertyRowKind.Scalar
                    && string.Equals(row.Property?.Name, propertyName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Gets one required component from an entity by exact assignable type.
        /// </summary>
        /// <typeparam name="T">Expected component type.</typeparam>
        /// <param name="entity">Entity that owns the component.</param>
        /// <returns>Matching component instance.</returns>
        T GetRequiredComponent<T>(Entity entity) where T : Component {
            Assert.NotNull(entity);
            Assert.NotNull(entity.Components);

            for (int index = 0; index < entity.Components.Count; index++) {
                if (entity.Components[index] is T component) {
                    return component;
                }
            }

            throw new InvalidOperationException($"Component '{typeof(T).FullName}' was not found on entity '{entity.GetType().FullName}'.");
        }

        /// <summary>
        /// Switches the component inspector into one platform context.
        /// </summary>
        /// <param name="panel">Panel whose platform context should change.</param>
        /// <param name="platformId">Platform identifier to activate.</param>
        void SelectInspectorPlatform(PropertiesPanel panel, string platformId) {
            MethodInfo method = FindPrivateMethod(panel.GetType(), "HandleComponentPlatformTabChanged");
            method.Invoke(panel, new object[] { platformId });
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = FindPrivateField(target.GetType(), fieldName);
            return Assert.IsAssignableFrom<T>(field.GetValue(target));
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = FindPrivateMethod(target.GetType(), methodName);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Finds one inherited non-public instance field by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the field lookup.</param>
        /// <param name="fieldName">Field name to locate.</param>
        /// <returns>Matching field metadata.</returns>
        FieldInfo FindPrivateField(Type type, string fieldName) {
            Type currentType = type;

            while (currentType != null) {
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (field != null) {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Field '{fieldName}' was not found on type '{type.FullName}'.");
        }

        /// <summary>
        /// Finds one inherited non-public instance method by walking the type hierarchy.
        /// </summary>
        /// <param name="type">Type that starts the method lookup.</param>
        /// <param name="methodName">Method name to locate.</param>
        /// <returns>Matching method metadata.</returns>
        MethodInfo FindPrivateMethod(Type type, string methodName) {
            Type currentType = type;

            while (currentType != null) {
                MethodInfo method = currentType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (method != null) {
                    return method;
                }

                currentType = currentType.BaseType;
            }

            throw new InvalidOperationException($"Method '{methodName}' was not found on type '{type.FullName}'.");
        }

        /// <summary>
        /// Finds the top-most camera whose viewport contains the supplied pointer coordinate.
        /// </summary>
        /// <param name="pointerX">Pointer X coordinate in window space.</param>
        /// <param name="pointerY">Pointer Y coordinate in window space.</param>
        /// <returns>Top-most camera at the pointer position, or null when no camera covers the point.</returns>
        ICamera FindTopCameraAt(int pointerX, int pointerY) {
            List<ICamera> cameras = Core.Instance.ObjectManager.Cameras;
            for (int cameraIndex = cameras.Count - 1; cameraIndex >= 0; cameraIndex--) {
                ICamera camera = cameras[cameraIndex];
                if (camera.Viewport.Contains(pointerX, pointerY)) {
                    return camera;
                }
            }

            return null;
        }

        /// <summary>
        /// Script component provider used to verify reflection-discovered components flow into the add modal.
        /// </summary>
        sealed class TestScriptComponentCatalogProvider : IEditorScriptComponentCatalogProvider {
            /// <summary>
            /// Returns one synthetic script component descriptor.
            /// </summary>
            /// <param name="entity">Entity that would receive the reflected component.</param>
            /// <returns>One script component descriptor.</returns>
            public IReadOnlyList<EditorComponentAddDescriptor> GetAvailableScriptComponents(Entity entity) {
                return new[] {
                    new EditorComponentAddDescriptor("Script Tool", typeof(TestScriptComponent), false, target => target.AddComponent(new TestScriptComponent()))
                };
            }
        }

        /// <summary>
        /// Synthetic script component used by the reflection-provider test.
        /// </summary>
        public sealed class TestScriptComponent : Component {
        }

        /// <summary>
        /// Synthetic component with enough editable scalar properties to overflow the properties panel body in tests.
        /// </summary>
        public sealed class TallPropertyTestComponent : Component {
            /// <summary>
            /// Gets or sets the first scalar value.
            /// </summary>
            public float Alpha { get; set; } = 1f;
            /// <summary>
            /// Gets or sets the second scalar value.
            /// </summary>
            public float Bravo { get; set; } = 2f;
            /// <summary>
            /// Gets or sets the third scalar value.
            /// </summary>
            public float Charlie { get; set; } = 3f;
            /// <summary>
            /// Gets or sets the fourth scalar value.
            /// </summary>
            public float Delta { get; set; } = 4f;
            /// <summary>
            /// Gets or sets the fifth scalar value.
            /// </summary>
            public float Echo { get; set; } = 5f;
            /// <summary>
            /// Gets or sets the sixth scalar value.
            /// </summary>
            public float Foxtrot { get; set; } = 6f;
            /// <summary>
            /// Gets or sets the seventh scalar value.
            /// </summary>
            public float Golf { get; set; } = 7f;
            /// <summary>
            /// Gets or sets the eighth scalar value.
            /// </summary>
            public float Hotel { get; set; } = 8f;
            /// <summary>
            /// Gets or sets the ninth scalar value.
            /// </summary>
            public float India { get; set; } = 9f;
            /// <summary>
            /// Gets or sets the tenth scalar value.
            /// </summary>
            public float Juliet { get; set; } = 10f;
            /// <summary>
            /// Gets or sets the eleventh scalar value.
            /// </summary>
            public float Kilo { get; set; } = 11f;
            /// <summary>
            /// Gets or sets the twelfth scalar value.
            /// </summary>
            public float Lima { get; set; } = 12f;
        }

        /// <summary>
        /// Creates a deterministic font asset for the component-shell tests.
        /// </summary>
        /// <returns>Font asset containing the glyphs required by the properties panel and remove dialog.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['X'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['?'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}

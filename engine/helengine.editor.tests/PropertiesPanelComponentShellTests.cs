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
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
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
            Assert.Equal(420 - removeButtonWidth - 16, firstSection.TitleText.Size.X);
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

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Camera", StringComparison.Ordinal));
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

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            dialog.ComponentSelected += _ => componentSelected = true;
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Camera", StringComparison.Ordinal));
            byte4 beforeClick = row.Background.Color;

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

            Assert.True(dialog.IsVisible);
            Assert.Same(row, GetPrivateField<ContextMenuRow>(dialog, "SelectedRow"));
            Assert.NotNull(GetPrivateField<EditorComponentAddDescriptor>(dialog, "SelectedDescriptor"));
            Assert.Equal("Camera", GetPrivateField<EditorComponentAddDescriptor>(dialog, "SelectedDescriptor").DisplayName);
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

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Camera", StringComparison.Ordinal));

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);
            FindPrivateField(dialog.GetType(), "LastActivatedTicks").SetValue(dialog, Environment.TickCount64 - 1);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

            Assert.False(dialog.IsVisible);
            Assert.Contains(entity.Components, value => value is CameraComponent);
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

            panel.ShowEntityProperties(entity);
            InvokePrivate(panel, "HandleAddComponentClicked");

            ComponentAddDialog dialog = GetPrivateField<ComponentAddDialog>(panel, "AddComponentDialog");
            List<ContextMenuRow> rows = GetPrivateField<List<ContextMenuRow>>(dialog, "Rows");
            ContextMenuRow row = rows.First(value => value.Entity.Enabled && string.Equals(value.Label.Text, "Camera", StringComparison.Ordinal));

            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);
            InvokePrivate(dialog, "HandleAddClicked");

            Assert.False(dialog.IsVisible);
            Assert.Contains(entity.Components, value => value is CameraComponent);
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
        /// Ensures the component modals can be attached to a shared top-level host instead of the docked panel.
        /// </summary>
        [Fact]
        public void PropertiesPanel_WhenProvidedSharedModalHost_AttachesComponentDialogsToThatHost() {
            EditorEntity modalHost = new EditorEntity();
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
        /// Ensures the remove button wiring raises a remove request for the correct component.
        /// </summary>
        [Fact]
        public void HandleSectionRemoveClicked_RaisesRemoveRequestedForTheSectionComponent() {
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = CreateEntityWithVisibleComponents();
            Component removedComponent = null;
            view.RemoveRequested += value => removedComponent = value;

            view.ShowComponents(entity);

            List<ComponentSectionView> sections = GetPrivateField<List<ComponentSectionView>>(view, "ActiveSections");

            InvokePrivate(view, "HandleSectionRemoveClicked", sections[0]);

            Assert.Same(sections[0].TargetComponent, removedComponent);
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

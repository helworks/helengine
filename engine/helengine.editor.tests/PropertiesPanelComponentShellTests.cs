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

using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies properties-panel edits emit scene-mutation notifications.
    /// </summary>
    public class PropertiesPanelMutationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the panel tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the properties panel.
        /// </summary>
        public PropertiesPanelMutationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-properties-panel-mutation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Deletes temporary test content and clears shared mutation subscriptions.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures renaming an entity through the properties panel marks the scene dirty.
        /// </summary>
        [Fact]
        public void UpdateTransformEdits_WhenNameChanges_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Original"
            };

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;
                panel.ShowEntityProperties(entity);

                TextBoxComponent nameField = GetPrivateField<TextBoxComponent>(panel, "NameField");
                nameField.Text = "Renamed";
                SetPrivateField(panel, "ApplyTransformRequested", true);
                InvokePrivate(panel, "UpdateTransformEdits");

                Assert.True(raised);
                Assert.Equal("Renamed", entity.Name);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
        }

        /// <summary>
        /// Ensures the first visible properties section leaves a small gap below the panel header.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_PositionsTheFirstSectionBelowTheTopEdge() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Original"
            };

            panel.ShowEntityProperties(entity);

            EditorEntity transformRoot = GetPrivateField<EditorEntity>(panel, "TransformRoot");

            Assert.True(transformRoot.LocalPosition.Y > 0f);
        }

        /// <summary>
        /// Ensures submitting one dynamic Camera scalar field updates the live component value.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenCameraScalarFieldIsSubmitted_UpdatesTheCameraComponent() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            camera.NearPlaneDistance = 0.1f;
            entity.AddComponent(camera);

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow nearPlaneRow = GetSingleRow(view, "Near Plane Distance");
            nearPlaneRow.ScalarField.Text = "2.5";

            MethodInfo submitMethod = typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic);
            submitMethod.Invoke(view, new object[] { nearPlaneRow.ScalarField });

            Assert.Equal(2.5f, camera.NearPlaneDistance, 3);
        }

        /// <summary>
        /// Ensures submitting Camera clear-depth edits writes back the rebuilt clear settings struct.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenClearDepthIsSubmitted_UpdatesCameraClearSettings() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            camera.ClearSettings = new CameraClearSettings(true, new float4(0f, 0f, 0f, 1f), true, 1f, false, 0);
            entity.AddComponent(camera);

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
            InvokeNestedSectionToggle(view, clearSettingsRow);

            ComponentPropertyRow clearDepthRow = GetSingleRow(view, "Clear Depth");
            clearDepthRow.ScalarField.Text = "0.25";

            MethodInfo submitMethod = typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic);
            submitMethod.Invoke(view, new object[] { clearDepthRow.ScalarField });

            Assert.Equal(0.25f, camera.ClearSettings.ClearDepth, 3);
        }

        /// <summary>
        /// Ensures toggling Camera clear-color enabled writes back the rebuilt clear settings struct.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenClearColorEnabledChanges_UpdatesCameraClearSettings() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            camera.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 1f), true, 1f, false, 0);
            entity.AddComponent(camera);

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
            InvokeNestedSectionToggle(view, clearSettingsRow);

            ComponentPropertyRow clearColorEnabledRow = GetSingleRow(view, "Clear Color Enabled");

            MethodInfo checkedChangedMethod = typeof(ComponentPropertiesView).GetMethod("HandleBooleanCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            checkedChangedMethod.Invoke(view, new object[] { clearColorEnabledRow.CheckBoxField, true });

            Assert.True(camera.ClearSettings.ClearColorEnabled);
        }

        /// <summary>
        /// Ensures editing clear-color state on one suppressed scene camera updates authored suppression data without unsuppressing the live runtime camera.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenSuppressedCameraClearColorEnabledChanges_KeepsTheLiveCameraSuppressed() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity();
            CameraComponent camera = new CameraComponent();
            camera.ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 1f), true, 1f, false, 0);
            entity.AddComponent(camera);
            EditorSceneCameraSuppressionService.AttachAndSuppress(entity);

            panel.ShowEntityProperties(entity);

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow clearSettingsRow = GetSingleRow(view, "Clear Settings");
            InvokeNestedSectionToggle(view, clearSettingsRow);

            ComponentPropertyRow clearColorEnabledRow = GetSingleRow(view, "Clear Color Enabled");
            MethodInfo checkedChangedMethod = typeof(ComponentPropertiesView).GetMethod("HandleBooleanCheckedChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            checkedChangedMethod.Invoke(view, new object[] { clearColorEnabledRow.CheckBoxField, true });

            EditorSceneCameraSuppressionComponent suppressionState = EditorSceneCameraSuppressionService.GetSuppressionState(camera);
            Assert.NotNull(suppressionState);
            Assert.False(camera.ClearSettings.ClearColorEnabled);
            Assert.True(suppressionState.ClearSettings.ClearColorEnabled);
        }

        /// <summary>
        /// Ensures editing a component on a platform tab creates an independent override without mutating the common live component.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenWindowsTabScalarFieldIsSubmitted_CreatesIndependentOverrideWithoutChangingTheCommonComponent() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Camera"
            };
            CameraComponent camera = new CameraComponent {
                FarPlaneDistance = 100f
            };
            entity.AddComponent(camera);

            panel.ShowEntityProperties(entity);
            SelectInspectorPlatform(panel, "windows");

            ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
            ComponentPropertyRow farPlaneRow = GetSingleRow(view, "Far Plane Distance");
            farPlaneRow.ScalarField.Text = "200";

            MethodInfo submitMethod = typeof(ComponentPropertiesView).GetMethod("HandleScalarSubmitted", BindingFlags.Instance | BindingFlags.NonPublic);
            submitMethod.Invoke(view, new object[] { farPlaneRow.ScalarField });

            Assert.Equal(100f, camera.FarPlaneDistance);

            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(camera);
            Assert.True(HasPlatformOverride(saveState, "windows"));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Finds one active property row by its label text.
        /// </summary>
        /// <param name="view">Properties view under test.</param>
        /// <param name="label">Row label to resolve.</param>
        /// <returns>The single matching row.</returns>
        ComponentPropertyRow GetSingleRow(ComponentPropertiesView view, string label) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => string.Equals(row.Label.Text, label, StringComparison.Ordinal));
        }

        /// <summary>
        /// Invokes the nested section toggle hook on one provider-backed property row.
        /// </summary>
        /// <param name="view">Properties view under test.</param>
        /// <param name="row">Nested section row to toggle.</param>
        void InvokeNestedSectionToggle(ComponentPropertiesView view, ComponentPropertyRow row) {
            MethodInfo toggleMethod = typeof(ComponentPropertiesView).GetMethod("HandleCustomSectionPressed", BindingFlags.Instance | BindingFlags.NonPublic);
            toggleMethod.Invoke(view, new object[] { row });
        }

        /// <summary>
        /// Switches the component inspector into one platform context.
        /// </summary>
        /// <param name="panel">Panel whose platform context should change.</param>
        /// <param name="platformId">Platform identifier to activate.</param>
        void SelectInspectorPlatform(PropertiesPanel panel, string platformId) {
            MethodInfo method = panel.GetType().GetMethod("HandleComponentPlatformTabChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(panel, new object[] { platformId });
        }

        /// <summary>
        /// Returns whether one component save-state exposes one platform override entry.
        /// </summary>
        /// <param name="saveState">Save-state whose platform override entry should be checked.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>True when one override entry exists for the platform.</returns>
        bool HasPlatformOverride(EntityComponentSaveState saveState, string platformId) {
            MethodInfo method = typeof(EntityComponentSaveState).GetMethod("HasPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method);
            return Assert.IsType<bool>(method.Invoke(saveState, new object[] { platformId }));
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the properties panel.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['N'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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

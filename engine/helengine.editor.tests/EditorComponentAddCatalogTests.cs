using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the component-add catalog used by the properties panel.
    /// </summary>
    public class EditorComponentAddCatalogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the catalog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the reflection-based component catalog tests.
        /// </summary>
        public EditorComponentAddCatalogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-component-add-catalog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();

            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content and clears shared caches after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the catalog does not expose the camera option even when the entity already owns one.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityAlreadyHasCamera_DoesNotIncludeCameraDescriptor() {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(new CameraComponent());

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.DoesNotContain(components, component => string.Equals(component.DisplayName, "Camera", StringComparison.Ordinal));
            Assert.Contains(components, component => string.Equals(component.DisplayName, "Mesh", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the hidden camera icon does not suppress the regular mesh option.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityHasEditorCameraVisualComponent_StillIncludesMeshDescriptor() {
            EditorEntity entity = new EditorEntity();
            EditorCameraVisualAttachmentService.Attach(entity);

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.Contains(components, component => string.Equals(component.DisplayName, "Mesh", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the default component catalog does not expose the camera component as an addable option.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityHasNoComponents_DoesNotIncludeCameraDescriptor() {
            EditorEntity entity = new EditorEntity();

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.DoesNotContain(components, component => string.Equals(component.DisplayName, "Camera", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the reflected catalog still exposes the rotate component even though it inherits the update base type.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityHasNoComponents_StillIncludesRotateDescriptor() {
            EditorEntity entity = new EditorEntity();

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.Contains(components, component => string.Equals(component.DisplayName, "Rotate", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the catalog suppresses duplicate debug overlay descriptors on entities that already own one.
        /// </summary>
        [Fact]
        public void GetAvailableComponents_WhenEntityAlreadyHasDebugComponent_DoesNotIncludeSecondDebugDescriptor() {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(new DebugComponent());

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.GetAvailableComponents(entity);

            Assert.DoesNotContain(components, component => component.ComponentType == typeof(DebugComponent));
        }

        /// <summary>
        /// Ensures the reflected catalog never exposes the base component type as an addable option.
        /// </summary>
        [Fact]
        public void BuildDescriptors_WhenAssemblyContainsBaseComponent_DoesNotExposeBaseComponentDescriptor() {
            Exception exception = Record.Exception(() => EditorComponentAddCatalog.BuildDescriptors(typeof(Component).Assembly));

            Assert.Null(exception);

            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.BuildDescriptors(typeof(Component).Assembly);

            Assert.DoesNotContain(components, component => component.ComponentType == typeof(Component));
        }

        /// <summary>
        /// Ensures the catalog can discover addable component types from a reflected assembly.
        /// </summary>
        [Fact]
        public void BuildDescriptors_WhenAssemblyContainsPublicComponent_ProducesDescriptor() {
            IReadOnlyList<EditorComponentAddDescriptor> components = EditorComponentAddCatalog.BuildDescriptors(typeof(TestReflectedComponent).Assembly);

            Assert.Contains(components, component => component.ComponentType == typeof(TestReflectedComponent));
        }

        /// <summary>
        /// Ensures the mesh add action attaches the reflected mesh component.
        /// </summary>
        [Fact]
        public void AddAction_WhenMeshDescriptorIsInvoked_AttachesMeshComponent() {
            EditorEntity entity = new EditorEntity();
            EditorComponentAddDescriptor meshDescriptor = Assert.Single(EditorComponentAddCatalog.GetAvailableComponents(entity), component => string.Equals(component.DisplayName, "Mesh", StringComparison.Ordinal));

            meshDescriptor.AddAction(entity);

            Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
        }

        /// <summary>
        /// Simple reflected test component used to verify assembly scanning.
        /// </summary>
        public class TestReflectedComponent : Component {
            /// <summary>
            /// Creates a parameterless reflected component instance.
            /// </summary>
            public TestReflectedComponent() {
            }
        }
    }
}


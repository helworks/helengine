using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies preview source selection for asset browser entries and scene selections.
    /// </summary>
    public class PreviewSourceResolverTests : IDisposable {
        /// <summary>
        /// Temporary project root used by preview resolver tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Temporary assets root used by the importer.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Asset import manager configured for deterministic texture loading.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Initializes the core services required by preview source resolution tests.
        /// </summary>
        public PreviewSourceResolverTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-preview-resolver-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(TempProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);

            ContentManager contentManager = new ContentManager(TempProjectRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            AssetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
            AssetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a selected texture asset resolves to the texture preview source.
        /// </summary>
        [Fact]
        public void TryResolve_WhenTextureAssetIsSelected_ReturnsTexturePreviewSource() {
            PreviewSourceResolver resolver = CreateResolver();
            string sourcePath = WriteSourceTexture("Preview.png");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Preview.png",
                "Textures/Preview.png",
                sourcePath,
                ".png",
                AssetEntryKind.Image);

            bool resolved = resolver.TryResolve(entry, null, out IPreviewSource source);

            Assert.True(resolved);
            Assert.IsType<TexturePreviewSource>(source);
        }

        /// <summary>
        /// Ensures a selected model asset resolves to the model preview source.
        /// </summary>
        [Fact]
        public void TryResolve_WhenModelAssetIsSelected_ReturnsModelPreviewSource() {
            PreviewSourceResolver resolver = CreateResolver();
            string sourcePath = WriteSourceModel("Preview.obj");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Preview.obj",
                "Models/Preview.obj",
                sourcePath,
                ".obj",
                AssetEntryKind.Model);

            bool resolved = resolver.TryResolve(entry, null, out IPreviewSource source);

            Assert.True(resolved);
            Assert.IsType<ModelPreviewSource>(source);
        }

        /// <summary>
        /// Ensures a selected camera resolves to the camera preview source.
        /// </summary>
        [Fact]
        public void TryResolve_WhenCameraEntityIsSelected_ReturnsCameraPreviewSource() {
            PreviewSourceResolver resolver = CreateResolver();
            EditorEntity cameraEntity = CreateCameraEntity();

            bool resolved = resolver.TryResolve(null, cameraEntity, out IPreviewSource source);

            Assert.True(resolved);
            Assert.IsType<CameraPreviewSource>(source);
        }

        /// <summary>
        /// Ensures camera selection outranks a texture asset selection when both are available.
        /// </summary>
        [Fact]
        public void TryResolve_WhenCameraAndTextureAreSelected_ReturnsCameraPreviewSource() {
            PreviewSourceResolver resolver = CreateResolver();
            string sourcePath = WriteSourceTexture("Preview.png");
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Preview.png",
                "Textures/Preview.png",
                sourcePath,
                ".png",
                AssetEntryKind.Image);
            EditorEntity cameraEntity = CreateCameraEntity();

            bool resolved = resolver.TryResolve(entry, cameraEntity, out IPreviewSource source);

            Assert.True(resolved);
            Assert.IsType<CameraPreviewSource>(source);
        }

        /// <summary>
        /// Creates a resolver using the deterministic test import manager.
        /// </summary>
        /// <returns>Configured preview resolver.</returns>
        PreviewSourceResolver CreateResolver() {
            return new PreviewSourceResolver(AssetImportManager, Core.Instance.RenderManager2D, Core.Instance.RenderManager3D);
        }

        /// <summary>
        /// Writes one minimal texture source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created source file.</returns>
        string WriteSourceTexture(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }

        /// <summary>
        /// Writes one minimal model source file inside the temporary assets root.
        /// </summary>
        /// <param name="fileName">Source file name to create.</param>
        /// <returns>Absolute path to the created source file.</returns>
        string WriteSourceModel(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }

        /// <summary>
        /// Creates one editor entity with a camera component for resolver tests.
        /// </summary>
        /// <returns>Editor entity with one camera component.</returns>
        EditorEntity CreateCameraEntity() {
            EditorEntity cameraEntity = new EditorEntity();
            float4 orientation;
            float4.CreateFromYawPitchRoll(0.1f, -0.2f, 0f, out orientation);
            cameraEntity.Position = new float3(6f, 2f, -5f);
            cameraEntity.Orientation = orientation;

            CameraComponent camera = new CameraComponent {
                CameraDrawOrder = 12,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(0f, 0f, 128f, 72f)
            };
            cameraEntity.AddComponent(camera);

            return cameraEntity;
        }
    }
}

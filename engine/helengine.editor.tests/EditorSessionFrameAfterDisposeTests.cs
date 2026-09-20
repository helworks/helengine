using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.platforms;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// The title bar's close button disposes the session from inside the frame that is running. The rest of that
    /// frame, and any frame the host loop still delivers, must be a quiet no-op rather than an exception.
    /// </summary>
    public sealed class EditorSessionFrameAfterDisposeTests : IDisposable {
        /// <summary>
        /// Temporary project root for the current test instance.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Canonical project file inside the temporary root.
        /// </summary>
        readonly string ProjectFilePath;

        /// <summary>
        /// Creates a minimal project on disk.
        /// </summary>
        public EditorSessionFrameAfterDisposeTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-frame-after-dispose-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
            ProjectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
            File.WriteAllText(
                ProjectFilePath,
                """
                {
                  "projectFormatVersion": 1,
                  "name": "Frame After Dispose",
                  "requiredEngineVersion": "0.4.0",
                  "supportedPlatforms": [ "windows" ],
                  "created": "2026-04-01T00:00:00Z",
                  "lastOpened": "2026-04-20T00:00:00Z",
                  "version": "1.0.0"
                }
                """);
        }

        /// <summary>
        /// Deletes the temporary project.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a frame delivered to a disposed session neither throws nor raises a first-chance exception.
        /// </summary>
        [Fact]
        public void UpdateFrame_AfterDispose_IsAQuietNoOp() {
            EditorSession session = CreateSession();
            session.Dispose();

            int firstChanceCount = 0;
            string firstStackTrace = string.Empty;
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => {
                firstChanceCount++;
                if (string.IsNullOrEmpty(firstStackTrace)) {
                    firstStackTrace = args.Exception.GetType().Name + ": " + args.Exception.Message + Environment.NewLine + Environment.StackTrace;
                }
            };

            AppDomain.CurrentDomain.FirstChanceException += handler;
            try {
                session.UpdateFrame(1280, 720);
            } finally {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.True(firstChanceCount == 0, $"Saw {firstChanceCount} first-chance exception(s) in a frame after dispose. First:{Environment.NewLine}{firstStackTrace}");
        }

        /// <summary>
        /// Builds a real session against test render managers.
        /// </summary>
        EditorSession CreateSession() {
            EditorCore core = new EditorCore(new Project {
                Name = "Frame After Dispose",
                Path = TempProjectRootPath
            });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());

            return new EditorSession(
                core,
                ProjectFilePath,
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default,
                CreateFont(),
                CreateFont(),
                TestDirectX11RenderManager3D.Create(),
                new TestRenderManager2D(),
                new TestInputBackend(),
                1280,
                720,
                CreateToolbarIcons(),
                CreateTexture(),
                Array.Empty<IAssetImporterRegistration>(),
                ResolveBrowseOutputFolder,
                shaderBackendRegistry,
                new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(TempProjectRootPath)));
        }

        /// <summary>
        /// Browse-folder resolver used by the session.
        /// </summary>
        string ResolveBrowseOutputFolder() {
            return TempProjectRootPath;
        }

        /// <summary>
        /// Minimal font with the glyphs the editor chrome draws.
        /// </summary>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = ' '; character <= '~'; character++) {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 14, 4f),
                new TestRuntimeTexture { Width = 64, Height = 64 },
                characters,
                14f,
                64,
                64);
        }

        /// <summary>
        /// Placeholder toolbar icon set.
        /// </summary>
        static EditorViewportToolbarIconSet CreateToolbarIcons() {
            return new EditorViewportToolbarIconSet(
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture(),
                CreateTexture());
        }

        /// <summary>
        /// Placeholder texture.
        /// </summary>
        static RuntimeTexture CreateTexture() {
            return new TestRuntimeTexture { Width = 16, Height = 16 };
        }
    }
}

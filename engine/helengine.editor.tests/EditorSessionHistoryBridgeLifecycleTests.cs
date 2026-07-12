using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.projectfile;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the real editor-session constructor and dispose path initialize and clear the static history bridges used by editor tooling.
    /// </summary>
    public sealed class EditorSessionHistoryBridgeLifecycleTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the current lifecycle test.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Canonical project file path used by the real editor-session constructor.
        /// </summary>
        readonly string ProjectFilePath;

        /// <summary>
        /// Initializes one temporary project root and canonical project file for the current lifecycle test.
        /// </summary>
        public EditorSessionHistoryBridgeLifecycleTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-history-bridge-lifecycle-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));
            ProjectFilePath = Path.Combine(TempProjectRootPath, "project.heproj");
            WriteCanonicalProjectFile(ProjectFilePath);
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
        }

        /// <summary>
        /// Clears shared static history bridges and removes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorEntityHistoryMutationService.Reset();
            EditorComponentHistoryMutationService.Reset();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the real editor-session constructor populates both static history bridges and dispose clears them again.
        /// </summary>
        [Fact]
        public void Constructor_and_dispose_initialize_and_clear_the_static_history_bridges() {
            EditorSession session = CreateSession();

            try {
                Assert.NotNull(EditorEntityHistoryMutationService.CaptureEntityState);
                Assert.NotNull(EditorEntityHistoryMutationService.RecordEntityStateChange);
                Assert.NotNull(EditorComponentHistoryMutationService.CaptureEntityState);
                Assert.NotNull(EditorComponentHistoryMutationService.RecordComponentMutation);
                Assert.NotNull(session.ComponentHistoryAdapters);
            } finally {
                session.Dispose();
            }

            Assert.Null(EditorEntityHistoryMutationService.CaptureEntityState);
            Assert.Null(EditorEntityHistoryMutationService.RecordEntityStateChange);
            Assert.Null(EditorComponentHistoryMutationService.CaptureEntityState);
            Assert.Null(EditorComponentHistoryMutationService.RecordComponentMutation);
        }

        /// <summary>
        /// Creates one real editor session backed by a temporary canonical project file.
        /// </summary>
        /// <returns>Initialized editor session.</returns>
        EditorSession CreateSession() {
            EditorCore core = new EditorCore(new Project {
                Name = "History Bridge Lifecycle",
                Path = TempProjectRootPath
            });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);

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
                shaderBackendRegistry);
        }

        /// <summary>
        /// Resolves one deterministic output folder for services that require a browse callback.
        /// </summary>
        /// <returns>Temporary project root path.</returns>
        string ResolveBrowseOutputFolder() {
            return TempProjectRootPath;
        }

        /// <summary>
        /// Writes one valid canonical project file consumed by the editor-session constructor.
        /// </summary>
        /// <param name="projectFilePath">Project file path to create.</param>
        void WriteCanonicalProjectFile(string projectFilePath) {
            File.WriteAllText(
                projectFilePath,
                """
                {
                  "projectFormatVersion": 1,
                  "name": "History Bridge Lifecycle",
                  "requiredEngineVersion": "0.4.0",
                  "supportedPlatforms": [ "windows" ],
                  "created": "2026-04-01T00:00:00Z",
                  "lastOpened": "2026-04-20T00:00:00Z",
                  "version": "1.0.0"
                }
                """);
        }

        /// <summary>
        /// Creates one deterministic toolbar icon set for the session constructor.
        /// </summary>
        /// <returns>Toolbar icon set backed by test textures.</returns>
        EditorViewportToolbarIconSet CreateToolbarIcons() {
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
                CreateTexture());
        }

        /// <summary>
        /// Creates one deterministic runtime texture.
        /// </summary>
        /// <returns>Runtime texture with a stable size.</returns>
        RuntimeTexture CreateTexture() {
            return new TestRuntimeTexture {
                Width = 16,
                Height = 16
            };
        }

        /// <summary>
        /// Creates one deterministic font asset that satisfies editor-session UI layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph coverage.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .:-_[]";
            for (int index = 0; index < glyphs.Length; index++) {
                char glyph = glyphs[index];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                float width = glyph == ' ' ? 4f : 8f;
                characters[glyph] = new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f);
            }

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

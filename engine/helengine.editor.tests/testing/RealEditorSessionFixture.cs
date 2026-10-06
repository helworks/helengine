using helengine.directx11;
using helengine.platforms;
using helengine.ui;
using helengine.vulkan;

namespace helengine.editor.tests.testing {
    /// <summary>Runs the production session constructor and frame loop with deterministic rendering and input in a visible build artifact directory.</summary>
    public sealed class RealEditorSessionFixture : IDisposable {
        /// <summary>Creates a minimal valid project and all host dependencies required by real editor startup.</summary>
        public RealEditorSessionFixture() {
            string root = Path.Combine(AppContext.BaseDirectory, "test-artifacts", "session-framing", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            string projectFile = Path.Combine(root, "project.heproj");
            File.WriteAllText(projectFile, """
                {
                  "projectFormatVersion": 1,
                  "name": "Session Framing",
                  "requiredEngineVersion": "0.4.0",
                  "supportedPlatforms": [ "windows" ],
                  "created": "2026-04-01T00:00:00Z",
                  "lastOpened": "2026-04-20T00:00:00Z",
                  "version": "1.0.0"
                }
                """);
            Core = new EditorCore(new Project { Name = "Session Framing", Path = root });
            ShaderBackendRegistry shaders = new ShaderBackendRegistry();
            shaders.Register(new DirectX11ShaderBackend());
            shaders.Register(new VulkanShaderBackend());
            Input = new TestInputBackend();
            RuntimeTexture texture = new TestRuntimeTexture { Width = 64, Height = 64 };
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = ' '; character <= '~'; character++) {
                characters[character] = new FontChar(new float4(0, 0, 8, 12), 0, 8, 0, 0);
            }
            Font = new FontAsset(new FontInfo("Test", 14, 4), texture, characters, 14, 64, 64);
            Session = new EditorSession(Core, projectFile,
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default, Font, Font, TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), Input,
                1280, 720, new EditorViewportToolbarIconSet(texture, texture, texture, texture, texture, texture, texture, texture, texture, texture, texture),
                texture, Array.Empty<IAssetImporterRegistration>(), () => root, shaders,
                new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(root)),
                pickingBackendFactory: new TestEditorPickingBackendFactory());
            Session.UpdateFrame(1280, 720);
        }

        /// <summary>Gets the core owned and updated by the session.</summary>
        public EditorCore Core { get; }

        /// <summary>Gets the raw keyboard and pointer backend captured during each frame.</summary>
        internal TestInputBackend Input { get; }

        /// <summary>Gets the shared font required by focusable test text entries.</summary>
        public FontAsset Font { get; }

        /// <summary>Gets the fully initialized session with its ordinary workspace and shortcut registrations.</summary>
        public EditorSession Session { get; }

        /// <summary>Disposes the complete session graph and all viewport-owned backends.</summary>
        public void Dispose() {
            Session.Dispose();
        }
    }
}

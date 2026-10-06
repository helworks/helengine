using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.platforms;
using helengine.ui;
using helengine.vulkan;

namespace helengine.editor.tests {
    /// <summary>Checks native picking is connected through real session startup, dynamic viewport creation, and session teardown.</summary>
    public sealed class EditorSessionPickingInitializationTests {
        /// <summary>Ensures both initial and duplicate viewports receive independent pickers from the host capability.</summary>
        [Theory]
        [InlineData(EditorViewportToolMode.Translate)]
        [InlineData(EditorViewportToolMode.Rotate)]
        [InlineData(EditorViewportToolMode.Scale)]
        public void Constructor_PropagatesPickingToPrimaryAndDuplicateViewports(EditorViewportToolMode tool) {
            string projectRoot = Path.Combine(AppContext.BaseDirectory, "test-artifacts", "session-picking", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRoot, "assets"));
            string projectFile = Path.Combine(projectRoot, "project.heproj");
            File.WriteAllText(projectFile, """
                {
                  "projectFormatVersion": 1,
                  "name": "Picking Initialization",
                  "requiredEngineVersion": "0.4.0",
                  "supportedPlatforms": [ "windows" ],
                  "created": "2026-04-01T00:00:00Z",
                  "lastOpened": "2026-04-20T00:00:00Z",
                  "version": "1.0.0"
                }
                """);
            EditorCore core = new EditorCore(new Project { Name = "Picking Initialization", Path = projectRoot });
            ShaderBackendRegistry shaders = new ShaderBackendRegistry();
            shaders.Register(new DirectX11ShaderBackend());
            shaders.Register(new VulkanShaderBackend());
            TestEditorPickingBackendFactory factory = new TestEditorPickingBackendFactory();
            TestInputBackend input = new TestInputBackend();
            RuntimeTexture texture = new TestRuntimeTexture { Width = 16, Height = 16 };
            EditorSession session = new EditorSession(
                core, projectFile,
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default, CreateFont(), CreateFont(),
                TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), input,
                1280, 720,
                new EditorViewportToolbarIconSet(texture, texture, texture, texture, texture, texture, texture, texture, texture, texture, texture),
                texture, Array.Empty<IAssetImporterRegistration>(), () => projectRoot, shaders,
                new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(projectRoot)),
                pickingBackendFactory: factory);
            try {
                CameraComponent primaryPicker = Assert.Single(factory.Cameras);
                Assert.Single(core.ObjectManager.Entities.SelectMany(entity => entity.Components).OfType<EditorViewportPicker>());
                AssertCameraGizmoInteraction(session, core, input, Assert.Single(factory.Backends), tool);
                session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
                Assert.Equal(2, factory.Cameras.Count);
                Assert.NotSame(primaryPicker, factory.Cameras[1]);
                Assert.Equal(2, core.ObjectManager.Entities.SelectMany(entity => entity.Components).OfType<EditorViewportPicker>().Count());
                Assert.All(factory.Cameras, camera => Assert.False(camera.Parent.Enabled));
                Assert.All(factory.Backends, backend => Assert.False(backend.IsDisposed));
            } finally {
                session.Dispose();
            }
            Assert.Equal(2, factory.Backends.Count);
            Assert.All(factory.Backends, backend => Assert.True(backend.IsDisposed));
        }

        /// <summary>Exercises camera-entity hover, highlight, press, drag, and release through the actual session frame loop and generated handle hierarchy.</summary>
        /// <param name="session">Fully constructed editor session.</param>
        /// <param name="core">Session-owned editor core.</param>
        /// <param name="input">Mouse backend sampled by the ordinary core frame loop.</param>
        /// <param name="backend">Host backend supplying an axis readback for this viewport.</param>
        /// <param name="tool">Transform operation to verify on the authored camera.</param>
        static void AssertCameraGizmoInteraction(EditorSession session, EditorCore core, TestInputBackend input, TestEditorPickingBackend backend, EditorViewportToolMode tool) {
            session.UpdateFrame(1280, 720);
            CameraComponent viewport = session.MainViewport.Camera;
            EditorEntity selected = new EditorEntity(core, session.InteractionServices) {
                Name = "Scene Camera", IsSceneOwned = true, LayerMask = EditorLayerMasks.SceneObjects
            };
            selected.AddComponent(new CameraComponent { LayerMask = EditorLayerMasks.SceneObjects });
            selected.Components.OfType<EntitySaveComponent>().Single().EntityId = core.SceneEntityIdAllocator.Allocate();
            selected.InitializeHierarchy();
            session.InteractionServices.Selection.SetSelectedEntity(selected);
            session.InteractionServices.ViewportTool.SetToolMode(viewport, tool);
            int x = (int)(viewport.Viewport.X + viewport.Viewport.Z / 2) + 40;
            int y = (int)(viewport.Viewport.Y + viewport.Viewport.W / 2);
            input.SetMouseState(new MouseState(x, y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            session.UpdateFrame(1280, 720);
            Assert.NotEmpty(backend.LastColors);
            KeyValuePair<IDrawable3D, byte4> pick = backend.LastColors.First(pair =>
                ((EditorEntity)ResolveHandle(pair.Key)).Name.EndsWith(" X", StringComparison.Ordinal));
            Entity handle = ResolveHandle(pick.Key);
            MeshComponent mesh = Assert.IsType<MeshComponent>(pick.Key);
            RuntimeMaterial normal = Assert.Single(mesh.Materials);
            backend.ReadbackColor = pick.Value;
            backend.IsReadbackReady = true;
            session.UpdateFrame(1280, 720);
            Assert.Same(handle, session.InteractionServices.GizmoHover.GetHoveredHandle(viewport));
            Assert.NotSame(normal, Assert.Single(mesh.Materials));
            input.SetMouseState(new MouseState(x, y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            Assert.True(session.InteractionServices.GizmoDrag.IsDragging(viewport));
            float3 position = selected.Position;
            float3 scale = selected.Scale;
            float4 orientation = selected.Orientation;
            input.SetMouseState(new MouseState(x + 30, y + 20, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            Assert.True(tool == EditorViewportToolMode.Translate ? selected.Position != position
                : tool == EditorViewportToolMode.Scale ? selected.Scale != scale : !selected.Orientation.Equals(orientation));
            input.SetMouseState(new MouseState(x + 30, y + 20, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            Assert.False(session.InteractionServices.GizmoDrag.IsDragging(viewport));
            Assert.Same(selected, session.InteractionServices.Selection.SelectedEntity);
        }

        /// <summary>Finds the handle that owns a generated shaft, tip, ring, or plane drawable.</summary>
        /// <param name="drawable">Drawable submitted by the production picker collector.</param>
        /// <returns>Owning entity carrying the drag constraint.</returns>
        static Entity ResolveHandle(IDrawable3D drawable) {
            for (Entity current = drawable.Parent; current != null; current = current.Parent) {
                if (current.Components.OfType<TransformGizmoHandleComponent>().Any()) {
                    return current;
                }
            }
            throw new InvalidOperationException("Generated gizmo drawable has no handle owner.");
        }

        /// <summary>Creates the ASCII glyph set required by real editor chrome without native font import.</summary>
        /// <returns>Deterministic font backed by the test renderer's texture.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = ' '; character <= '~'; character++) {
                characters[character] = new FontChar(new float4(0, 0, 8, 12), 0, 8, 0, 0);
            }
            return new FontAsset(new FontInfo("Test", 14, 4), new TestRuntimeTexture { Width = 64, Height = 64 }, characters, 14, 64, 64);
        }
    }
}

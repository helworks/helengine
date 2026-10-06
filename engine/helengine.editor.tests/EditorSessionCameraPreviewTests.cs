using System.Reflection;
using helengine.directx11;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.platforms;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>Checks camera-preview updates through the complete editor session, docking layout, and Inspector.</summary>
    public sealed class EditorSessionCameraPreviewTests : IDisposable {
        /// <summary>Workspace-owned project used to exercise the real session without changing a user project.</summary>
        readonly string ProjectRoot;
        /// <summary>Editor core whose update loop drives the dock panels.</summary>
        readonly EditorCore CoreValue;
        /// <summary>Complete session that owns selection, Inspector, and preview wiring.</summary>
        readonly EditorSession Session;
        /// <summary>Deterministic pointer state used to drive the production translation gizmo.</summary>
        readonly TestInputBackend InputValue = new TestInputBackend();

        /// <summary>Creates a minimal project and full editor session using resource-only test renderers.</summary>
        public EditorSessionCameraPreviewTests() {
            ProjectRoot = Path.Combine(Path.GetDirectoryName(TestSourceRepositoryLocator.ResolveHelEngineRootPath()),
                "builds", "helengine", "camera-preview-session", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "assets"));
            string projectFile = Path.Combine(ProjectRoot, "project.heproj");
            File.WriteAllText(projectFile, """
                { "projectFormatVersion": 1, "name": "Live camera preview", "requiredEngineVersion": "0.4.0",
                  "supportedPlatforms": [ "windows" ], "created": "2026-04-01T00:00:00Z",
                  "lastOpened": "2026-04-20T00:00:00Z", "version": "1.0.0" }
                """);
            CoreValue = new EditorCore(new Project { Name = "Live camera preview", Path = ProjectRoot });
            ShaderBackendRegistry backends = new ShaderBackendRegistry();
            backends.Register(new DirectX11ShaderBackend());
            FontAsset font = CreateFont();
            RuntimeTexture icon = new TestRuntimeTexture { Width = 16, Height = 16 };
            Session = new EditorSession(CoreValue, projectFile,
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default, font, font, TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(),
                InputValue, 1280, 720,
                new EditorViewportToolbarIconSet(icon, icon, icon, icon, icon, icon, icon, icon, icon, icon, icon), icon,
                Array.Empty<IAssetImporterRegistration>(), () => ProjectRoot, backends,
                new AvailablePlatformProviderResolver(new PlatformDiscoveryOptions(ProjectRoot)));
        }

        /// <summary>Releases session-owned entities before removing the isolated workspace project.</summary>
        public void Dispose() {
            Session.Dispose();
            CoreValue.Dispose();
            Directory.Delete(ProjectRoot, true);
        }

        /// <summary>Later frames mirror direct camera movement and committed Inspector position edits without reselection.</summary>
        [Fact]
        public void UpdateFrame_AfterFirstCameraRender_FollowsCameraAndInspectorEdits() {
            Session.UpdateFrame(1280, 720);
            EditorEntity entity = Assert.IsType<EditorEntity>(CoreValue.EntityFactory.Create("Preview camera"));
            entity.LayerMask = EditorLayerMasks.SceneObjects;
            entity.Position = new float3(32, 32, 100);
            CameraComponent camera = new CameraComponent { Viewport = new float4(0, 0, 1280, 720) };
            entity.AddComponent(camera);
            entity.InitializeHierarchy();
            EditorSceneCameraSuppressionService.AttachAndSuppress(entity, CoreValue.ObjectManager);
            Session.InteractionServices.Selection.SetSelectedEntity(entity);
            PreviewPanel panel = ReadField<PreviewPanel>(Session, "previewPanel");
            Session.UpdateFrame(1280, 720);
            CameraPreviewSource preview = Assert.IsType<CameraPreviewSource>(panel.ActivePreviewSource);
            Assert.Equal(entity.Position, preview.PreviewCamera.Parent.Position);
            Assert.Contains(preview.PreviewCamera, CoreValue.ObjectManager.Cameras);

            entity.Position = new float3(512, 32, 100);
            Session.UpdateFrame(1280, 720);
            Assert.Same(preview, panel.ActivePreviewSource);
            Assert.Equal(entity.Position, preview.PreviewCamera.Parent.Position);
            Assert.Contains(preview.PreviewCamera, CoreValue.ObjectManager.Cameras);

            PropertiesPanel properties = ReadField<PropertiesPanel>(Session, "propertiesPanel");
            TextBoxComponent[] fields = ReadField<TextBoxComponent[]>(properties, "PositionFields");
            fields[0].Text = "256";
            typeof(PropertiesPanel).GetMethod("HandleTransformSubmitted", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(properties, new object[] { fields[0] });
            Session.UpdateFrame(1280, 720);
            Session.UpdateFrame(1280, 720);
            Assert.Equal(new float3(256, 32, 100), entity.Position);
            Assert.Equal(entity.Position, preview.PreviewCamera.Parent.Position);
            Assert.Contains(preview.PreviewCamera, CoreValue.ObjectManager.Cameras);
        }

        /// <summary>A camera moved by the gizmo must reach its preview in the same editor frame, including after reselection.</summary>
        [Fact]
        public void UpdateFrame_WhenGizmoMovesCamera_UsesCurrentFramePoseAfterReselection() {
            Session.UpdateFrame(1280, 720);
            EditorEntity entity = Assert.IsType<EditorEntity>(CoreValue.EntityFactory.Create("Dragged camera"));
            entity.LayerMask = EditorLayerMasks.SceneObjects;
            CameraComponent camera = new CameraComponent { Viewport = new float4(0, 0, 500, 400) };
            entity.AddComponent(camera);
            entity.InitializeHierarchy();
            EditorSceneCameraSuppressionService.AttachAndSuppress(entity, CoreValue.ObjectManager);
            Session.InteractionServices.Selection.SetSelectedEntity(entity);
            PreviewPanel panel = ReadField<PreviewPanel>(Session, "previewPanel");
            Session.UpdateFrame(1280, 720);

            EditorEntity owner = new EditorEntity(CoreValue, Session.InteractionServices) { InternalEntity = true };
            EditorViewportCameraComponent sceneCamera = new EditorViewportCameraComponent {
                Viewport = new float4(0, 0, 500, 400), ProjectionMode = CameraProjectionMode.Orthographic,
                OrthographicVerticalSpan = 20, FarPlaneDistance = 1000
            };
            owner.Position = new float3(0, 0, 100);
            owner.AddComponent(sceneCamera);
            TransformTranslationGizmoDragComponent drag = new TransformTranslationGizmoDragComponent(sceneCamera);
            owner.AddComponent(drag);
            owner.InitializeHierarchy();
            Session.InteractionServices.ViewportTool.SetToolMode(sceneCamera, EditorViewportToolMode.Translate);
            SetField(drag, "IsDragging", true);
            SetField(drag, "DraggedEntity", entity);
            SetField(drag, "DragHandleEntity", owner);
            SetField(drag, "DragConstraintType", TransformGizmoHandleConstraintType.Plane);
            SetField(drag, "DragPrimaryDirection", float3.UnitX);
            SetField(drag, "DragSecondaryDirection", new float3(0, -1, 0));
            SetField(drag, "DragPlaneNormal", new float3(0, 0, 1));
            SetField(drag, "DragStartEntityPosition", float3.Zero);
            SetField(drag, "DragStartPlanePoint", float3.Zero);
            InputValue.SetMouseState(new MouseState(270, 200, 0, ButtonState.Pressed,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Session.UpdateFrame(1280, 720);
            CameraPreviewSource preview = Assert.IsType<CameraPreviewSource>(panel.ActivePreviewSource);
            Assert.InRange(entity.Position.X, 0.999f, 1.001f);
            Assert.Equal(entity.Position, preview.PreviewCamera.Parent.Position);

            SetField(drag, "DragChanged", false);
            SetField(drag, "IsDragging", false);
            Session.InteractionServices.Selection.ClearSelection();
            Session.InteractionServices.Selection.SetSelectedEntity(entity);
            CameraPreviewSource rebound = Assert.IsType<CameraPreviewSource>(panel.ActivePreviewSource);
            Assert.NotSame(preview, rebound);
            Assert.Equal(entity.Position, rebound.PreviewCamera.Parent.Position);
        }

        /// <summary>Reads existing session composition state without bypassing its update loop.</summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="owner">Session or panel that owns the field.</param>
        /// <param name="name">Private composition field name.</param>
        /// <returns>Value installed by the production constructor.</returns>
        static T ReadField<T>(object owner, string name) {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }

        /// <summary>Seeds an already active drag so the regression exercises real frame ordering rather than GPU picking.</summary>
        /// <param name="owner">Production drag component.</param>
        /// <param name="name">Captured drag-state field.</param>
        /// <param name="value">Initial state produced by a successful gizmo press.</param>
        static void SetField(object owner, string name, object value) {
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        }

        /// <summary>Provides basic glyph metrics for real panel construction without loading a platform font.</summary>
        /// <returns>Resource-only font covering editor chrome text.</returns>
        static FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = ' '; character <= '~'; character++) {
                characters[character] = new FontChar(new float4(0, 0, 0.125f, 0.1875f), 0, 8, 0, 0);
            }
            return new FontAsset(new FontInfo("Test", 14, 4), new TestRuntimeTexture { Width = 64, Height = 64 },
                characters, 14, 64, 64);
        }
    }
}

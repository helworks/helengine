using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    public sealed class EntityEnvironmentOverrideEditingServiceTests : IDisposable {
        public EntityEnvironmentOverrideEditingServiceTests() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new FakeContentStreamSource()
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        public void Dispose() {
            Core.Instance?.Dispose();
        }

        [Fact]
        public void ResolveExists_WhenEnvironmentOverrideExists_UsesPlatformThenEnvironmentInheritance() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();

            service.SetExists(saveComponent, new EditorOverrideScope("windows"), false);
            service.SetExists(saveComponent, new EditorOverrideScope("windows", "debug"), true);

            Assert.False(service.ResolveExists(saveComponent, new EditorOverrideScope("windows", "release")));
            Assert.True(service.ResolveExists(saveComponent, new EditorOverrideScope("windows", "debug")));
        }

        [Fact]
        public void SetExists_WhenEnvironmentMatchesPlatform_RemovesOnlyEnvironmentOverride() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();
            EditorOverrideScope platformScope = new EditorOverrideScope("windows");
            EditorOverrideScope environmentScope = new EditorOverrideScope("windows", "debug");

            service.SetExists(saveComponent, platformScope, false);
            service.SetExists(saveComponent, environmentScope, false);

            Assert.True(saveComponent.TryGetExistencePlatformOverride(platformScope, out _));
            Assert.False(saveComponent.TryGetExistencePlatformOverride(environmentScope, out _));
        }

        [Fact]
        public void ActivateScope_WhenEnvironmentOverrideExists_ProjectsPlatformThenEnvironmentTransform() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LocalPosition = new float3(1f, 2f, 3f),
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformTransformEditingService service = new EntityPlatformTransformEditingService();
            EditorOverrideScope platformScope = new EditorOverrideScope("windows");
            EditorOverrideScope environmentScope = new EditorOverrideScope("windows", "debug");

            saveComponent.SetTransformPlatformOverride(platformScope, new SceneEntityPlatformTransformOverrideAsset {
                HasLocalPositionOverride = true,
                LocalPosition = new float3(10f, 10f, 10f)
            });
            saveComponent.SetTransformPlatformOverride(environmentScope, new SceneEntityPlatformTransformOverrideAsset {
                HasLocalScaleOverride = true,
                LocalScale = new float3(2f, 2f, 2f)
            });

            service.ActivateScope(entity, saveComponent, environmentScope);

            Assert.Equal(new float3(10f, 10f, 10f), entity.LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), entity.LocalScale);
        }

        [Fact]
        public void ResolveEditableComponent_WhenEnvironmentOverrideExists_ProjectsPlatformThenEnvironmentProperty() {
            CameraComponent commonComponent = new CameraComponent {
                FarPlaneDistance = 100f
            };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            ComponentPlatformEditingService service = new ComponentPlatformEditingService();
            EditorOverrideScope platformScope = new EditorOverrideScope("windows");
            EditorOverrideScope environmentScope = new EditorOverrideScope("windows", "debug");

            CameraComponent platformComponent = Assert.IsType<CameraComponent>(service.EnsurePlatformOverrideComponent(commonComponent, saveComponent, "windows"));
            platformComponent.FarPlaneDistance = 200f;
            service.MarkPropertyOverride(commonComponent, saveComponent, "windows", nameof(CameraComponent.FarPlaneDistance));
            service.PersistPlatformOverride(commonComponent, platformComponent, saveComponent, "windows");

            CameraComponent environmentComponent = Assert.IsType<CameraComponent>(service.EnsureScopeOverrideComponent(commonComponent, saveComponent, environmentScope));
            environmentComponent.FarPlaneDistance = 300f;
            service.MarkScopePropertyOverride(commonComponent, saveComponent, environmentScope, nameof(CameraComponent.FarPlaneDistance));
            service.PersistScopeOverride(commonComponent, environmentComponent, saveComponent, environmentScope);

            CameraComponent loadedDebug = Assert.IsType<CameraComponent>(service.ResolveEditableComponent(commonComponent, saveComponent, environmentScope));
            CameraComponent loadedRelease = Assert.IsType<CameraComponent>(service.ResolveEditableComponent(commonComponent, saveComponent, platformScope));
            Assert.Equal(300f, loadedDebug.FarPlaneDistance);
            Assert.Equal(200f, loadedRelease.FarPlaneDistance);
        }
    }
}

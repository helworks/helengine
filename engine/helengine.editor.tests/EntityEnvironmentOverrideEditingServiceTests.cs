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

        [Fact]
        public void ResolveExists_WhenGroupChainIsAuthored_UsesTheDeepestPrefix() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));
            EditorOverrideScope handheldPsp = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "psp"));

            service.SetExists(saveComponent, EditorOverrideScope.Common, false);
            service.SetExists(saveComponent, handheld, true);
            service.SetExists(saveComponent, handheldPsp, false);

            Assert.True(service.ResolveExists(saveComponent, handheldDs));
            Assert.False(service.ResolveExists(saveComponent, handheldPsp));
            Assert.False(service.ResolveExists(saveComponent, new EditorOverrideScope("ps1")));
            Assert.False(service.ResolveExists(saveComponent, EditorOverrideScope.Common));
        }

        [Fact]
        public void SetExists_OnCommon_StoresFalseAndRemovesTrue() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();

            service.SetExists(saveComponent, EditorOverrideScope.Common, false);
            Assert.True(saveComponent.TryGetExistencePlatformOverride(EditorOverrideScope.Common, out SceneEntityPlatformExistenceOverrideAsset stored));
            Assert.Empty(stored.Scope);
            Assert.False(stored.Exists);

            service.SetExists(saveComponent, EditorOverrideScope.Common, true);
            Assert.False(saveComponent.TryGetExistencePlatformOverride(EditorOverrideScope.Common, out _));
        }

        [Fact]
        public void SetExists_WhenValueMatchesParentPrefix_RemovesTheDeeperOverride() {
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformExistenceEditingService service = new EntityPlatformExistenceEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDsDebug = handheld
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"))
                .Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            service.SetExists(saveComponent, handheld, false);
            service.SetExists(saveComponent, handheldDsDebug, true);
            Assert.True(saveComponent.TryGetExistencePlatformOverride(handheldDsDebug, out _));

            service.SetExists(saveComponent, handheldDsDebug, false);
            Assert.False(saveComponent.TryGetExistencePlatformOverride(handheldDsDebug, out _));
            Assert.True(service.HasExistenceOverride(saveComponent, handheld));
            Assert.False(service.HasExistenceOverride(saveComponent, handheldDsDebug));
        }

        [Fact]
        public void ActivateScope_WhenGroupPlatformAndConfigAreAuthored_FoldsEveryPrefix() {
            EditorEntity entity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) {
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            EntityPlatformTransformEditingService service = new EntityPlatformTransformEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));
            EditorOverrideScope handheldDsDebug = handheldDs.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.BuildConfig, "debug"));

            saveComponent.SetTransformPlatformOverride(handheld, new SceneEntityPlatformTransformOverrideAsset { HasLocalPositionOverride = true, LocalPosition = new float3(1f, 0f, 0f) });
            saveComponent.SetTransformPlatformOverride(handheldDs, new SceneEntityPlatformTransformOverrideAsset { HasLocalScaleOverride = true, LocalScale = new float3(3f, 3f, 3f) });
            saveComponent.SetTransformPlatformOverride(handheldDsDebug, new SceneEntityPlatformTransformOverrideAsset { HasLocalPositionOverride = true, LocalPosition = new float3(2f, 0f, 0f) });

            service.ActivateScope(entity, saveComponent, handheldDsDebug);
            Assert.Equal(new float3(2f, 0f, 0f), entity.LocalPosition);
            Assert.Equal(new float3(3f, 3f, 3f), entity.LocalScale);
            Assert.Equal(handheldDsDebug, saveComponent.ActiveTransformScope);

            entity.LocalScale = new float3(3f, 3f, 3f);
            entity.LocalPosition = new float3(1f, 0f, 0f);
            service.RestoreCommonScope(entity, saveComponent);

            Assert.False(saveComponent.TryGetTransformPlatformOverride(handheldDsDebug, out _));
            Assert.True(saveComponent.ActiveTransformScope.IsCommon);
            Assert.Equal(float3.Zero, entity.LocalPosition);
        }

        [Fact]
        public void ResolveEditableComponent_WhenGroupAndPlatformOverridesExist_LayersGroupThenPlatform() {
            CameraComponent commonComponent = new CameraComponent { FarPlaneDistance = 100f, NearPlaneDistance = 1f };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            ComponentPlatformEditingService service = new ComponentPlatformEditingService();
            EditorOverrideScope handheld = EditorOverrideScope.FromSteps(SceneOverrideScopePath.Group("handheld"));
            EditorOverrideScope handheldDs = handheld.Append(new EditorOverrideScopeStep(SceneOverrideScopeStepKind.Platform, "ds"));

            CameraComponent groupComponent = Assert.IsType<CameraComponent>(service.EnsureScopeOverrideComponent(commonComponent, saveComponent, handheld));
            groupComponent.FarPlaneDistance = 200f;
            service.MarkScopePropertyOverride(commonComponent, saveComponent, handheld, nameof(CameraComponent.FarPlaneDistance));
            service.PersistScopeOverride(commonComponent, groupComponent, saveComponent, handheld);

            CameraComponent platformComponent = Assert.IsType<CameraComponent>(service.EnsureScopeOverrideComponent(commonComponent, saveComponent, handheldDs));
            Assert.Equal(200f, platformComponent.FarPlaneDistance);
            platformComponent.NearPlaneDistance = 5f;
            service.MarkScopePropertyOverride(commonComponent, saveComponent, handheldDs, nameof(CameraComponent.NearPlaneDistance));
            service.PersistScopeOverride(commonComponent, platformComponent, saveComponent, handheldDs);

            CameraComponent resolved = Assert.IsType<CameraComponent>(service.ResolveEditableComponent(commonComponent, saveComponent, handheldDs));
            Assert.Equal(200f, resolved.FarPlaneDistance);
            Assert.Equal(5f, resolved.NearPlaneDistance);
            Assert.True(service.IsScopePropertyOverrideActive(commonComponent, resolved, saveComponent, handheldDs, nameof(CameraComponent.NearPlaneDistance)));
            Assert.False(service.IsScopePropertyOverrideActive(commonComponent, resolved, saveComponent, handheldDs, nameof(CameraComponent.FarPlaneDistance)));

            Assert.True(service.RemoveComponent(commonComponent, saveComponent, handheld));
            Assert.True(service.IsComponentRemoved(commonComponent, saveComponent, handheldDs));
            Assert.False(service.IsComponentRemoved(commonComponent, saveComponent, new EditorOverrideScope("ps1")));
        }
    }
}

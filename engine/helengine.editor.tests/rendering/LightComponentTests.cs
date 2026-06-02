using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies authored light component defaults exposed to rendering systems.
    /// </summary>
    public class LightComponentTests {
        /// <summary>
        /// Ensures directional lights default to the authored shadow-capable directional-light profile.
        /// </summary>
        [Fact]
        public void DirectionalLightComponent_WhenCreated_UsesShadowCapableDirectionalDefaults() {
            InitializeCore();
            DirectionalLightComponent lightComponent = new DirectionalLightComponent();

            Assert.Equal(LightType.Directional, lightComponent.LightType);
            Assert.True(lightComponent.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Auto, lightComponent.ShadowMapMode);
            Assert.Equal(50f, lightComponent.ShadowDistance);
        }

        /// <summary>
        /// Ensures ambient lights default to the non-shadowed global-light profile expected by 3D materials.
        /// </summary>
        [Fact]
        public void AmbientLightComponent_WhenCreated_UsesAmbientLightDefaults() {
            InitializeCore();
            AmbientLightComponent lightComponent = new AmbientLightComponent();

            Assert.Equal(LightType.Ambient, lightComponent.LightType);
            Assert.False(lightComponent.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Auto, lightComponent.ShadowMapMode);
            Assert.Equal(1f, lightComponent.Intensity);
            Assert.Equal(1f, lightComponent.ShadowStrength);
            Assert.Equal(new float4(1f, 1f, 1f, 1f), lightComponent.Color);
        }

        /// <summary>
        /// Ensures directional and spot lights derive their world direction from the owning entity forward axis.
        /// </summary>
        [Fact]
        public void LightDirectionUtility_WhenResolvingDirectionalAndSpotLights_UsesOwningEntityForwardAxis() {
            InitializeCore();
            Entity directionalEntity = CreateInitializedEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            directionalEntity.AddComponent(directionalLight);

            Entity spotEntity = CreateInitializedEntity();
            float4 spotOrientation;
            float4.CreateFromYawPitchRoll((float)(Math.PI * 0.5), 0f, 0f, out spotOrientation);
            spotEntity.LocalOrientation = spotOrientation;
            SpotLightComponent spotLight = new SpotLightComponent();
            spotEntity.AddComponent(spotLight);

            float3 directionalDirection = LightDirectionUtility.GetLightDirection(directionalLight);
            float3 spotDirection = LightDirectionUtility.GetLightDirection(spotLight);
            float3 expectedSpotDirection = LightDirectionUtility.GetEntityForwardDirection(spotEntity);

            Assert.Equal(0f, directionalDirection.X);
            Assert.Equal(0f, directionalDirection.Y);
            Assert.Equal(-1f, directionalDirection.Z);
            Assert.Equal(expectedSpotDirection.X, spotDirection.X);
            Assert.Equal(expectedSpotDirection.Y, spotDirection.Y);
            Assert.Equal(expectedSpotDirection.Z, spotDirection.Z);
        }

        /// <summary>
        /// Ensures parented directional lights preserve their local quaternion when the parent contributes no rotation.
        /// </summary>
        [Fact]
        public void LightDirectionUtility_WhenDirectionalLightIsParentedUnderIdentityParent_PreservesChildOrientation() {
            InitializeCore();
            Entity parentEntity = CreateInitializedEntity();
            parentEntity.InitChildren();
            Entity lightEntity = CreateInitializedEntity();
            float4 lightOrientation;
            float4.CreateFromYawPitchRoll((float)(-48.0 * Math.PI / 180.0), (float)(-44.0 * Math.PI / 180.0), 0f, out lightOrientation);
            lightEntity.LocalOrientation = lightOrientation;
            parentEntity.AddChild(lightEntity);

            DirectionalLightComponent lightComponent = new DirectionalLightComponent();
            lightEntity.AddComponent(lightComponent);

            float3 expectedDirection = float4.RotateVector(LightDirectionUtility.AuthoredForwardAxis, lightOrientation);
            float3 actualDirection = LightDirectionUtility.GetLightDirection(lightComponent);

            Assert.True(Math.Abs(expectedDirection.X - actualDirection.X) < 0.0001f, $"Expected X {expectedDirection.X} but found {actualDirection.X}.");
            Assert.True(Math.Abs(expectedDirection.Y - actualDirection.Y) < 0.0001f, $"Expected Y {expectedDirection.Y} but found {actualDirection.Y}.");
            Assert.True(Math.Abs(expectedDirection.Z - actualDirection.Z) < 0.0001f, $"Expected Z {expectedDirection.Z} but found {actualDirection.Z}.");
        }

        /// <summary>
        /// Ensures ambient lights reject directional resolution because they have no world-space facing axis.
        /// </summary>
        [Fact]
        public void LightDirectionUtility_WhenResolvingAmbientLight_ThrowsBecauseAmbientLightsHaveNoDirection() {
            InitializeCore();
            Entity ambientEntity = CreateInitializedEntity();
            AmbientLightComponent ambientLight = new AmbientLightComponent();
            ambientEntity.AddComponent(ambientLight);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => {
                LightDirectionUtility.GetLightDirection(ambientLight);
            });

            Assert.Equal("Ambient lights do not define a world-space light direction.", exception.Message);
        }

        /// <summary>
        /// Ensures enabled light components register with the object manager under their concrete light lists.
        /// </summary>
        [Fact]
        public void LightComponents_WhenAddedToEnabledEntity_RegisterWithTypedObjectManagerLists() {
            InitializeCore();
            Entity entity = CreateInitializedEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            AmbientLightComponent ambientLight = new AmbientLightComponent();
            PointLightComponent pointLight = new PointLightComponent();
            SpotLightComponent spotLight = new SpotLightComponent();

            entity.AddComponent(directionalLight);
            entity.AddComponent(ambientLight);
            entity.AddComponent(pointLight);
            entity.AddComponent(spotLight);

            Assert.Same(directionalLight, Assert.Single(Core.Instance.ObjectManager.DirectionalLights));
            Assert.Same(ambientLight, Assert.Single(Core.Instance.ObjectManager.AmbientLights));
            Assert.Same(pointLight, Assert.Single(Core.Instance.ObjectManager.PointLights));
            Assert.Same(spotLight, Assert.Single(Core.Instance.ObjectManager.SpotLights));
        }

        /// <summary>
        /// Ensures disabling the owning entity removes registered lights from the object manager until the entity is enabled again.
        /// </summary>
        [Fact]
        public void LightComponents_WhenParentEntityIsDisabled_AreRemovedFromTypedObjectManagerLists() {
            InitializeCore();
            Entity entity = CreateInitializedEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            AmbientLightComponent ambientLight = new AmbientLightComponent();

            entity.AddComponent(directionalLight);
            entity.AddComponent(ambientLight);
            entity.Enabled = false;

            Assert.Empty(Core.Instance.ObjectManager.DirectionalLights);
            Assert.Empty(Core.Instance.ObjectManager.AmbientLights);

            entity.Enabled = true;

            Assert.Same(directionalLight, Assert.Single(Core.Instance.ObjectManager.DirectionalLights));
            Assert.Same(ambientLight, Assert.Single(Core.Instance.ObjectManager.AmbientLights));
        }

        /// <summary>
        /// Ensures removing one light component unregisters only that light from the object manager.
        /// </summary>
        [Fact]
        public void LightComponents_WhenRemoved_UnregisterOnlyTheRemovedTypedLight() {
            InitializeCore();
            Entity entity = CreateInitializedEntity();
            DirectionalLightComponent directionalLight = new DirectionalLightComponent();
            SpotLightComponent spotLight = new SpotLightComponent();

            entity.AddComponent(directionalLight);
            entity.AddComponent(spotLight);
            entity.RemoveComponent(directionalLight);

            Assert.Empty(Core.Instance.ObjectManager.DirectionalLights);
            Assert.Same(spotLight, Assert.Single(Core.Instance.ObjectManager.SpotLights));
        }

        /// <summary>
        /// Creates one entity with initialized component storage for light tests.
        /// </summary>
        /// <returns>Initialized entity ready to receive light components.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            return entity;
        }

        /// <summary>
        /// Initializes a minimal core instance so test entities can be created safely.
        /// </summary>
        void InitializeCore() {
            Core core = new Core(new CoreInitializationOptions {
                RenderList3DInitialCapacity = 4,
                RenderList2DInitialCapacity = 4
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}

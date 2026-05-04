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
            DirectionalLightComponent lightComponent = new DirectionalLightComponent();

            Assert.Equal(LightType.Directional, lightComponent.LightType);
            Assert.True(lightComponent.ShadowsEnabled);
            Assert.Equal(ShadowMapMode.Auto, lightComponent.ShadowMapMode);
        }

        /// <summary>
        /// Ensures directional and spot lights derive their world direction from the owning entity forward axis.
        /// </summary>
        [Fact]
        public void LightDirectionUtility_WhenResolvingDirectionalAndSpotLights_UsesOwningEntityForwardAxis() {
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
        /// Creates one entity with initialized component storage for light tests.
        /// </summary>
        /// <returns>Initialized entity ready to receive light components.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            return entity;
        }
    }
}

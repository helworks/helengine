namespace helengine {
    /// <summary>
    /// Shared base component for authored scene lights consumed by render extraction and backend planning.
    /// Directional and spot-light orientation is derived from the owning entity forward axis defined by <see cref="LightDirectionUtility"/>.
    /// </summary>
    public class LightComponent : Component {
        /// <summary>
        /// Initializes one authored light component for the supplied light family.
        /// </summary>
        /// <param name="lightType">Stable light family represented by the component.</param>
        protected LightComponent(LightType lightType) {
            LightType = lightType;
            Color = new float4(1f, 1f, 1f, 1f);
            Intensity = 1f;
            ShadowStrength = 1f;
            ShadowMapMode = ShadowMapMode.Auto;
        }

        /// <summary>
        /// Gets the stable light family represented by the component.
        /// </summary>
        public LightType LightType { get; }

        /// <summary>
        /// Gets or sets the light color stored as linear RGBA.
        /// </summary>
        public float4 Color { get; set; }

        /// <summary>
        /// Gets or sets the authored light intensity multiplier.
        /// </summary>
        public float Intensity { get; set; }

        /// <summary>
        /// Gets or sets whether the light currently requests shadow rendering.
        /// </summary>
        public bool ShadowsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the authored shadow-map participation mode for the light.
        /// </summary>
        public ShadowMapMode ShadowMapMode { get; set; }

        /// <summary>
        /// Gets or sets the authored shadow-strength multiplier applied by the backend.
        /// </summary>
        public float ShadowStrength { get; set; }

        /// <summary>
        /// Registers the light with the object manager when added to an enabled entity.
        /// </summary>
        /// <param name="entity">Entity receiving the light component.</param>
        public override void ComponentAdded(Entity entity) {
            base.ComponentAdded(entity);

            if (entity.IsHierarchyEnabled) {
                RegisterWithObjectManager();
            }
        }

        /// <summary>
        /// Registers or unregisters the light based on parent enabled-state changes.
        /// </summary>
        /// <param name="newEnabled">True when the parent hierarchy became enabled.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (newEnabled) {
                RegisterWithObjectManager();
            } else {
                RemoveFromObjectManager();
            }
        }

        /// <summary>
        /// Removes the light from the object manager when detached from its entity.
        /// </summary>
        /// <param name="entity">Entity losing the light component.</param>
        public override void ComponentRemoved(Entity entity) {
            RemoveFromObjectManager();
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Registers this light in the object manager list that matches its concrete type.
        /// </summary>
        void RegisterWithObjectManager() {
            if (Core.Instance == null || Core.Instance.ObjectManager == null) {
                throw new InvalidOperationException("Core object manager must exist before registering lights.");
            }

            if (this is DirectionalLightComponent directionalLight) {
                Core.Instance.ObjectManager.RegisterDirectionalLight(directionalLight);
            } else if (this is AmbientLightComponent ambientLight) {
                Core.Instance.ObjectManager.RegisterAmbientLight(ambientLight);
            } else if (this is PointLightComponent pointLight) {
                Core.Instance.ObjectManager.RegisterPointLight(pointLight);
            } else if (this is SpotLightComponent spotLight) {
                Core.Instance.ObjectManager.RegisterSpotLight(spotLight);
            } else {
                throw new InvalidOperationException("Unsupported light component type.");
            }
        }

        /// <summary>
        /// Removes this light from the object manager list that matches its concrete type.
        /// </summary>
        void RemoveFromObjectManager() {
            if (Core.Instance == null || Core.Instance.ObjectManager == null) {
                throw new InvalidOperationException("Core object manager must exist before unregistering lights.");
            }

            if (this is DirectionalLightComponent directionalLight) {
                Core.Instance.ObjectManager.RemoveDirectionalLight(directionalLight);
            } else if (this is AmbientLightComponent ambientLight) {
                Core.Instance.ObjectManager.RemoveAmbientLight(ambientLight);
            } else if (this is PointLightComponent pointLight) {
                Core.Instance.ObjectManager.RemovePointLight(pointLight);
            } else if (this is SpotLightComponent spotLight) {
                Core.Instance.ObjectManager.RemoveSpotLight(spotLight);
            } else {
                throw new InvalidOperationException("Unsupported light component type.");
            }
        }
    }
}

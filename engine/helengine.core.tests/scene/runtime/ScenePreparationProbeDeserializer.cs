using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Materializes real viewport, text and observable lifecycle components through the normal component registry.</summary>
    internal sealed class ScenePreparationProbeDeserializer : IRuntimeComponentDeserializer {
        /// <summary>Stores the registered component discriminator.</summary>
        readonly string Id;
        /// <summary>Gets the probes materialized by this deserializer.</summary>
        public List<ScenePreparationProbeComponent> Probes { get; } = new List<ScenePreparationProbeComponent>();
        /// <summary>Gets or sets whether newly materialized probes fail readiness.</summary>
        public bool FailPreparation { get; set; }
        /// <summary>Gets or sets whether each probe removes itself while initializing.</summary>
        public bool RemoveDuringInitialization { get; set; }
        /// <summary>Registers one fixture discriminator without replacing any production deserializer.</summary>
        public ScenePreparationProbeDeserializer(string id) { Id = id; }
        /// <summary>Gets the stable discriminator used by the real registry.</summary>
        public string ComponentTypeId => Id;
        /// <summary>Creates the requested component using real fixed-viewport scaling and text layout.</summary>
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver resolver,
            RuntimeSceneReferenceFixups fixups = null) {
            if (Id == "test.viewport") {
                return new ViewportComponent {
                    BindingMode = ViewportComponent.FixedBindingMode,
                    FixedSize = new int2(640, 480),
                    ReferenceWidth = 1280,
                    ReferenceHeight = 720,
                    ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode
                };
            }
            if (Id == "test.text") { return new TextComponent { Size = new int2(280, 28), FontScale = 2f }; }
            ScenePreparationProbeComponent probe = new ScenePreparationProbeComponent {
                FailPreparation = FailPreparation, RemoveDuringInitialization = RemoveDuringInitialization
            };
            Probes.Add(probe);
            return probe;
        }
    }
}

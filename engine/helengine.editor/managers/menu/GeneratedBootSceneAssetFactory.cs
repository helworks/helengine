namespace helengine.editor {
    /// <summary>
    /// Builds one minimal generated boot scene that contains only a scene-map helper entity.
    /// </summary>
    public sealed class GeneratedBootSceneAssetFactory {
        /// <summary>
        /// Descriptor used to serialize the scene-map helper component.
        /// </summary>
        readonly SceneMapComponentPersistenceDescriptor SceneMapDescriptor;

        /// <summary>
        /// Initializes the generated boot-scene factory.
        /// </summary>
        public GeneratedBootSceneAssetFactory() {
            SceneMapDescriptor = new SceneMapComponentPersistenceDescriptor();
        }

        /// <summary>
        /// Builds one generated boot scene asset with the supplied startup target and scene remapping table.
        /// </summary>
        /// <param name="scenePath">Project-relative generated scene path.</param>
        /// <param name="initialSceneId">Logical startup scene id that should be redirected once.</param>
        /// <param name="mappings">Scene remapping table authored into the helper component.</param>
        /// <returns>Generated boot scene asset.</returns>
        public SceneAsset BuildSceneAsset(string scenePath, string initialSceneId, IReadOnlyDictionary<string, string> mappings) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(scenePath));
            } else if (string.IsNullOrWhiteSpace(initialSceneId)) {
                throw new ArgumentException("Initial scene id must be provided.", nameof(initialSceneId));
            } else if (mappings == null) {
                throw new ArgumentNullException(nameof(mappings));
            }

            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = initialSceneId
            };
            foreach (KeyValuePair<string, string> mapping in mappings) {
                sceneMapComponent.Mappings.Add(mapping.Key, mapping.Value);
            }

            return new SceneAsset {
                Id = scenePath,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile(),
                    DontUnload = true
                },
                RootEntities = [
                    new SceneEntityAsset {
                        Id = 1,
                        Name = "GeneratedBootSceneRoot",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = [
                            SceneMapDescriptor.SerializeComponent(sceneMapComponent, 0, null)
                        ],
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                ]
            };
        }
    }
}

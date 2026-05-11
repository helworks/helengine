namespace helengine.editor.tests.testing {
    /// <summary>
    /// Requests one runtime scene transition during update and records whether the current entity stayed attached until the update method returned.
    /// </summary>
    public sealed class TestSceneLoadTriggerComponent : UpdateComponent {
        /// <summary>
        /// Gets or sets the stable scene identifier that should be loaded during the next update.
        /// </summary>
        public string TargetSceneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether the component already requested its scene transition.
        /// </summary>
        public bool HasRequestedLoad { get; private set; }

        /// <summary>
        /// Gets whether the component still had an attached parent entity immediately after it requested the scene transition.
        /// </summary>
        public bool WasStillAttachedAfterRequest { get; private set; }

        /// <summary>
        /// Requests the configured scene transition once and records whether the current entity stayed attached until the update method returned.
        /// </summary>
        public override void Update() {
            if (HasRequestedLoad) {
                return;
            }
            if (string.IsNullOrWhiteSpace(TargetSceneId)) {
                throw new InvalidOperationException("A target scene id must be configured before requesting a scene transition.");
            }
            if (Core.Instance == null || Core.Instance.SceneManager == null) {
                throw new InvalidOperationException("A runtime scene manager must exist before requesting a scene transition.");
            }

            Core.Instance.SceneManager.LoadScene(TargetSceneId, SceneLoadMode.Single);
            WasStillAttachedAfterRequest = Parent != null;
            HasRequestedLoad = true;
        }
    }
}

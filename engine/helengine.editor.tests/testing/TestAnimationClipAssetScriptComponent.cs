namespace helengine.editor.tests.testing {
    /// <summary>
    /// Exposes one animation-clip asset member through automatic script-component persistence tests.
    /// </summary>
    internal sealed class TestAnimationClipAssetScriptComponent : Component {
        /// <summary>
        /// Gets or sets the authored label used to verify regular reflected payload members still round-trip beside the clip reference.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authored animation clip resolved through one scene asset reference.
        /// </summary>
        public AnimationClipAsset IdleClip { get; set; }
    }
}

namespace helengine {
    /// <summary>
    /// Stores scene-level authoring settings that apply to the entire scene asset.
    /// </summary>
    public class SceneSettingsAsset {
        /// <summary>
        /// Gets or sets the authored canvas profile used to evaluate 2D layout and previews for the scene.
        /// </summary>
        public SceneCanvasProfile CanvasProfile { get; set; } = new SceneCanvasProfile();
    }
}

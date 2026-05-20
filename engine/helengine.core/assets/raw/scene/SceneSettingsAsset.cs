namespace helengine {
    /// <summary>
    /// Stores scene-level authoring settings that apply to the entire scene asset.
    /// </summary>
    public class SceneSettingsAsset {
        /// <summary>
        /// Backing field for the authored canvas profile owned by this settings asset.
        /// </summary>
        SceneCanvasProfile CanvasProfileValue = new SceneCanvasProfile();

        /// <summary>
        /// Gets or sets the authored canvas profile used to evaluate 2D layout and previews for the scene.
        /// </summary>
        public SceneCanvasProfile CanvasProfile {
            get { return CanvasProfileValue; }
            set {
                SceneCanvasProfile newValue = value ?? throw new ArgumentNullException(nameof(value));
                if (CanvasProfileValue != null && !ReferenceEquals(CanvasProfileValue, newValue)) {
                    NativeOwnership.Delete(CanvasProfileValue);
                }

                CanvasProfileValue = newValue;
            }
        }

        /// <summary>
        /// Gets or sets whether the scene remains loaded during normal single-scene transitions.
        /// </summary>
        public bool DontUnload { get; set; }

        /// <summary>
        /// Releases the owned canvas profile before this settings asset is deleted by native runtime code.
        /// </summary>
        public void ReleaseOwnedValuesForNativeDelete() {
            NativeOwnership.Delete(CanvasProfileValue);
            CanvasProfileValue = null;
        }
    }
}

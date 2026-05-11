namespace helengine {
    /// <summary>
    /// Derives stable scene ids from authored scene asset paths and file names.
    /// </summary>
    public static class SceneIdUtility {
        /// <summary>
        /// Derives one stable scene id from the supplied authored scene path by using the file name without its extension.
        /// </summary>
        /// <param name="scenePath">Absolute or relative authored scene path.</param>
        /// <returns>Stable scene id derived from the scene asset file name, or an empty string when the path is blank.</returns>
        public static string FromPath(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            string sceneFileName = Path.GetFileName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneFileName)) {
                return string.Empty;
            }

            const string sceneExtension = ".helen";
            if (!sceneFileName.EndsWith(sceneExtension, StringComparison.OrdinalIgnoreCase)) {
                return sceneFileName;
            }

            return sceneFileName.Substring(0, sceneFileName.Length - sceneExtension.Length);
        }
    }
}

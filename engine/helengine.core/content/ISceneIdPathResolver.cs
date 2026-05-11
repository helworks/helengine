namespace helengine {
    /// <summary>
    /// Resolves authored scene paths from stable scene ids for editor-side scene loading flows.
    /// </summary>
    public interface ISceneIdPathResolver {
        /// <summary>
        /// Resolves one authored scene path from the supplied stable scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to resolve.</param>
        /// <returns>Authored scene path relative to the active content root.</returns>
        string ResolveScenePath(string sceneId);
    }
}

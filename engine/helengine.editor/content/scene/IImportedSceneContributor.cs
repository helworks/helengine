namespace helengine.editor {
    /// <summary>
    /// Adds generated files and entity components to an imported scene. The core contributor always runs first and creates
    /// the blueprint entities; plugin contributors run afterwards in registration order and may extend them.
    /// </summary>
    public interface IImportedSceneContributor {
        /// <summary>
        /// Gets the stable identifier of this contributor, unique within one registry.
        /// </summary>
        string ContributorId { get; }

        /// <summary>
        /// Reads the neutral scene data and records generated files and entity changes on the output.
        /// </summary>
        /// <param name="scene">Neutral scene data read from the source file.</param>
        /// <param name="output">Collects the generated files and exposes the blueprint entities.</param>
        void Contribute(ImportedSceneData scene, ImportedSceneOutput output);
    }
}

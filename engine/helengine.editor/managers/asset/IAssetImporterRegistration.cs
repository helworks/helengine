namespace helengine.editor {
    /// <summary>
    /// Describes an asset importer registration that can be applied to a manager.
    /// </summary>
    public interface IAssetImporterRegistration {
        /// <summary>
        /// Registers the importer with an asset import manager.
        /// </summary>
        /// <param name="manager">Manager to register with.</param>
        void Register(AssetImportManager manager);
    }
}

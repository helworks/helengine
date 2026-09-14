namespace helengine.editor {
    /// <summary>
    /// Describes an asset importer registration that can be applied to a host registry.
    /// </summary>
    public interface IAssetImporterRegistration {
        /// <summary>
        /// Registers the importer with the host importer registry.
        /// </summary>
        /// <param name="registry">Registry to register with.</param>
        void Register(AssetImporterRegistry registry);
    }
}

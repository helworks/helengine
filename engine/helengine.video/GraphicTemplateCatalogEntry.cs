namespace helengine.video {
    /// <summary>
    /// One validated graphic template registered in a <see cref="GraphicTemplateCatalog"/>, with its origin.
    /// </summary>
    public sealed class GraphicTemplateCatalogEntry {
        /// <summary>
        /// Gets the validated template definition.
        /// </summary>
        public GraphicTemplateAsset Template { get; }

        /// <summary>
        /// Gets whether the template came from a user project rather than the engine.
        /// </summary>
        public bool IsProjectTemplate { get; }

        /// <summary>
        /// Creates one entry.
        /// </summary>
        /// <param name="template">Validated template definition.</param>
        /// <param name="isProjectTemplate">Whether the template came from a user project.</param>
        public GraphicTemplateCatalogEntry(GraphicTemplateAsset template, bool isProjectTemplate) {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            IsProjectTemplate = isProjectTemplate;
        }
    }
}

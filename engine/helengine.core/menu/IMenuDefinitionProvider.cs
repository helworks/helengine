namespace helengine {
    /// <summary>
    /// Produces one reusable menu definition that can be materialized by the runtime menu host.
    /// </summary>
    public interface IMenuDefinitionProvider {
        /// <summary>
        /// Builds the menu definition consumed by the runtime menu host.
        /// </summary>
        /// <returns>Menu definition describing panels, items, and theme assets.</returns>
        MenuDefinition CreateMenuDefinition();
    }
}

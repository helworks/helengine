namespace helengine {
    /// <summary>
    /// Creates authored scene entities for the active host.
    /// </summary>
    public interface IEntityFactory {
        /// <summary>
        /// Creates one authored root entity.
        /// </summary>
        /// <param name="name">Display name requested for the created entity.</param>
        /// <returns>Created authored entity.</returns>
        Entity Create(string name);

        /// <summary>
        /// Creates one authored child entity and attaches it to the supplied parent.
        /// </summary>
        /// <param name="parent">Parent that will own the created child.</param>
        /// <param name="name">Display name requested for the created child.</param>
        /// <returns>Created child entity.</returns>
        Entity CreateChild(Entity parent, string name);
    }
}

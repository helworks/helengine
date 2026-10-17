namespace helengine {
    /// <summary>
    /// Lets an authoring host decide which component lifecycle callbacks may execute without coupling the portable core to metadata inspection.
    /// </summary>
    public interface IComponentExecutionPolicy {
        /// <summary>
        /// Determines whether a component may execute lifecycle callbacks while its owning entity is being authored.
        /// </summary>
        /// <param name="component">Component whose lifecycle is being evaluated.</param>
        /// <param name="entity">Entity that owns the component.</param>
        /// <returns>True when the authoring host permits execution; otherwise false.</returns>
        bool ShouldRunComponentLifecycle(Component component, Entity entity);
    }
}

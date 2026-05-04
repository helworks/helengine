namespace helengine {
    /// <summary>
    /// Describes the behavior executed by one menu item when the player confirms it.
    /// </summary>
    public class MenuActionDefinition {
        /// <summary>
        /// Initializes a new menu action definition.
        /// </summary>
        /// <param name="kind">Action kind performed by the menu item.</param>
        /// <param name="targetId">Panel id or scene path targeted by the action.</param>
        public MenuActionDefinition(MenuActionKind kind, string targetId) {
            Kind = kind;
            TargetId = targetId ?? string.Empty;
        }

        /// <summary>
        /// Gets the action kind performed by the menu item.
        /// </summary>
        public MenuActionKind Kind { get; }

        /// <summary>
        /// Gets the panel id or scene path targeted by the action.
        /// </summary>
        public string TargetId { get; }
    }
}

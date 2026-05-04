namespace helengine {
    /// <summary>
    /// Identifies the behavior performed when a menu item is activated.
    /// </summary>
    public enum MenuActionKind {
        /// <summary>
        /// Performs no work when the menu item is activated.
        /// </summary>
        None = 0,

        /// <summary>
        /// Switches the active menu host to another panel in the same definition.
        /// </summary>
        OpenPanel = 1,

        /// <summary>
        /// Loads a packaged scene asset and hides the active menu host.
        /// </summary>
        LoadScene = 2,

        /// <summary>
        /// Returns to the previous panel recorded by the menu host history stack.
        /// </summary>
        Back = 3
    }
}

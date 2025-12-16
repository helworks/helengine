namespace helengine {
    /// <summary>
    /// Represents an object that participates in the update loop.
    /// </summary>
    public interface IUpdateable {
        /// <summary>
        /// Gets or sets the update order bucket used to sequence updates.
        /// </summary>
        byte UpdateOrder { get; set; }

        /// <summary>
        /// Executes a frame update.
        /// </summary>
        void Update();
    }
}

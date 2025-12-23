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
        /// Gets or sets the update bucket index assigned by the object manager.
        /// </summary>
        int UpdateBucket { get; set; }

        /// <summary>
        /// Gets or sets the position within the assigned update bucket.
        /// </summary>
        int UpdateBucketIndex { get; set; }

        /// <summary>
        /// Executes a frame update.
        /// </summary>
        void Update();
    }
}

namespace helengine {
    /// <summary>
    /// Describes core initialization settings for ordering helpers, frame timing, and object-list capacities.
    /// </summary>
    public class CoreInitializationOptions {
        /// <summary>
        /// Gets or sets the root directory used by the core-owned content manager.
        /// </summary>
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <summary>
        /// Gets or sets the number of update order layers available for convenience helpers.
        /// </summary>
        public byte UpdateOrderLayers { get; set; } = 4;

        /// <summary>
        /// Gets or sets the number of 3D render order layers available for convenience helpers.
        /// </summary>
        public byte RenderOrderLayers3D { get; set; } = 4;

        /// <summary>
        /// Gets or sets the initial capacity for the update list.
        /// </summary>
        public int UpdateListInitialCapacity { get; set; } = 64;

        /// <summary>
        /// Gets or sets the initial capacity for 2D render lists.
        /// </summary>
        public int RenderList2DInitialCapacity { get; set; } = 64;

        /// <summary>
        /// Gets or sets the initial capacity for 3D render lists.
        /// </summary>
        public int RenderList3DInitialCapacity { get; set; } = 64;

        /// <summary>
        /// Gets or sets the default elapsed update time used when hosts call the parameterless core update path.
        /// </summary>
        public double DefaultUpdateDeltaSeconds { get; set; } = 1.0d / 60.0d;

        /// <summary>
        /// Gets or sets the fixed simulation step used by attached physics runtimes.
        /// </summary>
        public double PhysicsFixedStepSeconds { get; set; } = 1.0d / 60.0d;

        /// <summary>
        /// Validates option values for initialization.
        /// </summary>
        public void Normalize() {
            if (string.IsNullOrWhiteSpace(ContentRootPath)) {
                throw new InvalidOperationException("ContentRootPath must be provided.");
            }

            if (UpdateOrderLayers < 1) {
                throw new InvalidOperationException("UpdateOrderLayers must be at least 1.");
            }

            if (RenderOrderLayers3D < 1) {
                throw new InvalidOperationException("RenderOrderLayers3D must be at least 1.");
            }

            if (UpdateListInitialCapacity < 0) {
                throw new InvalidOperationException("UpdateListInitialCapacity cannot be negative.");
            }

            if (RenderList2DInitialCapacity < 0) {
                throw new InvalidOperationException("RenderList2DInitialCapacity cannot be negative.");
            }

            if (RenderList3DInitialCapacity < 0) {
                throw new InvalidOperationException("RenderList3DInitialCapacity cannot be negative.");
            }

            if (double.IsNaN(DefaultUpdateDeltaSeconds) ||
                double.IsInfinity(DefaultUpdateDeltaSeconds) ||
                DefaultUpdateDeltaSeconds <= 0d) {
                throw new InvalidOperationException("DefaultUpdateDeltaSeconds must be a finite value greater than zero.");
            }

            if (double.IsNaN(PhysicsFixedStepSeconds) ||
                double.IsInfinity(PhysicsFixedStepSeconds) ||
                PhysicsFixedStepSeconds <= 0d) {
                throw new InvalidOperationException("PhysicsFixedStepSeconds must be a finite value greater than zero.");
            }
        }
    }
}

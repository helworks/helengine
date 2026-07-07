namespace helengine {
    /// <summary>
    /// Describes core initialization settings for ordering helpers, frame timing, and object-list capacities.
    /// </summary>
    public class CoreInitializationOptions {
        /// <summary>
        /// Gets or sets the content stream source used by the core-owned content manager.
        /// This replaces the legacy content-root-path initialization seam with one explicit runtime asset source.
        /// </summary>
        public IContentStreamSource ContentStreamSource { get; set; } = new HostFileSystemContentStreamSource(AppContext.BaseDirectory);

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
        /// Gets or sets the default elapsed update time available to hosts that choose to drive <see cref="Core.Update(double)"/> explicitly.
        /// </summary>
        public double DefaultUpdateDeltaSeconds { get; set; } = 1.0d / 60.0d;

        /// <summary>
        /// Gets or sets the fixed simulation step used by attached physics runtimes.
        /// </summary>
        public double PhysicsFixedStepSeconds { get; set; } = 1.0d / 60.0d;

        /// <summary>
        /// Gets or sets the maximum number of fixed physics steps that one core update may consume before deferring the remaining simulation debt to later updates.
        /// </summary>
        public int PhysicsMaxStepsPerUpdate { get; set; } = 8;

        /// <summary>
        /// Gets or sets the runtime scene catalog that packaged hosts can inject before core initialization.
        /// </summary>
        public RuntimeSceneCatalog SceneCatalog { get; set; }

        /// <summary>
        /// Gets or sets the optional editor-side resolver used to map stable scene ids back to authored scene paths.
        /// </summary>
        public ISceneIdPathResolver ScenePathResolver { get; set; }

        /// <summary>
        /// Gets or sets the optional runtime diagnostics provider supplied by the active host.
        /// </summary>
        public IRuntimeDiagnosticsProvider RuntimeDiagnosticsProvider { get; set; }

        /// <summary>
        /// Gets or sets the configured engine-owned platform-standard input actions that should be registered during startup.
        /// </summary>
        public StandardPlatformInputConfiguration StandardPlatformInputConfiguration { get; set; } = StandardPlatformInputConfiguration.Empty;

        /// <summary>
        /// Gets or sets whether <see cref="Core.Draw"/> should commit queued scene operations automatically at the end of the draw call.
        /// Hosts that need a later safe point, such as a post-present boundary, should disable this and call <see cref="Core.CompleteFrameBoundary"/> explicitly.
        /// </summary>
        public bool CommitPendingSceneOperationsDuringDraw { get; set; } = true;

        /// <summary>
        /// Validates option values for initialization.
        /// </summary>
        public void Normalize() {
            if (ContentStreamSource == null) {
                throw new InvalidOperationException("ContentStreamSource must be provided.");
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

            if (PhysicsMaxStepsPerUpdate < 1) {
                throw new InvalidOperationException("PhysicsMaxStepsPerUpdate must be at least 1.");
            }

            if (StandardPlatformInputConfiguration == null) {
                throw new InvalidOperationException("StandardPlatformInputConfiguration must be provided.");
            }
        }
    }
}

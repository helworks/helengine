namespace helengine {
    /// <summary>
    /// Stores one emitted scene-memory probe checkpoint.
    /// </summary>
    public sealed class SceneMemoryProbeMeasurement {
        /// <summary>
        /// Gets or sets the authored probe name that emitted the checkpoint.
        /// </summary>
        public string ProbeName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the zero-based loop cycle index active when the checkpoint was emitted.
        /// </summary>
        public int CycleIndex { get; set; }

        /// <summary>
        /// Gets or sets the zero-based step index that produced the checkpoint.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Gets or sets the authored label associated with the current step.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authored action kind associated with the current step.
        /// </summary>
        public SceneMemoryProbeActionKind ActionKind { get; set; }

        /// <summary>
        /// Gets or sets the current resident memory size in bytes.
        /// </summary>
        public ulong ResidentBytes { get; set; }

        /// <summary>
        /// Gets or sets the current committed memory size in bytes.
        /// </summary>
        public ulong CommittedBytes { get; set; }

        /// <summary>
        /// Gets or sets the comma-separated list of loaded scene ids captured at the checkpoint.
        /// </summary>
        public string LoadedSceneIds { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the live 2D drawable count captured at the checkpoint.
        /// </summary>
        public int Drawables2DCount { get; set; }

        /// <summary>
        /// Gets or sets the live 3D drawable count captured at the checkpoint.
        /// </summary>
        public int Drawables3DCount { get; set; }

        /// <summary>
        /// Gets or sets the most recent render-manager draw-call count captured at the checkpoint.
        /// </summary>
        public int DrawCallCount { get; set; }

        /// <summary>
        /// Gets or sets the count of active owned textures tracked by the scene manager.
        /// </summary>
        public int ActiveOwnedTextureCount { get; set; }

        /// <summary>
        /// Gets or sets the count of active owned fonts tracked by the scene manager.
        /// </summary>
        public int ActiveOwnedFontCount { get; set; }

        /// <summary>
        /// Gets or sets the count of active owned models tracked by the scene manager.
        /// </summary>
        public int ActiveOwnedModelCount { get; set; }

        /// <summary>
        /// Gets or sets the count of active owned materials tracked by the scene manager.
        /// </summary>
        public int ActiveOwnedMaterialCount { get; set; }
    }
}

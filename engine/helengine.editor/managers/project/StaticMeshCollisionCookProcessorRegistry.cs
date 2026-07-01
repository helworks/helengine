namespace helengine.editor {
    /// <summary>
    /// Stores the static mesh collision cook processors available to the active editor packaging flow.
    /// </summary>
    public sealed class StaticMeshCollisionCookProcessorRegistry {
        /// <summary>
        /// Backing field for the shared registry instance.
        /// </summary>
        static StaticMeshCollisionCookProcessorRegistry SharedValue = new StaticMeshCollisionCookProcessorRegistry();

        /// <summary>
        /// Backing collection for the registered processors.
        /// </summary>
        readonly List<IStaticMeshCollisionCookProcessor3D> ProcessorsValue;

        /// <summary>
        /// Initializes one empty static mesh collision cook processor registry.
        /// </summary>
        public StaticMeshCollisionCookProcessorRegistry() {
            ProcessorsValue = new List<IStaticMeshCollisionCookProcessor3D>();
        }

        /// <summary>
        /// Gets the shared registry used by editor packaging when no explicit registry is supplied.
        /// </summary>
        public static StaticMeshCollisionCookProcessorRegistry Shared {
            get { return SharedValue; }
        }

        /// <summary>
        /// Gets the registered static mesh collision cook processors.
        /// </summary>
        public IReadOnlyList<IStaticMeshCollisionCookProcessor3D> Processors {
            get { return ProcessorsValue; }
        }

        /// <summary>
        /// Registers one static mesh collision cook processor for later packaging use.
        /// </summary>
        /// <param name="processor">Processor to register.</param>
        public void RegisterProcessor(IStaticMeshCollisionCookProcessor3D processor) {
            if (processor == null) {
                throw new ArgumentNullException(nameof(processor));
            }

            ProcessorsValue.Add(processor);
        }
    }
}

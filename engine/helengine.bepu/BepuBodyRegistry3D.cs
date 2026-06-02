namespace helengine {
    /// <summary>
    /// Tracks runtime body handles for the currently bound scene.
    /// </summary>
    public sealed class BepuBodyRegistry3D {
        readonly List<BepuBodyHandle3D> HandlesValue = new List<BepuBodyHandle3D>();

        /// <summary>
        /// Gets the registered runtime body handles.
        /// </summary>
        public IReadOnlyList<BepuBodyHandle3D> Handles => HandlesValue;

        /// <summary>
        /// Clears the registry.
        /// </summary>
        public void Clear() {
            HandlesValue.Clear();
        }

        /// <summary>
        /// Adds one runtime body handle.
        /// </summary>
        /// <param name="handle">Handle to add.</param>
        public void Add(BepuBodyHandle3D handle) {
            if (handle == null) {
                throw new ArgumentNullException(nameof(handle));
            }

            HandlesValue.Add(handle);
        }
    }
}

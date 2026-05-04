namespace helengine.directx11 {
    /// <summary>
    /// Copies one ordered 3D render queue into a stable snapshot for shared frame extraction.
    /// </summary>
    public sealed class DirectX11RenderQueueSnapshotVisitor : IRenderVisitor3D {
        /// <summary>
        /// Backing list used to accumulate one queue snapshot.
        /// </summary>
        readonly List<IDrawable3D> DrawablesValue;

        /// <summary>
        /// Initializes one empty render-queue snapshot visitor.
        /// </summary>
        public DirectX11RenderQueueSnapshotVisitor() {
            DrawablesValue = new List<IDrawable3D>();
        }

        /// <summary>
        /// Clears the current snapshot and prepares space for the next traversal.
        /// </summary>
        /// <param name="desiredCapacity">Expected number of drawables for the next traversal.</param>
        public void Reset(int desiredCapacity) {
            DrawablesValue.Clear();
            if (desiredCapacity > DrawablesValue.Capacity) {
                DrawablesValue.Capacity = desiredCapacity;
            }
        }

        /// <summary>
        /// Adds one drawable encountered during ordered queue traversal.
        /// </summary>
        /// <param name="drawable">Drawable encountered during traversal.</param>
        public void Visit(IDrawable3D drawable) {
            if (drawable == null) {
                return;
            }

            DrawablesValue.Add(drawable);
        }

        /// <summary>
        /// Creates one array snapshot from the visited drawables.
        /// </summary>
        /// <returns>Ordered drawable snapshot.</returns>
        public IDrawable3D[] CreateSnapshot() {
            return DrawablesValue.ToArray();
        }
    }
}

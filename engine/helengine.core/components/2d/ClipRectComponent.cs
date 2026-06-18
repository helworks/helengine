namespace helengine {
    /// <summary>
    /// Declares one rectangular clip region on an entity so descendant 2D drawables can be constrained during command building.
    /// </summary>
    public sealed class ClipRectComponent : Component, IClipRegion2D, IAnchorSizeProvider {
        int2 SizeValue;

        /// <summary>
        /// Gets or sets the clip rectangle size in pixels.
        /// </summary>
        public int2 Size {
            get { return SizeValue; }
            set {
                if (value.X < 0 || value.Y < 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Clip rectangle size must not be negative.");
                }

                SizeValue = value;
            }
        }

        /// <summary>
        /// Gets the clip rectangle size so child layout components can fill the clipped region.
        /// </summary>
        public int2 AnchorSize => SizeValue;

        /// <summary>
        /// Gets the resolved clip rectangle from the parent position and configured size.
        /// </summary>
        /// <returns>Resolved clip rectangle.</returns>
        public float4 GetClipRect() {
            if (Parent == null) {
                throw new InvalidOperationException("Clip rectangles require an attached parent entity.");
            }

            return new float4(Parent.Position.X, Parent.Position.Y, SizeValue.X, SizeValue.Y);
        }
    }
}

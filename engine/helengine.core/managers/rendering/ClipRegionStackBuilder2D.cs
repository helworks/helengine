namespace helengine {
    /// <summary>
    /// Resolves active rectangular clip-region chains for ordered 2D drawables and computes nested clip intersections.
    /// </summary>
    public sealed class ClipRegionStackBuilder2D {
        /// <summary>
        /// Resolves the ordered active clip chain from the drawable parent up through its ancestors.
        /// </summary>
        /// <param name="drawable">Drawable whose clip chain is being resolved.</param>
        /// <param name="clipChain">List reused to receive the active chain.</param>
        public void BuildClipChain(IDrawable2D drawable, List<IClipRegion2D> clipChain) {
            if (clipChain == null) {
                throw new ArgumentNullException(nameof(clipChain));
            }

            clipChain.Clear();
            Entity current = drawable != null ? drawable.Parent : null;
            while (current != null) {
                if (current.Components != null) {
                    for (int componentIndex = 0; componentIndex < current.Components.Count; componentIndex++) {
                        if (current.Components[componentIndex] is IClipRegion2D clipRegion) {
                            clipChain.Insert(0, clipRegion);
                        }
                    }
                }

                current = current.Parent;
            }
        }

        /// <summary>
        /// Intersects two rectangular clip regions and returns the overlapping rectangle in screen space.
        /// </summary>
        /// <param name="current">Current active clip rectangle.</param>
        /// <param name="next">Next clip rectangle to apply.</param>
        /// <returns>Intersected clip rectangle.</returns>
        public float4 Intersect(float4 current, float4 next) {
            float left = Math.Max(current.X, next.X);
            float top = Math.Max(current.Y, next.Y);
            float right = Math.Min(current.X + current.Z, next.X + next.Z);
            float bottom = Math.Min(current.Y + current.W, next.Y + next.W);
            float width = Math.Max(0f, right - left);
            float height = Math.Max(0f, bottom - top);
            return new float4(left, top, width, height);
        }
    }
}

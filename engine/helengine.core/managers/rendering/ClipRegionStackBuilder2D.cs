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
            RealEvaluation left = RealMath.Max(current.X, next.X);
            RealEvaluation top = RealMath.Max(current.Y, next.Y);
            RealEvaluation right = RealMath.Min(current.X + current.Z, next.X + next.Z);
            RealEvaluation bottom = RealMath.Min(current.Y + current.W, next.Y + next.W);
            RealEvaluation width = RealMath.Max(0f, right - left);
            RealEvaluation height = RealMath.Max(0f, bottom - top);
            return new float4(left, top, width, height);
        }
    }
}

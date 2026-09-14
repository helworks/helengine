namespace helengine {
    /// <summary>
    /// Tracks the nested clip regions active while a 2D render queue is traversed and turns them into scissor rectangles.
    /// The stack keeps the chain that is currently applied and the chain the next drawable needs, pops only the parts they do not share, and pushes the rest as intersected rectangles, so a backend re-applies the scissor once per drawable instead of rebuilding the whole chain. Backends supply only the two calls that touch their device.
    /// </summary>
    public abstract class ClipScissorStack2D {
        /// <summary>
        /// Resolves clip chains from drawable ancestry and intersects nested clip rectangles.
        /// </summary>
        readonly ClipRegionStackBuilder2D ClipRegionStackBuilder;

        /// <summary>
        /// Clip regions whose rectangles are currently reflected in the applied scissor state.
        /// </summary>
        readonly List<IClipRegion2D> ActiveClipChain;

        /// <summary>
        /// Clip chain required by the drawable about to be rendered.
        /// </summary>
        readonly List<IClipRegion2D> NextClipChain;

        /// <summary>
        /// Intersected clip rectangle for each entry of the active clip chain.
        /// </summary>
        readonly List<float4> ActiveClipRects;

        /// <summary>
        /// Initializes an empty clip stack.
        /// </summary>
        protected ClipScissorStack2D() {
            ClipRegionStackBuilder = new ClipRegionStackBuilder2D();
            ActiveClipChain = new List<IClipRegion2D>();
            NextClipChain = new List<IClipRegion2D>();
            ActiveClipRects = new List<float4>();
        }

        /// <summary>
        /// Drops every tracked clip region so the next drawable starts from the camera scissor. Call this whenever a camera or frame boundary invalidates the applied scissor state.
        /// </summary>
        public void Clear() {
            ActiveClipChain.Clear();
            NextClipChain.Clear();
            ActiveClipRects.Clear();
        }

        /// <summary>
        /// Resolves the clip chain of one drawable, synchronizes the applied chain with it and applies the resulting scissor rectangle.
        /// </summary>
        /// <param name="drawable">Drawable about to be rendered.</param>
        public void SyncForDrawable(IDrawable2D drawable) {
            ClipRegionStackBuilder.BuildClipChain(drawable, NextClipChain);
            SyncClipTransitions();
        }

        /// <summary>
        /// Intersects two rectangles with the shared clip-region rules, so backends can clip a resolved rectangle against their viewport.
        /// </summary>
        /// <param name="current">Current rectangle.</param>
        /// <param name="next">Rectangle to intersect with.</param>
        /// <returns>Intersected rectangle.</returns>
        protected float4 Intersect(float4 current, float4 next) {
            return ClipRegionStackBuilder.Intersect(current, next);
        }

        /// <summary>
        /// Applies one resolved clip rectangle, expressed in logical screen coordinates, to the backend scissor state.
        /// </summary>
        /// <param name="clipRect">Logical clip rectangle resolved for the current drawable.</param>
        protected abstract void ApplyClipScissor(float4 clipRect);

        /// <summary>
        /// Restores the camera viewport scissor after the clip stack empties.
        /// </summary>
        protected abstract void ApplyCameraScissor();

        /// <summary>
        /// Synchronizes the active clip stack with the pending clip chain and applies the resulting scissor rectangle.
        /// </summary>
        void SyncClipTransitions() {
            int sharedPrefixLength = GetSharedPrefixLength();

            while (ActiveClipChain.Count > sharedPrefixLength) {
                ActiveClipChain.RemoveAt(ActiveClipChain.Count - 1);
                ActiveClipRects.RemoveAt(ActiveClipRects.Count - 1);
            }

            while (ActiveClipChain.Count < NextClipChain.Count) {
                IClipRegion2D clipRegion = NextClipChain[ActiveClipChain.Count];
                float4 resolvedRect = ResolveClipRectForPush(clipRegion);
                ActiveClipChain.Add(clipRegion);
                ActiveClipRects.Add(resolvedRect);
            }

            if (ActiveClipRects.Count > 0) {
                ApplyClipScissor(ActiveClipRects[ActiveClipRects.Count - 1]);
            } else {
                ApplyCameraScissor();
            }
        }

        /// <summary>
        /// Returns the number of leading clip owners shared between the current and next clip chains.
        /// </summary>
        /// <returns>Shared clip-chain prefix length.</returns>
        int GetSharedPrefixLength() {
            int sharedPrefixLength = 0;
            int maxSharedLength = Math.Min(ActiveClipChain.Count, NextClipChain.Count);
            while (sharedPrefixLength < maxSharedLength &&
                   ReferenceEquals(ActiveClipChain[sharedPrefixLength], NextClipChain[sharedPrefixLength])) {
                sharedPrefixLength++;
            }

            return sharedPrefixLength;
        }

        /// <summary>
        /// Resolves one clip region against the current active clip stack.
        /// </summary>
        /// <param name="clipRegion">Clip region to resolve.</param>
        /// <returns>Effective clip rectangle in logical screen coordinates.</returns>
        float4 ResolveClipRectForPush(IClipRegion2D clipRegion) {
            float4 resolvedRect = clipRegion.GetClipRect();
            if (ActiveClipRects.Count <= 0) {
                return resolvedRect;
            }

            float4 currentRect = ActiveClipRects[ActiveClipRects.Count - 1];
            return ClipRegionStackBuilder.Intersect(currentRect, resolvedRect);
        }
    }
}

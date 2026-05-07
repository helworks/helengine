namespace helengine {
    /// <summary>
    /// Describes one entity-owned rectangular clip region that can constrain descendant 2D rendering.
    /// </summary>
    public interface IClipRegion2D {
        /// <summary>
        /// Gets the entity that owns this clip region.
        /// </summary>
        Entity Parent { get; }

        /// <summary>
        /// Gets the resolved clip rectangle in the same screen-space used by 2D drawables.
        /// </summary>
        /// <returns>Resolved clip rectangle.</returns>
        float4 GetClipRect();
    }
}

namespace helengine {
    /// <summary>
    /// Describes a 3D drawable object.
    /// </summary>
    public interface IDrawable3D {
        /// <summary>
        /// Gets the parent entity that owns the drawable.
        /// </summary>
        Entity Parent { get; }

        /// <summary>
        /// Gets or sets the render order bucket for 3D drawing.
        /// </summary>
        byte RenderOrder3D { get; set; }

        /// <summary>
        /// Gets or sets a variant index used to choose a render pipeline.
        /// </summary>
        byte Variant { get; set; }

        /// <summary>
        /// Gets the runtime model associated with this drawable.
        /// </summary>
        RuntimeModel Model { get; }
    }
}

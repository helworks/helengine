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
        /// Gets or sets the render order for 3D drawing.
        /// </summary>
        byte RenderOrder3D { get; set; }

        /// <summary>
        /// Gets the runtime model associated with this drawable.
        /// </summary>
        RuntimeModel Model { get; }

        /// <summary>
        /// Gets or sets the runtime material used to render this drawable.
        /// </summary>
        RuntimeMaterial Material { get; set; }
    }
}

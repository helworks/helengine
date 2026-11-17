namespace helengine.painting {
    /// <summary>Portable paint composition; the backend maps it to supported GPU blend factors.</summary>
    public enum PaintBlendMode {
        /// <summary>Source premultiplied color over retained destination.</summary>
        Paint,
        /// <summary>Destination color and alpha multiplied by one minus source coverage.</summary>
        Erase
    }
}

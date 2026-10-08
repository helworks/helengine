namespace helengine {
    /// <summary>
    /// Declares one visual piece of a graphic template, such as the item text, the separator between items or a strike
    /// bar: which items it is instantiated for, how it is colored and sized, and how it animates.
    /// </summary>
    public class GraphicTemplateElementAsset : IDisposable {
        /// <summary>
        /// Gets or sets the element name, unique within the template and used in compiled layer ids.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets which items or gaps the element is instantiated for.
        /// </summary>
        public GraphicElementRepeat Repeat { get; set; } = GraphicElementRepeat.EachItem;

        /// <summary>
        /// Gets or sets what the element draws.
        /// </summary>
        public GraphicElementContent Content { get; set; } = GraphicElementContent.ItemText;

        /// <summary>
        /// Gets or sets where the fill color comes from.
        /// </summary>
        public GraphicElementColor Color { get; set; } = GraphicElementColor.Text;

        /// <summary>
        /// Gets or sets the color parameter used by <see cref="GraphicElementColor.Parameter"/> and
        /// <see cref="GraphicElementColor.Accent"/>.
        /// </summary>
        public string ColorParameter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the font size relative to the item font size; separators are usually a little smaller.
        /// </summary>
        public float FontScale { get; set; } = 1f;

        /// <summary>
        /// Gets or sets a static horizontal offset from the laid-out position, in ems of the element font size.
        /// </summary>
        public float OffsetX { get; set; }

        /// <summary>
        /// Gets or sets a static vertical offset from the laid-out position, in ems of the element font size; a strike bar
        /// uses it to sit on the capital letters instead of the center of the line box.
        /// </summary>
        public float OffsetY { get; set; }

        /// <summary>
        /// Gets or sets the thickness of a <see cref="GraphicElementContent.Bar"/> in ems of the element font size.
        /// </summary>
        public float BarThickness { get; set; } = 0.08f;

        /// <summary>
        /// Gets or sets the draw order inside the graphic; higher values draw on top.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the name of a boolean parameter that must be true for the element to appear; empty always appears.
        /// </summary>
        public string EnabledParameter { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the animation tracks; tracks of one property must not overlap in time.
        /// </summary>
        public GraphicTemplateTrackAsset[] Tracks { get; set; } = Array.Empty<GraphicTemplateTrackAsset>();

        /// <summary>
        /// Releases the owned animation tracks.
        /// </summary>
        public void Dispose() {
            GraphicTemplateTrackAsset[] tracks = Tracks;
            Tracks = null;
            AnimationClipAsset.DisposeOwnedTracks(tracks);
        }
    }
}

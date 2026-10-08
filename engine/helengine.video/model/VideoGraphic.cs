using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Kinetic typography for an overlay: a catalog graphic template, the items it animates, the moment each item appears
    /// (usually the spoken word) and the template's typed parameters. The overlay text stays as the plain fallback.
    /// </summary>
    public sealed class VideoGraphic {
        /// <summary>
        /// Catalog graphic template id, e.g. <c>contrast_chain</c>.
        /// </summary>
        public string Template { get; set; } = "";

        /// <summary>
        /// Pinned catalog template version.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Item texts in order; one animated block each.
        /// </summary>
        public List<string> Items { get; set; } = [];

        /// <summary>
        /// When each item appears, one moment per item in the same order.
        /// </summary>
        public List<VideoMoment> At { get; set; } = [];

        /// <summary>
        /// Separator text drawn between items; the template default when absent.
        /// </summary>
        public string Separator { get; set; }

        /// <summary>
        /// Zero-based index of the item that receives the accent, for templates with an accent item slot.
        /// </summary>
        public int? AccentItem { get; set; }

        /// <summary>
        /// auto (choose by aspect and measured width), vertical or horizontal.
        /// </summary>
        public string Layout { get; set; } = "auto";

        /// <summary>
        /// Template parameters; omitted parameters use the template defaults.
        /// </summary>
        public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);

    }
}

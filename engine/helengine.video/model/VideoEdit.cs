using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// The scene-based video edit document (<c>helengine.video.edit.v1</c>): media, an ordered sequence of scenes and global tracks. It is self-contained and compiles to a helengine.media composition.
    /// </summary>
    public sealed class VideoEdit {
        /// <summary>
        /// Document schema id; always <c>helengine.video.edit.v1</c>.
        /// </summary>
        public string Schema { get; set; } = VideoEditJson.SchemaId;

        /// <summary>
        /// Stable document id, usually the product video id.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Revision number maintained by the product for optimistic concurrency.
        /// </summary>
        public long Revision { get; set; } = 0;

        /// <summary>
        /// Output frame size, frame rate and background color.
        /// </summary>
        public VideoFormat Format { get; set; } = new();

        /// <summary>
        /// Optional provenance: the script this edit was planned from.
        /// </summary>
        public VideoScriptReference ScriptRef { get; set; }

        /// <summary>
        /// Optional pin of the engine capability catalog the effects were chosen from.
        /// </summary>
        public VideoCatalogReference Catalog { get; set; }

        /// <summary>
        /// Optional provenance: id of the project profile the styles were copied from.
        /// </summary>
        public string ProjectProfile { get; set; }

        /// <summary>
        /// Named text style snapshots (font, size, colors, placement) used by captions, overlays and text layers.
        /// </summary>
        public Dictionary<string, JsonElement> TextStyles { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Every file the edit uses.
        /// </summary>
        public List<VideoMedia> Media { get; set; } = [];

        /// <summary>
        /// Scenes in playback order.
        /// </summary>
        public List<VideoScene> Scenes { get; set; } = [];

        /// <summary>
        /// Global tracks that cross scene boundaries.
        /// </summary>
        public VideoTracks Tracks { get; set; } = new();

    }
}

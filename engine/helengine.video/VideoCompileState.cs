using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Everything the compilation steps share while turning one edit into one composition.
    /// </summary>
    public sealed class VideoCompileState {
        /// <summary>
        /// Gets the edit being compiled.
        /// </summary>
        public VideoEdit Edit { get; }

        /// <summary>
        /// Gets the compilation context.
        /// </summary>
        public VideoCompileContext Context { get; }

        /// <summary>
        /// Gets the composition being built.
        /// </summary>
        public CompositionDocument Document { get; }

        /// <summary>
        /// Gets the resolved scene spans in playback order.
        /// </summary>
        public IReadOnlyList<VideoSceneSpan> Spans { get; }

        /// <summary>
        /// Gets the diagnostics sink.
        /// </summary>
        public List<VideoDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets the edit media by id.
        /// </summary>
        public Dictionary<string, VideoMedia> Media { get; }

        /// <summary>
        /// Gets the scene group layer of every scene, by scene id.
        /// </summary>
        public Dictionary<string, VisualLayer> Groups { get; } = new Dictionary<string, VisualLayer>(StringComparer.Ordinal);

        /// <summary>
        /// Creates the shared state.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="context">Context.</param>
        /// <param name="document">Composition being built.</param>
        /// <param name="spans">Scene spans.</param>
        /// <param name="diagnostics">Diagnostics sink.</param>
        public VideoCompileState(VideoEdit edit, VideoCompileContext context, CompositionDocument document, IReadOnlyList<VideoSceneSpan> spans, List<VideoDiagnostic> diagnostics) {
            Edit = edit;
            Context = context;
            Document = document;
            Spans = spans;
            Diagnostics = diagnostics;
            Media = edit.Media.ToDictionary(media => media.Id, StringComparer.Ordinal);
        }

        /// <summary>
        /// Resolves a scene moment to global time.
        /// </summary>
        /// <param name="span">Scene span.</param>
        /// <param name="moment">Moment, or null for the scene start.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Global time inside the scene.</returns>
        public MediaTime Global(VideoSceneSpan span, VideoMoment moment, string path) {
            return span.Start + VideoMomentResolver.Resolve(Edit, span, moment, path, Diagnostics);
        }

        /// <summary>
        /// Adds a member layer to the composition and to its scene group.
        /// </summary>
        /// <param name="span">Scene span owning the layer.</param>
        /// <param name="layer">Member layer.</param>
        public void AddMember(VideoSceneSpan span, VisualLayer layer) {
            Document.Layers.Add(layer);
            Groups[span.Scene.Id].Members.Add(layer.Id);
        }
    }
}

using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Compiles a <see cref="VideoEdit"/> into an executable <c>helengine.media.composition.v1</c> document: validates,
    /// lays scenes on the timeline, expands presets, builds layers, transitions, captions and audio, and checks the result
    /// against the engine's own composition validator. Every problem is reported through <see cref="VideoDiagnostic"/>.
    /// </summary>
    public static class VideoEditCompiler {
        /// <summary>
        /// Compiles one edit.
        /// </summary>
        /// <param name="edit">Edit document.</param>
        /// <param name="context">Catalog and preview/final mode.</param>
        /// <returns>Composition and diagnostics; the composition is null when the edit has errors.</returns>
        public static VideoCompileResult Compile(VideoEdit edit, VideoCompileContext context) {
            if (edit == null) {
                throw new ArgumentNullException(nameof(edit));
            }
            if (context?.Capabilities == null) {
                throw new ArgumentException("A capability catalog is required.", nameof(context));
            }
            VideoCompileResult result = new VideoCompileResult();
            result.Diagnostics.AddRange(VideoEditValidator.Validate(edit, context.Capabilities));
            if (result.HasErrors) {
                return result;
            }
            IReadOnlyList<VideoSceneSpan> spans = VideoSceneTimeline.Build(edit, result.Diagnostics);
            CompositionDocument document = new CompositionDocument {
                Id = edit.Id,
                Revision = (int)Math.Clamp(edit.Revision, 1, int.MaxValue),
                Width = edit.Format.Width,
                Height = edit.Format.Height,
                FrameRate = new MediaTime(edit.Format.FrameRate.Numerator, edit.Format.FrameRate.Denominator),
                Duration = spans.Count == 0 ? MediaTime.Zero : spans[^1].End,
                BackgroundColor = edit.Format.BackgroundColor,
                Media = edit.Media.Where(media => media.Kind != "font").Select(Reference).ToList()
            };
            VideoCompileState state = new VideoCompileState(edit, context, document, spans, result.Diagnostics);
            foreach (VideoSceneSpan span in spans) {
                VideoSceneCompiler.Compile(state, span);
                if (context.Final && span.Estimated) {
                    result.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "estimated_duration", span.Scene.Id, $"scenes[{span.Index}].duration", "The scene length is still an estimate; choose a take or a fixed length."));
                }
            }
            VideoCaptionBuilder.Build(state);
            VideoEntryCompiler.Compile(state);
            VideoAudioBuilder.Build(state);
            document.Audio.Enabled = document.AudioClips.Count > 0;
            if (result.HasErrors) {
                return result;
            }
            foreach (CompositionDiagnostic error in CompositionValidator.Validate(document, context.Capabilities)) {
                result.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "composition_invalid", null, error.Path, error.Code + ": " + error.Message));
            }
            result.Composition = result.HasErrors ? null : document;
            return result;
        }

        /// <summary>
        /// Converts one media entry to a pinned composition reference.
        /// </summary>
        /// <param name="media">Edit media.</param>
        /// <returns>Composition media reference.</returns>
        static MediaReference Reference(VideoMedia media) {
            return new MediaReference {
                Id = media.Id, Kind = media.Kind, Path = media.Path, Sha256 = media.Sha256, Width = media.Width, Height = media.Height,
                Duration = media.DurationSec > 0 ? MediaTime.FromSeconds(media.DurationSec) : MediaTime.Zero
            };
        }
    }
}

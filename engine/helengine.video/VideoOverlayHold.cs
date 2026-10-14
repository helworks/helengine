using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Applies the overlay reading time (<see cref="VideoOverlay.HoldSec"/>): an overlay stays visible at least that long
    /// after its last item appears, continuing over the following scenes when its own scene (or <c>until</c>) ends sooner,
    /// and reports held overlays that run into another overlay of a later scene in the same part of the screen.
    /// </summary>
    public static class VideoOverlayHold {
        /// <summary>
        /// Longest reading time an overlay may ask for, in seconds.
        /// </summary>
        public const double MaximumHoldSec = 10;

        /// <summary>
        /// Extends an overlay end so the overlay stays visible for its reading time after its last item appears, capped at
        /// the end of the video; an overlay without a reading time keeps its end.
        /// </summary>
        /// <param name="state">Compilation state supplying the video length.</param>
        /// <param name="overlay">Overlay.</param>
        /// <param name="lastAppearance">Absolute instant its last item appears.</param>
        /// <param name="end">Absolute end from <c>until</c> or the scene end.</param>
        /// <returns>Absolute end, never earlier than <paramref name="end"/>.</returns>
        public static MediaTime End(VideoCompileState state, VideoOverlay overlay, MediaTime lastAppearance, MediaTime end) {
            if (overlay.HoldSec is not double hold || !double.IsFinite(hold) || hold <= 0) {
                return end;
            }
            MediaTime held = lastAppearance + MediaTime.FromSeconds(Math.Min(hold, MaximumHoldSec));
            if (held > state.Document.Duration) {
                held = state.Document.Duration;
            }
            return held > end ? held : end;
        }

        /// <summary>
        /// Warns about every overlay held past its scene that is on screen at the same time as an overlay of a later scene
        /// whose box intersects its own (<c>overlay_hold_overlap</c>).
        /// </summary>
        /// <param name="state">Compilation state with every scene compiled.</param>
        public static void Check(VideoCompileState state) {
            foreach (VideoOverlayPlacement held in state.OverlayPlacements.Where(placement => placement.End > placement.Span.End)) {
                foreach (VideoOverlayPlacement other in state.OverlayPlacements) {
                    if (other.Span.Index <= held.Span.Index || other.Start >= held.End || other.End <= held.Start) {
                        continue;
                    }
                    if (held.Bounds == null || other.Bounds == null || !held.Bounds.Overlaps(other.Bounds)) {
                        continue;
                    }
                    state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "overlay_hold_overlap", other.Span.Scene.Id, other.Path,
                        $"Overlay '{held.Overlay.Id}' of scene '{held.Span.Scene.Id}' is held on screen until {held.End.ToSeconds():0.##}s and overlaps this overlay in the same part of the screen."));
                }
            }
        }
    }
}

using System.Globalization;
using System.Text;
using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Turns a <see cref="VideoMoment"/> into scene-local time. Seconds, seconds-before-end and fractions are arithmetic;
    /// a word anchor finds the n-th matching word spoken inside the scene take (case, accents and punctuation ignored).
    /// Results are clamped into the scene; problems are reported as diagnostics, never thrown.
    /// </summary>
    public static class VideoMomentResolver {
        /// <summary>
        /// Resolves one moment to scene-local time.
        /// </summary>
        /// <param name="edit">Edit owning the take media.</param>
        /// <param name="span">Scene span the moment belongs to.</param>
        /// <param name="moment">Moment to resolve; null means the scene start.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <param name="diagnostics">Receives anchor and clamping findings.</param>
        /// <returns>Local time between zero and the scene duration.</returns>
        public static MediaTime Resolve(VideoEdit edit, VideoSceneSpan span, VideoMoment moment, string path, List<VideoDiagnostic> diagnostics) {
            if (moment == null) {
                return MediaTime.Zero;
            }
            double duration = span.Duration.ToSeconds();
            double seconds;
            if (moment.Sec.HasValue) {
                seconds = moment.Sec.Value;
            } else if (moment.FromEnd.HasValue) {
                seconds = duration - moment.FromEnd.Value;
            } else if (moment.Fraction.HasValue) {
                seconds = duration * moment.Fraction.Value;
            } else if (!string.IsNullOrWhiteSpace(moment.Word)) {
                seconds = WordStart(edit, span, moment, path, diagnostics);
            } else {
                seconds = 0;
            }
            if (!double.IsFinite(seconds)) {
                seconds = 0;
            }
            if (seconds < 0 || seconds > duration) {
                diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "moment_clamped", span.Scene.Id, path, "The moment falls outside its scene and was moved to the nearest edge."));
                seconds = Math.Clamp(seconds, 0, duration);
            }
            return seconds >= duration ? span.Duration : MediaTime.FromSeconds(seconds);
        }

        /// <summary>
        /// Finds the local start of a spoken word or phrase inside the scene take.
        /// </summary>
        /// <param name="edit">Edit owning the take media.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="moment">Word moment; a phrase matches consecutive words.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <param name="diagnostics">Receives <c>anchor_unresolved</c> when the word is not found.</param>
        /// <returns>Local seconds of the first matching word start plus its offset, or zero when unresolved.</returns>
        static double WordStart(VideoEdit edit, VideoSceneSpan span, VideoMoment moment, string path, List<VideoDiagnostic> diagnostics) {
            VideoTake take = span.Scene.Take;
            VideoMedia media = take == null ? null : edit.Media.FirstOrDefault(item => item.Id == take.Media);
            string[] target = moment.Word.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(token => token.Length > 0).ToArray();
            List<VideoWord> words = (media?.Analysis?.Words ?? []).Where(word => word.StartSec >= take.InSec && word.StartSec < take.OutSec).ToList();
            int seen = 0;
            for (int index = 0; target.Length > 0 && index + target.Length <= words.Count; index++) {
                bool matches = true;
                for (int offset = 0; offset < target.Length && matches; offset++) {
                    matches = Normalize(words[index + offset].Text) == target[offset];
                }
                if (matches && ++seen == Math.Max(1, moment.Occurrence)) {
                    return words[index].StartSec - take.InSec + moment.OffsetSec;
                }
            }
            diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "anchor_unresolved", span.Scene.Id, path, $"The word '{moment.Word}' was not found in the scene take."));
            return 0;
        }

        /// <summary>
        /// Normalizes a word for comparison: lowercase, accents removed, only letters and digits kept.
        /// </summary>
        /// <param name="text">Word as written or transcribed.</param>
        /// <returns>Comparable form.</returns>
        public static string Normalize(string text) {
            string decomposed = (text ?? "").Normalize(NormalizationForm.FormD).ToLowerInvariant();
            StringBuilder result = new StringBuilder(decomposed.Length);
            foreach (char character in decomposed) {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)) {
                    result.Append(character);
                }
            }
            return result.ToString();
        }
    }
}

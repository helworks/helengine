using System.Globalization;
using System.Text;
using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Renders a cooked timeline as canonical text, one line per entry, so tests can compare whole assets and show a
    /// readable difference when they disagree.
    /// </summary>
    static class CookedTimelineDump {
        /// <summary>
        /// Renders the whole asset.
        /// </summary>
        /// <param name="asset">Cooked timeline.</param>
        /// <returns>Canonical text.</returns>
        public static string Describe(CookedTimelineAsset asset) {
            StringBuilder text = new StringBuilder();
            text.Append("rate=").Append(asset.TickRate).Append(" duration=").Append(asset.DurationTicks).AppendLine();
            text.Append("slots=").AppendLine(string.Join(",", asset.SlotNames));
            text.Append("strings=").AppendLine(string.Join(",", asset.Strings));
            for (int index = 0; index < asset.Tracks.Length; index++) {
                text.AppendLine(DescribeTrack(asset.Tracks[index]));
            }
            return text.ToString();
        }

        /// <summary>
        /// Renders one track on one line.
        /// </summary>
        /// <param name="track">Track to render.</param>
        /// <returns>Canonical text.</returns>
        public static string DescribeTrack(CookedTimelineTrack track) {
            StringBuilder text = new StringBuilder();
            text.Append(track.Kind).Append(" slot=").Append(track.SlotIndex).Append(" receiver=").Append(track.ReceiverId)
                .Append(" channel=").Append(track.ChannelIndex).Append(" mode=").Append(track.Mode).Append(':');
            for (int index = 0; index < track.Segments.Length; index++) {
                CookedTimelineSegment segment = track.Segments[index];
                text.Append(" [").Append(segment.StartTick).Append('-').Append(segment.EndTick).Append(' ').Append(Number(segment.From))
                    .Append("->").Append(Number(segment.To)).Append(" c").Append(segment.CurveCode).Append(']');
            }
            for (int index = 0; index < track.Intervals.Length; index++) {
                text.Append(" [").Append(track.Intervals[index].StartTick).Append('-').Append(track.Intervals[index].EndTick).Append(']');
            }
            for (int index = 0; index < track.AudioClips.Length; index++) {
                CookedTimelineAudioClip clip = track.AudioClips[index];
                text.Append(" [").Append(clip.StartTick).Append('-').Append(clip.EndTick).Append(" in").Append(clip.ClipInTicks)
                    .Append(" gain").Append(Number(clip.Gain)).Append(' ').Append(clip.Audio.RelativePath).Append(']');
            }
            for (int index = 0; index < track.AnimationClips.Length; index++) {
                CookedTimelineAnimationClip clip = track.AnimationClips[index];
                text.Append(" [").Append(clip.StartTick).Append('-').Append(clip.EndTick).Append(" in").Append(clip.ClipInTicks)
                    .Append(" x").Append(Number(clip.Speed)).Append(' ').Append(clip.Animation.RelativePath).Append(']');
            }
            for (int index = 0; index < track.Markers.Length; index++) {
                CookedTimelineMarker marker = track.Markers[index];
                text.Append(" [@").Append(marker.Tick).Append(' ').Append(marker.NameIndex).Append('=').Append(marker.ValueIndex).Append(']');
            }
            return text.ToString();
        }

        /// <summary>
        /// Formats a float with four decimals in the invariant culture.
        /// </summary>
        /// <param name="value">Value to format.</param>
        /// <returns>Formatted value.</returns>
        static string Number(float value) {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}

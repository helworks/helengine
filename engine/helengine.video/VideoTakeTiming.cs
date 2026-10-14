using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Maps a take's source seconds to scene time. A take plays its segments (or its single interval) back to back at its
    /// speed, so a source instant inside a kept segment lands at the segment's scene offset plus its distance from the
    /// segment start divided by the speed; source instants in the gaps between segments are not played.
    /// </summary>
    public static class VideoTakeTiming {
        /// <summary>
        /// Gets the segments a take plays: its <see cref="VideoTake.Segments"/>, or its single interval when it has none.
        /// </summary>
        /// <param name="take">Take.</param>
        /// <returns>Segments in playback order.</returns>
        public static IReadOnlyList<VideoTakeSegment> Segments(VideoTake take) {
            return take.Segments is { Count: > 0 } segments ? segments : [new VideoTakeSegment { InSec = take.InSec, OutSec = take.OutSec }];
        }

        /// <summary>
        /// Gets the usable speed of a take: its speed when finite and positive, otherwise real time (the validator reports it).
        /// </summary>
        /// <param name="take">Take.</param>
        /// <returns>Source seconds per scene second.</returns>
        public static double Speed(VideoTake take) {
            return double.IsFinite(take.Speed) && take.Speed > 0 ? take.Speed : 1;
        }

        /// <summary>
        /// Lays the take segments end to end in scene time. Each piece lasts its source length divided by the speed, rounded
        /// to the nanosecond and never consuming more source than the segment holds, so composition layers and clips cut
        /// from a piece always fit their source interval exactly. Segments without a positive length are skipped.
        /// </summary>
        /// <param name="take">Take.</param>
        /// <returns>Pieces in playback order; empty when no segment has a positive length.</returns>
        public static List<VideoTakePiece> Pieces(VideoTake take) {
            double speed = Speed(take);
            List<VideoTakePiece> pieces = new List<VideoTakePiece>();
            MediaTime cursor = MediaTime.Zero;
            foreach (VideoTakeSegment segment in Segments(take)) {
                if (segment == null || !double.IsFinite(segment.InSec) || !double.IsFinite(segment.OutSec) || segment.OutSec <= segment.InSec) {
                    continue;
                }
                MediaTime sourceIn = MediaTime.FromSeconds(segment.InSec), sourceOut = MediaTime.FromSeconds(segment.OutSec);
                MediaTime duration = MediaTime.FromSeconds((segment.OutSec - segment.InSec) / speed);
                MediaTime nanosecond = new MediaTime(1, 1000000000);
                while (duration > nanosecond && duration.AtRate(speed) > sourceOut - sourceIn) {
                    duration -= nanosecond;
                }
                pieces.Add(new VideoTakePiece { Segment = segment, SourceIn = sourceIn, SourceOut = sourceOut, Start = cursor, Duration = duration, Speed = speed });
                cursor += duration;
            }
            return pieces;
        }

        /// <summary>
        /// Gets the scene length a take plays: the sum of its piece lengths.
        /// </summary>
        /// <param name="take">Take.</param>
        /// <returns>Scene length; zero when the take has no playable segment.</returns>
        public static MediaTime Duration(VideoTake take) {
            List<VideoTakePiece> pieces = Pieces(take);
            return pieces.Count == 0 ? MediaTime.Zero : pieces[^1].End;
        }

        /// <summary>
        /// Finds the piece that plays a source instant, start inclusive and end exclusive.
        /// </summary>
        /// <param name="pieces">Pieces of the take.</param>
        /// <param name="sourceSec">Source seconds.</param>
        /// <returns>Playing piece, or null when the instant falls outside every segment.</returns>
        public static VideoTakePiece PieceAt(IReadOnlyList<VideoTakePiece> pieces, double sourceSec) {
            return pieces.FirstOrDefault(piece => sourceSec >= piece.Segment.InSec && sourceSec < piece.Segment.OutSec);
        }

        /// <summary>
        /// Maps a source span to the scene-time spans where it plays, clipped to every segment it overlaps.
        /// </summary>
        /// <param name="pieces">Pieces of the take.</param>
        /// <param name="startSec">Source start in seconds.</param>
        /// <param name="endSec">Source end in seconds.</param>
        /// <returns>Scene-time spans, each [start, end] in seconds, in playback order.</returns>
        public static List<double[]> MapSpan(IReadOnlyList<VideoTakePiece> pieces, double startSec, double endSec) {
            List<double[]> spans = new List<double[]>();
            foreach (VideoTakePiece piece in pieces) {
                double start = Math.Max(startSec, piece.Segment.InSec), end = Math.Min(endSec, piece.Segment.OutSec);
                if (end > start) {
                    spans.Add([piece.SceneSec(start), piece.SceneSec(end)]);
                }
            }
            return spans;
        }

        /// <summary>
        /// Maps source spans to scene time, merging spans that touch across a cut.
        /// </summary>
        /// <param name="pieces">Pieces of the take.</param>
        /// <param name="spans">Source spans, each [start, end] in seconds; null or malformed entries are ignored.</param>
        /// <returns>Scene-time spans in order, or null when <paramref name="spans"/> is null.</returns>
        public static List<double[]> MapSpans(IReadOnlyList<VideoTakePiece> pieces, IEnumerable<double[]> spans) {
            if (spans == null) {
                return null;
            }
            List<double[]> mapped = spans.Where(span => span is { Length: 2 } && double.IsFinite(span[0]) && double.IsFinite(span[1]))
                .SelectMany(span => MapSpan(pieces, span[0], span[1])).OrderBy(span => span[0]).ToList();
            List<double[]> merged = new List<double[]>();
            foreach (double[] span in mapped) {
                if (merged.Count > 0 && span[0] <= merged[^1][1] + 1e-9) {
                    merged[^1][1] = Math.Max(merged[^1][1], span[1]);
                } else {
                    merged.Add([span[0], span[1]]);
                }
            }
            return merged;
        }
    }
}

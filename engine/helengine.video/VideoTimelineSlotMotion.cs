using helengine.timeline;
using helengine.timeline.runtime;

namespace helengine.video {
    /// <summary>
    /// Everything a flattened overlay timeline does to one slot: its transform channels (absolute and offset), its opacity
    /// and reveal channels and its activation intervals. Evaluates the slot pose at any instant and lists the instants where
    /// its motion changes. A slot without an activation track is active for the whole timeline; channels without a value
    /// take their rest value (position 0, rotation 0, scale 1, opacity 1, reveal 1). Position Z, rotation X/Y and scale Z
    /// have no meaning in a video and are ignored.
    /// </summary>
    public sealed class VideoTimelineSlotMotion {
        /// <summary>
        /// Absolute transform tracks indexed by <see cref="CookedTimelineTransformChannel"/>; null where absent.
        /// </summary>
        readonly FlattenedCurveTrack[] Absolute = new FlattenedCurveTrack[9];

        /// <summary>
        /// Offset transform tracks indexed by <see cref="CookedTimelineTransformChannel"/>; null where absent.
        /// </summary>
        readonly FlattenedCurveTrack[] Offset = new FlattenedCurveTrack[9];

        /// <summary>
        /// Opacity channel, or null.
        /// </summary>
        readonly FlattenedCurveTrack OpacityTrack;

        /// <summary>
        /// Reveal channel, or null.
        /// </summary>
        readonly FlattenedCurveTrack RevealTrack;

        /// <summary>
        /// Collects the tracks of one slot.
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="slot">Root slot name.</param>
        public VideoTimelineSlotMotion(FlattenedTimeline flat, string slot) {
            Slot = slot;
            foreach (FlattenedCurveTrack track in flat.CurveTracks.Where(track => track.Slot == slot)) {
                if (track.Kind == TimelineTrackKind.Transform) {
                    (track.Mode == TimelineTransformMode.Offset ? Offset : Absolute)[(int)track.TransformChannel] = track;
                } else if (track.Channel == TimelineKnownChannels.Opacity) {
                    OpacityTrack = track;
                } else if (track.Channel == TimelineKnownChannels.Reveal) {
                    RevealTrack = track;
                }
            }
            FlattenedActivationTrack activation = flat.ActivationTracks.FirstOrDefault(track => track.Slot == slot);
            Intervals = activation == null ? null : activation.Intervals;
        }

        /// <summary>
        /// Gets the root slot name.
        /// </summary>
        public string Slot { get; }

        /// <summary>
        /// Gets the activation intervals in timeline seconds, or null when the slot has no activation track (always active).
        /// </summary>
        public IReadOnlyList<FlattenedInterval> Intervals { get; }

        /// <summary>
        /// Reports whether the slot is active at an instant (intervals include their start and exclude their end).
        /// </summary>
        /// <param name="seconds">Timeline instant.</param>
        /// <returns>True when active.</returns>
        public bool IsActive(double seconds) {
            return Intervals == null || Intervals.Any(interval => seconds >= interval.StartSeconds && seconds < interval.EndSeconds);
        }

        /// <summary>
        /// Evaluates the slot pose at an instant.
        /// </summary>
        /// <param name="seconds">Timeline instant.</param>
        /// <returns>Pose with the activation folded into the opacity.</returns>
        public VideoTimelinePose Pose(double seconds) {
            return new VideoTimelinePose {
                X = Additive(CookedTimelineTransformChannel.PositionX, seconds),
                Y = Additive(CookedTimelineTransformChannel.PositionY, seconds),
                Rotation = Additive(CookedTimelineTransformChannel.RotationZ, seconds),
                ScaleX = Multiplied(CookedTimelineTransformChannel.ScaleX, seconds),
                ScaleY = Multiplied(CookedTimelineTransformChannel.ScaleY, seconds),
                Opacity = IsActive(seconds) ? Math.Clamp(Value(OpacityTrack, seconds, 1), 0, 1) : 0,
                Reveal = Math.Clamp(Value(RevealTrack, seconds, 1), 0, 1)
            };
        }

        /// <summary>
        /// Lists the curve tracks a composition property of this slot depends on.
        /// </summary>
        /// <param name="property">Composition property.</param>
        /// <param name="rect">Whether the slot is drawn as a rectangle, whose reveal moves and scales it horizontally.</param>
        /// <returns>Contributing tracks.</returns>
        public List<FlattenedCurveTrack> Contributing(string property, bool rect) {
            List<FlattenedCurveTrack> tracks = new List<FlattenedCurveTrack>();
            if (property == "position_x") {
                Add(tracks, CookedTimelineTransformChannel.PositionX);
                if (rect) {
                    Add(tracks, CookedTimelineTransformChannel.ScaleX);
                    tracks.Add(RevealTrack);
                }
            } else if (property == "position_y") {
                Add(tracks, CookedTimelineTransformChannel.PositionY);
            } else if (property == "rotation_deg") {
                Add(tracks, CookedTimelineTransformChannel.RotationZ);
            } else if (property == "scale_x") {
                Add(tracks, CookedTimelineTransformChannel.ScaleX);
                if (rect) {
                    tracks.Add(RevealTrack);
                }
            } else if (property == "scale_y") {
                Add(tracks, CookedTimelineTransformChannel.ScaleY);
            } else if (property == "opacity") {
                tracks.Add(OpacityTrack);
            } else {
                throw new ArgumentException($"Unknown composition property '{property}'.", nameof(property));
            }
            tracks.RemoveAll(track => track == null);
            return tracks;
        }

        /// <summary>
        /// Lists every instant where the slot's motion may change: segment edges of its tracks and activation edges.
        /// </summary>
        /// <param name="tracks">Tracks whose segment edges count.</param>
        /// <param name="activation">Whether activation edges count.</param>
        /// <returns>Instants in timeline seconds, unsorted and possibly repeated.</returns>
        public List<double> Breakpoints(IEnumerable<FlattenedCurveTrack> tracks, bool activation) {
            List<double> breaks = new List<double>();
            foreach (FlattenedCurveTrack track in tracks) {
                foreach (FlattenedCurveSegment segment in track.Segments) {
                    breaks.Add(segment.StartSeconds);
                    breaks.Add(segment.EndSeconds);
                }
            }
            if (activation && Intervals != null) {
                foreach (FlattenedInterval interval in Intervals) {
                    breaks.Add(interval.StartSeconds);
                    breaks.Add(interval.EndSeconds);
                }
            }
            return breaks;
        }

        /// <summary>
        /// Gets every curve track of the slot.
        /// </summary>
        /// <returns>Tracks of the slot.</returns>
        public List<FlattenedCurveTrack> AllTracks() {
            List<FlattenedCurveTrack> tracks = new List<FlattenedCurveTrack>();
            tracks.AddRange(Absolute.Where(track => track != null));
            tracks.AddRange(Offset.Where(track => track != null));
            if (OpacityTrack != null) {
                tracks.Add(OpacityTrack);
            }
            if (RevealTrack != null) {
                tracks.Add(RevealTrack);
            }
            return tracks;
        }

        /// <summary>
        /// Finds the curve a property follows across an interval when exactly one of its tracks changes there, with one
        /// whole segment spanning the interval; linear otherwise (the caller verifies and subdivides).
        /// </summary>
        /// <param name="tracks">Tracks the property depends on.</param>
        /// <param name="start">Interval start in timeline seconds.</param>
        /// <param name="end">Interval end in timeline seconds.</param>
        /// <returns>Catalog curve id.</returns>
        public static string CurveOn(IReadOnlyList<FlattenedCurveTrack> tracks, double start, double end) {
            FlattenedCurveSegment found = null;
            int changing = 0;
            foreach (FlattenedCurveTrack track in tracks) {
                foreach (FlattenedCurveSegment segment in track.Segments) {
                    bool overlaps = segment.StartSeconds < end - 1e-9 && segment.EndSeconds > start + 1e-9;
                    if (overlaps && Math.Abs(segment.StartValue - segment.EndValue) > 1e-12) {
                        changing++;
                        found = segment;
                    }
                }
            }
            if (changing == 1 && found.IsWholeCurve && Math.Abs(found.StartSeconds - start) < 1e-9 && Math.Abs(found.EndSeconds - end) < 1e-9) {
                return found.Curve;
            }
            return CurveCatalog.Linear;
        }

        /// <summary>
        /// Adds the absolute and offset tracks of one transform channel.
        /// </summary>
        /// <param name="tracks">List to extend.</param>
        /// <param name="channel">Transform channel.</param>
        void Add(List<FlattenedCurveTrack> tracks, CookedTimelineTransformChannel channel) {
            tracks.Add(Absolute[(int)channel]);
            tracks.Add(Offset[(int)channel]);
        }

        /// <summary>
        /// Evaluates a position or rotation channel: the absolute value (0 at rest) plus the offset.
        /// </summary>
        /// <param name="channel">Transform channel.</param>
        /// <param name="seconds">Timeline instant.</param>
        /// <returns>Channel value.</returns>
        double Additive(CookedTimelineTransformChannel channel, double seconds) {
            return Value(Absolute[(int)channel], seconds, 0) + Value(Offset[(int)channel], seconds, 0);
        }

        /// <summary>
        /// Evaluates a scale channel: the absolute value (1 at rest) times the offset factor.
        /// </summary>
        /// <param name="channel">Transform channel.</param>
        /// <param name="seconds">Timeline instant.</param>
        /// <returns>Channel value.</returns>
        double Multiplied(CookedTimelineTransformChannel channel, double seconds) {
            return Value(Absolute[(int)channel], seconds, 1) * Value(Offset[(int)channel], seconds, 1);
        }

        /// <summary>
        /// Evaluates one track, or returns the rest value when the track is absent or has no value yet.
        /// </summary>
        /// <param name="track">Track, or null.</param>
        /// <param name="seconds">Timeline instant.</param>
        /// <param name="rest">Rest value.</param>
        /// <returns>Track value.</returns>
        static double Value(FlattenedCurveTrack track, double seconds, double rest) {
            if (track == null || !track.TryEvaluate(seconds, out double value)) {
                return rest;
            }
            return value;
        }
    }
}

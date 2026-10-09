using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// Accumulates everything <see cref="TimelineFlattener"/> collects while walking a timeline and its nested
    /// timelines, keyed by root slot and channel, and builds the merged <see cref="FlattenedTimeline"/> at the end.
    /// </summary>
    sealed class TimelineFlattenState {
        /// <summary>
        /// Curve targets in order of first appearance.
        /// </summary>
        readonly List<string> CurveKeys = new List<string>();

        /// <summary>
        /// Description (kind, slot, channel, mode) of each curve target.
        /// </summary>
        readonly Dictionary<string, FlattenedCurveTrack> CurveTargets = new Dictionary<string, FlattenedCurveTrack>(StringComparer.Ordinal);

        /// <summary>
        /// Segment lists collected for each curve target, in priority order.
        /// </summary>
        readonly Dictionary<string, List<List<FlattenedCurveSegment>>> CurveSources = new Dictionary<string, List<List<FlattenedCurveSegment>>>(StringComparer.Ordinal);

        /// <summary>
        /// Slots with activation clips in order of first appearance.
        /// </summary>
        readonly List<string> ActivationSlots = new List<string>();

        /// <summary>
        /// Activation intervals per slot.
        /// </summary>
        readonly Dictionary<string, List<FlattenedInterval>> ActivationBySlot = new Dictionary<string, List<FlattenedInterval>>(StringComparer.Ordinal);

        /// <summary>
        /// Slots with animation clips in order of first appearance.
        /// </summary>
        readonly List<string> AnimationSlots = new List<string>();

        /// <summary>
        /// Animation clips per slot.
        /// </summary>
        readonly Dictionary<string, List<FlattenedAnimationClip>> AnimationBySlot = new Dictionary<string, List<FlattenedAnimationClip>>(StringComparer.Ordinal);

        /// <summary>
        /// Sounds in collection order.
        /// </summary>
        readonly List<FlattenedAudioClip> AudioClips = new List<FlattenedAudioClip>();

        /// <summary>
        /// Events in collection order.
        /// </summary>
        readonly List<FlattenedEventMarker> Events = new List<FlattenedEventMarker>();

        /// <summary>
        /// Initializes the state.
        /// </summary>
        /// <param name="blendTolerance">Largest error accepted when approximating cross-fades.</param>
        public TimelineFlattenState(double blendTolerance) {
            BlendTolerance = blendTolerance;
        }

        /// <summary>
        /// Gets the largest error accepted when approximating cross-fades.
        /// </summary>
        public double BlendTolerance { get; }

        /// <summary>
        /// Adds one track instance's segments for a transform component.
        /// </summary>
        /// <param name="slot">Root slot.</param>
        /// <param name="channel">Transform component.</param>
        /// <param name="mode">Absolute or offset.</param>
        /// <param name="segments">Segments in root time.</param>
        public void AddTransform(string slot, CookedTimelineTransformChannel channel, TimelineTransformMode mode, List<FlattenedCurveSegment> segments) {
            string key = "transform|" + slot + "|" + (int)channel + "|" + (int)mode;
            AddCurve(key, new FlattenedCurveTrack { Kind = TimelineTrackKind.Transform, Slot = slot, TransformChannel = channel, Mode = mode }, segments);
        }

        /// <summary>
        /// Adds one track instance's segments for a value channel.
        /// </summary>
        /// <param name="slot">Root slot.</param>
        /// <param name="channel">Channel name.</param>
        /// <param name="segments">Segments in root time.</param>
        public void AddValue(string slot, string channel, List<FlattenedCurveSegment> segments) {
            string key = "value|" + slot + "|" + channel;
            AddCurve(key, new FlattenedCurveTrack { Kind = TimelineTrackKind.Value, Slot = slot, Channel = channel }, segments);
        }

        /// <summary>
        /// Adds an activation interval.
        /// </summary>
        /// <param name="slot">Root slot.</param>
        /// <param name="interval">Interval in root time.</param>
        public void AddActivation(string slot, FlattenedInterval interval) {
            List<FlattenedInterval> intervals;
            if (!ActivationBySlot.TryGetValue(slot, out intervals)) {
                intervals = new List<FlattenedInterval>();
                ActivationBySlot.Add(slot, intervals);
                ActivationSlots.Add(slot);
            }
            intervals.Add(interval);
        }

        /// <summary>
        /// Adds an animation clip.
        /// </summary>
        /// <param name="slot">Root slot.</param>
        /// <param name="clip">Clip in root time.</param>
        public void AddAnimation(string slot, FlattenedAnimationClip clip) {
            List<FlattenedAnimationClip> clips;
            if (!AnimationBySlot.TryGetValue(slot, out clips)) {
                clips = new List<FlattenedAnimationClip>();
                AnimationBySlot.Add(slot, clips);
                AnimationSlots.Add(slot);
            }
            clips.Add(clip);
        }

        /// <summary>
        /// Adds a sound.
        /// </summary>
        /// <param name="clip">Sound in root time.</param>
        public void AddAudio(FlattenedAudioClip clip) {
            AudioClips.Add(clip);
        }

        /// <summary>
        /// Adds an event.
        /// </summary>
        /// <param name="marker">Event in root time.</param>
        public void AddEvent(FlattenedEventMarker marker) {
            Events.Add(marker);
        }

        /// <summary>
        /// Merges everything collected into the flattened timeline of a root.
        /// </summary>
        /// <param name="root">Root timeline.</param>
        /// <returns>The flattened timeline.</returns>
        public FlattenedTimeline Build(TimelineAsset root) {
            FlattenedTimeline flat = new FlattenedTimeline { TimelineId = root.TimelineId, DurationSeconds = root.DurationSeconds };
            flat.Slots.AddRange(root.Slots);
            for (int index = 0; index < CurveKeys.Count; index++) {
                string key = CurveKeys[index];
                List<FlattenedCurveSegment> merged = TimelineCurveMerger.Merge(CurveSources[key]);
                if (merged.Count == 0) {
                    continue;
                }

                FlattenedCurveTrack track = CurveTargets[key];
                track.Segments.AddRange(merged);
                flat.CurveTracks.Add(track);
            }

            for (int index = 0; index < ActivationSlots.Count; index++) {
                FlattenedActivationTrack track = new FlattenedActivationTrack { Slot = ActivationSlots[index] };
                track.Intervals.AddRange(Union(ActivationBySlot[ActivationSlots[index]]));
                flat.ActivationTracks.Add(track);
            }

            for (int index = 0; index < AnimationSlots.Count; index++) {
                FlattenedAnimationTrack track = new FlattenedAnimationTrack { Slot = AnimationSlots[index] };
                track.Clips.AddRange(CutOverlaps(AnimationBySlot[AnimationSlots[index]]));
                flat.AnimationTracks.Add(track);
            }

            flat.AudioClips.AddRange(AudioClips.OrderBy(clip => clip.StartSeconds));
            flat.Events.AddRange(Events.OrderBy(marker => marker.TimeSeconds));
            return flat;
        }

        /// <summary>
        /// Records one segment list for a curve target, remembering the target on first sight.
        /// </summary>
        /// <param name="key">Target key.</param>
        /// <param name="target">Target description.</param>
        /// <param name="segments">Segments in root time.</param>
        void AddCurve(string key, FlattenedCurveTrack target, List<FlattenedCurveSegment> segments) {
            if (segments.Count == 0) {
                return;
            }

            List<List<FlattenedCurveSegment>> sources;
            if (!CurveSources.TryGetValue(key, out sources)) {
                sources = new List<List<FlattenedCurveSegment>>();
                CurveSources.Add(key, sources);
                CurveTargets.Add(key, target);
                CurveKeys.Add(key);
            }
            sources.Add(segments);
        }

        /// <summary>
        /// Sorts intervals and joins overlapping or touching ones.
        /// </summary>
        /// <param name="intervals">Intervals of one slot.</param>
        /// <returns>Sorted, disjoint intervals.</returns>
        static List<FlattenedInterval> Union(List<FlattenedInterval> intervals) {
            List<FlattenedInterval> sorted = intervals.OrderBy(interval => interval.StartSeconds).ToList();
            List<FlattenedInterval> output = new List<FlattenedInterval>();
            for (int index = 0; index < sorted.Count; index++) {
                FlattenedInterval interval = sorted[index];
                FlattenedInterval last = output.Count == 0 ? null : output[output.Count - 1];
                if (last != null && interval.StartSeconds <= last.EndSeconds + TimelineValidator.TimeTolerance) {
                    if (interval.EndSeconds > last.EndSeconds) {
                        last.EndSeconds = interval.EndSeconds;
                    }
                } else {
                    output.Add(new FlattenedInterval { StartSeconds = interval.StartSeconds, EndSeconds = interval.EndSeconds });
                }
            }
            return output;
        }

        /// <summary>
        /// Sorts animation clips by start and cuts each at the start of the next.
        /// </summary>
        /// <param name="clips">Clips of one slot.</param>
        /// <returns>Sorted, non-overlapping clips.</returns>
        static List<FlattenedAnimationClip> CutOverlaps(List<FlattenedAnimationClip> clips) {
            List<FlattenedAnimationClip> sorted = clips.OrderBy(clip => clip.StartSeconds).ToList();
            for (int index = 0; index + 1 < sorted.Count; index++) {
                if (sorted[index].EndSeconds > sorted[index + 1].StartSeconds) {
                    sorted[index].EndSeconds = sorted[index + 1].StartSeconds;
                }
            }
            return sorted;
        }
    }
}

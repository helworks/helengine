namespace helengine.timeline {
    /// <summary>
    /// Moves the root cues of a timeline to new times, as hosts do when they anchor cues on outside events (a video edit
    /// puts each cue on a spoken word). Clips anchored on a moved cue shift with it and keep their length; the timeline
    /// grows by the largest later shift so shifted clips still fit, and root activation clips that lasted until the
    /// authored end keep lasting until the new end. Clips and markers are put back in start order; the original timeline is
    /// never modified.
    /// </summary>
    static class TimelineCueRetiming {
        /// <summary>
        /// Builds the retimed copy of a timeline.
        /// </summary>
        /// <param name="timeline">Validated root timeline.</param>
        /// <param name="cueTimes">New time, in seconds of the root timeline, of every cue to move; other cues keep their time.</param>
        /// <returns>A deep copy with the cues moved, the duration extended and end-holding activation clips extended.</returns>
        /// <exception cref="ArgumentException">A cue name is unknown or a time is negative or not finite.</exception>
        public static TimelineAsset Apply(TimelineAsset timeline, IReadOnlyDictionary<string, double> cueTimes) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            } else if (cueTimes == null) {
                throw new ArgumentNullException(nameof(cueTimes));
            }

            TimelineAsset copy = TimelineJson.ReadUnvalidated(TimelineJson.Write(timeline));
            double shift = 0;
            foreach (KeyValuePair<string, double> entry in cueTimes) {
                TimelineCueAsset cue = FindCue(copy, entry.Key);
                if (cue == null) {
                    throw new ArgumentException($"Timeline '{timeline.TimelineId}' has no cue '{entry.Key}'.", nameof(cueTimes));
                } else if (!double.IsFinite(entry.Value) || entry.Value < 0) {
                    throw new ArgumentException($"Cue '{entry.Key}' needs a finite time of 0 or more, got {entry.Value}.", nameof(cueTimes));
                }

                shift = Math.Max(shift, entry.Value - cue.TimeSeconds);
            }

            double authoredEnd = timeline.DurationSeconds;
            List<TimelineClipAsset> holdingClips = new List<TimelineClipAsset>();
            for (int index = 0; index < copy.Tracks.Count; index++) {
                if (copy.Tracks[index] is TimelineActivationTrackAsset activation) {
                    for (int clipIndex = 0; clipIndex < activation.Clips.Count; clipIndex++) {
                        TimelineClipAsset clip = activation.Clips[clipIndex];
                        double start = clip.ResolveStart(CueTime(copy, clip.Cue));
                        if (start + clip.DurationSeconds >= authoredEnd - TimelineValidator.TimeTolerance) {
                            holdingClips.Add(clip);
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, double> entry in cueTimes) {
                FindCue(copy, entry.Key).TimeSeconds = entry.Value;
            }

            copy.DurationSeconds = authoredEnd + shift;
            for (int index = 0; index < holdingClips.Count; index++) {
                TimelineClipAsset clip = holdingClips[index];
                clip.DurationSeconds = copy.DurationSeconds - clip.ResolveStart(CueTime(copy, clip.Cue));
            }
            for (int index = 0; index < copy.Tracks.Count; index++) {
                Reorder(copy, copy.Tracks[index]);
            }
            return copy;
        }

        /// <summary>
        /// Puts the clips (or markers) of a track back in start order after the cues moved; equal starts keep their
        /// authored order. Whether the moved clips now overlap is left to the validator.
        /// </summary>
        /// <param name="timeline">Retimed timeline declaring the cues.</param>
        /// <param name="track">Track to reorder.</param>
        static void Reorder(TimelineAsset timeline, TimelineTrackAsset track) {
            if (track is TimelineTransformTrackAsset transform) {
                transform.Clips = SortClips(timeline, transform.Clips);
            } else if (track is TimelineValueTrackAsset value) {
                value.Clips = SortClips(timeline, value.Clips);
            } else if (track is TimelineActivationTrackAsset activation) {
                activation.Clips = SortClips(timeline, activation.Clips);
            } else if (track is TimelineAudioTrackAsset audio) {
                audio.Clips = SortClips(timeline, audio.Clips);
            } else if (track is TimelineAnimationTrackAsset animation) {
                animation.Clips = SortClips(timeline, animation.Clips);
            } else if (track is TimelineNestedTrackAsset nested) {
                nested.Clips = SortClips(timeline, nested.Clips);
            } else if (track is TimelineEventTrackAsset events) {
                events.Markers = events.Markers.OrderBy(marker => marker.ResolveTime(CueTime(timeline, marker.Cue))).ToList();
            }
        }

        /// <summary>
        /// Sorts clips by their resolved start, keeping the authored order of equal starts.
        /// </summary>
        /// <typeparam name="T">Clip type of the track.</typeparam>
        /// <param name="timeline">Retimed timeline declaring the cues.</param>
        /// <param name="clips">Clips of one track.</param>
        /// <returns>A new list in start order.</returns>
        static List<T> SortClips<T>(TimelineAsset timeline, List<T> clips) where T : TimelineClipAsset {
            return clips.OrderBy(clip => clip.ResolveStart(CueTime(timeline, clip.Cue))).ToList();
        }

        /// <summary>
        /// Finds a cue of a timeline by name.
        /// </summary>
        /// <param name="timeline">Timeline declaring the cue.</param>
        /// <param name="name">Cue name.</param>
        /// <returns>The cue, or null when the timeline has none of that name.</returns>
        static TimelineCueAsset FindCue(TimelineAsset timeline, string name) {
            for (int index = 0; index < timeline.Cues.Count; index++) {
                if (string.Equals(timeline.Cues[index].Name, name, StringComparison.Ordinal)) {
                    return timeline.Cues[index];
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the time of the cue a clip is anchored on, or 0 for an absolute clip.
        /// </summary>
        /// <param name="timeline">Timeline declaring the cue.</param>
        /// <param name="cue">Cue name, or empty.</param>
        /// <returns>The cue time.</returns>
        static double CueTime(TimelineAsset timeline, string cue) {
            if (string.IsNullOrEmpty(cue)) {
                return 0;
            }
            return FindCue(timeline, cue).TimeSeconds;
        }
    }
}

namespace helengine.timeline.runtime {
    /// <summary>
    /// Forward-only cursors over the sorted arrays of cooked tracks. Each method takes the previous cursor and returns
    /// the index of the last entry starting at or before the tick (-1 when none has started). Playing forward costs a
    /// step at most per entry crossed; a backward jump (seek, loop) restarts the scan from the first entry.
    /// </summary>
    static class TimelineCursors {
        /// <summary>
        /// Finds the segment that applies at a tick.
        /// </summary>
        /// <param name="segments">Segments sorted by start tick.</param>
        /// <param name="cursor">Cursor returned for the previous tick.</param>
        /// <param name="tick">Tick being evaluated.</param>
        /// <returns>Index of the last segment starting at or before the tick, or -1.</returns>
        internal static int SeekSegment(CookedTimelineSegment[] segments, int cursor, int tick) {
            if (cursor >= segments.Length || (cursor >= 0 && segments[cursor].StartTick > tick)) {
                cursor = -1;
            }
            while (cursor + 1 < segments.Length && segments[cursor + 1].StartTick <= tick) {
                cursor++;
            }
            return cursor;
        }

        /// <summary>
        /// Finds the activation interval that may contain a tick.
        /// </summary>
        /// <param name="intervals">Intervals sorted by start tick.</param>
        /// <param name="cursor">Cursor returned for the previous tick.</param>
        /// <param name="tick">Tick being evaluated.</param>
        /// <returns>Index of the last interval starting at or before the tick, or -1.</returns>
        internal static int SeekInterval(CookedTimelineInterval[] intervals, int cursor, int tick) {
            if (cursor >= intervals.Length || (cursor >= 0 && intervals[cursor].StartTick > tick)) {
                cursor = -1;
            }
            while (cursor + 1 < intervals.Length && intervals[cursor + 1].StartTick <= tick) {
                cursor++;
            }
            return cursor;
        }

        /// <summary>
        /// Finds the animation clip that applies at a tick.
        /// </summary>
        /// <param name="clips">Animation clips sorted by start tick.</param>
        /// <param name="cursor">Cursor returned for the previous tick.</param>
        /// <param name="tick">Tick being evaluated.</param>
        /// <returns>Index of the last clip starting at or before the tick, or -1.</returns>
        internal static int SeekAnimation(CookedTimelineAnimationClip[] clips, int cursor, int tick) {
            if (cursor >= clips.Length || (cursor >= 0 && clips[cursor].StartTick > tick)) {
                cursor = -1;
            }
            while (cursor + 1 < clips.Length && clips[cursor + 1].StartTick <= tick) {
                cursor++;
            }
            return cursor;
        }

        /// <summary>
        /// Finds the first event marker strictly after a tick, used to place the event cursor after a seek.
        /// </summary>
        /// <param name="markers">Markers sorted by tick.</param>
        /// <param name="tick">Tick playback continues from.</param>
        /// <returns>Index of the first marker after the tick, or the marker count.</returns>
        internal static int FirstMarkerAfter(CookedTimelineMarker[] markers, int tick) {
            int index = 0;
            while (index < markers.Length && markers[index].Tick <= tick) {
                index++;
            }
            return index;
        }

        /// <summary>
        /// Finds the first audio clip starting strictly after a tick, used to place the audio cursor after a seek.
        /// </summary>
        /// <param name="clips">Audio clips sorted by start tick.</param>
        /// <param name="tick">Tick playback continues from.</param>
        /// <returns>Index of the first clip starting after the tick, or the clip count.</returns>
        internal static int FirstAudioAfter(CookedTimelineAudioClip[] clips, int tick) {
            int index = 0;
            while (index < clips.Length && clips[index].StartTick <= tick) {
                index++;
            }
            return index;
        }
    }
}

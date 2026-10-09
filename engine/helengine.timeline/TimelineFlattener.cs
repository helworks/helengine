using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// Resolves a validated timeline into a <see cref="FlattenedTimeline"/> in seconds of the root timeline: cues become
    /// times, nested timelines (inline or referenced) are expanded with their offset, clip-in, speed and slot mapping and
    /// cut to their clip window, keyframes become curve segments, blend ramps between neighbouring clips become linear
    /// cross-fades, and every target is merged into one track. Both the game cooker (<see cref="TimelineCooker"/>) and
    /// other compilers (for example the video bridge) start from this result.
    /// </summary>
    public static class TimelineFlattener {
        /// <summary>
        /// Default largest error, in value units, accepted when approximating cross-fades with straight segments.
        /// </summary>
        public const double DefaultBlendTolerance = 0.001;

        /// <summary>
        /// Flattens a timeline with the default blend tolerance.
        /// </summary>
        /// <param name="timeline">Root timeline.</param>
        /// <param name="resolver">Resolver for referenced nested timelines; may be null when the timeline references none.</param>
        /// <returns>The flattened timeline.</returns>
        public static FlattenedTimeline Flatten(TimelineAsset timeline, ITimelineAssetResolver resolver) {
            return Flatten(timeline, resolver, DefaultBlendTolerance);
        }

        /// <summary>
        /// Validates and flattens a timeline.
        /// </summary>
        /// <param name="timeline">Root timeline.</param>
        /// <param name="resolver">Resolver for referenced nested timelines; may be null when the timeline references none.</param>
        /// <param name="blendTolerance">Largest error, in value units, accepted when approximating cross-fades.</param>
        /// <returns>The flattened timeline.</returns>
        public static FlattenedTimeline Flatten(TimelineAsset timeline, ITimelineAssetResolver resolver, double blendTolerance) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            } else if (!(blendTolerance > 0) || double.IsInfinity(blendTolerance)) {
                throw new ArgumentOutOfRangeException(nameof(blendTolerance), "The blend tolerance must be a positive number.");
            }

            TimelineValidator.EnsureValid(timeline, resolver);
            TimelineFlattenState state = new TimelineFlattenState(blendTolerance);
            Dictionary<string, string> slotMap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < timeline.Slots.Count; index++) {
                slotMap.Add(timeline.Slots[index].Name, timeline.Slots[index].Name);
            }

            FlattenInstance(timeline, TimelineTimeMap.Root(timeline.DurationSeconds), slotMap, resolver, 1, state);
            return state.Build(timeline);
        }

        /// <summary>
        /// Validates and flattens a timeline after moving some of its root cues, as a host does when it anchors cues on
        /// outside events (for example the spoken words of a video). Clips anchored on a moved cue shift with it and keep
        /// their length (they are never stretched); the timeline grows by the largest later shift, and root activation
        /// clips that lasted until the authored end last until the new end. Cues of nested timelines are not affected.
        /// </summary>
        /// <param name="timeline">Root timeline.</param>
        /// <param name="resolver">Resolver for referenced nested timelines; may be null when the timeline references none.</param>
        /// <param name="cueTimes">New time, in seconds of the root timeline, of every root cue to move; other cues keep their time.</param>
        /// <param name="blendTolerance">Largest error, in value units, accepted when approximating cross-fades.</param>
        /// <returns>The flattened timeline; its duration is the extended one.</returns>
        /// <exception cref="ArgumentException">A cue name is unknown or a cue time is negative or not finite.</exception>
        /// <exception cref="TimelineFormatException">The timeline is invalid, or the moved cues make it invalid (for example two clips of one track now overlap).</exception>
        public static FlattenedTimeline Flatten(TimelineAsset timeline, ITimelineAssetResolver resolver, IReadOnlyDictionary<string, double> cueTimes, double blendTolerance) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            }

            TimelineValidator.EnsureValid(timeline, resolver);
            return Flatten(TimelineCueRetiming.Apply(timeline, cueTimes), resolver, blendTolerance);
        }

        /// <summary>
        /// Collects every track of one timeline instance.
        /// </summary>
        /// <param name="timeline">Timeline of the instance.</param>
        /// <param name="map">Local-to-root time map and visible window.</param>
        /// <param name="slotMap">Instance slot name to root slot name.</param>
        /// <param name="resolver">Resolver for referenced nested timelines.</param>
        /// <param name="depth">Nesting depth of the instance (root is 1).</param>
        /// <param name="state">Collected results.</param>
        static void FlattenInstance(TimelineAsset timeline, TimelineTimeMap map, Dictionary<string, string> slotMap, ITimelineAssetResolver resolver, int depth, TimelineFlattenState state) {
            for (int index = 0; index < timeline.Tracks.Count; index++) {
                TimelineTrackAsset track = timeline.Tracks[index];
                if (track is TimelineTransformTrackAsset transform) {
                    FlattenTransform(timeline, transform, map, slotMap[transform.Slot], state);
                } else if (track is TimelineValueTrackAsset value) {
                    FlattenValue(timeline, value, map, slotMap[value.Slot], state);
                } else if (track is TimelineActivationTrackAsset activation) {
                    FlattenActivation(timeline, activation, map, slotMap[activation.Slot], state);
                } else if (track is TimelineAudioTrackAsset audio) {
                    FlattenAudio(timeline, audio, map, string.IsNullOrEmpty(audio.Slot) ? string.Empty : slotMap[audio.Slot], state);
                } else if (track is TimelineAnimationTrackAsset animation) {
                    FlattenAnimation(timeline, animation, map, slotMap[animation.Slot], state);
                } else if (track is TimelineEventTrackAsset events) {
                    FlattenEvents(timeline, events, map, state);
                } else if (track is TimelineNestedTrackAsset nested) {
                    FlattenNested(timeline, nested, map, slotMap, resolver, depth, state);
                } else {
                    throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has an unsupported track type '{track.GetType().Name}'.");
                }
            }
        }

        /// <summary>
        /// Collects the nine transform components of a transform track.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Transform track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slot">Root slot of the track.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenTransform(TimelineAsset timeline, TimelineTransformTrackAsset track, TimelineTimeMap map, string slot, TimelineFlattenState state) {
            for (int channel = 0; channel <= (int)CookedTimelineTransformChannel.ScaleZ; channel++) {
                int property = channel / 3;
                int axis = channel % 3;
                List<TimelineClipChannel> clips = new List<TimelineClipChannel>();
                bool any = false;
                for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++) {
                    TimelineTransformClipAsset clip = track.Clips[clipIndex];
                    List<TimelineVectorKeyframeAsset> keyframes = property == 0 ? clip.Position : (property == 1 ? clip.Rotation : clip.Scale);
                    List<TimelineKeyframeSample> samples = new List<TimelineKeyframeSample>();
                    for (int key = 0; key < keyframes.Count; key++) {
                        TimelineVectorKeyframeAsset keyframe = keyframes[key];
                        double value = axis == 0 ? keyframe.X : (axis == 1 ? keyframe.Y : keyframe.Z);
                        samples.Add(new TimelineKeyframeSample { TimeSeconds = keyframe.TimeSeconds, Value = value, Curve = keyframe.Curve });
                    }

                    TimelineClipChannel clipChannel = CreateClipChannel(timeline, clip, samples);
                    any = any || clipChannel.Segments != null;
                    clips.Add(clipChannel);
                }
                if (any) {
                    state.AddTransform(slot, (CookedTimelineTransformChannel)channel, track.Mode, ToRoot(clips, map, state.BlendTolerance));
                }
            }
        }

        /// <summary>
        /// Collects a value track.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Value track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slot">Root slot of the track.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenValue(TimelineAsset timeline, TimelineValueTrackAsset track, TimelineTimeMap map, string slot, TimelineFlattenState state) {
            List<TimelineClipChannel> clips = new List<TimelineClipChannel>();
            for (int clipIndex = 0; clipIndex < track.Clips.Count; clipIndex++) {
                TimelineValueClipAsset clip = track.Clips[clipIndex];
                List<TimelineKeyframeSample> samples = new List<TimelineKeyframeSample>();
                for (int key = 0; key < clip.Keyframes.Count; key++) {
                    TimelineKeyframeAsset keyframe = clip.Keyframes[key];
                    samples.Add(new TimelineKeyframeSample { TimeSeconds = keyframe.TimeSeconds, Value = keyframe.Value, Curve = keyframe.Curve });
                }
                clips.Add(CreateClipChannel(timeline, clip, samples));
            }
            state.AddValue(slot, track.Channel, ToRoot(clips, map, state.BlendTolerance));
        }

        /// <summary>
        /// Collects the intervals of an activation track, cut to the instance window.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Activation track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slot">Root slot of the track.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenActivation(TimelineAsset timeline, TimelineActivationTrackAsset track, TimelineTimeMap map, string slot, TimelineFlattenState state) {
            for (int index = 0; index < track.Clips.Count; index++) {
                TimelineClipAsset clip = track.Clips[index];
                double localStart = ResolveStart(timeline, clip);
                double start = Math.Max(map.ToRoot(localStart), map.WindowStart);
                double end = Math.Min(map.ToRoot(localStart + clip.DurationSeconds), map.WindowEnd);
                if (end - start > TimelineValidator.TimeTolerance) {
                    state.AddActivation(slot, new FlattenedInterval { StartSeconds = start, EndSeconds = end });
                }
            }
        }

        /// <summary>
        /// Collects the sounds of an audio track. A sound cut at the start of the instance window skips the cut part of the
        /// asset; sounds keep their natural rate under nested speed.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Audio track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slot">Root slot of the emitter, or empty.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenAudio(TimelineAsset timeline, TimelineAudioTrackAsset track, TimelineTimeMap map, string slot, TimelineFlattenState state) {
            for (int index = 0; index < track.Clips.Count; index++) {
                TimelineAudioClipAsset clip = track.Clips[index];
                double localStart = ResolveStart(timeline, clip);
                double start = map.ToRoot(localStart);
                double end = Math.Min(map.ToRoot(localStart + clip.DurationSeconds), map.WindowEnd);
                double clipIn = clip.ClipInSeconds;
                if (start < map.WindowStart) {
                    clipIn += map.WindowStart - start;
                    start = map.WindowStart;
                }
                if (end - start > TimelineValidator.TimeTolerance) {
                    state.AddAudio(new FlattenedAudioClip {
                        Slot = slot,
                        StartSeconds = start,
                        EndSeconds = end,
                        ClipInSeconds = clipIn,
                        Gain = clip.Gain,
                        EaseInSeconds = clip.EaseInSeconds * map.Scale,
                        EaseOutSeconds = clip.EaseOutSeconds * map.Scale,
                        Audio = clip.Audio
                    });
                }
            }
        }

        /// <summary>
        /// Collects the clips of an animation track; nested speed multiplies the clip speed.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Animation track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slot">Root slot of the track.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenAnimation(TimelineAsset timeline, TimelineAnimationTrackAsset track, TimelineTimeMap map, string slot, TimelineFlattenState state) {
            for (int index = 0; index < track.Clips.Count; index++) {
                TimelineAnimationClipAsset clip = track.Clips[index];
                double localStart = ResolveStart(timeline, clip);
                double speed = clip.Speed / map.Scale;
                double start = map.ToRoot(localStart);
                double end = Math.Min(map.ToRoot(localStart + clip.DurationSeconds), map.WindowEnd);
                double clipIn = clip.ClipInSeconds;
                if (start < map.WindowStart) {
                    clipIn += (map.WindowStart - start) * speed;
                    start = map.WindowStart;
                }
                if (end - start > TimelineValidator.TimeTolerance) {
                    state.AddAnimation(slot, new FlattenedAnimationClip { StartSeconds = start, EndSeconds = end, ClipInSeconds = clipIn, Speed = speed, Animation = clip.Animation });
                }
            }
        }

        /// <summary>
        /// Collects the markers of an event track that fall inside the instance window.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Event track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenEvents(TimelineAsset timeline, TimelineEventTrackAsset track, TimelineTimeMap map, TimelineFlattenState state) {
            for (int index = 0; index < track.Markers.Count; index++) {
                TimelineEventMarkerAsset marker = track.Markers[index];
                double time = map.ToRoot(marker.ResolveTime(CueTime(timeline, marker.Cue)));
                if (time < map.WindowStart - TimelineValidator.TimeTolerance || time > map.WindowEnd + TimelineValidator.TimeTolerance) {
                    continue;
                }

                time = Math.Min(Math.Max(time, map.WindowStart), map.WindowEnd);
                state.AddEvent(new FlattenedEventMarker { TimeSeconds = time, Name = marker.Name, Value = marker.Value ?? string.Empty });
            }
        }

        /// <summary>
        /// Expands the clips of a nested-timeline track.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="track">Nested track.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="slotMap">Instance slot name to root slot name.</param>
        /// <param name="resolver">Resolver for referenced timelines.</param>
        /// <param name="depth">Nesting depth of the instance.</param>
        /// <param name="state">Collected results.</param>
        static void FlattenNested(TimelineAsset timeline, TimelineNestedTrackAsset track, TimelineTimeMap map, Dictionary<string, string> slotMap, ITimelineAssetResolver resolver, int depth, TimelineFlattenState state) {
            if (depth >= TimelineValidator.MaxNestingDepth) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' nests deeper than {TimelineValidator.MaxNestingDepth} levels.");
            }

            for (int index = 0; index < track.Clips.Count; index++) {
                TimelineNestedClipAsset clip = track.Clips[index];
                TimelineAsset child = ResolveNested(timeline, clip, resolver);
                TimelineTimeMap childMap = map.Nest(ResolveStart(timeline, clip), clip.DurationSeconds, clip.ClipInSeconds, clip.Speed);
                if (childMap.WindowEnd - childMap.WindowStart <= TimelineValidator.TimeTolerance) {
                    continue;
                }

                Dictionary<string, string> childSlots = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int mapping = 0; mapping < clip.SlotMappings.Count; mapping++) {
                    TimelineSlotMappingAsset slotMapping = clip.SlotMappings[mapping];
                    childSlots.Add(slotMapping.Inner, slotMap[slotMapping.Outer]);
                }
                FlattenInstance(child, childMap, childSlots, resolver, depth + 1, state);
            }
        }

        /// <summary>
        /// Returns the inline definition of a nested clip or resolves its reference.
        /// </summary>
        /// <param name="timeline">Timeline holding the clip (for messages).</param>
        /// <param name="clip">Nested clip.</param>
        /// <param name="resolver">Resolver for referenced timelines.</param>
        /// <returns>The nested timeline.</returns>
        static TimelineAsset ResolveNested(TimelineAsset timeline, TimelineNestedClipAsset clip, ITimelineAssetResolver resolver) {
            if (clip.Definition != null) {
                return clip.Definition;
            } else if (clip.Timeline == null) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' has a nested clip without a definition or reference.");
            } else if (resolver == null) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' references nested timeline '{clip.Timeline.RelativePath}', but no resolver was supplied.");
            }

            TimelineAsset resolved = resolver.Resolve(clip.Timeline);
            if (resolved == null) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' references nested timeline '{clip.Timeline.RelativePath}', which the resolver could not find.");
            }
            return resolved;
        }

        /// <summary>
        /// Builds one clip's contribution to a channel.
        /// </summary>
        /// <param name="timeline">Timeline of the instance (for cues).</param>
        /// <param name="clip">Clip.</param>
        /// <param name="samples">Channel keyframes of the clip.</param>
        /// <returns>The clip channel in instance time.</returns>
        static TimelineClipChannel CreateClipChannel(TimelineAsset timeline, TimelineClipAsset clip, List<TimelineKeyframeSample> samples) {
            double start = ResolveStart(timeline, clip);
            return new TimelineClipChannel {
                StartSeconds = start,
                EndSeconds = start + clip.DurationSeconds,
                Segments = TimelineChannelBuilder.BuildClipSegments(start, clip.DurationSeconds, samples)
            };
        }

        /// <summary>
        /// Joins a track's clips for one channel, maps them to root time and cuts them to the instance window.
        /// </summary>
        /// <param name="clips">Clip channels in instance time.</param>
        /// <param name="map">Time map of the instance.</param>
        /// <param name="tolerance">Blend approximation tolerance.</param>
        /// <returns>Segments in root time.</returns>
        static List<FlattenedCurveSegment> ToRoot(List<TimelineClipChannel> clips, TimelineTimeMap map, double tolerance) {
            List<FlattenedCurveSegment> local = TimelineChannelBuilder.CombineClips(clips, tolerance);
            List<FlattenedCurveSegment> mapped = new List<FlattenedCurveSegment>(local.Count);
            for (int index = 0; index < local.Count; index++) {
                mapped.Add(local[index].Map(map.Offset, map.Scale));
            }

            List<FlattenedCurveSegment> output = new List<FlattenedCurveSegment>(mapped.Count);
            TimelinePiecewise.AppendTrimmed(mapped, map.WindowStart, map.WindowEnd, output);
            return output;
        }

        /// <summary>
        /// Resolves a clip start against the cues of its own timeline.
        /// </summary>
        /// <param name="timeline">Timeline holding the clip.</param>
        /// <param name="clip">Clip.</param>
        /// <returns>The start in the timeline's local time.</returns>
        static double ResolveStart(TimelineAsset timeline, TimelineClipAsset clip) {
            return clip.ResolveStart(CueTime(timeline, clip.Cue));
        }

        /// <summary>
        /// Returns the time of a named cue, or 0 when no cue is named.
        /// </summary>
        /// <param name="timeline">Timeline declaring the cue.</param>
        /// <param name="cue">Cue name or empty.</param>
        /// <returns>The cue time.</returns>
        static double CueTime(TimelineAsset timeline, string cue) {
            if (string.IsNullOrEmpty(cue)) {
                return 0;
            }

            double time;
            if (!timeline.TryGetCueTime(cue, out time)) {
                throw new InvalidOperationException($"Timeline '{timeline.TimelineId}' uses unknown cue '{cue}'.");
            }
            return time;
        }
    }
}

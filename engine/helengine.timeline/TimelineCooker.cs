using helengine.timeline.runtime;

namespace helengine.timeline {
    /// <summary>
    /// Cooks an authoring timeline into the flat <see cref="CookedTimelineAsset"/> a game plays. It flattens the timeline
    /// (<see cref="TimelineFlattener"/>: nesting, cues, blends), converts seconds to integer ticks at the profile's tick
    /// rate, writes curves natively or as straight segments, resolves slot names to indices and value channels to
    /// receiver ids and channel indices, and moves event names and values into a string table.
    /// <para>
    /// Times become ticks by rounding to the nearest tick (exact halves round up), so every boundary moves by at most
    /// half a tick and shared boundaries stay shared. A segment whose rounded span is empty becomes a step to its end
    /// value (dropped when another segment starts on the same tick). Linearized curves are split at ticks so that every
    /// tick is within the tolerance of the native curve evaluated on the same rounded span.
    /// </para>
    /// </summary>
    public static class TimelineCooker {
        /// <summary>
        /// Tolerance added before rounding seconds to ticks, so values a hair under an exact half still round up.
        /// </summary>
        const double TickEpsilon = 1e-9;

        /// <summary>
        /// Validates, flattens and cooks a timeline.
        /// </summary>
        /// <param name="timeline">Authoring timeline.</param>
        /// <param name="options">Platform profile settings.</param>
        /// <param name="resolver">Resolver for referenced nested timelines; may be null when none are referenced.</param>
        /// <param name="channels">Resolver for value channels; may be null when the timeline has no value tracks.</param>
        /// <returns>The cooked timeline, identified like the authoring asset.</returns>
        public static CookedTimelineAsset Cook(TimelineAsset timeline, TimelineCookOptions options, ITimelineAssetResolver resolver, ITimelineChannelResolver channels) {
            if (timeline == null) {
                throw new ArgumentNullException(nameof(timeline));
            } else if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            FlattenedTimeline flat = TimelineFlattener.Flatten(timeline, resolver, options.Tolerance);
            CookedTimelineAsset cooked = CookFlattened(flat, options, channels);
            cooked.Id = string.IsNullOrEmpty(timeline.Id) ? timeline.TimelineId : timeline.Id;
            cooked.AuthoringAssetId = timeline.AuthoringAssetId ?? string.Empty;
            return cooked;
        }

        /// <summary>
        /// Cooks an already flattened timeline (the asset gets the timeline id as its id).
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="options">Platform profile settings.</param>
        /// <param name="channels">Resolver for value channels; may be null when there are no value tracks.</param>
        /// <returns>The cooked timeline.</returns>
        public static CookedTimelineAsset CookFlattened(FlattenedTimeline flat, TimelineCookOptions options, ITimelineChannelResolver channels) {
            if (flat == null) {
                throw new ArgumentNullException(nameof(flat));
            } else if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            options.Validate();
            int durationTicks = Math.Max(1, ToTick(flat.DurationSeconds, options.TickRate));
            CookedStringTable strings = new CookedStringTable();
            List<CookedTimelineTrack> tracks = new List<CookedTimelineTrack>();
            for (int index = 0; index < flat.CurveTracks.Count; index++) {
                tracks.Add(CookCurveTrack(flat, flat.CurveTracks[index], options, durationTicks, channels));
            }
            for (int index = 0; index < flat.ActivationTracks.Count; index++) {
                tracks.Add(CookActivation(flat, flat.ActivationTracks[index], options.TickRate, durationTicks));
            }
            for (int index = 0; index < flat.AnimationTracks.Count; index++) {
                tracks.Add(CookAnimation(flat, flat.AnimationTracks[index], options.TickRate, durationTicks));
            }
            if (flat.AudioClips.Count > 0) {
                tracks.Add(CookAudio(flat, options.TickRate, durationTicks));
            }
            if (flat.Events.Count > 0) {
                tracks.Add(CookEvents(flat, strings, options.TickRate, durationTicks));
            }

            string[] slotNames = new string[flat.Slots.Count];
            for (int index = 0; index < slotNames.Length; index++) {
                slotNames[index] = flat.Slots[index].Name;
            }

            return new CookedTimelineAsset {
                Id = flat.TimelineId,
                TickRate = options.TickRate,
                DurationTicks = durationTicks,
                SlotNames = slotNames,
                Strings = strings.ToArray(),
                Tracks = tracks.ToArray()
            };
        }

        /// <summary>
        /// Converts seconds to the nearest tick (exact halves round up).
        /// </summary>
        /// <param name="seconds">Time in seconds (non-negative).</param>
        /// <param name="tickRate">Ticks per second.</param>
        /// <returns>The tick.</returns>
        public static int ToTick(double seconds, int tickRate) {
            return (int)Math.Floor(seconds * tickRate + 0.5 + TickEpsilon);
        }

        /// <summary>
        /// Cooks one transform component or value channel.
        /// </summary>
        /// <param name="flat">Flattened timeline (for slot indices).</param>
        /// <param name="source">Flattened track.</param>
        /// <param name="options">Profile settings.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <param name="channels">Value channel resolver.</param>
        /// <returns>The cooked track.</returns>
        static CookedTimelineTrack CookCurveTrack(FlattenedTimeline flat, FlattenedCurveTrack source, TimelineCookOptions options, int durationTicks, ITimelineChannelResolver channels) {
            CookedTimelineTrack track = new CookedTimelineTrack { SlotIndex = SlotIndex(flat, source.Slot) };
            if (source.Kind == TimelineTrackKind.Transform) {
                track.Kind = CookedTimelineTrackKind.Transform;
                track.ChannelIndex = (int)source.TransformChannel;
                track.Mode = source.Mode == TimelineTransformMode.Offset ? CookedTimelineTransformMode.Offset : CookedTimelineTransformMode.Absolute;
            } else {
                if (channels == null) {
                    throw new InvalidOperationException($"Timeline '{flat.TimelineId}' has value channel '{source.Channel}' on slot '{source.Slot}', but no channel resolver was supplied.");
                }

                TimelineChannelBinding binding = channels.Resolve(source.Slot, source.Channel);
                track.Kind = CookedTimelineTrackKind.Value;
                track.ReceiverId = binding.ReceiverId;
                track.ChannelIndex = binding.ChannelIndex;
            }

            List<CookedTimelineSegment> segments = new List<CookedTimelineSegment>();
            for (int index = 0; index < source.Segments.Count; index++) {
                FlattenedCurveSegment segment = source.Segments[index];
                int start = Clamp(ToTick(segment.StartSeconds, options.TickRate), durationTicks);
                int end = Clamp(ToTick(segment.EndSeconds, options.TickRate), durationTicks);
                AppendSegment(segment, start, end, options, segments);
            }

            track.Segments = DropShadowedSteps(segments).ToArray();
            return track;
        }

        /// <summary>
        /// Appends the cooked form of one flattened segment over its rounded tick span: a step for an empty span, one
        /// segment for a linear piece or (in native mode) a whole curve, otherwise straight pieces within the tolerance.
        /// </summary>
        /// <param name="segment">Flattened segment.</param>
        /// <param name="start">Rounded start tick.</param>
        /// <param name="end">Rounded end tick.</param>
        /// <param name="options">Profile settings.</param>
        /// <param name="output">Cooked segments.</param>
        static void AppendSegment(FlattenedCurveSegment segment, int start, int end, TimelineCookOptions options, List<CookedTimelineSegment> output) {
            if (end <= start) {
                output.Add(Cooked(start, start, segment.EndValue, segment.EndValue, CurveCatalog.LinearCode));
                return;
            }

            byte code = CurveCatalog.GetCode(segment.Curve);
            if (code == CurveCatalog.LinearCode) {
                output.Add(Cooked(start, end, segment.StartValue, segment.EndValue, CurveCatalog.LinearCode));
                return;
            } else if (options.CurveMode == TimelineCurveMode.Native && segment.IsWholeCurve) {
                output.Add(Cooked(start, end, segment.From, segment.To, code));
                return;
            }

            double[] values = new double[end - start + 1];
            for (int tick = 0; tick < values.Length; tick++) {
                double fraction = (double)tick / (end - start);
                values[tick] = segment.ValueAtProgress(segment.ProgressStart + (segment.ProgressEnd - segment.ProgressStart) * fraction);
            }

            List<int> breaks = TimelineCurveLinearizer.LinearizeTicks(values, options.Tolerance);
            for (int index = 0; index + 1 < breaks.Count; index++) {
                output.Add(Cooked(start + breaks[index], start + breaks[index + 1], values[breaks[index]], values[breaks[index + 1]], CurveCatalog.LinearCode));
            }
        }

        /// <summary>
        /// Removes steps that another segment starting on the same tick would override anyway.
        /// </summary>
        /// <param name="segments">Cooked segments in order.</param>
        /// <returns>The segments without shadowed steps.</returns>
        static List<CookedTimelineSegment> DropShadowedSteps(List<CookedTimelineSegment> segments) {
            List<CookedTimelineSegment> output = new List<CookedTimelineSegment>(segments.Count);
            for (int index = 0; index < segments.Count; index++) {
                CookedTimelineSegment segment = segments[index];
                bool isStep = segment.EndTick == segment.StartTick;
                if (isStep && index + 1 < segments.Count && segments[index + 1].StartTick == segment.StartTick) {
                    continue;
                }
                output.Add(segment);
            }
            return output;
        }

        /// <summary>
        /// Cooks the activation of one slot.
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="source">Flattened activation.</param>
        /// <param name="tickRate">Ticks per second.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <returns>The cooked track.</returns>
        static CookedTimelineTrack CookActivation(FlattenedTimeline flat, FlattenedActivationTrack source, int tickRate, int durationTicks) {
            List<CookedTimelineInterval> intervals = new List<CookedTimelineInterval>();
            for (int index = 0; index < source.Intervals.Count; index++) {
                int start = Clamp(ToTick(source.Intervals[index].StartSeconds, tickRate), durationTicks);
                int end = Clamp(ToTick(source.Intervals[index].EndSeconds, tickRate), durationTicks);
                if (end <= start) {
                    continue;
                }

                CookedTimelineInterval last = intervals.Count == 0 ? null : intervals[intervals.Count - 1];
                if (last != null && start <= last.EndTick) {
                    last.EndTick = Math.Max(last.EndTick, end);
                } else {
                    intervals.Add(new CookedTimelineInterval { StartTick = start, EndTick = end });
                }
            }

            return new CookedTimelineTrack { Kind = CookedTimelineTrackKind.Activation, SlotIndex = SlotIndex(flat, source.Slot), Intervals = intervals.ToArray() };
        }

        /// <summary>
        /// Cooks the animation of one slot.
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="source">Flattened animation.</param>
        /// <param name="tickRate">Ticks per second.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <returns>The cooked track.</returns>
        static CookedTimelineTrack CookAnimation(FlattenedTimeline flat, FlattenedAnimationTrack source, int tickRate, int durationTicks) {
            List<CookedTimelineAnimationClip> clips = new List<CookedTimelineAnimationClip>();
            for (int index = 0; index < source.Clips.Count; index++) {
                FlattenedAnimationClip clip = source.Clips[index];
                int start = Clamp(ToTick(clip.StartSeconds, tickRate), durationTicks);
                int end = Clamp(ToTick(clip.EndSeconds, tickRate), durationTicks);
                if (end <= start) {
                    continue;
                }

                clips.Add(new CookedTimelineAnimationClip { StartTick = start, EndTick = end, ClipInTicks = ToTick(clip.ClipInSeconds, tickRate), Speed = (float)clip.Speed, Animation = clip.Animation });
            }

            return new CookedTimelineTrack { Kind = CookedTimelineTrackKind.Animation, SlotIndex = SlotIndex(flat, source.Slot), AnimationClips = clips.ToArray() };
        }

        /// <summary>
        /// Cooks every sound into one audio track (the runtime plays sounds without an emitter, so the slot is dropped).
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="tickRate">Ticks per second.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <returns>The cooked track.</returns>
        static CookedTimelineTrack CookAudio(FlattenedTimeline flat, int tickRate, int durationTicks) {
            List<CookedTimelineAudioClip> clips = new List<CookedTimelineAudioClip>();
            for (int index = 0; index < flat.AudioClips.Count; index++) {
                FlattenedAudioClip clip = flat.AudioClips[index];
                int start = Clamp(ToTick(clip.StartSeconds, tickRate), durationTicks);
                int end = Clamp(ToTick(clip.EndSeconds, tickRate), durationTicks);
                if (end <= start) {
                    continue;
                }

                clips.Add(new CookedTimelineAudioClip { StartTick = start, EndTick = end, ClipInTicks = ToTick(clip.ClipInSeconds, tickRate), Gain = (float)clip.Gain, Audio = clip.Audio });
            }

            return new CookedTimelineTrack { Kind = CookedTimelineTrackKind.Audio, SlotIndex = -1, AudioClips = clips.ToArray() };
        }

        /// <summary>
        /// Cooks every event into one event track with string-table indices.
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="strings">String table being built.</param>
        /// <param name="tickRate">Ticks per second.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <returns>The cooked track.</returns>
        static CookedTimelineTrack CookEvents(FlattenedTimeline flat, CookedStringTable strings, int tickRate, int durationTicks) {
            CookedTimelineMarker[] markers = new CookedTimelineMarker[flat.Events.Count];
            for (int index = 0; index < markers.Length; index++) {
                FlattenedEventMarker marker = flat.Events[index];
                markers[index] = new CookedTimelineMarker {
                    Tick = Clamp(ToTick(marker.TimeSeconds, tickRate), durationTicks),
                    NameIndex = strings.IndexOf(marker.Name),
                    ValueIndex = strings.IndexOf(marker.Value)
                };
            }

            return new CookedTimelineTrack { Kind = CookedTimelineTrackKind.Event, SlotIndex = -1, Markers = markers };
        }

        /// <summary>
        /// Creates a cooked segment, storing values as floats.
        /// </summary>
        /// <param name="start">Start tick.</param>
        /// <param name="end">End tick.</param>
        /// <param name="from">Start value.</param>
        /// <param name="to">End value.</param>
        /// <param name="code">Curve code.</param>
        /// <returns>The segment.</returns>
        static CookedTimelineSegment Cooked(int start, int end, double from, double to, byte code) {
            return new CookedTimelineSegment { StartTick = start, EndTick = end, From = (float)from, To = (float)to, CurveCode = code };
        }

        /// <summary>
        /// Returns the index of a root slot.
        /// </summary>
        /// <param name="flat">Flattened timeline.</param>
        /// <param name="slot">Slot name.</param>
        /// <returns>The index.</returns>
        static int SlotIndex(FlattenedTimeline flat, string slot) {
            int index = flat.IndexOfSlot(slot);
            if (index < 0) {
                throw new InvalidOperationException($"Timeline '{flat.TimelineId}' targets undeclared slot '{slot}'.");
            }
            return index;
        }

        /// <summary>
        /// Clamps a tick to the timeline.
        /// </summary>
        /// <param name="tick">Tick.</param>
        /// <param name="durationTicks">Timeline length in ticks.</param>
        /// <returns>The clamped tick.</returns>
        static int Clamp(int tick, int durationTicks) {
            if (tick < 0) {
                return 0;
            }
            return tick > durationTicks ? durationTicks : tick;
        }
    }
}

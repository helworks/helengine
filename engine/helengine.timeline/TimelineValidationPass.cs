namespace helengine.timeline {
    /// <summary>
    /// One run of <see cref="TimelineValidator"/> over a timeline tree. It owns the diagnostics being collected and the
    /// stacks of timelines and references currently being visited, which is how nesting cycles are detected.
    /// </summary>
    sealed class TimelineValidationPass {
        /// <summary>
        /// Diagnostics collected so far, in document order.
        /// </summary>
        readonly List<TimelineDiagnostic> Diagnostics = new List<TimelineDiagnostic>();

        /// <summary>
        /// Loads referenced timelines; null when references are not followed.
        /// </summary>
        readonly ITimelineAssetResolver Resolver;

        /// <summary>
        /// Keys of the asset references being visited from the root down to the current timeline.
        /// </summary>
        readonly List<string> ReferenceStack = new List<string>();

        /// <summary>
        /// Timelines being visited from the root down to the current one.
        /// </summary>
        readonly List<TimelineAsset> TimelineStack = new List<TimelineAsset>();

        /// <summary>
        /// Creates a validation run.
        /// </summary>
        /// <param name="resolver">Loads referenced timelines; may be null.</param>
        public TimelineValidationPass(ITimelineAssetResolver resolver) {
            Resolver = resolver;
        }

        /// <summary>
        /// Validates a root timeline and its nested timelines.
        /// </summary>
        /// <param name="timeline">Root timeline.</param>
        /// <returns>Every diagnostic found.</returns>
        public IReadOnlyList<TimelineDiagnostic> Run(TimelineAsset timeline) {
            ValidateTimeline(timeline, string.Empty, 0);
            return Diagnostics;
        }

        /// <summary>
        /// Validates one timeline (root, inline or referenced) located at a path.
        /// </summary>
        /// <param name="timeline">Timeline to check.</param>
        /// <param name="path">Path of the timeline object; empty for the root.</param>
        /// <param name="depth">Nesting level; zero for the root.</param>
        void ValidateTimeline(TimelineAsset timeline, string path, int depth) {
            TimelineStack.Add(timeline);
            if (!TimelineNames.IsId(timeline.TimelineId)) {
                Error(TimelinePath.Member(path, "id"), "The id must be 1-64 lowercase letters, digits, '_', '.' or '-', starting with a letter or digit (found '" + timeline.TimelineId + "').");
            }
            if (timeline.Version < 1) {
                Error(TimelinePath.Member(path, "version"), "The version must be 1 or greater.");
            }
            double duration = timeline.DurationSeconds;
            bool durationValid = IsFinite(duration) && duration > 0 && duration <= TimelineValidator.MaxDurationSeconds;
            if (!durationValid) {
                Error(TimelinePath.Member(path, "duration"), "The duration must be greater than 0 and at most " + TimelinePath.Number(TimelineValidator.MaxDurationSeconds) + " seconds (found " + TimelinePath.Number(duration) + ").");
            }
            ValidateSlots(timeline, path);
            ValidateCues(timeline, path, durationValid);
            if (timeline.Tracks == null) {
                Error(TimelinePath.Member(path, "tracks"), "The track list is missing.");
            } else {
                for (int index = 0; index < timeline.Tracks.Count; index++) {
                    ValidateTrack(timeline, timeline.Tracks[index], TimelinePath.Index(TimelinePath.Member(path, "tracks"), index), depth, durationValid);
                }
            }
            TimelineStack.RemoveAt(TimelineStack.Count - 1);
        }

        /// <summary>
        /// Checks slot names (identifiers, unique) and kinds.
        /// </summary>
        /// <param name="timeline">Timeline declaring the slots.</param>
        /// <param name="path">Path of the timeline object.</param>
        void ValidateSlots(TimelineAsset timeline, string path) {
            string slotsPath = TimelinePath.Member(path, "slots");
            if (timeline.Slots == null) {
                Error(slotsPath, "The slot list is missing.");
                return;
            }
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < timeline.Slots.Count; index++) {
                TimelineSlotAsset slot = timeline.Slots[index];
                string slotPath = TimelinePath.Index(slotsPath, index);
                if (slot == null) {
                    Error(slotPath, "A slot cannot be null.");
                    continue;
                }
                if (!TimelineNames.IsIdentifier(slot.Name)) {
                    Error(TimelinePath.Member(slotPath, "name"), IdentifierRule("slot name", slot.Name));
                } else if (!names.Add(slot.Name)) {
                    Error(TimelinePath.Member(slotPath, "name"), "The slot name '" + slot.Name + "' is declared twice.");
                }
                if (!Enum.IsDefined(slot.Kind)) {
                    Error(TimelinePath.Member(slotPath, "kind"), "The slot kind must be entity, text, media or rect.");
                }
            }
        }

        /// <summary>
        /// Checks cue names (identifiers, unique) and times (inside the timeline).
        /// </summary>
        /// <param name="timeline">Timeline declaring the cues.</param>
        /// <param name="path">Path of the timeline object.</param>
        /// <param name="durationValid">Whether the timeline duration can be used as a bound.</param>
        void ValidateCues(TimelineAsset timeline, string path, bool durationValid) {
            string cuesPath = TimelinePath.Member(path, "cues");
            if (timeline.Cues == null) {
                Error(cuesPath, "The cue list is missing.");
                return;
            }
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < timeline.Cues.Count; index++) {
                TimelineCueAsset cue = timeline.Cues[index];
                string cuePath = TimelinePath.Index(cuesPath, index);
                if (cue == null) {
                    Error(cuePath, "A cue cannot be null.");
                    continue;
                }
                if (!TimelineNames.IsIdentifier(cue.Name)) {
                    Error(TimelinePath.Member(cuePath, "name"), IdentifierRule("cue name", cue.Name));
                } else if (!names.Add(cue.Name)) {
                    Error(TimelinePath.Member(cuePath, "name"), "The cue name '" + cue.Name + "' is declared twice.");
                }
                if (!IsFinite(cue.TimeSeconds) || cue.TimeSeconds < 0 || (durationValid && cue.TimeSeconds > timeline.DurationSeconds + TimelineValidator.TimeTolerance)) {
                    Error(TimelinePath.Member(cuePath, "time"), "The cue time must lie between 0 and the duration (" + TimelinePath.Number(timeline.DurationSeconds) + "); found " + TimelinePath.Number(cue.TimeSeconds) + ".");
                }
            }
        }

        /// <summary>
        /// Validates one track: its slot, the timing of its clips and the kind-specific content.
        /// </summary>
        /// <param name="timeline">Timeline holding the track.</param>
        /// <param name="track">Track to check.</param>
        /// <param name="path">Path of the track object.</param>
        /// <param name="depth">Nesting level of the timeline.</param>
        /// <param name="durationValid">Whether the timeline duration can be used as a bound.</param>
        void ValidateTrack(TimelineAsset timeline, TimelineTrackAsset track, string path, int depth, bool durationValid) {
            if (track == null) {
                Error(path, "A track cannot be null.");
                return;
            }
            ValidateTrackSlot(timeline, track, path);
            if (track is TimelineEventTrackAsset eventTrack) {
                ValidateMarkers(timeline, eventTrack, path, durationValid);
                return;
            }
            if (!ValidateClipTiming(timeline, track, path, durationValid)) {
                return;
            }
            string clipsPath = TimelinePath.Member(path, "clips");
            if (track is TimelineTransformTrackAsset transformTrack) {
                if (!Enum.IsDefined(transformTrack.Mode)) {
                    Error(TimelinePath.Member(path, "mode"), "The transform mode must be absolute or offset.");
                }
                for (int index = 0; index < transformTrack.Clips.Count; index++) {
                    ValidateTransformClip(transformTrack.Clips[index], TimelinePath.Index(clipsPath, index));
                }
            } else if (track is TimelineValueTrackAsset valueTrack) {
                if (!TimelineNames.IsIdentifier(valueTrack.Channel)) {
                    Error(TimelinePath.Member(path, "channel"), IdentifierRule("channel name", valueTrack.Channel));
                }
                for (int index = 0; index < valueTrack.Clips.Count; index++) {
                    ValidateValueClip(valueTrack.Clips[index], valueTrack.Channel, TimelinePath.Index(clipsPath, index));
                }
            } else if (track is TimelineActivationTrackAsset activationTrack) {
                for (int index = 0; index < activationTrack.Clips.Count; index++) {
                    ValidateActivationClip(activationTrack.Clips[index], TimelinePath.Index(clipsPath, index));
                }
            } else if (track is TimelineAudioTrackAsset audioTrack) {
                for (int index = 0; index < audioTrack.Clips.Count; index++) {
                    ValidateAudioClip(audioTrack.Clips[index], TimelinePath.Index(clipsPath, index));
                }
            } else if (track is TimelineAnimationTrackAsset animationTrack) {
                for (int index = 0; index < animationTrack.Clips.Count; index++) {
                    ValidateAnimationClip(animationTrack.Clips[index], TimelinePath.Index(clipsPath, index));
                }
            } else if (track is TimelineNestedTrackAsset nestedTrack) {
                for (int index = 0; index < nestedTrack.Clips.Count; index++) {
                    ValidateNestedClip(timeline, nestedTrack.Clips[index], TimelinePath.Index(clipsPath, index), depth);
                }
            } else {
                Error(TimelinePath.Member(path, "kind"), "Unknown track kind '" + track.Kind + "'.");
            }
        }

        /// <summary>
        /// Checks that the track's slot exists and that its kind suits the track kind.
        /// </summary>
        /// <param name="timeline">Timeline declaring the slots.</param>
        /// <param name="track">Track to check.</param>
        /// <param name="path">Path of the track object.</param>
        void ValidateTrackSlot(TimelineAsset timeline, TimelineTrackAsset track, string path) {
            string slotPath = TimelinePath.Member(path, "slot");
            bool empty = string.IsNullOrEmpty(track.Slot);
            if (track.Kind == TimelineTrackKind.Event || track.Kind == TimelineTrackKind.Timeline) {
                if (!empty) {
                    string reason = track.Kind == TimelineTrackKind.Event
                        ? "Event tracks have no slot."
                        : "Nested timeline tracks have no slot; map slots on each clip with 'slots'.";
                    Error(slotPath, reason);
                }
                return;
            }
            if (empty) {
                if (track.Kind != TimelineTrackKind.Audio) {
                    Error(slotPath, KindName(track.Kind) + " tracks need a slot.");
                }
                return;
            }
            TimelineSlotAsset slot = timeline.Slots == null ? null : timeline.FindSlot(track.Slot);
            if (slot == null) {
                Error(slotPath, "Unknown slot '" + track.Slot + "'; declared slots: " + ListSlots(timeline) + ".");
                return;
            }
            if ((track.Kind == TimelineTrackKind.Animation || track.Kind == TimelineTrackKind.Audio) && slot.Kind != TimelineSlotKind.Entity) {
                Error(slotPath, KindName(track.Kind) + " tracks need an entity slot, but '" + slot.Name + "' is a " + SlotKindName(slot.Kind) + " slot.");
            }
        }

        /// <summary>
        /// Checks the timing shared by every clip kind: start (or cue + offset), duration, clip-in, blend ramps, the
        /// timeline bounds, start order and overlaps (allowed only inside the blend ramps of two neighbouring clips).
        /// </summary>
        /// <param name="timeline">Timeline holding the track.</param>
        /// <param name="track">Track whose clips are checked.</param>
        /// <param name="path">Path of the track object.</param>
        /// <param name="durationValid">Whether the timeline duration can be used as a bound.</param>
        /// <returns>False when the clip list itself is missing or holds null clips, so kind checks must be skipped.</returns>
        bool ValidateClipTiming(TimelineAsset timeline, TimelineTrackAsset track, string path, bool durationValid) {
            string clipsPath = TimelinePath.Member(path, "clips");
            IReadOnlyList<TimelineClipAsset> clips = track.ClipView;
            if (clips == null) {
                Error(clipsPath, "The clip list is missing.");
                return false;
            }
            bool allPresent = true;
            double[] starts = new double[clips.Count];
            double[] ends = new double[clips.Count];
            bool[] known = new bool[clips.Count];
            for (int index = 0; index < clips.Count; index++) {
                TimelineClipAsset clip = clips[index];
                string clipPath = TimelinePath.Index(clipsPath, index);
                if (clip == null) {
                    Error(clipPath, "A clip cannot be null.");
                    allPresent = false;
                    continue;
                }
                known[index] = ValidateClip(timeline, clip, clipPath, durationValid, out starts[index], out ends[index]);
                if (!known[index] || index == 0 || !known[index - 1]) {
                    continue;
                }
                if (starts[index] < starts[index - 1] - TimelineValidator.TimeTolerance) {
                    Error(TimelinePath.Member(clipPath, "start"), "Clips must be listed in start order: this clip starts at " + TimelinePath.Number(starts[index]) + ", before clips[" + (index - 1) + "] at " + TimelinePath.Number(starts[index - 1]) + ".");
                    continue;
                }
                ValidateOverlaps(clips, index, clipPath, starts, ends, known);
            }
            return allPresent;
        }

        /// <summary>
        /// Checks one clip's own timing fields and resolves its start and end in timeline seconds.
        /// </summary>
        /// <param name="timeline">Timeline holding the clip.</param>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        /// <param name="durationValid">Whether the timeline duration can be used as a bound.</param>
        /// <param name="start">Resolved start when the timing is usable.</param>
        /// <param name="end">Resolved end when the timing is usable.</param>
        /// <returns>True when start and end could be resolved for order and overlap checks.</returns>
        bool ValidateClip(TimelineAsset timeline, TimelineClipAsset clip, string path, bool durationValid, out double start, out double end) {
            bool usable = true;
            double cueTime = 0;
            start = 0;
            end = 0;
            if (!string.IsNullOrEmpty(clip.Cue) && !timeline.TryGetCueTime(clip.Cue, out cueTime)) {
                Error(TimelinePath.Member(TimelinePath.Member(path, "start"), "cue"), "Unknown cue '" + clip.Cue + "'; declared cues: " + ListCues(timeline) + ".");
                usable = false;
            }
            if (!IsFinite(clip.StartSeconds)) {
                Error(TimelinePath.Member(path, "start"), "The start must be a finite number of seconds.");
                usable = false;
            }
            if (!IsFinite(clip.DurationSeconds) || clip.DurationSeconds <= 0) {
                Error(TimelinePath.Member(path, "duration"), "The clip duration must be greater than 0 (found " + TimelinePath.Number(clip.DurationSeconds) + ").");
                usable = false;
            }
            if (!IsFinite(clip.ClipInSeconds) || clip.ClipInSeconds < 0) {
                Error(TimelinePath.Member(path, "clip_in"), "clip_in must be 0 or greater (found " + TimelinePath.Number(clip.ClipInSeconds) + ").");
            }
            bool rampsValid = true;
            if (!IsFinite(clip.EaseInSeconds) || clip.EaseInSeconds < 0) {
                Error(TimelinePath.Member(path, "ease_in"), "ease_in must be 0 or greater (found " + TimelinePath.Number(clip.EaseInSeconds) + ").");
                rampsValid = false;
            }
            if (!IsFinite(clip.EaseOutSeconds) || clip.EaseOutSeconds < 0) {
                Error(TimelinePath.Member(path, "ease_out"), "ease_out must be 0 or greater (found " + TimelinePath.Number(clip.EaseOutSeconds) + ").");
                rampsValid = false;
            }
            if (rampsValid && IsFinite(clip.DurationSeconds) && clip.EaseInSeconds + clip.EaseOutSeconds > clip.DurationSeconds + TimelineValidator.TimeTolerance) {
                Error(TimelinePath.Member(path, "ease_out"), "The blend ramps (ease_in + ease_out = " + TimelinePath.Number(clip.EaseInSeconds + clip.EaseOutSeconds) + ") cannot be longer than the clip (" + TimelinePath.Number(clip.DurationSeconds) + ").");
            }
            if (!usable) {
                return false;
            }
            start = clip.ResolveStart(cueTime);
            end = start + clip.DurationSeconds;
            if (!durationValid) {
                return true;
            }
            if (start < -TimelineValidator.TimeTolerance) {
                Error(TimelinePath.Member(path, "start"), "The clip starts at " + TimelinePath.Number(start) + ", before the timeline starts.");
            }
            if (end > timeline.DurationSeconds + TimelineValidator.TimeTolerance) {
                Error(TimelinePath.Member(path, "duration"), "The clip ends at " + TimelinePath.Number(end) + ", after the timeline duration (" + TimelinePath.Number(timeline.DurationSeconds) + ").");
            }
            return true;
        }

        /// <summary>
        /// Reports overlaps of one clip with earlier clips: only the previous clip may overlap it, and only inside the
        /// previous clip's ease-out ramp and this clip's ease-in ramp.
        /// </summary>
        /// <param name="clips">Clips of the track.</param>
        /// <param name="index">Index of the clip being checked.</param>
        /// <param name="path">Path of the clip being checked.</param>
        /// <param name="starts">Resolved starts of the clips.</param>
        /// <param name="ends">Resolved ends of the clips.</param>
        /// <param name="known">Which clips have resolved timing.</param>
        void ValidateOverlaps(IReadOnlyList<TimelineClipAsset> clips, int index, string path, double[] starts, double[] ends, bool[] known) {
            for (int earlier = 0; earlier < index - 1; earlier++) {
                if (known[earlier] && ends[earlier] - starts[index] > TimelineValidator.TimeTolerance) {
                    Error(TimelinePath.Member(path, "start"), "The clip overlaps clips[" + earlier + "] as well as clips[" + (index - 1) + "]; at most two clips may overlap on one track.");
                    return;
                }
            }
            double overlap = ends[index - 1] - starts[index];
            if (overlap <= TimelineValidator.TimeTolerance) {
                return;
            }
            TimelineClipAsset previous = clips[index - 1];
            TimelineClipAsset clip = clips[index];
            if (overlap > previous.EaseOutSeconds + TimelineValidator.TimeTolerance || overlap > clip.EaseInSeconds + TimelineValidator.TimeTolerance) {
                Error(TimelinePath.Member(path, "start"), "The clip overlaps clips[" + (index - 1) + "] by " + TimelinePath.Number(overlap) + " s; clips may only overlap inside the blend ramps (clips[" + (index - 1) + "].ease_out = " + TimelinePath.Number(previous.EaseOutSeconds) + ", this ease_in = " + TimelinePath.Number(clip.EaseInSeconds) + ").");
            }
        }

        /// <summary>
        /// Checks a transform clip: no clip-in and at least one keyframe across its position, rotation and scale channels.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateTransformClip(TimelineTransformClipAsset clip, string path) {
            RejectClipIn(clip, path, "transform");
            if (clip.Position == null || clip.Rotation == null || clip.Scale == null) {
                Error(path, "A transform clip needs position, rotation and scale lists (empty lists are allowed).");
                return;
            }
            if (clip.Position.Count + clip.Rotation.Count + clip.Scale.Count == 0) {
                Error(path, "A transform clip needs at least one keyframe in position, rotation or scale.");
            }
            ValidateVectorKeyframes(clip.Position, TimelinePath.Member(path, "position"), clip.DurationSeconds);
            ValidateVectorKeyframes(clip.Rotation, TimelinePath.Member(path, "rotation"), clip.DurationSeconds);
            ValidateVectorKeyframes(clip.Scale, TimelinePath.Member(path, "scale"), clip.DurationSeconds);
        }

        /// <summary>
        /// Checks a value clip: no clip-in, at least one keyframe, and values inside the range of known channels.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="channel">Channel the track animates.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateValueClip(TimelineValueClipAsset clip, string channel, string path) {
            RejectClipIn(clip, path, "value");
            string keyframesPath = TimelinePath.Member(path, "keyframes");
            if (clip.Keyframes == null || clip.Keyframes.Count == 0) {
                Error(keyframesPath, "A value clip needs at least one keyframe.");
                return;
            }
            double minimum;
            double maximum;
            bool ranged = TimelineKnownChannels.TryGetRange(channel, out minimum, out maximum);
            double previous = 0;
            for (int index = 0; index < clip.Keyframes.Count; index++) {
                TimelineKeyframeAsset keyframe = clip.Keyframes[index];
                string keyframePath = TimelinePath.Index(keyframesPath, index);
                if (keyframe == null) {
                    Error(keyframePath, "A keyframe cannot be null.");
                    continue;
                }
                ValidateKeyframeTime(keyframe.TimeSeconds, index > 0, previous, clip.DurationSeconds, keyframePath);
                previous = keyframe.TimeSeconds;
                ValidateCurve(keyframe.Curve, keyframePath);
                if (!IsFinite(keyframe.Value)) {
                    Error(TimelinePath.Member(keyframePath, "value"), "The value must be a finite number.");
                } else if (ranged && (keyframe.Value < minimum || keyframe.Value > maximum)) {
                    Error(TimelinePath.Member(keyframePath, "value"), "The '" + channel + "' channel takes values from " + TimelinePath.Number(minimum) + " to " + TimelinePath.Number(maximum) + " (found " + TimelinePath.Number(keyframe.Value) + ").");
                }
            }
        }

        /// <summary>
        /// Checks an activation interval: no clip-in and no blend ramps, since activation is on or off.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateActivationClip(TimelineClipAsset clip, string path) {
            RejectClipIn(clip, path, "activation");
            if (clip.EaseInSeconds != 0 || clip.EaseOutSeconds != 0) {
                Error(path, "Activation clips cannot blend; ease_in and ease_out must be 0.");
            }
        }

        /// <summary>
        /// Checks an audio clip: an audio reference and a gain between 0 and <see cref="TimelineValidator.MaxGain"/>.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateAudioClip(TimelineAudioClipAsset clip, string path) {
            if (clip.Audio == null) {
                Error(TimelinePath.Member(path, "audio"), "Audio clips need an audio asset reference.");
            }
            if (!IsFinite(clip.Gain) || clip.Gain < 0 || clip.Gain > TimelineValidator.MaxGain) {
                Error(TimelinePath.Member(path, "gain"), "The gain must be between 0 and " + TimelinePath.Number(TimelineValidator.MaxGain) + " (found " + TimelinePath.Number(clip.Gain) + ").");
            }
        }

        /// <summary>
        /// Checks an animation clip placement: an animation reference and a valid speed.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateAnimationClip(TimelineAnimationClipAsset clip, string path) {
            if (clip.Animation == null) {
                Error(TimelinePath.Member(path, "animation"), "Animation clips need an animation clip asset reference.");
            }
            ValidateSpeed(clip.Speed, path);
        }

        /// <summary>
        /// Checks a nested timeline clip: exactly one source, speed, nesting depth, cycles, the slot mapping, and the nested
        /// timeline itself (inline definitions always; references when a resolver can load them).
        /// </summary>
        /// <param name="timeline">Timeline holding the clip.</param>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        /// <param name="depth">Nesting level of the timeline holding the clip.</param>
        void ValidateNestedClip(TimelineAsset timeline, TimelineNestedClipAsset clip, string path, int depth) {
            ValidateSpeed(clip.Speed, path);
            if (clip.Timeline != null && clip.Definition != null) {
                Error(path, "A nested timeline clip takes either 'timeline' (a reference) or 'definition' (inline), not both.");
                return;
            }
            if (clip.Timeline == null && clip.Definition == null) {
                Error(path, "A nested timeline clip needs 'timeline' (a reference) or 'definition' (inline).");
                return;
            }
            if (depth + 1 > TimelineValidator.MaxNestingDepth) {
                Error(path, "Nested timelines may be at most " + TimelineValidator.MaxNestingDepth + " levels deep.");
                return;
            }
            TimelineAsset inner = clip.Definition;
            string innerPath = TimelinePath.Member(path, "definition");
            string referenceKey = string.Empty;
            if (clip.Timeline != null) {
                innerPath = TimelinePath.Member(path, "timeline");
                referenceKey = ReferenceKey(clip.Timeline);
                if (ReferenceStack.Contains(referenceKey)) {
                    Error(innerPath, "The timeline '" + clip.Timeline.RelativePath + "' contains itself through nesting.");
                    return;
                }
                inner = Resolver == null ? null : Resolver.Resolve(clip.Timeline);
                if (Resolver != null && inner == null) {
                    Error(innerPath, "The timeline '" + clip.Timeline.RelativePath + "' cannot be found.");
                }
            }
            if (inner != null && TimelineStack.Contains(inner)) {
                Error(innerPath, "The nested timeline '" + inner.TimelineId + "' contains itself through nesting.");
                return;
            }
            ValidateSlotMappings(timeline, inner, clip, path);
            if (inner == null) {
                return;
            }
            if (referenceKey.Length > 0) {
                ReferenceStack.Add(referenceKey);
            }
            ValidateTimeline(inner, innerPath, depth + 1);
            if (referenceKey.Length > 0) {
                ReferenceStack.RemoveAt(ReferenceStack.Count - 1);
            }
        }

        /// <summary>
        /// Checks a nested clip's slot mapping: unique inner names, outer slots that exist, identical slot kinds, and (when
        /// the nested timeline is known) that every inner slot is mapped and every mapped name exists inside.
        /// </summary>
        /// <param name="outer">Timeline holding the clip.</param>
        /// <param name="inner">Nested timeline; null when a reference was not followed.</param>
        /// <param name="clip">Clip whose mapping is checked.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateSlotMappings(TimelineAsset outer, TimelineAsset inner, TimelineNestedClipAsset clip, string path) {
            string slotsPath = TimelinePath.Member(path, "slots");
            if (clip.SlotMappings == null) {
                Error(slotsPath, "The slot mapping is missing.");
                return;
            }
            HashSet<string> mapped = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < clip.SlotMappings.Count; index++) {
                TimelineSlotMappingAsset mapping = clip.SlotMappings[index];
                if (mapping == null || string.IsNullOrEmpty(mapping.Inner)) {
                    Error(slotsPath, "Every slot mapping needs an inner slot name.");
                    continue;
                }
                string mappingPath = TimelinePath.Member(slotsPath, mapping.Inner);
                if (!mapped.Add(mapping.Inner)) {
                    Error(mappingPath, "The inner slot '" + mapping.Inner + "' is mapped twice.");
                    continue;
                }
                TimelineSlotAsset outerSlot = outer.Slots == null ? null : outer.FindSlot(mapping.Outer);
                if (outerSlot == null) {
                    Error(mappingPath, "Unknown slot '" + mapping.Outer + "'; declared slots: " + ListSlots(outer) + ".");
                }
                if (inner == null || inner.Slots == null) {
                    continue;
                }
                TimelineSlotAsset innerSlot = inner.FindSlot(mapping.Inner);
                if (innerSlot == null) {
                    Error(mappingPath, "The nested timeline has no slot '" + mapping.Inner + "'; its slots: " + ListSlots(inner) + ".");
                } else if (outerSlot != null && innerSlot.Kind != outerSlot.Kind) {
                    Error(mappingPath, "Slot kinds differ: the nested slot '" + innerSlot.Name + "' is " + SlotKindName(innerSlot.Kind) + " but '" + outerSlot.Name + "' is " + SlotKindName(outerSlot.Kind) + ".");
                }
            }
            if (inner == null || inner.Slots == null) {
                return;
            }
            for (int index = 0; index < inner.Slots.Count; index++) {
                TimelineSlotAsset innerSlot = inner.Slots[index];
                if (innerSlot != null && !mapped.Contains(innerSlot.Name)) {
                    Error(slotsPath, "The nested slot '" + innerSlot.Name + "' is not mapped to a slot of this timeline.");
                }
            }
        }

        /// <summary>
        /// Checks event markers: names, cue anchors, times inside the timeline and time order.
        /// </summary>
        /// <param name="timeline">Timeline holding the track.</param>
        /// <param name="track">Event track to check.</param>
        /// <param name="path">Path of the track object.</param>
        /// <param name="durationValid">Whether the timeline duration can be used as a bound.</param>
        void ValidateMarkers(TimelineAsset timeline, TimelineEventTrackAsset track, string path, bool durationValid) {
            string markersPath = TimelinePath.Member(path, "markers");
            if (track.Markers == null) {
                Error(markersPath, "The marker list is missing.");
                return;
            }
            bool hasPrevious = false;
            double previous = 0;
            for (int index = 0; index < track.Markers.Count; index++) {
                TimelineEventMarkerAsset marker = track.Markers[index];
                string markerPath = TimelinePath.Index(markersPath, index);
                if (marker == null) {
                    Error(markerPath, "A marker cannot be null.");
                    continue;
                }
                if (!TimelineNames.IsId(marker.Name)) {
                    Error(TimelinePath.Member(markerPath, "name"), "The event name must be 1-64 lowercase letters, digits, '_', '.' or '-' (found '" + marker.Name + "').");
                }
                double cueTime = 0;
                if (!string.IsNullOrEmpty(marker.Cue) && !timeline.TryGetCueTime(marker.Cue, out cueTime)) {
                    Error(TimelinePath.Member(TimelinePath.Member(markerPath, "time"), "cue"), "Unknown cue '" + marker.Cue + "'; declared cues: " + ListCues(timeline) + ".");
                    continue;
                }
                if (!IsFinite(marker.TimeSeconds)) {
                    Error(TimelinePath.Member(markerPath, "time"), "The marker time must be a finite number of seconds.");
                    continue;
                }
                double time = marker.ResolveTime(cueTime);
                if (time < -TimelineValidator.TimeTolerance || (durationValid && time > timeline.DurationSeconds + TimelineValidator.TimeTolerance)) {
                    Error(TimelinePath.Member(markerPath, "time"), "The marker falls at " + TimelinePath.Number(time) + ", outside the timeline (0 to " + TimelinePath.Number(timeline.DurationSeconds) + ").");
                }
                if (hasPrevious && time < previous - TimelineValidator.TimeTolerance) {
                    Error(TimelinePath.Member(markerPath, "time"), "Markers must be listed in time order: this marker falls at " + TimelinePath.Number(time) + ", before the previous one at " + TimelinePath.Number(previous) + ".");
                }
                hasPrevious = true;
                previous = time;
            }
        }

        /// <summary>
        /// Checks one list of vector keyframes: present entries, increasing times inside the clip, finite components and
        /// catalog curves.
        /// </summary>
        /// <param name="keyframes">Keyframes to check.</param>
        /// <param name="path">Path of the keyframe list.</param>
        /// <param name="clipDuration">Clip duration bounding keyframe times.</param>
        void ValidateVectorKeyframes(List<TimelineVectorKeyframeAsset> keyframes, string path, double clipDuration) {
            double previous = 0;
            for (int index = 0; index < keyframes.Count; index++) {
                TimelineVectorKeyframeAsset keyframe = keyframes[index];
                string keyframePath = TimelinePath.Index(path, index);
                if (keyframe == null) {
                    Error(keyframePath, "A keyframe cannot be null.");
                    continue;
                }
                ValidateKeyframeTime(keyframe.TimeSeconds, index > 0, previous, clipDuration, keyframePath);
                previous = keyframe.TimeSeconds;
                ValidateCurve(keyframe.Curve, keyframePath);
                if (!IsFinite(keyframe.X) || !IsFinite(keyframe.Y) || !IsFinite(keyframe.Z)) {
                    Error(TimelinePath.Member(keyframePath, "value"), "Every component of the value must be a finite number.");
                }
            }
        }

        /// <summary>
        /// Checks one keyframe time: finite, inside the clip, and strictly after the previous keyframe.
        /// </summary>
        /// <param name="time">Keyframe time in clip seconds.</param>
        /// <param name="hasPrevious">Whether a previous keyframe exists.</param>
        /// <param name="previous">Previous keyframe time.</param>
        /// <param name="clipDuration">Clip duration bounding the time.</param>
        /// <param name="path">Path of the keyframe object.</param>
        void ValidateKeyframeTime(double time, bool hasPrevious, double previous, double clipDuration, string path) {
            string timePath = TimelinePath.Member(path, "time");
            if (!IsFinite(time) || time < 0 || (IsFinite(clipDuration) && clipDuration > 0 && time > clipDuration + TimelineValidator.TimeTolerance)) {
                Error(timePath, "Keyframe times are in clip time and must lie between 0 and the clip duration (" + TimelinePath.Number(clipDuration) + "); found " + TimelinePath.Number(time) + ".");
            } else if (hasPrevious && time <= previous) {
                Error(timePath, "Keyframe times must increase: " + TimelinePath.Number(time) + " does not come after " + TimelinePath.Number(previous) + ".");
            }
        }

        /// <summary>
        /// Checks that a keyframe names a curve of the catalog.
        /// </summary>
        /// <param name="curve">Curve id.</param>
        /// <param name="path">Path of the keyframe object.</param>
        void ValidateCurve(string curve, string path) {
            if (!CurveCatalog.Supports(curve)) {
                Error(TimelinePath.Member(path, "curve"), "Unknown curve '" + curve + "'; use one of " + ListCurves() + ".");
            }
        }

        /// <summary>
        /// Checks a speed multiplier: greater than 0 and at most <see cref="TimelineValidator.MaxSpeed"/>.
        /// </summary>
        /// <param name="speed">Speed to check.</param>
        /// <param name="path">Path of the clip object.</param>
        void ValidateSpeed(double speed, string path) {
            if (!IsFinite(speed) || speed <= 0 || speed > TimelineValidator.MaxSpeed) {
                Error(TimelinePath.Member(path, "speed"), "The speed must be greater than 0 and at most " + TimelinePath.Number(TimelineValidator.MaxSpeed) + " (found " + TimelinePath.Number(speed) + ").");
            }
        }

        /// <summary>
        /// Reports a clip-in on a clip kind that has no source to skip into.
        /// </summary>
        /// <param name="clip">Clip to check.</param>
        /// <param name="path">Path of the clip object.</param>
        /// <param name="kind">Clip kind name used in the message.</param>
        void RejectClipIn(TimelineClipAsset clip, string path, string kind) {
            if (clip.ClipInSeconds != 0) {
                Error(TimelinePath.Member(path, "clip_in"), "clip_in only applies to audio, animation and nested timeline clips; " + kind + " clips must leave it at 0.");
            }
        }

        /// <summary>
        /// Records one diagnostic.
        /// </summary>
        /// <param name="path">Location of the problem.</param>
        /// <param name="message">Readable explanation.</param>
        void Error(string path, string message) {
            Diagnostics.Add(new TimelineDiagnostic(path, message));
        }

        /// <summary>
        /// Builds the message for a name that breaks the identifier rule.
        /// </summary>
        /// <param name="what">What the name names, such as <c>slot name</c>.</param>
        /// <param name="name">The offending name.</param>
        /// <returns>The message.</returns>
        static string IdentifierRule(string what, string name) {
            return "The " + what + " must be 1-64 lowercase letters, digits or '_', starting with a letter (found '" + name + "').";
        }

        /// <summary>
        /// Lists the declared slot names for messages.
        /// </summary>
        /// <param name="timeline">Timeline declaring the slots.</param>
        /// <returns>Comma-separated names, or <c>none</c>.</returns>
        static string ListSlots(TimelineAsset timeline) {
            List<string> names = new List<string>();
            if (timeline.Slots != null) {
                for (int index = 0; index < timeline.Slots.Count; index++) {
                    if (timeline.Slots[index] != null) {
                        names.Add(timeline.Slots[index].Name);
                    }
                }
            }
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        /// <summary>
        /// Lists the declared cue names for messages.
        /// </summary>
        /// <param name="timeline">Timeline declaring the cues.</param>
        /// <returns>Comma-separated names, or <c>none</c>.</returns>
        static string ListCues(TimelineAsset timeline) {
            List<string> names = new List<string>();
            if (timeline.Cues != null) {
                for (int index = 0; index < timeline.Cues.Count; index++) {
                    if (timeline.Cues[index] != null) {
                        names.Add(timeline.Cues[index].Name);
                    }
                }
            }
            return names.Count == 0 ? "none" : string.Join(", ", names);
        }

        /// <summary>
        /// Lists the catalog curve ids for messages.
        /// </summary>
        /// <returns>Comma-separated curve ids.</returns>
        static string ListCurves() {
            List<string> ids = new List<string>(CurveCatalog.Count);
            for (int code = 0; code < CurveCatalog.Count; code++) {
                ids.Add(CurveCatalog.GetId((byte)code));
            }
            return string.Join(", ", ids);
        }

        /// <summary>
        /// Builds the identity key of an asset reference used for cycle detection.
        /// </summary>
        /// <param name="reference">Reference to key.</param>
        /// <returns>A key that is equal for references to the same asset.</returns>
        static string ReferenceKey(SceneAssetReference reference) {
            return (int)reference.SourceKind + "|" + reference.ProviderId + "|" + reference.AssetId + "|" + reference.RelativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Returns the capitalized JSON name of a track kind for messages.
        /// </summary>
        /// <param name="kind">Track kind.</param>
        /// <returns>Name such as <c>Animation</c>.</returns>
        static string KindName(TimelineTrackKind kind) {
            return kind.ToString();
        }

        /// <summary>
        /// Returns the JSON name of a slot kind for messages.
        /// </summary>
        /// <param name="kind">Slot kind.</param>
        /// <returns>Name such as <c>text</c>.</returns>
        static string SlotKindName(TimelineSlotKind kind) {
            return kind.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Checks that a number is neither NaN nor infinite.
        /// </summary>
        /// <param name="value">Number to check.</param>
        /// <returns>True for finite numbers.</returns>
        static bool IsFinite(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}

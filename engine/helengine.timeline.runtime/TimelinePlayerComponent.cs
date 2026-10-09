namespace helengine.timeline.runtime {
    /// <summary>
    /// Plays a <see cref="CookedTimelineAsset"/> in a scene. Each slot of the timeline is bound to a scene entity through
    /// <see cref="Slots"/>; receivers, animators, audio assets and event listeners are resolved once when the player
    /// binds (on the first <see cref="Play"/>, <see cref="Seek"/> or <see cref="Evaluate"/>), so the per-frame path does
    /// no lookups, string work or allocation: it advances the time, moves one forward-only cursor per track and applies
    /// transform, value, activation and animation tracks. Events fire, and sounds start, only while playing, when playback
    /// crosses their tick.
    /// <para>
    /// Rules that keep playback deterministic: the applied state depends only on the tick (<see cref="Evaluate"/>); a
    /// transform or value track holds the end value of its last finished segment until its next segment starts and does
    /// nothing before its first segment (transform groups a timeline drives then show the transform captured at bind);
    /// at the final tick activation reads the last frame, so slots active until the end stay visible when holding.
    /// Seeking never fires the events of the skipped span (a marker exactly at the seek target does not fire either) and
    /// starts sounds that span the target with a start offset when the audio backend supports it, skipping them otherwise.
    /// </para>
    /// </summary>
    public class TimelinePlayerComponent : UpdateComponent {
        /// <summary>
        /// Tolerance added before flooring time to ticks so exact multiples of the tick length are not lost to rounding.
        /// </summary>
        const double TickEpsilon = 1e-9;

        /// <summary>
        /// Backing field of <see cref="Timeline"/>.
        /// </summary>
        CookedTimelineAsset TimelineValue;

        /// <summary>
        /// Whether the player loaded <see cref="TimelineValue"/> from <see cref="TimelinePath"/> and must release it.
        /// </summary>
        bool OwnsTimeline;

        /// <summary>
        /// Backing field of <see cref="TimelinePath"/>.
        /// </summary>
        string TimelinePathValue = string.Empty;

        /// <summary>
        /// Backing field of <see cref="Slots"/>.
        /// </summary>
        SceneEntityReference[] SlotsValue = Array.Empty<SceneEntityReference>();

        /// <summary>
        /// Backing field of <see cref="PlayOnStart"/>.
        /// </summary>
        bool PlayOnStartValue;

        /// <summary>
        /// Backing field of <see cref="EndMode"/>.
        /// </summary>
        TimelineEndMode EndModeValue = TimelineEndMode.Stop;

        /// <summary>
        /// Backing field of <see cref="Speed"/>.
        /// </summary>
        float SpeedValue = 1f;

        /// <summary>
        /// Backing field of <see cref="AssetSource"/>.
        /// </summary>
        ITimelineAssetSource AssetSourceValue;

        /// <summary>
        /// Asset source created from the core resolver when none was supplied.
        /// </summary>
        [NativeOwnedMember]
        SceneTimelineAssetSource DefaultAssetSource;

        /// <summary>
        /// Whether slots, receivers and assets are resolved.
        /// </summary>
        bool IsBoundValue;

        /// <summary>
        /// Entity bound to each slot (null for slots no track needs).
        /// </summary>
        [NativeOwnedMember]
        Entity[] SlotEntities;

        /// <summary>
        /// Per-track cursor: last started segment, interval or animation; next audio clip to start; next marker to fire.
        /// </summary>
        [NativeOwnedMember]
        int[] Cursors;

        /// <summary>
        /// Receiver of each value track (null for other kinds).
        /// </summary>
        [NativeOwnedMember]
        ITimelineReceiver[] TrackReceivers;

        /// <summary>
        /// Animator of each animation track (null for other kinds).
        /// </summary>
        [NativeOwnedMember]
        AnimationPlayerComponent[] TrackAnimators;

        /// <summary>
        /// Index of each track's first entry in the flat audio or animation arrays below.
        /// </summary>
        [NativeOwnedMember]
        int[] TrackClipBase;

        /// <summary>
        /// Resolved animation clip of every animation entry, all tracks concatenated.
        /// </summary>
        [NativeOwnedMember]
        AnimationClipAsset[] AnimationAssets;

        /// <summary>
        /// Resolved audio asset of every audio entry, all tracks concatenated.
        /// </summary>
        [NativeOwnedMember]
        AudioAsset[] AudioAssets;

        /// <summary>
        /// Reusable playback request of every audio entry, created at bind so starting a sound allocates nothing here.
        /// </summary>
        [NativeOwnedMember]
        AudioPlaybackRequest[] AudioRequests;

        /// <summary>
        /// Voice id of every audio entry while <see cref="AudioPlaying"/> is set.
        /// </summary>
        [NativeOwnedMember]
        int[] AudioVoiceIds;

        /// <summary>
        /// Whether each audio entry currently has a voice.
        /// </summary>
        [NativeOwnedMember]
        bool[] AudioPlaying;

        /// <summary>
        /// Event listeners on the player's entity.
        /// </summary>
        [NativeOwnedMember]
        ITimelineEventListener[] Listeners;

        /// <summary>
        /// Transform composition state.
        /// </summary>
        [NativeOwnedMember]
        TimelineTransformComposer Composer;

        /// <summary>
        /// Audio manager of the owning core, borrowed when the timeline has sounds.
        /// </summary>
        AudioManager Audio;

        /// <summary>
        /// Current playback time in seconds.
        /// </summary>
        double TimeValue;

        /// <summary>
        /// Whether playback advances on update.
        /// </summary>
        bool IsPlayingValue;

        /// <summary>
        /// Whether playback is paused (resumable at <see cref="TimeValue"/>) rather than stopped.
        /// </summary>
        bool IsPausedValue;

        /// <summary>
        /// Last tick whose events and sound starts were processed (-1 before the first).
        /// </summary>
        int LastTick = -1;

        /// <summary>
        /// Whether sounds must be restarted at the current tick when playback resumes (after a pause or a seek while not
        /// playing).
        /// </summary>
        bool AudioNeedsResync;

        /// <summary>
        /// Gets or sets the cooked timeline to play. Set it in code, or leave it null and set <see cref="TimelinePath"/>.
        /// Cannot change once the player is bound.
        /// </summary>
        public CookedTimelineAsset Timeline {
            get { return TimelineValue; }
            set {
                EnsureNotBound();
                TimelineValue = value;
                OwnsTimeline = false;
            }
        }

        /// <summary>
        /// Gets or sets the content path of a <c>.hctimeline</c> file loaded at bind time through the core content
        /// manager when <see cref="Timeline"/> is not set (requires <see cref="TimelineRuntimeRegistration.Register"/>).
        /// </summary>
        public string TimelinePath {
            get { return TimelinePathValue; }
            set {
                EnsureNotBound();
                TimelinePathValue = value ?? string.Empty;
            }
        }

        /// <summary>
        /// Gets or sets one entity reference per timeline slot, in slot order. Slots no track drives may stay unresolved.
        /// </summary>
        public SceneEntityReference[] Slots {
            get { return SlotsValue; }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                EnsureNotBound();
                SlotsValue = value;
            }
        }

        /// <summary>
        /// Gets or sets whether playback starts when the component is initialized.
        /// </summary>
        public bool PlayOnStart {
            get { return PlayOnStartValue; }
            set { PlayOnStartValue = value; }
        }

        /// <summary>
        /// Gets or sets what happens when playback reaches the end.
        /// </summary>
        public TimelineEndMode EndMode {
            get { return EndModeValue; }
            set { EndModeValue = value; }
        }

        /// <summary>
        /// Gets or sets the playback speed multiplier (0 freezes time, 1 is normal).
        /// </summary>
        public float Speed {
            get { return SpeedValue; }
            set {
                if (!(value >= 0f) || float.IsInfinity(value)) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Timeline speed must be a finite, non-negative number.");
                }

                SpeedValue = value;
            }
        }

        /// <summary>
        /// Gets or sets where audio and animation assets come from; null uses the core's scene asset resolver.
        /// </summary>
        public ITimelineAssetSource AssetSource {
            get { return AssetSourceValue; }
            set {
                EnsureNotBound();
                AssetSourceValue = value;
            }
        }

        /// <summary>
        /// Gets the playback time in seconds.
        /// </summary>
        public double Time {
            get { return TimeValue; }
        }

        /// <summary>
        /// Gets the tick most recently reached by playback or a seek (-1 before playback starts).
        /// </summary>
        public int CurrentTick {
            get { return LastTick; }
        }

        /// <summary>
        /// Gets whether playback advances on update (also true while holding the end).
        /// </summary>
        public bool IsPlaying {
            get { return IsPlayingValue; }
        }

        /// <summary>
        /// Gets whether playback is paused at <see cref="Time"/>.
        /// </summary>
        public bool IsPaused {
            get { return IsPausedValue; }
        }

        /// <summary>
        /// Gets whether slots, receivers and assets are resolved.
        /// </summary>
        public bool IsBound {
            get { return IsBoundValue; }
        }

        /// <summary>
        /// Starts playback automatically when <see cref="PlayOnStart"/> is set.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        public override void ComponentInitialized(Entity entity) {
            base.ComponentInitialized(entity);
            if (PlayOnStartValue) {
                Play();
            }
        }

        /// <summary>
        /// Silences the timeline's sounds when the component leaves its entity.
        /// </summary>
        /// <param name="entity">Entity the component is removed from.</param>
        public override void ComponentRemoved(Entity entity) {
            StopAllVoices();
            IsPlayingValue = false;
            IsPausedValue = false;
            base.ComponentRemoved(entity);
        }

        /// <summary>
        /// Advances playback by the core's frame time.
        /// </summary>
        public override void Update() {
            base.Update();
            Advance(OwnerCore.DeltaTime);
        }

        /// <summary>
        /// Resolves the timeline, slot entities, receivers, animators, assets and listeners. Called automatically by
        /// the playback methods; calling it again does nothing.
        /// </summary>
        public void Bind() {
            if (IsBoundValue) {
                return;
            } else if (Parent == null) {
                throw new InvalidOperationException("TimelinePlayerComponent must be added to an entity before it can bind.");
            }

            CookedTimelineAsset timeline = ResolveTimeline();
            if (SlotsValue.Length != timeline.SlotCount) {
                throw new InvalidOperationException($"Timeline '{timeline.Id}' declares {timeline.SlotCount} slots, but the player binds {SlotsValue.Length}.");
            }

            int trackCount = timeline.Tracks.Length;
            SlotEntities = new Entity[timeline.SlotCount];
            Cursors = new int[trackCount];
            TrackReceivers = new ITimelineReceiver[trackCount];
            TrackAnimators = new AnimationPlayerComponent[trackCount];
            TrackClipBase = new int[trackCount];
            int animationCount = 0;
            int audioCount = 0;
            for (int index = 0; index < trackCount; index++) {
                CookedTimelineTrack track = timeline.Tracks[index];
                Cursors[index] = -1;
                if (track.Kind == CookedTimelineTrackKind.Animation) {
                    TrackClipBase[index] = animationCount;
                    animationCount += track.AnimationClips.Length;
                } else if (track.Kind == CookedTimelineTrackKind.Audio) {
                    TrackClipBase[index] = audioCount;
                    audioCount += track.AudioClips.Length;
                }
                if (track.SlotIndex >= 0 && track.Kind != CookedTimelineTrackKind.Audio) {
                    SlotEntities[track.SlotIndex] = RequireSlotEntity(timeline, track.SlotIndex);
                }
            }

            ITimelineAssetSource assetSource = null;
            if (animationCount > 0 || audioCount > 0) {
                assetSource = ResolveAssetSource();
            }
            if (audioCount > 0) {
                Audio = OwnerCore.AudioManager;
                if (Audio == null) {
                    throw new InvalidOperationException($"Timeline '{timeline.Id}' plays audio, but the core has no audio backend.");
                }
            }

            AnimationAssets = new AnimationClipAsset[animationCount];
            AudioAssets = new AudioAsset[audioCount];
            AudioRequests = new AudioPlaybackRequest[audioCount];
            AudioVoiceIds = new int[audioCount];
            AudioPlaying = new bool[audioCount];
            for (int index = 0; index < trackCount; index++) {
                BindTrack(timeline, index, assetSource);
            }

            Listeners = CollectListeners();
            Composer = new TimelineTransformComposer(SlotEntities, timeline);
            TimelineValue = timeline;
            IsBoundValue = true;
        }

        /// <summary>
        /// Starts playback from the beginning, or resumes it after <see cref="Pause"/> or <see cref="Seek"/>. Does
        /// nothing while already playing.
        /// </summary>
        public void Play() {
            Bind();
            if (IsPlayingValue) {
                return;
            }
            if (IsPausedValue) {
                IsPausedValue = false;
                IsPlayingValue = true;
                if (AudioNeedsResync) {
                    ResyncAudio(LastTick);
                    AudioNeedsResync = false;
                }
                return;
            }

            StopAllVoices();
            ResetLaneCursors();
            TimeValue = 0;
            IsPlayingValue = true;
            AudioNeedsResync = false;
            Step(0);
            Evaluate(0);
            LastTick = 0;
        }

        /// <summary>
        /// Pauses playback at the current time and silences the timeline's sounds; <see cref="Play"/> resumes.
        /// </summary>
        public void Pause() {
            if (!IsPlayingValue) {
                return;
            }

            IsPlayingValue = false;
            IsPausedValue = true;
            StopAllVoices();
            AudioNeedsResync = true;
        }

        /// <summary>
        /// Stops playback and rewinds to the beginning without changing the applied values; the next <see cref="Play"/>
        /// starts over.
        /// </summary>
        public void Stop() {
            StopAllVoices();
            IsPlayingValue = false;
            IsPausedValue = false;
            AudioNeedsResync = false;
            TimeValue = 0;
            LastTick = -1;
        }

        /// <summary>
        /// Jumps to a time (clamped to the timeline) and applies it. Events in the skipped span do not fire; sounds
        /// spanning the target restart with a start offset (when the backend supports it) now if playing, or on resume.
        /// A stopped player becomes paused at the target.
        /// </summary>
        /// <param name="seconds">Target time in seconds.</param>
        public void Seek(double seconds) {
            if (double.IsNaN(seconds)) {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            Bind();
            double duration = TimelineValue.DurationSeconds;
            double target = seconds < 0 ? 0 : (seconds > duration ? duration : seconds);
            StopAllVoices();
            TimeValue = target;
            int tick = ToTick(target);
            CookedTimelineTrack[] tracks = TimelineValue.Tracks;
            for (int index = 0; index < tracks.Length; index++) {
                if (tracks[index].Kind == CookedTimelineTrackKind.Event) {
                    Cursors[index] = TimelineCursors.FirstMarkerAfter(tracks[index].Markers, tick);
                }
            }

            LastTick = tick;
            Evaluate(tick);
            if (IsPlayingValue) {
                ResyncAudio(tick);
            } else {
                IsPausedValue = true;
                AudioNeedsResync = true;
            }
        }

        /// <summary>
        /// Advances playback by a time step (scaled by <see cref="Speed"/>), firing crossed events, starting and stopping
        /// sounds, applying the new tick and handling the end according to <see cref="EndMode"/>. Does nothing unless
        /// playing.
        /// </summary>
        /// <param name="deltaSeconds">Elapsed real time in seconds.</param>
        public void Advance(double deltaSeconds) {
            if (!IsPlayingValue) {
                return;
            } else if (!(deltaSeconds >= 0) || double.IsInfinity(deltaSeconds)) {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "The time step must be a finite, non-negative number.");
            }

            double duration = TimelineValue.DurationSeconds;
            int endTick = TimelineValue.DurationTicks;
            TimeValue += deltaSeconds * SpeedValue;
            int reachedTick = ToTick(TimeValue);
            if (TimeValue < duration && reachedTick < endTick) {
                if (reachedTick != LastTick) {
                    Step(reachedTick);
                    Evaluate(reachedTick);
                    LastTick = reachedTick;
                }
                return;
            }

            if (LastTick < endTick) {
                Step(endTick);
            }
            if (EndModeValue == TimelineEndMode.Loop) {
                WrapAround(TimeValue - duration, duration);
                return;
            }

            TimeValue = duration;
            Evaluate(endTick);
            LastTick = endTick;
            if (EndModeValue == TimelineEndMode.Stop) {
                StopAllVoices();
                IsPlayingValue = false;
                IsPausedValue = false;
            }
        }

        /// <summary>
        /// Applies the timeline state at a tick (clamped to the timeline): transforms, receiver values, activation and
        /// animation poses. Fires no events, starts no sounds and does not change <see cref="Time"/>, so tools and tests
        /// can scrub with it; the result depends only on the tick.
        /// </summary>
        /// <param name="tick">Tick to apply.</param>
        public void Evaluate(int tick) {
            Bind();
            int durationTicks = TimelineValue.DurationTicks;
            if (tick < 0) {
                tick = 0;
            } else if (tick > durationTicks) {
                tick = durationTicks;
            }

            Composer.BeginFrame();
            CookedTimelineTrack[] tracks = TimelineValue.Tracks;
            for (int index = 0; index < tracks.Length; index++) {
                CookedTimelineTrack track = tracks[index];
                CookedTimelineTrackKind kind = track.Kind;
                if (kind == CookedTimelineTrackKind.Transform) {
                    int cursor = TimelineCursors.SeekSegment(track.Segments, Cursors[index], tick);
                    Cursors[index] = cursor;
                    if (cursor >= 0) {
                        Composer.Write(track.SlotIndex, track.ChannelIndex, track.Mode, track.Segments[cursor].Evaluate(tick));
                    }
                } else if (kind == CookedTimelineTrackKind.Value) {
                    int cursor = TimelineCursors.SeekSegment(track.Segments, Cursors[index], tick);
                    Cursors[index] = cursor;
                    if (cursor >= 0) {
                        TrackReceivers[index].SetTimelineValue(track.ChannelIndex, track.Segments[cursor].Evaluate(tick));
                    }
                } else if (kind == CookedTimelineTrackKind.Activation) {
                    int activationTick = tick == durationTicks ? tick - 1 : tick;
                    int cursor = TimelineCursors.SeekInterval(track.Intervals, Cursors[index], activationTick);
                    Cursors[index] = cursor;
                    bool active = cursor >= 0 && activationTick < track.Intervals[cursor].EndTick;
                    Entity entity = SlotEntities[track.SlotIndex];
                    if (entity.Enabled != active) {
                        entity.Enabled = active;
                    }
                } else if (kind == CookedTimelineTrackKind.Animation) {
                    int cursor = TimelineCursors.SeekAnimation(track.AnimationClips, Cursors[index], tick);
                    Cursors[index] = cursor;
                    if (cursor >= 0) {
                        ApplyAnimation(index, track, cursor, tick);
                    }
                }
            }

            Composer.Apply();
        }

        /// <summary>
        /// Stops the timeline's sounds and releases everything resolved at bind time, including a timeline loaded from
        /// <see cref="TimelinePath"/>.
        /// </summary>
        public override void Dispose() {
            StopAllVoices();
            if (Composer != null) {
                Composer.Release();
            }

            NativeOwnership.Release(ref Composer);
            NativeOwnership.Release(ref SlotEntities);
            NativeOwnership.Release(ref Cursors);
            NativeOwnership.Release(ref TrackReceivers);
            NativeOwnership.Release(ref TrackAnimators);
            NativeOwnership.Release(ref TrackClipBase);
            NativeOwnership.Release(ref AnimationAssets);
            NativeOwnership.Release(ref AudioAssets);
            NativeOwnership.DeleteItemsAndRelease(ref AudioRequests);
            NativeOwnership.Release(ref AudioVoiceIds);
            NativeOwnership.Release(ref AudioPlaying);
            NativeOwnership.Release(ref Listeners);
            NativeOwnership.Release(ref DefaultAssetSource);
            if (OwnsTimeline) {
                NativeOwnership.DisposeAndRelease(ref TimelineValue);
                OwnsTimeline = false;
            }

            IsBoundValue = false;
            base.Dispose();
        }

        /// <summary>
        /// Returns the timeline to bind: the assigned asset, or the one loaded from <see cref="TimelinePath"/>.
        /// </summary>
        /// <returns>The cooked timeline.</returns>
        [NativeBorrowedReturn]
        CookedTimelineAsset ResolveTimeline() {
            if (TimelineValue != null) {
                return TimelineValue;
            } else if (string.IsNullOrEmpty(TimelinePathValue)) {
                throw new InvalidOperationException("TimelinePlayerComponent needs a Timeline or a TimelinePath before it can bind.");
            }

            TimelineValue = OwnerCore.GetContentManager().Load<CookedTimelineAsset>(TimelinePathValue, TimelineRuntimeRegistration.CookedTimelineProcessorId);
            OwnsTimeline = true;
            return TimelineValue;
        }

        /// <summary>
        /// Returns the asset source to resolve clips with, creating the core-backed default when none was supplied.
        /// </summary>
        /// <returns>The asset source.</returns>
        [NativeBorrowedReturn]
        ITimelineAssetSource ResolveAssetSource() {
            if (AssetSourceValue != null) {
                return AssetSourceValue;
            }

            NativeOwnership.Release(ref DefaultAssetSource);
            DefaultAssetSource = new SceneTimelineAssetSource(OwnerCore.SceneAssetReferenceResolver);
            return DefaultAssetSource;
        }

        /// <summary>
        /// Returns the entity bound to a slot a track needs, failing when the reference is unresolved.
        /// </summary>
        /// <param name="timeline">Timeline being bound, for messages.</param>
        /// <param name="slot">Slot index.</param>
        /// <returns>The bound entity.</returns>
        [NativeBorrowedReturn]
        Entity RequireSlotEntity(CookedTimelineAsset timeline, int slot) {
            SceneEntityReference reference = SlotsValue[slot];
            if (reference == null || reference.ResolvedEntity == null) {
                throw new InvalidOperationException($"Timeline '{timeline.Id}' slot '{timeline.SlotNames[slot]}' (index {slot}) is not bound to an entity.");
            }

            return reference.ResolvedEntity;
        }

        /// <summary>
        /// Resolves what one track needs at runtime: its receiver, animator, or clip assets.
        /// </summary>
        /// <param name="timeline">Timeline being bound.</param>
        /// <param name="index">Track index.</param>
        /// <param name="assetSource">Source of audio and animation assets (null when the timeline has none).</param>
        void BindTrack(CookedTimelineAsset timeline, int index, ITimelineAssetSource assetSource) {
            CookedTimelineTrack track = timeline.Tracks[index];
            if (track.Kind == CookedTimelineTrackKind.Value) {
                TrackReceivers[index] = FindReceiver(timeline, SlotEntities[track.SlotIndex], track);
            } else if (track.Kind == CookedTimelineTrackKind.Animation) {
                TrackAnimators[index] = FindAnimator(timeline, SlotEntities[track.SlotIndex], track.SlotIndex);
                for (int clip = 0; clip < track.AnimationClips.Length; clip++) {
                    SceneAssetReference reference = track.AnimationClips[clip].Animation;
                    if (reference == null) {
                        throw new InvalidOperationException($"Timeline '{timeline.Id}' has an animation clip without an animation asset.");
                    }

                    AnimationAssets[TrackClipBase[index] + clip] = assetSource.ResolveAnimationClip(reference);
                }
            } else if (track.Kind == CookedTimelineTrackKind.Audio) {
                for (int clip = 0; clip < track.AudioClips.Length; clip++) {
                    CookedTimelineAudioClip audioClip = track.AudioClips[clip];
                    if (audioClip.Audio == null) {
                        throw new InvalidOperationException($"Timeline '{timeline.Id}' has an audio clip without an audio asset.");
                    }

                    int flat = TrackClipBase[index] + clip;
                    AudioAssets[flat] = assetSource.ResolveAudio(audioClip.Audio);
                    AudioPlaybackRequest request = new AudioPlaybackRequest();
                    request.Gain = audioClip.Gain;
                    AudioRequests[flat] = request;
                }
            }
        }

        /// <summary>
        /// Finds the receiver component a value track names on its slot entity.
        /// </summary>
        /// <param name="timeline">Timeline being bound, for messages.</param>
        /// <param name="entity">Slot entity.</param>
        /// <param name="track">Value track.</param>
        /// <returns>The receiver.</returns>
        [NativeBorrowedReturn]
        static ITimelineReceiver FindReceiver(CookedTimelineAsset timeline, Entity entity, CookedTimelineTrack track) {
            List<Component> components = entity.Components;
            if (components != null) {
                for (int index = 0; index < components.Count; index++) {
                    if (components[index] is ITimelineReceiver receiver && receiver.TimelineReceiverId == track.ReceiverId) {
                        return receiver;
                    }
                }
            }

            throw new InvalidOperationException($"Timeline '{timeline.Id}' slot '{timeline.SlotNames[track.SlotIndex]}' needs a timeline receiver with id {track.ReceiverId} on its entity.");
        }

        /// <summary>
        /// Finds the animation player on an animation track's slot entity.
        /// </summary>
        /// <param name="timeline">Timeline being bound, for messages.</param>
        /// <param name="entity">Slot entity.</param>
        /// <param name="slot">Slot index, for messages.</param>
        /// <returns>The animation player.</returns>
        [NativeBorrowedReturn]
        static AnimationPlayerComponent FindAnimator(CookedTimelineAsset timeline, Entity entity, int slot) {
            List<Component> components = entity.Components;
            if (components != null) {
                for (int index = 0; index < components.Count; index++) {
                    if (components[index] is AnimationPlayerComponent animator) {
                        return animator;
                    }
                }
            }

            throw new InvalidOperationException($"Timeline '{timeline.Id}' slot '{timeline.SlotNames[slot]}' plays animations, so its entity needs an AnimationPlayerComponent.");
        }

        /// <summary>
        /// Collects the event listeners on the player's own entity.
        /// </summary>
        /// <returns>The listeners, in component order.</returns>
        [NativeOwnedReturn]
        ITimelineEventListener[] CollectListeners() {
            List<Component> components = Parent.Components;
            int count = 0;
            if (components != null) {
                for (int index = 0; index < components.Count; index++) {
                    if (components[index] is ITimelineEventListener) {
                        count++;
                    }
                }
            }

            ITimelineEventListener[] listeners = new ITimelineEventListener[count];
            int next = 0;
            if (components != null) {
                for (int index = 0; index < components.Count; index++) {
                    if (components[index] is ITimelineEventListener listener) {
                        listeners[next] = listener;
                        next++;
                    }
                }
            }

            return listeners;
        }

        /// <summary>
        /// Shows the animation an animation track has at a tick, switching the animator to the entry's clip when needed
        /// and holding the entry's last pose after its end.
        /// </summary>
        /// <param name="index">Track index.</param>
        /// <param name="track">Animation track.</param>
        /// <param name="cursor">Entry that applies at the tick.</param>
        /// <param name="tick">Tick being applied.</param>
        void ApplyAnimation(int index, CookedTimelineTrack track, int cursor, int tick) {
            CookedTimelineAnimationClip clip = track.AnimationClips[cursor];
            AnimationClipAsset asset = AnimationAssets[TrackClipBase[index] + cursor];
            AnimationPlayerComponent animator = TrackAnimators[index];
            if (!ReferenceEquals(animator.CurrentClip, asset)) {
                animator.Play(asset, false);
                animator.Pause();
            }

            int elapsedTicks = (tick < clip.EndTick ? tick : clip.EndTick) - clip.StartTick;
            double seconds = (clip.ClipInTicks + elapsedTicks * (double)clip.Speed) / TimelineValue.TickRate;
            animator.Seek((float)seconds);
        }

        /// <summary>
        /// Processes the event and audio lanes up to a tick: fires markers the event cursors pass, stops sounds that
        /// reached their end, and starts sounds whose start the audio cursors pass.
        /// </summary>
        /// <param name="tick">Tick playback reached.</param>
        void Step(int tick) {
            CookedTimelineTrack[] tracks = TimelineValue.Tracks;
            for (int index = 0; index < tracks.Length; index++) {
                CookedTimelineTrack track = tracks[index];
                if (track.Kind == CookedTimelineTrackKind.Event) {
                    FireMarkers(index, track, tick);
                } else if (track.Kind == CookedTimelineTrackKind.Audio) {
                    StepAudio(index, track, tick);
                }
            }
        }

        /// <summary>
        /// Fires every marker of an event track from its cursor up to a tick.
        /// </summary>
        /// <param name="index">Track index.</param>
        /// <param name="track">Event track.</param>
        /// <param name="tick">Tick playback reached.</param>
        void FireMarkers(int index, CookedTimelineTrack track, int tick) {
            CookedTimelineMarker[] markers = track.Markers;
            string[] strings = TimelineValue.Strings;
            int cursor = Cursors[index] < 0 ? 0 : Cursors[index];
            while (cursor < markers.Length && markers[cursor].Tick <= tick) {
                CookedTimelineMarker marker = markers[cursor];
                for (int listener = 0; listener < Listeners.Length; listener++) {
                    Listeners[listener].OnTimelineEvent(strings[marker.NameIndex], strings[marker.ValueIndex]);
                }
                cursor++;
            }

            Cursors[index] = cursor;
        }

        /// <summary>
        /// Stops an audio track's sounds that reached their end and starts those whose start was crossed.
        /// </summary>
        /// <param name="index">Track index.</param>
        /// <param name="track">Audio track.</param>
        /// <param name="tick">Tick playback reached.</param>
        void StepAudio(int index, CookedTimelineTrack track, int tick) {
            CookedTimelineAudioClip[] clips = track.AudioClips;
            int baseIndex = TrackClipBase[index];
            int cursor = Cursors[index] < 0 ? 0 : Cursors[index];
            for (int clip = 0; clip < cursor; clip++) {
                if (AudioPlaying[baseIndex + clip] && clips[clip].EndTick <= tick) {
                    StopVoice(baseIndex + clip);
                }
            }
            while (cursor < clips.Length && clips[cursor].StartTick <= tick) {
                if (clips[cursor].EndTick > tick) {
                    StartAudio(index, cursor, tick, false);
                }
                cursor++;
            }

            Cursors[index] = cursor;
        }

        /// <summary>
        /// Restarts the sounds that span a tick (after a seek or a pause) and moves the audio cursors past it. Sounds
        /// that would start mid-clip are skipped when the backend cannot seek.
        /// </summary>
        /// <param name="tick">Tick playback continues from.</param>
        void ResyncAudio(int tick) {
            CookedTimelineTrack[] tracks = TimelineValue.Tracks;
            for (int index = 0; index < tracks.Length; index++) {
                CookedTimelineTrack track = tracks[index];
                if (track.Kind != CookedTimelineTrackKind.Audio) {
                    continue;
                }

                CookedTimelineAudioClip[] clips = track.AudioClips;
                for (int clip = 0; clip < clips.Length; clip++) {
                    if (clips[clip].StartTick <= tick && tick < clips[clip].EndTick && !AudioPlaying[TrackClipBase[index] + clip]) {
                        StartAudio(index, clip, tick, true);
                    }
                }

                Cursors[index] = TimelineCursors.FirstAudioAfter(clips, tick);
            }
        }

        /// <summary>
        /// Starts one sound at a tick, offset into the asset by its clip-in plus the time since its start. A start
        /// requested by a seek or resume past the clip's first tick is skipped when the backend cannot seek; a normal
        /// crossing always plays (a backend without seeking then starts at the asset's beginning).
        /// </summary>
        /// <param name="index">Track index.</param>
        /// <param name="clip">Clip index within the track.</param>
        /// <param name="tick">Tick playback is at.</param>
        /// <param name="resync">Whether the start comes from a seek or resume rather than a crossing.</param>
        void StartAudio(int index, int clip, int tick, bool resync) {
            CookedTimelineAudioClip audioClip = TimelineValue.Tracks[index].AudioClips[clip];
            if (resync && tick > audioClip.StartTick && !Audio.SupportsStartOffset) {
                return;
            }

            int flat = TrackClipBase[index] + clip;
            AudioPlaybackRequest request = AudioRequests[flat];
            request.StartOffsetSeconds = (float)((double)(audioClip.ClipInTicks + tick - audioClip.StartTick) / TimelineValue.TickRate);
            AudioVoiceIds[flat] = Audio.Play(AudioAssets[flat], request);
            AudioPlaying[flat] = true;
        }

        /// <summary>
        /// Stops one sound if it is playing.
        /// </summary>
        /// <param name="flat">Index of the sound in the flat audio arrays.</param>
        void StopVoice(int flat) {
            if (!AudioPlaying[flat]) {
                return;
            }

            Audio.Stop(AudioVoiceIds[flat]);
            AudioPlaying[flat] = false;
        }

        /// <summary>
        /// Stops every sound the player started.
        /// </summary>
        void StopAllVoices() {
            if (AudioPlaying == null) {
                return;
            }
            for (int flat = 0; flat < AudioPlaying.Length; flat++) {
                StopVoice(flat);
            }
        }

        /// <summary>
        /// Rewinds the event and audio cursors to the first entry.
        /// </summary>
        void ResetLaneCursors() {
            CookedTimelineTrack[] tracks = TimelineValue.Tracks;
            for (int index = 0; index < tracks.Length; index++) {
                CookedTimelineTrackKind kind = tracks[index].Kind;
                if (kind == CookedTimelineTrackKind.Event || kind == CookedTimelineTrackKind.Audio) {
                    Cursors[index] = 0;
                }
            }
        }

        /// <summary>
        /// Continues a looping timeline from the start after it passed the end.
        /// </summary>
        /// <param name="overflowSeconds">Time past the end.</param>
        /// <param name="duration">Timeline duration in seconds.</param>
        void WrapAround(double overflowSeconds, double duration) {
            double wrapped = overflowSeconds;
            if (wrapped < 0) {
                wrapped = 0;
            } else if (wrapped >= duration) {
                wrapped = wrapped % duration;
            }

            StopAllVoices();
            ResetLaneCursors();
            TimeValue = wrapped;
            int tick = ToTick(wrapped);
            Step(tick);
            Evaluate(tick);
            LastTick = tick;
        }

        /// <summary>
        /// Converts seconds to the timeline tick that contains them, clamped to the timeline.
        /// </summary>
        /// <param name="seconds">Time in seconds.</param>
        /// <returns>The tick.</returns>
        int ToTick(double seconds) {
            int tick = (int)Math.Floor(seconds * TimelineValue.TickRate + TickEpsilon);
            if (tick < 0) {
                return 0;
            } else if (tick > TimelineValue.DurationTicks) {
                return TimelineValue.DurationTicks;
            }

            return tick;
        }

        /// <summary>
        /// Rejects configuration changes after binding, when they would no longer take effect.
        /// </summary>
        void EnsureNotBound() {
            if (IsBoundValue) {
                throw new InvalidOperationException("The timeline, path, slots and asset source cannot change once the player is bound.");
            }
        }
    }
}

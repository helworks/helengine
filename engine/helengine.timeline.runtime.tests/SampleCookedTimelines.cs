namespace helengine.timeline.runtime.tests {
    /// <summary>
    /// Hand-built cooked timelines for the player tests, at 10 ticks per second so tick arithmetic stays readable.
    /// </summary>
    static class SampleCookedTimelines {
        /// <summary>
        /// Slot index of the transformed hero.
        /// </summary>
        public const int HeroSlot = 0;

        /// <summary>
        /// Slot index of the entity carrying <see cref="TestLampReceiver"/>.
        /// </summary>
        public const int LampSlot = 1;

        /// <summary>
        /// Slot index of the entity toggled by activation.
        /// </summary>
        public const int DoorSlot = 2;

        /// <summary>
        /// Slot index of the entity with an animation player.
        /// </summary>
        public const int ActorSlot = 3;

        /// <summary>
        /// Path of the sample sound.
        /// </summary>
        public const string SoundPath = "sfx/pop.wav";

        /// <summary>
        /// Path of the sample animation.
        /// </summary>
        public const string AnimationPath = "anim/wave.hanim";

        /// <summary>
        /// Builds a 2-second timeline (20 ticks) using every track kind:
        /// <list type="bullet">
        /// <item>hero position X (absolute): 0 to 10 over ticks 0-10 (linear), then 10 to 0 over ticks 14-18 (smoothstep);</item>
        /// <item>hero scale Y (offset): multiplier 1 to 2 over ticks 5-10;</item>
        /// <item>lamp channel 1 (range): 0 to 4 over ticks 2-6;</item>
        /// <item>door active during ticks [4, 8) and [12, 20);</item>
        /// <item>events start@0, mid=x@5, end@20;</item>
        /// <item>a sound from tick 3 to 9 with 2 ticks of clip-in and gain 0.5;</item>
        /// <item>actor animation from tick 10 to 16, clip-in 5 ticks, speed 2.</item>
        /// </list>
        /// </summary>
        /// <returns>The cooked timeline.</returns>
        public static CookedTimelineAsset Rich() {
            CookedTimelineAsset asset = new CookedTimelineAsset {
                Id = "timelines/rich",
                TickRate = 10,
                DurationTicks = 20,
                SlotNames = new string[] { "hero", "lamp", "door", "actor" },
                Strings = new string[] { "", "start", "mid", "x", "end" }
            };
            asset.Tracks = new CookedTimelineTrack[] {
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Transform, SlotIndex = HeroSlot, ChannelIndex = (int)CookedTimelineTransformChannel.PositionX,
                    Segments = new CookedTimelineSegment[] {
                        Segment(0, 10, 0, 10, CurveCatalog.LinearCode),
                        Segment(14, 18, 10, 0, CurveCatalog.SmoothstepCode)
                    }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Transform, SlotIndex = HeroSlot, ChannelIndex = (int)CookedTimelineTransformChannel.ScaleY,
                    Mode = CookedTimelineTransformMode.Offset,
                    Segments = new CookedTimelineSegment[] { Segment(5, 10, 1, 2, CurveCatalog.LinearCode) }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Value, SlotIndex = LampSlot, ReceiverId = TestLampReceiver.Id, ChannelIndex = 1,
                    Segments = new CookedTimelineSegment[] { Segment(2, 6, 0, 4, CurveCatalog.LinearCode) }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Activation, SlotIndex = DoorSlot,
                    Intervals = new CookedTimelineInterval[] {
                        new CookedTimelineInterval { StartTick = 4, EndTick = 8 },
                        new CookedTimelineInterval { StartTick = 12, EndTick = 20 }
                    }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Event,
                    Markers = new CookedTimelineMarker[] {
                        new CookedTimelineMarker { Tick = 0, NameIndex = 1, ValueIndex = 0 },
                        new CookedTimelineMarker { Tick = 5, NameIndex = 2, ValueIndex = 3 },
                        new CookedTimelineMarker { Tick = 20, NameIndex = 4, ValueIndex = 0 }
                    }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Audio,
                    AudioClips = new CookedTimelineAudioClip[] {
                        new CookedTimelineAudioClip { StartTick = 3, EndTick = 9, ClipInTicks = 2, Gain = 0.5f, Audio = SceneAssetReferenceFactory.CreateFileSystemAudio(SoundPath) }
                    }
                },
                new CookedTimelineTrack {
                    Kind = CookedTimelineTrackKind.Animation, SlotIndex = ActorSlot,
                    AnimationClips = new CookedTimelineAnimationClip[] {
                        new CookedTimelineAnimationClip { StartTick = 10, EndTick = 16, ClipInTicks = 5, Speed = 2f, Animation = SceneAssetReferenceFactory.CreateFileSystemAnimationClip(AnimationPath) }
                    }
                }
            };
            return asset;
        }

        /// <summary>
        /// Creates one segment.
        /// </summary>
        /// <param name="start">Start tick.</param>
        /// <param name="end">End tick.</param>
        /// <param name="from">Start value.</param>
        /// <param name="to">End value.</param>
        /// <param name="curve">Curve code.</param>
        /// <returns>The segment.</returns>
        public static CookedTimelineSegment Segment(int start, int end, float from, float to, byte curve) {
            return new CookedTimelineSegment { StartTick = start, EndTick = end, From = from, To = to, CurveCode = curve };
        }
    }
}

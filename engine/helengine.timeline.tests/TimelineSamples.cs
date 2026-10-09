namespace helengine.timeline.tests {
    /// <summary>
    /// Builds the timelines the tests share: a rich three-term comparison ("LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR") that uses
    /// every track kind, cue anchors, blend ramps and an inline nested timeline, and a small reusable "pop" timeline.
    /// </summary>
    static class TimelineSamples {
        /// <summary>
        /// Builds the rich sample; it is valid without a resolver.
        /// </summary>
        /// <returns>A fresh, valid timeline.</returns>
        public static TimelineAsset ContrastThree() {
            TimelineAsset timeline = new TimelineAsset {
                TimelineId = "contrast_three",
                Version = 2,
                DisplayName = "Three-term contrast",
                Description = "Three terms appear on their spoken words, separated by ≠; a bar strikes the last one.",
                DurationSeconds = 4
            };
            timeline.Slots.Add(new TimelineSlotAsset { Name = "term_a", Kind = TimelineSlotKind.Text, Description = "First term, e.g. LEGALIZAR." });
            timeline.Slots.Add(new TimelineSlotAsset { Name = "term_b", Kind = TimelineSlotKind.Text });
            timeline.Slots.Add(new TimelineSlotAsset { Name = "term_c", Kind = TimelineSlotKind.Text });
            timeline.Slots.Add(new TimelineSlotAsset { Name = "separator", Kind = TimelineSlotKind.Text });
            timeline.Slots.Add(new TimelineSlotAsset { Name = "strike", Kind = TimelineSlotKind.Rect });
            timeline.Slots.Add(new TimelineSlotAsset { Name = "presenter", Kind = TimelineSlotKind.Entity });
            timeline.Cues.Add(new TimelineCueAsset { Name = "a", TimeSeconds = 0.2 });
            timeline.Cues.Add(new TimelineCueAsset { Name = "b", TimeSeconds = 1.4 });
            timeline.Cues.Add(new TimelineCueAsset { Name = "c", TimeSeconds = 2.6 });

            TimelineActivationTrackAsset activation = new TimelineActivationTrackAsset { Slot = "term_a", Name = "show a" };
            activation.Clips.Add(new TimelineClipAsset { Cue = "a", DurationSeconds = 3.8 });
            timeline.Tracks.Add(activation);

            TimelineTransformTrackAsset pop = new TimelineTransformTrackAsset { Slot = "term_a", Mode = TimelineTransformMode.Offset };
            TimelineTransformClipAsset popClip = new TimelineTransformClipAsset { Cue = "a", StartSeconds = -0.1, DurationSeconds = 0.5 };
            popClip.Position.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0, X = 0, Y = 0.05, Z = 0, Curve = CurveCatalog.EaseOutCubic });
            popClip.Position.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0.3 });
            popClip.Rotation.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0.1, Z = -4.5 });
            popClip.Scale.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0, X = 0.6, Y = 0.6, Z = 1, Curve = CurveCatalog.EaseOutBack });
            popClip.Scale.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0.35, X = 1, Y = 1, Z = 1 });
            pop.Clips.Add(popClip);
            timeline.Tracks.Add(pop);

            TimelineValueTrackAsset fade = new TimelineValueTrackAsset { Slot = "term_b", Channel = TimelineKnownChannels.Opacity };
            TimelineValueClipAsset fadeIn = new TimelineValueClipAsset { Cue = "b", DurationSeconds = 0.5, EaseOutSeconds = 0.2 };
            fadeIn.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0, Value = 0, Curve = CurveCatalog.Smoothstep });
            fadeIn.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0.3, Value = 1 });
            fade.Clips.Add(fadeIn);
            TimelineValueClipAsset dim = new TimelineValueClipAsset { Cue = "c", StartSeconds = -0.9, DurationSeconds = 0.9, EaseInSeconds = 0.25 };
            dim.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0.5, Value = 0.4, Curve = CurveCatalog.EaseInQuad });
            fade.Clips.Add(dim);
            timeline.Tracks.Add(fade);

            TimelineValueTrackAsset reveal = new TimelineValueTrackAsset { Slot = "strike", Channel = TimelineKnownChannels.Reveal };
            TimelineValueClipAsset strike = new TimelineValueClipAsset { StartSeconds = 3.2, DurationSeconds = 0.4 };
            strike.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0, Value = 0, Curve = CurveCatalog.EaseOutCubic });
            strike.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0.4, Value = 1 });
            reveal.Clips.Add(strike);
            timeline.Tracks.Add(reveal);

            TimelineAudioTrackAsset audio = new TimelineAudioTrackAsset();
            audio.Clips.Add(new TimelineAudioClipAsset {
                Cue = "c", DurationSeconds = 0.5, ClipInSeconds = 0.05, EaseOutSeconds = 0.1, Gain = 0.8,
                Audio = SceneAssetReferenceFactory.CreateFileSystemAudio("assets/sfx/whoosh.wav")
            });
            timeline.Tracks.Add(audio);

            TimelineAnimationTrackAsset animation = new TimelineAnimationTrackAsset { Slot = "presenter" };
            animation.Clips.Add(new TimelineAnimationClipAsset {
                DurationSeconds = 2, EaseOutSeconds = 0.5,
                Animation = SceneAssetReferenceFactory.CreateFileSystemReference(new string('a', 32), "assets/anim/point.hanim", "sha256:" + new string('0', 64))
            });
            animation.Clips.Add(new TimelineAnimationClipAsset {
                StartSeconds = 1.6, DurationSeconds = 2.4, EaseInSeconds = 0.4, ClipInSeconds = 0.1, Speed = 1.25,
                Animation = SceneAssetReferenceFactory.CreateFileSystemAnimationClip("assets/anim/idle.hanim")
            });
            timeline.Tracks.Add(animation);

            TimelineEventTrackAsset events = new TimelineEventTrackAsset { Name = "fx" };
            events.Markers.Add(new TimelineEventMarkerAsset { Cue = "c", TimeSeconds = 0.05, Name = "shake", Value = "0.3" });
            events.Markers.Add(new TimelineEventMarkerAsset { TimeSeconds = 3.2, Name = "strike" });
            timeline.Tracks.Add(events);

            TimelineNestedTrackAsset nested = new TimelineNestedTrackAsset();
            TimelineNestedClipAsset nestedPop = new TimelineNestedClipAsset { Cue = "c", StartSeconds = -0.1, DurationSeconds = 0.6, Speed = 1.5, Definition = Pop() };
            nestedPop.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = "target", Outer = "term_c" });
            nested.Clips.Add(nestedPop);
            timeline.Tracks.Add(nested);
            return timeline;
        }

        /// <summary>
        /// Builds a small reusable pop: a text target scales up with an overshoot and fades in.
        /// </summary>
        /// <returns>A fresh, valid timeline with one text slot named <c>target</c>.</returns>
        public static TimelineAsset Pop() {
            TimelineAsset timeline = new TimelineAsset { TimelineId = "pop", DurationSeconds = 0.6 };
            timeline.Slots.Add(new TimelineSlotAsset { Name = "target", Kind = TimelineSlotKind.Text });
            TimelineTransformTrackAsset scale = new TimelineTransformTrackAsset { Slot = "target" };
            TimelineTransformClipAsset clip = new TimelineTransformClipAsset { DurationSeconds = 0.6 };
            clip.Scale.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0, X = 0.5, Y = 0.5, Z = 1, Curve = CurveCatalog.EaseOutBack });
            clip.Scale.Add(new TimelineVectorKeyframeAsset { TimeSeconds = 0.4, X = 1, Y = 1, Z = 1 });
            scale.Clips.Add(clip);
            timeline.Tracks.Add(scale);
            TimelineValueTrackAsset fade = new TimelineValueTrackAsset { Slot = "target", Channel = TimelineKnownChannels.Opacity };
            TimelineValueClipAsset fadeClip = new TimelineValueClipAsset { DurationSeconds = 0.2 };
            fadeClip.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0, Value = 0 });
            fadeClip.Keyframes.Add(new TimelineKeyframeAsset { TimeSeconds = 0.2, Value = 1 });
            fade.Clips.Add(fadeClip);
            timeline.Tracks.Add(fade);
            return timeline;
        }

        /// <summary>
        /// Wraps a timeline in a nested clip that references it by path and maps its <c>target</c> slot.
        /// </summary>
        /// <param name="outerSlot">Slot of the containing timeline bound to <c>target</c>.</param>
        /// <param name="path">Referenced asset path.</param>
        /// <param name="duration">Clip duration.</param>
        /// <returns>A nested track holding one referencing clip.</returns>
        public static TimelineNestedTrackAsset ReferenceTrack(string outerSlot, string path, double duration) {
            TimelineNestedTrackAsset track = new TimelineNestedTrackAsset();
            TimelineNestedClipAsset clip = new TimelineNestedClipAsset { DurationSeconds = duration, Timeline = TimelineAssetReferences.FromPath(path) };
            clip.SlotMappings.Add(new TimelineSlotMappingAsset { Inner = "target", Outer = outerSlot });
            track.Clips.Add(clip);
            return track;
        }
    }
}

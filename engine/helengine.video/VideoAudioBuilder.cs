using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Builds the composition audio: one voice clip per take scene (gain, mute and envelopes), equal-power voice fades on
    /// both sides of a transition with <c>audio_fade_sec</c>, and the global audio tracks placed by scene moments.
    /// </summary>
    public static class VideoAudioBuilder {
        /// <summary>
        /// Adds every audio clip.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        public static void Build(VideoCompileState state) {
            Dictionary<string, AudioClip> voices = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (VideoSceneSpan span in state.Spans) {
                AudioClip voice = Voice(state, span);
                if (voice != null) {
                    voices[span.Scene.Id] = voice;
                    state.Document.AudioClips.Add(voice);
                }
            }
            for (int index = 1; index < state.Spans.Count; index++) {
                VideoScene scene = state.Spans[index].Scene;
                double fade = scene.Entry?.AudioFadeSec ?? 0;
                if (fade <= 0) {
                    continue;
                }
                if (voices.TryGetValue(state.Spans[index - 1].Scene.Id, out AudioClip outgoing)) {
                    MediaTime length = Clamp(fade, outgoing);
                    outgoing.Envelopes.Add(new AudioEnvelope { Type = "equal_power_out", Start = outgoing.End - outgoing.Start - length, Duration = length, From = 1, To = 0 });
                }
                if (voices.TryGetValue(scene.Id, out AudioClip incoming)) {
                    incoming.Envelopes.Add(new AudioEnvelope { Type = "equal_power_in", Start = MediaTime.Zero, Duration = Clamp(fade, incoming), From = 0, To = 1 });
                }
            }
            for (int index = 0; index < state.Edit.Tracks.Audio.Count; index++) {
                AudioClip clip = Track(state, state.Edit.Tracks.Audio[index], $"tracks.audio[{index}]");
                if (clip != null) {
                    state.Document.AudioClips.Add(clip);
                }
            }
        }

        /// <summary>
        /// Builds the voice clip of a take scene.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <returns>Voice clip, or null when the scene has no take with audio.</returns>
        static AudioClip Voice(VideoCompileState state, VideoSceneSpan span) {
            VideoTake take = span.Scene.Take;
            if (take == null || !state.Media.TryGetValue(take.Media, out VideoMedia media) || !media.HasAudio) {
                return null;
            }
            VideoVoice voice = span.Scene.Voice ?? new VideoVoice();
            AudioClip clip = new AudioClip {
                Id = "voice-" + span.Scene.Id, MediaId = take.Media, Start = span.Start, End = span.End,
                SourceIn = MediaTime.FromSeconds(take.InSec), SourceOut = MediaTime.FromSeconds(take.OutSec), Gain = voice.Gain, Muted = voice.Muted
            };
            for (int index = 0; index < voice.Envelopes.Count; index++) {
                VideoEnvelope envelope = voice.Envelopes[index];
                MediaTime start = VideoMomentResolver.Resolve(state.Edit, span, envelope.At, $"scenes[{span.Index}].voice.envelopes[{index}].at", state.Diagnostics);
                clip.Envelopes.Add(Envelope(envelope, start, span.Duration));
            }
            return clip;
        }

        /// <summary>
        /// Builds one global audio track clip.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="track">Track.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Audio clip, or null when it has no time to play.</returns>
        static AudioClip Track(VideoCompileState state, VideoAudioTrack track, string path) {
            VideoMedia media = state.Media[track.Media];
            MediaTime start = Position(state, track.Start, path + ".start");
            MediaTime end = state.Document.Duration;
            if (track.End != null) {
                end = Position(state, track.End, path + ".end");
            } else if (media.DurationSec > 0) {
                MediaTime available = start + MediaTime.FromSeconds(media.DurationSec - track.InSec);
                if (available < end) {
                    end = available;
                }
            }
            if (end <= start) {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "track_skipped", null, path, "The track ends before it starts and was skipped."));
                return null;
            }
            MediaTime length = end - start;
            AudioClip clip = new AudioClip {
                Id = "track-" + track.Id, MediaId = track.Media, Start = start, End = end,
                SourceIn = MediaTime.FromSeconds(track.InSec), SourceOut = MediaTime.FromSeconds(track.InSec) + length, Gain = track.Gain, Muted = track.Muted
            };
            for (int index = 0; index < track.Envelopes.Count; index++) {
                VideoEnvelope envelope = track.Envelopes[index];
                clip.Envelopes.Add(Envelope(envelope, ClipMoment(state, envelope.At, length, $"{path}.envelopes[{index}].at"), length));
            }
            return clip;
        }

        /// <summary>
        /// Resolves a global track position.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="moment">Track position.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Global time.</returns>
        static MediaTime Position(VideoCompileState state, VideoTrackMoment moment, string path) {
            VideoSceneSpan span = state.Spans.First(item => item.Scene.Id == moment.Scene);
            return state.Global(span, moment.At, path + ".at");
        }

        /// <summary>
        /// Resolves a moment measured from the start of an audio clip; word anchors do not apply to clips.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="moment">Moment.</param>
        /// <param name="length">Clip length.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Clip-local time.</returns>
        static MediaTime ClipMoment(VideoCompileState state, VideoMoment moment, MediaTime length, string path) {
            double seconds = length.ToSeconds(), value;
            if (moment.Sec.HasValue) {
                value = moment.Sec.Value;
            } else if (moment.FromEnd.HasValue) {
                value = seconds - moment.FromEnd.Value;
            } else if (moment.Fraction.HasValue) {
                value = seconds * moment.Fraction.Value;
            } else {
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "anchor_unresolved", null, path, "Word anchors are not available on audio track envelopes."));
                value = 0;
            }
            return MediaTime.FromSeconds(Math.Clamp(value, 0, seconds));
        }

        /// <summary>
        /// Converts an envelope, shortening it to fit the clip.
        /// </summary>
        /// <param name="envelope">Edit envelope.</param>
        /// <param name="start">Clip-local start.</param>
        /// <param name="length">Clip length.</param>
        /// <returns>Composition envelope.</returns>
        static AudioEnvelope Envelope(VideoEnvelope envelope, MediaTime start, MediaTime length) {
            MediaTime duration = MediaTime.FromSeconds(Math.Min(envelope.DurationSec, (length - start).ToSeconds()));
            return new AudioEnvelope { Type = envelope.Type, Start = start, Duration = duration, From = envelope.From, To = envelope.To };
        }

        /// <summary>
        /// Caps a fade length to the clip length.
        /// </summary>
        /// <param name="seconds">Requested fade.</param>
        /// <param name="clip">Clip.</param>
        /// <returns>Fade length.</returns>
        static MediaTime Clamp(double seconds, AudioClip clip) {
            return MediaTime.FromSeconds(Math.Min(seconds, (clip.End - clip.Start).ToSeconds()));
        }
    }
}

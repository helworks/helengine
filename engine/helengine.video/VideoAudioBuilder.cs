using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Builds the composition audio: one voice clip per take segment, played at the take speed with the pitch preserved and
    /// joined by short equal-power crossfades (gain, mute and scene envelopes apply across the whole scene), equal-power
    /// voice fades on both sides of a transition with <c>audio_fade_sec</c>, and the global audio tracks placed by scene
    /// moments.
    /// </summary>
    public static class VideoAudioBuilder {
        /// <summary>
        /// Half length of the crossfade at a jump cut inside a take: the outgoing segment plays this long past the cut and
        /// the incoming one starts this long before it.
        /// </summary>
        public static readonly MediaTime JoinHalf = new MediaTime(5, 1000);

        /// <summary>
        /// Number of linear pieces approximating the part of an equal-power envelope that a jump cut splits.
        /// </summary>
        const int CurvePieces = 4;

        /// <summary>
        /// Adds every audio clip.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        public static void Build(VideoCompileState state) {
            Dictionary<string, List<AudioClip>> voices = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);
            foreach (VideoSceneSpan span in state.Spans) {
                List<AudioClip> voice = Voice(state, span);
                if (voice.Count > 0) {
                    voices[span.Scene.Id] = voice;
                    state.Document.AudioClips.AddRange(voice);
                }
            }
            for (int index = 1; index < state.Spans.Count; index++) {
                VideoScene scene = state.Spans[index].Scene;
                double fade = scene.Entry?.AudioFadeSec ?? 0;
                if (fade <= 0) {
                    continue;
                }
                if (voices.TryGetValue(state.Spans[index - 1].Scene.Id, out List<AudioClip> outgoingClips)) {
                    AudioClip outgoing = outgoingClips[^1];
                    MediaTime length = Clamp(fade, outgoing);
                    outgoing.Envelopes.Add(new AudioEnvelope { Type = "equal_power_out", Start = outgoing.End - outgoing.Start - length, Duration = length, From = 1, To = 0 });
                }
                if (voices.TryGetValue(scene.Id, out List<AudioClip> incomingClips)) {
                    AudioClip incoming = incomingClips[0];
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
        /// Builds the voice clips of a take scene: one per segment, at the take speed. Between two segments the outgoing
        /// clip runs <see cref="JoinHalf"/> past the cut into its unused source and the incoming one starts as much before
        /// it, crossfading with equal power so the jump cut does not click; the overlap shrinks when the media has no
        /// source to spare. Scene envelopes are cut into the part each clip covers.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <returns>Voice clips in playback order; empty when the scene has no take with audio.</returns>
        static List<AudioClip> Voice(VideoCompileState state, VideoSceneSpan span) {
            VideoTake take = span.Scene.Take;
            if (take == null || !state.Media.TryGetValue(take.Media, out VideoMedia media) || !media.HasAudio) {
                return [];
            }
            VideoVoice voice = span.Scene.Voice ?? new VideoVoice();
            List<AudioEnvelope> envelopes = new List<AudioEnvelope>();
            for (int index = 0; index < voice.Envelopes.Count; index++) {
                VideoEnvelope envelope = voice.Envelopes[index];
                MediaTime start = VideoMomentResolver.Resolve(state.Edit, span, envelope.At, $"scenes[{span.Index}].voice.envelopes[{index}].at", state.Diagnostics);
                envelopes.Add(Envelope(envelope, start, span.Duration));
            }
            List<VideoTakePiece> pieces = VideoTakeTiming.Pieces(take);
            double speed = VideoTakeTiming.Speed(take);
            if (pieces.Count <= 1) {
                // One interval: the clip spans the scene, at the take speed.
                AudioClip single = new AudioClip {
                    Id = "voice-" + span.Scene.Id, MediaId = take.Media, Start = span.Start, End = span.End,
                    SourceIn = MediaTime.FromSeconds(take.InSec), SourceOut = MediaTime.FromSeconds(take.OutSec), Gain = voice.Gain, Muted = voice.Muted, Rate = speed
                };
                single.Envelopes.AddRange(envelopes);
                return [single];
            }
            MediaTime mediaEnd = media.DurationSec > 0 ? MediaTime.FromSeconds(media.DurationSec) : default;
            MediaTime[] halves = new MediaTime[pieces.Count];
            halves[0] = MediaTime.Zero;
            for (int index = 1; index < pieces.Count; index++) {
                halves[index] = JoinOverlap(pieces[index - 1], pieces[index], speed, mediaEnd);
            }
            List<AudioClip> clips = new List<AudioClip>();
            for (int index = 0; index < pieces.Count; index++) {
                VideoTakePiece piece = pieces[index];
                MediaTime lead = halves[index], tail = index + 1 < pieces.Count ? halves[index + 1] : MediaTime.Zero;
                MediaTime from = piece.Start - lead;
                if (from >= span.Duration) {
                    break;
                }
                // The last clip reaches the scene end, so a fixed scene longer than the take still reports its missing source.
                MediaTime to = index + 1 < pieces.Count && piece.End + tail < span.Duration ? piece.End + tail : span.Duration;
                if (to == span.Duration) {
                    tail = MediaTime.Zero;
                }
                AudioClip clip = new AudioClip {
                    Id = index == 0 ? "voice-" + span.Scene.Id : "voice-" + span.Scene.Id + "-segment-" + index, MediaId = take.Media,
                    Start = span.Start + from, End = span.Start + to, SourceIn = piece.SourceIn - lead.AtRate(speed), SourceOut = piece.SourceOut + tail.AtRate(speed),
                    Gain = voice.Gain, Muted = voice.Muted, Rate = speed
                };
                MediaTime length = to - from;
                if (lead > MediaTime.Zero) {
                    clip.Envelopes.Add(new AudioEnvelope { Type = "equal_power_in", Start = MediaTime.Zero, Duration = lead + lead, From = 0, To = 1 });
                }
                if (tail > MediaTime.Zero) {
                    clip.Envelopes.Add(new AudioEnvelope { Type = "equal_power_out", Start = length - tail - tail, Duration = tail + tail, From = 1, To = 0 });
                }
                foreach (AudioEnvelope envelope in envelopes) {
                    clip.Envelopes.AddRange(Slice(envelope, from, to));
                }
                clips.Add(clip);
                if (to == span.Duration) {
                    break;
                }
            }
            return clips;
        }

        /// <summary>
        /// Chooses the half overlap of the crossfade at one cut: <see cref="JoinHalf"/>, shortened so the outgoing segment
        /// does not read past the media end, the incoming one does not read before the media start, and fades cover at most
        /// half of either piece.
        /// </summary>
        /// <param name="outgoing">Piece before the cut.</param>
        /// <param name="incoming">Piece after the cut.</param>
        /// <param name="speed">Take speed.</param>
        /// <param name="mediaEnd">Media duration, or an uninitialized value when unknown.</param>
        /// <returns>Half overlap in scene time; zero for a hard cut.</returns>
        static MediaTime JoinOverlap(VideoTakePiece outgoing, VideoTakePiece incoming, double speed, MediaTime mediaEnd) {
            double half = JoinHalf.ToSeconds();
            half = Math.Min(half, incoming.SourceIn.ToSeconds() / speed);
            if (mediaEnd.Denominator > 0) {
                half = Math.Min(half, (mediaEnd - outgoing.SourceOut).ToSeconds() / speed);
            }
            half = Math.Min(half, Math.Min(outgoing.Duration.ToSeconds(), incoming.Duration.ToSeconds()) / 4);
            // Rounded down to whole microseconds and kept a microsecond inside the bounds above, so the extra source read
            // at the take speed never passes them.
            long micros = (long)Math.Floor(half * 1000000) - 1;
            return micros > 0 ? new MediaTime(micros, 1000000) : MediaTime.Zero;
        }

        /// <summary>
        /// Cuts a scene-time envelope to the clip covering <paramref name="from"/> to <paramref name="to"/>, as clip-local
        /// envelopes whose product reproduces the scene gain there. An envelope inside the clip moves as is; one outside
        /// becomes its held gain; a linear one that a cut splits keeps the exact slice; an equal-power one that a cut splits
        /// becomes a chain of <see cref="CurvePieces"/> linear steps (the first ramps between gains, each later one scales
        /// by the ratio of consecutive gains, since envelopes multiply and hold their end values).
        /// </summary>
        /// <param name="envelope">Envelope in scene time.</param>
        /// <param name="from">Clip start in scene time.</param>
        /// <param name="to">Clip end in scene time.</param>
        /// <returns>Clip-local envelopes; empty when the gain is one throughout.</returns>
        static List<AudioEnvelope> Slice(AudioEnvelope envelope, MediaTime from, MediaTime to) {
            MediaTime start = envelope.Start, end = envelope.Start + envelope.Duration, length = to - from;
            MediaTime low = start > from ? start : from, high = end < to ? end : to;
            if (low >= high) {
                double held = envelope.At(from);
                return held == 1 ? [] : [new AudioEnvelope { Type = "linear", Start = MediaTime.Zero, Duration = length, From = held, To = held }];
            }
            if (start >= from && end <= to) {
                return [new AudioEnvelope { Type = envelope.Type, Start = start - from, Duration = envelope.Duration, From = envelope.From, To = envelope.To }];
            }
            if (envelope.Type == "linear") {
                return [new AudioEnvelope { Type = "linear", Start = low - from, Duration = high - low, From = envelope.At(low), To = envelope.At(high) }];
            }
            List<AudioEnvelope> chain = new List<AudioEnvelope>();
            MediaTime span = high - low;
            for (int index = 0; index < CurvePieces; index++) {
                MediaTime a = low + span * new MediaTime(index, CurvePieces), b = low + span * new MediaTime(index + 1, CurvePieces);
                double gainA = envelope.At(a), gainB = envelope.At(b);
                if (index == 0) {
                    chain.Add(new AudioEnvelope { Type = "linear", Start = a - from, Duration = b - a, From = gainA, To = gainB });
                } else if (gainA > 1e-9) {
                    chain.Add(new AudioEnvelope { Type = "linear", Start = a - from, Duration = b - a, From = 1, To = gainB / gainA });
                } else {
                    // The gain already reached zero and the earlier steps hold it there.
                    break;
                }
            }
            return chain;
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

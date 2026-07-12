using NAudio.Wave;

namespace helengine {
    /// <summary>
    /// Implements the shared runtime audio backend contract over Windows wave-out playback.
    /// </summary>
    public sealed class WindowsAudioBackend : IAudioBackend, IDisposable {
        readonly Dictionary<int, WindowsAudioVoice> VoicesById;
        readonly Dictionary<string, float> BusGainsById;
        readonly HashSet<string> PausedBusIds;
        int nextVoiceId;

        /// <summary>
        /// Initializes an empty Windows audio backend.
        /// </summary>
        public WindowsAudioBackend() {
            VoicesById = new Dictionary<int, WindowsAudioVoice>();
            BusGainsById = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase) {
                ["master"] = 1f,
                ["music"] = 1f,
                ["sfx"] = 1f
            };
            PausedBusIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Starts playback of one audio asset and returns its backend voice identifier.
        /// </summary>
        /// <param name="asset">Resolved audio asset to play.</param>
        /// <param name="request">Runtime playback request metadata.</param>
        /// <returns>Backend-owned voice identifier.</returns>
        public int Play(AudioAsset asset, AudioPlaybackRequest request) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            AudioPlaybackRequest effectiveRequest = request ?? new AudioPlaybackRequest();
            string busId = string.IsNullOrWhiteSpace(effectiveRequest.BusId) ? "master" : effectiveRequest.BusId;
            WaveStream stream = CreatePlaybackStream(asset, effectiveRequest);
            WaveOutEvent output = new WaveOutEvent();
            output.Init(stream);

            WindowsAudioVoice voice = new WindowsAudioVoice(output, stream, busId, ClampGain(effectiveRequest.Gain));
            int voiceId = nextVoiceId++;
            VoicesById.Add(voiceId, voice);
            ApplyVoiceState(voice);
            return voiceId;
        }

        /// <summary>
        /// Stops one active voice and releases its playback resources.
        /// </summary>
        /// <param name="voiceId">Backend-owned voice identifier.</param>
        public void Stop(int voiceId) {
            if (!VoicesById.Remove(voiceId, out WindowsAudioVoice voice)) {
                return;
            }

            voice.Dispose();
        }

        /// <summary>
        /// Applies one linear gain value to the supplied bus and all active voices routed through it.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="gain">Linear gain multiplier.</param>
        public void SetBusGain(string busId, float gain) {
            string normalizedBusId = NormalizeBusId(busId);
            BusGainsById[normalizedBusId] = ClampGain(gain);
            foreach (WindowsAudioVoice voice in VoicesById.Values) {
                if (string.Equals(voice.BusId, normalizedBusId, StringComparison.OrdinalIgnoreCase)) {
                    ApplyVoiceVolume(voice);
                }
            }
        }

        /// <summary>
        /// Pauses or resumes one mixer bus and every active voice routed through it.
        /// </summary>
        /// <param name="busId">Stable bus identifier.</param>
        /// <param name="paused">True to pause; false to resume.</param>
        public void SetBusPaused(string busId, bool paused) {
            string normalizedBusId = NormalizeBusId(busId);
            if (paused) {
                PausedBusIds.Add(normalizedBusId);
            } else {
                PausedBusIds.Remove(normalizedBusId);
            }

            foreach (WindowsAudioVoice voice in VoicesById.Values) {
                if (string.Equals(voice.BusId, normalizedBusId, StringComparison.OrdinalIgnoreCase)) {
                    ApplyVoicePlaybackState(voice);
                }
            }
        }

        /// <summary>
        /// Returns whether one voice is still active.
        /// </summary>
        /// <param name="voiceId">Backend-owned voice identifier.</param>
        /// <returns>True when the voice is playing or paused.</returns>
        public bool IsPlaying(int voiceId) {
            return VoicesById.TryGetValue(voiceId, out WindowsAudioVoice voice)
                && voice.Output.PlaybackState != PlaybackState.Stopped;
        }

        /// <summary>
        /// Removes any voices whose playback already completed.
        /// </summary>
        public void Update() {
            if (VoicesById.Count == 0) {
                return;
            }

            List<int> completedVoiceIds = [];
            foreach (KeyValuePair<int, WindowsAudioVoice> pair in VoicesById) {
                if (pair.Value.Output.PlaybackState == PlaybackState.Stopped) {
                    completedVoiceIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < completedVoiceIds.Count; index++) {
                Stop(completedVoiceIds[index]);
            }
        }

        /// <summary>
        /// Stops and disposes all active voices.
        /// </summary>
        public void Dispose() {
            List<int> voiceIds = [.. VoicesById.Keys];
            for (int index = 0; index < voiceIds.Count; index++) {
                Stop(voiceIds[index]);
            }
        }

        /// <summary>
        /// Creates the playback stream used for one request, wrapping it for looping when required.
        /// </summary>
        /// <param name="asset">Resolved audio asset to stream.</param>
        /// <param name="request">Runtime playback request.</param>
        /// <returns>Wave stream used by the output device.</returns>
        static WaveStream CreatePlaybackStream(AudioAsset asset, AudioPlaybackRequest request) {
            RawSourceWaveStream rawStream = CreateRawWaveStream(asset);
            if (request != null && request.Loop) {
                return new LoopingWaveStream(rawStream);
            }

            return rawStream;
        }

        /// <summary>
        /// Creates one raw PCM wave stream from the encoded asset payload.
        /// </summary>
        /// <param name="asset">Resolved audio asset to stream.</param>
        /// <returns>Raw PCM wave stream.</returns>
        static RawSourceWaveStream CreateRawWaveStream(AudioAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }
            if (asset.SampleRate <= 0) {
                throw new InvalidOperationException("Audio assets must declare a positive sample rate before Windows playback can begin.");
            }
            if (asset.Channels <= 0) {
                throw new InvalidOperationException("Audio assets must declare a positive channel count before Windows playback can begin.");
            }

            byte[] pcmBytes = asset.EncodedBytes ?? Array.Empty<byte>();
            MemoryStream memoryStream = new MemoryStream(pcmBytes, writable: false);
            WaveFormat waveFormat = new WaveFormat(asset.SampleRate, 16, asset.Channels);
            return new RawSourceWaveStream(memoryStream, waveFormat);
        }

        void ApplyVoiceState(WindowsAudioVoice voice) {
            ApplyVoiceVolume(voice);
            ApplyVoicePlaybackState(voice);
        }

        void ApplyVoiceVolume(WindowsAudioVoice voice) {
            float busGain = BusGainsById.TryGetValue(voice.BusId, out float configuredBusGain)
                ? configuredBusGain
                : 1f;
            voice.Output.Volume = ClampGain(voice.BaseGain * busGain);
        }

        void ApplyVoicePlaybackState(WindowsAudioVoice voice) {
            bool paused = PausedBusIds.Contains(voice.BusId);
            if (paused) {
                if (voice.Output.PlaybackState == PlaybackState.Playing) {
                    voice.Output.Pause();
                } else if (voice.Output.PlaybackState == PlaybackState.Stopped) {
                    voice.Output.Play();
                    voice.Output.Pause();
                }
                return;
            }

            if (voice.Output.PlaybackState != PlaybackState.Playing) {
                voice.Output.Play();
            }
        }

        static string NormalizeBusId(string busId) {
            return string.IsNullOrWhiteSpace(busId) ? "master" : busId;
        }

        static float ClampGain(float gain) {
            if (float.IsNaN(gain) || float.IsInfinity(gain)) {
                return 1f;
            }

            return Math.Clamp(gain, 0f, 1f);
        }

        /// <summary>
        /// Rewinds a source stream automatically when NAudio reaches its end.
        /// </summary>
        sealed class LoopingWaveStream : WaveStream {
            readonly WaveStream Source;

            public LoopingWaveStream(WaveStream source) {
                Source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public override WaveFormat WaveFormat => Source.WaveFormat;

            public override long Length => Source.Length;

            public override long Position {
                get => Source.Position;
                set => Source.Position = value;
            }

            public override int Read(byte[] buffer, int offset, int count) {
                int totalBytesRead = 0;
                while (totalBytesRead < count) {
                    int bytesRead = Source.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0) {
                        Source.Position = 0;
                        continue;
                    }

                    totalBytesRead += bytesRead;
                }

                return totalBytesRead;
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                    Source.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}

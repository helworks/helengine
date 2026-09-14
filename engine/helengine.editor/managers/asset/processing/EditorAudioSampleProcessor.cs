namespace helengine.editor {
    /// <summary>
    /// Performs target-independent PCM validation, channel conversion, resampling, and payload encoding.
    /// </summary>
    public sealed class EditorAudioSampleProcessor {
        /// <summary>
        /// Registered payload encoders keyed by their stable family identifiers.
        /// </summary>
        readonly Dictionary<string, IEditorAudioPayloadEncoder> EncodersById;

        /// <summary>
        /// Creates a processor with the standard PCM and Nintendo DS encoders.
        /// </summary>
        public EditorAudioSampleProcessor()
            : this([
                new Pcm16AudioPayloadEncoder(),
                new NintendoDsImaAdpcmAudioPayloadEncoder()]) {
        }

        /// <summary>
        /// Creates a processor with explicitly registered payload encoders.
        /// </summary>
        /// <param name="encoders">Payload encoders available to processor settings.</param>
        public EditorAudioSampleProcessor(IEnumerable<IEditorAudioPayloadEncoder> encoders) {
            if (encoders == null) {
                throw new ArgumentNullException(nameof(encoders));
            }

            EncodersById = new Dictionary<string, IEditorAudioPayloadEncoder>(StringComparer.OrdinalIgnoreCase);
            foreach (IEditorAudioPayloadEncoder encoder in encoders) {
                if (encoder == null) {
                    throw new ArgumentException("Encoder entries must be non-null.", nameof(encoders));
                }
                if (string.IsNullOrWhiteSpace(encoder.EncodingFamilyId)) {
                    throw new ArgumentException("Encoder family identifiers must be non-empty.", nameof(encoders));
                }
                if (EncodersById.ContainsKey(encoder.EncodingFamilyId)) {
                    throw new InvalidOperationException($"Audio encoder '{encoder.EncodingFamilyId}' is already registered.");
                }

                EncodersById.Add(encoder.EncodingFamilyId, encoder);
            }

            RegisterPcmAliases();
        }

        /// <summary>
        /// Decodes a little-endian PCM16 payload into signed samples after validating complete frames.
        /// </summary>
        /// <param name="pcm16Bytes">PCM16 payload bytes.</param>
        /// <param name="sourceChannels">Declared source channel count.</param>
        /// <returns>Interleaved signed PCM16 samples.</returns>
        public short[] DecodePcm16Samples(byte[] pcm16Bytes, ushort sourceChannels) {
            if (pcm16Bytes == null) {
                throw new ArgumentNullException(nameof(pcm16Bytes));
            } else if (sourceChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceChannels), "Source channel count must be positive.");
            } else if ((pcm16Bytes.Length % sizeof(short)) != 0) {
                throw new InvalidOperationException("Audio importers must provide a PCM16 payload aligned to 16-bit sample boundaries.");
            }

            int sampleCount = pcm16Bytes.Length / sizeof(short);
            if ((sampleCount % sourceChannels) != 0) {
                throw new InvalidOperationException("Audio importers must provide full PCM16 frames for the declared channel count.");
            }

            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcm16Bytes, 0, samples, 0, pcm16Bytes.Length);
            return samples;
        }

        /// <summary>
        /// Converts interleaved samples between the supported mono and multi-channel layouts.
        /// </summary>
        /// <param name="sourceSamples">Source samples.</param>
        /// <param name="sourceChannels">Source channel count.</param>
        /// <param name="targetChannels">Requested channel count.</param>
        /// <returns>Channel-adjusted samples.</returns>
        public short[] ConvertAudioChannels(short[] sourceSamples, ushort sourceChannels, ushort targetChannels) {
            if (sourceSamples == null) {
                throw new ArgumentNullException(nameof(sourceSamples));
            } else if (sourceChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceChannels), "Source channel count must be positive.");
            } else if (targetChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetChannels), "Target channel count must be positive.");
            }

            if (sourceSamples.Length % sourceChannels != 0) {
                throw new InvalidOperationException("Audio samples must contain complete source frames.");
            }
            if (sourceChannels == targetChannels) {
                return sourceSamples;
            }

            int sourceFrameCount = sourceSamples.Length / sourceChannels;
            short[] convertedSamples = new short[sourceFrameCount * targetChannels];
            if (targetChannels == 1) {
                for (int frameIndex = 0; frameIndex < sourceFrameCount; frameIndex++) {
                    int sourceFrameOffset = frameIndex * sourceChannels;
                    int summedSample = 0;
                    for (int channelIndex = 0; channelIndex < sourceChannels; channelIndex++) {
                        summedSample += sourceSamples[sourceFrameOffset + channelIndex];
                    }
                    convertedSamples[frameIndex] = ClampToInt16(Math.Round(summedSample / (double)sourceChannels));
                }
                return convertedSamples;
            }

            if (sourceChannels == 1) {
                for (int frameIndex = 0; frameIndex < sourceFrameCount; frameIndex++) {
                    short sample = sourceSamples[frameIndex];
                    int targetFrameOffset = frameIndex * targetChannels;
                    for (int channelIndex = 0; channelIndex < targetChannels; channelIndex++) {
                        convertedSamples[targetFrameOffset + channelIndex] = sample;
                    }
                }
                return convertedSamples;
            }

            throw new InvalidOperationException($"Audio channel conversion from {sourceChannels} to {targetChannels} is not implemented.");
        }

        /// <summary>
        /// Resamples interleaved PCM16 samples with linear interpolation per channel.
        /// </summary>
        /// <param name="sourceSamples">Source samples.</param>
        /// <param name="channelCount">Channel count.</param>
        /// <param name="sourceSampleRate">Source sample rate.</param>
        /// <param name="targetSampleRate">Target sample rate.</param>
        /// <returns>Resampled samples.</returns>
        public short[] ResampleAudioSamples(short[] sourceSamples, ushort channelCount, int sourceSampleRate, int targetSampleRate) {
            if (sourceSamples == null) {
                throw new ArgumentNullException(nameof(sourceSamples));
            } else if (channelCount == 0) {
                throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be positive.");
            } else if (sourceSampleRate <= 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceSampleRate), "Source sample rate must be positive.");
            } else if (targetSampleRate <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetSampleRate), "Target sample rate must be positive.");
            } else if (sourceSamples.Length % channelCount != 0) {
                throw new InvalidOperationException("Audio samples must contain complete source frames.");
            }

            if (sourceSampleRate == targetSampleRate || sourceSamples.Length == 0) {
                return sourceSamples;
            }

            int sourceFrameCount = sourceSamples.Length / channelCount;
            if (sourceFrameCount == 0) {
                return Array.Empty<short>();
            }

            int targetFrameCount = Math.Max(1, (int)Math.Round(sourceFrameCount * (double)targetSampleRate / sourceSampleRate));
            short[] resampledSamples = new short[targetFrameCount * channelCount];
            for (int targetFrameIndex = 0; targetFrameIndex < targetFrameCount; targetFrameIndex++) {
                double sourceFramePosition = targetFrameIndex * (double)sourceSampleRate / targetSampleRate;
                int leftFrameIndex = Math.Min(sourceFrameCount - 1, (int)Math.Floor(sourceFramePosition));
                int rightFrameIndex = Math.Min(sourceFrameCount - 1, leftFrameIndex + 1);
                double blend = sourceFramePosition - leftFrameIndex;
                int targetFrameOffset = targetFrameIndex * channelCount;
                int leftFrameOffset = leftFrameIndex * channelCount;
                int rightFrameOffset = rightFrameIndex * channelCount;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++) {
                    double leftSample = sourceSamples[leftFrameOffset + channelIndex];
                    double rightSample = sourceSamples[rightFrameOffset + channelIndex];
                    resampledSamples[targetFrameOffset + channelIndex] = ClampToInt16(Math.Round(leftSample + ((rightSample - leftSample) * blend)));
                }
            }
            return resampledSamples;
        }

        /// <summary>
        /// Encodes processed samples using a registered family identifier.
        /// </summary>
        /// <param name="samples">Processed samples.</param>
        /// <param name="encodingFamilyId">Requested encoding family.</param>
        /// <returns>Encoded payload bytes.</returns>
        public byte[] EncodeProcessedAudioPayload(short[] samples, string encodingFamilyId) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            } else if (string.IsNullOrWhiteSpace(encodingFamilyId)) {
                throw new ArgumentException("Encoding family id must be provided.", nameof(encodingFamilyId));
            }
            if (!EncodersById.TryGetValue(encodingFamilyId, out IEditorAudioPayloadEncoder encoder)) {
                throw new InvalidOperationException($"Audio encoding family '{encodingFamilyId}' is not registered.");
            }
            return encoder.Encode(samples);
        }

        /// <summary>
        /// Adds the historic PCM profile aliases while retaining a single implementation.
        /// </summary>
        void RegisterPcmAliases() {
            if (!EncodersById.TryGetValue("pcm", out IEditorAudioPayloadEncoder pcmEncoder)) {
                return;
            }
            EncodersById["pcm-streamed"] = pcmEncoder;
            EncodersById["pcm-buffered"] = pcmEncoder;
        }

        /// <summary>
        /// Clamps a floating-point sample value into the signed PCM16 range.
        /// </summary>
        /// <param name="value">Sample value.</param>
        /// <returns>Clamped sample.</returns>
        static short ClampToInt16(double value) {
            if (value < short.MinValue) {
                return short.MinValue;
            }
            if (value > short.MaxValue) {
                return short.MaxValue;
            }
            return (short)value;
        }
    }
}

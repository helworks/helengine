using NAudio.Wave;

namespace helengine.editor {
    /// <summary>
    /// Imports authored WAV and MP3 sources by decoding them to PCM16.
    /// </summary>
    public sealed class NAudioSourceAudioImporter : IAudioImporter {
        /// <summary>
        /// Imports one authored audio source from a stream and returns decoded PCM metadata.
        /// </summary>
        /// <param name="stream">Source stream containing WAV or MP3 audio.</param>
        /// <returns>Decoded audio payload and metadata.</returns>
        public ImportedAudioSource ImportAudio(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using WaveStream sourceStream = CreateSourceStream(stream);
            using WaveStream pcmStream = WaveFormatConversionStream.CreatePcmStream(sourceStream);
            using MemoryStream sampleBuffer = new MemoryStream();
            pcmStream.CopyTo(sampleBuffer);

            return new ImportedAudioSource {
                Channels = checked((ushort)pcmStream.WaveFormat.Channels),
                SampleRate = pcmStream.WaveFormat.SampleRate,
                DurationSeconds = (float)pcmStream.TotalTime.TotalSeconds,
                Pcm16Bytes = sampleBuffer.ToArray()
            };
        }

        /// <summary>
        /// Creates the appropriate NAudio reader for the supplied source stream.
        /// </summary>
        /// <param name="stream">Seekable source stream positioned at the start of the authored bytes.</param>
        /// <returns>Concrete reader for one supported source format.</returns>
        static WaveStream CreateSourceStream(Stream stream) {
            if (!stream.CanSeek) {
                throw new InvalidOperationException("Audio source streams must support seeking.");
            }

            try {
                stream.Position = 0;
                return new WaveFileReader(stream);
            } catch (Exception exception) when (exception is FormatException || exception is InvalidDataException) {
                stream.Position = 0;
                return new Mp3FileReader(stream);
            }
        }
    }
}

using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies audio import-manager behavior for typed sidecars and cached audio assets.
    /// </summary>
    public sealed class AudioAssetImportManagerTests : IDisposable {
        /// <summary>
        /// Temporary project root used for each isolated test case.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary project assets root used for source audio files and sidecars.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Temporary cache root used for imported audio outputs.
        /// </summary>
        readonly string CacheRootPath;

        /// <summary>
        /// Initializes isolated project directories for each test case.
        /// </summary>
        public AudioAssetImportManagerTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-audio-asset-import-manager-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheRootPath = Path.Combine(ProjectRootPath, "cache");
            Directory.CreateDirectory(AssetsRootPath);
            Directory.CreateDirectory(CacheRootPath);
        }

        /// <summary>
        /// Deletes the temporary project directories after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures source audio files import into cached audio assets through typed audio sidecars and platform processor settings.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenSourceAudioExists_ImportsAndCachesAudioAsset() {
            string sourcePath = WriteSourceAudio("audio/menu/theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "windows";

            AudioAssetImportSettings settings = manager.LoadOrCreateAudioImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new AudioAssetProcessorSettings {
                PlaybackMode = AudioPlaybackMode.Streamed,
                EncodingFamilyId = "pcm-streamed",
                DefaultLoop = true,
                DefaultBusId = "music",
                StreamChunkByteSize = 4
            };
            manager.SaveAudioImportSettings(sourcePath, settings);

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(AudioPlaybackMode.Streamed, asset.PlaybackMode);
            Assert.True(asset.DefaultLoop);
            Assert.Equal("music", asset.DefaultBusId);
            Assert.Equal(2, asset.Channels);
            Assert.Equal(44100, asset.SampleRate);
            Assert.Equal(3.5f, asset.DurationSeconds);
            Assert.Equal("pcm-streamed", asset.EncodingFamilyId);
            Assert.Equal([1, 2, 3, 4], asset.EncodedBytes);

            AudioChunkDescriptor chunk = Assert.Single(asset.Chunks);
            Assert.Equal(0, chunk.ByteOffset);
            Assert.Equal(4, chunk.ByteLength);

            AudioAssetImportSettings resolvedSettings = manager.LoadOrCreateAudioImportSettings(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, resolvedSettings.Importer.AssetId);
            Assert.True(File.Exists(outputPath));
        }

        /// <summary>
        /// Ensures the PSP default audio cook profile downsamples unconfigured imported music into a smaller mono PCM payload that can fit constrained memory budgets.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsPsp_AppliesDefaultDownmixAndResample() {
            string sourcePath = WriteSourceAudio("audio/menu/psp-theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestResamplingAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "psp";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(11025, asset.SampleRate);
            Assert.Equal(2, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3], asset.EncodedBytes);
            Assert.Equal(1f / 11025f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Ensures the PS2 default audio cook profile aggressively downsamples long music tracks into a mono streamed PCM payload that fits the current EE memory budget.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsPs2_AppliesDefaultMonoLowRateResampleAndChunkBudget() {
            string sourcePath = WriteSourceAudio("audio/menu/ps2-theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestResamplingAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "ps2";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(AudioPlaybackMode.Streamed, asset.PlaybackMode);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(4000, asset.SampleRate);
            Assert.Equal(2, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3], asset.EncodedBytes);
            Assert.Equal(4f / 44100f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Ensures the Wii default audio cook profile downsamples unconfigured imported music into a smaller mono PCM payload that fits the runtime memory budget.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsWii_AppliesDefaultDownmixAndResample() {
            string sourcePath = WriteSourceAudio("audio/menu/wii-theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestResamplingAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "wii";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(11025, asset.SampleRate);
            Assert.Equal(2, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3], asset.EncodedBytes);
            Assert.Equal(1f / 11025f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Ensures the GameCube default audio cook profile downsamples unconfigured imported music into a smaller mono PCM payload that fits the runtime memory budget.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsGameCube_AppliesDefaultDownmixAndResample() {
            string sourcePath = WriteSourceAudio("audio/menu/gamecube-theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestResamplingAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "gamecube";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(11025, asset.SampleRate);
            Assert.Equal(2, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3], asset.EncodedBytes);
            Assert.Equal(1f / 11025f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Ensures the DS default audio cook profile downsamples unconfigured imported music into a smaller mono buffered payload that fits the runtime memory budget.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsDs_AppliesDefaultDownmixResampleAndBufferedAdpcmEncoding() {
            string sourcePath = WriteSourceAudio("audio/menu/ds-theme.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestResamplingAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "ds";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(AudioPlaybackMode.Predecoded, asset.PlaybackMode);
            Assert.Equal("adpcm-buffered", asset.EncodingFamilyId);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(11025, asset.SampleRate);
            Assert.Equal(4, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3, 0, 0], asset.EncodedBytes);
            Assert.Equal(1f / 11025f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Ensures the DS audio cook path emits real IMA ADPCM nibble data beyond the 4-byte predictor header.
        /// </summary>
        [Fact]
        public void TryLoadAudioAsset_WhenCurrentPlatformIsDs_EncodesBufferedAdpcmPayloadNibbles() {
            string sourcePath = WriteSourceAudio("audio/menu/ds-theme-adpcm.wav");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterAudioImporter(new AudioImporterRegistration("test-audio", new TestNintendoDsAdpcmAudioImporter(), [".wav"]));
            manager.CurrentPlatformId = "ds";

            bool loaded = manager.TryLoadAudioAsset(sourcePath, out AudioAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(AudioPlaybackMode.Predecoded, asset.PlaybackMode);
            Assert.Equal("adpcm-buffered", asset.EncodingFamilyId);
            Assert.Equal((ushort)1, asset.Channels);
            Assert.Equal(11025, asset.SampleRate);
            Assert.Equal(5, Assert.Single(asset.Chunks).ByteLength);
            Assert.Equal([232, 3, 0, 0, 7], asset.EncodedBytes);
            Assert.Equal(2f / 11025f, asset.DurationSeconds, 6);
        }

        /// <summary>
        /// Writes one minimal source audio file for importer tests.
        /// </summary>
        /// <param name="relativePath">Asset-relative source file path.</param>
        /// <returns>Absolute path to the created source file.</returns>
        string WriteSourceAudio(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string sourcePath = Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Source audio directory could not be resolved.");
            Directory.CreateDirectory(directoryPath);
            File.WriteAllBytes(sourcePath, [1, 2, 3, 4]);
            return sourcePath;
        }

        /// <summary>
        /// Provides deterministic imported audio metadata for test coverage.
        /// </summary>
        sealed class TestAudioImporter : IAudioImporter {
            /// <summary>
            /// Imports one source stream into deterministic PCM metadata.
            /// </summary>
            /// <param name="stream">Source stream containing authored audio bytes.</param>
            /// <returns>Imported audio metadata and PCM payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                return new ImportedAudioSource {
                    Channels = 2,
                    SampleRate = 44100,
                    DurationSeconds = 3.5f,
                    Pcm16Bytes = [1, 2, 3, 4]
                };
            }
        }

        /// <summary>
        /// Provides deterministic stereo PCM metadata that exercises channel and sample-rate conversion.
        /// </summary>
        sealed class TestResamplingAudioImporter : IAudioImporter {
            /// <summary>
            /// Imports one source stream into deterministic stereo PCM metadata for resampling coverage.
            /// </summary>
            /// <param name="stream">Source stream containing authored audio bytes.</param>
            /// <returns>Imported audio metadata and PCM payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                short[] samples = [
                    1000, 1000,
                    2000, 2000,
                    3000, 3000,
                    4000, 4000
                ];
                byte[] pcm16Bytes = new byte[samples.Length * sizeof(short)];
                Buffer.BlockCopy(samples, 0, pcm16Bytes, 0, pcm16Bytes.Length);
                return new ImportedAudioSource {
                    Channels = 2,
                    SampleRate = 44100,
                    DurationSeconds = 4f / 44100f,
                    Pcm16Bytes = pcm16Bytes
                };
            }
        }

        /// <summary>
        /// Provides deterministic mono PCM metadata that exercises DS ADPCM nibble emission without resampling.
        /// </summary>
        sealed class TestNintendoDsAdpcmAudioImporter : IAudioImporter {
            /// <summary>
            /// Imports one source stream into deterministic mono PCM metadata for ADPCM coverage.
            /// </summary>
            /// <param name="stream">Source stream containing authored audio bytes.</param>
            /// <returns>Imported audio metadata and PCM payload.</returns>
            public ImportedAudioSource ImportAudio(Stream stream) {
                if (stream == null) {
                    throw new ArgumentNullException(nameof(stream));
                }

                short[] samples = [
                    1000,
                    2000
                ];
                byte[] pcm16Bytes = new byte[samples.Length * sizeof(short)];
                Buffer.BlockCopy(samples, 0, pcm16Bytes, 0, pcm16Bytes.Length);
                return new ImportedAudioSource {
                    Channels = 1,
                    SampleRate = 11025,
                    DurationSeconds = 2f / 11025f,
                    Pcm16Bytes = pcm16Bytes
                };
            }
        }
    }
}

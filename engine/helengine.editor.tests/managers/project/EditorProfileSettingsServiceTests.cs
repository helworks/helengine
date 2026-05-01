using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies editor-local platform profile settings persistence.
    /// </summary>
    public sealed class EditorProfileSettingsServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the profile-settings tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated project root for the profile-settings tests.
        /// </summary>
        public EditorProfileSettingsServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-profile-settings-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a missing profile file seeds one default profile per supported platform.
        /// </summary>
        [Fact]
        public void Load_WhenProfileFileIsMissing_SeedsDefaultBuildAndGraphicsProfilesForEachSupportedPlatform() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new List<string> { "windows", "ps2" });

            Assert.Equal(2, document.Platforms.Count);
            Assert.Equal("windows", document.Platforms[0].PlatformId);
            Assert.Equal(100, document.Platforms[0].Build.TextureScalePercent);
            Assert.True(document.Platforms[0].Graphics.VSyncEnabled);
            Assert.Equal("ps2", document.Platforms[1].PlatformId);
        }

        /// <summary>
        /// Ensures saved profile values survive a reload.
        /// </summary>
        [Fact]
        public void SaveAndReload_PreservesPlatformSpecificBuildAndGraphicsProfileValues() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
            EditorProfileSettingsDocument document = new EditorProfileSettingsDocument {
                Platforms = new List<EditorPlatformProfileSettingsDocument> {
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "windows",
                        Build = new EditorBuildProfileSettingsDocument {
                            TextureScalePercent = 75,
                            ShaderVariantPruningEnabled = false
                        },
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            DefaultWidth = 1920,
                            DefaultHeight = 1080,
                            VSyncEnabled = false,
                            FullscreenEnabled = true
                        }
                    }
                }
            };

            service.Save(document);

            EditorProfileSettingsDocument reloaded = service.Load(new List<string> { "windows" });
            Assert.Equal(75, reloaded.Platforms[0].Build.TextureScalePercent);
            Assert.False(reloaded.Platforms[0].Graphics.VSyncEnabled);
            Assert.True(reloaded.Platforms[0].Graphics.FullscreenEnabled);
        }
    }
}

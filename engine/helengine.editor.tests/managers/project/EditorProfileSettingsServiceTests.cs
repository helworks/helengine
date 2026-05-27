using helengine.editor;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies project-shared platform profile settings persistence.
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
        /// Ensures a missing settings directory seeds one default profile per supported platform.
        /// </summary>
        [Fact]
        public void Load_WhenPlatformFilesAreMissing_SeedsDefaultBuildAndGraphicsProfilesForEachSupportedPlatform() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new List<string> { "windows", "ps2" });

            Assert.Equal(2, document.Platforms.Count);
            Assert.Equal("windows", document.Platforms[0].PlatformId);
            Assert.Equal(100, document.Platforms[0].Build.TextureScalePercent);
            Assert.True(document.Platforms[0].Graphics.VSyncEnabled);
            Assert.Equal("ps2", document.Platforms[1].PlatformId);
            Assert.True(File.Exists(Path.Combine(TempRootPath, "settings", "platform.windows.json")));
            Assert.True(File.Exists(Path.Combine(TempRootPath, "settings", "platform.ps2.json")));
        }

        /// <summary>
        /// Ensures saving one multi-platform document writes one file per platform.
        /// </summary>
        [Fact]
        public void Save_WhenOnePlatformChanges_WritesOnlyPerPlatformFiles() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
            EditorProfileSettingsDocument document = CreateProfileDocument("windows", "ps2");

            service.Save(document);

            Assert.True(File.Exists(Path.Combine(TempRootPath, "settings", "platform.windows.json")));
            Assert.True(File.Exists(Path.Combine(TempRootPath, "settings", "platform.ps2.json")));
            Assert.False(File.Exists(Path.Combine(TempRootPath, "user_settings", "profile_config.json")));
        }

        /// <summary>
        /// Ensures the service ignores the older combined profile document under `user_settings/profile_config.json`.
        /// </summary>
        [Fact]
        public void Load_WhenLegacyCombinedProfileDocumentExists_IgnoresItAndSeedsCurrentPlatformFiles() {
            Directory.CreateDirectory(Path.Combine(TempRootPath, "user_settings"));
            File.WriteAllText(
                Path.Combine(TempRootPath, "user_settings", "profile_config.json"),
                """
                {
                  "platforms": [
                    {
                      "platformId": "windows",
                      "build": {
                        "textureScalePercent": 25
                      }
                    }
                  ]
                }
                """);
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new List<string> { "windows" });

            Assert.Single(document.Platforms);
            Assert.Equal("windows", document.Platforms[0].PlatformId);
            Assert.Equal(100, document.Platforms[0].Build.TextureScalePercent);
            Assert.True(File.Exists(Path.Combine(TempRootPath, "user_settings", "profile_config.json")));
            Assert.True(File.Exists(Path.Combine(TempRootPath, "settings", "platform.windows.json")));
        }

        /// <summary>
        /// Ensures loading one supported subset leaves an unavailable platform file untouched on disk.
        /// </summary>
        [Fact]
        public void Load_WhenSupportedPlatformIsUnavailable_LeavesItsFileUntouched() {
            SeedPlatformProfileFile("windows", "50");
            SeedPlatformProfileFile("ps2", "75");
            string ps2FilePath = Path.Combine(TempRootPath, "settings", "platform.ps2.json");
            string originalPs2Json = File.ReadAllText(ps2FilePath);
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new[] { "windows" });

            Assert.Single(document.Platforms);
            Assert.Equal("windows", document.Platforms[0].PlatformId);
            Assert.True(File.Exists(ps2FilePath));
            Assert.Equal(originalPs2Json, File.ReadAllText(ps2FilePath));
        }

        /// <summary>
        /// Ensures saved profile values survive a reload through per-platform files.
        /// </summary>
        [Fact]
        public void SaveAndReload_PreservesPlatformSpecificBuildAndGraphicsProfileValues() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
            EditorProfileSettingsDocument document = CreateProfileDocument("windows");

            service.Save(document);

            EditorProfileSettingsDocument reloaded = service.Load(new List<string> { "windows" });
            Assert.Equal(75, reloaded.Platforms[0].Build.TextureScalePercent);
            Assert.False(reloaded.Platforms[0].Graphics.VSyncEnabled);
            Assert.True(reloaded.Platforms[0].Graphics.FullscreenEnabled);
        }

        /// <summary>
        /// Ensures one missing DS platform file seeds standard platform action mappings.
        /// </summary>
        [Fact]
        public void Load_WhenDsPlatformFileIsMissing_SeedsStandardPlatformActions() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new List<string> { "ds" });

            EditorPlatformProfileSettingsDocument platform = Assert.Single(document.Platforms);
            Assert.Equal("ds", platform.PlatformId);
            Assert.NotNull(platform.Input);
            Assert.NotNull(platform.Input.StandardActions);
            Assert.NotNull(platform.Input.StandardActions.Accept);
            Assert.NotNull(platform.Input.StandardActions.Return);
        }

        /// <summary>
        /// Ensures one missing GameCube platform file seeds standard platform action mappings.
        /// </summary>
        [Fact]
        public void Load_WhenGameCubePlatformFileIsMissing_SeedsStandardPlatformActions() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);

            EditorProfileSettingsDocument document = service.Load(new List<string> { "gamecube" });

            EditorPlatformProfileSettingsDocument platform = Assert.Single(document.Platforms);
            Assert.Equal("gamecube", platform.PlatformId);
            Assert.NotNull(platform.Input);
            Assert.NotNull(platform.Input.StandardActions);
            Assert.NotNull(platform.Input.StandardActions.Accept);
            Assert.NotNull(platform.Input.StandardActions.Return);
            Assert.Equal(0, platform.Input.StandardActions.Accept.ControlIndex);
            Assert.Equal(1, platform.Input.StandardActions.Return.ControlIndex);
        }

        /// <summary>
        /// Ensures configured standard platform actions survive save and reload.
        /// </summary>
        [Fact]
        public void SaveAndReload_WhenStandardPlatformActionsAreConfigured_PreservesInputSectionValues() {
            EditorProfileSettingsService service = new EditorProfileSettingsService(TempRootPath);
            EditorProfileSettingsDocument document = CreateProfileDocument("ps2");

            document.Platforms[0].Input.StandardActions.Accept.ControlIndex = 0;
            document.Platforms[0].Input.StandardActions.Return.ControlIndex = 3;
            service.Save(document);

            EditorProfileSettingsDocument reloaded = service.Load(new List<string> { "ps2" });
            Assert.Equal(0, reloaded.Platforms[0].Input.StandardActions.Accept.ControlIndex);
            Assert.Equal(3, reloaded.Platforms[0].Input.StandardActions.Return.ControlIndex);
        }

        /// <summary>
        /// Creates one multi-platform profile document with stable values for the supplied platforms.
        /// </summary>
        /// <param name="platformIds">Platform identifiers that should be included in the document.</param>
        /// <returns>Profile document containing one record per requested platform.</returns>
        EditorProfileSettingsDocument CreateProfileDocument(params string[] platformIds) {
            List<EditorPlatformProfileSettingsDocument> platforms = new List<EditorPlatformProfileSettingsDocument>(platformIds.Length);

            for (int index = 0; index < platformIds.Length; index++) {
                string platformId = platformIds[index];
                platforms.Add(new EditorPlatformProfileSettingsDocument {
                    PlatformId = platformId,
                    Build = new EditorBuildProfileSettingsDocument {
                        TextureScalePercent = index == 0 ? 75 : 50,
                        ShaderVariantPruningEnabled = index != 0
                    },
                    Graphics = new EditorGraphicsProfileSettingsDocument {
                        DefaultWidth = 1920,
                        DefaultHeight = 1080,
                        VSyncEnabled = false,
                        FullscreenEnabled = true
                    },
                    Codegen = new EditorCodegenProfileSettingsDocument {
                        SelectedCodegenProfileId = "default"
                    },
                    Input = new EditorInputProfileSettingsDocument {
                        StandardActions = new EditorStandardPlatformActionSettingsDocument {
                            Accept = new EditorInputControlSettingsDocument(),
                            Return = new EditorInputControlSettingsDocument()
                        }
                    }
                });
            }

            return new EditorProfileSettingsDocument {
                Platforms = platforms
            };
        }

        /// <summary>
        /// Seeds one per-platform profile file with the supplied texture scale value.
        /// </summary>
        /// <param name="platformId">Platform identifier whose file should be written.</param>
        /// <param name="textureScalePercent">Texture scale value persisted into the file.</param>
        void SeedPlatformProfileFile(string platformId, string textureScalePercent) {
            Directory.CreateDirectory(Path.Combine(TempRootPath, "settings"));
            File.WriteAllText(
                Path.Combine(TempRootPath, "settings", $"platform.{platformId}.json"),
                $$"""
                {
                  "platformId": "{{platformId}}",
                  "build": {
                    "selectedBuildProfileId": "",
                    "textureScalePercent": {{textureScalePercent}},
                    "shaderVariantPruningEnabled": true,
                    "selectedOptionValues": {}
                  },
                  "graphics": {
                    "selectedGraphicsProfileId": "",
                    "defaultWidth": 1280,
                    "defaultHeight": 720,
                    "vSyncEnabled": true,
                    "fullscreenEnabled": false,
                    "rendererDepthPrepassMode": 0,
                    "rendererShadowQualityTier": "medium",
                    "rendererHdrEnabled": true,
                    "rendererPostProcessTier": 2,
                    "selectedOptionValues": {}
                  },
                  "codegen": {
                    "selectedCodegenProfileId": "",
                    "selectedOptionValues": {}
                  }
                }
                """);
        }
    }
}

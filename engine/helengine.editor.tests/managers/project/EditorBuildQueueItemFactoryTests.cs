using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies queued-build snapshot creation uses builder metadata defaults and preserves scene ordering.
    /// </summary>
    public sealed class EditorBuildQueueItemFactoryTests : IDisposable {
        /// <summary>
        /// Gets the isolated temporary project root used by the current test instance.
        /// </summary>
        string TempProjectRootPath { get; }

        /// <summary>
        /// Initializes one isolated temporary project root for queue-item factory tests.
        /// </summary>
        public EditorBuildQueueItemFactoryTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-queue-factory-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));
        }

        /// <summary>
        /// Deletes the isolated temporary project root after the current test completes.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one queued Windows build snapshot seeds builder defaults and inserts the generated boot scene before the ordered authored scenes.
        /// </summary>
        [Fact]
        public void Create_WhenPlatformConfigOmitsProfileSelections_SeedsDefaultsAndOrdersScenes() {
            WriteScene("Scenes/A.helen");
            WriteScene("Scenes/B.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "windows",
                SelectedSceneIds = [
                    "B",
                    "A"
                ],
                SceneOrders = [
                    new EditorBuildSceneOrderDocument {
                        SceneId = "A",
                        OrderNumber = 2
                    },
                    new EditorBuildSceneOrderDocument {
                        SceneId = "B",
                        OrderNumber = 1
                    }
                ]
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal(new[] { "GeneratedBootScene", "B", "A" }, queueItem.SelectedSceneIds);
            Assert.Equal("debug", queueItem.SelectedBuildProfileId);
            Assert.Equal("directx11", queueItem.SelectedGraphicsProfileId);
            Assert.Equal("default", queueItem.SelectedCodegenProfileId);
            Assert.Equal("loose-files", queueItem.SelectedStorageProfileId);
            Assert.Equal("windows-install-tree", queueItem.SelectedMediaProfileId);
            Assert.Equal("100", queueItem.SelectedBuildOptionValues["texture-scale-percent"]);
            Assert.Equal("true", queueItem.SelectedBuildOptionValues["shader-variant-pruning"]);
            Assert.Equal("1280", queueItem.SelectedGraphicsOptionValues["default-width"]);
            Assert.Equal("720", queueItem.SelectedGraphicsOptionValues["default-height"]);
            Assert.Equal("true", queueItem.SelectedGraphicsOptionValues["vsync-enabled"]);
            Assert.Equal("false", queueItem.SelectedGraphicsOptionValues["fullscreen-enabled"]);
            Assert.Equal("true", queueItem.SelectedCodegenOptionValues["write-conversion-report"]);
            Assert.Equal("windows-no-shaders", queueItem.SelectedCodegenOptionValues[PlatformCodegenSettingIds.PresetId]);
        }

        /// <summary>
        /// Ensures PS2 graphics profiles seed the depth-handler choice with the hardware default.
        /// </summary>
        [Fact]
        public void Create_WhenPs2GraphicsProfileOmitsDepthHandlerMode_SeedsHardwareDefault() {
            WriteScene("Scenes/A.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "ps2",
                SelectedSceneIds = [
                    "A"
                ]
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreatePs2SelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal("ps2-default", queueItem.SelectedBuildProfileId);
            Assert.Equal("ps2-standard-forward", queueItem.SelectedGraphicsProfileId);
            Assert.Equal("hardware", queueItem.SelectedGraphicsOptionValues["depth-handler-mode"]);
        }

        /// <summary>
        /// Ensures one blank platform scene selection falls back to the project scene catalog.
        /// </summary>
        [Fact]
        public void Create_WhenPlatformConfigOmitsSceneSelection_SeedsProjectScenes() {
            WriteScene("Scenes/A.helen");
            WriteScene("Scenes/B.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "ps2"
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal(new[] { "A", "B" }, queueItem.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures Nintendo DS queued builds insert the generated boot scene first even when it was not selected explicitly.
        /// </summary>
        [Fact]
        public void Create_WhenDsBuildOmitsGeneratedBootScene_InsertsItAsStartupScene() {
            WriteScene("Scenes/ColoredCubeGrid.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "ds",
                SelectedSceneIds = [
                    "ColoredCubeGrid"
                ]
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal(new[] { "GeneratedBootScene", "ColoredCubeGrid" }, queueItem.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures Nintendo DS queued builds also include generated companion scenes when the selected authored scene has one.
        /// </summary>
        [Fact]
        public void Create_WhenDsBuildSelectsSceneWithGeneratedCompanion_IncludesNintendoDsCompanionScene() {
            WriteScene("Scenes/rendering/cube_test.helen");
            WriteScene("Scenes/rendering/ds/cube_test_ds.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "ds",
                SelectedSceneIds = [
                    "cube_test"
                ]
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal(new[] { "GeneratedBootScene", "cube_test", "cube_test_ds" }, queueItem.SelectedSceneIds);
        }

        /// <summary>
        /// Ensures Windows queued builds insert the generated boot scene first even when it was not selected explicitly.
        /// </summary>
        [Fact]
        public void Create_WhenWindowsBuildOmitsGeneratedBootScene_InsertsItAsStartupScene() {
            WriteScene("Scenes/ColoredCubeGrid.helen");

            EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
            EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
            EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
                PlatformId = "windows",
                SelectedSceneIds = [
                    "ColoredCubeGrid"
                ]
            };

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreateSelectionModel());
            EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

            Assert.Equal(new[] { "GeneratedBootScene", "ColoredCubeGrid" }, queueItem.SelectedSceneIds);
        }

        /// <summary>
        /// Writes one empty scene file that the scene catalog can enumerate.
        /// </summary>
        /// <param name="sceneId">Project-relative scene identifier to create.</param>
        void WriteScene(string sceneId) {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            File.WriteAllText(scenePath, string.Empty);
        }

        /// <summary>
        /// Creates one builder metadata definition used by the queue-item factory test.
        /// </summary>
        /// <returns>Selection model backed by the supplied definition.</returns>
        static PlatformDefinition CreateSelectionModel() {
            return new PlatformDefinition(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        "default",
                        [
                            new PlatformSettingDefinition(
                                "texture-scale-percent",
                                "Texture scale %",
                                PlatformSettingKind.Text,
                                "100",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "shader-variant-pruning",
                                "Shader variant pruning",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [
                            new PlatformSettingDefinition(
                                "default-width",
                                "Default width",
                                PlatformSettingKind.Text,
                                "1280",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "default-height",
                                "Default height",
                                PlatformSettingKind.Text,
                                "720",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "vsync-enabled",
                                "VSync",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "fullscreen-enabled",
                                "Fullscreen",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                [])
                        ])
                ],
                [],
                [
                    new PlatformComponentSupportRule(
                        "helengine.FPSComponent",
                        PlatformComponentSupportKind.PassThrough,
                        "FPS overlay is canonical on this platform.",
                        string.Empty)
                ],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [
                            new PlatformSettingDefinition(
                                "write-conversion-report",
                                "Write Conversion Report",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "include-project-defined-preprocessor-symbols",
                                "Include Project Symbols",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "load-native-runtime-metadata",
                                "Load Native Runtime Metadata",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                            []),
                            new PlatformSettingDefinition(
                                PlatformCodegenSettingIds.PresetId,
                                "Preset",
                                PlatformSettingKind.Text,
                                "windows-no-shaders",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformStorageProfileDefinition(
                        "loose-files",
                        "Loose Files",
                        PlatformStorageProfileKind.LooseFiles,
                        "windows-loose-files",
                        false)
                ],
                [
                    new PlatformMediaProfileDefinition(
                        "windows-install-tree",
                        "Windows Install Tree",
                        PlatformMediaLayoutKind.InstallTree,
                        true,
                        false)
                ]);
        }

        /// <summary>
        /// Creates one PS2 builder definition used by the PS2 queue-item test.
        /// </summary>
        /// <returns>Selection model backed by the supplied definition.</returns>
        static PlatformDefinition CreatePs2SelectionModel() {
            return new PlatformDefinition(
                "ps2",
                "PlayStation 2",
                [
                    new PlatformBuildProfileDefinition(
                        "ps2-default",
                        "PS2 Default",
                        "PS2 player build",
                        "ps2-standard-forward",
                        "default",
                        [
                            new PlatformSettingDefinition(
                                "texture-scale-percent",
                                "Texture scale %",
                                PlatformSettingKind.Text,
                                "100",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "shader-variant-pruning",
                                "Shader variant pruning",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "ps2-standard-forward",
                        "PS2 Standard Forward",
                        "Default PS2 forward renderer",
                        [
                            new PlatformSettingDefinition(
                                "default-width",
                                "Default width",
                                PlatformSettingKind.Text,
                                "640",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "default-height",
                                "Default height",
                                PlatformSettingKind.Text,
                                "448",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "vsync-enabled",
                                "VSync",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "fullscreen-enabled",
                                "Fullscreen",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "depth-handler-mode",
                                "Depth Handler Mode",
                                PlatformSettingKind.Choice,
                                "hardware",
                                true,
                                ["hardware", "software"])
                        ])
                ],
                [],
                [],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [
                            new PlatformSettingDefinition(
                                "write-conversion-report",
                                "Write Conversion Report",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "include-project-defined-preprocessor-symbols",
                                "Include Project Symbols",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "load-native-runtime-metadata",
                                "Load Native Runtime Metadata",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                PlatformCodegenSettingIds.PresetId,
                                "Preset",
                                PlatformSettingKind.Text,
                                "ps2-default",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformStorageProfileDefinition(
                        "disc-layout",
                        "Disc Layout",
                        PlatformStorageProfileKind.DiscLayout,
                        "ps2-disc-layout",
                        true)
                ],
                [
                    new PlatformMediaProfileDefinition(
                        "ps2-install-tree",
                        "PS2 Install Tree",
                        PlatformMediaLayoutKind.InstallTree,
                        true,
                        true)
                ]);
        }
    }
}


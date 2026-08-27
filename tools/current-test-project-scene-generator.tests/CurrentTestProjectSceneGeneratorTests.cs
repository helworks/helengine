using helengine.current_test_project_scene_generator;
using helengine.editor;
using helengine.files;

namespace helengine.current_test_project_scene_generator.tests {
    /// <summary>
    /// Verifies the maintained current-format rendering fixture generator's public command contract.
    /// </summary>
    public sealed class CurrentTestProjectSceneGeneratorTests {
        /// <summary>
        /// Ensures the default path resolves from the executable output directory to the repository test project.
        /// </summary>
        [Fact]
        public void ResolveDefaultProjectRoot_FromOutputDirectory_UsesRepositoryTestProject() {
            string repositoryRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string outputDirectory = Path.Combine(repositoryRootPath, "tools", "current-test-project-scene-generator", "bin", "Debug", "net9.0");

            Assert.Equal(
                Path.Combine(repositoryRootPath, "test-project"),
                TestProjectPathResolver.ResolveDefaultProjectRoot(outputDirectory));
        }

        /// <summary>
        /// Ensures the committed fixture catalog is explicit and complete.
        /// </summary>
        [Fact]
        public void SceneFileNames_ListsCommittedRenderingCatalog() {
            Assert.Equal(
                new[] {
                    "depth-prepass.helen",
                    "directional-shadow-lab.helen",
                    "directional-shadow-plaza.helen",
                    "material-inputs.helen",
                    "opaque-basics.helen",
                    "point-shadow-lab.helen",
                    "point-shadow.helen",
                    "ps2_basis_light_test.helen",
                    "spot-shadow-lab.helen",
                    "transparency-order.helen"
                },
                RenderingSceneFixtureGenerator.SceneFileNames);
        }

        /// <summary>
        /// Ensures running the supported generator twice produces identical current-format files.
        /// </summary>
        [Fact]
        public void Generate_Twice_ProducesByteIdenticalRenderingFixtures() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-current-rendering-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));

            try {
                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
                });
                RenderingSceneFixtureGenerator generator = new RenderingSceneFixtureGenerator();
                generator.Generate(projectRootPath);
                Dictionary<string, byte[]> firstRun = ReadGeneratedFiles(projectRootPath);

                generator.Generate(projectRootPath);
                Dictionary<string, byte[]> secondRun = ReadGeneratedFiles(projectRootPath);

                Assert.Equal(firstRun.Keys.OrderBy(path => path), secondRun.Keys.OrderBy(path => path));
                foreach (string relativePath in firstRun.Keys) {
                    Assert.Equal(firstRun[relativePath], secondRun[relativePath]);
                }
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures the material dependencies referenced by the rendering catalog use the current common-settings writer.
        /// </summary>
        [Fact]
        public void Generate_WritesCurrentMaterialCommonSettings() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-current-rendering-materials", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));

            try {
                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
                });
                new RenderingSceneFixtureGenerator().Generate(projectRootPath);
                string materialPath = Path.Combine(projectRootPath, "assets", "Materials", "rendering", "TransparentStandard.helmat");
                using FileStream stream = File.OpenRead(materialPath);
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);

                Assert.Equal(MaterialAssetCommonSettingsDocumentBinarySerializer.CurrentVersion, header.Version);
                Assert.Equal((ushort)EditorBinaryRecordKind.AssetImportSettings, header.RecordKind);
                Assert.Equal((ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument, header.ValueKind);
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures the stale PS2 basis material documents and their platform overrides use current writers.
        /// </summary>
        [Fact]
        public void Generate_WritesCurrentPs2BasisMaterialDocuments() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-current-rendering-ps2-materials", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));

            try {
                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
                });
                new RenderingSceneFixtureGenerator().Generate(projectRootPath);

                string materialRootPath = Path.Combine(projectRootPath, "assets", "Materials", "rendering", "ps2_basis_light_test");
                string[] materialNames = ["Center", "Corner", "Ground", "MinusX", "MinusZ", "PlusX", "PlusZ"];
                foreach (string materialName in materialNames) {
                    AssertCurrentHeader(Path.Combine(materialRootPath, materialName + ".hasset"), 3, 5);
                    AssertCurrentHeader(Path.Combine(materialRootPath, materialName + ".hasset.ps2.hasset"), 3, 6);
                    AssertCurrentHeader(Path.Combine(materialRootPath, materialName + ".hasset.windows.hasset"), 3, 6);
                }
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures the project bootstrap scene is regenerated with the current authored-asset writer.
        /// </summary>
        [Fact]
        public void Generate_WritesCurrentBootstrapScene() {
            string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-current-bootstrap-scene", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));

            try {
                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath)
                });
                new RenderingSceneFixtureGenerator().Generate(projectRootPath);

                AssertCurrentHeader(
                    Path.Combine(projectRootPath, "assets", "Scenes", "Bootstrap.helen"),
                    24,
                    6);
            } finally {
                if (Directory.Exists(projectRootPath)) {
                    Directory.Delete(projectRootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures the maintenance tool consumes the public scene-component authoring surface rather than serializer internals.
        /// </summary>
        [Fact]
        public void RenderingGenerator_UsesPublicSceneComponentAuthoringService() {
            string repositoryRootPath = TestProjectPathResolver.ResolveRepositoryRoot(AppContext.BaseDirectory);
            string generatorPath = Path.Combine(
                repositoryRootPath,
                "tools",
                "current-test-project-scene-generator",
                "RenderingSceneFixtureGenerator.cs");
            string source = File.ReadAllText(generatorPath);

            Assert.Contains("GeneratedSceneComponentAuthoringService", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AutomaticScriptComponentPersistenceDescriptor", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EditorTaggedSceneComponentFieldWriter", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the public authoring service produces payloads for the supported fixture component shapes.
        /// </summary>
        [Fact]
        public void PublicSceneComponentAuthoringService_CreatesCurrentComponentPayloads() {
            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            GeneratedSceneComponentAuthoringService service = new GeneratedSceneComponentAuthoringService();
            CameraComponent camera = new CameraComponent();

            Assert.NotEmpty(service.CreateCameraPayload(camera));
            Assert.NotEmpty(service.CreateLightPayload(new PointLightComponent()));
            Assert.NotEmpty(service.CreateMeshPayload(
                EngineSceneAssetReferenceFactory.CreateCubeModel(),
                EngineSceneAssetReferenceFactory.CreateStandardMaterial()));
            Assert.NotEmpty(service.CreateEmptyScriptPayload());
        }

        /// <summary>
        /// Reads only the deterministic fixture outputs owned by the maintained generator.
        /// </summary>
        /// <param name="projectRootPath">Temporary project root containing generated fixtures.</param>
        /// <returns>Generated file bytes keyed by project-relative path.</returns>
        static Dictionary<string, byte[]> ReadGeneratedFiles(string projectRootPath) {
            string assetsRootPath = Path.Combine(projectRootPath, "assets");
            return Directory.GetFiles(assetsRootPath, "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".helen", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".helmat", StringComparison.OrdinalIgnoreCase) ||
                               path.EndsWith(".hasset", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(
                    path => Path.GetRelativePath(projectRootPath, path),
                    path => File.ReadAllBytes(path),
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies one current HELE header without invoking a serializer internals path.
        /// </summary>
        /// <param name="path">Binary file to inspect.</param>
        /// <param name="version">Expected exact serializer version.</param>
        /// <param name="valueKind">Expected value kind.</param>
        static void AssertCurrentHeader(string path, byte version, ushort valueKind) {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length >= 12);
            Assert.Equal((byte)'H', bytes[0]);
            Assert.Equal((byte)'E', bytes[1]);
            Assert.Equal((byte)'L', bytes[2]);
            Assert.Equal((byte)'E', bytes[3]);
            Assert.Equal(version, bytes[5]);
            Assert.Equal(valueKind, BitConverter.ToUInt16(bytes, 10));
        }
    }
}

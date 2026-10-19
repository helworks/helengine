using helengine.baseplatform.Definitions;
using helengine.editor;

namespace helengine.editor.tests {
    /// <summary>Verifies independent numeric policies survive queued build creation and persistence.</summary>
    public sealed class EditorBuildNumericPolicyTests : IDisposable {
        /// <summary>Own project fixture below the explicitly selected task scratch directory.</summary>
        readonly string ProjectRoot;

        /// <summary>Creates one authored scene without depending on a real DemoDisc checkout.</summary>
        public EditorBuildNumericPolicyTests() {
            ProjectRoot = Path.Combine(@"C:\temp\numeric-precision\windows\fixtures", Guid.NewGuid().ToString("N"));
            string scenePath = Path.Combine(ProjectRoot, "assets", "Scenes", "Main.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            using FileStream stream = File.Create(scenePath);
            AssetSerializer.Serialize(stream, new SceneAsset { Id = "Main", AuthoringAssetId = Guid.NewGuid().ToString("N") });
        }

        /// <summary>Releases only the project fixture owned by this test instance.</summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRoot)) { Directory.Delete(ProjectRoot, true); }
        }

        /// <summary>The first queue item retains its policy when the same platform is configured differently.</summary>
        [Fact]
        public void QueuedBuildRetainsOriginalPolicy() {
            EditorBuildPlatformConfigDocument config = new() {
                PlatformId = "windows",
                SelectedSceneIds = ["Main"],
                SelectedCodegenOptionValues = new Dictionary<string, string> {
                    [PlatformCodegenSettingIds.RealStorageBits] = "32",
                    [PlatformCodegenSettingIds.RealArithmeticBits] = "32"
                }
            };
            EditorPlatformBuildSelectionModel model = CreateSelectionModel();
            EditorProjectSceneCatalogService catalog = new(ProjectRoot);
            EditorBuildQueueItemDocument first = EditorBuildQueueItemDocument.Create(catalog, config, model, Path.Combine(ProjectRoot, "output"));
            config.SelectedCodegenOptionValues[PlatformCodegenSettingIds.RealStorageBits] = "64";
            config.SelectedCodegenOptionValues[PlatformCodegenSettingIds.RealArithmeticBits] = "64";
            EditorBuildQueueItemDocument second = EditorBuildQueueItemDocument.Create(catalog, config, model, Path.Combine(ProjectRoot, "output"));
            Assert.Equal("real-v1-s32-a32", first.NumericPolicy.Identity);
            Assert.Equal("real-v1-s64-a64", second.NumericPolicy.Identity);
            Assert.Equal("32", first.SelectedCodegenOptionValues[PlatformCodegenSettingIds.RealStorageBits]);
            EditorBuildConfigService service = new(ProjectRoot);
            service.Save(new EditorBuildConfigDocument { Platforms = [config], QueueItems = [first, second] });
            EditorBuildConfigDocument restored = service.TryLoadExisting();
            Assert.Equal(first.NumericPolicy.Identity, restored.QueueItems[0].NumericPolicy.Identity);
            Assert.Equal(second.NumericPolicy.Identity, restored.QueueItems[1].NumericPolicy.Identity);
        }

        /// <summary>A backend rejects unavailable mixed arithmetic before the build is queued.</summary>
        [Fact]
        public void QueueRejectsUnavailableCombination() {
            EditorBuildPlatformConfigDocument config = new() {
                PlatformId = "windows", SelectedSceneIds = ["Main"],
                SelectedCodegenOptionValues = new Dictionary<string, string> {
                    [PlatformCodegenSettingIds.RealStorageBits] = "16",
                    [PlatformCodegenSettingIds.RealArithmeticBits] = "32"
                }
            };
            Assert.Throws<NotSupportedException>(() => EditorBuildQueueItemDocument.Create(
                new EditorProjectSceneCatalogService(ProjectRoot), config, CreateSelectionModel(), Path.Combine(ProjectRoot, "output")));
        }

        /// <summary>Malformed explicit precision does not silently inherit a valid profile default.</summary>
        [Theory]
        [InlineData("")] [InlineData(" ")] [InlineData("float")] [InlineData("32.0")]
        public void MalformedPrecisionDoesNotBecomeDefault(string invalidValue) {
            EditorBuildPlatformConfigDocument config = new() {
                PlatformId = "windows", SelectedSceneIds = ["Main"],
                SelectedCodegenOptionValues = new Dictionary<string, string> {
                    [PlatformCodegenSettingIds.RealStorageBits] = invalidValue
                }
            };
            Assert.Throws<ArgumentException>(() => EditorBuildQueueItemDocument.Create(
                new EditorProjectSceneCatalogService(ProjectRoot), config, CreateSelectionModel(), Path.Combine(ProjectRoot, "output")));
        }

        /// <summary>The execution handoff uses the queued policy even after backend defaults or option maps change.</summary>
        [Fact]
        public void ExecutionRetainsFrozenNumericSelection() {
            EditorBuildQueueItemDocument item = new() { NumericPolicy = new(64, 64) };
            Dictionary<string, string> effectiveOptions = new() {
                [PlatformCodegenSettingIds.RealStorageBits] = "32", [PlatformCodegenSettingIds.RealArithmeticBits] = "32"
            };
            EditorBuildNumericPolicyResolver.Apply(item, CreateSelectionModel().ResolveCodegenProfile("default"), effectiveOptions);
            Assert.Equal("64", effectiveOptions[PlatformCodegenSettingIds.RealStorageBits]);
            Assert.Equal("64", effectiveOptions[PlatformCodegenSettingIds.RealArithmeticBits]);
            Assert.Equal("real-v1-s64-a64", item.NumericPolicy.Identity);
        }

        /// <summary>Existing persisted queue items capture a policy at their first validated execution handoff.</summary>
        [Fact]
        public void ExistingQueueItemResolvesBeforeExecution() {
            EditorBuildQueueItemDocument item = new();
            Dictionary<string, string> effectiveOptions = new() {
                [PlatformCodegenSettingIds.RealStorageBits] = "64", [PlatformCodegenSettingIds.RealArithmeticBits] = "64"
            };
            EditorBuildNumericPolicyResolver.Apply(item, CreateSelectionModel().ResolveCodegenProfile("default"), effectiveOptions);
            Assert.Equal("real-v1-s64-a64", item.NumericPolicy.Identity);
        }

        /// <summary>Creates minimal metadata with explicitly declared 32/32 and 64/64 support.</summary>
        static EditorPlatformBuildSelectionModel CreateSelectionModel() {
            PlatformDefinition definition = new(
                "windows", "Windows numeric test",
                [new PlatformBuildProfileDefinition("release", "Release", "Test build", "directx11", "default", [])],
                [new PlatformGraphicsProfileDefinition("directx11", "DirectX 11", "Test graphics", [])],
                [], [],
                [new PlatformCodegenProfileDefinition("default", "Default", "Test codegen", PlatformCodegenLanguage.Cpp,
                    helengine.baseplatform.Profiles.PlatformSerializationEndianness.LittleEndian, [],
                    new PlatformNumericCapabilities(NumericPrecisionPolicy.Default, [NumericPrecisionPolicy.Default, new(64, 64)]))],
                [new PlatformStorageProfileDefinition("loose", "Loose", PlatformStorageProfileKind.LooseFiles, "loose", false)],
                [new PlatformMediaProfileDefinition("install", "Install", PlatformMediaLayoutKind.InstallTree, true, false)]);
            return EditorPlatformBuildSelectionModel.From(definition);
        }
    }
}

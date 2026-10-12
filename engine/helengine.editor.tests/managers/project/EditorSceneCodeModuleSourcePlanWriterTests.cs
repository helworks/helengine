using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using helengine.platforms;
using System.Text.Json;

namespace helengine.editor.tests.managers.project {
    /// <summary>Verifies real multi-root source planning distinguishes converter aggregation files from independently owned type definitions.</summary>
    public sealed class EditorSceneCodeModuleSourcePlanWriterTests {
        /// <summary>Fixed visible fixture directory whose contents are owned only by these source-plan tests.</summary>
        const string FixtureRoot = "C:/dev/helworks/builds/helengine-ps1/menu-20261007/scene-modules/source-plan-tests/fixtures";

        /// <summary>Different project aggregation files are ignored while identical shared type definitions keep their first canonical owner.</summary>
        [Fact]
        public void AuthoredDiscoveryIgnoresScaffoldingAndDeduplicatesIdenticalDefinitions() {
            string root = PrepareFixture("discovery");
            string codeRoot = Path.Combine(root, "code");
            WriteModuleSources(codeRoot);
            Dictionary<string, string> owners = EditorSceneCodeModuleSourcePlanWriter.DiscoverAuthoredSourceOwners(codeRoot, ["First", "Second"]);
            Assert.Equal(3, owners.Count);
            Assert.Equal("First", owners["SharedType"]);
            Assert.Equal("First", owners["RuntimeFeatureRequirementAttribute"]);
            Assert.Equal("Second", owners["SecondOnly"]);
            Assert.DoesNotContain("generated_unity", owners.Keys);
            Assert.DoesNotContain("helengine_core_unity", owners.Keys);
            Assert.DoesNotContain("feature_manifest", owners.Keys);
            Assert.True(EditorSceneCodeModuleSourcePlanWriter.IsAggregationSource(Path.Combine(codeRoot, "generated_unity.cpp")));
            Assert.True(EditorSceneCodeModuleSourcePlanWriter.IsAggregationSource(Path.Combine(codeRoot, "helengine_core_unity.cpp")));
            Assert.False(EditorSceneCodeModuleSourcePlanWriter.IsAggregationSource(Path.Combine(codeRoot, "RuntimeFeatureRequirementAttribute.cpp")));
            Assert.False(EditorSceneCodeModuleSourcePlanWriter.IsAggregationSource(Path.Combine(codeRoot, "runtime/feature_manifest.cpp")));
        }

        /// <summary>A genuine differing type definition in two generated module roots still stops source planning with its exact type name.</summary>
        [Fact]
        public void AuthoredDiscoveryRejectsConflictingSharedDefinition() {
            string root = PrepareFixture("conflict");
            string codeRoot = Path.Combine(root, "code");
            WriteModuleSources(codeRoot);
            File.WriteAllText(Path.Combine(codeRoot, "Second/SharedType.cpp"), "int SharedTypeValue() { return 2; }\n");
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                EditorSceneCodeModuleSourcePlanWriter.DiscoverAuthoredSourceOwners(codeRoot, ["First", "Second"]));
            Assert.Contains("SharedType", error.Message);
            Assert.Contains("conflicting transitive authored definitions", error.Message);
        }

        /// <summary>Exercises the public writer against a real cooked scene, proving aggregation filtering does not discard resident runtime feature support.</summary>
        [Fact]
        public void PublicWriteExcludesUnityCandidatesButRetainsRuntimeFeatureManifestAndSharedOwner() {
            string root = PrepareFixture("public-write");
            string projectRoot = Path.Combine(root, "project");
            string scenePath = Path.Combine(projectRoot, "assets/Scenes/Empty.helen");
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            using (FileStream sceneStream = File.Create(scenePath)) {
                AssetSerializer.Serialize(sceneStream, new SceneAsset {
                    Id = "Scenes/Empty.helen", AuthoringAssetId = "10000000000000000000000000000001",
                    RootEntities = [], AssetReferences = []
                });
            }
            string contentRoot = Path.Combine(root, "content");
            TestPlatformMaterialAssetBuilder builder = new();
            PlatformDefinition original = builder.Definition;
            RuntimeGenerationContract contract = new(original.RuntimeGenerationContract.MaterialResolutionMode,
                original.RuntimeGenerationContract.SupportsRenderManager2DTextureReleaseFlush,
                original.RuntimeGenerationContract.PackagedPathPolicy, supportsSceneCodeModules: true);
            PlatformDefinition definition = new(original.PlatformId, original.DisplayName, original.BuildProfiles, original.GraphicsProfiles,
                original.AssetRequirements, original.MaterialSchemas, original.ComponentSupportRules, original.CodegenProfiles,
                original.StorageProfiles, original.MediaProfiles, contract, original.HostDebugCapability, original.AssetCookCapabilities);
            EditorPlatformAssetCookService cook = new(projectRoot, "1.0.0-engine", "source-plan", "1.0.0", [],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = cook.Cook(definition, ["Empty"], contentRoot, ["windows"], builder);
            string codeRoot = Path.Combine(root, "code");
            WriteModuleSources(codeRoot);
            string coreRoot = Path.Combine(root, "core");
            Directory.CreateDirectory(Path.Combine(coreRoot, "runtime"));
            File.WriteAllText(Path.Combine(coreRoot, "SharedType.cpp"), File.ReadAllText(Path.Combine(codeRoot, "First/SharedType.cpp")));
            File.WriteAllText(Path.Combine(coreRoot, "RuntimeFeatureRequirementAttribute.cpp"), "int RequirementAttributeValue() { return 3; }\n");
            File.WriteAllText(Path.Combine(coreRoot, "generated_unity.cpp"), "#include \"SharedType.cpp\"\n");
            File.WriteAllText(Path.Combine(coreRoot, "helengine_core_unity.cpp"), "#include \"RuntimeFeatureRequirementAttribute.cpp\"\n");
            string runtimeFeaturePath = Path.Combine(coreRoot, "runtime", "feature_manifest.cpp");
            File.WriteAllText(runtimeFeaturePath, "int ResidentRuntimeFeatureCount() { return 0; }\n");
            string planPath = new EditorSceneCodeModuleSourcePlanWriter().Write(manifest, coreRoot, codeRoot, contentRoot,
                ["First", "Second"], new DictionaryScriptTypeResolver(), definition, true, false);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(planPath));
            JsonElement[] candidates = document.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
            Assert.DoesNotContain(candidates, candidate => Path.GetFileName(candidate.GetProperty("sourcePath").GetString()) == "generated_unity.cpp"
                || Path.GetFileName(candidate.GetProperty("sourcePath").GetString()) == "helengine_core_unity.cpp");
            JsonElement shared = Assert.Single(candidates, candidate => Path.GetFileName(candidate.GetProperty("sourcePath").GetString()) == "SharedType.cpp");
            Assert.Equal("First", shared.GetProperty("ownerModuleId").GetString());
            JsonElement runtimeFeature = Assert.Single(candidates, candidate => candidate.GetProperty("sourcePath").GetString() == runtimeFeaturePath);
            Assert.Equal("resident", runtimeFeature.GetProperty("ownerModuleId").GetString());
            Assert.Contains(runtimeFeaturePath, document.RootElement.GetProperty("residentRoots").EnumerateArray().Select(value => value.GetString()));
            Assert.Single(document.RootElement.GetProperty("scenes").EnumerateArray());
        }

        /// <summary>Exercises the graph's real loading-policy/source-plan boundary and keeps nonbootstrap engine factories owned by the scenes that use them.</summary>
        [Fact]
        public void GraphSourcePlanUsesConfiguredLoadingBootstrapWithoutRootingEveryCoreFactory() {
            string root = PrepareFixture("kernel-roots");
            string projectRoot = Path.Combine(root, "project");
            WriteComponentScene(projectRoot, "Scenes/Menu.helen", new CameraComponent());
            WriteComponentScene(projectRoot, "Scenes/Loading.helen", new RoundedRectComponent());
            WriteComponentScene(projectRoot, "Scenes/Gameplay.helen", new SpotLightComponent());
            new EditorBuildConfigService(projectRoot).Save(new EditorBuildConfigDocument {
                SceneLoadingBudgetMilliseconds = 8,
                Platforms = [new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows", OverridesLoadingScreenSceneId = true, LoadingScreenSceneId = "Scenes/Loading.helen"
                }]
            });
            TestPlatformMaterialAssetBuilder builder = new();
            PlatformDefinition original = builder.Definition;
            RuntimeGenerationContract contract = new(original.RuntimeGenerationContract.MaterialResolutionMode,
                original.RuntimeGenerationContract.SupportsRenderManager2DTextureReleaseFlush,
                original.RuntimeGenerationContract.PackagedPathPolicy, supportsSceneCodeModules: true);
            PlatformDefinition definition = new(original.PlatformId, original.DisplayName, original.BuildProfiles, original.GraphicsProfiles,
                original.AssetRequirements, original.MaterialSchemas, original.ComponentSupportRules, original.CodegenProfiles,
                original.StorageProfiles, original.MediaProfiles, contract, original.HostDebugCapability, original.AssetCookCapabilities);
            string contentRoot = Path.Combine(root, "content");
            EditorPlatformAssetCookService cook = new(projectRoot, "1.0.0-engine", "kernel-roots", "1.0.0", [],
                PackagedFontAssetFactory.Create(), null, null, TestGeneratedAssetGraph.CreateShaderLibrary());
            PlatformBuildManifest manifest = cook.Cook(definition, ["Menu", "Loading", "Gameplay"], contentRoot, ["windows"], builder);
            Assert.Equal(string.Empty, manifest.LoadingScreenSceneId);
            string coreRoot = Path.Combine(root, "core");
            Directory.CreateDirectory(Path.Combine(coreRoot, "runtime"));
            File.WriteAllText(Path.Combine(coreRoot, "UnrelatedCoreCodec.cpp"), "int UnrelatedCodec() { return 1; }\n");
            ScriptComponentPlayerDeserializerGenerator generator = new(true, false);
            foreach (Type type in new[] { typeof(CameraComponent), typeof(RoundedRectComponent), typeof(SpotLightComponent) }) {
                ScriptComponentReflectionSchema schema = new ScriptComponentReflectionSchemaBuilder().Build(type);
                File.WriteAllText(Path.Combine(coreRoot, type.Name + ".cpp"), "// independent physical component unit\n");
                File.WriteAllText(Path.Combine(coreRoot, generator.BuildNativeDeserializerClassName(schema) + ".cpp"), generator.GenerateNativeDeserializerSource(schema));
            }
            EditorPlatformBuildGraphRunner runner = new(projectRoot, "1", "kernel-roots", "1", Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("windows", "Windows", typeof(EditorSceneCodeModuleSourcePlanWriterTests).Assembly.Location,
                    string.Empty, true, coreRoot), null, new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService(), null, TestGeneratedAssetGraph.CreateShaderLibrary(), new EngineCodegenToolProvider());
            string planPath = runner.WriteSceneCodeModuleSourcePlan(manifest, coreRoot, Path.Combine(root, "code"), contentRoot, [], definition, true, false);
            Assert.Equal("Loading", manifest.LoadingScreenSceneId);
            Assert.Equal(8d, manifest.SceneLoadingBudgetMilliseconds);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(planPath));
            Assert.Equal(new[] { "Loading", "Menu" }, document.RootElement.GetProperty("bootstrapSceneIds").EnumerateArray().Select(value => value.GetString()));
            string[] residentRoots = document.RootElement.GetProperty("residentRoots").EnumerateArray().Select(value => Path.GetFileName(value.GetString())).ToArray();
            Assert.Contains("CameraComponent.cpp", residentRoots);
            Assert.Contains("RoundedRectComponent.cpp", residentRoots);
            Assert.DoesNotContain("SpotLightComponent.cpp", residentRoots);
            Assert.DoesNotContain("GeneratedRuntimeSpotLightComponentDeserializer.cpp", residentRoots);
            Assert.DoesNotContain("UnrelatedCoreCodec.cpp", residentRoots);
            Assert.Contains(document.RootElement.GetProperty("candidates").EnumerateArray(), value => Path.GetFileName(value.GetProperty("sourcePath").GetString()) == "UnrelatedCoreCodec.cpp");
            JsonElement gameplay = Assert.Single(document.RootElement.GetProperty("scenes").EnumerateArray(), value => value.GetProperty("sceneId").GetString() == "Gameplay");
            Assert.False(gameplay.GetProperty("isResident").GetBoolean());
            Assert.Contains("GeneratedRuntimeSpotLightComponentDeserializer.cpp", gameplay.GetProperty("rootSources").EnumerateArray().Select(value => Path.GetFileName(value.GetString())));
            JsonElement spotlightFactory = Assert.Single(document.RootElement.GetProperty("componentFactories").EnumerateArray(), value => value.GetProperty("componentTypeId").GetString() == typeof(SpotLightComponent).FullName);
            Assert.False(spotlightFactory.GetProperty("resident").GetBoolean());
            string registration = File.ReadAllText(Path.Combine(coreRoot, "GeneratedRuntimeComponentDeserializerRegistration.cpp"));
            Assert.Contains("new ::GeneratedRuntimeCameraComponentDeserializer()", registration);
            Assert.Contains("new ::GeneratedRuntimeRoundedRectComponentDeserializer()", registration);
            Assert.DoesNotContain("GeneratedRuntimeSpotLightComponentDeserializer", registration);
            new EditorRuntimeNativeManifestWriter().Write(coreRoot, manifest);
            Assert.Contains("kRuntimeLoadingScreenSceneId[] = \"Loading\";", File.ReadAllText(Path.Combine(coreRoot, "runtime/runtime_startup_manifest.cpp")));
        }

        /// <summary>Method-level templates are eligible for movement only when their emitted header is owner independent; generic classes remain conservatively resident.</summary>
        [Fact]
        public void TemplatePolicyRetainsOnlyUnresolvedOrOwnerDependentDefinitions() {
            string root = PrepareFixture("template-policy");
            string header = Path.Combine(root, "Methods.hpp");
            File.WriteAllText(header, "template<typename T> T* Find(Object* value) { return dynamic_cast<T*>(value); }\n");
            Assert.False(EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(typeof(Enumerable), header));
            File.WriteAllText(header, "template<typename T> auto Bind() { return HeCppBindCodeModule(HELENGINE_CODE_MODULE_ID); }\n");
            Assert.True(EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(typeof(Enumerable), header));
            Assert.True(EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(typeof(List<>), Path.Combine(root, "missing.hpp")));
            Assert.Throws<FileNotFoundException>(() => EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(typeof(Enumerable), Path.Combine(root, "missing.hpp")));
            Assert.False(EditorSceneCodeModuleTemplateResidency.RequiresResidentSource(typeof(CameraComponent), Path.Combine(root, "missing.hpp")));
        }

        /// <summary>Writes an actual authored automatic-component record so the normal cook and schema discovery paths are exercised.</summary>
        /// <param name="projectRoot">Isolated project root.</param>
        /// <param name="scenePath">Canonical authored scene identity.</param>
        /// <param name="component">Real engine component to serialize using the official persistence descriptor.</param>
        static void WriteComponentScene(string projectRoot, string scenePath, Component component) {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new(new ScriptComponentReflectionSchemaBuilder());
            string path = Path.Combine(projectRoot, "assets", scenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, AssetSerializer.SerializeToBytes(new SceneAsset {
                Id = scenePath, AuthoringAssetId = "1000000000000000000000000000000" + (scenePath.Contains("Loading") ? "2" : scenePath.Contains("Gameplay") ? "3" : "1"),
                AssetReferences = [], RootEntities = [new SceneEntityAsset {
                    Id = 1, Name = "Component root", LocalScale = float3.One, LocalOrientation = float4.Identity,
                    Components = [descriptor.SerializeComponent(component, 0, null)], Children = []
                }]
            }));
        }

        /// <summary>Creates one clean fixed test-owned directory without touching production or other test fixtures.</summary>
        /// <param name="name">Stable test-case directory name.</param>
        /// <returns>Absolute isolated fixture root.</returns>
        static string PrepareFixture(string name) {
            string root = Path.GetFullPath(FixtureRoot);
            string path = Path.GetFullPath(Path.Combine(root, name));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Source-plan fixture escaped its fixed root.");
            }
            if (Directory.Exists(path)) { Directory.Delete(path, true); }
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Writes two generated project roots with different aggregators, identical shared definitions and separate owned types.</summary>
        /// <param name="codeRoot">Generated authored modules root used by the real production discovery method.</param>
        static void WriteModuleSources(string codeRoot) {
            foreach (string module in new[] { "First", "Second" }) {
                string moduleRoot = Path.Combine(codeRoot, module);
                Directory.CreateDirectory(moduleRoot);
                File.WriteAllText(Path.Combine(moduleRoot, "generated_unity.cpp"), "#include \"" + module + "Only.cpp\"\n");
                File.WriteAllText(Path.Combine(moduleRoot, "helengine_core_unity.cpp"), "// aggregation for " + module + "\n");
                File.WriteAllText(Path.Combine(moduleRoot, "feature_manifest.cpp"), "int " + module + "FeatureCount() { return 1; }\n");
                File.WriteAllText(Path.Combine(moduleRoot, "SharedType.cpp"), "int SharedTypeValue() { return 1; }\n");
            }
            File.WriteAllText(Path.Combine(codeRoot, "First/RuntimeFeatureRequirementAttribute.cpp"), "int RequirementAttributeValue() { return 3; }\n");
            File.WriteAllText(Path.Combine(codeRoot, "Second/SecondOnly.cpp"), "int SecondOnlyValue() { return 4; }\n");
        }
    }
}

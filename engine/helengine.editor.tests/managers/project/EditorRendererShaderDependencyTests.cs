using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>Verifies renderer-owned shader dependencies reach the platform shader cook request.</summary>
public sealed class EditorRendererShaderDependencyTests {
    /// <summary>Stable renderer shader asset identity used by the packaging contract.</summary>
    const string ShaderAssetId = "SpriteBatchShader";
    /// <summary>Stable renderer vertex program identity.</summary>
    const string VertexProgramName = "SpriteBatchShader.vs";
    /// <summary>Stable renderer pixel program identity.</summary>
    const string PixelProgramName = "SpriteBatchShader.ps";
    /// <summary>Build-owned root reserved for this test instance's project and cook fixtures.</summary>
    readonly string TestRootPath;

    /// <summary>Creates a fixture under the explicitly configured Task 4 build-owned artifact root.</summary>
    public EditorRendererShaderDependencyTests() {
        string configuredRoot = Environment.GetEnvironmentVariable("HELENGINE_TASK4_TEST_OUTPUT");
        if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathFullyQualified(configuredRoot)) {
            throw new InvalidOperationException("HELENGINE_TASK4_TEST_OUTPUT must name the absolute Task 4 test artifact root.");
        }

        TestRootPath = Path.Combine(Path.GetFullPath(configuredRoot), "editor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestRootPath);
    }

    /// <summary>Checks opted-in renderer variants pass through source resolution into the real platform cook request.</summary>
    [Fact]
    public void Cook_includes_renderer_dependencies_without_material_dependencies_and_forwards_resolved_source() {
        string projectRoot = Path.Combine(TestRootPath, "project");
        string outputRoot = Path.Combine(TestRootPath, "output");
        string assetsRoot = Path.Combine(projectRoot, "assets");
        string fixtureSourcePath = Path.Combine(assetsRoot, ShaderAssetId + ".hlsl");
        byte[] fixtureSource = "// task4 renderer fixture\nfloat4 VS() : SV_Position { return 0; }\nfloat4 PS() : SV_Target { return 1; }\n"u8.ToArray();
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllBytes(fixtureSourcePath, fixtureSource);
        WriteEmptyScene(projectRoot, "Scenes/Main.helen");
        using EditorBuiltInShaderAssetLibrary shaderLibrary = TestGeneratedAssetGraph.CreateShaderLibrary();
        RendererDependencyBuilder builder = new();
        EditorPlatformAssetCookService service = CreateService(projectRoot, shaderLibrary);

        service.Cook(builder.Definition, ["Main"], outputRoot, ["ps3"], builder);

        PlatformShaderArtifactCookRequest request = Assert.IsType<PlatformShaderArtifactCookRequest>(builder.LastShaderRequest);
        Assert.Equal(new[] { "Textured", "RoundedShape" }, request.ShaderDependencies.Select(dependency => dependency.VariantName));
        Assert.All(request.ShaderDependencies, dependency => {
            Assert.Equal(ShaderAssetId, dependency.ShaderAssetId);
            Assert.Equal(VertexProgramName, dependency.VertexProgramName);
            Assert.Equal(PixelProgramName, dependency.PixelProgramName);
        });
        PlatformShaderArtifactCookSource source = Assert.Single(request.ShaderSources);
        Assert.Equal(ShaderAssetId, source.ShaderAssetId);
        Assert.Equal(Path.GetFullPath(fixtureSourcePath), source.SourcePath);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fixtureSource)), source.SourceHash);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(fixtureSource), source.SourceText);
    }

    /// <summary>Checks identical material and renderer identities are cooked once in first-request order.</summary>
    [Fact]
    public void Resolver_deduplicates_identical_renderer_dependency_identities_ordinally() {
        PlatformShaderDependency[] duplicates = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured"),
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured")
        ];

        IReadOnlyList<PlatformShaderDependency> merged = EditorRendererShaderDependencyResolver.Merge(
            Array.Empty<PlatformShaderDependency>(), duplicates);

        Assert.Single(merged);
        Assert.Equal("Textured", merged[0].VariantName);
    }

    /// <summary>Rejects a material program pair that conflicts with a renderer-reserved asset and variant.</summary>
    [Fact]
    public void Resolver_rejects_material_program_pair_conflicting_with_renderer_reservation() {
        PlatformShaderDependency material = new(ShaderAssetId, "Material.vs", "Material.ps", "Textured");
        PlatformShaderDependency renderer = new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EditorRendererShaderDependencyResolver.Merge([material], [renderer]));

        Assert.Contains("conflicts", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects a renderer declaration whose list contains a null entry.</summary>
    [Fact]
    public void Resolver_rejects_null_renderer_dependency_entries_explicitly() {
        PlatformShaderDependency[] dependencies = [null];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EditorRendererShaderDependencyResolver.Merge(Array.Empty<PlatformShaderDependency>(), dependencies));

        Assert.Contains("index 0 is null", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects a missing renderer declaration list rather than silently dropping renderer shader inputs.</summary>
    [Fact]
    public void Resolver_rejects_missing_renderer_dependency_list() {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            EditorRendererShaderDependencyResolver.Merge(Array.Empty<PlatformShaderDependency>(), null));

        Assert.Equal("rendererDependencies", exception.ParamName);
    }

    /// <summary>Deduplicates only exact ordinal identities and keeps differently cased variants separate.</summary>
    [Fact]
    public void Resolver_uses_full_ordinal_identity_for_deduplication() {
        PlatformShaderDependency[] rendererDependencies = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured"),
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "textured")
        ];

        IReadOnlyList<PlatformShaderDependency> merged = EditorRendererShaderDependencyResolver.Merge(
            Array.Empty<PlatformShaderDependency>(), rendererDependencies);

        Assert.Equal(new[] { "Textured", "textured" }, merged.Select(dependency => dependency.VariantName));
    }

    /// <summary>Keeps optional material entries first while deduplicating a renderer pair already requested by a material.</summary>
    [Fact]
    public void Resolver_preserves_material_order_and_existing_optional_material_entries() {
        PlatformShaderDependency optionalMaterial = new("LegacyShader", null, null, null);
        PlatformShaderDependency texturedMaterial = new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured");
        PlatformShaderDependency[] rendererDependencies = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured"),
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "RoundedShape")
        ];

        IReadOnlyList<PlatformShaderDependency> merged = EditorRendererShaderDependencyResolver.Merge(
            [optionalMaterial, texturedMaterial], rendererDependencies);

        Assert.Equal(new[] { "LegacyShader", ShaderAssetId, ShaderAssetId }, merged.Select(dependency => dependency.ShaderAssetId));
        Assert.Equal(new[] { string.Empty, "Textured", "RoundedShape" }, merged.Select(dependency => dependency.VariantName));
        Assert.False(merged[0].HasProgramPair);
    }

    /// <summary>Rejects two renderer program pairs that reserve the same asset and variant.</summary>
    [Fact]
    public void Resolver_rejects_conflicting_renderer_program_pairs() {
        PlatformShaderDependency[] rendererDependencies = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured"),
            new(ShaderAssetId, "Other.vs", "Other.ps", "Textured")
        ];

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EditorRendererShaderDependencyResolver.Merge(Array.Empty<PlatformShaderDependency>(), rendererDependencies));

        Assert.Contains("conflicts", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects renderer declarations when the selected builder cannot cook shader artifacts.</summary>
    [Fact]
    public void Cook_fails_when_renderer_dependencies_have_no_shader_artifact_capability() {
        string projectRoot = Path.Combine(TestRootPath, "missing-capability-project");
        string outputRoot = Path.Combine(TestRootPath, "missing-capability-output");
        WriteEmptyScene(projectRoot, "Scenes/Main.helen");
        using EditorBuiltInShaderAssetLibrary shaderLibrary = TestGeneratedAssetGraph.CreateShaderLibrary();
        RendererDependencyOnlyBuilder builder = new();
        EditorPlatformAssetCookService service = CreateService(projectRoot, shaderLibrary);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            service.Cook(builder.Definition, ["Main"], outputRoot, ["windows"], builder));

        Assert.Contains("must provide shader artifact cooking", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Forwards a changed build-owned source snapshot and its new content hash on a subsequent cook.</summary>
    [Fact]
    public void Cook_forwards_new_source_hash_after_renderer_shader_source_changes() {
        string projectRoot = Path.Combine(TestRootPath, "changed-source-project");
        string assetsRoot = Path.Combine(projectRoot, "assets");
        string sourcePath = Path.Combine(assetsRoot, ShaderAssetId + ".hlsl");
        byte[] firstSource = "// first copied source snapshot\n"u8.ToArray();
        Directory.CreateDirectory(assetsRoot);
        File.WriteAllBytes(sourcePath, firstSource);
        WriteEmptyScene(projectRoot, "Scenes/Main.helen");
        using EditorBuiltInShaderAssetLibrary shaderLibrary = TestGeneratedAssetGraph.CreateShaderLibrary();
        RendererDependencyBuilder builder = new();
        EditorPlatformAssetCookService service = CreateService(projectRoot, shaderLibrary);

        service.Cook(builder.Definition, ["Main"], Path.Combine(TestRootPath, "changed-source-first"), ["ps3"], builder);
        string firstHash = Assert.Single(builder.LastShaderRequest.ShaderSources).SourceHash;
        string[] firstVariants = builder.LastShaderRequest.ShaderDependencies.Select(dependency => dependency.VariantName).ToArray();
        service.Cook(builder.Definition, ["Main"], Path.Combine(TestRootPath, "changed-source-unchanged"), ["ps3"], builder);
        Assert.Equal(firstHash, Assert.Single(builder.LastShaderRequest.ShaderSources).SourceHash);
        Assert.Equal(firstVariants, builder.LastShaderRequest.ShaderDependencies.Select(dependency => dependency.VariantName));
        byte[] secondSource = "// changed copied source snapshot\n"u8.ToArray();
        File.WriteAllBytes(sourcePath, secondSource);
        service.Cook(builder.Definition, ["Main"], Path.Combine(TestRootPath, "changed-source-mutated"), ["ps3"], builder);
        string secondHash = Assert.Single(builder.LastShaderRequest.ShaderSources).SourceHash;

        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(firstSource)), firstHash);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(secondSource)), secondHash);
        Assert.NotEqual(firstHash, secondHash);
    }

    /// <summary>Preserves the prior no-artifact behavior for builders that do not opt into renderer shader declarations.</summary>
    [Fact]
    public void Cook_preserves_non_opted_in_builder_behavior() {
        string projectRoot = Path.Combine(TestRootPath, "non-opted-project");
        string outputRoot = Path.Combine(TestRootPath, "non-opted-output");
        WriteEmptyScene(projectRoot, "Scenes/Main.helen");
        using EditorBuiltInShaderAssetLibrary shaderLibrary = TestGeneratedAssetGraph.CreateShaderLibrary();
        TestPlatformMaterialAssetBuilder builder = new();
        EditorPlatformAssetCookService service = CreateService(projectRoot, shaderLibrary);

        PlatformBuildManifest manifest = service.Cook(builder.Definition, ["Main"], outputRoot, ["windows"], builder);

        Assert.Equal("Main", manifest.StartupSceneId);
        Assert.DoesNotContain(manifest.CookedArtifacts, artifact => artifact.LogicalArtifactId.Contains("SpriteBatchShader", StringComparison.Ordinal));
    }

    /// <summary>Creates the editor cook service with only the dependencies needed for a renderer source-resolution test.</summary>
    /// <param name="projectRoot">Build-owned project fixture root.</param>
    /// <param name="shaderLibrary">Built-in shader source library used by source resolution.</param>
    /// <returns>Configured asset cook service.</returns>
    static EditorPlatformAssetCookService CreateService(string projectRoot, EditorBuiltInShaderAssetLibrary shaderLibrary) {
        return new EditorPlatformAssetCookService(
            projectRoot,
            "1.0.0-engine",
            "task4",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            PackagedFontAssetFactory.Create(),
            null,
            null,
            shaderLibrary);
    }

    /// <summary>Copies a platform definition while removing its material schemas for renderer-only packaging tests.</summary>
    /// <param name="materialDefinition">Existing platform definition supplying valid profile metadata.</param>
    /// <returns>Equivalent platform definition with no material schemas.</returns>
    static PlatformDefinition CreateMaterialFreeDefinition(PlatformDefinition materialDefinition) {
        return new PlatformDefinition(
            materialDefinition.PlatformId,
            materialDefinition.DisplayName,
            materialDefinition.BuildProfiles,
            materialDefinition.GraphicsProfiles,
            materialDefinition.AssetRequirements,
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            materialDefinition.ComponentSupportRules,
            materialDefinition.CodegenProfiles,
            materialDefinition.StorageProfiles,
            materialDefinition.MediaProfiles,
            materialDefinition.RuntimeGenerationContract,
            materialDefinition.HostDebugCapability,
            materialDefinition.AssetCookCapabilities,
            materialDefinition.ComponentMemberDefinitions);
    }

    /// <summary>Writes one empty scene so the public editor cook path packages a renderer-only build.</summary>
    /// <param name="projectRoot">Build-owned project fixture root.</param>
    /// <param name="sceneId">Scene path relative to the project assets directory.</param>
    static void WriteEmptyScene(string projectRoot, string sceneId) {
        string scenePath = Path.Combine(projectRoot, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        SceneAsset scene = new() {
            Id = sceneId,
            AuthoringAssetId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sceneId)))[..32].ToLowerInvariant(),
            RootEntities = [new SceneEntityAsset {
                Id = 1,
                Name = "Root",
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity,
                Components = Array.Empty<SceneComponentAssetRecord>(),
                Children = Array.Empty<SceneEntityAsset>()
            }]
        };
        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, scene);
    }

    /// <summary>Acts as a renderer-capable platform builder and records the actual request produced by editor cooking.</summary>
    sealed class RendererDependencyBuilder : IPlatformAssetBuilder, IPlatformShaderArtifactBuilder, IPlatformRendererShaderDependencyProvider {
        /// <summary>Supplies the stable descriptor and baseline platform metadata for this fixture.</summary>
        readonly TestPlatformMaterialAssetBuilder MaterialBuilder = new();
        /// <summary>Stores the material-free definition used by the renderer-only cook.</summary>
        readonly PlatformDefinition DefinitionValue;
        /// <summary>Stores both supported sprite batch variants in the fixed renderer request order.</summary>
        readonly PlatformShaderDependency[] Dependencies = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured"),
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "RoundedShape")
        ];

        /// <summary>Creates a material-free platform definition for the renderer-only cook fixture.</summary>
        public RendererDependencyBuilder() {
            DefinitionValue = CreateMaterialFreeDefinition(MaterialBuilder.Definition);
        }

        /// <summary>Gets the recorded request passed through the public asset cooking flow.</summary>
        public PlatformShaderArtifactCookRequest LastShaderRequest { get; private set; }

        /// <summary>Gets the wrapped test builder descriptor.</summary>
        public PlatformBuilderDescriptor Descriptor => MaterialBuilder.Descriptor;

        /// <summary>Gets the wrapped platform definition used by the scene packager.</summary>
        public PlatformDefinition Definition => DefinitionValue;

        /// <summary>Gets the read-only renderer shader declarations in their stable first-request order.</summary>
        public IReadOnlyList<PlatformShaderDependency> RendererShaderDependencies => Dependencies;

        /// <summary>Forwards material translation to the existing test builder.</summary>
        /// <param name="request">Material request created by scene packaging.</param>
        /// <returns>Translated material payload.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) => MaterialBuilder.CookMaterial(request);

        /// <summary>Records the real editor-generated shader cook request and returns no additional artifacts.</summary>
        /// <param name="request">Resolved source snapshots and merged dependencies from editor cooking.</param>
        /// <returns>Successful empty artifact result.</returns>
        public PlatformShaderArtifactCookResult CookShaderArtifacts(PlatformShaderArtifactCookRequest request) {
            LastShaderRequest = request;
            return new PlatformShaderArtifactCookResult(Array.Empty<PlatformCookedArtifactDeclaration>());
        }

        /// <summary>Forwards platform build execution to the existing test builder.</summary>
        /// <param name="request">Build request supplied by the editor.</param>
        /// <param name="progressReporter">Progress reporter supplied by the editor.</param>
        /// <param name="diagnosticReporter">Diagnostic reporter supplied by the editor.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the editor.</param>
        /// <returns>The wrapped test builder's completed build report.</returns>
        public Task<PlatformBuildReport> BuildAsync(PlatformBuildRequest request, IPlatformBuildProgressReporter progressReporter, IPlatformBuildDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken) {
            return MaterialBuilder.BuildAsync(request, progressReporter, diagnosticReporter, cancellationToken);
        }
    }

    /// <summary>Provides renderer dependencies without shader artifact cooking to verify the capability error path.</summary>
    sealed class RendererDependencyOnlyBuilder : IPlatformAssetBuilder, IPlatformRendererShaderDependencyProvider {
        /// <summary>Supplies the stable descriptor and material translation implementation for this fixture.</summary>
        readonly TestPlatformMaterialAssetBuilder MaterialBuilder = new();
        /// <summary>Stores the material-free definition used by the renderer-only fixture.</summary>
        readonly PlatformDefinition DefinitionValue;
        /// <summary>Stores the declared renderer dependency that must not be silently ignored.</summary>
        readonly PlatformShaderDependency[] Dependencies = [
            new(ShaderAssetId, VertexProgramName, PixelProgramName, "Textured")
        ];

        /// <summary>Creates a renderer-only builder with no shader artifact capability.</summary>
        public RendererDependencyOnlyBuilder() {
            DefinitionValue = CreateMaterialFreeDefinition(MaterialBuilder.Definition);
        }

        /// <summary>Gets the wrapped platform builder descriptor.</summary>
        public PlatformBuilderDescriptor Descriptor => MaterialBuilder.Descriptor;

        /// <summary>Gets the material-free platform definition used by the renderer-only fixture.</summary>
        public PlatformDefinition Definition => DefinitionValue;

        /// <summary>Gets the renderer-owned shader dependency declaration.</summary>
        public IReadOnlyList<PlatformShaderDependency> RendererShaderDependencies => Dependencies;

        /// <summary>Forwards the unused material translation surface to the existing test builder.</summary>
        /// <param name="request">Material request created by packaging.</param>
        /// <returns>Cooked material result from the wrapped test builder.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) => MaterialBuilder.CookMaterial(request);

        /// <summary>Forwards platform build execution to the existing test builder.</summary>
        /// <param name="request">Build request supplied by the editor.</param>
        /// <param name="progressReporter">Progress reporter supplied by the editor.</param>
        /// <param name="diagnosticReporter">Diagnostic reporter supplied by the editor.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the editor.</param>
        /// <returns>The wrapped test builder's completed build report.</returns>
        public Task<PlatformBuildReport> BuildAsync(PlatformBuildRequest request, IPlatformBuildProgressReporter progressReporter, IPlatformBuildDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken) {
            return MaterialBuilder.BuildAsync(request, progressReporter, diagnosticReporter, cancellationToken);
        }
    }
}

using helengine.editor;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using helengine.platforms;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the shared shader package pipeline recognizes the PS Vita target as a first-class compile target.
    /// </summary>
    public class PsVitaShaderPackagePipelineTests {
        /// <summary>
        /// Ensures the stable target-name mapping returns the expected lowercase PS Vita identifier.
        /// </summary>
        [Fact]
        public void GetTargetName_whenTargetIsPsVita_returnsPsvita() {
            Assert.Equal("psvita", ShaderTargetNames.GetTargetName(ShaderCompileTarget.PsVita));
        }

        /// <summary>
        /// Ensures the target-name parser resolves the PS Vita identifier back into the compile-target enum value.
        /// </summary>
        [Fact]
        public void TryParseTarget_whenNameIsPsvita_returnsPsVita() {
            Assert.True(ShaderTargetNames.TryParseTarget("psvita", out ShaderCompileTarget target));
            Assert.Equal(ShaderCompileTarget.PsVita, target);
        }

        /// <summary>
        /// Ensures shared shader compilation code does not directly instantiate renderer-specific backends.
        /// </summary>
        [Fact]
        public void SharedShaderCompilationSource_whenInspected_doesNotDirectlyInstantiateRendererSpecificBackends() {
            string helengineRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string moduleManagerPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "shaders", "ShaderModuleManager.cs");
            string builtInLibraryPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "shaders", "EditorBuiltInShaderAssetLibrary.cs");
            string editorSessionPath = Path.Combine(helengineRootPath, "engine", "helengine.editor", "EditorSession.cs");
            string renderValidationPath = Path.Combine(helengineRootPath, "engine", "helengine.render.validation", "RenderShaderFactory.cs");

            string moduleManagerSource = File.ReadAllText(moduleManagerPath);
            string builtInLibrarySource = File.ReadAllText(builtInLibraryPath);
            string editorSessionSource = File.ReadAllText(editorSessionPath);
            string renderValidationSource = File.ReadAllText(renderValidationPath);

            Assert.DoesNotContain("new helengine.directx11.DirectX11ShaderBackend()", moduleManagerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.vulkan.VulkanShaderBackend()", moduleManagerSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.directx11.DirectX11ShaderBackend()", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("new helengine.vulkan.VulkanShaderBackend()", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.directx11.DirectX11Renderer3D", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.vulkan.VulkanRenderer3D", builtInLibrarySource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.directx11.DirectX11Renderer3D", editorSessionSource, StringComparison.Ordinal);
            Assert.DoesNotContain("helengine.vulkan.VulkanRenderer3D", editorSessionSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new DirectX11ShaderBackend()", renderValidationSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new VulkanShaderBackend()", renderValidationSource, StringComparison.Ordinal);
            Assert.Contains("ShaderBackendRegistry", moduleManagerSource, StringComparison.Ordinal);
            Assert.Contains("ShaderBackendRegistry", builtInLibrarySource, StringComparison.Ordinal);
            Assert.Contains("IShaderCompileTargetProvider", builtInLibrarySource, StringComparison.Ordinal);
            Assert.Contains("IShaderCompileTargetProvider", editorSessionSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures dynamically loaded platform builders can contribute shader backends into the shared registry without a compile-time editor dependency on PS Vita assemblies.
        /// </summary>
        [Fact]
        public void RegisterShaderBackends_whenLoadedPlatformBuilderImplementsShaderBackendContributor_registersContributedTarget() {
            AvailablePlatformDescriptor descriptor = new AvailablePlatformDescriptor("psvita", "PS Vita");
            EditorLoadedPlatformBuilder loadedBuilder = new EditorLoadedPlatformBuilder(descriptor, new TestShaderBackendContributorBuilder());
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();

            loadedBuilder.RegisterShaderBackends(shaderBackendRegistry);

            Assert.True(shaderBackendRegistry.ContainsTarget(ShaderCompileTarget.PsVita));
        }

        /// <summary>
        /// Supplies one fake loaded platform builder that contributes a PS Vita shader backend through the shared contributor contract.
        /// </summary>
        sealed class TestShaderBackendContributorBuilder : IPlatformAssetBuilder, IShaderBackendRegistryContributor {
            /// <summary>
            /// Gets the fake builder descriptor required by the loaded-builder wrapper.
            /// </summary>
            public PlatformBuilderDescriptor Descriptor { get; } = new PlatformBuilderDescriptor(
                "test.psvita.builder",
                "1.0.0",
                "psvita",
                new EngineCompatibilityRange("1.0.0", "1.0.0"),
                new ManifestCompatibilityRange(1, 1),
                ["psvita"],
                ["debug"]);

            /// <summary>
            /// Gets the minimal platform definition required by the loaded-builder wrapper.
            /// </summary>
            public PlatformDefinition Definition { get; } = new PlatformDefinition(
                "psvita",
                "PS Vita",
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                new RuntimeGenerationContract(
                    RuntimeMaterialResolutionMode.CookedPlatformOwned,
                    true,
                    PackagedPathPolicy.ContentRelativeOnly));

            /// <summary>
            /// This fake builder does not participate in material cooking during this registry test.
            /// </summary>
            /// <param name="request">Unused material cook request.</param>
            /// <returns>Never returns because the test should not call this path.</returns>
            public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
                throw new NotSupportedException();
            }

            /// <summary>
            /// This fake builder does not participate in build execution during this registry test.
            /// </summary>
            /// <param name="request">Unused build request.</param>
            /// <param name="progressReporter">Unused progress reporter.</param>
            /// <param name="diagnosticReporter">Unused diagnostic reporter.</param>
            /// <param name="cancellationToken">Unused cancellation token.</param>
            /// <returns>Never returns because the test should not call this path.</returns>
            public Task<PlatformBuildReport> BuildAsync(
                PlatformBuildRequest request,
                IPlatformBuildProgressReporter progressReporter,
                IPlatformBuildDiagnosticReporter diagnosticReporter,
                CancellationToken cancellationToken) {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Registers one fake PS Vita backend into the supplied registry.
            /// </summary>
            /// <param name="shaderBackendRegistry">Registry that should receive the fake PS Vita backend.</param>
            public void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry) {
                if (shaderBackendRegistry == null) {
                    throw new ArgumentNullException(nameof(shaderBackendRegistry));
                }

                shaderBackendRegistry.Register(new TestPsVitaShaderBackend());
            }
        }

        /// <summary>
        /// Supplies one fake PS Vita shader backend for the dynamic builder contribution test.
        /// </summary>
        sealed class TestPsVitaShaderBackend : IShaderBackend {
            /// <summary>
            /// Gets the target associated with the fake backend.
            /// </summary>
            public ShaderCompileTarget Target => ShaderCompileTarget.PsVita;

            /// <summary>
            /// Gets the fake capability set exposed by the backend.
            /// </summary>
            public ShaderBackendCapabilities Capabilities { get; } = new ShaderBackendCapabilities(
                new ShaderModel(4, 0),
                new ShaderModel(4, 0),
                [ShaderStage.Vertex, ShaderStage.Pixel],
                false);

            /// <summary>
            /// This fake backend is never asked to compile during the registry contribution test.
            /// </summary>
            /// <param name="request">Unused compile request.</param>
            /// <param name="includeResolver">Unused include resolver.</param>
            /// <returns>Never returns because the test should not call this path.</returns>
            public ShaderCompileResult Compile(ShaderCompileRequest request, IShaderIncludeResolver includeResolver) {
                throw new NotSupportedException();
            }
        }
    }
}

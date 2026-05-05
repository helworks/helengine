using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor shader-package export service stages referenced shader packages into the Windows build root.
    /// </summary>
    public class EditorShaderPackageExportServiceTests : IDisposable {
        /// <summary>
        /// Temporary directory that acts as the shader cache root for the test case.
        /// </summary>
        readonly string ShaderCacheRootPath;

        /// <summary>
        /// Temporary directory that acts as the Windows build root for the test case.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes isolated cache and build roots for shader export verification.
        /// </summary>
        public EditorShaderPackageExportServiceTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-shader-export-tests", Guid.NewGuid().ToString("N"));
            ShaderCacheRootPath = Path.Combine(workspaceRootPath, "shader-cache");
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(ShaderCacheRootPath);
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the temporary workspace after each test.
        /// </summary>
        public void Dispose() {
            string workspaceRootPath = Path.GetDirectoryName(ShaderCacheRootPath);
            if (!string.IsNullOrWhiteSpace(workspaceRootPath) && Directory.Exists(workspaceRootPath)) {
                Directory.Delete(workspaceRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one referenced shader package is copied into the build root using the runtime package layout.
        /// </summary>
        [Fact]
        public void Export_WhenOneReferencedShaderExists_CopiesItIntoBuildShadersFolder() {
            string shaderId = "ForwardStandardShader";
            WriteCompiledShaderPackage(ShaderCacheRootPath, shaderId, ShaderCompileTarget.DirectX11);

            EditorShaderPackageExportService exportService = new EditorShaderPackageExportService(ShaderCacheRootPath);
            exportService.Export(new[] { shaderId }, ShaderCompileTarget.DirectX11, BuildRootPath);

            string exportedPackagePath = Path.Combine(BuildRootPath, "shaders", "ForwardStandardShader.dx11.shader.asset");
            Assert.True(File.Exists(exportedPackagePath));

            ShaderAsset exportedShaderAsset = LoadShaderAsset(exportedPackagePath);
            Assert.Equal(shaderId, exportedShaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(ShaderCompileTarget.DirectX11), exportedShaderAsset.TargetName);
        }

        /// <summary>
        /// Ensures the export service fails clearly when a referenced shader package is missing from the cache.
        /// </summary>
        [Fact]
        public void Export_WhenShaderPackageIsMissing_ThrowsClearError() {
            EditorShaderPackageExportService exportService = new EditorShaderPackageExportService(ShaderCacheRootPath);

            FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
                exportService.Export(new[] { "MissingShader" }, ShaderCompileTarget.DirectX11, BuildRootPath));

            Assert.Contains("MissingShader", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Writes one compiled shader package into the supplied cache root for export verification.
        /// </summary>
        /// <param name="shaderCacheRootPath">Shader cache root path receiving the package.</param>
        /// <param name="shaderAssetId">Shader asset identifier to encode.</param>
        /// <param name="target">Shader compile target to encode.</param>
        void WriteCompiledShaderPackage(string shaderCacheRootPath, string shaderAssetId, ShaderCompileTarget target) {
            if (string.IsNullOrWhiteSpace(shaderCacheRootPath)) {
                throw new ArgumentException("Shader cache root path must be provided.", nameof(shaderCacheRootPath));
            }

            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            string packagePath = ShaderPackagePaths.GetPackagePath(shaderCacheRootPath, shaderAssetId, target);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));
            ShaderAsset shaderAsset = CreateShaderAsset(shaderAssetId, target);
            using FileStream stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Loads one serialized shader asset from disk for verification.
        /// </summary>
        /// <param name="packagePath">Absolute shader package path.</param>
        /// <returns>Deserialized shader asset.</returns>
        ShaderAsset LoadShaderAsset(string packagePath) {
            using FileStream stream = File.OpenRead(packagePath);
            return Assert.IsType<ShaderAsset>(AssetSerializer.Deserialize(stream));
        }

        /// <summary>
        /// Creates a minimal serialized shader asset that matches the current export layout.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier.</param>
        /// <param name="target">Shader compile target.</param>
        /// <returns>Serialized shader asset payload.</returns>
        ShaderAsset CreateShaderAsset(string shaderAssetId, ShaderCompileTarget target) {
            string targetName = ShaderTargetNames.GetTargetName(target);
            return new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = targetName,
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = new[] {
                            new ShaderVariantAsset {
                                Name = "default",
                                Defines = Array.Empty<string>()
                            }
                        }
                    }
                },
                Binaries = new[] {
                    new ShaderBinaryAsset {
                        ProgramName = string.Concat(shaderAssetId, ".vs"),
                        Stage = ShaderStage.Vertex,
                        TargetName = targetName,
                        Variant = "default",
                        Bytecode = new byte[] { 1, 2, 3, 4 }
                    }
                }
            };
        }
    }
}

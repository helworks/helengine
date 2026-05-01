using System.Reflection;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the Windows build executor stages referenced shader packages into the final build root.
    /// </summary>
    public class EditorWindowsBuildExecutorTests : IDisposable {
        /// <summary>
        /// Temporary project root used for the executor test case.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used for the executor test case.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated project workspace for executor verification.
        /// </summary>
        public EditorWindowsBuildExecutorTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-windows-build-executor-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "output");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "shader-cache"));
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the temporary workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the executor's shader-export handoff copies referenced shader packages into the final build root.
        /// </summary>
        [Fact]
        public void ExportReferencedShaderPackages_WhenCalled_CopiesPackagesIntoBuildShadersFolder() {
            string shaderAssetId = "EditorDefaultMesh";
            WriteCompiledShaderPackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            EditorWindowsBuildExecutor executor = new EditorWindowsBuildExecutor(ProjectRootPath, "1.0.0-test");
            EditorWindowsBuildPaths buildPaths = new EditorWindowsBuildPaths(BuildRootPath);
            EditorWindowsBuildScenePackagerResult packageResult = new EditorWindowsBuildScenePackagerResult(new[] { shaderAssetId });

            InvokePrivate(executor, "ExportReferencedShaderPackages", packageResult, buildPaths);

            Assert.True(File.Exists(Path.Combine(BuildRootPath, "shaders", "EditorDefaultMesh.dx11.shader.asset")));
        }

        /// <summary>
        /// Writes one compiled shader package into the local shader cache for executor verification.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to encode.</param>
        /// <param name="target">Shader compile target to encode.</param>
        void WriteCompiledShaderPackage(string shaderAssetId, ShaderCompileTarget target) {
            string packagePath = ShaderPackagePaths.GetPackagePath(Path.Combine(ProjectRootPath, "shader-cache"), shaderAssetId, target);
            Directory.CreateDirectory(Path.GetDirectoryName(packagePath));

            ShaderAsset shaderAsset = new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = ShaderTargetNames.GetTargetName(target),
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
                        TargetName = ShaderTargetNames.GetTargetName(target),
                        Variant = "default",
                        Bytecode = new byte[] { 1, 2, 3, 4 }
                    }
                }
            };

            using FileStream stream = new FileStream(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Invokes one private executor method through reflection so the test can validate the build handoff without running the native toolchain.
        /// </summary>
        /// <param name="instance">Object instance whose private method should be called.</param>
        /// <param name="methodName">Private method name to invoke.</param>
        /// <param name="arguments">Arguments passed to the private method.</param>
        static void InvokePrivate(object instance, string methodName, params object[] arguments) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(methodName)) {
                throw new ArgumentException("Method name must be provided.", nameof(methodName));
            }

            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException($"Private method '{methodName}' was not found.");
            }

            method.Invoke(instance, arguments);
        }
    }
}

using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies Windows scene packaging collects referenced shader ids from packaged material assets.
    /// </summary>
    public class EditorWindowsBuildScenePackagerTests : IDisposable {
        /// <summary>
        /// Temporary project root used for scene-packager tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build root used for scene-packager tests.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Initializes one isolated project workspace for scene packaging verification.
        /// </summary>
        public EditorWindowsBuildScenePackagerTests() {
            string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-packager-tests", Guid.NewGuid().ToString("N"));
            ProjectRootPath = workspaceRootPath;
            BuildRootPath = Path.Combine(workspaceRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "shader-cache"));
            Directory.CreateDirectory(BuildRootPath);
        }

        /// <summary>
        /// Deletes the temporary project workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a packaged scene referencing one material reports one deduplicated shader id.
        /// </summary>
        [Fact]
        public void Package_WhenTwoMeshesReferenceTheSameShader_ReportsOneReferencedShaderId() {
            string sceneId = "Scenes/TestScene.helen";
            string materialRelativePath = "Materials/TestMaterial.helmat";
            string shaderAssetId = "EditorDefaultMesh";

            WriteShaderCachePackage(shaderAssetId, ShaderCompileTarget.DirectX11);
            WriteMaterialAsset(materialRelativePath, shaderAssetId);
            WriteSceneAsset(sceneId, materialRelativePath);

            EditorWindowsBuildScenePackager packager = new EditorWindowsBuildScenePackager(ProjectRootPath);
            EditorWindowsBuildScenePackagerResult result = packager.Package(new[] { sceneId }, BuildRootPath);

            Assert.Equal(new[] { shaderAssetId }, result.ReferencedShaderAssetIds);
        }

        /// <summary>
        /// Writes one serialized scene asset that references the supplied material path from a mesh component payload.
        /// </summary>
        /// <param name="sceneId">Scene asset id to write.</param>
        /// <param name="materialRelativePath">Project-relative material path to reference.</param>
        void WriteSceneAsset(string sceneId, string materialRelativePath) {
            string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));

            SceneAsset sceneAsset = new SceneAsset {
                Id = sceneId,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "root-entity",
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = "second-root-entity",
                        Name = "SecondRoot",
                        LocalPosition = new float3(1f, 0f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = "helengine.MeshComponent",
                                ComponentIndex = 0,
                                Payload = WriteMeshComponentPayload(materialRelativePath)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Writes one serialized material asset that references the supplied shader asset id.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to write.</param>
        /// <param name="shaderAssetId">Shader asset id referenced by the material.</param>
        void WriteMaterialAsset(string materialRelativePath, string shaderAssetId) {
            string materialPath = Path.Combine(ProjectRootPath, "assets", materialRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));

            MaterialAsset materialAsset = new MaterialAsset {
                Id = materialRelativePath,
                ShaderAssetId = shaderAssetId,
                VertexProgram = string.Concat(shaderAssetId, ".vs"),
                PixelProgram = string.Concat(shaderAssetId, ".ps"),
                Variant = "default",
                RenderState = new MaterialRenderState(),
                ConstantBuffers = Array.Empty<MaterialConstantBufferAsset>()
            };

            using FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Writes one compiled shader package into the local editor shader cache.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier to encode.</param>
        /// <param name="target">Shader compile target to encode.</param>
        void WriteShaderCachePackage(string shaderAssetId, ShaderCompileTarget target) {
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
        /// Writes one mesh-component payload that points at the supplied material path.
        /// </summary>
        /// <param name="materialRelativePath">Project-relative material path to encode.</param>
        /// <returns>Serialized mesh component payload.</returns>
        byte[] WriteMeshComponentPayload(string materialRelativePath) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(0);
            writer.WriteByte(1);
            writer.WriteInt32((int)SceneAssetReferenceSourceKind.FileSystem);
            writer.WriteString(materialRelativePath);
            writer.WriteString(string.Empty);
            writer.WriteString(string.Empty);
            writer.WriteByte(0);
            return stream.ToArray();
        }
    }
}

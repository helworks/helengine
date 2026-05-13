using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Imports model files through Assimp and converts them into the engine's dual-width indexed model asset format.
    /// </summary>
    public class HelengineAssimpImporter : IModelImporter {
        /// <summary>
        /// Vertex program used by generated standard imported materials.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";
        /// <summary>
        /// Pixel program used by generated standard imported materials.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";
        /// <summary>
        /// Default shader variant used by generated standard imported materials.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Assimp post-processing flags used to normalize imported geometry for the engine.
        /// </summary>
        const PostProcessSteps ImportPostProcessSteps =
            PostProcessSteps.Triangulate |
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.GenerateUVCoords |
            PostProcessSteps.ImproveCacheLocality;

        /// <summary>
        /// Converts imported scenes into engine model assets.
        /// </summary>
        readonly AssimpSceneModelAssetConverter SceneConverter = new AssimpSceneModelAssetConverter();

        /// <summary>
        /// Imports a model asset from the given data stream.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Imported model payload together with generated materials.</returns>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using AssimpContext importer = new AssimpContext();
            Scene scene = ImportScene(importer, stream);
            if (scene == null) {
                throw new InvalidOperationException("Assimp did not return a scene for the model stream.");
            }

            ModelAsset asset = SceneConverter.Convert(scene);
            ImportedModelMaterialAsset[] generatedMaterials = BuildGeneratedMaterials(scene, stream);
            return new ImportedModelAssetSet(asset, generatedMaterials);
        }

        /// <summary>
        /// Imports one Assimp scene, preferring file-backed import when a source file path is available so companion resources like `.mtl` can be resolved.
        /// </summary>
        /// <param name="importer">Assimp context used to read the model.</param>
        /// <param name="stream">Source model stream.</param>
        /// <returns>Imported Assimp scene.</returns>
        Scene ImportScene(AssimpContext importer, Stream stream) {
            if (importer == null) {
                throw new ArgumentNullException(nameof(importer));
            } else if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream is FileStream fileStream && !string.IsNullOrWhiteSpace(fileStream.Name) && File.Exists(fileStream.Name)) {
                return importer.ImportFile(fileStream.Name, ImportPostProcessSteps);
            }

            string formatHint = ResolveFormatHint(stream);
            return importer.ImportFileFromStream(stream, ImportPostProcessSteps, formatHint);
        }

        /// <summary>
        /// Resolves the Assimp stream format hint from a file stream name when available.
        /// </summary>
        /// <param name="stream">Source stream.</param>
        /// <returns>Lowercase format hint without a leading dot, or an empty string when unavailable.</returns>
        string ResolveFormatHint(Stream stream) {
            if (stream is FileStream fileStream) {
                string extension = Path.GetExtension(fileStream.Name);
                if (!string.IsNullOrWhiteSpace(extension)) {
                    return extension.TrimStart('.').ToLowerInvariant();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds generated `.hasset` payloads for the distinct materials referenced by the imported meshes.
        /// </summary>
        /// <param name="scene">Imported scene that owns the materials.</param>
        /// <param name="stream">Source stream used to resolve deterministic sibling output paths.</param>
        /// <returns>Generated material asset payloads keyed to the referenced mesh materials.</returns>
        ImportedModelMaterialAsset[] BuildGeneratedMaterials(Scene scene, Stream stream) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            string generatedMaterialDirectoryName = ResolveGeneratedMaterialDirectoryName(stream);
            HashSet<int> referencedMaterialIndices = CollectReferencedMaterialIndices(scene);
            string[] materialNames = AssimpMaterialNameCatalog.ResolveMaterialNames(scene);
            List<ImportedModelMaterialAsset> generatedMaterials = new List<ImportedModelMaterialAsset>(referencedMaterialIndices.Count);
            foreach (int materialIndex in referencedMaterialIndices.OrderBy(value => value)) {
                Material material = scene.Materials[materialIndex];
                string materialName = materialNames[materialIndex];
                string relativeMaterialPath = BuildRelativeMaterialPath(generatedMaterialDirectoryName, materialName);
                MaterialAsset materialAsset = BuildMaterialAsset(relativeMaterialPath, material);
                generatedMaterials.Add(new ImportedModelMaterialAsset(materialName, relativeMaterialPath, materialAsset));
            }

            return generatedMaterials.ToArray();
        }

        /// <summary>
        /// Collects the distinct material indices referenced by the imported meshes.
        /// </summary>
        /// <param name="scene">Imported scene whose mesh materials should be collected.</param>
        /// <returns>Distinct material indices referenced by the scene meshes.</returns>
        HashSet<int> CollectReferencedMaterialIndices(Scene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            HashSet<int> materialIndices = new HashSet<int>();
            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported scene contains a null mesh.");
                }
                if (mesh.MaterialIndex < 0 || mesh.MaterialIndex >= scene.MaterialCount) {
                    continue;
                }

                materialIndices.Add(mesh.MaterialIndex);
            }

            return materialIndices;
        }

        /// <summary>
        /// Builds one engine material asset from one imported Assimp material.
        /// </summary>
        /// <param name="relativeMaterialPath">Relative path where the generated material will be written.</param>
        /// <param name="material">Imported Assimp material definition.</param>
        /// <returns>Generated engine material asset.</returns>
        MaterialAsset BuildMaterialAsset(string relativeMaterialPath, Material material) {
            if (string.IsNullOrWhiteSpace(relativeMaterialPath)) {
                throw new ArgumentException("Relative material path must be provided.", nameof(relativeMaterialPath));
            } else if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            return new MaterialAsset {
                Id = relativeMaterialPath,
                ShaderAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = DefaultVariantName,
                DiffuseTextureAssetId = ResolveDiffuseTextureAssetId(material)
            };
        }

        /// <summary>
        /// Resolves the imported diffuse texture asset id from one Assimp material.
        /// </summary>
        /// <param name="material">Imported material definition.</param>
        /// <returns>Normalized diffuse texture asset id, or an empty string when the material has no diffuse texture.</returns>
        string ResolveDiffuseTextureAssetId(Material material) {
            if (material == null) {
                throw new ArgumentNullException(nameof(material));
            }

            if (material.GetMaterialTextureCount(TextureType.Diffuse) > 0) {
                if (material.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot textureSlot)) {
                    return NormalizeRelativePath(textureSlot.FilePath);
                }
            }

            if (material.HasTextureDiffuse) {
                return NormalizeRelativePath(material.TextureDiffuse.FilePath);
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves the deterministic sibling folder name used for generated material assets.
        /// </summary>
        /// <param name="stream">Source stream used to determine the source model name.</param>
        /// <returns>Sibling folder name derived from the source model file name, or an empty string when unavailable.</returns>
        string ResolveGeneratedMaterialDirectoryName(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream is FileStream fileStream && !string.IsNullOrWhiteSpace(fileStream.Name)) {
                return Path.GetFileNameWithoutExtension(fileStream.Name) ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds the relative path used when writing one generated material asset.
        /// </summary>
        /// <param name="generatedMaterialDirectoryName">Deterministic sibling folder name for the source model.</param>
        /// <param name="materialName">Stable material name resolved from the imported model.</param>
        /// <returns>Normalized relative material asset path.</returns>
        string BuildRelativeMaterialPath(string generatedMaterialDirectoryName, string materialName) {
            if (string.IsNullOrWhiteSpace(materialName)) {
                throw new ArgumentException("Material name must be provided.", nameof(materialName));
            }

            string fileName = string.Concat(materialName, EditorFileTemplateRegistry.MaterialExtension);
            if (string.IsNullOrWhiteSpace(generatedMaterialDirectoryName)) {
                return NormalizeRelativePath(fileName);
            }

            return NormalizeRelativePath(Path.Combine(generatedMaterialDirectoryName, fileName));
        }

        /// <summary>
        /// Normalizes a relative asset path to forward slashes for cross-platform asset identifiers.
        /// </summary>
        /// <param name="relativePath">Relative path to normalize.</param>
        /// <returns>Forward-slash normalized relative path.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/');
        }
    }
}

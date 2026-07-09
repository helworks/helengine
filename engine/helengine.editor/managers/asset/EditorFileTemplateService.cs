namespace helengine.editor {
    /// <summary>
    /// Creates new files from editor file templates.
    /// </summary>
    public static class EditorFileTemplateService {
        /// <summary>
        /// Creates a new file from the provided template in the target directory.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        /// <param name="directory">Target directory for the new file.</param>
        public static void CreateFile(EditorFileTemplate template, string directory) {
            if (template == null) {
                throw new ArgumentNullException(nameof(template));
            }
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new ArgumentException("Target directory must be provided.", nameof(directory));
            }

            Directory.CreateDirectory(directory);
            switch (template.Kind) {
                case EditorFileTemplateKind.Text:
                    CreateTextFile(template, directory);
                    break;
                case EditorFileTemplateKind.Shader:
                    CreateShaderFile(template, directory);
                    break;
                case EditorFileTemplateKind.Material:
                    CreateMaterialFile(template, directory);
                    break;
                case EditorFileTemplateKind.Blueprint:
                    CreateBlueprintFile(template, directory);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported file template kind.");
            }
        }

        /// <summary>
        /// Creates a text file using the template contents.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        /// <param name="directory">Target directory.</param>
        static void CreateTextFile(EditorFileTemplate template, string directory) {
            string fileName = AssetCreationUtils.BuildUniqueFileName(directory, template.DefaultName, template.Extension);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, template.DefaultContents ?? string.Empty);
        }

        /// <summary>
        /// Creates a shader source file using the template contents.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        /// <param name="directory">Target directory.</param>
        static void CreateShaderFile(EditorFileTemplate template, string directory) {
            string fileName = AssetCreationUtils.BuildUniqueFileName(directory, template.DefaultName, template.Extension);
            string path = Path.Combine(directory, fileName);
            File.WriteAllText(path, template.DefaultContents ?? string.Empty);
        }

        /// <summary>
        /// Creates a serialized material asset and its companion shader source if needed.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        /// <param name="directory">Target directory.</param>
        static void CreateMaterialFile(EditorFileTemplate template, string directory) {
            string fileName = AssetCreationUtils.BuildUniqueFileName(directory, template.DefaultName, template.Extension);
            string materialPath = Path.Combine(directory, fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(baseName)) {
                throw new InvalidOperationException("Material base name could not be resolved.");
            }

            string materialId = BuildMaterialAssetId(materialPath);

            var materialAsset = new MaterialAsset { Id = materialId };

            using (FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, materialAsset);
            }
        }

        /// <summary>
        /// Creates a serialized blueprint asset with one blank root entity.
        /// </summary>
        /// <param name="template">Template to apply.</param>
        /// <param name="directory">Target directory.</param>
        static void CreateBlueprintFile(EditorFileTemplate template, string directory) {
            string fileName = AssetCreationUtils.BuildUniqueFileName(directory, template.DefaultName, template.Extension);
            string blueprintPath = Path.Combine(directory, fileName);

            BlueprintAsset blueprintAsset = new BlueprintAsset {
                Id = BuildBlueprintAssetId(blueprintPath),
                RootEntity = new SceneEntityAsset {
                    Id = 1u,
                    Name = "Root",
                    Enabled = true,
                    LayerMask = 0b00000001,
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                },
                AssetReferences = Array.Empty<SceneAssetReference>()
            };

            using FileStream stream = new FileStream(blueprintPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, blueprintAsset);
        }

        /// <summary>
        /// Builds the material asset id for a material source path.
        /// </summary>
        /// <param name="materialPath">Absolute material asset path.</param>
        /// <returns>Material asset id derived from the path.</returns>
        static string BuildMaterialAssetId(string materialPath) {
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }

            string assetsRoot = EditorProjectPaths.AssetsRoot;
            if (string.IsNullOrWhiteSpace(assetsRoot)) {
                throw new InvalidOperationException("Assets root path has not been initialized.");
            }

            string fullMaterialPath = Path.GetFullPath(materialPath);
            string fullAssetsRoot = Path.GetFullPath(assetsRoot);
            if (!IsPathUnderRoot(fullMaterialPath, fullAssetsRoot)) {
                throw new InvalidOperationException("Material path must be located under the assets root.");
            }

            string relativePath = Path.GetRelativePath(fullAssetsRoot, fullMaterialPath);
            string withoutExtension = Path.ChangeExtension(relativePath, null);
            if (string.IsNullOrWhiteSpace(withoutExtension)) {
                throw new InvalidOperationException("Material id could not be resolved from the path.");
            }

            string normalized = withoutExtension.Replace(Path.DirectorySeparatorChar, '.');
            normalized = normalized.Replace(Path.AltDirectorySeparatorChar, '.');
            return string.Concat(normalized, ".material");
        }

        /// <summary>
        /// Builds the blueprint asset id for a blueprint source path.
        /// </summary>
        /// <param name="blueprintPath">Absolute blueprint asset path.</param>
        /// <returns>Blueprint asset id derived from the path.</returns>
        static string BuildBlueprintAssetId(string blueprintPath) {
            if (string.IsNullOrWhiteSpace(blueprintPath)) {
                throw new ArgumentException("Blueprint path must be provided.", nameof(blueprintPath));
            }

            string assetsRoot = EditorProjectPaths.AssetsRoot;
            if (string.IsNullOrWhiteSpace(assetsRoot)) {
                throw new InvalidOperationException("Assets root path has not been initialized.");
            }

            string fullBlueprintPath = Path.GetFullPath(blueprintPath);
            string fullAssetsRoot = Path.GetFullPath(assetsRoot);
            if (!IsPathUnderRoot(fullBlueprintPath, fullAssetsRoot)) {
                throw new InvalidOperationException("Blueprint path must be located under the assets root.");
            }

            string relativePath = Path.GetRelativePath(fullAssetsRoot, fullBlueprintPath);
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new InvalidOperationException("Blueprint id could not be resolved from the path.");
            }

            return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        /// <summary>
        /// Determines whether a path is located under a root directory.
        /// </summary>
        /// <param name="path">Path to test.</param>
        /// <param name="root">Root directory to compare.</param>
        /// <returns>True when the path is under the root.</returns>
        static bool IsPathUnderRoot(string path, string root) {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) {
                return false;
            }

            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                root = root + Path.DirectorySeparatorChar;
            }

            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// Always-first scene contributor: writes one model per mesh in mesh-local space, one material per used source material,
    /// and creates the blueprint entities that mirror the node tree with a mesh component per mesh index.
    /// </summary>
    public sealed class CoreImportedSceneContributor : IImportedSceneContributor {
        /// <summary>
        /// Identifier of the core contributor.
        /// </summary>
        public const string CoreContributorId = "helengine.scene.core";

        /// <summary>
        /// Component type id of the mesh component.
        /// </summary>
        const string MeshComponentTypeId = "helengine.MeshComponent";

        /// <summary>
        /// Layer mask of freshly authored entities.
        /// </summary>
        const ushort DefaultLayerMask = 1;

        /// <summary>
        /// Extension of engine-native model files.
        /// </summary>
        const string NativeModelExtension = ".hasset";

        /// <summary>
        /// Folder inside the generated folder that holds the mesh models.
        /// </summary>
        const string MeshFolder = "mesh";

        /// <summary>
        /// Folder inside the generated folder that holds the materials.
        /// </summary>
        const string MaterialFolder = "materials";

        /// <summary>
        /// Folder inside the generated folder that holds the extracted texture images.
        /// </summary>
        const string TextureFolder = "textures";

        /// <summary>
        /// Vertex program of the standard shader, matching the single-model importer.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";

        /// <summary>
        /// Pixel program of the standard shader, matching the single-model importer.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";

        /// <summary>
        /// Shader variant of the standard shader, matching the single-model importer.
        /// </summary>
        const string DefaultVariantName = "default";

        /// <summary>
        /// Builds the mesh component payloads.
        /// </summary>
        readonly GeneratedSceneComponentAuthoringService ComponentAuthoringService = new GeneratedSceneComponentAuthoringService();

        /// <summary>
        /// Gets the stable identifier of this contributor.
        /// </summary>
        public string ContributorId => CoreContributorId;

        /// <summary>
        /// Adds the model and material files and creates the blueprint entity tree.
        /// </summary>
        /// <param name="scene">Neutral scene data read from the source file.</param>
        /// <param name="output">Collects the generated files and the blueprint entities.</param>
        public void Contribute(ImportedSceneData scene, ImportedSceneOutput output) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }

            string[] materialPaths = AddMaterials(scene, output);
            string[] meshPaths = AddMeshes(scene, output, materialPaths);
            HashSet<uint> usedEntityIds = new HashSet<uint>();
            CreateEntity(scene.RootNode, scene, output, meshPaths, materialPaths, usedEntityIds);
        }

        /// <summary>
        /// Adds one material file for every material a mesh uses.
        /// </summary>
        /// <param name="scene">Scene data being imported.</param>
        /// <param name="output">Output receiving the files.</param>
        /// <returns>Relative file path per material index; null for materials no mesh uses.</returns>
        string[] AddMaterials(ImportedSceneData scene, ImportedSceneOutput output) {
            string[] paths = new string[scene.Materials.Length];
            string[] texturePaths = new string[scene.Textures.Length];
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedTextureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int meshIndex = 0; meshIndex < scene.Meshes.Length; meshIndex++) {
                int materialIndex = scene.Meshes[meshIndex].MaterialIndex;
                if (materialIndex < 0 || materialIndex >= scene.Materials.Length) {
                    throw new InvalidOperationException($"Mesh '{scene.Meshes[meshIndex].Name}' uses material index {materialIndex}, which the scene does not contain.");
                }
                if (paths[materialIndex] != null) {
                    continue;
                }

                ImportedSceneMaterial material = scene.Materials[materialIndex];
                string fileName = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(material.Name), usedNames);
                string path = MaterialFolder + "/" + fileName + EditorFileTemplateRegistry.MaterialExtension;
                ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                    Id = output.GeneratedFolderRelativePath + "/" + path,
                    ShaderAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId,
                    VertexProgram = StandardVertexProgramName,
                    PixelProgram = StandardPixelProgramName,
                    Variant = DefaultVariantName,
                    DiffuseTextureAssetId = AddBaseColorTexture(scene, output, material, texturePaths, usedTextureNames)
                };
                output.AddGeneratedFile(path, materialAsset);
                output.MarkMaterialConsumed(materialIndex);
                paths[materialIndex] = path;
            }

            return paths;
        }

        /// <summary>
        /// Writes the base color image of a material into the generated folder, once per texture, and returns the id the material binds it by.
        /// Normal and roughness slots stay in the scene data and are not wired into the generated material yet.
        /// </summary>
        /// <param name="scene">Scene data being imported.</param>
        /// <param name="output">Output receiving the image file.</param>
        /// <param name="material">Material whose base color slot is resolved.</param>
        /// <param name="texturePaths">Relative image path per texture index, filled as images are written.</param>
        /// <param name="usedTextureNames">Image file names already taken in the textures folder.</param>
        /// <returns>The assets-relative path of the image, or an empty string when the material has no base color texture.</returns>
        string AddBaseColorTexture(ImportedSceneData scene, ImportedSceneOutput output, ImportedSceneMaterial material, string[] texturePaths, HashSet<string> usedTextureNames) {
            int textureIndex = ImportedSceneTextureReference.Parse(material.BaseColorTexturePath);
            if (textureIndex < 0) {
                return string.Empty;
            } else if (textureIndex >= scene.Textures.Length) {
                throw new InvalidOperationException($"Material '{material.Name}' refers to texture {textureIndex}, which the scene does not contain.");
            }

            if (texturePaths[textureIndex] == null) {
                ImportedSceneTexture texture = scene.Textures[textureIndex];
                string fileName = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(texture.Name), usedTextureNames);
                string path = TextureFolder + "/" + fileName + "." + texture.FormatHint;
                if (!output.IsTextureExtensionSupported("." + texture.FormatHint)) {
                    throw new InvalidOperationException($"Material '{material.Name}' uses a .{texture.FormatHint} texture, but the project has no texture importer registered for the extension .{texture.FormatHint}.");
                }
                output.AddGeneratedSourceFile(path, texture.IsEmbedded ? texture.EncodedBytes : output.ReadExternalFile(texture.ExternalRelativePath, material.Name));
                texturePaths[textureIndex] = path;
            }

            return output.GeneratedFolderRelativePath + "/" + texturePaths[textureIndex];
        }

        /// <summary>
        /// Adds one model file per mesh.
        /// </summary>
        /// <param name="scene">Scene data being imported.</param>
        /// <param name="output">Output receiving the files.</param>
        /// <param name="materialPaths">Relative material file path per material index.</param>
        /// <returns>Relative model file path per mesh index.</returns>
        string[] AddMeshes(ImportedSceneData scene, ImportedSceneOutput output, string[] materialPaths) {
            string[] paths = new string[scene.Meshes.Length];
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int meshIndex = 0; meshIndex < scene.Meshes.Length; meshIndex++) {
                ImportedSceneMesh mesh = scene.Meshes[meshIndex];
                string name = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(mesh.Name), usedNames);
                string path = MeshFolder + "/" + name + NativeModelExtension;
                ModelAsset model = BuildModelAsset(mesh, scene.Materials[mesh.MaterialIndex].Name);
                model.Id = output.SourceAssetId + "#" + MeshFolder + "/" + name;
                output.AddGeneratedFile(path, model);
                output.MarkMeshConsumed(meshIndex);
                paths[meshIndex] = path;
            }

            return paths;
        }

        /// <summary>
        /// Converts one scene mesh to a model asset in mesh-local space.
        /// </summary>
        /// <param name="mesh">Scene mesh.</param>
        /// <param name="materialSlotName">Name of the material the mesh uses.</param>
        /// <returns>Model asset with one submesh.</returns>
        ModelAsset BuildModelAsset(ImportedSceneMesh mesh, string materialSlotName) {
            ModelAsset model = new ModelAsset {
                Positions = (float3[])mesh.Positions.Clone(),
                Normals = (float3[])mesh.Normals.Clone(),
                TexCoords = (float2[])mesh.TexCoords.Clone(),
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        IndexStart = 0,
                        IndexCount = mesh.Indices.Length,
                        MaterialSlotName = materialSlotName
                    }
                }
            };
            if (mesh.Positions.Length > ushort.MaxValue + 1) {
                model.Indices32 = (uint[])mesh.Indices.Clone();
            } else {
                ushort[] indices = new ushort[mesh.Indices.Length];
                for (int index = 0; index < indices.Length; index++) {
                    indices[index] = checked((ushort)mesh.Indices[index]);
                }
                model.Indices16 = indices;
            }

            return model;
        }

        /// <summary>
        /// Creates the entity for a node, its mesh components and its children, and registers them on the output.
        /// </summary>
        /// <param name="node">Node to mirror.</param>
        /// <param name="scene">Scene data being imported.</param>
        /// <param name="output">Output receiving the entity.</param>
        /// <param name="meshPaths">Relative model file path per mesh index.</param>
        /// <param name="materialPaths">Relative material file path per material index.</param>
        /// <param name="usedEntityIds">Entity ids already taken in this blueprint.</param>
        /// <returns>The created entity.</returns>
        SceneEntityAsset CreateEntity(
            ImportedSceneNode node,
            ImportedSceneData scene,
            ImportedSceneOutput output,
            string[] meshPaths,
            string[] materialPaths,
            HashSet<uint> usedEntityIds) {
            SceneComponentAssetRecord[] components = new SceneComponentAssetRecord[node.MeshIndices.Length];
            for (int index = 0; index < components.Length; index++) {
                int meshIndex = node.MeshIndices[index];
                SceneAssetReference model = output.ReferenceGeneratedFile(meshPaths[meshIndex], AssetEntryKind.Model);
                SceneAssetReference material = output.ReferenceGeneratedFile(materialPaths[scene.Meshes[meshIndex].MaterialIndex], AssetEntryKind.Material);
                components[index] = new SceneComponentAssetRecord {
                    ComponentTypeId = MeshComponentTypeId,
                    ComponentIndex = index,
                    Payload = ComponentAuthoringService.CreateMeshPayload(model, material)
                };
            }

            SceneEntityAsset[] children = new SceneEntityAsset[node.Children.Length];
            for (int index = 0; index < children.Length; index++) {
                children[index] = CreateEntity(node.Children[index], scene, output, meshPaths, materialPaths, usedEntityIds);
            }

            SceneEntityAsset entity = new SceneEntityAsset {
                Id = ReserveEntityId(node.Path, usedEntityIds),
                Name = node.Name,
                Enabled = true,
                LayerMask = DefaultLayerMask,
                LocalPosition = node.LocalPosition,
                LocalScale = node.LocalScale,
                LocalOrientation = node.LocalRotation,
                Components = components,
                Children = children
            };
            output.RegisterNodeEntity(node.Path, entity);
            return entity;
        }

        /// <summary>
        /// Derives a deterministic non-zero entity id from the node path and resolves hash collisions deterministically.
        /// </summary>
        /// <param name="nodePath">Unique node path.</param>
        /// <param name="usedEntityIds">Entity ids already taken in this blueprint.</param>
        /// <returns>Entity id unique within the blueprint.</returns>
        static uint ReserveEntityId(string nodePath, HashSet<uint> usedEntityIds) {
            string key = "node:" + nodePath;
            uint id = StableHash(key);
            int attempt = 1;
            while (!usedEntityIds.Add(id)) {
                id = StableHash(key + "#" + attempt);
                attempt++;
            }

            return id;
        }

        /// <summary>
        /// Computes a 32-bit FNV-1a hash that is never zero.
        /// </summary>
        /// <param name="value">Text to hash.</param>
        /// <returns>Non-zero hash.</returns>
        static uint StableHash(string value) {
            uint hash = 2166136261u;
            for (int index = 0; index < value.Length; index++) {
                hash ^= value[index];
                hash *= 16777619u;
            }

            return hash == 0 ? 1u : hash;
        }
    }
}

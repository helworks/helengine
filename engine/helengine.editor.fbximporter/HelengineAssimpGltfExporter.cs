using helengine;
using helengine.editor;

namespace helengine.editor.assimp {
    /// <summary>
    /// Exports the live editor scene's mesh entities into one glTF file through the Assimp export pipeline.
    /// </summary>
    public sealed class HelengineAssimpGltfExporter : ISceneMeshExporter {
        /// <summary>
        /// Exports the supplied live root entities into one glTF file on disk.
        /// </summary>
        /// <param name="rootEntities">Live scene root entities to export.</param>
        /// <param name="assetsRootPath">Absolute project assets root used to resolve referenced model assets.</param>
        /// <param name="outputPath">Absolute output file path; `.glb` selects binary glTF and any other extension selects text glTF.</param>
        /// <returns>Human-readable one-line export summary.</returns>
        public string Export(IReadOnlyList<Entity> rootEntities, string assetsRootPath, string outputPath) {
            if (rootEntities == null) {
                throw new ArgumentNullException(nameof(rootEntities));
            }
            if (string.IsNullOrWhiteSpace(assetsRootPath)) {
                throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath));
            }
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            Assimp.Scene exportScene = new Assimp.Scene {
                RootNode = new Assimp.Node("Scene")
            };
            exportScene.Materials.Add(new Assimp.Material { Name = "Default" });

            ExportState state = new ExportState(assetsRootPath);
            for (int rootIndex = 0; rootIndex < rootEntities.Count; rootIndex++) {
                AppendEntityRecursive(rootEntities[rootIndex], exportScene.RootNode, exportScene, state);
            }

            if (state.ExportedMeshCount == 0) {
                throw new InvalidOperationException("The current scene contains no exportable mesh entities.");
            }

            string directoryPath = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            string formatId = outputPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ? "glb2" : "gltf2";
            using Assimp.AssimpContext context = new Assimp.AssimpContext();
            if (!context.ExportFile(exportScene, outputPath, formatId)) {
                throw new InvalidOperationException($"Assimp failed to export '{outputPath}' with format '{formatId}'.");
            }

            string summary = $"Exported {state.ExportedMeshCount} meshes to {outputPath}";
            if (state.SkippedEntityNames.Count > 0) {
                summary += $" (skipped without model data: {string.Join(", ", state.SkippedEntityNames)})";
            }

            return summary;
        }

        /// <summary>
        /// Walks one live entity subtree and appends a flat world-space node for every mesh-carrying entity.
        /// </summary>
        /// <param name="entity">Live entity to inspect.</param>
        /// <param name="parentNode">Assimp scene root receiving flat mesh nodes.</param>
        /// <param name="exportScene">Assimp scene collecting meshes and materials.</param>
        /// <param name="state">Shared export bookkeeping.</param>
        void AppendEntityRecursive(Entity entity, Assimp.Node parentNode, Assimp.Scene exportScene, ExportState state) {
            if (entity == null || !entity.Enabled) {
                return;
            }
            if (entity is EditorEntity editorEntity && editorEntity.InternalEntity) {
                return;
            }

            MeshComponent meshComponent = FindMeshComponent(entity);
            if (meshComponent != null) {
                string entityName = entity is EditorEntity namedEditorEntity ? namedEditorEntity.Name : null;
                string nodeName = string.IsNullOrWhiteSpace(entityName) ? $"Entity{state.NextAnonymousNodeIndex++}" : entityName;
                ModelAsset modelAsset = ResolveModelAsset(entity, meshComponent, state);
                if (modelAsset != null) {
                    Assimp.Node node = new Assimp.Node(nodeName);
                    node.Transform = BuildAssimpWorldTransform(entity);
                    parentNode.Children.Add(node);
                    AppendModelMeshes(modelAsset, nodeName, node, exportScene, state);
                } else {
                    state.SkippedEntityNames.Add(nodeName);
                }
            }

            if (entity.Children != null) {
                for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                    AppendEntityRecursive(entity.Children[childIndex], parentNode, exportScene, state);
                }
            }
        }

        /// <summary>
        /// Appends one model asset's submeshes as Assimp meshes attached to the supplied node.
        /// </summary>
        /// <param name="modelAsset">Source model geometry.</param>
        /// <param name="nodeName">Owning node name used to label meshes.</param>
        /// <param name="node">Assimp node that references the appended meshes.</param>
        /// <param name="exportScene">Assimp scene collecting meshes and materials.</param>
        /// <param name="state">Shared export bookkeeping.</param>
        void AppendModelMeshes(ModelAsset modelAsset, string nodeName, Assimp.Node node, Assimp.Scene exportScene, ExportState state) {
            if (modelAsset.Positions == null || modelAsset.Indices16 == null || modelAsset.Positions.Length == 0 || modelAsset.Indices16.Length == 0) {
                state.SkippedEntityNames.Add(nodeName);
                return;
            }

            ModelSubmeshAsset[] submeshes = modelAsset.Submeshes is { Length: > 0 }
                ? modelAsset.Submeshes
                : [new ModelSubmeshAsset { MaterialSlotName = "Default", IndexStart = 0, IndexCount = modelAsset.Indices16.Length }];

            foreach (ModelSubmeshAsset submesh in submeshes) {
                if (submesh == null || submesh.IndexCount <= 0) {
                    continue;
                }

                Assimp.Mesh mesh = new Assimp.Mesh($"{nodeName}.{submesh.MaterialSlotName ?? "Submesh"}", Assimp.PrimitiveType.Triangle);
                for (int vertexIndex = 0; vertexIndex < modelAsset.Positions.Length; vertexIndex++) {
                    float3 position = modelAsset.Positions[vertexIndex];
                    mesh.Vertices.Add(new System.Numerics.Vector3(position.X, position.Y, position.Z));
                    if (modelAsset.Normals != null && vertexIndex < modelAsset.Normals.Length) {
                        float3 normal = modelAsset.Normals[vertexIndex];
                        mesh.Normals.Add(new System.Numerics.Vector3(normal.X, normal.Y, normal.Z));
                    }
                    if (modelAsset.TexCoords != null && vertexIndex < modelAsset.TexCoords.Length) {
                        float2 texCoord = modelAsset.TexCoords[vertexIndex];
                        mesh.TextureCoordinateChannels[0].Add(new System.Numerics.Vector3(texCoord.X, texCoord.Y, 0f));
                    }
                }

                if (mesh.TextureCoordinateChannels[0].Count > 0) {
                    mesh.UVComponentCount[0] = 2;
                }

                for (int index = submesh.IndexStart; index + 2 < submesh.IndexStart + submesh.IndexCount; index += 3) {
                    mesh.Faces.Add(new Assimp.Face([
                        modelAsset.Indices16[index],
                        modelAsset.Indices16[index + 1],
                        modelAsset.Indices16[index + 2]
                    ]));
                }

                mesh.MaterialIndex = state.GetOrCreateMaterialIndex(exportScene, submesh.MaterialSlotName);
                exportScene.Meshes.Add(mesh);
                node.MeshIndices.Add(exportScene.Meshes.Count - 1);
                state.ExportedMeshCount++;
            }
        }

        /// <summary>
        /// Resolves the CPU model geometry for one mesh entity from retained raw data or its persisted model asset reference.
        /// </summary>
        /// <param name="entity">Entity owning the mesh component.</param>
        /// <param name="meshComponent">Mesh component whose geometry should be resolved.</param>
        /// <param name="state">Shared export bookkeeping with the model cache.</param>
        /// <returns>Resolved model asset, or <c>null</c> when no CPU geometry source exists.</returns>
        ModelAsset ResolveModelAsset(Entity entity, MeshComponent meshComponent, ExportState state) {
            if (meshComponent.Model?.RawModelAsset != null) {
                return meshComponent.Model.RawModelAsset;
            }

            EntitySaveComponent saveComponent = FindSaveComponent(entity);
            if (saveComponent == null
                || !saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState componentState)
                || !componentState.TryGetAssetReference("Model", out SceneAssetReference modelReference)) {
                return null;
            }

            if (modelReference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return state.GetOrCreateEngineModel(modelReference.AssetId);
            }

            if (string.IsNullOrWhiteSpace(modelReference.RelativePath)) {
                return null;
            }

            if (state.ModelAssetsByRelativePath.TryGetValue(modelReference.RelativePath, out ModelAsset cachedModel)) {
                return cachedModel;
            }

            ModelAsset loadedModel = null;
            string fullPath = Path.Combine(state.AssetsRootPath, modelReference.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) {
                try {
                    using FileStream stream = File.OpenRead(fullPath);
                    loadedModel = AssetSerializer.Deserialize(stream) as ModelAsset;
                } catch (Exception) {
                    loadedModel = null;
                }
            }

            state.ModelAssetsByRelativePath[modelReference.RelativePath] = loadedModel;
            return loadedModel;
        }

        /// <summary>
        /// Builds the Assimp-column-vector local transform for one entity from its engine row-vector scale, rotation, and translation.
        /// </summary>
        /// <param name="entity">Entity whose world transform should be converted.</param>
        /// <returns>Assimp column-translation world transform matrix.</returns>
        static System.Numerics.Matrix4x4 BuildAssimpWorldTransform(Entity entity) {
            float3 scale = entity.Scale;
            float4x4.CreateScale(scale.X, scale.Y, scale.Z, out float4x4 scaleMatrix);
            float4 orientation = entity.Orientation;
            float4x4.CreateFromQuaternion(ref orientation, out float4x4 rotationMatrix);
            float3 position = entity.Position;
            float4x4.CreateTranslation(ref position, out float4x4 translationMatrix);

            float4x4.Multiply(ref scaleMatrix, ref rotationMatrix, out float4x4 scaleRotation);
            float4x4.Multiply(ref scaleRotation, ref translationMatrix, out float4x4 local);

            return new System.Numerics.Matrix4x4(
                local.M11, local.M21, local.M31, local.M41,
                local.M12, local.M22, local.M32, local.M42,
                local.M13, local.M23, local.M33, local.M43,
                local.M14, local.M24, local.M34, local.M44);
        }

        /// <summary>
        /// Finds the first mesh component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached mesh component, or <c>null</c>.</returns>
        static MeshComponent FindMeshComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is MeshComponent meshComponent) {
                    return meshComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the entity save component attached to one entity.
        /// </summary>
        /// <param name="entity">Entity to inspect.</param>
        /// <returns>Attached save component, or <c>null</c>.</returns>
        static EntitySaveComponent FindSaveComponent(Entity entity) {
            if (entity.Components == null) {
                return null;
            }

            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            return null;
        }

        /// <summary>
        /// Shared bookkeeping carried through one export pass.
        /// </summary>
        sealed class ExportState {
            /// <summary>
            /// Initializes bookkeeping rooted at one project assets folder.
            /// </summary>
            /// <param name="assetsRootPath">Absolute project assets root.</param>
            public ExportState(string assetsRootPath) {
                AssetsRootPath = assetsRootPath;
            }

            /// <summary>
            /// Absolute project assets root used to resolve referenced model assets.
            /// </summary>
            public string AssetsRootPath { get; }

            /// <summary>
            /// Number of Assimp meshes appended so far.
            /// </summary>
            public int ExportedMeshCount { get; set; }

            /// <summary>
            /// Counter used to label unnamed entities deterministically.
            /// </summary>
            public int NextAnonymousNodeIndex { get; set; }

            /// <summary>
            /// Names of mesh entities skipped because no CPU geometry source was available.
            /// </summary>
            public List<string> SkippedEntityNames { get; } = new List<string>();

            /// <summary>
            /// Cache of resolved model assets by project-relative reference path.
            /// </summary>
            public Dictionary<string, ModelAsset> ModelAssetsByRelativePath { get; } = new Dictionary<string, ModelAsset>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Material indices previously created for submesh slot names.
            /// </summary>
            readonly Dictionary<string, int> MaterialIndicesBySlotName = new Dictionary<string, int>(StringComparer.Ordinal);

            /// <summary>
            /// Cache of synthesized engine-generated model assets by generated asset id.
            /// </summary>
            readonly Dictionary<string, ModelAsset> EngineModelsByAssetId = new Dictionary<string, ModelAsset>(StringComparer.Ordinal);

            /// <summary>
            /// Returns the synthesized engine primitive for one generated model asset id, or <c>null</c> for unknown ids.
            /// </summary>
            /// <param name="assetId">Generated provider asset id.</param>
            /// <returns>Synthesized model asset, or <c>null</c>.</returns>
            public ModelAsset GetOrCreateEngineModel(string assetId) {
                if (string.IsNullOrWhiteSpace(assetId)) {
                    return null;
                }
                if (EngineModelsByAssetId.TryGetValue(assetId, out ModelAsset cachedModel)) {
                    return cachedModel;
                }

                ModelAsset generatedModel = null;
                if (string.Equals(assetId, ModelUtils.GeneratedCubeModelId, StringComparison.Ordinal)) {
                    generatedModel = ModelUtils.GenerateCubeMesh(float3.Zero, float3.One);
                } else if (string.Equals(assetId, ModelUtils.GeneratedSphereModelId, StringComparison.Ordinal)) {
                    generatedModel = ModelUtils.GenerateSphereMesh(float3.Zero, float3.One);
                } else if (string.Equals(assetId, ModelUtils.GeneratedPlaneModelId, StringComparison.Ordinal)) {
                    generatedModel = ModelUtils.GeneratePlaneMesh(float3.Zero, float3.One);
                }

                EngineModelsByAssetId[assetId] = generatedModel;
                return generatedModel;
            }

            /// <summary>
            /// Returns the Assimp material index for one submesh slot name, creating a named material on first use.
            /// </summary>
            /// <param name="exportScene">Assimp scene collecting materials.</param>
            /// <param name="materialSlotName">Submesh material slot name.</param>
            /// <returns>Assimp material index.</returns>
            public int GetOrCreateMaterialIndex(Assimp.Scene exportScene, string materialSlotName) {
                string slotName = string.IsNullOrWhiteSpace(materialSlotName) ? "Default" : materialSlotName;
                if (MaterialIndicesBySlotName.TryGetValue(slotName, out int existingIndex)) {
                    return existingIndex;
                }

                exportScene.Materials.Add(new Assimp.Material { Name = slotName });
                int materialIndex = exportScene.Materials.Count - 1;
                MaterialIndicesBySlotName[slotName] = materialIndex;
                return materialIndex;
            }
        }
    }
}

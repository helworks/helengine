using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Converts Assimp scenes into flattened engine model assets with either 16-bit or 32-bit index buffers.
    /// </summary>
    public class AssimpSceneModelAssetConverter {
        /// <summary>
        /// Converts an Assimp scene into one flattened engine model asset.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <returns>Flattened model asset.</returns>
        public ModelAsset Convert(Scene scene) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            if (!scene.HasMeshes || scene.MeshCount == 0) {
                throw new InvalidOperationException("Imported model does not contain any meshes.");
            }

            bool usesNodeHierarchy = scene.RootNode != null && CountNodeMeshInstances(scene.RootNode) > 0;
            int totalVertices = usesNodeHierarchy ? CountNodeVertices(scene.RootNode, scene) : CountVertices(scene);
            int totalIndices = usesNodeHierarchy ? CountNodeIndices(scene.RootNode, scene) : CountIndices(scene);
            if (totalVertices == 0) {
                throw new InvalidOperationException("Imported model does not contain any vertices.");
            }

            string[] materialNames = AssimpMaterialNameCatalog.ResolveMaterialNames(scene);
            float3[] positions = new float3[totalVertices];
            float3[] normals = new float3[totalVertices];
            float2[] texCoords = new float2[totalVertices];
            List<ModelSubmeshAsset> submeshes = new List<ModelSubmeshAsset>(usesNodeHierarchy ? CountNodeMeshInstances(scene.RootNode) : scene.MeshCount);
            int vertexOffset = 0;
            int indexOffset = 0;
            bool uses32BitIndices = totalVertices > ushort.MaxValue + 1;
            ushort[] indices16 = null;
            uint[] indices32 = null;
            if (uses32BitIndices) {
                indices32 = new uint[totalIndices];
            } else {
                indices16 = new ushort[totalIndices];
            }

            if (usesNodeHierarchy) {
                CopyNodeMeshes(
                    scene.RootNode,
                    scene,
                    positions,
                    normals,
                    texCoords,
                    indices16,
                    indices32,
                    materialNames,
                    submeshes,
                    ref vertexOffset,
                    ref indexOffset,
                    System.Numerics.Matrix4x4.Identity);
            } else {
                CopySceneMeshes(
                    scene,
                    positions,
                    normals,
                    texCoords,
                    indices16,
                    indices32,
                    materialNames,
                    submeshes,
                    ref vertexOffset,
                    ref indexOffset);
            }

            return new ModelAsset {
                Positions = positions,
                Normals = normals,
                TexCoords = texCoords,
                Indices16 = indices16,
                Indices32 = indices32,
                Submeshes = submeshes.ToArray()
            };
        }

        /// <summary>
        /// Copies a flat scene mesh collection into the engine model arrays.
        /// </summary>
        /// <param name="scene">Imported scene that owns the meshes.</param>
        /// <param name="positions">Destination position array.</param>
        /// <param name="normals">Destination normal array.</param>
        /// <param name="texCoords">Destination texture coordinate array.</param>
        /// <param name="indices16">Destination 16-bit index array when used.</param>
        /// <param name="indices32">Destination 32-bit index array when used.</param>
        /// <param name="materialNames">Resolved unique material names keyed by material index.</param>
        /// <param name="submeshes">Destination submesh list.</param>
        /// <param name="vertexOffset">Current destination vertex offset.</param>
        /// <param name="indexOffset">Current destination index offset.</param>
        void CopySceneMeshes(
            Scene scene,
            float3[] positions,
            float3[] normals,
            float2[] texCoords,
            ushort[] indices16,
            uint[] indices32,
            string[] materialNames,
            List<ModelSubmeshAsset> submeshes,
            ref int vertexOffset,
            ref int indexOffset) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            } else if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            } else if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            } else if (materialNames == null) {
                throw new ArgumentNullException(nameof(materialNames));
            } else if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }

            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported scene contains a null mesh.");
                }

                CopyMeshInstance(
                    scene,
                    mesh,
                    meshIndex,
                    positions,
                    normals,
                    texCoords,
                    indices16,
                    indices32,
                    materialNames,
                    submeshes,
                    ref vertexOffset,
                    ref indexOffset,
                    System.Numerics.Matrix4x4.Identity);
            }
        }

        /// <summary>
        /// Recursively copies all mesh instances referenced by one Assimp node hierarchy.
        /// </summary>
        /// <param name="node">Current node being processed.</param>
        /// <param name="scene">Imported scene that owns the node meshes.</param>
        /// <param name="positions">Destination position array.</param>
        /// <param name="normals">Destination normal array.</param>
        /// <param name="texCoords">Destination texture coordinate array.</param>
        /// <param name="indices16">Destination 16-bit index array when used.</param>
        /// <param name="indices32">Destination 32-bit index array when used.</param>
        /// <param name="materialNames">Resolved unique material names keyed by material index.</param>
        /// <param name="submeshes">Destination submesh list.</param>
        /// <param name="vertexOffset">Current destination vertex offset.</param>
        /// <param name="indexOffset">Current destination index offset.</param>
        /// <param name="parentTransform">Accumulated world transform for the current node.</param>
        void CopyNodeMeshes(
            Node node,
            Scene scene,
            float3[] positions,
            float3[] normals,
            float2[] texCoords,
            ushort[] indices16,
            uint[] indices32,
            string[] materialNames,
            List<ModelSubmeshAsset> submeshes,
            ref int vertexOffset,
            ref int indexOffset,
            System.Numerics.Matrix4x4 parentTransform) {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            } else if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            } else if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            } else if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            } else if (materialNames == null) {
                throw new ArgumentNullException(nameof(materialNames));
            } else if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }

            System.Numerics.Matrix4x4 nodeTransform = CombineTransforms(node.Transform, parentTransform);
            for (int meshReferenceIndex = 0; meshReferenceIndex < node.MeshCount; meshReferenceIndex++) {
                int meshIndex = node.MeshIndices[meshReferenceIndex];
                if (meshIndex < 0 || meshIndex >= scene.MeshCount) {
                    throw new InvalidOperationException("Imported node references a mesh index outside the scene.");
                }

                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported node references a null mesh.");
                }

                CopyMeshInstance(
                    scene,
                    mesh,
                    meshIndex,
                    positions,
                    normals,
                    texCoords,
                    indices16,
                    indices32,
                    materialNames,
                    submeshes,
                    ref vertexOffset,
                    ref indexOffset,
                    nodeTransform);
            }

            for (int childIndex = 0; childIndex < node.ChildCount; childIndex++) {
                CopyNodeMeshes(
                    node.Children[childIndex],
                    scene,
                    positions,
                    normals,
                    texCoords,
                    indices16,
                    indices32,
                    materialNames,
                    submeshes,
                    ref vertexOffset,
                    ref indexOffset,
                    nodeTransform);
            }
        }

        /// <summary>
        /// Copies one referenced mesh instance into the flattened model arrays.
        /// </summary>
        /// <param name="scene">Imported scene that owns the mesh.</param>
        /// <param name="mesh">Mesh to copy.</param>
        /// <param name="meshIndex">Zero-based mesh index used for fallback naming.</param>
        /// <param name="positions">Destination position array.</param>
        /// <param name="normals">Destination normal array.</param>
        /// <param name="texCoords">Destination texture coordinate array.</param>
        /// <param name="indices16">Destination 16-bit index array when used.</param>
        /// <param name="indices32">Destination 32-bit index array when used.</param>
        /// <param name="materialNames">Resolved unique material names keyed by material index.</param>
        /// <param name="submeshes">Destination submesh list.</param>
        /// <param name="vertexOffset">Current destination vertex offset.</param>
        /// <param name="indexOffset">Current destination index offset.</param>
        /// <param name="transform">World transform to apply to the mesh instance.</param>
        void CopyMeshInstance(
            Scene scene,
            Mesh mesh,
            int meshIndex,
            float3[] positions,
            float3[] normals,
            float2[] texCoords,
            ushort[] indices16,
            uint[] indices32,
            string[] materialNames,
            List<ModelSubmeshAsset> submeshes,
            ref int vertexOffset,
            ref int indexOffset,
            System.Numerics.Matrix4x4 transform) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (mesh == null) {
                throw new ArgumentNullException(nameof(mesh));
            } else if (positions == null) {
                throw new ArgumentNullException(nameof(positions));
            } else if (normals == null) {
                throw new ArgumentNullException(nameof(normals));
            } else if (texCoords == null) {
                throw new ArgumentNullException(nameof(texCoords));
            } else if (materialNames == null) {
                throw new ArgumentNullException(nameof(materialNames));
            } else if (submeshes == null) {
                throw new ArgumentNullException(nameof(submeshes));
            }

            CopyMeshVertices(mesh, positions, normals, texCoords, vertexOffset, transform);
            int submeshIndexStart = indexOffset;
            if (indices32 != null) {
                CopyMeshIndices32(mesh, indices32, vertexOffset, ref indexOffset);
            } else {
                CopyMeshIndices16(mesh, indices16, vertexOffset, ref indexOffset);
            }

            submeshes.Add(new ModelSubmeshAsset {
                MaterialSlotName = ResolveMaterialSlotName(scene, mesh, meshIndex, materialNames),
                IndexStart = submeshIndexStart,
                IndexCount = indexOffset - submeshIndexStart
            });

            vertexOffset += mesh.VertexCount;
        }

        /// <summary>
        /// Counts the mesh references contained in one node hierarchy.
        /// </summary>
        /// <param name="node">Root node of the hierarchy to inspect.</param>
        /// <returns>Total number of referenced mesh instances.</returns>
        int CountNodeMeshInstances(Node node) {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            }

            int totalMeshInstances = node.MeshCount;
            for (int childIndex = 0; childIndex < node.ChildCount; childIndex++) {
                totalMeshInstances += CountNodeMeshInstances(node.Children[childIndex]);
            }

            return totalMeshInstances;
        }

        /// <summary>
        /// Counts vertices across all mesh instances referenced by one node hierarchy.
        /// </summary>
        /// <param name="node">Root node of the hierarchy to inspect.</param>
        /// <param name="scene">Imported scene that owns the meshes.</param>
        /// <returns>Total vertex count across every referenced mesh instance.</returns>
        int CountNodeVertices(Node node, Scene scene) {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            } else if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            int totalVertices = 0;
            for (int meshReferenceIndex = 0; meshReferenceIndex < node.MeshCount; meshReferenceIndex++) {
                int meshIndex = node.MeshIndices[meshReferenceIndex];
                if (meshIndex < 0 || meshIndex >= scene.MeshCount) {
                    throw new InvalidOperationException("Imported node references a mesh index outside the scene.");
                }

                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported node references a null mesh.");
                }

                totalVertices += mesh.VertexCount;
            }

            for (int childIndex = 0; childIndex < node.ChildCount; childIndex++) {
                totalVertices += CountNodeVertices(node.Children[childIndex], scene);
            }

            return totalVertices;
        }

        /// <summary>
        /// Counts triangle indices across all mesh instances referenced by one node hierarchy.
        /// </summary>
        /// <param name="node">Root node of the hierarchy to inspect.</param>
        /// <param name="scene">Imported scene that owns the meshes.</param>
        /// <returns>Total index count across every referenced mesh instance.</returns>
        int CountNodeIndices(Node node, Scene scene) {
            if (node == null) {
                throw new ArgumentNullException(nameof(node));
            } else if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            }

            int totalIndices = 0;
            for (int meshReferenceIndex = 0; meshReferenceIndex < node.MeshCount; meshReferenceIndex++) {
                int meshIndex = node.MeshIndices[meshReferenceIndex];
                if (meshIndex < 0 || meshIndex >= scene.MeshCount) {
                    throw new InvalidOperationException("Imported node references a mesh index outside the scene.");
                }

                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported node references a null mesh.");
                }

                if (!mesh.HasFaces) {
                    throw new InvalidOperationException("Imported mesh does not contain faces.");
                }

                for (int faceIndex = 0; faceIndex < mesh.FaceCount; faceIndex++) {
                    Face face = mesh.Faces[faceIndex];
                    if (face.IndexCount != 3) {
                        throw new InvalidOperationException("Imported mesh contains a non-triangle face after triangulation.");
                    }

                    totalIndices += face.IndexCount;
                }
            }

            for (int childIndex = 0; childIndex < node.ChildCount; childIndex++) {
                totalIndices += CountNodeIndices(node.Children[childIndex], scene);
            }

            return totalIndices;
        }

        /// <summary>
        /// Resolves the stable material slot name associated with one imported mesh.
        /// </summary>
        /// <param name="scene">Imported scene that owns the mesh.</param>
        /// <param name="mesh">Mesh whose material slot should be resolved.</param>
        /// <param name="meshIndex">Zero-based mesh index used for fallback naming.</param>
        /// <param name="materialNames">Resolved unique material names keyed by material index.</param>
        /// <returns>Stable material slot name for the mesh.</returns>
        string ResolveMaterialSlotName(Scene scene, Mesh mesh, int meshIndex, string[] materialNames) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (mesh == null) {
                throw new ArgumentNullException(nameof(mesh));
            } else if (meshIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(meshIndex), "Mesh index must be non-negative.");
            } else if (materialNames == null) {
                throw new ArgumentNullException(nameof(materialNames));
            }

            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount) {
                return materialNames[mesh.MaterialIndex];
            }

            if (!string.IsNullOrWhiteSpace(mesh.Name)) {
                return mesh.Name;
            }

            return string.Concat("Material", meshIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Counts vertices across all meshes in an imported scene.
        /// </summary>
        /// <param name="scene">Scene to inspect.</param>
        /// <returns>Total vertex count.</returns>
        int CountVertices(Scene scene) {
            int totalVertices = 0;
            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported scene contains a null mesh.");
                }

                totalVertices += mesh.VertexCount;
            }

            return totalVertices;
        }

        /// <summary>
        /// Counts triangle indices across all meshes in an imported scene.
        /// </summary>
        /// <param name="scene">Scene to inspect.</param>
        /// <returns>Total index count.</returns>
        int CountIndices(Scene scene) {
            int totalIndices = 0;
            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                if (mesh == null) {
                    throw new InvalidOperationException("Imported scene contains a null mesh.");
                }

                if (!mesh.HasFaces) {
                    throw new InvalidOperationException("Imported mesh does not contain faces.");
                }

                for (int faceIndex = 0; faceIndex < mesh.FaceCount; faceIndex++) {
                    Face face = mesh.Faces[faceIndex];
                    if (face.IndexCount != 3) {
                        throw new InvalidOperationException("Imported mesh contains a non-triangle face after triangulation.");
                    }

                    totalIndices += face.IndexCount;
                }
            }

            return totalIndices;
        }

        /// <summary>
        /// Copies one Assimp mesh's vertex data into flattened engine arrays.
        /// </summary>
        /// <param name="mesh">Assimp mesh to copy.</param>
        /// <param name="positions">Destination position array.</param>
        /// <param name="normals">Destination normal array.</param>
        /// <param name="texCoords">Destination texture coordinate array.</param>
        /// <param name="vertexOffset">Start vertex offset for this mesh.</param>
        /// <param name="transform">World transform to apply to the copied mesh instance.</param>
        void CopyMeshVertices(Mesh mesh, float3[] positions, float3[] normals, float2[] texCoords, int vertexOffset, System.Numerics.Matrix4x4 transform) {
            if (mesh == null) {
                throw new ArgumentNullException(nameof(mesh));
            }
            if (!mesh.HasVertices) {
                throw new InvalidOperationException("Imported mesh does not contain positions.");
            }
            if (!mesh.HasNormals) {
                throw new InvalidOperationException("Imported mesh does not contain normals after post-processing.");
            }

            System.Numerics.Matrix4x4 normalTransform = ResolveNormalTransform(transform);
            bool hasTexCoords = mesh.HasTextureCoords(0);
            for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++) {
                System.Numerics.Vector3 position = System.Numerics.Vector3.Transform(mesh.Vertices[vertexIndex], transform);
                System.Numerics.Vector3 normal = System.Numerics.Vector3.TransformNormal(mesh.Normals[vertexIndex], normalTransform);
                float normalLength = normal.Length();
                if (normalLength > 0f) {
                    normal /= normalLength;
                }

                positions[vertexOffset + vertexIndex] = new float3(position.X, position.Y, position.Z);
                normals[vertexOffset + vertexIndex] = new float3(normal.X, normal.Y, normal.Z);

                if (hasTexCoords) {
                    System.Numerics.Vector3 texCoord = mesh.TextureCoordinateChannels[0][vertexIndex];
                    texCoords[vertexOffset + vertexIndex] = new float2(texCoord.X, texCoord.Y);
                } else {
                    texCoords[vertexOffset + vertexIndex] = new float2(0f, 0f);
                }
            }
        }

        /// <summary>
        /// Combines one local node transform with the accumulated parent transform.
        /// </summary>
        /// <param name="localTransform">Node-local transform.</param>
        /// <param name="parentTransform">Accumulated parent transform.</param>
        /// <returns>Combined world transform for the current node.</returns>
        System.Numerics.Matrix4x4 CombineTransforms(System.Numerics.Matrix4x4 localTransform, System.Numerics.Matrix4x4 parentTransform) {
            return System.Numerics.Matrix4x4.Multiply(localTransform, parentTransform);
        }

        /// <summary>
        /// Resolves the normal-space transform that should be applied to a mesh instance.
        /// </summary>
        /// <param name="transform">World transform applied to the mesh instance.</param>
        /// <returns>Inverse-transpose transform used for normals.</returns>
        System.Numerics.Matrix4x4 ResolveNormalTransform(System.Numerics.Matrix4x4 transform) {
            if (!System.Numerics.Matrix4x4.Invert(transform, out System.Numerics.Matrix4x4 inverseTransform)) {
                throw new InvalidOperationException("Imported node transform is not invertible.");
            }

            return System.Numerics.Matrix4x4.Transpose(inverseTransform);
        }

        /// <summary>
        /// Copies one Assimp mesh's triangle indices into a flattened 16-bit index buffer.
        /// </summary>
        /// <param name="mesh">Assimp mesh to copy.</param>
        /// <param name="indices">Destination index buffer.</param>
        /// <param name="vertexOffset">Vertex offset to apply to local mesh indices.</param>
        /// <param name="indexOffset">Current destination index offset.</param>
        void CopyMeshIndices16(Mesh mesh, ushort[] indices, int vertexOffset, ref int indexOffset) {
            for (int faceIndex = 0; faceIndex < mesh.FaceCount; faceIndex++) {
                Face face = mesh.Faces[faceIndex];
                if (face.IndexCount != 3) {
                    throw new InvalidOperationException("Imported mesh contains a non-triangle face after triangulation.");
                }

                for (int faceVertexIndex = 0; faceVertexIndex < face.IndexCount; faceVertexIndex++) {
                    int index = face.Indices[faceVertexIndex] + vertexOffset;
                    if (index > ushort.MaxValue) {
                        throw new InvalidOperationException($"Imported model index {index} exceeds the 16-bit index limit.");
                    }

                    indices[indexOffset] = (ushort)index;
                    indexOffset++;
                }
            }
        }

        /// <summary>
        /// Copies one Assimp mesh's triangle indices into a flattened 32-bit index buffer.
        /// </summary>
        /// <param name="mesh">Assimp mesh to copy.</param>
        /// <param name="indices">Destination index buffer.</param>
        /// <param name="vertexOffset">Vertex offset to apply to local mesh indices.</param>
        /// <param name="indexOffset">Current destination index offset.</param>
        void CopyMeshIndices32(Mesh mesh, uint[] indices, int vertexOffset, ref int indexOffset) {
            for (int faceIndex = 0; faceIndex < mesh.FaceCount; faceIndex++) {
                Face face = mesh.Faces[faceIndex];
                if (face.IndexCount != 3) {
                    throw new InvalidOperationException("Imported mesh contains a non-triangle face after triangulation.");
                }

                for (int faceVertexIndex = 0; faceVertexIndex < face.IndexCount; faceVertexIndex++) {
                    indices[indexOffset] = (uint)(face.Indices[faceVertexIndex] + vertexOffset);
                    indexOffset++;
                }
            }
        }
    }
}

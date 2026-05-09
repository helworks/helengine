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

            int totalVertices = CountVertices(scene);
            int totalIndices = CountIndices(scene);
            if (totalVertices == 0) {
                throw new InvalidOperationException("Imported model does not contain any vertices.");
            }

            string[] materialNames = AssimpMaterialNameCatalog.ResolveMaterialNames(scene);
            float3[] positions = new float3[totalVertices];
            float3[] normals = new float3[totalVertices];
            float2[] texCoords = new float2[totalVertices];
            List<ModelSubmeshAsset> submeshes = new List<ModelSubmeshAsset>(scene.MeshCount);
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

            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                CopyMeshVertices(mesh, positions, normals, texCoords, vertexOffset);
                int submeshIndexStart = indexOffset;
                if (uses32BitIndices) {
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
        void CopyMeshVertices(Mesh mesh, float3[] positions, float3[] normals, float2[] texCoords, int vertexOffset) {
            if (mesh == null) {
                throw new ArgumentNullException(nameof(mesh));
            }
            if (!mesh.HasVertices) {
                throw new InvalidOperationException("Imported mesh does not contain positions.");
            }
            if (!mesh.HasNormals) {
                throw new InvalidOperationException("Imported mesh does not contain normals after post-processing.");
            }

            bool hasTexCoords = mesh.HasTextureCoords(0);
            for (int vertexIndex = 0; vertexIndex < mesh.VertexCount; vertexIndex++) {
                System.Numerics.Vector3 position = mesh.Vertices[vertexIndex];
                System.Numerics.Vector3 normal = mesh.Normals[vertexIndex];
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

using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Imports model files through Assimp and converts them into the engine's single-buffer model asset format.
    /// </summary>
    public class HelengineAssimpImporter : IModelImporter {
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
        /// Imports a model asset from the given data stream.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Loaded model asset.</returns>
        public ModelAsset ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            string formatHint = ResolveFormatHint(stream);
            using AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFileFromStream(stream, ImportPostProcessSteps, formatHint);
            if (scene == null) {
                throw new InvalidOperationException("Assimp did not return a scene for the model stream.");
            }

            return ConvertScene(scene);
        }

        /// <summary>
        /// Converts an Assimp scene into one flattened engine model asset.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <returns>Flattened model asset.</returns>
        ModelAsset ConvertScene(Scene scene) {
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
            if (totalVertices > ushort.MaxValue + 1) {
                throw new InvalidOperationException($"Imported model has {totalVertices} vertices, which exceeds the 16-bit index limit.");
            }

            float3[] positions = new float3[totalVertices];
            float3[] normals = new float3[totalVertices];
            float2[] texCoords = new float2[totalVertices];
            ushort[] indices = new ushort[totalIndices];
            int vertexOffset = 0;
            int indexOffset = 0;

            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                Mesh mesh = scene.Meshes[meshIndex];
                CopyMeshVertices(mesh, positions, normals, texCoords, vertexOffset);
                CopyMeshIndices(mesh, indices, vertexOffset, ref indexOffset);
                vertexOffset += mesh.VertexCount;
            }

            return new ModelAsset {
                Positions = positions,
                Normals = normals,
                TexCoords = texCoords,
                Indices16 = indices
            };
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
        void CopyMeshIndices(Mesh mesh, ushort[] indices, int vertexOffset, ref int indexOffset) {
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
    }
}

using Assimp;

namespace helengine.editor.assimp {
    /// <summary>
    /// Converts an imported Assimp scene into neutral scene data without baking node transforms into vertices.
    /// </summary>
    public sealed class AssimpSceneDataReader {
        /// <summary>
        /// Assimp convention for animations that report no tick rate.
        /// </summary>
        const double DefaultTicksPerSecond = 25d;
        /// <summary>
        /// Assimp material key for the glTF base color factor.
        /// </summary>
        const string BaseColorKey = "$clr.base";
        /// <summary>
        /// Assimp material key for the glTF roughness factor.
        /// </summary>
        const string RoughnessKey = "$mat.roughnessFactor";
        /// <summary>
        /// Assimp material key for the glTF metallic factor.
        /// </summary>
        const string MetallicKey = "$mat.metallicFactor";

        /// <summary>
        /// Reads the whole scene: node hierarchy with local TRS, meshes, materials and animations.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <param name="sourceFileName">File name of the source the scene came from.</param>
        /// <returns>Neutral scene data.</returns>
        public ImportedSceneData Read(Scene scene, string sourceFileName) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (sourceFileName == null) {
                throw new ArgumentNullException(nameof(sourceFileName));
            } else if (scene.RootNode == null) {
                throw new InvalidOperationException("Imported scene has no root node.");
            }

            ImportedSceneMesh[] meshes = ReadMeshes(scene);
            List<ImportedSceneTexture> textures = new List<ImportedSceneTexture>();
            ImportedSceneMaterial[] materials = ReadMaterials(scene, textures);
            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> pathsBySourceName = new Dictionary<string, string>(StringComparer.Ordinal);
            ImportedSceneNode root = ReadNode(scene.RootNode, string.Empty, true, usedNames, pathsBySourceName);
            ImportedSceneAnimation[] animations = ReadAnimations(scene, pathsBySourceName);
            return new ImportedSceneData(root, meshes, materials, textures.ToArray(), animations, sourceFileName);
        }

        /// <summary>
        /// Reads one node and its subtree depth-first so repeated names are disambiguated in source order.
        /// Assimp animation channels carry only a node name, so when names repeat a channel targets the first node in depth-first order.
        /// </summary>
        /// <param name="node">Assimp node.</param>
        /// <param name="parentPath">Path of the parent node, empty for the root and its direct children.</param>
        /// <param name="isRoot">True for the scene root, whose path is empty.</param>
        /// <param name="usedNames">Scene-wide set of names already handed out.</param>
        /// <param name="pathsBySourceName">Receives the path of the first node seen for each source name, used to address animation channels.</param>
        /// <returns>Neutral node.</returns>
        ImportedSceneNode ReadNode(Node node, string parentPath, bool isRoot, HashSet<string> usedNames, Dictionary<string, string> pathsBySourceName) {
            string sourceName = node.Name ?? string.Empty;
            string name = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(sourceName), usedNames);
            string path = isRoot ? string.Empty : (parentPath.Length == 0 ? name : parentPath + "/" + name);
            if (!pathsBySourceName.ContainsKey(sourceName)) {
                pathsBySourceName.Add(sourceName, path);
            }

            // Assimp stores matrices row-major with translation in the last column; System.Numerics expects it in the last row.
            System.Numerics.Matrix4x4 local = System.Numerics.Matrix4x4.Transpose(node.Transform);
            System.Numerics.Vector3 scale;
            System.Numerics.Quaternion rotation;
            System.Numerics.Vector3 translation;
            AssimpTransformUtils.Decompose(local, out scale, out rotation, out translation);

            int[] meshIndices = new int[node.MeshCount];
            for (int index = 0; index < node.MeshCount; index++) {
                meshIndices[index] = node.MeshIndices[index];
            }

            ImportedSceneNode[] children = new ImportedSceneNode[node.ChildCount];
            for (int index = 0; index < node.ChildCount; index++) {
                children[index] = ReadNode(node.Children[index], path, false, usedNames, pathsBySourceName);
            }

            return new ImportedSceneNode(
                name,
                path,
                new float3(translation.X, translation.Y, translation.Z),
                new float4(rotation.X, rotation.Y, rotation.Z, rotation.W),
                new float3(scale.X, scale.Y, scale.Z),
                meshIndices,
                children);
        }

        /// <summary>
        /// Reads every scene mesh once, in scene order, so nodes can share them by index.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <returns>Neutral meshes.</returns>
        ImportedSceneMesh[] ReadMeshes(Scene scene) {
            ImportedSceneMesh[] meshes = new ImportedSceneMesh[scene.MeshCount];
            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int meshIndex = 0; meshIndex < scene.MeshCount; meshIndex++) {
                meshes[meshIndex] = ReadMesh(scene.Meshes[meshIndex], usedNames);
            }

            return meshes;
        }

        /// <summary>
        /// Reads one mesh in its own local space using the same UV convention as the single-model converter.
        /// </summary>
        /// <param name="mesh">Assimp mesh.</param>
        /// <param name="usedNames">Mesh names already handed out.</param>
        /// <returns>Neutral mesh.</returns>
        ImportedSceneMesh ReadMesh(Mesh mesh, HashSet<string> usedNames) {
            if (mesh == null) {
                throw new InvalidOperationException("Imported scene contains a null mesh.");
            } else if (!mesh.HasVertices) {
                throw new InvalidOperationException("Imported mesh does not contain positions.");
            } else if (!mesh.HasNormals) {
                throw new InvalidOperationException("Imported mesh does not contain normals after post-processing.");
            }

            int vertexCount = mesh.VertexCount;
            bool hasTexCoords = mesh.HasTextureCoords(0);
            bool hasTangents = mesh.HasTangentBasis;
            float3[] positions = new float3[vertexCount];
            float3[] normals = new float3[vertexCount];
            float2[] texCoords = new float2[vertexCount];
            float4[] tangents = new float4[hasTangents ? vertexCount : 0];
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++) {
                System.Numerics.Vector3 position = mesh.Vertices[vertexIndex];
                System.Numerics.Vector3 normal = mesh.Normals[vertexIndex];
                positions[vertexIndex] = new float3(position.X, position.Y, position.Z);
                normals[vertexIndex] = new float3(normal.X, normal.Y, normal.Z);
                if (hasTexCoords) {
                    System.Numerics.Vector3 texCoord = mesh.TextureCoordinateChannels[0][vertexIndex];
                    texCoords[vertexIndex] = new float2(texCoord.X, texCoord.Y);
                } else {
                    texCoords[vertexIndex] = new float2(0f, 0f);
                }
                if (hasTangents) {
                    System.Numerics.Vector3 tangent = mesh.Tangents[vertexIndex];
                    System.Numerics.Vector3 bitangent = mesh.BiTangents[vertexIndex];
                    float sign = System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(normal, tangent), bitangent) < 0f ? -1f : 1f;
                    tangents[vertexIndex] = new float4(tangent.X, tangent.Y, tangent.Z, sign);
                }
            }

            List<uint> indices = new List<uint>(mesh.FaceCount * 3);
            for (int faceIndex = 0; faceIndex < mesh.FaceCount; faceIndex++) {
                Face face = mesh.Faces[faceIndex];
                if (face.IndexCount != 3) {
                    throw new InvalidOperationException("Imported mesh contains a non-triangle face after triangulation.");
                }
                indices.Add((uint)face.Indices[0]);
                indices.Add((uint)face.Indices[1]);
                indices.Add((uint)face.Indices[2]);
            }

            string name = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(mesh.Name ?? string.Empty), usedNames);
            return new ImportedSceneMesh(name, positions, normals, texCoords, tangents, indices.ToArray(), mesh.MaterialIndex);
        }

        /// <summary>
        /// Reads every scene material, in scene order.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <param name="textures">Receives every texture image the materials use, in order of first use.</param>
        /// <returns>Neutral materials.</returns>
        ImportedSceneMaterial[] ReadMaterials(Scene scene, List<ImportedSceneTexture> textures) {
            string[] names = AssimpMaterialNameCatalog.ResolveMaterialNames(scene);
            Dictionary<string, int> textureIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> usedTextureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ImportedSceneMaterial[] materials = new ImportedSceneMaterial[scene.MaterialCount];
            for (int index = 0; index < scene.MaterialCount; index++) {
                materials[index] = ReadMaterial(scene, scene.Materials[index], names[index], textures, textureIndices, usedTextureNames);
            }

            return materials;
        }

        /// <summary>
        /// Reads the PBR factors and texture paths of one material.
        /// </summary>
        /// <param name="scene">Imported Assimp scene that owns the embedded textures.</param>
        /// <param name="material">Assimp material.</param>
        /// <param name="name">Resolved unique material name.</param>
        /// <param name="textures">Texture table being filled.</param>
        /// <param name="textureIndices">Table index per Assimp texture path, so a shared image is stored once.</param>
        /// <param name="usedTextureNames">Texture names already handed out.</param>
        /// <returns>Neutral material.</returns>
        ImportedSceneMaterial ReadMaterial(Scene scene, Material material, string name, List<ImportedSceneTexture> textures, Dictionary<string, int> textureIndices, HashSet<string> usedTextureNames) {
            float4 baseColor = new float4(1f, 1f, 1f, 1f);
            if (material.HasColorDiffuse) {
                System.Numerics.Vector4 color = material.ColorDiffuse;
                baseColor = new float4(color.X, color.Y, color.Z, color.W);
            }
            MaterialProperty baseColorProperty = material.GetProperty(BaseColorKey, TextureType.None, 0);
            if (baseColorProperty != null) {
                System.Numerics.Vector4 color = baseColorProperty.GetVector4Value();
                baseColor = new float4(color.X, color.Y, color.Z, color.W);
            }

            float roughness = ReadFloatFactor(material, RoughnessKey, 1f);
            float metallic = ReadFloatFactor(material, MetallicKey, 1f);
            string baseColorTexture = ReadTexturePath(material, TextureType.BaseColor);
            if (baseColorTexture.Length == 0) {
                baseColorTexture = ReadTexturePath(material, TextureType.Diffuse);
            }
            // Normal and roughness paths are kept as the raw source paths: nothing consumes them yet, so they are neither extracted nor validated.
            string normalTexture = ReadTexturePath(material, TextureType.Normals);
            string roughnessTexture = ReadTexturePath(material, TextureType.GltfMetallicRoughness);
            if (roughnessTexture.Length == 0) {
                roughnessTexture = ReadTexturePath(material, TextureType.Roughness);
            }

            return new ImportedSceneMaterial(
                name,
                baseColor,
                roughness,
                metallic,
                RegisterTexture(scene, name, baseColorTexture, "basecolor", textures, textureIndices, usedTextureNames),
                normalTexture,
                roughnessTexture);
        }

        /// <summary>
        /// Adds the image behind one Assimp texture path to the texture table, once, and returns the material slot value that refers to it.
        /// Assimp names an image embedded in the source "*N"; any other path is an external file relative to the source.
        /// </summary>
        /// <param name="scene">Imported Assimp scene that owns the embedded textures.</param>
        /// <param name="materialName">Name of the material that uses the texture, for errors and fallback names.</param>
        /// <param name="assimpPath">Texture path as Assimp reports it, empty when the slot is unused.</param>
        /// <param name="role">Slot role used to name an image that carries no name of its own.</param>
        /// <param name="textures">Texture table being filled.</param>
        /// <param name="textureIndices">Table index per Assimp texture path.</param>
        /// <param name="usedTextureNames">Texture names already handed out.</param>
        /// <returns>The slot value: empty for an unused slot, otherwise "*K" for table entry K.</returns>
        string RegisterTexture(
            Scene scene,
            string materialName,
            string assimpPath,
            string role,
            List<ImportedSceneTexture> textures,
            Dictionary<string, int> textureIndices,
            HashSet<string> usedTextureNames) {
            if (assimpPath.Length == 0) {
                return string.Empty;
            }

            int tableIndex;
            if (textureIndices.TryGetValue(assimpPath, out tableIndex)) {
                return ImportedSceneTextureReference.Format(tableIndex);
            }

            ImportedSceneTexture texture;
            if (assimpPath.StartsWith("*", StringComparison.Ordinal)) {
                texture = ReadEmbeddedTexture(scene, materialName, assimpPath, role, usedTextureNames);
            } else {
                string format = NormalizeFormat(Path.GetExtension(assimpPath));
                if (format.Length == 0) {
                    throw new NotSupportedException($"Material '{materialName}' uses the texture '{assimpPath}', which is not a PNG or JPEG image.");
                }
                texture = new ImportedSceneTexture(
                    ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(Path.GetFileNameWithoutExtension(assimpPath)), usedTextureNames),
                    format,
                    Array.Empty<byte>(),
                    assimpPath);
            }

            tableIndex = textures.Count;
            textures.Add(texture);
            textureIndices.Add(assimpPath, tableIndex);
            return ImportedSceneTextureReference.Format(tableIndex);
        }

        /// <summary>
        /// Reads the encoded image bytes of a texture embedded in the source file, for example inside a .glb.
        /// </summary>
        /// <param name="scene">Imported Assimp scene that owns the embedded textures.</param>
        /// <param name="materialName">Name of the material that uses the texture, for errors and fallback names.</param>
        /// <param name="assimpPath">Assimp path "*N" of the embedded texture.</param>
        /// <param name="role">Slot role used to name an image that carries no name of its own.</param>
        /// <param name="usedTextureNames">Texture names already handed out.</param>
        /// <returns>The embedded texture.</returns>
        ImportedSceneTexture ReadEmbeddedTexture(Scene scene, string materialName, string assimpPath, string role, HashSet<string> usedTextureNames) {
            int embeddedIndex;
            if (!int.TryParse(assimpPath.Substring(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out embeddedIndex)
                || embeddedIndex >= scene.TextureCount) {
                throw new InvalidOperationException($"Material '{materialName}' refers to the embedded texture '{assimpPath}', which the source does not contain.");
            }

            EmbeddedTexture embedded = scene.Textures[embeddedIndex];
            if (!embedded.IsCompressed) {
                throw new NotSupportedException($"Material '{materialName}' uses an uncompressed embedded texture ('{assimpPath}'); only embedded PNG and JPEG images are supported.");
            }

            byte[] bytes = embedded.CompressedData;
            string format = NormalizeFormat(embedded.CompressedFormatHint);
            if (format.Length == 0) {
                format = SniffFormat(bytes);
            }
            if (bytes == null || bytes.Length == 0 || format.Length == 0) {
                throw new NotSupportedException($"Material '{materialName}' uses an embedded texture ('{assimpPath}') that is not a PNG or JPEG image.");
            }

            string sourceName = Path.GetFileNameWithoutExtension(embedded.Filename ?? string.Empty);
            if (sourceName.Length == 0 || sourceName.StartsWith("*", StringComparison.Ordinal)) {
                sourceName = materialName + "_" + role;
            }

            return new ImportedSceneTexture(
                ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(sourceName), usedTextureNames),
                format,
                bytes,
                string.Empty);
        }

        /// <summary>
        /// Maps an image format hint or file extension to the lowercase format the importer writes.
        /// </summary>
        /// <param name="hint">Hint such as "png", ".JPG" or "jpeg"; may be null or empty.</param>
        /// <returns>"png" or "jpg", or an empty string when the hint is not a supported format.</returns>
        static string NormalizeFormat(string hint) {
            string lowered = (hint ?? string.Empty).TrimStart('.').ToLowerInvariant();
            if (lowered == "png") {
                return "png";
            } else if (lowered == "jpg" || lowered == "jpeg") {
                return "jpg";
            }

            return string.Empty;
        }

        /// <summary>
        /// Recognizes PNG and JPEG data by its signature, for embedded images whose format hint is missing.
        /// </summary>
        /// <param name="bytes">Encoded image bytes.</param>
        /// <returns>"png" or "jpg", or an empty string when the signature is neither.</returns>
        static string SniffFormat(byte[] bytes) {
            if (bytes != null && bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) {
                return "png";
            } else if (bytes != null && bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) {
                return "jpg";
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads one scalar material factor, using the glTF specification default when the source omits it.
        /// </summary>
        /// <param name="material">Assimp material.</param>
        /// <param name="key">Assimp material property key.</param>
        /// <param name="specificationDefault">Value the source format defines for an omitted factor.</param>
        /// <returns>Factor value.</returns>
        float ReadFloatFactor(Material material, string key, float specificationDefault) {
            MaterialProperty property = material.GetProperty(key, TextureType.None, 0);
            return property == null ? specificationDefault : property.GetFloatValue();
        }

        /// <summary>
        /// Reads the first texture path of the given type, normalized to forward slashes.
        /// </summary>
        /// <param name="material">Assimp material.</param>
        /// <param name="type">Texture slot type.</param>
        /// <returns>Texture path, or an empty string when the slot is unused.</returns>
        string ReadTexturePath(Material material, TextureType type) {
            if (material.GetMaterialTextureCount(type) > 0 && material.GetMaterialTexture(type, 0, out TextureSlot slot)) {
                return slot.FilePath.Replace('\\', '/');
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads every animation, converting key times from ticks to seconds.
        /// </summary>
        /// <param name="scene">Imported Assimp scene.</param>
        /// <param name="pathsBySourceName">Node paths keyed by Assimp node name.</param>
        /// <returns>Neutral animations.</returns>
        ImportedSceneAnimation[] ReadAnimations(Scene scene, Dictionary<string, string> pathsBySourceName) {
            ImportedSceneAnimation[] animations = new ImportedSceneAnimation[scene.AnimationCount];
            for (int index = 0; index < scene.AnimationCount; index++) {
                Animation animation = scene.Animations[index];
                double ticksPerSecond = animation.TicksPerSecond > 0d ? animation.TicksPerSecond : DefaultTicksPerSecond;
                ImportedSceneAnimationChannel[] channels = new ImportedSceneAnimationChannel[animation.NodeAnimationChannelCount];
                for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++) {
                    channels[channelIndex] = ReadChannel(animation.NodeAnimationChannels[channelIndex], ticksPerSecond, pathsBySourceName);
                }

                animations[index] = new ImportedSceneAnimation(animation.Name ?? string.Empty, animation.DurationInTicks / ticksPerSecond, channels);
            }

            return animations;
        }

        /// <summary>
        /// Reads one node channel and resolves the node it animates to a unique path.
        /// Assimp animation channels carry only a node name, so when names repeat the channel targets the first node in depth-first order.
        /// </summary>
        /// <param name="channel">Assimp channel.</param>
        /// <param name="ticksPerSecond">Tick rate of the owning animation.</param>
        /// <param name="pathsBySourceName">Node paths keyed by Assimp node name.</param>
        /// <returns>Neutral channel.</returns>
        ImportedSceneAnimationChannel ReadChannel(NodeAnimationChannel channel, double ticksPerSecond, Dictionary<string, string> pathsBySourceName) {
            string nodePath;
            if (!pathsBySourceName.TryGetValue(channel.NodeName ?? string.Empty, out nodePath)) {
                throw new InvalidOperationException("Animation channel targets node '" + channel.NodeName + "' which is not in the scene hierarchy.");
            }

            ImportedSceneVectorKey[] positionKeys = new ImportedSceneVectorKey[channel.PositionKeyCount];
            for (int index = 0; index < positionKeys.Length; index++) {
                VectorKey key = channel.PositionKeys[index];
                positionKeys[index] = new ImportedSceneVectorKey(key.Time / ticksPerSecond, new float3(key.Value.X, key.Value.Y, key.Value.Z));
            }

            ImportedSceneRotationKey[] rotationKeys = new ImportedSceneRotationKey[channel.RotationKeyCount];
            for (int index = 0; index < rotationKeys.Length; index++) {
                QuaternionKey key = channel.RotationKeys[index];
                rotationKeys[index] = new ImportedSceneRotationKey(key.Time / ticksPerSecond, new float4(key.Value.X, key.Value.Y, key.Value.Z, key.Value.W));
            }

            ImportedSceneVectorKey[] scaleKeys = new ImportedSceneVectorKey[channel.ScalingKeyCount];
            for (int index = 0; index < scaleKeys.Length; index++) {
                VectorKey key = channel.ScalingKeys[index];
                scaleKeys[index] = new ImportedSceneVectorKey(key.Time / ticksPerSecond, new float3(key.Value.X, key.Value.Y, key.Value.Z));
            }

            return new ImportedSceneAnimationChannel(nodePath, positionKeys, rotationKeys, scaleKeys);
        }
    }
}

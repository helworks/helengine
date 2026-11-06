using helengine.editor.assimp;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the Assimp scene reader preserves the glTF hierarchy, local transforms, shared meshes and animation timing.
    /// </summary>
    public class AssimpSceneDataReaderTests {
        /// <summary>
        /// Comparison precision for transform and key values.
        /// </summary>
        const int Precision = 4;

        /// <summary>
        /// Imports the hand-written hinged box fixture through the public scene importer entry point.
        /// </summary>
        /// <returns>Neutral scene data read from the fixture.</returns>
        static ImportedSceneData ReadFixture() {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "hinged_box.gltf");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            ISceneSourceImporter importer = new HelengineAssimpImporter();
            return importer.ImportScene(stream);
        }

        /// <summary>
        /// Finds a node by its unique path.
        /// </summary>
        /// <param name="node">Node to search from.</param>
        /// <param name="path">Unique node path.</param>
        /// <returns>Matching node, or null.</returns>
        static ImportedSceneNode FindByPath(ImportedSceneNode node, string path) {
            if (node.Path == path) {
                return node;
            }
            for (int index = 0; index < node.Children.Length; index++) {
                ImportedSceneNode found = FindByPath(node.Children[index], path);
                if (found != null) {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Collects every node path in the subtree.
        /// </summary>
        /// <param name="node">Subtree root.</param>
        /// <param name="paths">Receives the paths.</param>
        static void CollectPaths(ImportedSceneNode node, List<string> paths) {
            paths.Add(node.Path);
            for (int index = 0; index < node.Children.Length; index++) {
                CollectPaths(node.Children[index], paths);
            }
        }

        /// <summary>
        /// Ensures child local TRS equals the glTF values and mesh positions are not baked.
        /// </summary>
        [Fact]
        public void HierarchyAndLocalTransforms_arePreserved() {
            ImportedSceneData scene = ReadFixture();

            Assert.Equal("hinged_box.gltf", scene.SourceFileName);
            ImportedSceneNode lid = FindByPath(scene.RootNode, "Base/Lid");
            ImportedSceneNode flap = FindByPath(scene.RootNode, "Base/Lid/Flap");
            ImportedSceneNode baseNode = FindByPath(scene.RootNode, "Base");
            Assert.NotNull(baseNode);
            Assert.NotNull(lid);
            Assert.NotNull(flap);

            Assert.Equal(1d, (double)baseNode.LocalPosition.X, Precision);
            Assert.Equal(2d, (double)baseNode.LocalPosition.Y, Precision);
            Assert.Equal(3d, (double)baseNode.LocalPosition.Z, Precision);
            Assert.Equal(0d, (double)lid.LocalPosition.X, Precision);
            Assert.Equal(1d, (double)lid.LocalPosition.Y, Precision);
            Assert.Equal(0d, (double)lid.LocalPosition.Z, Precision);
            Assert.Equal(0.3826834d, (double)lid.LocalRotation.Y, Precision);
            Assert.Equal(0.9238795d, (double)lid.LocalRotation.W, Precision);
            Assert.Equal(2d, (double)lid.LocalScale.X, Precision);
            Assert.Equal(2d, (double)lid.LocalScale.Y, Precision);
            Assert.Equal(2d, (double)lid.LocalScale.Z, Precision);
            Assert.Equal(5d, (double)flap.LocalPosition.Z, Precision);

            // Positions stay in mesh-local space: the second vertex is (1, 0, 0), not offset by any node transform.
            ImportedSceneMesh mesh = scene.Meshes[baseNode.MeshIndices[0]];
            Assert.Equal(3, mesh.Positions.Length);
            Assert.Equal(1d, (double)mesh.Positions[1].X, Precision);
            Assert.Equal(0d, (double)mesh.Positions[1].Y, Precision);
            Assert.Equal(0d, (double)mesh.Positions[1].Z, Precision);
            Assert.Equal(3, mesh.Normals.Length);
            Assert.Equal(3, mesh.TexCoords.Length);
            Assert.Equal(3, mesh.Indices.Length);
        }

        /// <summary>
        /// Ensures nodes using the same glTF mesh reference one scene mesh entry.
        /// </summary>
        [Fact]
        public void SharedMesh_isReadOnce() {
            ImportedSceneData scene = ReadFixture();

            Assert.Single(scene.Meshes);
            // Assimp may append a default material, so the mesh material is resolved through the mesh index.
            ImportedSceneMaterial material = scene.Materials[scene.Meshes[0].MaterialIndex];
            Assert.Equal("Paint", material.Name);
            Assert.Equal(0.3d, (double)material.Metallic, Precision);
            Assert.Equal(0.6d, (double)material.Roughness, Precision);
            Assert.Equal(0.5d, (double)material.BaseColor.X, Precision);
            Assert.Equal(0.75d, (double)material.BaseColor.Z, Precision);
            Assert.Equal(string.Empty, material.BaseColorTexturePath);
            Assert.Equal(3, scene.Meshes[0].Tangents.Length);
            Assert.Equal(new[] { 0 }, FindByPath(scene.RootNode, "Base").MeshIndices);
            Assert.Equal(new[] { 0 }, FindByPath(scene.RootNode, "Base/Lid").MeshIndices);
            Assert.Equal(new[] { 0 }, FindByPath(scene.RootNode, "Base/Lid/Flap").MeshIndices);
        }

        /// <summary>
        /// Ensures animation channels address nodes by unique path and carry key times in seconds.
        /// </summary>
        [Fact]
        public void AnimationChannels_targetNodePathsInSeconds() {
            ImportedSceneData scene = ReadFixture();

            Assert.Single(scene.Animations);
            ImportedSceneAnimation animation = scene.Animations[0];
            Assert.Equal("Open", animation.Name);
            Assert.Equal(1d, animation.DurationSeconds, Precision);
            ImportedSceneAnimationChannel channel = Assert.Single(animation.Channels);
            Assert.Equal("Base/Lid", channel.NodePath);
            Assert.Equal(3, channel.RotationKeys.Length);
            Assert.Equal(0d, channel.RotationKeys[0].TimeSeconds, Precision);
            Assert.Equal(0.5d, channel.RotationKeys[1].TimeSeconds, Precision);
            Assert.Equal(1d, channel.RotationKeys[2].TimeSeconds, Precision);
            Assert.Equal(0.7071068d, (double)channel.RotationKeys[1].Value.Z, Precision);
            Assert.Equal(0.7071068d, (double)channel.RotationKeys[1].Value.W, Precision);
            Assert.Equal(1d, (double)channel.RotationKeys[2].Value.Z, Precision);
        }

        /// <summary>
        /// Ensures the two nodes named Flap become Flap and Flap_2 and every path is unique.
        /// </summary>
        [Fact]
        public void DuplicateAndUnsafeNames_areDisambiguated() {
            ImportedSceneData scene = ReadFixture();

            Assert.NotNull(FindByPath(scene.RootNode, "Base/Lid/Flap"));
            ImportedSceneNode second = FindByPath(scene.RootNode, "Flap_2");
            Assert.NotNull(second);
            Assert.Equal("Flap_2", second.Name);
            Assert.Equal(-4d, (double)second.LocalPosition.X, Precision);

            List<string> paths = new List<string>();
            CollectPaths(scene.RootNode, paths);
            Assert.Equal(paths.Count, new HashSet<string>(paths).Count);

            Assert.Equal("a_b_c_d", ImportedSceneNameUtils.MakeSafeName("a/b\\c:d"));
            Assert.Equal("Node", ImportedSceneNameUtils.MakeSafeName(string.Empty));
            HashSet<string> used = new HashSet<string>();
            Assert.Equal("X", ImportedSceneNameUtils.Disambiguate("X", used));
            Assert.Equal("X_2", ImportedSceneNameUtils.Disambiguate("X", used));
            Assert.Equal("X_3", ImportedSceneNameUtils.Disambiguate("X", used));
        }

        /// <summary>
        /// Reads the zero-scale fixture, which hides nodes with collapsed scale axes.
        /// </summary>
        /// <returns>Neutral scene data read from the fixture.</returns>
        static ImportedSceneData ReadZeroScaleFixture() {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "zero_scale.gltf");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new HelengineAssimpImporter().ImportScene(stream);
        }

        /// <summary>
        /// Ensures texture coordinates keep the source's top-left origin, which is how the engine samples textures (their first
        /// row is the image top). Assimp flips V to a bottom-left origin on read; the reader must undo it, or printed art
        /// shows upside down and mirrored. The fixture triangle's glTF UVs equal its XY positions.
        /// </summary>
        [Fact]
        public void TexCoords_keepTheSourceTopLeftOrigin() {
            ImportedSceneMesh mesh = ReadZeroScaleFixture().Meshes[0];

            Assert.Equal(3, mesh.TexCoords.Length);
            for (int index = 0; index < mesh.TexCoords.Length; index++) {
                Assert.Equal((double)mesh.Positions[index].X, (double)mesh.TexCoords[index].X, Precision);
                Assert.Equal((double)mesh.Positions[index].Y, (double)mesh.TexCoords[index].Y, Precision);
            }
        }

        /// <summary>
        /// Ensures a node with scale (0,0,0) and a node with scale (1,0,1) import instead of failing decomposition.
        /// </summary>
        [Fact]
        public void ZeroScaleNodes_areReadWithRecoveredTransforms() {
            ImportedSceneData scene = ReadZeroScaleFixture();

            ImportedSceneNode hidden = FindByPath(scene.RootNode, "a_b");
            Assert.NotNull(hidden);
            Assert.Equal(1d, (double)hidden.LocalPosition.X, Precision);
            Assert.Equal(3d, (double)hidden.LocalPosition.Z, Precision);
            Assert.Equal(0d, (double)hidden.LocalScale.X, Precision);
            Assert.Equal(0d, (double)hidden.LocalScale.Y, Precision);
            Assert.Equal(0d, (double)hidden.LocalScale.Z, Precision);
            Assert.Equal(1d, (double)hidden.LocalRotation.W, Precision);

            ImportedSceneNode squash = FindByPath(scene.RootNode, "Squash");
            Assert.NotNull(squash);
            Assert.Equal(4d, (double)squash.LocalPosition.X, Precision);
            Assert.Equal(1d, (double)squash.LocalScale.X, Precision);
            Assert.Equal(0d, (double)squash.LocalScale.Y, Precision);
            Assert.Equal(1d, (double)squash.LocalScale.Z, Precision);
            Assert.Equal(0d, (double)squash.LocalRotation.X, Precision);
            Assert.Equal(0.7071068d, (double)squash.LocalRotation.Y, Precision);
            Assert.Equal(0d, (double)squash.LocalRotation.Z, Precision);
            Assert.Equal(0.7071068d, (double)squash.LocalRotation.W, Precision);
        }

        /// <summary>
        /// Ensures an unsafe source node name such as a/b is sanitized to a_b in the node name and path.
        /// </summary>
        [Fact]
        public void UnsafeNodeName_isSanitizedInNameAndPath() {
            ImportedSceneData scene = ReadZeroScaleFixture();

            ImportedSceneNode node = FindByPath(scene.RootNode, "a_b");
            Assert.NotNull(node);
            Assert.Equal("a_b", node.Name);
        }

        /// <summary>
        /// Ensures the reader lists an image embedded in a glb in the texture table and points the material slot at it.
        /// </summary>
        [Fact]
        public void EmbeddedImage_isListedInTextureTable() {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "textured_quad.glb");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            ImportedSceneData scene = new HelengineAssimpImporter().ImportScene(stream);

            ImportedSceneTexture texture = Assert.Single(scene.Textures);
            Assert.True(texture.IsEmbedded);
            Assert.Equal("png", texture.FormatHint);
            Assert.Equal("quad_art", texture.Name);
            Assert.Equal(0x89, texture.EncodedBytes[0]);
            Assert.Equal("*0", scene.Materials[scene.Meshes[0].MaterialIndex].BaseColorTexturePath);
        }

        /// <summary>
        /// Ensures an external image path stays a relative path in the texture table.
        /// </summary>
        [Fact]
        public void ExternalImage_keepsItsRelativePath() {
            string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "scene", "textured_quad.gltf");
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            ImportedSceneData scene = new HelengineAssimpImporter().ImportScene(stream);

            ImportedSceneTexture texture = Assert.Single(scene.Textures);
            Assert.False(texture.IsEmbedded);
            Assert.Equal("textured_quad.png", texture.ExternalRelativePath);
            Assert.Equal("png", texture.FormatHint);
        }
    }
}

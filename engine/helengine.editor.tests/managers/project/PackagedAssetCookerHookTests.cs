using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies plugin packaged-asset cookers are consulted by the scene packager and registered once per extension.
    /// </summary>
    public sealed class PackagedAssetCookerHookTests : IDisposable {
        /// <summary>
        /// Scene id written by every test.
        /// </summary>
        const string SceneId = "Scenes/CookHook.helen";

        /// <summary>
        /// Project-relative path of the authored asset referenced by the scene.
        /// </summary>
        const string AuthoredRelativePath = "clips/a.hfake";

        /// <summary>
        /// Temporary project workspace root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary build output root.
        /// </summary>
        readonly string BuildRootPath;

        /// <summary>
        /// Session-owned shader library required by the packager.
        /// </summary>
        readonly EditorBuiltInShaderAssetLibrary BuiltInShaderAssetLibrary;

        /// <summary>
        /// Creates the temporary workspace.
        /// </summary>
        public PackagedAssetCookerHookTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-cook-hook-tests", Guid.NewGuid().ToString("N"));
            BuildRootPath = Path.Combine(ProjectRootPath, "Build");
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "clips"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "cache", "shader-cache"));
            Directory.CreateDirectory(BuildRootPath);
            BuiltInShaderAssetLibrary = TestGeneratedAssetGraph.CreateShaderLibrary();
        }

        /// <summary>
        /// Deletes the temporary workspace.
        /// </summary>
        public void Dispose() {
            BuiltInShaderAssetLibrary.Dispose();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// A registered cooker handles its extension instead of the raw copy, and its returned reference is kept.
        /// </summary>
        [Fact]
        public void RegisteredCooker_handlesItsExtensionBeforeRawCopy() {
            FakePackagedAssetCooker cooker = new FakePackagedAssetCooker(".hfake");
            WriteAuthoredAsset();
            WriteScene();
            using EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath, new IAssetImporterRegistration[] { new FakePackagedAssetCookerRegistration(cooker) }, "windows", BuiltInShaderAssetLibrary);

            packager.Package(new[] { SceneId }, BuildRootPath);

            SceneAssetReference received = Assert.Single(cooker.ReceivedReferences);
            Assert.Equal(AuthoredRelativePath, received.RelativePath);
            PackagedAssetCookContext context = Assert.Single(cooker.ReceivedContexts);
            Assert.Equal(Path.GetFullPath(Path.Combine(ProjectRootPath, "assets", "clips", "a.hfake")), context.ProjectAssetFullPath);
            Assert.Equal(Path.GetFullPath(BuildRootPath), context.BuildRootPath);
            Assert.Equal("windows", context.TargetPlatformId);
            Assert.Equal(string.Empty, context.EnvironmentId);
            Assert.True(File.Exists(Path.Combine(BuildRootPath, "cooked", "fake", "clip.hfakecooked")));
            Assert.False(File.Exists(Path.Combine(BuildRootPath, "clips", "a.hfake")));
            SceneAssetReference kept = Assert.Single(ReadPackagedScene().AssetReferences);
            Assert.Equal(FakePackagedAssetCooker.CookedRelativePath, kept.RelativePath.TrimStart('/'));
        }

        /// <summary>
        /// Registering a second cooker for the same extension, in any case, throws.
        /// </summary>
        [Fact]
        public void DuplicateExtension_throws() {
            AssetImporterRegistry registry = CreateRegistry();
            registry.RegisterPackagedAssetCooker(new FakePackagedAssetCooker(".hfake"));

            Assert.Throws<InvalidOperationException>(() => registry.RegisterPackagedAssetCooker(new FakePackagedAssetCooker(".HFAKE")));
        }

        /// <summary>
        /// Registration after the registry is frozen throws, and lookup is case-insensitive with null for unknown extensions.
        /// </summary>
        [Fact]
        public void FrozenRegistry_rejectsRegistration_andLookupIsCaseInsensitive() {
            AssetImporterRegistry registry = CreateRegistry();
            FakePackagedAssetCooker cooker = new FakePackagedAssetCooker(".hfake");
            registry.RegisterPackagedAssetCooker(cooker);
            registry.Freeze();

            Assert.Throws<InvalidOperationException>(() => registry.RegisterPackagedAssetCooker(new FakePackagedAssetCooker(".hother")));
            Assert.Same(cooker, registry.FindPackagedAssetCooker(".HFAKE"));
            Assert.Null(registry.FindPackagedAssetCooker(".hnone"));
        }

        /// <summary>
        /// With no cooker registered the file is copied raw, exactly as before.
        /// </summary>
        [Fact]
        public void UnknownExtension_keepsRawCopy() {
            WriteAuthoredAsset();
            WriteScene();
            using EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), "windows", BuiltInShaderAssetLibrary);

            packager.Package(new[] { SceneId }, BuildRootPath);

            Assert.Equal("raw", File.ReadAllText(Path.Combine(BuildRootPath, "clips", "a.hfake")));
            SceneAssetReference kept = Assert.Single(ReadPackagedScene().AssetReferences);
            Assert.Equal(AuthoredRelativePath, kept.RelativePath.TrimStart('/'));
        }

        /// <summary>
        /// Creates an unfrozen registry over a throwaway import manager.
        /// </summary>
        /// <returns>Empty registry.</returns>
        AssetImporterRegistry CreateRegistry() {
            return new AssetImporterRegistry(new AssetImportManager(ProjectRootPath, new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath))));
        }

        /// <summary>
        /// Writes the authored asset the scene references.
        /// </summary>
        void WriteAuthoredAsset() {
            File.WriteAllText(Path.Combine(ProjectRootPath, "assets", "clips", "a.hfake"), "raw");
        }

        /// <summary>
        /// Writes a scene with one empty root entity and one file-system asset reference.
        /// </summary>
        void WriteScene() {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(SceneId));
            SceneAsset scene = new SceneAsset {
                Id = SceneId,
                AuthoringAssetId = Convert.ToHexString(hash)[..32].ToLowerInvariant(),
                AssetReferences = new[] {
                    SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(AuthoredRelativePath)
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            string scenePath = Path.Combine(ProjectRootPath, "assets", SceneId.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, scene);
        }

        /// <summary>
        /// Reads the packaged scene written to the build root.
        /// </summary>
        /// <returns>Packaged scene asset.</returns>
        SceneAsset ReadPackagedScene() {
            string packagedPath = Path.Combine(BuildRootPath, PackagedScenePathResolver.BuildRelativePath(SceneId, 0).Replace('/', Path.DirectorySeparatorChar));
            using FileStream stream = File.OpenRead(packagedPath);
            return PackagedAssetBinarySerializer.DeserializeSceneAsset(stream);
        }
    }
}

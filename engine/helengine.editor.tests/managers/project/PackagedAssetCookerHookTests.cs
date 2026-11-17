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
            ProjectRootPath = Path.Combine(@"C:\temp", "helengine-cook-hook-tests", Guid.NewGuid().ToString("N"));
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
        /// Built-in UI cooking preserves bytes and includes the resulting runtime reference in the scene preload closure.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BuiltInUiCooker_preservesBinaryAndRecordsSceneDependency(bool inComponent) {
            const string authoredPath = "ui/library.heui";
            byte[] bytes = Convert.FromHexString("48454c4501014955010001000400000000000000");
            string sourcePath = Path.Combine(ProjectRootPath, "assets", "ui", "library.heui");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllBytes(sourcePath, bytes);
            WriteScene(authoredPath, inComponent);
            using EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(
                ProjectRootPath, Array.Empty<IAssetImporterRegistration>(), "windows", BuiltInShaderAssetLibrary);

            EditorPlatformBuildScenePackagerResult result = packager.Package(new[] { SceneId }, BuildRootPath);

            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(BuildRootPath, "cooked", "ui", "library.heui")));
            SceneAssetReference reference = Assert.Single(ReadPackagedScene().AssetReferences);
            Assert.Equal("cooked/ui/library.heui", reference.RelativePath.TrimStart('/'));
            Assert.Contains("cooked/ui/library.heui", result.SceneCookedDependencies[SceneId]);
            Assert.DoesNotContain("ui/library.heui", result.SceneCookedDependencies[SceneId]);
            if (inComponent) {
                byte[] payload = Assert.Single(Assert.Single(ReadPackagedScene().RootEntities).Components).Payload;
                Assert.Contains("cooked/ui/library.heui", System.Text.Encoding.UTF8.GetString(payload));
            }
            Assert.IsType<UiBinaryAssetCooker>(CreateRegistry().FindPackagedAssetCooker(".HEUI"));
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
        /// Ensures a cooker cannot write or reference a file outside the build root through a rooted path or a ".." escape.
        /// </summary>
        /// <param name="buildRelativePath">Offending path.</param>
        [Theory]
        [InlineData("/etc/x.hfake")]
        [InlineData("\\x.hfake")]
        [InlineData("C:\\x.hfake")]
        [InlineData("C:x.hfake")]
        [InlineData("\\\\server\\share\\x.hfake")]
        [InlineData("../x.hfake")]
        [InlineData("cooked/../../x.hfake")]
        [InlineData("cooked\\..\\..\\x.hfake")]
        public void CookContext_rejectsPathsOutsideTheBuildRoot(string buildRelativePath) {
            List<string> written = new List<string>();
            List<string> referenced = new List<string>();
            PackagedAssetCookContext context = CreateCookContext(written, referenced);

            Assert.Throws<ArgumentException>(() => context.WriteAsset(buildRelativePath, new ModelAsset()));
            Assert.Throws<ArgumentException>(() => context.WriteBytes(buildRelativePath, Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => context.CreatePackagedReference(buildRelativePath));
            Assert.Empty(written);
            Assert.Empty(referenced);
        }

        /// <summary>
        /// Ensures a relative path that stays under the build root is written beneath it and handed to the reference factory unchanged.
        /// </summary>
        [Fact]
        public void CookContext_acceptsPathsUnderTheBuildRoot() {
            List<string> written = new List<string>();
            List<string> referenced = new List<string>();
            PackagedAssetCookContext context = CreateCookContext(written, referenced);

            context.WriteAsset("cooked/clips/a.hfake", new ModelAsset());
            context.CreatePackagedReference("cooked/clips/a.hfake");

            Assert.Equal(Path.GetFullPath(Path.Combine(BuildRootPath, "cooked", "clips", "a.hfake")), Path.GetFullPath(Assert.Single(written)));
            Assert.Equal("cooked/clips/a.hfake", Assert.Single(referenced));
        }

        /// <summary>
        /// Creates a cook context whose callbacks record the paths they receive.
        /// </summary>
        /// <param name="written">Receives the absolute paths passed to the asset writer.</param>
        /// <param name="referenced">Receives the build-relative paths passed to the reference factory.</param>
        /// <returns>The context.</returns>
        PackagedAssetCookContext CreateCookContext(List<string> written, List<string> referenced) {
            return new PackagedAssetCookContext(
                Path.Combine(ProjectRootPath, "assets", "clips", "a.hfake"),
                BuildRootPath,
                "windows",
                string.Empty,
                (path, asset) => written.Add(path),
                path => {
                    referenced.Add(path);
                    return null;
                });
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
        /// <param name="assetRelativePath">Authored asset referenced by the scene.</param>
        void WriteScene(string assetRelativePath = AuthoredRelativePath, bool inComponent = false) {
            byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(SceneId));
            SceneAsset scene = new SceneAsset {
                Id = SceneId,
                AuthoringAssetId = Convert.ToHexString(hash)[..32].ToLowerInvariant(),
                AssetReferences = new[] {
                    SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(assetRelativePath)
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = inComponent ? new[] {
                            new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder()).SerializeComponent(
                                new CookedUiReferenceComponent {
                                    InterfaceAsset = SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(assetRelativePath),
                                    Interfaces = new[] { SceneAssetReferenceTestFactory.CreateFileSystemAnimationClip(assetRelativePath) }
                                }, 0, null)
                        } : Array.Empty<SceneComponentAssetRecord>(),
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

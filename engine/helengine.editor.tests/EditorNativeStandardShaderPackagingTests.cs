using System.Reflection;

namespace helengine.editor.tests {
    /// <summary>Verifies generated standard materials package shaders for their target rather than the authoring host.</summary>
    public sealed class EditorNativeStandardShaderPackagingTests {
        /// <summary>A seeded target cache must satisfy standard shader generation without loading a DirectX compiler.</summary>
        [Theory]
        [InlineData("linux", ShaderCompileTarget.Vulkan)]
        [InlineData("macos", ShaderCompileTarget.Metal)]
        [InlineData("ios", ShaderCompileTarget.Metal)]
        public void PackagesStandardShaderForNativeTarget(string platformId, ShaderCompileTarget target) {
            string hostPlatform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";
            string root = Path.Combine(OperatingSystem.IsWindows() ? @"C:\temp\helengine" : "/tmp/helengine", hostPlatform, "standard-shader-test");
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            string targetName = ShaderTargetNames.GetTargetName(target);
            try {
                using EditorBuiltInShaderAssetLibrary library = new EditorBuiltInShaderAssetLibrary(new ShaderBackendRegistry());
                library.RegisterCompiledAsset(target, "ForwardStandardShader.hlsl", new ShaderAsset {
                    Id = "ForwardStandardShader", Name = "ForwardStandardShader", TargetName = targetName,
                    Programs = [], Binaries = []
                });
                using EditorPlatformBuildScenePackager packager = new EditorPlatformBuildScenePackager(root, [], platformId, library);
                MethodInfo generate = typeof(EditorPlatformBuildScenePackager).GetMethod("EnsureGeneratedStandardMaterialAssets", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(generate);
                generate.Invoke(packager, [root]);
                string expectedName = "forwardstandardshader." + targetName + ".hasset";
                string path = Path.Combine(root, "cooked", "shaders", expectedName);
                using FileStream stream = File.OpenRead(path);
                ShaderAsset written = Assert.IsType<ShaderAsset>(AssetSerializer.Deserialize(stream));
                Assert.Equal(targetName, written.TargetName);
                Assert.Equal(expectedName, Path.GetFileName(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)))));
            } finally {
                Directory.Delete(root, true);
            }
        }
    }
}

using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>Verifies that imported material defaults follow the platform texture cook contract.</summary>
    public sealed class MaterialAssetCookPathSeedingTests {
        /// <summary>Checks filename and hash identities without depending on a built-in console id.</summary>
        [Theory]
        [InlineData("RUEDAS.JPG", PlatformAssetNamingPolicy.RuntimeAssetIdHex16, ".hetex", "cooked/imported/d49842942b14f4eb.hetex")]
        [InlineData("RUEDAS.JPG", PlatformAssetNamingPolicy.PreserveAssetId, ".htex", "cooked/imported/ruedas.jpg.htex")]
        [InlineData("52d7dd22e6125230cf5bb184738ef76c5324ef5256e9d7765df5eff2250548e4", PlatformAssetNamingPolicy.RuntimeAssetIdHex16, ".hetex", "cooked/imported/9728e000e6f723bd.hetex")]
        public void LoadOrCreateInMemory_UsesPublishedTextureNamingAndExtension(string textureId, PlatformAssetNamingPolicy namingPolicy, string extension, string expectedPath) {
            string root = Path.Combine(Path.GetTempPath(), "material-cook-path-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                PlatformMaterialSchemaDefinition schema = new PlatformMaterialSchemaDefinition("textured", "Textured", [], [
                    new PlatformMaterialFieldDefinition("texture-relative-path", "Texture", PlatformMaterialFieldKind.Text, string.Empty, false, [])
                ]);
                PlatformAssetCookCapabilityDefinition capability = new PlatformAssetCookCapabilityDefinition(
                    "texture", "runtime-texture", PlatformAssetCookOwnershipKind.BuilderOwned, "portable-texture", string.Empty,
                    null, extension, namingPolicy, null);
                PlatformDefinition definition = new PlatformDefinition("portable", "Portable", [], [], [], [schema], [], [], [], [], assetCookCapabilities: [capability]);
                EditorPlatformBuildSelectionModel selection = new EditorPlatformBuildSelectionModel(definition, "portable", "Portable", [], [], [], [schema], [], [], [], []);
                MaterialAssetSettingsService service = new MaterialAssetSettingsService(root);
                string path = Path.Combine(root, "material.hasset");
                ShaderMaterialAsset material = new ShaderMaterialAsset { Id = "material", DiffuseTextureAssetId = textureId };

                MaterialAssetImportSettings settings = service.LoadOrCreateInMemory(path, material, ["portable"], _ => selection);

                Assert.Equal(expectedPath,
                    settings.Processor.Platforms["portable"].FieldValues["texture-relative-path"]);
                Assert.False(File.Exists(path));
            } finally {
                Directory.Delete(root, true);
            }
        }
    }
}

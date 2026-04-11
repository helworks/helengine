using System.Collections.Generic;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies generated-asset provider registration and source-aware asset entries.
    /// </summary>
    public class GeneratedAssetProviderRegistryTests {
        /// <summary>
        /// Ensures generated entries keep provider metadata and resolve models from the registry.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenProviderIsRegistered_ReturnsGeneratedEntryMetadata() {
            GeneratedAssetProviderRegistry.ResetForTests();
            TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedDirectory("Engine", "Engine", "engine"),
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
                },
                new TestRuntimeModel());
            GeneratedAssetProviderRegistry.Register(provider);

            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            GeneratedAssetProviderRegistry.LoadEntries(string.Empty, entries);

            AssetBrowserEntry engineEntry = Assert.Single(entries);
            Assert.Equal("Engine", engineEntry.Name);
            Assert.Equal(AssetBrowserEntrySourceKind.Generated, engineEntry.SourceKind);
            Assert.Equal("engine", engineEntry.ProviderId);
            Assert.Equal(AssetEntryKind.Directory, engineEntry.EntryKind);
        }

        /// <summary>
        /// Ensures generated model picks resolve through the provider that owns the entry.
        /// </summary>
        [Fact]
        public void ResolveRuntimeModel_WhenGeneratedModelEntryIsPicked_UsesTheOwningProvider() {
            GeneratedAssetProviderRegistry.ResetForTests();
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube")
                },
                runtimeModel);
            GeneratedAssetProviderRegistry.Register(provider);

            RuntimeModel resolvedModel = GeneratedAssetProviderRegistry.ResolveRuntimeModel(
                AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube"));

            Assert.Same(runtimeModel, resolvedModel);
        }

        /// <summary>
        /// Ensures generated material picks resolve through the provider that owns the entry.
        /// </summary>
        [Fact]
        public void ResolveRuntimeMaterial_WhenGeneratedMaterialEntryIsPicked_UsesTheOwningProvider() {
            GeneratedAssetProviderRegistry.ResetForTests();
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            TestGeneratedAssetProvider provider = new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", "engine:material:standard")
                },
                new TestRuntimeModel(),
                runtimeMaterial);
            GeneratedAssetProviderRegistry.Register(provider);

            RuntimeMaterial resolvedMaterial = GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(
                AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", "engine:material:standard"));

            Assert.Same(runtimeMaterial, resolvedMaterial);
        }
    }
}

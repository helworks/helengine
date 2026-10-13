using helengine.baseplatform.Player;
using helengine.directx11;
using helengine.editor;

namespace helengine.player {
    /// <summary>
    /// DirectX 11 renderer for the Play player. It renders exactly like the desktop runtime except that scene materials are
    /// platform-cooked: each one is decoded by the platform player adapter and rebuilt as a standard desktop material.
    /// </summary>
    public sealed class PlayerRenderManager3D : DirectX11Renderer3D {
        /// <summary>
        /// Creates the player renderer.
        /// </summary>
        /// <param name="adapter">Initialized platform player adapter that decodes cooked materials.</param>
        /// <param name="shaders">Built-in shader library that provides the standard shader used for decoded materials.</param>
        public PlayerRenderManager3D(IPlatformPlayerAdapter adapter, EditorBuiltInShaderAssetLibrary shaders) {
            PlatformAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            MaterialFactory = new PlayerStandardMaterialFactory(this, shaders);
        }

        /// <summary>Gets the platform player adapter that decodes cooked materials.</summary>
        public IPlatformPlayerAdapter PlatformAdapter { get; }

        /// <summary>Gets the factory that turns decoded material descriptions into desktop runtime materials.</summary>
        public PlayerStandardMaterialFactory MaterialFactory { get; }

        /// <summary>
        /// Builds a runtime material from one platform-cooked material: the adapter decodes it, its diffuse texture (if any)
        /// is loaded through the content manager and uploaded, and the standard material factory builds the result.
        /// </summary>
        /// <param name="assetContentManager">Content manager whose stream source reads the cooked platform content.</param>
        /// <param name="materialAssetPath">Content-root-relative path of the cooked material.</param>
        /// <returns>Runtime material instance.</returns>
        [NativeOwnedReturn]
        public override RuntimeMaterial BuildMaterialFromRawAsset(
            ContentManager assetContentManager,
            string materialAssetPath) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            PlayerMaterialDescription description;
            using (Stream stream = assetContentManager.ContentStreamSource.OpenRead(materialAssetPath)) {
                description = PlatformAdapter.DecodeMaterial(stream, materialAssetPath);
            }
            if (description == null) {
                throw new InvalidOperationException($"Platform player adapter returned no material description for '{materialAssetPath}'.");
            }

            RuntimeTexture diffuseTexture = null;
            if (!string.IsNullOrEmpty(description.DiffuseTextureRelativePath)) {
                TextureAsset textureAsset = assetContentManager.Load<TextureAsset>(description.DiffuseTextureRelativePath, RuntimeContentProcessorIds.TextureAsset);
                try {
                    diffuseTexture = OwnerCore.RenderManager2D.BuildTextureFromRaw(textureAsset);
                } finally {
                    NativeOwnership.DisposeAndDelete(textureAsset);
                }
            }

            return MaterialFactory.Create(description, diffuseTexture);
        }
    }
}

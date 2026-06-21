using System.Security.Cryptography;
using System.Text;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Bakes authored text components into generated sprite textures using the editor's exact 2D preview capture path.
    /// </summary>
    public sealed class TextComponentSpriteBakeService : ITextComponentSpriteBakeService {
        /// <summary>
        /// Generated provider id reserved for the editor's built-in font asset.
        /// </summary>
        const string EditorGeneratedProviderId = "editor";

        /// <summary>
        /// Stable asset id used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontAssetId = "ui-font";

        /// <summary>
        /// Renderer used to allocate and draw the offscreen capture scene.
        /// </summary>
        readonly RenderManager3D RenderManager3D;

        /// <summary>
        /// Render-target reader used to read one captured preview target back into a raw texture asset.
        /// </summary>
        readonly IRenderTargetTextureAssetReader RenderTargetTextureAssetReader;

        /// <summary>
        /// Absolute project assets root used to resolve file-backed font references.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Project content manager used to load packaged font assets.
        /// </summary>
        readonly ContentManager ProjectContentManager;

        /// <summary>
        /// Asset import manager used to import source font files.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Default editor font asset used to resolve generated editor-font references.
        /// </summary>
        readonly FontAsset DefaultEditorFontAsset;

        /// <summary>
        /// Initializes one text-component sprite bake service.
        /// </summary>
        /// <param name="renderManager3D">Renderer used to allocate and draw the offscreen capture scene.</param>
        /// <param name="renderTargetTextureAssetReader">Render-target reader used to read captured preview targets back into raw texture assets.</param>
        /// <param name="assetsRootPath">Absolute project assets root used to resolve file-backed font references.</param>
        /// <param name="projectContentManager">Project content manager used to load packaged font assets.</param>
        /// <param name="assetImportManager">Asset import manager used to import source font files.</param>
        /// <param name="defaultEditorFontAsset">Default editor font asset used to resolve generated editor-font references.</param>
        public TextComponentSpriteBakeService(
            RenderManager3D renderManager3D,
            IRenderTargetTextureAssetReader renderTargetTextureAssetReader,
            string assetsRootPath,
            ContentManager projectContentManager,
            AssetImportManager assetImportManager,
            FontAsset defaultEditorFontAsset) {
            RenderManager3D = renderManager3D ?? throw new ArgumentNullException(nameof(renderManager3D));
            RenderTargetTextureAssetReader = renderTargetTextureAssetReader ?? throw new ArgumentNullException(nameof(renderTargetTextureAssetReader));
            AssetsRootPath = string.IsNullOrWhiteSpace(assetsRootPath)
                ? throw new ArgumentException("Assets root path must be provided.", nameof(assetsRootPath))
                : Path.GetFullPath(assetsRootPath);
            ProjectContentManager = projectContentManager ?? throw new ArgumentNullException(nameof(projectContentManager));
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            DefaultEditorFontAsset = defaultEditorFontAsset ?? throw new ArgumentNullException(nameof(defaultEditorFontAsset));
        }

        /// <summary>
        /// Bakes one authored text-component request into a generated sprite-texture result.
        /// </summary>
        /// <param name="request">Authored text inputs that should be rendered into a sprite texture.</param>
        /// <returns>Generated texture payload and metadata used by packaging.</returns>
        public TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            FontAsset fontAsset = ResolveFontAsset(request.FontReference);
            string stableKey = ComputeStableKey(request);
            string generatedTextureAssetId = string.Concat("generated:text-sprite:", stableKey);

            using EditorExact2DPreviewCaptureService captureService = new EditorExact2DPreviewCaptureService(RenderManager3D);
            Entity sourceEntity = new Entity();
            sourceEntity.InitComponents();
            sourceEntity.InitChildren();

            TextComponent sourceComponent = new TextComponent {
                Font = fontAsset,
                Text = request.Text,
                WrapText = request.WrapText,
                Size = request.Size,
                Color = request.Color,
                Rotation = request.Rotation,
                FontScale = request.FontScale,
                RenderOrder2D = request.RenderOrder2D,
                LayerMask = request.LayerMask,
                Alignment = request.Alignment
            };
            sourceEntity.AddComponent(sourceComponent);

            captureService.CaptureTextPreview(sourceEntity, sourceComponent, request.Size);
            RenderManager3D.Draw();

            TextureAsset textureAsset = RenderTargetTextureAssetReader.ReadTextureAsset(captureService.PreviewRenderTarget, generatedTextureAssetId);
            TextureAssetProcessorSettings processorSettings = ResolveProcessorSettings(request.TargetPlatformId);
            return new TextComponentSpriteBakeResult(textureAsset, processorSettings, stableKey);
        }

        /// <summary>
        /// Resolves the authored font reference used by the bake request into one editor-usable font asset.
        /// </summary>
        /// <param name="fontReference">Authored scene font reference to resolve.</param>
        /// <returns>Resolved font asset used for preview capture.</returns>
        FontAsset ResolveFontAsset(SceneAssetReference fontReference) {
            if (fontReference == null) {
                throw new ArgumentNullException(nameof(fontReference));
            }

            if (fontReference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                if (!string.Equals(fontReference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Unsupported generated font provider '{fontReference.ProviderId}'.");
                }
                if (string.Equals(fontReference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    return DefaultEditorFontAsset;
                }
                if (!string.Equals(fontReference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                    throw new InvalidOperationException($"Unsupported generated font asset id '{fontReference.AssetId}'.");
                }
            }
            if (fontReference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                string fullPath = ResolveFileSystemFontPath(fontReference.RelativePath);
                if (string.Equals(Path.GetExtension(fullPath), ".hefont", StringComparison.OrdinalIgnoreCase)) {
                    return ProjectContentManager.Load<FontAsset>(fullPath, RuntimeContentProcessorIds.FontAsset);
                }
                if (AssetImportManager.TryLoadFontAsset(fullPath, out FontAsset importedFontAsset) && importedFontAsset != null) {
                    return importedFontAsset;
                }

                throw new InvalidOperationException($"Font source '{fontReference.RelativePath}' could not be imported for text sprite baking.");
            }

            throw new InvalidOperationException($"Unsupported font reference source kind '{fontReference.SourceKind}'.");
        }

        /// <summary>
        /// Resolves one file-backed font reference into an absolute path under the project assets root.
        /// </summary>
        /// <param name="relativePath">Project-relative font path stored by the scene reference.</param>
        /// <returns>Absolute path to the referenced font asset.</returns>
        string ResolveFileSystemFontPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string normalizedAssetsRoot = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedAssetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("File-backed font references must stay inside the project assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Resolves the shared default processor settings for the generated texture.
        /// </summary>
        /// <param name="targetPlatformId">Target platform requesting the bake.</param>
        /// <returns>Processor settings that the generic texture cook pipeline should apply.</returns>
        static TextureAssetProcessorSettings ResolveProcessorSettings(string targetPlatformId) {
            _ = targetPlatformId;
            return new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = string.Empty
            };
        }

        /// <summary>
        /// Computes one deterministic stable key for generated text-sprite outputs.
        /// </summary>
        /// <param name="request">Bake request whose authored values should contribute to the key.</param>
        /// <returns>Lowercase hexadecimal stable key.</returns>
        static string ComputeStableKey(TextComponentSpriteBakeRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(request.ComponentIndex);
            builder.Append('|');
            builder.Append(request.TargetPlatformId);
            builder.Append('|');
            builder.Append(request.FontReference.SourceKind);
            builder.Append('|');
            builder.Append(request.FontReference.ProviderId ?? string.Empty);
            builder.Append('|');
            builder.Append(request.FontReference.AssetId ?? string.Empty);
            builder.Append('|');
            builder.Append(request.FontReference.RelativePath ?? string.Empty);
            builder.Append('|');
            builder.Append(request.Text ?? string.Empty);
            builder.Append('|');
            builder.Append(request.Size.X);
            builder.Append('|');
            builder.Append(request.Size.Y);
            builder.Append('|');
            builder.Append(request.Color.X);
            builder.Append('|');
            builder.Append(request.Color.Y);
            builder.Append('|');
            builder.Append(request.Color.Z);
            builder.Append('|');
            builder.Append(request.Color.W);
            builder.Append('|');
            builder.Append(request.WrapText ? '1' : '0');
            builder.Append('|');
            builder.Append(request.FontScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append((int)request.Alignment);
            builder.Append('|');
            builder.Append(request.Rotation.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append('|');
            builder.Append(request.RenderOrder2D);
            builder.Append('|');
            builder.Append(request.LayerMask);

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

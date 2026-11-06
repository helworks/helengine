using helengine.animation.runtime;
using helengine.editor;

namespace helengine.animation {
    /// <summary>
    /// Cooks authored <c>.hanimclip</c> files while a scene is packaged: the clip is read, validated by the runtime writer and
    /// written into the build root at its packaged path with the build's numeric policy. The packaged path is
    /// <see cref="GetPackagedPath"/> of the authored path because <see cref="HierarchyAnimationPlayerComponent.ClipPath"/> is a
    /// plain string the packager does not rewrite; the importer stores that same path in the player.
    /// </summary>
    public sealed class HierarchyAnimationClipCooker : IPackagedAssetCooker {
        /// <summary>
        /// Build folder that platform builders ship as runtime content; packaged files outside it never reach the player.
        /// </summary>
        const string CookedFolder = "cooked/";

        /// <summary>
        /// Returns the build-relative path a clip is packaged at, which is also the path players load it from: the canonical
        /// form of the authored path beneath the cooked folder.
        /// </summary>
        /// <param name="authoredRelativePath">Assets-relative path of the authored clip.</param>
        /// <returns>Packaged clip path.</returns>
        public static string GetPackagedPath(string authoredRelativePath) {
            return CookedFolder + CanonicalPackagedAssetPath.Normalize(authoredRelativePath);
        }

        /// <summary>
        /// Gets the clip file extension.
        /// </summary>
        public string Extension => HierarchyAnimationClipAsset.FileExtension;

        /// <summary>
        /// Writes the clip into the build root and returns its packaged reference.
        /// </summary>
        /// <param name="reference">Authored file-system reference to the clip.</param>
        /// <param name="context">Packaging context of the current build.</param>
        /// <returns>Packaged reference to the cooked clip.</returns>
        public SceneAssetReference Cook(SceneAssetReference reference, PackagedAssetCookContext context) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            } else if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            Asset asset;
            using (FileStream stream = File.OpenRead(context.ProjectAssetFullPath)) {
                asset = global::helengine.files.AssetSerializer.Deserialize(stream);
            }

            HierarchyAnimationClipAsset clip = asset as HierarchyAnimationClipAsset;
            if (clip == null) {
                throw new InvalidOperationException($"'{reference.RelativePath}' does not contain a hierarchy animation clip.");
            }

            string packagedPath = GetPackagedPath(reference.RelativePath);
            context.WriteAsset(packagedPath, clip);
            SceneAssetReference packagedReference = context.CreatePackagedReference(packagedPath);
            if (!string.Equals(packagedReference.RelativePath, packagedPath, StringComparison.Ordinal)) {
                throw new InvalidOperationException(
                    $"Platform '{context.TargetPlatformId}' maps the clip '{packagedPath}' to the runtime path '{packagedReference.RelativePath}', but hierarchy animation players load clips by their canonical packaged path.");
            }

            return packagedReference;
        }
    }
}

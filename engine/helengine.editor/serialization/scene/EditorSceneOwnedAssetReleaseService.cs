namespace helengine.editor {
    /// <summary>
    /// Releases scene-owned runtime assets that were materialized by editor scene loading.
    /// </summary>
    public static class EditorSceneOwnedAssetReleaseService {
        /// <summary>
        /// Releases one scene-owned asset set through the active runtime ownership seams and flushes any deferred renderer releases.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned runtime assets to release.</param>
        public static void ReleaseOwnedAssets(RuntimeSceneOwnedAssetSet ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }
            if (Core.Instance == null || Core.Instance.RenderManager2D == null || Core.Instance.RenderManager3D == null) {
                throw new InvalidOperationException("Editor scene-owned asset release requires initialized 2D and 3D render managers.");
            }

            ReleaseOwnedFonts(ownedAssets.OwnedFonts);
            ReleaseOwnedTextures(ownedAssets.OwnedTextures);
            ReleaseOwnedModels(ownedAssets.OwnedModels);
            ReleaseOwnedMaterials(ownedAssets.OwnedMaterials);
            FlushReleasedAssets();
        }

        /// <summary>
        /// Releases the supplied scene-owned fonts.
        /// </summary>
        /// <param name="ownedFonts">Scene-owned fonts to release.</param>
        static void ReleaseOwnedFonts(IReadOnlyList<FontAsset> ownedFonts) {
            if (ownedFonts == null) {
                throw new ArgumentNullException(nameof(ownedFonts));
            }

            for (int index = 0; index < ownedFonts.Count; index++) {
                FontAsset ownedFont = ownedFonts[index];
                if (ownedFont == null || ownedFont.IsDisposed) {
                    continue;
                }

                Core.Instance.RenderManager2D.ReleaseFont(ownedFont);
            }
        }

        /// <summary>
        /// Releases the supplied scene-owned textures.
        /// </summary>
        /// <param name="ownedTextures">Scene-owned textures to release.</param>
        static void ReleaseOwnedTextures(IReadOnlyList<RuntimeTexture> ownedTextures) {
            if (ownedTextures == null) {
                throw new ArgumentNullException(nameof(ownedTextures));
            }

            for (int index = 0; index < ownedTextures.Count; index++) {
                RuntimeTexture ownedTexture = ownedTextures[index];
                if (ownedTexture == null || ownedTexture.IsDisposed) {
                    continue;
                }

                Core.Instance.RenderManager2D.ReleaseTexture(ownedTexture);
            }
        }

        /// <summary>
        /// Releases the supplied scene-owned models.
        /// </summary>
        /// <param name="ownedModels">Scene-owned models to release.</param>
        static void ReleaseOwnedModels(IReadOnlyList<RuntimeModel> ownedModels) {
            if (ownedModels == null) {
                throw new ArgumentNullException(nameof(ownedModels));
            }

            for (int index = 0; index < ownedModels.Count; index++) {
                RuntimeModel ownedModel = ownedModels[index];
                if (ownedModel == null) {
                    continue;
                }

                Core.Instance.RenderManager3D.ReleaseModel(ownedModel);
            }
        }

        /// <summary>
        /// Releases the supplied scene-owned materials.
        /// </summary>
        /// <param name="ownedMaterials">Scene-owned materials to release.</param>
        static void ReleaseOwnedMaterials(IReadOnlyList<RuntimeMaterial> ownedMaterials) {
            if (ownedMaterials == null) {
                throw new ArgumentNullException(nameof(ownedMaterials));
            }

            for (int index = 0; index < ownedMaterials.Count; index++) {
                RuntimeMaterial ownedMaterial = ownedMaterials[index];
                if (ownedMaterial == null) {
                    continue;
                }

                Core.Instance.RenderManager3D.ReleaseMaterial(ownedMaterial);
            }
        }

        /// <summary>
        /// Flushes any renderer-owned deferred releases now that the scene-owned asset set has been handed back to the render managers.
        /// </summary>
        static void FlushReleasedAssets() {
            Core.Instance.RenderManager2D.FlushReleasedTextures();
            Core.Instance.RenderManager3D.FlushReleasedAssets();
            Core.Instance.RenderManager2D.FlushReleasedTextures();
        }
    }
}

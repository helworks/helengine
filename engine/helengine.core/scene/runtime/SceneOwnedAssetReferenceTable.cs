namespace helengine {
    /// <summary>
    /// Tracks reference counts for one category of scene-owned runtime asset shared across multiple loaded scenes, and releases each asset through an injected callback once its last referencing scene unloads.
    /// </summary>
    /// <typeparam name="T">Reference type of the scene-owned asset tracked by this table.</typeparam>
    public sealed class SceneOwnedAssetReferenceTable<T> where T : class {
        /// <summary>
        /// Human-readable description of the tracked asset category, used in diagnostic exception messages.
        /// </summary>
        readonly string AssetDescription;

        /// <summary>
        /// Callback invoked to release one asset once no loaded scene still references it.
        /// </summary>
        readonly Action<T> ReleaseAsset;

        /// <summary>
        /// Reference counts for each currently tracked asset instance.
        /// </summary>
        readonly Dictionary<T, int> ReferenceCounts;

        /// <summary>
        /// Initializes one scene-owned asset reference table.
        /// </summary>
        /// <param name="assetDescription">Human-readable description of the tracked asset category, used in diagnostic exception messages.</param>
        /// <param name="releaseAsset">Callback invoked to release one asset once no loaded scene still references it.</param>
        public SceneOwnedAssetReferenceTable(string assetDescription, Action<T> releaseAsset) {
            if (string.IsNullOrWhiteSpace(assetDescription)) {
                throw new ArgumentException("Asset description is required.", nameof(assetDescription));
            } else if (releaseAsset == null) {
                throw new ArgumentNullException(nameof(releaseAsset));
            }

            AssetDescription = assetDescription;
            ReleaseAsset = releaseAsset;
            ReferenceCounts = new Dictionary<T, int>();
        }

        /// <summary>
        /// Gets the number of distinct assets currently tracked across all loaded scenes.
        /// </summary>
        public int Count => ReferenceCounts.Count;

        /// <summary>
        /// Registers one scene's owned assets against the active set, incrementing the reference count for each non-null entry.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned assets resolved during materialization.</param>
        public void Register(IReadOnlyList<T> ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            for (int assetIndex = 0; assetIndex < ownedAssets.Count; assetIndex++) {
                T ownedAsset = ownedAssets[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }

                if (ReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    ReferenceCounts[ownedAsset] = existingReferenceCount + 1;
                } else {
                    ReferenceCounts.Add(ownedAsset, 1);
                }
            }
        }

        /// <summary>
        /// Releases one scene's owned assets, decrementing the reference count for each non-null entry and invoking the release callback once an asset is no longer referenced by any loaded scene.
        /// </summary>
        /// <param name="ownedAssets">Scene-owned assets resolved during materialization.</param>
        public void Release(IReadOnlyList<T> ownedAssets) {
            if (ownedAssets == null) {
                throw new ArgumentNullException(nameof(ownedAssets));
            }

            for (int assetIndex = 0; assetIndex < ownedAssets.Count; assetIndex++) {
                T ownedAsset = ownedAssets[assetIndex];
                if (ownedAsset == null) {
                    continue;
                }
                if (!ReferenceCounts.TryGetValue(ownedAsset, out int existingReferenceCount)) {
                    throw new InvalidOperationException($"Scene-owned {AssetDescription} was not tracked before release.");
                }

                if (existingReferenceCount > 1) {
                    ReferenceCounts[ownedAsset] = existingReferenceCount - 1;
                    continue;
                }

                ReferenceCounts.Remove(ownedAsset);
                ReleaseAsset(ownedAsset);
            }
        }
    }
}

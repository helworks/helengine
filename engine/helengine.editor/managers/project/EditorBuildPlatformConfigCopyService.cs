namespace helengine.editor {
    /// <summary>
    /// Owns the copy and per-scene ordering mutations applied to persisted platform build configurations, keeping the build dialog
    /// free of document mutation logic so it only renders and wires input.
    /// </summary>
    static class EditorBuildPlatformConfigCopyService {
        /// <summary>
        /// Replaces the contents of one string list with the entries of another, keeping the destination list instance.
        /// </summary>
        /// <param name="sourceValues">Values to copy from.</param>
        /// <param name="destinationValues">List that receives the copied values.</param>
        public static void CopyStringValues(IReadOnlyList<string> sourceValues, List<string> destinationValues) {
            if (sourceValues == null) {
                throw new ArgumentNullException(nameof(sourceValues));
            }

            if (destinationValues == null) {
                throw new ArgumentNullException(nameof(destinationValues));
            }

            destinationValues.Clear();
            for (int index = 0; index < sourceValues.Count; index++) {
                destinationValues.Add(sourceValues[index]);
            }
        }

        /// <summary>
        /// Copies one platform's selected scene ids into another platform configuration.
        /// </summary>
        /// <param name="sourcePlatformConfig">Platform configuration supplying the selected scene ids.</param>
        /// <param name="destinationPlatformConfig">Platform configuration receiving the copied scene ids.</param>
        public static void CopySelectedSceneIds(EditorBuildPlatformConfigDocument sourcePlatformConfig, EditorBuildPlatformConfigDocument destinationPlatformConfig) {
            if (sourcePlatformConfig == null) {
                throw new ArgumentNullException(nameof(sourcePlatformConfig));
            }

            if (destinationPlatformConfig == null) {
                throw new ArgumentNullException(nameof(destinationPlatformConfig));
            }

            destinationPlatformConfig.SelectedSceneIds.Clear();
            for (int index = 0; index < sourcePlatformConfig.SelectedSceneIds.Count; index++) {
                destinationPlatformConfig.SelectedSceneIds.Add(sourcePlatformConfig.SelectedSceneIds[index]);
            }
        }

        /// <summary>
        /// Copies one platform's scene-order entries into another platform configuration.
        /// </summary>
        /// <param name="sourcePlatformConfig">Platform configuration supplying the saved order values.</param>
        /// <param name="destinationPlatformConfig">Platform configuration receiving the copied order values.</param>
        public static void CopySceneOrders(EditorBuildPlatformConfigDocument sourcePlatformConfig, EditorBuildPlatformConfigDocument destinationPlatformConfig) {
            if (sourcePlatformConfig == null) {
                throw new ArgumentNullException(nameof(sourcePlatformConfig));
            }

            if (destinationPlatformConfig == null) {
                throw new ArgumentNullException(nameof(destinationPlatformConfig));
            }

            destinationPlatformConfig.SceneOrders.Clear();
            for (int index = 0; index < sourcePlatformConfig.SceneOrders.Count; index++) {
                EditorBuildSceneOrderDocument sourceSceneOrder = sourcePlatformConfig.SceneOrders[index];
                destinationPlatformConfig.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                    SceneId = sourceSceneOrder.SceneId,
                    OrderNumber = sourceSceneOrder.OrderNumber
                });
            }
        }

        /// <summary>
        /// Keeps the persisted scene-order entries aligned with the current project scene list by dropping stale entries and appending new ones.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <param name="catalogSceneIds">Project-relative scene ids currently known to the project.</param>
        public static void EnsureSceneOrderEntries(EditorBuildPlatformConfigDocument platformConfig, IReadOnlyList<string> catalogSceneIds) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            if (catalogSceneIds == null) {
                throw new ArgumentNullException(nameof(catalogSceneIds));
            }

            if (platformConfig.SceneOrders == null) {
                platformConfig.SceneOrders = new List<EditorBuildSceneOrderDocument>();
            }

            for (int index = platformConfig.SceneOrders.Count - 1; index >= 0; index--) {
                EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                if (IndexOfSceneId(catalogSceneIds, sceneOrder.SceneId) < 0) {
                    platformConfig.SceneOrders.RemoveAt(index);
                }
            }

            for (int index = 0; index < catalogSceneIds.Count; index++) {
                string sceneId = catalogSceneIds[index];
                if (FindSceneOrder(platformConfig, sceneId) != null) {
                    continue;
                }

                platformConfig.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                    SceneId = sceneId,
                    OrderNumber = GetNextSceneOrderNumber(platformConfig)
                });
            }
        }

        /// <summary>
        /// Finds one persisted scene-order entry for the requested scene identifier.
        /// </summary>
        /// <param name="platformConfig">Platform configuration containing the saved ordering values.</param>
        /// <param name="sceneId">Project-relative scene identifier to find.</param>
        /// <returns>Matching scene-order entry, or null when none exists.</returns>
        public static EditorBuildSceneOrderDocument FindSceneOrder(EditorBuildPlatformConfigDocument platformConfig, string sceneId) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                EditorBuildSceneOrderDocument sceneOrder = platformConfig.SceneOrders[index];
                if (sceneOrder.SceneId == sceneId) {
                    return sceneOrder;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next available ordering number for a new scene entry.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <returns>1-based ordering number that comes after all currently saved order values.</returns>
        public static int GetNextSceneOrderNumber(EditorBuildPlatformConfigDocument platformConfig) {
            if (platformConfig == null) {
                throw new ArgumentNullException(nameof(platformConfig));
            }

            int nextOrderNumber = 1;
            for (int index = 0; index < platformConfig.SceneOrders.Count; index++) {
                int candidateOrderNumber = platformConfig.SceneOrders[index].OrderNumber;
                if (candidateOrderNumber >= nextOrderNumber) {
                    nextOrderNumber = candidateOrderNumber + 1;
                }
            }

            return nextOrderNumber;
        }

        /// <summary>
        /// Reads the persisted ordering number for one scene, falling back to the catalog order when no usable entry exists.
        /// </summary>
        /// <param name="platformConfig">Platform configuration containing the saved ordering values.</param>
        /// <param name="sceneId">Project-relative scene identifier whose order should be resolved.</param>
        /// <param name="catalogSceneIds">Project-relative scene ids currently known to the project, used for the catalog-order fallback.</param>
        /// <returns>1-based ordering number for the requested scene.</returns>
        public static int GetSceneOrderNumber(EditorBuildPlatformConfigDocument platformConfig, string sceneId, IReadOnlyList<string> catalogSceneIds) {
            if (catalogSceneIds == null) {
                throw new ArgumentNullException(nameof(catalogSceneIds));
            }

            EditorBuildSceneOrderDocument sceneOrder = FindSceneOrder(platformConfig, sceneId);
            if (sceneOrder != null && sceneOrder.OrderNumber > 0) {
                return sceneOrder.OrderNumber;
            }

            int sceneIndex = IndexOfSceneId(catalogSceneIds, sceneId);
            if (sceneIndex >= 0) {
                return sceneIndex + 1;
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Creates a new scene-id list sorted by the persisted per-scene order numbers, breaking ties with the project catalog order.
        /// </summary>
        /// <param name="platformConfig">Platform configuration that stores the saved ordering values.</param>
        /// <param name="sceneIdsToSort">Scene ids that should be ordered.</param>
        /// <param name="catalogSceneIds">Project-relative scene ids currently known to the project, used for tie-breaking and fallbacks.</param>
        /// <returns>New scene-id list ordered by the saved per-scene order numbers.</returns>
        public static List<string> CreateSceneIdsOrderedByOrderNumber(
            EditorBuildPlatformConfigDocument platformConfig,
            IReadOnlyList<string> sceneIdsToSort,
            IReadOnlyList<string> catalogSceneIds) {
            if (sceneIdsToSort == null) {
                throw new ArgumentNullException(nameof(sceneIdsToSort));
            }

            if (catalogSceneIds == null) {
                throw new ArgumentNullException(nameof(catalogSceneIds));
            }

            List<string> orderedSceneIds = new List<string>(sceneIdsToSort.Count);
            for (int index = 0; index < sceneIdsToSort.Count; index++) {
                orderedSceneIds.Add(sceneIdsToSort[index]);
            }

            orderedSceneIds.Sort((leftSceneId, rightSceneId) => {
                int leftOrderNumber = GetSceneOrderNumber(platformConfig, leftSceneId, catalogSceneIds);
                int rightOrderNumber = GetSceneOrderNumber(platformConfig, rightSceneId, catalogSceneIds);
                int orderComparison = leftOrderNumber.CompareTo(rightOrderNumber);
                if (orderComparison != 0) {
                    return orderComparison;
                }

                int leftSceneIndex = IndexOfSceneId(catalogSceneIds, leftSceneId);
                int rightSceneIndex = IndexOfSceneId(catalogSceneIds, rightSceneId);
                return leftSceneIndex.CompareTo(rightSceneIndex);
            });

            return orderedSceneIds;
        }

        /// <summary>
        /// Returns the position of one scene id inside the supplied catalog list.
        /// </summary>
        /// <param name="catalogSceneIds">Project-relative scene ids currently known to the project.</param>
        /// <param name="sceneId">Project-relative scene identifier to locate.</param>
        /// <returns>Zero-based position of the scene id, or -1 when the catalog does not contain it.</returns>
        static int IndexOfSceneId(IReadOnlyList<string> catalogSceneIds, string sceneId) {
            for (int index = 0; index < catalogSceneIds.Count; index++) {
                if (catalogSceneIds[index] == sceneId) {
                    return index;
                }
            }

            return -1;
        }
    }
}

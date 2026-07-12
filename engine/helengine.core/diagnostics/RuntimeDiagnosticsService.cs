namespace helengine {
    /// <summary>
    /// Coordinates runtime diagnostics capture and overlays shared scene state onto provider snapshots.
    /// </summary>
    public sealed class RuntimeDiagnosticsService {
        /// <summary>
        /// Estimated byte width used for reference slots inside long-lived runtime lists.
        /// </summary>
        const int ReferenceSlotSizeInBytes = 8;

        /// <summary>
        /// Provider used to capture platform-specific diagnostics snapshots.
        /// </summary>
        readonly IRuntimeDiagnosticsProvider RuntimeDiagnosticsProvider;

        /// <summary>
        /// Scene manager used to resolve the currently loaded scene ids.
        /// </summary>
        readonly SceneManager RuntimeSceneManager;

        /// <summary>
        /// Object manager used to expose live runtime collection sizes and capacities.
        /// </summary>
        readonly ObjectManager RuntimeObjectManager;

        /// <summary>
        /// Initializes one runtime diagnostics service.
        /// </summary>
        /// <param name="runtimeDiagnosticsProvider">Provider used to capture platform-specific diagnostics snapshots.</param>
        /// <param name="runtimeSceneManager">Scene manager used to resolve currently loaded scene ids.</param>
        /// <param name="runtimeObjectManager">Object manager used to expose live runtime collection sizes and capacities.</param>
        public RuntimeDiagnosticsService(
            IRuntimeDiagnosticsProvider runtimeDiagnosticsProvider,
            SceneManager runtimeSceneManager,
            ObjectManager runtimeObjectManager) {
            RuntimeDiagnosticsProvider = runtimeDiagnosticsProvider;
            RuntimeSceneManager = runtimeSceneManager;
            RuntimeObjectManager = runtimeObjectManager;
        }

        /// <summary>
        /// Captures one runtime diagnostics snapshot and overlays the currently loaded scene ids.
        /// </summary>
        /// <returns>Portable runtime diagnostics snapshot for the current frame.</returns>
        public RuntimeMemoryDiagnosticsSnapshot CaptureSnapshot() {
            RuntimeMemoryDiagnosticsSnapshot snapshot = RuntimeDiagnosticsProvider != null
                ? RuntimeDiagnosticsProvider.CaptureSnapshot()
                : new RuntimeMemoryDiagnosticsSnapshot();

            if (snapshot == null) {
                snapshot = new RuntimeMemoryDiagnosticsSnapshot();
            }

            List<string> trackedSceneIds = snapshot.TrackedSceneIds ?? new List<string>();
            trackedSceneIds.Clear();
            if (RuntimeSceneManager != null) {
                List<string> loadedSceneIds = RuntimeSceneManager.GetLoadedSceneIds();
                for (int index = 0; index < loadedSceneIds.Count; index++) {
                    trackedSceneIds.Add(loadedSceneIds[index]);
                }

                NativeOwnership.Delete(loadedSceneIds);
            }

            snapshot.TrackedSceneIds = trackedSceneIds;
            AppendEngineCollectionMetrics(snapshot);
            return snapshot;
        }

        /// <summary>
        /// Captures reusable scalar memory counters without materializing the full diagnostics snapshot graph when the provider supports that path.
        /// </summary>
        /// <param name="counters">Reusable counter container that should receive the latest values.</param>
        public void CaptureMemoryCounters(RuntimeMemoryCounters counters) {
            if (counters == null) {
                throw new ArgumentNullException(nameof(counters));
            }

            counters.Reset();
            if (RuntimeDiagnosticsProvider is IRuntimeMemoryCounterProvider memoryCounterProvider) {
                memoryCounterProvider.CaptureMemoryCounters(counters);
                return;
            }

            if (RuntimeDiagnosticsProvider == null) {
                return;
            }

            RuntimeMemoryDiagnosticsSnapshot snapshot = RuntimeDiagnosticsProvider.CaptureSnapshot();
            try {
                if (snapshot != null) {
                    counters.CopyFromSnapshot(snapshot);
                }
            } finally {
                NativeOwnership.DisposeAndDelete(snapshot);
            }
        }

        /// <summary>
        /// Appends shared engine collection diagnostics to the current runtime snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot that should receive engine collection metrics.</param>
        void AppendEngineCollectionMetrics(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (RuntimeObjectManager != null) {
                AppendMetric(snapshot, "object_manager_entities_count", (ulong)RuntimeObjectManager.Entities.Count);
                AppendMetric(snapshot, "object_manager_entities_capacity", (ulong)RuntimeObjectManager.EntityCapacity);
                AppendMetric(snapshot, "object_manager_entities_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.EntityCapacity));
                AppendMetric(snapshot, "object_manager_updateables_count", (ulong)RuntimeObjectManager.Updateables.Count);
                AppendMetric(snapshot, "object_manager_updateables_capacity", (ulong)RuntimeObjectManager.UpdateableCapacity);
                AppendMetric(snapshot, "object_manager_updateables_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.UpdateableCapacity));
                AppendMetric(snapshot, "object_manager_last_updateable_pass", (ulong)RuntimeObjectManager.LastUpdateableDiagnosticPass);
                AppendMetric(snapshot, "object_manager_last_updateable_index", RuntimeObjectManager.LastUpdateableDiagnosticIndex < 0 ? 0u : (ulong)RuntimeObjectManager.LastUpdateableDiagnosticIndex);
                AppendMetric(snapshot, "object_manager_last_updateable_type_hash", RuntimeObjectManager.LastUpdateableDiagnosticTypeHash);
                AppendMetric(snapshot, "object_manager_last_updateable_owner_scene_entity_id", RuntimeObjectManager.LastUpdateableDiagnosticOwnerSceneEntityId);
                AppendMetric(snapshot, "object_manager_pending_update_operations_count", (ulong)RuntimeObjectManager.PendingUpdateOperationCount);
                AppendMetric(snapshot, "object_manager_pending_update_operations_capacity", (ulong)RuntimeObjectManager.PendingUpdateOperationCapacity);
                AppendMetric(snapshot, "object_manager_pending_update_operations_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.PendingUpdateOperationCapacity));
                AppendMetric(snapshot, "object_manager_drawables_2d_count", (ulong)RuntimeObjectManager.Drawables2D.Count);
                AppendMetric(snapshot, "object_manager_drawables_2d_capacity", (ulong)RuntimeObjectManager.Drawable2DCapacity);
                AppendMetric(snapshot, "object_manager_drawables_2d_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.Drawable2DCapacity));
                AppendMetric(snapshot, "object_manager_drawables_3d_count", (ulong)RuntimeObjectManager.Drawables3D.Count);
                AppendMetric(snapshot, "object_manager_drawables_3d_capacity", (ulong)RuntimeObjectManager.Drawable3DCapacity);
                AppendMetric(snapshot, "object_manager_drawables_3d_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.Drawable3DCapacity));
                AppendMetric(snapshot, "object_manager_cameras_count", (ulong)RuntimeObjectManager.Cameras.Count);
                AppendMetric(snapshot, "object_manager_cameras_capacity", (ulong)RuntimeObjectManager.CameraCapacity);
                AppendMetric(snapshot, "object_manager_cameras_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.CameraCapacity));
                AppendMetric(snapshot, "object_manager_directional_lights_count", (ulong)RuntimeObjectManager.DirectionalLights.Count);
                AppendMetric(snapshot, "object_manager_ambient_lights_count", (ulong)RuntimeObjectManager.AmbientLights.Count);
                AppendMetric(snapshot, "object_manager_point_lights_count", (ulong)RuntimeObjectManager.PointLights.Count);
                AppendMetric(snapshot, "object_manager_spot_lights_count", (ulong)RuntimeObjectManager.SpotLights.Count);
                AppendMetric(snapshot, "object_manager_interactables_count", (ulong)RuntimeObjectManager.Interactables.Count);
                AppendMetric(snapshot, "object_manager_interactables_capacity", (ulong)RuntimeObjectManager.InteractableCapacity);
                AppendMetric(snapshot, "object_manager_interactables_estimated_bytes", EstimateReferenceListBytes(RuntimeObjectManager.InteractableCapacity));
                AppendEntityHierarchyMetrics(snapshot);
                AppendCameraQueueMetrics(snapshot);
            }

            if (RuntimeSceneManager != null) {
                AppendMetric(snapshot, "scene_manager_loaded_scenes_count", (ulong)RuntimeSceneManager.LoadedScenes.Count);
                AppendMetric(snapshot, "scene_manager_loaded_scenes_capacity", (ulong)RuntimeSceneManager.LoadedSceneRecordCapacity);
                AppendMetric(snapshot, "scene_manager_loaded_scenes_estimated_bytes", EstimateReferenceListBytes(RuntimeSceneManager.LoadedSceneRecordCapacity));
                AppendMetric(snapshot, "scene_manager_pending_operations_count", (ulong)RuntimeSceneManager.LastTracePendingOperationCount);
                AppendMetric(snapshot, "scene_manager_pending_operations_capacity", (ulong)RuntimeSceneManager.PendingOperationCapacity);
                AppendMetric(snapshot, "scene_manager_pending_operations_estimated_bytes", EstimateReferenceListBytes(RuntimeSceneManager.PendingOperationCapacity));
                AppendMetric(snapshot, "scene_manager_active_owned_textures_count", (ulong)RuntimeSceneManager.ActiveOwnedTextureReferenceCount);
                AppendMetric(snapshot, "scene_manager_active_owned_fonts_count", (ulong)RuntimeSceneManager.ActiveOwnedFontReferenceCount);
                AppendMetric(snapshot, "scene_manager_active_owned_models_count", (ulong)RuntimeSceneManager.ActiveOwnedModelReferenceCount);
                AppendMetric(snapshot, "scene_manager_active_owned_materials_count", (ulong)RuntimeSceneManager.ActiveOwnedMaterialReferenceCount);
            }
        }

        /// <summary>
        /// Appends aggregate live-entity component and child-list capacities to the snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot that should receive entity hierarchy metrics.</param>
        void AppendEntityHierarchyMetrics(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            int totalComponentCount = 0;
            int totalComponentCapacity = 0;
            int totalChildCount = 0;
            int totalChildCapacity = 0;

            for (int entityIndex = 0; entityIndex < RuntimeObjectManager.Entities.Count; entityIndex++) {
                Entity entity = RuntimeObjectManager.Entities[entityIndex];
                if (entity == null) {
                    continue;
                }

                if (entity.Components != null) {
                    totalComponentCount += entity.Components.Count;
                    totalComponentCapacity += entity.Components.Capacity;
                }

                if (entity.Children != null) {
                    totalChildCount += entity.Children.Count;
                    totalChildCapacity += entity.Children.Capacity;
                }
            }

            AppendMetric(snapshot, "entity_components_count_total", (ulong)totalComponentCount);
            AppendMetric(snapshot, "entity_components_capacity_total", (ulong)totalComponentCapacity);
            AppendMetric(snapshot, "entity_components_estimated_bytes_total", EstimateReferenceListBytes(totalComponentCapacity));
            AppendMetric(snapshot, "entity_children_count_total", (ulong)totalChildCount);
            AppendMetric(snapshot, "entity_children_capacity_total", (ulong)totalChildCapacity);
            AppendMetric(snapshot, "entity_children_estimated_bytes_total", EstimateReferenceListBytes(totalChildCapacity));
        }

        /// <summary>
        /// Appends aggregate per-camera render-queue capacities to the snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot that should receive camera queue metrics.</param>
        void AppendCameraQueueMetrics(RuntimeMemoryDiagnosticsSnapshot snapshot) {
            int totalRenderList2DCount = 0;
            int totalRenderList2DCapacity = 0;
            int totalRenderList3DCount = 0;
            int totalRenderList3DCapacity = 0;

            for (int cameraIndex = 0; cameraIndex < RuntimeObjectManager.Cameras.Count; cameraIndex++) {
                ICamera camera = RuntimeObjectManager.Cameras[cameraIndex];
                if (camera == null) {
                    continue;
                }

                IRenderQueue2D renderQueue2D = camera.RenderQueue2D;
                if (renderQueue2D != null) {
                    totalRenderList2DCount += renderQueue2D.Count;
                    totalRenderList2DCapacity += renderQueue2D.Capacity;
                }

                IRenderQueue3D renderQueue3D = camera.RenderQueue3D;
                if (renderQueue3D != null) {
                    totalRenderList3DCount += renderQueue3D.Count;
                    totalRenderList3DCapacity += renderQueue3D.Capacity;
                }
            }

            AppendMetric(snapshot, "camera_render_list_2d_count_total", (ulong)totalRenderList2DCount);
            AppendMetric(snapshot, "camera_render_list_2d_capacity_total", (ulong)totalRenderList2DCapacity);
            AppendMetric(snapshot, "camera_render_list_2d_estimated_bytes_total", EstimateReferenceListBytes(totalRenderList2DCapacity));
            AppendMetric(snapshot, "camera_render_list_3d_count_total", (ulong)totalRenderList3DCount);
            AppendMetric(snapshot, "camera_render_list_3d_capacity_total", (ulong)totalRenderList3DCapacity);
            AppendMetric(snapshot, "camera_render_list_3d_estimated_bytes_total", EstimateReferenceListBytes(totalRenderList3DCapacity));
        }

        /// <summary>
        /// Appends one numeric diagnostics metric to the snapshot.
        /// </summary>
        /// <param name="snapshot">Snapshot that should receive the metric.</param>
        /// <param name="name">Stable metric name.</param>
        /// <param name="value">Numeric metric value.</param>
        static void AppendMetric(RuntimeMemoryDiagnosticsSnapshot snapshot, string name, ulong value) {
            snapshot.DetailMetrics.Add(new RuntimeDiagnosticsMetric(name, value));
        }

        /// <summary>
        /// Estimates the byte size reserved by one list of reference slots.
        /// </summary>
        /// <param name="capacity">Reserved reference-slot capacity.</param>
        /// <returns>Estimated reserved bytes for the list backing array.</returns>
        static ulong EstimateReferenceListBytes(int capacity) {
            if (capacity <= 0) {
                return 0;
            }

            return (ulong)capacity * ReferenceSlotSizeInBytes;
        }
    }
}

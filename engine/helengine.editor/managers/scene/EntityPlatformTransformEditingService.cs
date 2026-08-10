namespace helengine.editor {
    /// <summary>
    /// Projects sparse per-platform entity transform overrides into the live editor entity while keeping the shared common transform available for restore and serialization.
    /// </summary>
    public sealed class EntityPlatformTransformEditingService {
        /// <summary>
        /// Stable platform id used by the shared common transform state.
        /// </summary>
        public const string CommonPlatformId = ComponentPlatformEditingService.CommonPlatformId;

        /// <summary>
        /// Activates one platform transform projection on the supplied entity.
        /// </summary>
        /// <param name="entity">Entity whose live transform should reflect the requested platform.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier whose transform should be projected into the live entity.</param>
        public void ActivatePlatform(Entity entity, EntitySaveComponent saveComponent, string platformId) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            string normalizedRequestedPlatformId = NormalizePlatformId(platformId);
            string normalizedActivePlatformId = NormalizePlatformId(saveComponent.ActiveTransformPlatformId);
            if (string.Equals(normalizedRequestedPlatformId, normalizedActivePlatformId, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            PersistActivePlatform(entity, saveComponent);
            if (!IsCommonPlatformId(normalizedActivePlatformId) && IsCommonPlatformId(normalizedRequestedPlatformId)) {
                RestoreCommonTransform(entity, saveComponent);
                ClearActiveProjection(saveComponent);
                return;
            }

            if (IsCommonPlatformId(normalizedRequestedPlatformId)) {
                ClearActiveProjection(saveComponent);
                return;
            }

            if (!saveComponent.HasCommonTransformSnapshot) {
                CaptureCommonTransform(entity, saveComponent);
            }

            ApplyPlatformTransform(entity, saveComponent, normalizedRequestedPlatformId);
            saveComponent.ActiveTransformPlatformId = normalizedRequestedPlatformId;
        }

        /// <summary>
        /// Activates a platform or nested environment transform projection using common-to-platform-to-environment inheritance.
        /// </summary>
        public void ActivateScope(Entity entity, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            EditorOverrideScope activeScope = new EditorOverrideScope(
                NormalizePlatformId(saveComponent.ActiveTransformPlatformId),
                saveComponent.ActiveTransformEnvironmentId);
            if (activeScope == scope) {
                return;
            }

            PersistActiveScope(entity, saveComponent);
            if (IsCommonPlatformId(scope.PlatformId)) {
                RestoreCommonTransform(entity, saveComponent);
                ClearActiveProjection(saveComponent);
                return;
            }

            if (!saveComponent.HasCommonTransformSnapshot) {
                CaptureCommonTransform(entity, saveComponent);
            }

            ApplyScopeTransform(entity, saveComponent, scope);
            saveComponent.ActiveTransformPlatformId = scope.PlatformId;
            saveComponent.ActiveTransformEnvironmentId = scope.EnvironmentId;
        }

        /// <summary>
        /// Persists the currently projected platform or environment transform payload.
        /// </summary>
        public void PersistActiveScope(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            string platformId = NormalizePlatformId(saveComponent.ActiveTransformPlatformId);
            if (IsCommonPlatformId(platformId) || !saveComponent.HasCommonTransformSnapshot) {
                return;
            }

            EditorOverrideScope scope = new EditorOverrideScope(platformId, saveComponent.ActiveTransformEnvironmentId);
            float3 parentPosition = saveComponent.CommonLocalPositionSnapshot;
            float3 parentScale = saveComponent.CommonLocalScaleSnapshot;
            float4 parentOrientation = saveComponent.CommonLocalOrientationSnapshot;
            if (!scope.IsPlatformOnly && saveComponent.TryGetTransformPlatformOverride(new EditorOverrideScope(platformId), out SceneEntityPlatformTransformOverrideAsset platformOverride)) {
                ApplyOverride(ref parentPosition, ref parentScale, ref parentOrientation, platformOverride);
            }

            SceneEntityPlatformTransformOverrideAsset overrideState = saveComponent.GetOrCreateTransformPlatformOverride(scope);
            overrideState.PlatformId = scope.PlatformId;
            overrideState.EnvironmentId = scope.EnvironmentId;
            overrideState.HasLocalPositionOverride = entity.LocalPosition != parentPosition;
            overrideState.LocalPosition = entity.LocalPosition;
            overrideState.HasLocalScaleOverride = entity.LocalScale != parentScale;
            overrideState.LocalScale = entity.LocalScale;
            overrideState.HasLocalOrientationOverride = !entity.LocalOrientation.Equals(parentOrientation);
            overrideState.LocalOrientation = entity.LocalOrientation;

            if (!overrideState.HasLocalPositionOverride
                && !overrideState.HasLocalScaleOverride
                && !overrideState.HasLocalOrientationOverride) {
                saveComponent.RemoveTransformPlatformOverride(scope);
            }
        }

        /// <summary>
        /// Clears one transform field from a platform or nested environment payload.
        /// </summary>
        public void ClearScopeOverride(Entity entity, EntitySaveComponent saveComponent, EditorOverrideScope scope, string fieldName) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }
            if (string.IsNullOrWhiteSpace(fieldName)) {
                throw new ArgumentException("Transform field name must be provided.", nameof(fieldName));
            }
            if (!saveComponent.TryGetTransformPlatformOverride(scope, out SceneEntityPlatformTransformOverrideAsset overrideState)) {
                return;
            }

            if (string.Equals(fieldName, "position", StringComparison.OrdinalIgnoreCase)) {
                overrideState.HasLocalPositionOverride = false;
                overrideState.LocalPosition = float3.Zero;
            } else if (string.Equals(fieldName, "rotation", StringComparison.OrdinalIgnoreCase)) {
                overrideState.HasLocalOrientationOverride = false;
                overrideState.LocalOrientation = float4.Identity;
            } else if (string.Equals(fieldName, "scale", StringComparison.OrdinalIgnoreCase)) {
                overrideState.HasLocalScaleOverride = false;
                overrideState.LocalScale = float3.Zero;
            }

            if (!overrideState.HasLocalPositionOverride
                && !overrideState.HasLocalScaleOverride
                && !overrideState.HasLocalOrientationOverride) {
                saveComponent.RemoveTransformPlatformOverride(scope);
            }

            EditorOverrideScope activeScope = new EditorOverrideScope(
                NormalizePlatformId(saveComponent.ActiveTransformPlatformId),
                saveComponent.ActiveTransformEnvironmentId);
            if (activeScope == scope) {
                ApplyScopeTransform(entity, saveComponent, scope);
            }
        }

        /// <summary>
        /// Persists the currently projected platform override from the live entity back into the hidden save metadata.
        /// </summary>
        /// <param name="entity">Entity whose live transform should be captured.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        public void PersistActivePlatform(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            string normalizedActivePlatformId = NormalizePlatformId(saveComponent.ActiveTransformPlatformId);
            if (IsCommonPlatformId(normalizedActivePlatformId) || !saveComponent.HasCommonTransformSnapshot) {
                return;
            }

            SceneEntityPlatformTransformOverrideAsset overrideState = saveComponent.GetOrCreateTransformPlatformOverride(normalizedActivePlatformId);
            overrideState.PlatformId = normalizedActivePlatformId;
            overrideState.HasLocalPositionOverride = entity.LocalPosition != saveComponent.CommonLocalPositionSnapshot;
            overrideState.LocalPosition = entity.LocalPosition;
            overrideState.HasLocalScaleOverride = entity.LocalScale != saveComponent.CommonLocalScaleSnapshot;
            overrideState.LocalScale = entity.LocalScale;
            overrideState.HasLocalOrientationOverride = !entity.LocalOrientation.Equals(saveComponent.CommonLocalOrientationSnapshot);
            overrideState.LocalOrientation = entity.LocalOrientation;
        }

        /// <summary>
        /// Restores the live entity transform back to its shared common state and clears the active projection marker.
        /// </summary>
        /// <param name="entity">Entity whose live transform should be restored.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        public void RestoreCommon(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            PersistActivePlatform(entity, saveComponent);
            RestoreCommonTransform(entity, saveComponent);
            ClearActiveProjection(saveComponent);
        }

        /// <summary>
        /// Persists the active platform or nested environment projection and restores common transform state.
        /// </summary>
        public void RestoreCommonScope(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            PersistActiveScope(entity, saveComponent);
            RestoreCommonTransform(entity, saveComponent);
            ClearActiveProjection(saveComponent);
        }

        /// <summary>
        /// Resolves the common local position that should be serialized for one entity even when a platform override is currently projected into the live editor scene.
        /// </summary>
        /// <param name="entity">Entity whose common local position should be returned.</param>
        /// <param name="saveComponent">Hidden save component that may hold the common snapshot.</param>
        /// <returns>Common local position that should be written into scene assets.</returns>
        public float3 ResolveSerializedLocalPosition(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (saveComponent.HasCommonTransformSnapshot && !IsCommonPlatformId(saveComponent.ActiveTransformPlatformId)) {
                return saveComponent.CommonLocalPositionSnapshot;
            }

            return entity.LocalPosition;
        }

        /// <summary>
        /// Resolves the common local scale that should be serialized for one entity even when a platform override is currently projected into the live editor scene.
        /// </summary>
        /// <param name="entity">Entity whose common local scale should be returned.</param>
        /// <param name="saveComponent">Hidden save component that may hold the common snapshot.</param>
        /// <returns>Common local scale that should be written into scene assets.</returns>
        public float3 ResolveSerializedLocalScale(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (saveComponent.HasCommonTransformSnapshot && !IsCommonPlatformId(saveComponent.ActiveTransformPlatformId)) {
                return saveComponent.CommonLocalScaleSnapshot;
            }

            return entity.LocalScale;
        }

        /// <summary>
        /// Resolves the common local orientation that should be serialized for one entity even when a platform override is currently projected into the live editor scene.
        /// </summary>
        /// <param name="entity">Entity whose common local orientation should be returned.</param>
        /// <param name="saveComponent">Hidden save component that may hold the common snapshot.</param>
        /// <returns>Common local orientation that should be written into scene assets.</returns>
        public float4 ResolveSerializedLocalOrientation(Entity entity, EntitySaveComponent saveComponent) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            }

            if (saveComponent.HasCommonTransformSnapshot && !IsCommonPlatformId(saveComponent.ActiveTransformPlatformId)) {
                return saveComponent.CommonLocalOrientationSnapshot;
            }

            return entity.LocalOrientation;
        }

        /// <summary>
        /// Returns whether the supplied platform currently overrides the entity's local position.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier to query.</param>
        /// <returns>True when the platform defines a local-position override.</returns>
        public bool IsPositionOverrideActive(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return saveComponent.TryGetTransformPlatformOverride(platformId, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalPositionOverride;
        }

        /// <summary>Returns whether a platform or nested environment explicitly overrides local position.</summary>
        public bool IsScopePositionOverrideActive(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            return saveComponent != null
                && saveComponent.TryGetTransformPlatformOverride(scope, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalPositionOverride;
        }

        /// <summary>
        /// Returns whether the supplied platform currently overrides the entity's local rotation.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier to query.</param>
        /// <returns>True when the platform defines a local-orientation override.</returns>
        public bool IsRotationOverrideActive(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return saveComponent.TryGetTransformPlatformOverride(platformId, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalOrientationOverride;
        }

        /// <summary>Returns whether a platform or nested environment explicitly overrides local rotation.</summary>
        public bool IsScopeRotationOverrideActive(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            return saveComponent != null
                && saveComponent.TryGetTransformPlatformOverride(scope, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalOrientationOverride;
        }

        /// <summary>
        /// Returns whether the supplied platform currently overrides the entity's local scale.
        /// </summary>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier to query.</param>
        /// <returns>True when the platform defines a local-scale override.</returns>
        public bool IsScaleOverrideActive(EntitySaveComponent saveComponent, string platformId) {
            if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return saveComponent.TryGetTransformPlatformOverride(platformId, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalScaleOverride;
        }

        /// <summary>Returns whether a platform or nested environment explicitly overrides local scale.</summary>
        public bool IsScopeScaleOverrideActive(EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            return saveComponent != null
                && saveComponent.TryGetTransformPlatformOverride(scope, out SceneEntityPlatformTransformOverrideAsset overrideState)
                && overrideState.HasLocalScaleOverride;
        }

        /// <summary>
        /// Clears the local-position override for the supplied platform and reapplies the active projection if needed.
        /// </summary>
        /// <param name="entity">Entity whose live transform may need to update.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier whose position override should be cleared.</param>
        public void ClearPositionOverride(Entity entity, EntitySaveComponent saveComponent, string platformId) {
            ClearTransformOverride(entity, saveComponent, platformId, TransformOverrideFieldKind.Position);
        }

        /// <summary>
        /// Clears the local-rotation override for the supplied platform and reapplies the active projection if needed.
        /// </summary>
        /// <param name="entity">Entity whose live transform may need to update.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier whose rotation override should be cleared.</param>
        public void ClearRotationOverride(Entity entity, EntitySaveComponent saveComponent, string platformId) {
            ClearTransformOverride(entity, saveComponent, platformId, TransformOverrideFieldKind.Rotation);
        }

        /// <summary>
        /// Clears the local-scale override for the supplied platform and reapplies the active projection if needed.
        /// </summary>
        /// <param name="entity">Entity whose live transform may need to update.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier whose scale override should be cleared.</param>
        public void ClearScaleOverride(Entity entity, EntitySaveComponent saveComponent, string platformId) {
            ClearTransformOverride(entity, saveComponent, platformId, TransformOverrideFieldKind.Scale);
        }

        /// <summary>
        /// Copies the entity's current common local transform into the hidden snapshot fields before a platform override projection becomes active.
        /// </summary>
        /// <param name="entity">Entity whose common local transform should be captured.</param>
        /// <param name="saveComponent">Hidden save component that stores the common snapshot.</param>
        void CaptureCommonTransform(Entity entity, EntitySaveComponent saveComponent) {
            saveComponent.CommonLocalPositionSnapshot = entity.LocalPosition;
            saveComponent.CommonLocalScaleSnapshot = entity.LocalScale;
            saveComponent.CommonLocalOrientationSnapshot = entity.LocalOrientation;
            saveComponent.HasCommonTransformSnapshot = true;
        }

        /// <summary>
        /// Applies the effective transform for one non-common platform to the live entity.
        /// </summary>
        /// <param name="entity">Entity whose live transform should be updated.</param>
        /// <param name="saveComponent">Hidden save component that stores the common snapshot and override metadata.</param>
        /// <param name="platformId">Non-common platform identifier whose effective transform should be applied.</param>
        void ApplyPlatformTransform(Entity entity, EntitySaveComponent saveComponent, string platformId) {
            ApplyScopeTransform(entity, saveComponent, new EditorOverrideScope(platformId));
        }

        /// <summary>
        /// Applies common, platform, and environment transform payloads in inheritance order.
        /// </summary>
        void ApplyScopeTransform(Entity entity, EntitySaveComponent saveComponent, EditorOverrideScope scope) {
            float3 localPosition = saveComponent.CommonLocalPositionSnapshot;
            float3 localScale = saveComponent.CommonLocalScaleSnapshot;
            float4 localOrientation = saveComponent.CommonLocalOrientationSnapshot;
            if (saveComponent.TryGetTransformPlatformOverride(new EditorOverrideScope(scope.PlatformId), out SceneEntityPlatformTransformOverrideAsset platformOverride)) {
                ApplyOverride(ref localPosition, ref localScale, ref localOrientation, platformOverride);
            }
            if (!scope.IsPlatformOnly && saveComponent.TryGetTransformPlatformOverride(scope, out SceneEntityPlatformTransformOverrideAsset environmentOverride)) {
                ApplyOverride(ref localPosition, ref localScale, ref localOrientation, environmentOverride);
            }

            entity.LocalPosition = localPosition;
            entity.LocalScale = localScale;
            entity.LocalOrientation = localOrientation;
        }

        /// <summary>
        /// Applies the explicitly authored fields from one sparse transform override.
        /// </summary>
        static void ApplyOverride(ref float3 position, ref float3 scale, ref float4 orientation, SceneEntityPlatformTransformOverrideAsset overrideState) {
            if (overrideState.HasLocalPositionOverride) {
                position = overrideState.LocalPosition;
            }
            if (overrideState.HasLocalScaleOverride) {
                scale = overrideState.LocalScale;
            }
            if (overrideState.HasLocalOrientationOverride) {
                orientation = overrideState.LocalOrientation;
            }
        }

        /// <summary>
        /// Restores the previously captured common local transform to the live entity when leaving one platform override projection.
        /// </summary>
        /// <param name="entity">Entity whose live transform should return to the common state.</param>
        /// <param name="saveComponent">Hidden save component that stores the common snapshot.</param>
        void RestoreCommonTransform(Entity entity, EntitySaveComponent saveComponent) {
            if (!saveComponent.HasCommonTransformSnapshot) {
                return;
            }

            entity.LocalPosition = saveComponent.CommonLocalPositionSnapshot;
            entity.LocalScale = saveComponent.CommonLocalScaleSnapshot;
            entity.LocalOrientation = saveComponent.CommonLocalOrientationSnapshot;
        }

        /// <summary>
        /// Clears the active projection metadata after the live entity returns to the shared common transform.
        /// </summary>
        /// <param name="saveComponent">Hidden save component whose active projection metadata should be cleared.</param>
        void ClearActiveProjection(EntitySaveComponent saveComponent) {
            saveComponent.ActiveTransformPlatformId = string.Empty;
            saveComponent.ActiveTransformEnvironmentId = string.Empty;
            saveComponent.HasCommonTransformSnapshot = false;
            saveComponent.CommonLocalPositionSnapshot = float3.Zero;
            saveComponent.CommonLocalScaleSnapshot = float3.Zero;
            saveComponent.CommonLocalOrientationSnapshot = float4.Identity;
        }

        /// <summary>
        /// Clears one transform override field from the supplied platform payload and reapplies the active projection when required.
        /// </summary>
        /// <param name="entity">Entity whose live transform may need to update.</param>
        /// <param name="saveComponent">Hidden save component that owns the transform override metadata.</param>
        /// <param name="platformId">Platform identifier whose override field should be cleared.</param>
        /// <param name="fieldKind">Transform field that should return to common behavior.</param>
        void ClearTransformOverride(Entity entity, EntitySaveComponent saveComponent, string platformId, TransformOverrideFieldKind fieldKind) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            } else if (saveComponent == null) {
                throw new ArgumentNullException(nameof(saveComponent));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!saveComponent.TryGetTransformPlatformOverride(platformId, out SceneEntityPlatformTransformOverrideAsset overrideState)) {
                return;
            }

            if (fieldKind == TransformOverrideFieldKind.Position) {
                overrideState.HasLocalPositionOverride = false;
                overrideState.LocalPosition = float3.Zero;
            } else if (fieldKind == TransformOverrideFieldKind.Rotation) {
                overrideState.HasLocalOrientationOverride = false;
                overrideState.LocalOrientation = float4.Identity;
            } else if (fieldKind == TransformOverrideFieldKind.Scale) {
                overrideState.HasLocalScaleOverride = false;
                overrideState.LocalScale = float3.Zero;
            }

            if (!overrideState.HasLocalPositionOverride
                && !overrideState.HasLocalOrientationOverride
                && !overrideState.HasLocalScaleOverride) {
                saveComponent.RemoveTransformPlatformOverride(platformId);
            }

            if (string.Equals(NormalizePlatformId(saveComponent.ActiveTransformPlatformId), NormalizePlatformId(platformId), StringComparison.OrdinalIgnoreCase)) {
                ApplyPlatformTransform(entity, saveComponent, NormalizePlatformId(platformId));
            }
        }

        /// <summary>
        /// Normalizes one platform identifier so comparison logic can treat missing values as the shared common state.
        /// </summary>
        /// <param name="platformId">Platform identifier to normalize.</param>
        /// <returns>Normalized platform identifier.</returns>
        string NormalizePlatformId(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                return CommonPlatformId;
            }

            return platformId;
        }

        /// <summary>
        /// Returns whether the supplied platform identifier refers to the shared common transform state.
        /// </summary>
        /// <param name="platformId">Platform identifier to classify.</param>
        /// <returns>True when the supplied identifier points at the common transform state.</returns>
        bool IsCommonPlatformId(string platformId) {
            return string.Equals(NormalizePlatformId(platformId), CommonPlatformId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Identifies one transform field inside a platform override payload.
        /// </summary>
        enum TransformOverrideFieldKind {
            /// <summary>
            /// Clears the local-position override.
            /// </summary>
            Position,
            /// <summary>
            /// Clears the local-orientation override.
            /// </summary>
            Rotation,
            /// <summary>
            /// Clears the local-scale override.
            /// </summary>
            Scale
        }
    }
}

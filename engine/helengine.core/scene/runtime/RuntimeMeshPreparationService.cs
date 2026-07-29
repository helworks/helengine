namespace helengine {
    /// <summary>
    /// Builds private runtime mesh variants for enabled MeshComponent operations selected to run while a scene loads.
    /// </summary>
    public sealed class RuntimeMeshPreparationService {
        /// <summary>
        /// Stable synthetic member indicating whether component tessellation is enabled.
        /// </summary>
        const string TessellateMemberName = "MeshTessellate";

        /// <summary>
        /// Stable synthetic member containing the maximum permitted world-space tessellation edge length.
        /// </summary>
        const string TessellationMaxEdgeLengthMemberName = "MeshTessellationMaxEdgeLength";

        /// <summary>
        /// Stable synthetic member indicating whether scale baking is enabled.
        /// </summary>
        const string BakeScaleMemberName = "MeshBakeScale";

        /// <summary>
        /// Stable synthetic member selecting package-time execution for tessellation.
        /// </summary>
        const string TessellateAtCookTimeMemberName = "MeshTessellateAtCookTime";

        /// <summary>
        /// Stable synthetic member selecting package-time execution for scale baking.
        /// </summary>
        const string BakeScaleAtCookTimeMemberName = "MeshBakeScaleAtCookTime";

        /// <summary>
        /// Prepares every mesh in one deserialized entity hierarchy before hierarchy initialization registers it with render managers.
        /// </summary>
        /// <param name="rootEntity">Root of the deserialized hierarchy.</param>
        /// <param name="trackPreparedModel">Tracks a created render model as owned by the loading scene.</param>
        public void Prepare(Entity rootEntity, Action<RuntimeModel> trackPreparedModel) {
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            } else if (trackPreparedModel == null) {
                throw new ArgumentNullException(nameof(trackPreparedModel));
            }

            PrepareEntity(rootEntity, trackPreparedModel);
        }

        /// <summary>
        /// Prepares mesh components on one entity and recursively prepares its children.
        /// </summary>
        /// <param name="entity">Entity whose hierarchy is prepared.</param>
        /// <param name="trackPreparedModel">Tracks a created render model as owned by the loading scene.</param>
        void PrepareEntity(Entity entity, Action<RuntimeModel> trackPreparedModel) {
            for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                if (entity.Components[componentIndex] is MeshComponent meshComponent) {
                    PrepareMeshComponent(meshComponent, trackPreparedModel);
                }
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                PrepareEntity(entity.Children[childIndex], trackPreparedModel);
            }
        }

        /// <summary>
        /// Replaces one mesh component's shared source model with a private prepared runtime model when an enabled operation targets load time.
        /// </summary>
        /// <param name="meshComponent">Mesh component whose source model may require preparation.</param>
        /// <param name="trackPreparedModel">Tracks a created render model as owned by the loading scene.</param>
        void PrepareMeshComponent(MeshComponent meshComponent, Action<RuntimeModel> trackPreparedModel) {
            bool tessellateAtLoadTime = meshComponent.GetSyntheticBooleanMemberOrDefault(TessellateMemberName, false)
                && !meshComponent.GetSyntheticBooleanMemberOrDefault(TessellateAtCookTimeMemberName, true);
            bool bakeScaleAtLoadTime = meshComponent.GetSyntheticBooleanMemberOrDefault(BakeScaleMemberName, false)
                && !meshComponent.GetSyntheticBooleanMemberOrDefault(BakeScaleAtCookTimeMemberName, true);
            if (!tessellateAtLoadTime && !bakeScaleAtLoadTime) {
                return;
            }
            if (meshComponent.Model == null || meshComponent.Model.RawModelAsset == null) {
                throw new InvalidOperationException("Load-time mesh preparation requires a runtime model with retained raw geometry.");
            } else if (meshComponent.Parent == null) {
                throw new InvalidOperationException("Load-time mesh preparation requires a MeshComponent parent entity.");
            }

            ModelAsset preparedAsset = ModelTessellationProcessor.Clone(meshComponent.Model.RawModelAsset);
            preparedAsset.Id = string.Empty;
            float3 worldScale = meshComponent.Parent.Scale;
            if (bakeScaleAtLoadTime) {
                ModelTessellationProcessor.ApplyBakeScale(preparedAsset, worldScale);
            }
            if (tessellateAtLoadTime) {
                double maximumEdgeLength = meshComponent.GetSyntheticSingleMemberOrDefault(TessellationMaxEdgeLengthMemberName, 1f);
                ModelTessellationProcessor.Apply(preparedAsset, maximumEdgeLength, bakeScaleAtLoadTime ? float3.One : worldScale);
            }

            RuntimeModel preparedModel = Core.Instance.RenderManager3D.BuildModelFromRaw(preparedAsset);
            preparedModel.SetRawModelAsset(preparedAsset);
            meshComponent.Model = preparedModel;
            meshComponent.SetSyntheticBooleanMember(TessellateAtCookTimeMemberName, true);
            meshComponent.SetSyntheticBooleanMember(BakeScaleAtCookTimeMemberName, true);
            trackPreparedModel(preparedModel);
        }
    }
}

using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Counts model cleanup while exercising the base model's actual owned-buffer release.</summary>
    public sealed class SceneAssetOwnershipTestModel : RuntimeModel {
        /// <summary>Gets the number of model resource releases performed.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Records cleanup before releasing the real submesh and raw-model owners.</summary>
        public override void Dispose() {
            DisposeCount++;
            base.Dispose();
        }
    }
}

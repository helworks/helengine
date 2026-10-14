using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Provides the ordinary 3D release seam without introducing geometry or a graphics device into lifetime tests.</summary>
    public sealed class SceneAssetOwnershipTestRenderManager : RenderManager3D {
        /// <summary>Rejects unexpected model creation because tests supply their own tracked runtime objects.</summary>
        /// <param name="data">Unused CPU geometry that must not enter this lifetime fixture.</param>
        /// <returns>No model is created through this test seam.</returns>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            throw new NotSupportedException("Lifetime tests provide model instances explicitly.");
        }
    }
}

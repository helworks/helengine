using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Counts texture resource disposal so tracking-container deletion cannot masquerade as renderer cleanup.</summary>
    public sealed class SceneAssetOwnershipTestTexture : RuntimeTexture {
        /// <summary>Gets the number of renderer resource releases performed for this texture.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Records one resource release and retains the base texture's disposed state.</summary>
        public override void Dispose() {
            DisposeCount++;
            base.Dispose();
        }
    }
}

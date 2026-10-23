using helengine;
using helengine.core.tests.scene.runtime;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Records generated model ownership without opening a graphics device or retaining disposable raw geometry.</summary>
    public sealed class TextMeshTestRenderManager : RenderManager3D {
        /// <summary>Counts successful generated-model uploads.</summary>
        public int BuildCount { get; private set; }
        /// <summary>Records each model released through its creating renderer.</summary>
        public List<RuntimeModel> Released { get; } = new List<RuntimeModel>();
        /// <summary>Allows a deterministic upload failure to test replacement ownership.</summary>
        public bool FailBuild { get; set; }
        /// <summary>Copies model bounds and returns a distinct owned runtime model for a nonempty glyph run.</summary>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            if (FailBuild) { throw new InvalidOperationException("Synthetic upload failure."); }
            Assert.NotEmpty(data.Positions); BuildCount++;
            SceneAssetOwnershipTestModel model = new SceneAssetOwnershipTestModel(); model.SetBounds(data.BoundsMin, data.BoundsMax); return model;
        }
        /// <summary>Tracks the real base disposal call so double releases remain observable.</summary>
        public override void ReleaseModel(RuntimeModel model) { Released.Add(model); base.ReleaseModel(model); }
    }
}
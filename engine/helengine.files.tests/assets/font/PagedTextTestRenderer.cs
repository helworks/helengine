using helengine;
namespace helengine.files.tests.assets.font {
    /// <summary>Counts actual geometry rebuilds independently of visible atlas residency.</summary>
    sealed class PagedTextTestRenderer : RenderManager3D {
        /// <summary>Counts nonempty model uploads requested by the text component.</summary>
        public int BuildCount { get; private set; }
        /// <summary>Creates a distinct metadata-only model while raw glyph geometry remains scoped to the caller.</summary>
        public override RuntimeModel BuildModelFromRaw(ModelAsset data) {
            BuildCount++; PagedTextTestModel model = new PagedTextTestModel(); model.SetBounds(data.BoundsMin, data.BoundsMax); return model;
        }
    }
}

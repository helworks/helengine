using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies viewport selection resolves blueprint-expanded children to their owning instance root.
    /// </summary>
    public sealed class EditorViewportSceneSelectionFilterBlueprintTests {
        /// <summary>
        /// Ensures picking one expanded blueprint child selects the blueprint instance root instead of the inner entity.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntity_WhenEntityIsInsideBlueprintInstance_ReturnsTheInstanceRoot() {
            CreateCore();
            EditorEntity instanceRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { Name = "Coin01", IsSceneOwned = true };
            instanceRoot.AddComponent(new BlueprintInstanceComponent());
            EditorEntity expandedChild = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { Name = "GoldenCoin", IsSceneOwned = true };
            instanceRoot.AddChild(expandedChild);
            EditorEntity expandedMesh = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { Name = "CoinMesh", IsSceneOwned = true };
            expandedChild.AddChild(expandedMesh);

            Assert.Same(instanceRoot, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(expandedMesh));
            Assert.Same(instanceRoot, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(expandedChild));
            Assert.Same(instanceRoot, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(instanceRoot));
        }

        /// <summary>
        /// Ensures nested blueprint instances resolve to the outermost instance root.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntity_WhenBlueprintInstancesNest_ReturnsTheOutermostRoot() {
            CreateCore();
            EditorEntity outerRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            outerRoot.AddComponent(new BlueprintInstanceComponent());
            EditorEntity innerRoot = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            innerRoot.AddComponent(new BlueprintInstanceComponent());
            outerRoot.AddChild(innerRoot);
            EditorEntity innerChild = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            innerRoot.AddChild(innerChild);

            Assert.Same(outerRoot, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(innerChild));
        }

        /// <summary>
        /// Ensures entities outside any blueprint instance keep resolving to themselves.
        /// </summary>
        [Fact]
        public void ResolveSelectableEntity_WhenEntityIsNotInsideBlueprintInstance_ReturnsTheEntity() {
            CreateCore();
            EditorEntity parentEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            EditorEntity childEntity = new EditorEntity(Core.Instance, new helengine.editor.EditorSessionInteractionServices()) { IsSceneOwned = true };
            parentEntity.AddChild(childEntity);

            Assert.Same(childEntity, EditorViewportSceneSelectionFilter.ResolveSelectableEntity(childEntity));
        }

        /// <summary>
        /// Creates one initialized core instance for selection filter tests.
        /// </summary>
        static void CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }
    }
}

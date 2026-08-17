using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the runtime-only entity suppression flag drives component registration without touching authored enabled state.
    /// </summary>
    public sealed class EntityRuntimeSuppressionTests {
        /// <summary>
        /// Ensures suppressing an entity unregisters its lights while authored enabled state stays untouched.
        /// </summary>
        [Fact]
        public void RuntimeSuppressed_WhenToggled_UnregistersAndReregistersLightsWithoutChangingEnabled() {
            Core core = CreateCore();
            EditorEntity entity = new EditorEntity();
            DirectionalLightComponent light = new DirectionalLightComponent();
            entity.AddComponent(light);
            Assert.Contains(light, core.ObjectManager.DirectionalLights);

            entity.RuntimeSuppressed = true;

            Assert.DoesNotContain(light, core.ObjectManager.DirectionalLights);
            Assert.True(entity.Enabled);
            Assert.False(entity.IsHierarchyEnabled);

            entity.RuntimeSuppressed = false;

            Assert.Contains(light, core.ObjectManager.DirectionalLights);
            Assert.True(entity.IsHierarchyEnabled);
        }

        /// <summary>
        /// Ensures suppression on a parent propagates through the hierarchy so child registrations are released too.
        /// </summary>
        [Fact]
        public void RuntimeSuppressed_OnParent_PropagatesToChildComponents() {
            Core core = CreateCore();
            EditorEntity parentEntity = new EditorEntity();
            EditorEntity childEntity = new EditorEntity();
            parentEntity.AddChild(childEntity);
            DirectionalLightComponent childLight = new DirectionalLightComponent();
            childEntity.AddComponent(childLight);
            Assert.Contains(childLight, core.ObjectManager.DirectionalLights);

            parentEntity.RuntimeSuppressed = true;

            Assert.DoesNotContain(childLight, core.ObjectManager.DirectionalLights);
            Assert.True(childEntity.Enabled);

            parentEntity.RuntimeSuppressed = false;

            Assert.Contains(childLight, core.ObjectManager.DirectionalLights);
        }

        /// <summary>
        /// Creates one initialized core instance for suppression tests.
        /// </summary>
        /// <returns>Initialized core.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            return core;
        }
    }
}

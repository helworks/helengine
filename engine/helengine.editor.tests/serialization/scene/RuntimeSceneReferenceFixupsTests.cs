using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies sparse runtime scene-entity reference binding.
    /// </summary>
    public sealed class RuntimeSceneReferenceFixupsTests {
        [Fact]
        public void Bind_WhenMultipleReferencesRequestOneChild_BindsEachReferenceToThatEntity() {
            using Core core = CreateCore();
            Entity root = CreateEntity(core, 10u);
            Entity target = CreateEntity(core, 20u);
            root.AddChild(target);
            SceneEntityReference first = new SceneEntityReference { EntityId = 20u };
            SceneEntityReference second = new SceneEntityReference { EntityId = 20u };
            RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(first, "Demo.FirstComponent");
            fixups.Track(second, "Demo.SecondComponent");

            fixups.Bind(new[] { root });

            Assert.Same(target, first.ResolvedEntity);
            Assert.Same(target, second.ResolvedEntity);
        }

        [Fact]
        public void Bind_WhenReferenceIsNullOrZero_LeavesItOptionalAndUnresolved() {
            using Core core = CreateCore();
            Entity root = CreateEntity(core, 10u);
            SceneEntityReference zero = new SceneEntityReference { EntityId = 0u };
            RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(null, "Demo.NullComponent");
            fixups.Track(zero, "Demo.OptionalComponent");

            fixups.Bind(new[] { root });

            Assert.Null(zero.ResolvedEntity);
        }

        [Fact]
        public void Bind_WhenRequestedIdIsMissing_ReportsComponentAndIdAndClearsRequests() {
            using Core core = CreateCore();
            Entity root = CreateEntity(core, 10u);
            SceneEntityReference missing = new SceneEntityReference { EntityId = 99u };
            RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(missing, "Demo.MissingComponent");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => fixups.Bind(new[] { root }));

            Assert.Contains("Demo.MissingComponent", exception.Message, StringComparison.Ordinal);
            Assert.Contains("99", exception.Message, StringComparison.Ordinal);
            fixups.Bind(Array.Empty<Entity>());
        }

        [Fact]
        public void Bind_WhenRequestedIdOccursOnTwoEntities_ReportsComponentAndId() {
            using Core core = CreateCore();
            Entity firstTarget = CreateEntity(core, 20u);
            Entity secondTarget = CreateEntity(core, 20u);
            SceneEntityReference reference = new SceneEntityReference { EntityId = 20u };
            RuntimeSceneReferenceFixups fixups = new RuntimeSceneReferenceFixups();
            fixups.Track(reference, "Demo.DuplicateComponent");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => fixups.Bind(new[] { firstTarget, secondTarget }));

            Assert.Contains("Demo.DuplicateComponent", exception.Message, StringComparison.Ordinal);
            Assert.Contains("20", exception.Message, StringComparison.Ordinal);
        }

        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            return core;
        }

        static Entity CreateEntity(Core core, uint sceneEntityId) {
            Entity entity = new Entity(core);
            entity.InitComponents();
            entity.InitChildren();
            entity.AddComponent(new SceneEntityRuntimeIdComponent { SceneEntityId = sceneEntityId });
            return entity;
        }
    }
}

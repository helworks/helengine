using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the global entity-disposal ordering contract used by runtime components that own generated child hierarchies.
    /// </summary>
    public class EntityDisposeOrderingTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes a deterministic runtime core for entity-lifecycle tests.
        /// </summary>
        public EntityDisposeOrderingTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-dispose-ordering-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            TestClockDrivenCore core = new TestClockDrivenCore(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures direct parent components still see their generated child hierarchy during parent disposal.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentOwnsGeneratedChildHierarchy_ParentComponentSeesChildBeforeChildDisposal() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();
            ParentChildVisibilityProbeComponent probe = new ParentChildVisibilityProbeComponent(child);

            parent.AddChild(child);
            parent.AddComponent(probe);

            parent.Dispose();

            Assert.True(probe.SawExpectedChildStateDuringRemoval);
        }

        /// <summary>
        /// Ensures parent component removal happens before child component removal during recursive disposal.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentHasChild_ParentComponentsAreRemovedBeforeChildComponents() {
            List<string> events = new List<string>();
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.AddComponent(new OrderingProbeComponent("parent", events));
            child.AddComponent(new OrderingProbeComponent("child", events));
            parent.AddChild(child);

            parent.Dispose();

            Assert.Equal(
                new[] { "parent:disabled", "parent:removed", "child:disabled", "child:removed" },
                events);
        }

        /// <summary>
        /// Ensures generated child references become invalid after the owning parent disposal completes.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentOwnsGeneratedChildHierarchy_DisposedChildReferenceThrowsAfterTeardownCompletes() {
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.AddChild(child);
            parent.Dispose();

            Assert.Throws<InvalidOperationException>(() => _ = child.Position);
        }

        /// <summary>
        /// Ensures parent component disposal is deferred until child component-removal callbacks finish so child teardown can still detach from parent-owned resources safely.
        /// </summary>
        [Fact]
        public void Dispose_WhenParentHasChild_ParentComponentDisposeRunsAfterChildComponentRemoval() {
            List<string> events = new List<string>();
            Entity parent = CreateInitializedEntity();
            Entity child = CreateInitializedEntity();

            parent.AddComponent(new DisposalTimingProbeComponent("parent", events));
            child.AddComponent(new DisposalTimingProbeComponent("child", events));
            parent.AddChild(child);

            parent.Dispose();

            int parentRemovedIndex = events.IndexOf("parent:removed");
            int childRemovedIndex = events.IndexOf("child:removed");
            int parentDisposedIndex = events.IndexOf("parent:disposed");

            Assert.True(parentRemovedIndex >= 0);
            Assert.True(childRemovedIndex >= 0);
            Assert.True(parentDisposedIndex >= 0);
            Assert.True(parentRemovedIndex < childRemovedIndex);
            Assert.True(childRemovedIndex < parentDisposedIndex);
        }

        /// <summary>
        /// Creates one initialized entity ready for component lifecycle participation.
        /// </summary>
        /// <returns>Initialized entity with child and component collections.</returns>
        static Entity CreateInitializedEntity() {
            Entity entity = new Entity();
            entity.InitChildren();
            entity.InitComponents();
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Records whether the expected child entity is still visible during parent-component removal.
        /// </summary>
        sealed class ParentChildVisibilityProbeComponent : Component {
            /// <summary>
            /// Child entity expected to remain visible until parent-component removal completes.
            /// </summary>
            readonly Entity ExpectedChild;

            /// <summary>
            /// Gets whether the parent still owned the expected child during removal.
            /// </summary>
            public bool SawExpectedChildStateDuringRemoval { get; private set; }

            /// <summary>
            /// Initializes the probe with the expected generated child entity.
            /// </summary>
            /// <param name="expectedChild">Child entity that should still be attached during removal.</param>
            public ParentChildVisibilityProbeComponent(Entity expectedChild) {
                ExpectedChild = expectedChild ?? throw new ArgumentNullException(nameof(expectedChild));
            }

            /// <summary>
            /// Captures whether the child is still attached during parent-component removal.
            /// </summary>
            /// <param name="entity">Parent entity being disposed.</param>
            public override void ComponentRemoved(Entity entity) {
                SawExpectedChildStateDuringRemoval =
                    entity != null
                    && entity.Children != null
                    && entity.Children.Contains(ExpectedChild)
                    && ExpectedChild.Parent == entity;
                base.ComponentRemoved(entity);
            }
        }

        /// <summary>
        /// Records the disable and removal ordering for one component.
        /// </summary>
        sealed class OrderingProbeComponent : Component {
            /// <summary>
            /// Human-readable probe name.
            /// </summary>
            readonly string Name;

            /// <summary>
            /// Shared event sink that records lifecycle callbacks.
            /// </summary>
            readonly List<string> Events;

            /// <summary>
            /// Initializes the ordering probe.
            /// </summary>
            /// <param name="name">Probe name written into the shared log.</param>
            /// <param name="events">Shared event list.</param>
            public OrderingProbeComponent(string name, List<string> events) {
                Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Probe name must be provided.", nameof(name)) : name;
                Events = events ?? throw new ArgumentNullException(nameof(events));
            }

            /// <summary>
            /// Records one disable callback.
            /// </summary>
            /// <param name="newEnabled">New enabled state.</param>
            public override void ParentEnabledChange(bool newEnabled) {
                Events.Add(Name + ":" + (newEnabled ? "enabled" : "disabled"));
                base.ParentEnabledChange(newEnabled);
            }

            /// <summary>
            /// Records one removal callback.
            /// </summary>
            /// <param name="entity">Owning entity being disposed.</param>
            public override void ComponentRemoved(Entity entity) {
                Events.Add(Name + ":removed");
                base.ComponentRemoved(entity);
            }
        }

        /// <summary>
        /// Records when one component is removed and when its disposal method executes.
        /// </summary>
        sealed class DisposalTimingProbeComponent : Component {
            /// <summary>
            /// Human-readable probe name.
            /// </summary>
            readonly string Name;

            /// <summary>
            /// Shared event sink that records lifecycle callbacks.
            /// </summary>
            readonly List<string> Events;

            /// <summary>
            /// Initializes the probe with one log sink.
            /// </summary>
            /// <param name="name">Probe name written into the shared log.</param>
            /// <param name="events">Shared event list.</param>
            public DisposalTimingProbeComponent(string name, List<string> events) {
                Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Probe name must be provided.", nameof(name)) : name;
                Events = events ?? throw new ArgumentNullException(nameof(events));
            }

            /// <summary>
            /// Records one removal callback.
            /// </summary>
            /// <param name="entity">Owning entity being disposed.</param>
            public override void ComponentRemoved(Entity entity) {
                Events.Add(Name + ":removed");
                base.ComponentRemoved(entity);
            }

            /// <summary>
            /// Records one disposal callback.
            /// </summary>
            public override void Dispose() {
                Events.Add(Name + ":disposed");
                base.Dispose();
            }
        }
    }
}

using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that entity registration honors the effective enabled state of the full hierarchy.
    /// </summary>
    public class EntityHierarchyEnabledStateTests {
        /// <summary>
        /// Ensures components created before parenting are removed from engine registries when the child enters a disabled hierarchy.
        /// </summary>
        [Fact]
        public void AddChild_WhenParentIsDisabled_UnregistersExistingChildComponentsUntilHierarchyIsReenabled() {
            InitializeCore();

            Entity parent = CreateEntity();
            parent.Enabled = false;

            Entity child = CreateEntity();
            SpriteComponent sprite = new SpriteComponent();
            InteractableComponent interactable = new InteractableComponent();
            UpdateComponent updateComponent = new UpdateComponent();

            child.AddComponent(sprite);
            child.AddComponent(interactable);
            child.AddComponent(updateComponent);

            Assert.Contains(sprite, Core.Instance.ObjectManager.Drawables2D);
            Assert.Contains(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.Contains(updateComponent, Core.Instance.ObjectManager.Updateables);

            parent.AddChild(child);

            Assert.DoesNotContain(sprite, Core.Instance.ObjectManager.Drawables2D);
            Assert.DoesNotContain(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.DoesNotContain(updateComponent, Core.Instance.ObjectManager.Updateables);

            parent.Enabled = true;

            Assert.Contains(sprite, Core.Instance.ObjectManager.Drawables2D);
            Assert.Contains(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.Contains(updateComponent, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Ensures components added after parenting into a disabled hierarchy stay inactive until the hierarchy becomes enabled again.
        /// </summary>
        [Fact]
        public void AddComponent_WhenEntityIsInsideDisabledHierarchy_DoesNotRegisterUntilHierarchyIsReenabled() {
            InitializeCore();

            Entity parent = CreateEntity();
            parent.Enabled = false;

            Entity child = CreateEntity();
            parent.AddChild(child);

            SpriteComponent sprite = new SpriteComponent();
            InteractableComponent interactable = new InteractableComponent();
            UpdateComponent updateComponent = new UpdateComponent();

            child.AddComponent(sprite);
            child.AddComponent(interactable);
            child.AddComponent(updateComponent);

            Assert.DoesNotContain(sprite, Core.Instance.ObjectManager.Drawables2D);
            Assert.DoesNotContain(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.DoesNotContain(updateComponent, Core.Instance.ObjectManager.Updateables);

            parent.Enabled = true;

            Assert.Contains(sprite, Core.Instance.ObjectManager.Drawables2D);
            Assert.Contains(interactable, Core.Instance.ObjectManager.Interactables);
            Assert.Contains(updateComponent, Core.Instance.ObjectManager.Updateables);
        }

        /// <summary>
        /// Initializes the core services required for entity-registration tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates an entity with initialized component and child collections.
        /// </summary>
        /// <returns>Entity ready for parenting and component attachment.</returns>
        Entity CreateEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}

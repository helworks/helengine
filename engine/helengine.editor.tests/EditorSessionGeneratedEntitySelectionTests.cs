using System.Reflection;
using helengine.editor.tests.testing;

namespace helengine.editor.tests {
    /// <summary>Checks hierarchy selection and inspector rendering for generated entities without authored save metadata.</summary>
    public sealed class EditorSessionGeneratedEntitySelectionTests {
        /// <summary>Authored menu scroll components remain data through attachment, initialization, property changes, and frames without generating runtime helpers.</summary>
        [Fact]
        public void SceneScrollComponents_InEditor_CreateNoHelpersOrScrollUpdates() {
            using RealEditorSessionFixture fixture = new RealEditorSessionFixture();
            EditorSession session = fixture.Session;
            EditorEntity items = new EditorEntity(fixture.Core, session.InteractionServices) {
                Name = "Panel-scene-select-ItemsRoot", IsSceneOwned = true, LayerMask = EditorLayerMasks.SceneObjects
            };
            items.Components.OfType<EntitySaveComponent>().Single().EntityId = fixture.Core.SceneEntityIdAllocator.Allocate();
            ScrollComponent scroll = new ScrollComponent { Size = new int2(160, 100), ItemCount = 20, VisibleItemCount = 4, ItemExtent = 48 };
            ScrollBarComponent standaloneBar = new ScrollBarComponent(new int2(8, 100)) { Target = scroll };
            int entityCount = fixture.Core.ObjectManager.Entities.Count;
            items.AddComponent(scroll);
            items.AddComponent(standaloneBar);
            items.InitializeHierarchy();
            scroll.ShowScrollBar = false;
            scroll.ShowScrollBar = true;
            fixture.Input.SetMouseState(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            fixture.Input.SetMouseState(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            session.UpdateFrame(1280, 720);
            Assert.True(scroll.ShowScrollBar);
            Assert.Null(scroll.ScrollBar);
            Assert.Empty(items.Children);
            Assert.Equal(0, scroll.ScrollOffset);
            Assert.DoesNotContain(scroll, fixture.Core.ObjectManager.Updateables);
            Assert.Equal(entityCount, fixture.Core.ObjectManager.Entities.Count);
            session.RefreshHierarchy();
            SceneHierarchyPanel hierarchy = (SceneHierarchyPanel)Assert.Single(session.GetPanelInstancesForTest("scene-hierarchy")).Dockable;
            List<SceneHierarchyRow> rows = GetField<List<SceneHierarchyRow>>(hierarchy, "rows");
            SceneHierarchyRow row = Assert.Single(rows, candidate => ReferenceEquals(candidate.NodeEntity, items));
            session.InteractionServices.KeyboardFocus.SetFocusedTarget(row.FocusTarget);
            session.InteractionServices.KeyboardFocus.HandleActivationKey(Keys.Enter);
            Assert.Same(items, session.InteractionServices.Selection.SelectedEntity);
        }

        /// <summary>Editor-owned scrolling retains its update registration, wheel movement, and generated draggable scrollbar in the production session.</summary>
        [Fact]
        public void EditorScrollComponent_InEditor_RunsWheelAndScrollBarInput() {
            using RealEditorSessionFixture fixture = new RealEditorSessionFixture();
            SceneHierarchyPanel hierarchy = (SceneHierarchyPanel)Assert.Single(fixture.Session.GetPanelInstancesForTest("scene-hierarchy")).Dockable;
            EditorScrollComponent panelScroll = Assert.IsType<EditorScrollComponent>(GetField<ScrollComponent>(hierarchy, "scrollComponent"));
            Assert.IsType<EditorScrollBarComponent>(panelScroll.ScrollBar);
            EditorEntity viewport = new EditorEntity(fixture.Core, fixture.Session.InteractionServices);
            EditorScrollComponent scroll = new EditorScrollComponent { Size = new int2(160, 100), ItemCount = 24, ItemExtent = 10 };
            viewport.AddComponent(scroll);
            viewport.InitializeHierarchy();
            EditorScrollBarComponent bar = Assert.IsType<EditorScrollBarComponent>(scroll.ScrollBar);
            Assert.Contains(scroll, fixture.Core.ObjectManager.Updateables);
            Assert.True(bar.IsVisible);
            fixture.Input.SetMouseState(new MouseState(40, 50, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            fixture.Session.UpdateFrame(1280, 720);
            fixture.Input.SetMouseState(new MouseState(40, 50, -120, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            fixture.Session.UpdateFrame(1280, 720);
            Assert.Equal(1, scroll.ScrollOffset);
            InteractableComponent input = Assert.Single(bar.Parent.Children[0].Components.OfType<InteractableComponent>());
            input.OnCursor(new int2(7, 99), int2.Zero, PointerInteraction.Press);
            input.OnCursor(new int2(7, 99), int2.Zero, PointerInteraction.Release);
            Assert.Equal(scroll.MaximumScrollOffset, scroll.ScrollOffset);
        }

        /// <summary>Generated text and cameras remain inspectable across scopes without inventing persistence or exposing unsaved component edits.</summary>
        /// <param name="camera">True to exercise custom camera properties; false to exercise generated text.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SelectGeneratedHierarchyRow_WithoutSaveMetadata_ShowsReadOnlyComponents(bool camera) {
            using RealEditorSessionFixture fixture = new RealEditorSessionFixture();
            EditorSession session = fixture.Session;
            EditorEntity root = new EditorEntity(fixture.Core, session.InteractionServices) {
                Name = "Generated Menu", IsSceneOwned = true, LayerMask = EditorLayerMasks.SceneObjects
            };
            root.Components.OfType<EntitySaveComponent>().Single().EntityId = fixture.Core.SceneEntityIdAllocator.Allocate();
            Entity child = new Entity(fixture.Core) { LayerMask = EditorLayerMasks.SceneObjects };
            child.InitComponents();
            child.InitChildren();
            root.AddChild(child);
            if (camera) {
                child.AddComponent(new CameraComponent());
            } else {
                child.AddComponent(new TextComponent { Text = "HELENGINE", Size = new int2(280, 28) });
            }
            root.InitializeHierarchy();
            session.RefreshHierarchy();
            SceneHierarchyPanel hierarchy = (SceneHierarchyPanel)Assert.Single(session.GetPanelInstancesForTest("scene-hierarchy")).Dockable;
            SceneHierarchyRow row = GetField<List<SceneHierarchyRow>>(hierarchy, "rows").Single(candidate => ReferenceEquals(candidate.NodeEntity, child));
            session.InteractionServices.KeyboardFocus.SetFocusedTarget(row.FocusTarget);
            fixture.Input.SetKeyboardState(new KeyboardState(Keys.Enter));
            session.UpdateFrame(1280, 720);
            Assert.Same(child, session.InteractionServices.Selection.SelectedEntity);
            PropertiesPanel properties = (PropertiesPanel)Assert.Single(session.GetPanelInstancesForTest("properties")).Dockable;
            ComponentPropertiesView view = GetField<ComponentPropertiesView>(properties, "ComponentView");
            foreach (string platform in new[] { "common", "windows" }) {
                view.ShowComponents(child, platform, "release", false);
                List<ComponentPropertyRow> rows = GetField<List<ComponentPropertyRow>>(view, "ActiveRows");
                Assert.NotEmpty(rows);
                Assert.All(rows, property => Assert.Equal(ComponentPropertyRowKind.ReadOnly, property.Kind));
            }
            Assert.Empty(child.Components.OfType<EntitySaveComponent>());
        }

        /// <summary>Retrieves panel-owned rows and presentation state to exercise real session selection wiring.</summary>
        /// <param name="owner">Panel that owns the requested field.</param>
        /// <param name="name">Field name used by the panel.</param>
        /// <typeparam name="T">Field type expected by this regression.</typeparam>
        /// <returns>Current panel-owned field value.</returns>
        static T GetField<T>(object owner, string name) {
            return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        }
    }
}

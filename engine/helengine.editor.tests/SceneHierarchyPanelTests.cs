using System.Collections.Generic;
using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene-hierarchy interaction behavior that should mirror viewport selection.
    /// </summary>
    public class SceneHierarchyPanelTests : IDisposable {
        /// <summary>
        /// Clears shared editor selection state after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
        }

        /// <summary>
        /// Ensures clicking a hierarchy row selects the entity represented by that row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyRow_SelectsTheRowEntity() {
            InitializeCore();
            EditorEntity selectedEntity = new EditorEntity {
                Name = "Selected From Hierarchy"
            };
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());

            InteractableComponent rowInteractable = FindHierarchyRowInteractable();

            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Hover);
            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Press);
            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Same(selectedEntity, EditorSelectionService.SelectedEntity);
        }

        /// <summary>
        /// Initializes a core instance with the minimum services required by dockable UI controls.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy hierarchy label layout in tests.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }

        /// <summary>
        /// Finds the interactable associated with the first visible hierarchy row.
        /// </summary>
        /// <returns>Interactable for the first hierarchy row.</returns>
        InteractableComponent FindHierarchyRowInteractable() {
            List<IInteractable2D> interactables = Core.Instance.ObjectManager.Interactables;
            for (int interactableIndex = 0; interactableIndex < interactables.Count; interactableIndex++) {
                if (interactables[interactableIndex] is InteractableComponent interactable &&
                    interactable.Size.Y == SceneHierarchyPanel.RowHeight) {
                    return interactable;
                }
            }

            throw new InvalidOperationException("Expected the hierarchy panel to register a row interactable.");
        }
    }
}

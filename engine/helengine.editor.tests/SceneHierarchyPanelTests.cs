using System.Collections.Generic;
using System.Reflection;
using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies scene-hierarchy interaction behavior that should mirror viewport selection.
    /// </summary>
    public class SceneHierarchyPanelTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate test core services.
        /// </summary>
        readonly string TempRootPath;
        /// <summary>
        /// Input manager used to simulate pointer interaction for context-menu tests.
        /// </summary>
        readonly TestInputBackend Input;

        /// <summary>
        /// Initializes the core services required by scene-hierarchy tests.
        /// </summary>
        public SceneHierarchyPanelTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scenehierarchy-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            Input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), Input);

            CreateUiCamera(640, 480, 0b1000000000000000);
        }

        /// <summary>
        /// Clears shared editor selection state after each test.
        /// </summary>
        public void Dispose() {
            EditorSelectionService.ClearSelection();
            EditorInputCaptureService.Reset();
            EditorKeyboardFocusService.Reset();

            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures clicking a hierarchy row selects the entity represented by that row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyRow_SelectsTheRowEntity() {
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
        /// Ensures clicking the parent-row arrow collapses and re-expands that branch without removing the parent row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyArrow_CollapsesAndExpandsTheParentBranch() {
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);

            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));

            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Hover);
            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Press);
            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(new[] { parent }, GetVisibleRowEntities(panel));

            parentRow = FindVisibleRow(panel, parent);
            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Hover);
            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Press);
            parentRow.Interactable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));
        }

        /// <summary>
        /// Ensures clicking the row body selects the parent entity without collapsing its visible child branch.
        /// </summary>
        [Fact]
        public void ClickingHierarchyRowBody_SelectsWithoutCollapsingTheBranch() {
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);

            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Hover);
            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Press);
            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Same(parent, EditorSelectionService.SelectedEntity);
            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));
        }

        /// <summary>
        /// Ensures activating the Reparent context-menu entry raises a reparent request for the clicked row entity.
        /// </summary>
        [Fact]
        public void RightClickingHierarchyRow_ReparentMenuItem_RaisesReparentRequested() {
            EditorEntity selectedEntity = new EditorEntity {
                Name = "Selected From Hierarchy"
            };
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
                Position = new float3(32, 40, 0),
                Size = new int2(320, 240)
            };

            Entity requestedEntity = null;
            int requestedCount = 0;
            panel.ReparentRequested += entity => {
                requestedEntity = entity;
                requestedCount++;
            };

            int2 rowPointer = new int2(
                (int)Math.Round(panel.Position.X) + 24,
                (int)Math.Round(panel.Position.Y) + DockableEntity.TitleBarHeight + (SceneHierarchyPanel.RowHeight / 2));
            int2 menuPointer = new int2(
                rowPointer.X + 16,
                rowPointer.Y + ContextMenu.PaddingY + (ContextMenu.RowHeight / 2));
            ContextMenu hierarchyContextMenu = GetPrivateField<ContextMenu>(panel, "hierarchyContextMenu");

            AdvanceInput(new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released));
            Assert.Same(selectedEntity, EditorSelectionService.SelectedEntity);
            Assert.True(hierarchyContextMenu.IsVisible);
            AdvanceInput(new MouseState(rowPointer.X, rowPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Assert.True(hierarchyContextMenu.IsVisible);
            AdvanceInput(new MouseState(menuPointer.X, menuPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            AdvanceInput(new MouseState(menuPointer.X, menuPointer.Y, 0, ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));
            Assert.True(hierarchyContextMenu.IsVisible);
            AdvanceInput(new MouseState(menuPointer.X, menuPointer.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released));

            Assert.Equal(1, requestedCount);
            Assert.Same(selectedEntity, requestedEntity);
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

        /// <summary>
        /// Returns the currently visible row assigned to the provided entity.
        /// </summary>
        /// <param name="panel">Panel to inspect.</param>
        /// <param name="entity">Entity expected in one visible row.</param>
        /// <returns>Visible row representing the entity.</returns>
        SceneHierarchyRow FindVisibleRow(SceneHierarchyPanel panel, Entity entity) {
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (row.Entity.Enabled && ReferenceEquals(row.NodeEntity, entity)) {
                    return row;
                }
            }

            throw new InvalidOperationException("Expected the entity to have one visible hierarchy row.");
        }

        /// <summary>
        /// Returns the entities currently represented by enabled hierarchy rows.
        /// </summary>
        /// <param name="panel">Panel to inspect.</param>
        /// <returns>Visible hierarchy row entities in display order.</returns>
        Entity[] GetVisibleRowEntities(SceneHierarchyPanel panel) {
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            List<Entity> entities = new List<Entity>();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (!row.Entity.Enabled || row.NodeEntity == null) {
                    continue;
                }

                entities.Add(row.NodeEntity);
            }

            return entities.ToArray();
        }

        /// <summary>
        /// Advances the test input state by one core frame.
        /// </summary>
        /// <param name="mouseState">Mouse state to expose during the frame.</param>
        void AdvanceInput(MouseState mouseState) {
            Input.SetMouseState(mouseState);
            Core.Instance.Update();
        }

        /// <summary>
        /// Reads one private field from the provided object.
        /// </summary>
        /// <typeparam name="T">Field value type.</typeparam>
        /// <param name="instance">Object that owns the field.</param>
        /// <param name="fieldName">Private field name.</param>
        /// <returns>Resolved field value.</returns>
        T GetPrivateField<T>(object instance, string fieldName) {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (T)field.GetValue(instance);
        }

        /// <summary>
        /// Creates the UI camera used to route hierarchy and context-menu hit testing.
        /// </summary>
        /// <param name="width">Viewport width.</param>
        /// <param name="height">Viewport height.</param>
        /// <param name="layerMask">Layer mask rendered by the camera.</param>
        void CreateUiCamera(int width, int height, ushort layerMask) {
            EditorEntity cameraEntity = new EditorEntity {
                InternalEntity = true,
                LayerMask = layerMask
            };

            CameraComponent camera = new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = 255,
                Viewport = new float4(0f, 0f, width, height)
            };
            cameraEntity.AddComponent(camera);
        }
    }
}


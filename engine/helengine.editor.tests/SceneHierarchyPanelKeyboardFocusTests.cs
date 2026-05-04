using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for scene-hierarchy rows.
    /// </summary>
    public class SceneHierarchyPanelKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Clears shared editor selection and keyboard-focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();
        }

        /// <summary>
        /// Ensures keyboard activation on one hierarchy row selects the represented entity.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenRowIsActivated_SelectsTheRepresentedEntity() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity cube = new EditorEntity {
                Name = "Cube"
            };

            panel.RefreshHierarchy();

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            rows[0].FocusTarget.ActivateFromKey(Keys.Enter);

            Assert.Same(cube, EditorSelectionService.SelectedEntity);
        }

        /// <summary>
        /// Ensures row relayout reuses pooled focus targets and updates traversal order instead of recreating targets.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenRowsAreRelaidOut_ReusesExistingFocusTargetsAndUpdatesTabIndex() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity first = new EditorEntity {
                Name = "First"
            };

            panel.RefreshHierarchy();

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            EditorFocusTarget firstTarget = rows[0].FocusTarget;

            EditorEntity second = new EditorEntity {
                Name = "Second"
            };

            panel.RefreshHierarchy();

            rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            Assert.Same(firstTarget, rows[0].FocusTarget);
            Assert.Same(first, rows[0].NodeEntity);
            Assert.Same(second, rows[1].NodeEntity);
            Assert.Equal(0, rows[0].FocusTarget.TabIndex);
            Assert.Equal(1, rows[1].FocusTarget.TabIndex);
        }

        /// <summary>
        /// Ensures changing the editor selection updates the matching hierarchy row state and highlight color.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenSelectionChanges_MarksTheMatchingRowAsSelected() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity first = new EditorEntity {
                Name = "First"
            };
            EditorEntity second = new EditorEntity {
                Name = "Second"
            };

            panel.RefreshHierarchy();

            EditorSelectionService.SetSelectedEntity(second);

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            Assert.Same(first, rows[0].NodeEntity);
            Assert.Same(second, rows[1].NodeEntity);
            Assert.False(rows[0].IsSelected);
            Assert.True(rows[1].IsSelected);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, rows[1].Background.Color);
        }

        /// <summary>
        /// Ensures pressing the right arrow on a collapsed focused parent expands its visible branch.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenRightKeyIsPressedOnCollapsedParent_ExpandsTheFocusedBranch() {
            TestInputBackend input = InitializeCore();
            SceneHierarchyPanel panel = CreateRegisteredPanel();
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            parent.AddChild(child);

            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);
            ClickArrow(parentRow);

            parentRow = FindVisibleRow(panel, parent);
            EditorKeyboardFocusService.SetFocusedTarget(parentRow.FocusTarget);

            PressKey(input, Keys.Right);

            Entity[] visibleEntities = GetVisibleRowEntities(panel);

            Assert.Equal(2, visibleEntities.Length);
            Assert.Same(parent, visibleEntities[0]);
            Assert.Same(child, visibleEntities[1]);
            Assert.True(FindVisibleRow(panel, parent).IsKeyboardFocused);
        }

        /// <summary>
        /// Ensures pressing the left arrow on an expanded focused parent hides its visible descendants.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenLeftKeyIsPressedOnExpandedParent_CollapsesTheFocusedBranch() {
            TestInputBackend input = InitializeCore();
            SceneHierarchyPanel panel = CreateRegisteredPanel();
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            parent.AddChild(child);

            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);
            EditorKeyboardFocusService.SetFocusedTarget(parentRow.FocusTarget);

            PressKey(input, Keys.Left);

            Entity[] visibleEntities = GetVisibleRowEntities(panel);

            Assert.Single(visibleEntities);
            Assert.Same(parent, visibleEntities[0]);
            Assert.True(FindVisibleRow(panel, parent).IsKeyboardFocused);
        }

        /// <summary>
        /// Ensures pressing the down arrow on a focused row moves keyboard focus to the next visible row.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenDownKeyIsPressed_MovesFocusToTheNextVisibleRow() {
            TestInputBackend input = InitializeCore();
            SceneHierarchyPanel panel = CreateRegisteredPanel();
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            EditorEntity sibling = new EditorEntity {
                Name = "Sibling"
            };
            parent.AddChild(child);

            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);
            SceneHierarchyRow childRow = FindVisibleRow(panel, child);
            SceneHierarchyRow siblingRow = FindVisibleRow(panel, sibling);

            EditorKeyboardFocusService.SetFocusedTarget(parentRow.FocusTarget);

            PressKey(input, Keys.Down);

            Assert.True(childRow.IsKeyboardFocused);
            Assert.False(parentRow.IsKeyboardFocused);
            Assert.False(siblingRow.IsKeyboardFocused);
        }

        /// <summary>
        /// Ensures pressing the up arrow on a focused row moves keyboard focus to the previous visible row.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenUpKeyIsPressed_MovesFocusToThePreviousVisibleRow() {
            TestInputBackend input = InitializeCore();
            SceneHierarchyPanel panel = CreateRegisteredPanel();
            EditorEntity parent = new EditorEntity {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity {
                Name = "Child"
            };
            EditorEntity sibling = new EditorEntity {
                Name = "Sibling"
            };
            parent.AddChild(child);

            panel.RefreshHierarchy();

            SceneHierarchyRow childRow = FindVisibleRow(panel, child);
            SceneHierarchyRow siblingRow = FindVisibleRow(panel, sibling);

            EditorKeyboardFocusService.SetFocusedTarget(siblingRow.FocusTarget);

            PressKey(input, Keys.Up);

            Assert.True(childRow.IsKeyboardFocused);
            Assert.False(siblingRow.IsKeyboardFocused);
        }

        /// <summary>
        /// Initializes the core services required by hierarchy keyboard-focus tests.
        /// </summary>
        /// <returns>Input manager bound to the created core.</returns>
        TestInputBackend InitializeCore() {
            Core core = new Core();
            TestInputBackend input = new TestInputBackend();
            core.Initialize(null, new TestRenderManager2D(), input);
            core.InputSystem.SetKeyboardActive(true);
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();

            EditorEntity keyboardFocusEntity = new EditorEntity {
                InternalEntity = true,
                Enabled = true,
                LayerMask = EditorLayerMasks.EditorUi
            };
            EditorKeyboardFocusUpdateComponent keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
                UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1)
            };
            keyboardFocusEntity.AddComponent(keyboardFocusUpdateComponent);

            return input;
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Creates a deterministic font asset for hierarchy row layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
        /// Creates one hierarchy panel and registers it with the shared keyboard-focus service.
        /// </summary>
        /// <returns>Registered hierarchy panel.</returns>
        SceneHierarchyPanel CreateRegisteredPanel() {
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorKeyboardFocusService.RegisterGroup(panel);
            return panel;
        }

        /// <summary>
        /// Finds one visible hierarchy row for the provided entity.
        /// </summary>
        /// <param name="panel">Panel that owns the rows.</param>
        /// <param name="entity">Entity represented by the expected row.</param>
        /// <returns>Matching visible row.</returns>
        SceneHierarchyRow FindVisibleRow(SceneHierarchyPanel panel, Entity entity) {
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            SceneHierarchyRow row = rows.FirstOrDefault(candidate =>
                candidate.Entity.Enabled &&
                ReferenceEquals(candidate.NodeEntity, entity));
            if (row == null) {
                throw new InvalidOperationException("Expected visible hierarchy row was not found.");
            }

            return row;
        }

        /// <summary>
        /// Returns the entities represented by the currently visible hierarchy rows.
        /// </summary>
        /// <param name="panel">Panel that owns the rows.</param>
        /// <returns>Visible row entities in on-screen order.</returns>
        Entity[] GetVisibleRowEntities(SceneHierarchyPanel panel) {
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            return rows
                .Where(row => row.Entity.Enabled && row.NodeEntity != null)
                .Select(row => row.NodeEntity)
                .ToArray();
        }

        /// <summary>
        /// Simulates one complete left-click on a row arrow hit target.
        /// </summary>
        /// <param name="row">Row whose arrow should be clicked.</param>
        void ClickArrow(SceneHierarchyRow row) {
            int2 point = new int2(10, SceneHierarchyPanel.RowHeight / 2);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Hover);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Press);
            row.Interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Release);
        }

        /// <summary>
        /// Simulates one key press and release across successive core updates.
        /// </summary>
        /// <param name="input">Input manager used by the active core.</param>
        /// <param name="key">Key to press.</param>
        void PressKey(TestInputBackend input, Keys key) {
            input.SetKeyboardState(new KeyboardState(key));
            Core.Instance.Update();
            input.SetKeyboardState(new KeyboardState());
            Core.Instance.Update();
        }
    }
}


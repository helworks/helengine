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
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        readonly helengine.editor.EditorSessionInteractionServices InteractionServices;
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

            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            Input = new TestInputBackend();
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
            InteractionServices = GeneratedAssetGraph.InteractionServices;

            CreateUiCamera(640, 480, 0b1000000000000000);
        }

        /// <summary>
        /// Clears shared editor selection state after each test.
        /// </summary>
        public void Dispose() {
            InteractionServices.Selection.ClearSelection();
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures clicking a hierarchy row selects the entity represented by that row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyRow_SelectsTheRowEntity() {
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Selected From Hierarchy"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());

            InteractableComponent rowInteractable = FindHierarchyRowInteractable();

            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Hover);
            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Press);
            rowInteractable.OnCursor(new int2(2, 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Same(selectedEntity, InteractionServices.Selection.SelectedEntity);
        }

        /// <summary>Selection changes the row background while leaving both horizontal separators untouched.</summary>
        [Fact]
        public void ClickingHierarchyRow_ChangesOnlyBackgroundForSelectedEntity() {
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Selected From Hierarchy"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            SceneHierarchyRow row = FindVisibleRow(panel, selectedEntity);
            byte4 originalBackground = row.Background.Color;
            byte4 originalTopBorder = row.TopBorder.Color;
            byte4 originalBottomBorder = row.BottomBorder.Color;
            int2 rowPoint = new int2(48, SceneHierarchyPanel.RowHeight / 2);

            Click(row.Interactable, rowPoint);
            row.Interactable.OnCursor(rowPoint, new int2(0, 0), PointerInteraction.Leave);

            Assert.Same(selectedEntity, InteractionServices.Selection.SelectedEntity);
            Assert.NotEqual(originalBackground, row.Background.Color);
            Assert.Equal(originalTopBorder, row.TopBorder.Color);
            Assert.Equal(originalBottomBorder, row.BottomBorder.Color);
        }

        /// <summary>
        /// Ensures clicking the parent-row arrow collapses and re-expands that branch without removing the parent row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyArrow_CollapsesAndExpandsTheParentBranch() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);

            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));

            Click(parentRow.DisclosureInteractable, new int2(10, SceneHierarchyPanel.RowHeight / 2));

            Assert.Equal(new[] { parent }, GetVisibleRowEntities(panel));

            parentRow = FindVisibleRow(panel, parent);
            Click(parentRow.DisclosureInteractable, new int2(10, SceneHierarchyPanel.RowHeight / 2));

            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));
        }

        /// <summary>Hover fills the disclosure button while keeping its border color unchanged.</summary>
        [Fact]
        public void HoveringHierarchyDisclosure_ChangesBackgroundWithoutChangingBorder() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            SceneHierarchyRow row = FindVisibleRow(panel, parent);
            byte4 originalBorderColor = row.DisclosureBackground.BorderColor;

            row.DisclosureInteractable.OnCursor(
                new int2(10, SceneHierarchyPanel.RowHeight / 2),
                new int2(0, 0),
                PointerInteraction.Hover);

            Assert.Equal(ThemeManager.Colors.AccentSecondary, row.DisclosureBackground.FillColor);
            Assert.Equal(originalBorderColor, row.DisclosureBackground.BorderColor);

            row.DisclosureInteractable.OnCursor(
                new int2(10, SceneHierarchyPanel.RowHeight / 2),
                new int2(0, 0),
                PointerInteraction.Leave);

            Assert.Equal(new byte4(255, 255, 255, 0), row.DisclosureBackground.FillColor);
            Assert.Equal(originalBorderColor, row.DisclosureBackground.BorderColor);
        }

        /// <summary>Hovering an expanded branch highlights its own depth guide until it leaves or collapses.</summary>
        [Fact]
        public void HoveringExpandedDisclosure_HighlightsItsDepthGuideUntilLeaveOrCollapse() {
            EditorEntity root = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Root"
            };
            EditorEntity branch = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Branch"
            };
            EditorEntity grandchild = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Grandchild"
            };
            EditorEntity sibling = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Sibling"
            };
            root.AddChild(branch);
            root.AddChild(sibling);
            branch.AddChild(grandchild);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow branchRow = FindVisibleRow(panel, branch);
            SceneHierarchyRow grandchildRow = FindVisibleRow(panel, grandchild);
            List<(Entity Host, SpriteComponent Sprite)> branchGuides = FindVerticalDepthGuideSprites(grandchildRow.Entity);
            int2 disclosurePoint = new int2(10, SceneHierarchyPanel.RowHeight / 2);

            Assert.Equal(2, branchGuides.Count);
            Assert.Equal(ThemeManager.Colors.SurfaceInput, branchGuides[1].Sprite.Color);
            branchRow.DisclosureInteractable.OnCursor(disclosurePoint, new int2(0, 0), PointerInteraction.Hover);

            Assert.Equal(ThemeManager.Colors.AccentSecondary, branchGuides[1].Sprite.Color);
            Assert.Equal(ThemeManager.Colors.SurfaceInput, branchGuides[0].Sprite.Color);

            branchRow.DisclosureInteractable.OnCursor(disclosurePoint, new int2(0, 0), PointerInteraction.Leave);

            Assert.Equal(ThemeManager.Colors.SurfaceInput, branchGuides[1].Sprite.Color);

            Click(branchRow.DisclosureInteractable, disclosurePoint);

            Assert.Equal(new[] { root, branch, sibling }, GetVisibleRowEntities(panel));
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            Assert.All(rows.SelectMany(row => FindVerticalDepthGuideSprites(row.Entity)), guide =>
                Assert.Equal(ThemeManager.Colors.SurfaceInput, guide.Sprite.Color));
        }

        /// <summary>Visibility controls stay at the row's left edge while disclosure and labels follow hierarchy depth.</summary>
        [Fact]
        public void RefreshHierarchy_KeepsVisibilityButtonAtLeftWhileDisclosureFollowsHierarchyDepth() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);
            SceneHierarchyRow childRow = FindVisibleRow(panel, child);

            Assert.Equal(parentRow.VisibilityHost.Position.X, childRow.VisibilityHost.Position.X);
            Assert.Equal(parentRow.VisibilityHitLeft, childRow.VisibilityHitLeft);
            Assert.Equal(14f, childRow.ArrowHost.Position.X - parentRow.ArrowHost.Position.X);
            Assert.Equal(14, childRow.ArrowHitLeft - parentRow.ArrowHitLeft);
            Assert.Equal(14f, childRow.LabelHost.Position.X - parentRow.LabelHost.Position.X);
        }

        /// <summary>Each visible descendant row draws one vertical guide for every ancestor depth.</summary>
        [Fact]
        public void RefreshHierarchy_DrawsVerticalDepthGuidesWithinEachVisibleBranch() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            EditorEntity grandchild = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Grandchild"
            };
            EditorEntity sibling = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Sibling"
            };
            EditorEntity otherRoot = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Other Root"
            };
            parent.AddChild(child);
            parent.AddChild(sibling);
            child.AddChild(grandchild);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();

            List<(Entity Host, SpriteComponent Sprite)> parentGuides = FindVerticalDepthGuideSprites(FindVisibleRow(panel, parent).Entity);
            List<(Entity Host, SpriteComponent Sprite)> childGuides = FindVerticalDepthGuideSprites(FindVisibleRow(panel, child).Entity);
            List<(Entity Host, SpriteComponent Sprite)> grandchildGuides = FindVerticalDepthGuideSprites(FindVisibleRow(panel, grandchild).Entity);
            List<(Entity Host, SpriteComponent Sprite)> siblingGuides = FindVerticalDepthGuideSprites(FindVisibleRow(panel, sibling).Entity);
            List<(Entity Host, SpriteComponent Sprite)> otherRootGuides = FindVerticalDepthGuideSprites(FindVisibleRow(panel, otherRoot).Entity);

            Assert.Empty(parentGuides);
            Assert.Single(childGuides);
            Assert.Equal(new[] { 38f }, childGuides.Select(guide => guide.Host.Position.X));
            Assert.Equal(new[] { 38f, 52f }, grandchildGuides.Select(guide => guide.Host.Position.X));
            Assert.Single(siblingGuides);
            Assert.Equal(new[] { 38f }, siblingGuides.Select(guide => guide.Host.Position.X));
            Assert.Empty(otherRootGuides);
            Assert.All(childGuides.Concat(grandchildGuides).Concat(siblingGuides), guide => {
                Assert.Equal(1, guide.Sprite.Size.X);
                Assert.Equal(SceneHierarchyPanel.RowHeight, guide.Sprite.Size.Y);
            });
        }

        /// <summary>
        /// Ensures the visibility control hides the entity without disabling, selecting, or collapsing its hierarchy row.
        /// </summary>
        [Fact]
        public void ClickingHierarchyVisibilityIcon_HidesEntityWithoutSelectingOrCollapsingBranch() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();
            InteractionServices.Selection.SetSelectedEntity(child);
            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);

            int2 rowPoint = new int2(18, SceneHierarchyPanel.RowHeight / 2);
            parentRow.Interactable.OnCursor(rowPoint, new int2(0, 0), PointerInteraction.Hover);
            parentRow.Interactable.OnCursor(rowPoint, new int2(0, 0), PointerInteraction.Press);
            parentRow.VisibilityInteractable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Hover);
            parentRow.VisibilityInteractable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Press);
            parentRow.Interactable.OnCursor(rowPoint, new int2(0, 0), PointerInteraction.Release);
            parentRow.VisibilityInteractable.OnCursor(new int2(10, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Release);

            Assert.True(parent.Hidden);
            Assert.True(parent.Enabled);
            Assert.True(parent.IsHierarchyEnabled);
            Assert.Same(child, InteractionServices.Selection.SelectedEntity);
            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));
        }

        /// <summary>
        /// Ensures clicking the row body selects the parent entity without collapsing its visible child branch.
        /// </summary>
        [Fact]
        public void ClickingHierarchyRowBody_SelectsWithoutCollapsingTheBranch() {
            EditorEntity parent = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Parent"
            };
            EditorEntity child = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Child"
            };
            parent.AddChild(child);

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.RefreshHierarchy();

            SceneHierarchyRow parentRow = FindVisibleRow(panel, parent);

            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Hover);
            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Press);
            parentRow.Interactable.OnCursor(new int2(48, SceneHierarchyPanel.RowHeight / 2), new int2(0, 0), PointerInteraction.Release);

            Assert.Same(parent, InteractionServices.Selection.SelectedEntity);
            Assert.Equal(new[] { parent, child }, GetVisibleRowEntities(panel));
        }

        /// <summary>
        /// Ensures activating the Reparent context-menu entry raises a reparent request for the clicked row entity.
        /// </summary>
        [Fact]
        public void RightClickingHierarchyRow_ReparentMenuItem_RaisesReparentRequested() {
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Selected From Hierarchy"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(32, 40, 0);
            panel.Size = new int2(320, 240);

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
            Assert.Same(selectedEntity, InteractionServices.Selection.SelectedEntity);
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
        /// Ensures scaled dock metrics move the hierarchy content root below the scaled title bar and enlarge row chrome.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_WithScaledMetrics_UsesScaledTitleBarOffsetAndRowHeight() {
            EditorEntity selectedEntity = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Scaled Hierarchy Entity"
            };
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            SceneHierarchyPanel panel = CreatePanel(CreateFont(), metrics);
            panel.Size = new int2(320, 240);

            panel.RefreshHierarchy();

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            SceneHierarchyRow row = FindVisibleRow(panel, selectedEntity);

            Assert.Equal(30f, contentRoot.Position.Y);
            Assert.Equal(new int2(330, 33), row.Background.Size);
            Assert.Equal(new int2(330, 33), row.Interactable.Size);
        }

        /// <summary>
        /// Ensures the hierarchy only enables rows that fit inside the current panel viewport.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_WhenSceneContainsMoreRowsThanViewport_OnlyEnablesVisibleRows() {
            for (int entityIndex = 0; entityIndex < 20; entityIndex++) {
                new EditorEntity(CoreValue, InteractionServices);
            }

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            EditorEntity contentRoot = GetPrivateField<EditorEntity>(panel, "contentRoot");
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            int enabledRowCount = 0;
            SceneHierarchyRow lastVisibleRow = null;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow row = rows[rowIndex];
                if (!row.Entity.Enabled || row.NodeEntity == null) {
                    continue;
                }

                enabledRowCount++;
                lastVisibleRow = row;
            }

            Assert.Equal(8, enabledRowCount);
            Assert.NotNull(lastVisibleRow);
            Assert.True(lastVisibleRow.Entity.Position.Y + SceneHierarchyPanel.RowHeight <= contentRoot.Position.Y + panel.Size.Y);
        }

        /// <summary>
        /// Ensures Scene Hierarchy row visuals render on the dedicated hierarchy content layer instead of the shared editor UI layer.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_AssignsVisibleRowsToTheHierarchyContentLayer() {
            EditorEntity entity = new EditorEntity(CoreValue, InteractionServices) {
                Name = "Layered Hierarchy Entity"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(24f, 32f, 0f);
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            SceneHierarchyRow row = null;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                SceneHierarchyRow candidate = rows[rowIndex];
                if (candidate.Entity.Enabled && ReferenceEquals(candidate.NodeEntity, entity)) {
                    row = candidate;
                    break;
                }
            }

            Assert.NotNull(row);
            Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.Entity.LayerMask);
            Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.ArrowHost.LayerMask);
            Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.LabelHost.LayerMask);
        }

        /// <summary>Ensures the scrollbar shares the clipped content camera and renders after the row separators, including after scrolling.</summary>
        /// <param name="scrollOffset">Row offset at which to inspect the rendered hierarchy.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void RefreshHierarchy_RendersScrollBarAboveRowSeparators(int scrollOffset) {
            for (int index = 0; index < 20; index++) {
                new EditorEntity(CoreValue, InteractionServices) {
                    Name = $"Hierarchy {index}"
                };
            }

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Size = new int2(320, 176);
            panel.RefreshHierarchy();
            ScrollComponent scroll = GetPrivateField<EditorScrollComponent>(panel, "scrollComponent");
            scroll.ScrollTo(scrollOffset);
            Assert.True(scroll.ScrollBar.IsVisible);

            Entity visuals = scroll.ScrollBar.Parent.Children[0];
            RoundedRectComponent track = Assert.Single(visuals.Components.OfType<RoundedRectComponent>());
            RoundedRectComponent thumb = Assert.Single(visuals.Children[0].Components.OfType<RoundedRectComponent>());
            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");
            RenderList2D queue = Assert.IsType<RenderList2D>(contentCamera.RenderQueue2D);
            List<IDrawable2D> drawables = Enumerable.Range(0, queue.Count).Select(index => queue[index]).ToList();
            Assert.Contains(track, drawables);
            Assert.Contains(thumb, drawables);
            Assert.Equal(contentCamera.LayerMask, track.Parent.LayerMask);
            Assert.Equal(contentCamera.LayerMask, thumb.Parent.LayerMask);

            int trackIndex = drawables.IndexOf(track);
            int thumbIndex = drawables.IndexOf(thumb);
            Assert.True(trackIndex < thumbIndex);
            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            Assert.Contains(rows, row => row.BottomBorderEntity.IsHierarchyEnabled);
            foreach (SceneHierarchyRow row in rows.Where(row => row.Entity.IsHierarchyEnabled)) {
                Assert.Contains(row.TopBorder, drawables);
                Assert.True(drawables.IndexOf(row.TopBorder) < trackIndex);
                if (row.BottomBorderEntity.IsHierarchyEnabled) {
                    Assert.Contains(row.BottomBorder, drawables);
                    Assert.True(drawables.IndexOf(row.BottomBorder) < trackIndex);
                }
            }
        }

        /// <summary>
        /// Ensures the Scene Hierarchy content camera viewport matches the panel body below the title bar.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_ConfiguresContentCameraViewportToMatchPanelBody() {
            new EditorEntity(CoreValue, InteractionServices) {
                Name = "Viewport Hierarchy Entity"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(40f, 64f, 0f);
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

            Assert.Equal(40f, contentCamera.Viewport.X);
            Assert.Equal(64f + DockableEntity.TitleBarHeight, contentCamera.Viewport.Y);
            Assert.Equal(320f, contentCamera.Viewport.Z);
            Assert.Equal(176f, contentCamera.Viewport.W);
        }

        /// <summary>
        /// Ensures the Scene Hierarchy content camera renders below the shared modal UI camera tier.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_UsesPanelContentCameraTierBelowModalUiTier() {
            new EditorEntity(CoreValue, InteractionServices) {
                Name = "Hierarchy Camera Tier Entity"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(40f, 64f, 0f);
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

            Assert.True(contentCamera.CameraDrawOrder < EditorUiCameraDrawOrders.ModalUi);
            Assert.Equal(EditorUiCameraDrawOrders.PanelContent, contentCamera.CameraDrawOrder);
        }

        /// <summary>
        /// Ensures modal UI rendering always uses a later camera tier than Scene Hierarchy content.
        /// </summary>
        [Fact]
        public void RefreshHierarchy_WhenModalUiTierIsCompared_RendersBelowModalDialogs() {
            new EditorEntity(CoreValue, InteractionServices) {
                Name = "Modal Comparison Entity"
            };
            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(24f, 32f, 0f);
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

            Assert.True(EditorUiCameraDrawOrders.ModalUi > contentCamera.CameraDrawOrder);
        }

        /// <summary>
        /// Ensures rows outside the visible Scene Hierarchy viewport are not hit by pointer resolution.
        /// </summary>
        [Fact]
        public void UpdateContextMenuInput_WhenPointerTargetsClippedOverflow_DoesNotResolveAHiddenRow() {
            for (int entityIndex = 0; entityIndex < 20; entityIndex++) {
                new EditorEntity(CoreValue, InteractionServices) {
                    Name = $"Hierarchy {entityIndex}"
                };
            }

            SceneHierarchyPanel panel = CreatePanel(CreateFont());
            panel.Position = new float3(32f, 40f, 0f);
            panel.Size = new int2(320, 176);

            panel.RefreshHierarchy();

            bool resolved = TryGetRowAtScreenPoint(
                panel,
                new int2(48, 40 + DockableEntity.TitleBarHeight + 220),
                out SceneHierarchyRow row);

            Assert.False(resolved);
            Assert.Null(row);
        }

        SceneHierarchyPanel CreatePanel(FontAsset font) {
            return CreatePanel(font, EditorUiMetrics.Default);
        }

        SceneHierarchyPanel CreatePanel(FontAsset font, EditorUiMetrics metrics) {
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CoreValue, InteractionServices, font, metrics);
            panel.SetObjectManager(CoreValue.ObjectManager);
            panel.SetInput(CoreValue.Input);
            panel.RefreshHierarchy();
            return panel;
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
            List<IInteractable2D> interactables = CoreValue.ObjectManager.Interactables;
            for (int interactableIndex = 0; interactableIndex < interactables.Count; interactableIndex++) {
                if (interactables[interactableIndex] is InteractableComponent interactable &&
                    interactable.Size.Y == SceneHierarchyPanel.RowHeight) {
                    return interactable;
                }
            }

            throw new InvalidOperationException("Expected the hierarchy panel to register a row interactable.");
        }

        /// <summary>Dispatches one complete click sequence to an individual hierarchy button.</summary>
        /// <param name="interactable">Button receiving the click.</param>
        /// <param name="point">Pointer position in button-local coordinates.</param>
        static void Click(InteractableComponent interactable, int2 point) {
            interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Hover);
            interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Press);
            interactable.OnCursor(point, new int2(0, 0), PointerInteraction.Release);
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

        /// <summary>Finds one-pixel-wide row-height sprites used as vertical hierarchy depth guides.</summary>
        static List<(Entity Host, SpriteComponent Sprite)> FindVerticalDepthGuideSprites(Entity rowEntity) {
            List<(Entity Host, SpriteComponent Sprite)> guides = new List<(Entity Host, SpriteComponent Sprite)>();
            CollectVerticalDepthGuideSprites(rowEntity, guides);
            return guides;
        }

        /// <summary>Collects vertical hierarchy guide sprites from one row's entity subtree.</summary>
        static void CollectVerticalDepthGuideSprites(Entity entity, List<(Entity Host, SpriteComponent Sprite)> guides) {
            if (entity.Components != null) {
                for (int componentIndex = 0; componentIndex < entity.Components.Count; componentIndex++) {
                    if (entity.Components[componentIndex] is SpriteComponent sprite &&
                        sprite.Size.X == 1 && sprite.Size.Y == SceneHierarchyPanel.RowHeight) {
                        guides.Add((entity, sprite));
                    }
                }
            }

            if (entity.Children == null) {
                return;
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                CollectVerticalDepthGuideSprites(entity.Children[childIndex], guides);
            }
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
            CoreValue.Update();
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
        /// Invokes the private row-hit helper using reflection and returns the resolved row state.
        /// </summary>
        /// <param name="panel">Panel that owns the private hit-test helper.</param>
        /// <param name="pointer">Screen-space pointer coordinate to test.</param>
        /// <param name="row">Resolved row when one was found.</param>
        /// <returns>True when the private helper resolved a visible row.</returns>
        bool TryGetRowAtScreenPoint(SceneHierarchyPanel panel, int2 pointer, out SceneHierarchyRow row) {
            MethodInfo method = typeof(SceneHierarchyPanel).GetMethod("TryGetRowAtScreenPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = new object[] {
                pointer,
                null
            };
            bool resolved = (bool)method.Invoke(panel, arguments);
            row = (SceneHierarchyRow)arguments[1];
            return resolved;
        }

        /// <summary>
        /// Creates the UI camera used to route hierarchy and context-menu hit testing.
        /// </summary>
        /// <param name="width">Viewport width.</param>
        /// <param name="height">Viewport height.</param>
        /// <param name="layerMask">Layer mask rendered by the camera.</param>
        void CreateUiCamera(int width, int height, ushort layerMask) {
            EditorEntity cameraEntity = new EditorEntity(CoreValue, InteractionServices) {
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

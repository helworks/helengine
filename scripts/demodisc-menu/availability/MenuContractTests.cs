namespace DemoDisc.MenuContractTests {
    /// <summary>Checks that packaging availability cannot alter the authored menu structure or its navigation geometry.</summary>
    public sealed class MenuContractTests {
        /// <summary>Retains every row, action, link and scroll position through readiness, selection wrapping and rebinding.</summary>
        /// <param name="catalogKind">One of the four supported catalog configurations.</param>
        [Theory]
        [InlineData("cube-only")]
        [InlineData("empty")]
        [InlineData("full")]
        [InlineData("null")]
        public void AuthoredMenuIsIdenticalAcrossCatalogs(string catalogKind) {
            using MenuContractFixture fixture = new MenuContractFixture(catalogKind);
            Assert.True(fixture.Menu.IsInitialized);
            Assert.Equal("main", fixture.Menu.ActivePanelId);
            Assert.Equal("main-scenes", fixture.Menu.SelectedItemId);
            Assert.Equal(7, fixture.Panels.Count);
            Assert.Equal(44, fixture.Panels.Values.Sum(panel => panel.Items.Length));
            AssertBoundMetadata(fixture);
            foreach (MenuPanelDefinition definition in fixture.Definition.Panels) {
                fixture.Invoke("ActivatePanel", definition.PanelId, false);
                Assert.Equal(definition.Items[0].ItemId, fixture.Menu.SelectedItemId);
                for (int index = 1; index < definition.Items.Length; index++) {
                    fixture.Invoke("MoveSelection", 1);
                    Assert.Equal(definition.Items[index].ItemId, fixture.Menu.SelectedItemId);
                }
                MenuPanelRuntime panel = fixture.Panels[definition.PanelId];
                int expectedOffset = Math.Max(0, definition.Items.Length - definition.VisibleItemCount);
                Assert.Equal(expectedOffset, panel.ItemsScrollComponent.ScrollOffset);
                Assert.Equal(-expectedOffset * MenuContractFixture.RowStep, panel.ItemsRootEntity.LocalPosition.Y);
                fixture.Invoke("MoveSelection", 1);
                Assert.Equal(definition.Items[0].ItemId, fixture.Menu.SelectedItemId);
                Assert.Equal(0, panel.ItemsScrollComponent.ScrollOffset);
            }
            fixture.Invoke("ActivatePanel", "main", false);
            fixture.Invoke("ConfirmSelection");
            Assert.Equal("scene-select", fixture.Menu.ActivePanelId);
            fixture.Invoke("ActivatePanel", "ray-tracing-select", true);
            Assert.Equal(5, fixture.Panels["ray-tracing-select"].Items.Length);
            fixture.Invoke("MoveSelection", -1);
            fixture.Invoke("ConfirmSelection");
            Assert.Equal("scene-select", fixture.Menu.ActivePanelId);
            fixture.Invoke("BindPanels", fixture.Root);
            fixture.Invoke("ActivatePanel", "main", false);
            fixture.Menu.ScenePrepared(fixture.Root);
            AssertBoundMetadata(fixture);
            Assert.Equal("main-scenes", fixture.Menu.SelectedItemId);
        }

        /// <summary>Confirms the packaged Cube action still enters the existing deferred scene transition path.</summary>
        [Fact]
        public void PackagedCubeActionRequestsNormalTransition() {
            using MenuContractFixture fixture = new MenuContractFixture("cube-only");
            fixture.Invoke("ActivatePanel", "scene-select", false);
            Assert.Equal("scene-cube-test", fixture.Menu.SelectedItemId);
            fixture.Invoke("ConfirmSelection");
            Assert.True(fixture.Core.SceneManager.IsSceneTransitionActive);
            Assert.Equal("cube_test", fixture.Core.SceneManager.TransitionTargetSceneId);
            Assert.Equal(0f, fixture.Core.SceneManager.SceneTransitionProgress);
        }

        /// <summary>Rebinding disposes runtime records without disposing their borrowed entities, components or labels.</summary>
        [Fact]
        public void RebindAndTeardownPreserveBorrowedOwnerLifetimes() {
            using MenuContractFixture fixture = new MenuContractFixture("empty");
            MenuPanelRuntime previousPanel = fixture.Panels["scene-select"];
            Entity row = previousPanel.Items[1].OwnerEntity;
            TextComponent label = (TextComponent)row.Children[0].Components[0];
            fixture.Invoke("BindPanels", fixture.Root);
            fixture.Invoke("ActivatePanel", "main", false);
            Assert.NotSame(previousPanel, fixture.Panels["scene-select"]);
            Assert.False(row.IsDisposed);
            Assert.False(IsComponentDisposed(label));
            Assert.Equal("Colored Cubes", label.Text);
            fixture.Root.Dispose();
            Assert.True(row.IsDisposed);
            Assert.True(IsComponentDisposed(label));
            fixture.Root.Dispose();
        }

        /// <summary>Reads the base lifetime flag because component disposal is intentionally not a public menu API.</summary>
        /// <param name="component">Borrowed label whose entity retains disposal ownership.</param>
        /// <returns>Whether the real component disposal lifecycle has executed.</returns>
        static bool IsComponentDisposed(Component component) {
            return (bool)typeof(Component).GetField("IsDisposedValue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
        }

        /// <summary>Compares every bound runtime field to original metadata rather than duplicating the menu ids in the test.</summary>
        /// <param name="fixture">Prepared component with its original provider definition.</param>
        static void AssertBoundMetadata(MenuContractFixture fixture) {
            foreach (MenuPanelDefinition expected in fixture.Definition.Panels) {
                MenuPanelRuntime panel = fixture.Panels[expected.PanelId];
                Assert.Equal(expected.Items.Length, panel.Items.Length);
                Assert.Equal(expected.Items.Length, panel.ItemsScrollComponent.ItemCount);
                Assert.Equal(expected.VisibleItemCount, panel.ItemsScrollComponent.VisibleItemCount);
                Assert.Same(panel.ItemsRootEntity.Parent, panel.ItemsScrollComponent.ClipOriginEntity);
                for (int index = 0; index < expected.Items.Length; index++) {
                    MenuItemDefinition item = expected.Items[index];
                    MenuItemRuntime bound = panel.Items[index];
                    Assert.Equal(index, bound.Index);
                    Assert.Equal(item.ItemId, bound.Definition.ItemId);
                    Assert.Equal(item.Action.Kind, bound.Definition.ActionKind);
                    Assert.Equal(item.Action.TargetId, bound.Definition.TargetId);
                    Assert.Equal(index * MenuContractFixture.RowStep, bound.OwnerEntity.LocalPosition.Y);
                    Assert.True(bound.OwnerEntity.Enabled);
                    Assert.Equal(new int2(100, 20), bound.Background.Size);
                    Assert.Equal(item.Label, ((TextComponent)bound.OwnerEntity.Children[0].Components[0]).Text);
                }
            }
        }
    }
}

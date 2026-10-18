using helengine.baseplatform.Builders;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>Checks the detached cook pass against concrete layout results and owner boundaries.</summary>
    public sealed class ViewportLayoutCookTests {
        /// <summary>Controls whose visuals do not exist before initialization must not silently ship unprepared geometry.</summary>
        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, false)]
        public void GeneratedControl_RequiresSupportedProcessing(int controlKind, bool cook) {
            string controlType = controlKind == 0 ? "helengine.CheckBoxComponent" : controlKind == 1 ? "helengine.TextBoxComponent" : "helengine.ComboBoxComponent";
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset root = fixture.CreateEntity("Menu", new ViewportComponent {
                ScalingMode = ViewportComponent.ReferenceCanvasStretchScalingMode,
                LayoutProcessingMode = cook ? ViewportLayoutProcessingMode.Cook : ViewportLayoutProcessingMode.Dynamic
            });
            root.Children = [new SceneEntityAsset { Name = "GeneratedControl", Components = [new SceneComponentAssetRecord { ComponentTypeId = controlType }] }];
            ViewportLayoutCookProcessor processor = new ViewportLayoutCookProcessor(fixture.Resolve);
            SceneAsset scene = new SceneAsset { RootEntities = [root] };
            PlatformLayoutTarget target = new PlatformLayoutTarget(320, 240, 1, false, 0, 1);
            if (cook) {
                InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => processor.Process(scene, target));
                Assert.Contains("Menu", error.Message);
                Assert.Contains(controlType, error.Message);
                Assert.Contains("OnLoad", error.Message);
            } else { processor.Process(scene, target); }
            Assert.Equal(0, fixture.GameplayResolveCount);
        }

        /// <summary>Cooked anchors cannot borrow virtual bounds from a Dynamic parent with unknown density.</summary>
        [Theory]
        [InlineData(LayoutComponent.ParentLayoutRectSpace)]
        [InlineData(LayoutComponent.InheritedLayoutSpace)]
        public void Cook_RejectsUnresolvedParentAnchorBounds(byte layoutSpace) {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset outer = fixture.CreateEntity("UnknownDensity", new ViewportComponent { ScalingMode = ViewportComponent.DensityScalingMode });
            outer.Components = outer.Components.Concat(fixture.CreateEntity("ParentRect", new RoundedRectComponent { Size = new int2(400, 200) }).Components)
                .Concat(fixture.CreateEntity("ParentLayout", new LayoutComponent()).Components).ToArray();
            SceneEntityAsset inner = fixture.CreateEntity("CookedFit", new ViewportComponent {
                BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(320, 240),
                LayoutProcessingMode = ViewportLayoutProcessingMode.Cook
            });
            inner.Components = inner.Components.Concat(fixture.CreateEntity("Fit", new ReferenceCanvasFitComponent()).Components)
                .Concat(fixture.CreateEntity("Anchors", new LayoutComponent {
                    LayoutSpace = layoutSpace, AnchorFlags = LayoutComponent.LeftAnchorFlag | LayoutComponent.RightAnchorFlag
                }).Components).ToArray();
            outer.Children = [inner];
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [outer] },
                    new PlatformLayoutTarget(640, 480, 1, false, 0, 1)));
            Assert.Contains("CookedFit", error.Message);
            Assert.Contains("parent layout bounds", error.Message);
        }

        /// <summary>Known density and user scaling are baked using physical units without retaining originals.</summary>
        [Fact]
        public void Cook_DensityUsesEffectiveDpiAndUiScale() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ViewportComponent viewport = new ViewportComponent {
                ScalingMode = ViewportComponent.DensityScalingMode, PixelsPerCentimeter = (float)(96d / 2.54d),
                UiScale = 0.8f, LayoutProcessingMode = ViewportLayoutProcessingMode.Cook
            };
            SceneEntityAsset root = fixture.CreateEntity("Density", viewport);
            TextComponent text = new TextComponent { Size = new int2(120, 30), FontScale = 1.5f };
            SceneEntityAsset child = fixture.CreateEntity("Text", text);
            child.LocalPosition = new float3(30, 20, 0);
            root.Children = [child];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                new PlatformLayoutTarget(1280, 720, 1, true, 192, 1.25));
            Assert.Equal(new int2(240, 60), text.Size);
            Assert.Equal(3f, text.FontScale);
            Assert.Equal(new float3(60, 40, 0), child.LocalPosition);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Cooked fields match the numerical results already pinned for load and dynamic processing.</summary>
        [Theory]
        [InlineData(2, 320, 240, 1, 128, 32, 96, 30)]
        [InlineData(1, 320, 240, 1, 128, 24, 96, 22.5)]
        public void Cook_EqualsOnLoadAndDynamic(byte scaling, int width, int height, double aspect, int expectedWidth, int expectedHeight, float x, float y) {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ViewportComponent viewport = new ViewportComponent { ReferenceWidth = 1280, ReferenceHeight = 720,
                ScalingMode = scaling, LayoutProcessingMode = ViewportLayoutProcessingMode.Cook };
            SceneEntityAsset root = fixture.CreateEntity("MenuRoot", viewport);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(512, 96) };
            SceneEntityAsset child = fixture.CreateEntity("Panel", rectangle);
            child.LocalPosition = new float3(384, 90, 0);
            root.Children = [child];
            SceneAsset scene = new SceneAsset { RootEntities = [root] };
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(scene, new PlatformLayoutTarget(width, height, aspect, false, 0, 1));
            Assert.Equal(new int2(expectedWidth, expectedHeight), rectangle.Size);
            Assert.Equal(new float3(x, y, 0), child.LocalPosition);
            Assert.Equal(1, viewport.CookedLayout.AlgorithmVersion);
            Assert.Equal(width, viewport.CookedLayout.TargetWidth);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Targets and density failures identify the owner and the unresolved parameter.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnknownTargetOrDpi_IdentifiesViewport(bool density) {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ViewportComponent viewport = new ViewportComponent {
                LayoutProcessingMode = ViewportLayoutProcessingMode.Cook,
                ScalingMode = density ? ViewportComponent.DensityScalingMode : ViewportComponent.ReferenceCanvasScalingMode
            };
            SceneEntityAsset root = fixture.CreateEntity("UnresolvedMenu", viewport);
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                    density ? new PlatformLayoutTarget(320, 240, 1, false, 0, 1) : null));
            Assert.Contains("UnresolvedMenu", error.Message);
            Assert.Contains(density ? "DPI" : "target", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Cook stops at a separately scaled owner even when its policy is resolved at load time.</summary>
        [Fact]
        public void NestedOwners_ProcessOnce() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ViewportComponent outer = new ViewportComponent { LayoutProcessingMode = ViewportLayoutProcessingMode.Cook, ScalingMode = 2 };
            ViewportComponent inner = new ViewportComponent { LayoutProcessingMode = ViewportLayoutProcessingMode.OnLoad, ScalingMode = 2 };
            SceneEntityAsset root = fixture.CreateEntity("Outer", outer);
            SceneEntityAsset nested = fixture.CreateEntity("Inner", inner);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            nested.Children = [fixture.CreateEntity("Unbaked", rectangle)];
            root.Children = [nested];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] }, new PlatformLayoutTarget(320, 240, 1, false, 0, 1));
            Assert.Equal(new int2(100, 40), rectangle.Size);
            Assert.NotNull(outer.CookedLayout);
            Assert.Null(inner.CookedLayout);
        }

        /// <summary>Gameplay records remain untouched; layout processing needs no running Core or lifecycle callbacks.</summary>
        [Fact]
        public void Cook_DoesNotResolveGameplay() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset root = fixture.CreateEntity("Menu", new ViewportComponent { LayoutProcessingMode = ViewportLayoutProcessingMode.Cook, ScalingMode = 2 });
            SceneComponentAssetRecord gameplay = new SceneComponentAssetRecord { ComponentTypeId = "gameplay.MenuController", Payload = [13, 37] };
            root.Children = [new SceneEntityAsset { Components = [gameplay] }];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] }, new PlatformLayoutTarget(320, 240, 1, false, 0, 1));
            Assert.Equal(0, fixture.GameplayResolveCount);
            Assert.Equal(new byte[] { 13, 37 }, gameplay.Payload);
        }
    }
}

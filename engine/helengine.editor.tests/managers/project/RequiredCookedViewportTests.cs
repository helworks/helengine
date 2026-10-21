using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;

namespace helengine.editor.tests.managers.project {
    /// <summary>Verifies platform-mandated cooking independently of authoring policies.</summary>
    public sealed class RequiredCookedViewportTests {
        /// <summary>The same typed policy selects the player source and participates in serialized platform cache identity.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeContract_SelectsCookedSourceOnlyWhenRequired(bool required) {
            RuntimeGenerationContract contract = new RuntimeGenerationContract(RuntimeMaterialResolutionMode.RawShaderBacked,
                true, PackagedPathPolicy.ContentRelativeOnly, null, false, required);
            PlatformDefinition platform = new PlatformDefinition("test", "Test", [], [], [], [], [], [], [], [], contract);
            IReadOnlyList<string> symbols = EditorPlatformPreprocessorSymbolService.ResolvePortableInputSymbols(platform);
            Assert.Equal(required, symbols.Contains("HELENGINE_RUNTIME_COOKED_VIEWPORT_LAYOUT"));
            Assert.Contains("\"RequiresCookedViewportLayout\":" + (required ? "true" : "false"), System.Text.Json.JsonSerializer.Serialize(platform));
            Assert.False(RuntimeGenerationContract.CreateDefault().RequiresCookedViewportLayout);
        }
        /// <summary>Both outer and nested owners must be cooked, with each subtree transformed exactly once.</summary>
        [Fact]
        public void RequiredCook_PreparesEveryViewport() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ViewportComponent outer = new ViewportComponent { ScalingMode = 2 };
            ViewportComponent inner = new ViewportComponent {
                ScalingMode = 2, LayoutProcessingMode = ViewportLayoutProcessingMode.OnLoad,
                BindingMode = ViewportComponent.FixedBindingMode, FixedSize = new int2(640, 360)
            };
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            SceneEntityAsset root = fixture.CreateEntity("Outer", outer);
            SceneEntityAsset child = fixture.CreateEntity("Inner", inner);
            child.Children = [fixture.CreateEntity("Panel", rectangle)];
            root.Children = [child];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                new PlatformLayoutTarget(320, 240, 1, false, 0, 1), true);
            Assert.Equal(ViewportLayoutProcessingMode.Cook, outer.LayoutProcessingMode);
            Assert.Equal(ViewportLayoutProcessingMode.Cook, inner.LayoutProcessingMode);
            Assert.Equal(320, outer.CookedLayout.TargetWidth);
            Assert.Equal(640, inner.CookedLayout.TargetWidth);
            Assert.Equal(new int2(50, 20), rectangle.Size);
        }

        /// <summary>An older standalone fit cannot pull the dynamic originals cache into a Cook-only player.</summary>
        [Fact]
        public void RequiredCook_PreparesStandaloneFit() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 640, ReferenceHeight = 480 };
            SceneEntityAsset root = fixture.CreateEntity("Fit", fit);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            root.Children = [fixture.CreateEntity("Panel", rectangle)];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                new PlatformLayoutTarget(320, 240, 1, false, 0, 1), true);
            Assert.NotNull(fit.CookedLayout);
            Assert.Equal(new int2(50, 20), rectangle.Size);
        }

        /// <summary>Mandatory cooking must report missing platform metrics rather than silently retaining Dynamic.</summary>
        [Fact]
        public void RequiredCook_RejectsMissingTarget() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset root = fixture.CreateEntity("MissingTarget", new ViewportComponent());
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] }, null, true));
            Assert.Contains("MissingTarget", error.Message);
        }

        /// <summary>An ancestor-camera viewport must cook for that camera's subrectangle rather than the full screen.</summary>
        [Fact]
        public void RequiredCook_ResolvesAncestorCameraRectangle() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset root = fixture.CreateEntity("Camera", new CameraComponent { Viewport = new float4(0.5f, 0f, 0.5f, 1f) });
            ViewportComponent viewport = new ViewportComponent { BindingMode = ViewportComponent.AncestorCameraBindingMode, ScalingMode = 2 };
            SceneEntityAsset child = fixture.CreateEntity("CameraUI", viewport);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(1280, 720) };
            child.Children = [fixture.CreateEntity("Panel", rectangle)];
            root.Children = [child];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                new PlatformLayoutTarget(320, 240, 1, false, 0, 1), true);
            Assert.Equal(160, viewport.CookedLayout.TargetWidth);
            Assert.Equal(240, viewport.CookedLayout.TargetHeight);
            Assert.Equal(new int2(160, 240), rectangle.Size);
        }

        /// <summary>A standalone fit preserves its established full-camera-surface semantics, unlike a camera-bound viewport.</summary>
        [Fact]
        public void RequiredCook_StandaloneFitUsesFullCameraSurface() {
            ViewportLayoutCookFixture fixture = new ViewportLayoutCookFixture();
            SceneEntityAsset root = fixture.CreateEntity("Camera", new CameraComponent { Viewport = new float4(0f, 0f, 0.5f, 1f) });
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 640, ReferenceHeight = 480 };
            SceneEntityAsset child = fixture.CreateEntity("Fit", fit);
            root.Children = [child];
            new ViewportLayoutCookProcessor(fixture.Resolve).Process(new SceneAsset { RootEntities = [root] },
                new PlatformLayoutTarget(640, 480, 1, false, 0, 1), true);
            Assert.Equal(640, fit.CookedLayout.TargetWidth);
            Assert.Equal(480, fit.CookedLayout.TargetHeight);
            Assert.Equal(1d, fit.CookedLayout.ScaleX);
        }
    }
}

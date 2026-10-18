namespace helengine.core.tests.components {
    /// <summary>Exercises layout policy paths and cold hierarchy mutation against the real runtime.</summary>
    public sealed class ViewportLayoutProcessingTests {
        /// <summary>Generated scrollbars use resolved scroll bounds and never add original-value records.</summary>
        [Theory]
        [InlineData(ViewportLayoutProcessingMode.Dynamic)]
        [InlineData(ViewportLayoutProcessingMode.OnLoad)]
        [InlineData(ViewportLayoutProcessingMode.Cook)]
        public void GeneratedScrollbars_DoNotCaptureOriginals(ViewportLayoutProcessingMode mode) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ScrollComponent scroll = new ScrollComponent {
                Size = mode == ViewportLayoutProcessingMode.Cook ? new int2(80, 50) : new int2(160, 100),
                ItemCount = 24, ItemExtent = mode == ViewportLayoutProcessingMode.Cook ? 5 : 10
            };
            root.AddComponent(scroll);
            ViewportComponent viewport = scene.AddViewport(root, mode);
            if (mode == ViewportLayoutProcessingMode.Cook) {
                viewport.CookedLayout = new ViewportCookedLayout {
                    AlgorithmVersion = 1, TargetWidth = 320, TargetHeight = 240, PixelAspect = 1,
                    ScaleX = 0.5, ScaleY = 0.5, GlyphScale = 0.5, AnchorSize = new int2(320, 240)
                };
            }
            root.InitializeHierarchy();
            viewport.Update();
            Assert.Equal(new int2(80, 50), scroll.Size);
            Assert.NotNull(scroll.ScrollBar);
            Assert.Equal(new int2(8, 50), scroll.ScrollBar.Size);
            Assert.Equal(mode == ViewportLayoutProcessingMode.Dynamic ? 1 : 0, viewport.RetainedLayoutPositionCount);
            scroll.ShowScrollBar = false;
            scroll.ShowScrollBar = true;
            viewport.Update();
            Assert.Equal(new int2(8, 50), scroll.ScrollBar.Size);
            Assert.Equal(mode == ViewportLayoutProcessingMode.Dynamic ? 1 : 0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Single-pass preparation allocates no cache, including a temporary cache discarded before observation.</summary>
        [Theory]
        [InlineData(ViewportLayoutProcessingMode.OnLoad)]
        [InlineData(ViewportLayoutProcessingMode.Cook)]
        public void CookAndOnLoad_PrepareWithoutAnyAllocation(ViewportLayoutProcessingMode mode) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            root.AddComponent(new RoundedRectComponent { Size = new int2(100, 40) });
            ViewportComponent viewport = scene.AddViewport(root, mode);
            if (mode == ViewportLayoutProcessingMode.Cook) {
                viewport.CookedLayout = new ViewportCookedLayout {
                    AlgorithmVersion = 1, TargetWidth = 320, TargetHeight = 240, PixelAspect = 1,
                    ScaleX = 0.5, ScaleY = 0.5, GlyphScale = 0.5, AnchorSize = new int2(320, 240)
                };
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            viewport.Update();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Independent nested policies cannot scale each other's participants.</summary>
        [Fact]
        public void MixedNestedPolicies_RespectBoundary() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity outer = scene.CreateEntity();
            ViewportComponent dynamic = scene.AddViewport(outer, ViewportLayoutProcessingMode.Dynamic);
            Entity inner = scene.CreateEntity();
            ViewportComponent once = scene.AddViewport(inner, ViewportLayoutProcessingMode.OnLoad);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            inner.AddComponent(rectangle);
            outer.AddChild(inner);
            dynamic.Update();
            once.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            dynamic.Update();
            once.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
        }

        /// <summary>Policy changes cannot reinterpret already resolved values as authoring input.</summary>
        [Fact]
        public void PolicyChangeAfterPreparation_RequiresReload() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            ViewportComponent viewport = scene.AddViewport(scene.CreateEntity(), ViewportLayoutProcessingMode.OnLoad);
            viewport.Update();
            Assert.Throws<InvalidOperationException>(() => viewport.LayoutProcessingMode = ViewportLayoutProcessingMode.Dynamic);
        }
        /// <summary>Single-load bounds remain fixed so anchored elements cannot resize through provider callbacks.</summary>
        [Fact]
        public void OnLoad_AnchoredGeometryRemainsPreparedAfterResize() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.OnLoad);
            Entity child = scene.CreateEntity();
            child.LocalPosition = new float3(100, 40, 0);
            child.AddComponent(new RoundedRectComponent { Size = new int2(100, 40) });
            LayoutComponent anchors = new LayoutComponent();
            child.AddComponent(anchors);
            root.AddChild(child);
            anchors.EnableAnchoring(right: true, bottom: true);
            viewport.Update();
            float3 prepared = child.LocalPosition;
            scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            viewport.Update();
            Assert.Equal(prepared, child.LocalPosition);
        }

        /// <summary>A newly initialized authored subtree is prepared once without rescaling existing siblings.</summary>
        [Fact]
        public void NewRuntimeSubtree_AppliesOnlyOnce() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            RoundedRectComponent existing = new RoundedRectComponent { Size = new int2(100, 40) };
            root.AddComponent(existing);
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.OnLoad);
            root.InitializeHierarchy();
            viewport.Update();
            Entity child = scene.CreateEntity();
            RoundedRectComponent added = new RoundedRectComponent { Size = new int2(200, 80) };
            child.AddComponent(added);
            root.AddChild(child);
            viewport.Update();
            Assert.Equal(new int2(100, 40), added.Size);
            Assert.Equal(new int2(50, 20), existing.Size);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Legacy fitting inherits a viewport's single-load policy without reintroducing snapshots.</summary>
        [Fact]
        public void LegacyFit_UsesOwnerPolicy() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.OnLoad);
            viewport.ScalingMode = ViewportComponent.NoScalingMode;
            Entity child = scene.CreateEntity();
            ReferenceCanvasFitComponent fit = new ReferenceCanvasFitComponent { ReferenceWidth = 640, ReferenceHeight = 480 };
            child.AddComponent(fit);
            child.AddComponent(new RoundedRectComponent { Size = new int2(100, 40) });
            root.AddChild(child);
            viewport.Update();
            fit.Update();
            Assert.Equal(0, fit.RetainedLayoutPositionCount);
            Assert.Equal(new float2(0.5f, 0.5f), fit.CalculateScale());
            scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            fit.Update();
            Assert.Equal(new float2(0.5f, 0.5f), fit.CalculateScale());
        }
        /// <summary>A single-load owner never rescales existing values after preparation or a resize.</summary>
        [Fact]
        public void OnLoad_ResizeDoesNotReapply() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            root.AddComponent(rectangle);
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.OnLoad);
            viewport.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
            _ = viewport.AnchorSpace;
            _ = viewport.ResolvedViewportBounds;
            viewport.Update();
            scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
            viewport.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Cooked geometry is final and needs only shared metadata in the player.</summary>
        [Fact]
        public void Cook_DoesNotScalePackagedValues() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(50, 20) };
            root.AddComponent(rectangle);
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Cook);
            viewport.CookedLayout = new ViewportCookedLayout {
                AlgorithmVersion = 1, TargetWidth = 320, TargetHeight = 240, PixelAspect = 1,
                ScaleX = 0.5, ScaleY = 0.5, GlyphScale = 0.5, AnchorSize = new int2(320, 240)
            };
            viewport.Update();
            viewport.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            Assert.Equal(new int2(320, 240), viewport.AnchorSpace.Size);
            Assert.Equal(0, viewport.RetainedLayoutPositionCount);
        }

        /// <summary>Missing cook output fails explicitly instead of quietly becoming dynamic.</summary>
        [Fact]
        public void Cook_MissingMetadataFails() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            ViewportComponent viewport = scene.AddViewport(scene.CreateEntity(), ViewportLayoutProcessingMode.Cook);
            Assert.Throws<InvalidOperationException>(() => viewport.Update());
        }

        /// <summary>Replacing a component invalidates its owner without relying on entity counts.</summary>
        [Fact]
        public void SameCountComponentReplacement_DropsOldReference() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            RoundedRectComponent old = new RoundedRectComponent { Size = new int2(100, 40) };
            root.AddComponent(old);
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Dynamic);
            viewport.Update();
            root.RemoveComponent(old);
            old.Dispose();
            RoundedRectComponent replacement = new RoundedRectComponent { Size = new int2(200, 80) };
            root.AddComponent(replacement);
            viewport.Update();
            Assert.Equal(new int2(100, 40), replacement.Size);
        }

        /// <summary>Reparenting transfers originals instead of treating scaled values as new authoring data.</summary>
        [Fact]
        public void ReparentBetweenScaledOwners_DoesNotCompound() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity first = scene.CreateEntity();
            Entity second = scene.CreateEntity();
            ViewportComponent a = scene.AddViewport(first, ViewportLayoutProcessingMode.Dynamic);
            ViewportComponent b = scene.AddViewport(second, ViewportLayoutProcessingMode.Dynamic);
            Entity child = scene.CreateEntity();
            child.LocalPosition = new float3(100, 40, 0);
            RoundedRectComponent rectangle = new RoundedRectComponent { Size = new int2(100, 40) };
            child.AddComponent(rectangle);
            first.AddChild(child);
            a.Update();
            first.RemoveChild(child);
            second.AddChild(child);
            b.Update();
            Assert.Equal(new int2(50, 20), rectangle.Size);
            Assert.Equal(new float3(50, 20, 0), child.LocalPosition);
        }
    }
}

namespace helengine.core.tests.components {
    /// <summary>Verifies shared animation clips compose with prepared UI instead of restoring unscaled transforms.</summary>
    public sealed class ViewportLayoutAnimationTests {
        /// <summary>Automatic playback on a newly attached authored instance keeps its nonzero first offset exactly once.</summary>
        [Fact]
        public void NewRuntimeAnimator_PreparesNonzeroFirstOffsetOnce() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.OnLoad);
            root.InitializeHierarchy();
            viewport.Update();
            using AnimationClipAsset clip = CreateOffsetClip();
            Entity child = scene.CreateEntity();
            child.LocalPosition = new float3(100, 40, 0);
            AnimationPlayerComponent player = new AnimationPlayerComponent { Clip = clip, PlayAutomatically = true };
            child.AddComponent(player);
            root.AddChild(child);
            Assert.Equal(new float3(55, 25, 0), child.LocalPosition);
            player.Advance(0.5f);
            Assert.Equal(new float3(60, 30, 0), child.LocalPosition);
        }

        /// <summary>Dynamic resize reevaluates the current animation frame from one unchanged playback base.</summary>
        [Fact]
        public void DynamicResize_PreservesCurrentAnimatedPoseWithoutDrift() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = scene.AddViewport(root, ViewportLayoutProcessingMode.Dynamic);
            Entity child = scene.CreateEntity();
            child.LocalPosition = new float3(100, 40, 0);
            AnimationPlayerComponent player = new AnimationPlayerComponent();
            child.AddComponent(player);
            root.AddChild(child);
            using AnimationClipAsset clip = CreateOffsetClip();
            player.Play(clip, false);
            viewport.Update();
            player.Advance(0.5f);
            for (int index = 0; index < 8; index++) {
                scene.Meshes.OnWindowResize(IntPtr.Zero, 640, 480);
                viewport.Update();
                Assert.Equal(new float3(120, 60, 0), child.LocalPosition);
                scene.Meshes.OnWindowResize(IntPtr.Zero, 320, 240);
                viewport.Update();
                Assert.Equal(new float3(60, 30, 0), child.LocalPosition);
            }
        }
        /// <summary>Offset tracks stay in prepared layout space from the first pose through subsequent frames.</summary>
        [Theory]
        [InlineData(ViewportLayoutProcessingMode.Dynamic)]
        [InlineData(ViewportLayoutProcessingMode.OnLoad)]
        [InlineData(ViewportLayoutProcessingMode.Cook)]
        public void OffsetAnimation_RemainsPreparedAfterFirstFrame(ViewportLayoutProcessingMode mode) {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity root = scene.CreateEntity();
            ViewportComponent viewport = scene.AddViewport(root, mode);
            if (mode == ViewportLayoutProcessingMode.Cook) {
                viewport.CookedLayout = new ViewportCookedLayout { AlgorithmVersion = 1, TargetWidth = 320, TargetHeight = 240,
                    PixelAspect = 1, ScaleX = 0.5, ScaleY = 0.5, GlyphScale = 0.5, AnchorSize = new int2(320, 240) };
            }
            Entity child = scene.CreateEntity();
            child.LocalPosition = mode == ViewportLayoutProcessingMode.Cook ? new float3(50, 20, 0) : new float3(100, 40, 0);
            AnimationPlayerComponent player = new AnimationPlayerComponent();
            child.AddComponent(player);
            root.AddChild(child);
            using AnimationClipAsset clip = CreateOffsetClip();
            player.Play(clip, false);
            viewport.Update();
            Assert.Equal(new float3(55, 25, 0), child.LocalPosition);
            player.Advance(0.5f);
            Assert.Equal(new float3(60, 30, 0), child.LocalPosition);
            player.Advance(0.25f);
            Assert.Equal(new float3(62.5f, 32.5f, 0), child.LocalPosition);
            player.Stop();
            Assert.Equal(new float3(50, 20, 0), child.LocalPosition);
        }

        /// <summary>Absolute channels under separate owners do not rewrite their shared clip or affect a world-space player.</summary>
        [Fact]
        public void SharedClip_UsesEachOwnerWithoutMutation() {
            using ViewportLayoutTestScene scene = new ViewportLayoutTestScene();
            Entity halfRoot = scene.CreateEntity();
            ViewportComponent half = scene.AddViewport(halfRoot, ViewportLayoutProcessingMode.OnLoad);
            Entity fullRoot = scene.CreateEntity();
            ViewportComponent full = scene.AddViewport(fullRoot, ViewportLayoutProcessingMode.OnLoad);
            full.BindingMode = ViewportComponent.FixedBindingMode;
            full.FixedSize = new int2(640, 480);
            Entity a = scene.CreateEntity();
            Entity b = scene.CreateEntity();
            Entity world = scene.CreateEntity();
            AnimationPlayerComponent pa = new AnimationPlayerComponent();
            AnimationPlayerComponent pb = new AnimationPlayerComponent();
            AnimationPlayerComponent pw = new AnimationPlayerComponent();
            a.AddComponent(pa); b.AddComponent(pb); world.AddComponent(pw);
            halfRoot.AddChild(a); fullRoot.AddChild(b);
            using AnimationClipAsset clip = new AnimationClipAsset { Duration = 1, PositionTracks = [new PositionKeyframeTrackAsset {
                Keyframes = [new PositionKeyframeAsset(0, new float3(100, 40, 0), AnimationInterpolationMode.Linear),
                    new PositionKeyframeAsset(1, new float3(200, 80, 0), AnimationInterpolationMode.Linear)]
            }] };
            pa.Play(clip, false); pb.Play(clip, false); pw.Play(clip, false);
            half.Update(); full.Update();
            pa.Advance(0.5f); pb.Advance(0.5f); pw.Advance(0.5f);
            Assert.Equal(new float3(75, 30, 0), a.LocalPosition);
            Assert.Equal(new float3(150, 60, 0), b.LocalPosition);
            Assert.Equal(new float3(150, 60, 0), world.LocalPosition);
            Assert.Equal(new float3(100, 40, 0), clip.PositionTracks[0].Keyframes[0].Value);
            Assert.Equal(new float3(200, 80, 0), clip.PositionTracks[0].Keyframes[1].Value);
        }

        /// <summary>Creates one additive clip with a deliberately nonzero first frame.</summary>
        static AnimationClipAsset CreateOffsetClip() {
            return new AnimationClipAsset { Duration = 1, PositionOffsetTracks = [new PositionOffsetKeyframeTrackAsset {
                Keyframes = [new PositionKeyframeAsset(0, new float3(10, 10, 0), AnimationInterpolationMode.Linear),
                    new PositionKeyframeAsset(1, new float3(30, 30, 0), AnimationInterpolationMode.Linear)]
            }] };
        }
    }
}

namespace helengine.animation.runtime.tests {
    /// <summary>
    /// Plays the sample clip on a two-level hierarchy and checks seeking, target resolution and playback.
    /// </summary>
    public sealed class HierarchyAnimationPlayerTests {
        /// <summary>
        /// At 1 s the arm is halfway along its position keys and the hand has turned a quarter of the way along its
        /// rotation keys with spherical interpolation (22.5 degrees, which a normalized lerp would miss), scaled to 1.5.
        /// </summary>
        [Fact]
        public void Seek_appliesInterpolatedTrsToNamedDescendants() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Initialize();

            scene.Player.Seek(1);

            Assert.Equal(1.0, scene.Player.Time, 9);
            TransformAssert.Vector(new float3(1, 2, 3), scene.Arm.LocalPosition);
            double halfAngle = Math.PI / 16;
            TransformAssert.Quaternion(new float4(0, 0, Math.Sin(halfAngle), Math.Cos(halfAngle)), scene.Hand.LocalOrientation);
            TransformAssert.Vector(new float3(1.5, 1.5, 1.5), scene.Hand.LocalScale);
        }

        /// <summary>
        /// Seeking before the start or past the end clamps to the clip range, and seeking the same time twice gives the
        /// same transforms.
        /// </summary>
        [Fact]
        public void Seek_clampsAndIsIdempotent() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Initialize();

            scene.Player.Seek(0);
            float3 armAtStart = scene.Arm.LocalPosition;
            float4 handAtStart = scene.Hand.LocalOrientation;
            scene.Player.Seek(-1);
            Assert.Equal(0.0, scene.Player.Time, 9);
            Assert.Equal(armAtStart, scene.Arm.LocalPosition);
            Assert.Equal(handAtStart, scene.Hand.LocalOrientation);

            scene.Player.Seek(4);
            float3 armAtEnd = scene.Arm.LocalPosition;
            float4 handAtEnd = scene.Hand.LocalOrientation;
            float3 scaleAtEnd = scene.Hand.LocalScale;
            scene.Player.Seek(99);
            Assert.Equal(4.0, scene.Player.Time, 9);
            Assert.Equal(armAtEnd, scene.Arm.LocalPosition);
            Assert.Equal(handAtEnd, scene.Hand.LocalOrientation);
            Assert.Equal(scaleAtEnd, scene.Hand.LocalScale);
            TransformAssert.Quaternion(HierarchyAnimationTestClips.QuarterTurnAboutZ(), handAtEnd);

            scene.Player.Seek(2.7);
            float3 armFirst = scene.Arm.LocalPosition;
            float4 handFirst = scene.Hand.LocalOrientation;
            float3 scaleFirst = scene.Hand.LocalScale;
            scene.Player.Seek(1);
            scene.Player.Seek(2.7);
            Assert.Equal(armFirst, scene.Arm.LocalPosition);
            Assert.Equal(handFirst, scene.Hand.LocalOrientation);
            Assert.Equal(scaleFirst, scene.Hand.LocalScale);
        }

        /// <summary>
        /// A track whose target reference does not resolve fails initialization with an error naming its target path.
        /// </summary>
        [Fact]
        public void MissingTarget_throwsNamingPath() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(false);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(scene.Initialize);

            Assert.Contains("Arm/Hand", error.Message);
        }

        /// <summary>
        /// Frames advance the time only while playing, scaled by the speed, and a looping player wraps past the end.
        /// </summary>
        [Fact]
        public void Update_advancesOnlyWhilePlaying_andLoops() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Initialize();

            scene.Core.Update(0.5);
            Assert.Equal(0.0, scene.Player.Time, 6);

            scene.Player.Playing = true;
            scene.Player.Loop = true;
            scene.Core.Update(0.5);
            Assert.Equal(0.5, scene.Player.Time, 6);
            TransformAssert.Vector(new float3(0.5, 1, 1.5), scene.Arm.LocalPosition);

            scene.Player.Speed = 2;
            scene.Core.Update(2);
            Assert.Equal(0.5, scene.Player.Time, 6);
            TransformAssert.Vector(new float3(0.5, 1, 1.5), scene.Arm.LocalPosition);

            scene.Player.Playing = false;
            scene.Core.Update(1);
            Assert.Equal(0.5, scene.Player.Time, 6);
        }

        /// <summary>
        /// A zero-length frame (the core's first update has a zero delta) keeps a player at time 0 playing, and the next
        /// frame advances it; a non-looping player stops only when it reaches the end.
        /// </summary>
        [Fact]
        public void Update_zeroDeltaAtStart_keepsPlaying() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Initialize();
            scene.Player.Playing = true;

            scene.Core.Update(0);
            Assert.True(scene.Player.Playing);
            Assert.Equal(0.0, scene.Player.Time, 6);

            scene.Core.Update(1);
            Assert.True(scene.Player.Playing);
            Assert.Equal(1.0, scene.Player.Time, 6);

            scene.Core.Update(5);
            Assert.False(scene.Player.Playing);
            Assert.Equal(4.0, scene.Player.Time, 6);
        }

        /// <summary>
        /// Initialization applies the pose at time 0, so the shown pose matches <see cref="HierarchyAnimationPlayerComponent.Time"/>.
        /// </summary>
        [Fact]
        public void Initialize_appliesPoseAtTimeZero() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Arm.LocalPosition = new float3(9, 9, 9);
            scene.Hand.LocalScale = new float3(7, 7, 7);

            scene.Initialize();

            Assert.Equal(0.0, scene.Player.Time, 9);
            TransformAssert.Vector(new float3(0, 0, 0), scene.Arm.LocalPosition);
            TransformAssert.Vector(new float3(1, 1, 1), scene.Hand.LocalScale);
        }

        /// <summary>
        /// A player whose track target count differs from the clip's track count fails initialization naming both counts
        /// and the clip path.
        /// </summary>
        [Fact]
        public void TrackTargetCountMismatch_throwsNamingCountsAndClip() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            scene.Player.TrackTargets = new SceneEntityReference[] { scene.Player.TrackTargets[0] };

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(scene.Initialize);

            Assert.Contains("2 tracks", error.Message);
            Assert.Contains("1 track targets", error.Message);
            Assert.Contains(HierarchyAnimationTestClips.ClipPath, error.Message);
        }
    }
}

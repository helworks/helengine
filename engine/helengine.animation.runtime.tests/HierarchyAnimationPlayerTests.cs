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
            AssertVector(new float3(1, 2, 3), scene.Arm.LocalPosition);
            double halfAngle = Math.PI / 16;
            AssertQuaternion(new float4(0, 0, Math.Sin(halfAngle), Math.Cos(halfAngle)), scene.Hand.LocalOrientation);
            AssertVector(new float3(1.5, 1.5, 1.5), scene.Hand.LocalScale);
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
            AssertQuaternion(HierarchyAnimationTestClips.QuarterTurnAboutZ(), handAtEnd);

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
            AssertVector(new float3(0.5, 1, 1.5), scene.Arm.LocalPosition);

            scene.Player.Speed = 2;
            scene.Core.Update(2);
            Assert.Equal(0.5, scene.Player.Time, 6);
            AssertVector(new float3(0.5, 1, 1.5), scene.Arm.LocalPosition);

            scene.Player.Playing = false;
            scene.Core.Update(1);
            Assert.Equal(0.5, scene.Player.Time, 6);
        }

        /// <summary>
        /// Asserts two vectors match component by component.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        static void AssertVector(float3 expected, float3 actual) {
            Assert.Equal((double)expected.X, (double)actual.X, 5);
            Assert.Equal((double)expected.Y, (double)actual.Y, 5);
            Assert.Equal((double)expected.Z, (double)actual.Z, 5);
        }

        /// <summary>
        /// Asserts two quaternions match component by component.
        /// </summary>
        /// <param name="expected">Expected quaternion.</param>
        /// <param name="actual">Actual quaternion.</param>
        static void AssertQuaternion(float4 expected, float4 actual) {
            Assert.Equal((double)expected.X, (double)actual.X, 5);
            Assert.Equal((double)expected.Y, (double)actual.Y, 5);
            Assert.Equal((double)expected.Z, (double)actual.Z, 5);
            Assert.Equal((double)expected.W, (double)actual.W, 5);
        }
    }
}

namespace helengine.animation.runtime.tests {
    /// <summary>
    /// Samples clips directly with <see cref="HierarchyAnimationEvaluator.Apply"/> on the test hierarchy's entities.
    /// </summary>
    public sealed class HierarchyAnimationEvaluatorTests {
        /// <summary>
        /// Step interpolation holds the earlier key for position, rotation and scale until the next key time.
        /// </summary>
        [Fact]
        public void Apply_stepInterpolation_holdsEarlierKey() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            HierarchyAnimationTrack track = new HierarchyAnimationTrack {
                TargetPath = "Arm",
                PositionTimes = new double[] { 0, 2 },
                PositionValues = new float3[] { new float3(1, 1, 1), new float3(5, 5, 5) },
                RotationTimes = new double[] { 0, 2 },
                RotationValues = new float4[] { float4.Identity, HierarchyAnimationTestClips.QuarterTurnAboutZ() },
                ScaleTimes = new double[] { 0, 2 },
                ScaleValues = new float3[] { new float3(2, 2, 2), new float3(4, 4, 4) },
                Interpolation = AnimationInterpolationMode.Step
            };
            HierarchyAnimationClipAsset clip = new HierarchyAnimationClipAsset {
                Duration = 2,
                Tracks = new HierarchyAnimationTrack[] { track }
            };

            HierarchyAnimationEvaluator.Apply(clip, new Entity[] { scene.Arm }, 1.9);
            TransformAssert.Vector(new float3(1, 1, 1), scene.Arm.LocalPosition);
            TransformAssert.Quaternion(float4.Identity, scene.Arm.LocalOrientation);
            TransformAssert.Vector(new float3(2, 2, 2), scene.Arm.LocalScale);

            HierarchyAnimationEvaluator.Apply(clip, new Entity[] { scene.Arm }, 2);
            TransformAssert.Vector(new float3(5, 5, 5), scene.Arm.LocalPosition);
            TransformAssert.Quaternion(HierarchyAnimationTestClips.QuarterTurnAboutZ(), scene.Arm.LocalOrientation);
            TransformAssert.Vector(new float3(4, 4, 4), scene.Arm.LocalScale);
        }

        /// <summary>
        /// A channel with no keys leaves that part of the entity's transform untouched.
        /// </summary>
        [Fact]
        public void Apply_emptyChannels_leaveTransformUntouched() {
            using HierarchyPlayerScene scene = new HierarchyPlayerScene(true);
            float4 orientation = HierarchyAnimationTestClips.QuarterTurnAboutZ();
            scene.Arm.LocalOrientation = orientation;
            scene.Arm.LocalScale = new float3(3, 3, 3);
            HierarchyAnimationClipAsset clip = new HierarchyAnimationClipAsset {
                Duration = 1,
                Tracks = new HierarchyAnimationTrack[] {
                    new HierarchyAnimationTrack {
                        TargetPath = "Arm",
                        PositionTimes = new double[] { 0, 1 },
                        PositionValues = new float3[] { new float3(0, 0, 0), new float3(2, 0, 0) }
                    }
                }
            };

            HierarchyAnimationEvaluator.Apply(clip, new Entity[] { scene.Arm }, 0.5);

            TransformAssert.Vector(new float3(1, 0, 0), scene.Arm.LocalPosition);
            TransformAssert.Quaternion(orientation, scene.Arm.LocalOrientation);
            TransformAssert.Vector(new float3(3, 3, 3), scene.Arm.LocalScale);
        }
    }
}

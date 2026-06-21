using helengine.directx11;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies DirectX11 batching categorization from extracted drawable submissions.
    /// </summary>
    public class DirectX11BatchingPlannerTests {
        /// <summary>
        /// Ensures the planner categorizes static, dynamic, and instancing-eligible drawables into separate buckets.
        /// </summary>
        [Fact]
        public void Build_WhenDrawablesExposeBatchingEligibility_CategorizesEachBatchingBucket() {
            DirectX11BatchingPlanner planner = new DirectX11BatchingPlanner();
            RenderFrameDrawableSubmission[] drawableSubmissions = [
                new RenderFrameDrawableSubmission(
                    new TestDrawable3D(),
                    false,
                    new RenderFrameBatchingMetadata(true, false, false)),
                new RenderFrameDrawableSubmission(
                    new TestDrawable3D(),
                    false,
                    new RenderFrameBatchingMetadata(false, true, false)),
                new RenderFrameDrawableSubmission(
                    new TestDrawable3D(),
                    false,
                    new RenderFrameBatchingMetadata(false, false, true))
            ];

            DirectX11BatchingPlan plan = planner.Build(drawableSubmissions);

            Assert.Single(plan.StaticBatchDrawables);
            Assert.Single(plan.DynamicBatchDrawables);
            Assert.Single(plan.InstancedDrawables);
        }

        /// <summary>
        /// Provides one minimal drawable implementation for batching-planner tests.
        /// </summary>
        sealed class TestDrawable3D : IDrawable3D {
            /// <summary>
            /// Initializes one test drawable with placeholder runtime resources.
            /// </summary>
            public TestDrawable3D() {
                Model = new TestRuntimeModel();
                Materials = new[] { new TestRuntimeMaterial() };
            }

            /// <summary>
            /// Gets the parent entity that owns the drawable.
            /// </summary>
            public Entity Parent => null;

            /// <summary>
            /// Gets or sets the render order for 3D drawing.
            /// </summary>
            public byte RenderOrder3D { get; set; }

            /// <summary>
            /// Gets the runtime model associated with this drawable.
            /// </summary>
            public RuntimeModel Model { get; }

            /// <summary>
            /// Gets or sets the runtime materials bound to each submesh slot.
            /// </summary>
            public RuntimeMaterial[] Materials { get; set; }
        }
    }
}

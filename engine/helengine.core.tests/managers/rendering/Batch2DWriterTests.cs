using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Verifies ordered, bounded batching and the synchronous ownership contract of the 2D writer.
    /// </summary>
    public sealed class Batch2DWriterTests {
        /// <summary>
        /// Confirms that same-key quads share one submission and retain literal triangle indices.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenKeysMatch_CopiesVerticesAndBuildsSequentialIndices() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            Batch2DRun run = CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480);

            writer.AppendQuad(run, CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.AppendQuad(run, CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.Flush();

            Assert.Single(sink.Runs);
            Assert.Equal(8, sink.Vertices[0].Length);
            Assert.Equal(12, sink.Indices[0].Length);
            Assert.Equal(new ushort[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 }, sink.Indices[0]);
            Assert.Equal(5f, sink.Vertices[0][4].Position.X);
            Assert.Equal(8f, sink.Vertices[0][7].Position.X);
        }

        /// <summary>
        /// Confirms that A/B/A texture ordering stays in three adjacent runs.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenTextureSequenceIsAThenBThenA_PreservesRunOrder() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            ManagedRuntimeTexture textureA = new ManagedRuntimeTexture();
            ManagedRuntimeTexture textureB = new ManagedRuntimeTexture();

            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, textureA, 0, 0, 640, 480), CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, textureB, 0, 0, 640, 480), CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, textureA, 0, 0, 640, 480), CreateVertex(9f), CreateVertex(10f), CreateVertex(11f), CreateVertex(12f));
            writer.Flush();

            Assert.Equal(3, sink.Runs.Count);
            Assert.Same(textureA, sink.Runs[0].Texture);
            Assert.Same(textureB, sink.Runs[1].Texture);
            Assert.Same(textureA, sink.Runs[2].Texture);
        }

        /// <summary>
        /// Confirms that reaching configured capacity submits the current chunk before the next quad.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenCapacityIsReached_SubmitsSeparateChunks() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            Batch2DRun run = CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480);

            writer.AppendQuad(run, CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.AppendQuad(run, CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.Flush();

            Assert.Equal(2, sink.Runs.Count);
            Assert.All(sink.Vertices, vertices => Assert.Equal(4, vertices.Length));
            Assert.All(sink.Indices, indices => Assert.Equal(new ushort[] { 0, 1, 2, 0, 2, 3 }, indices));
        }

        /// <summary>
        /// Confirms that camera changes submit old geometry before forwarding the new camera.
        /// </summary>
        [Fact]
        public void ConfigureCamera_WhenCameraChanges_FlushesBeforeSettingNewCamera() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            float4x4 firstProjection = float4x4.Identity;
            float4x4 secondProjection = float4x4.Identity;
            secondProjection.M11 = 2f;
            writer.ConfigureCamera(firstProjection, new float4(0f, 0f, 640f, 480f));
            Batch2DRun run = CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480);
            writer.AppendQuad(run, CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));

            float4 secondViewport = new float4(10f, 20f, 640f, 480f);
            writer.ConfigureCamera(secondProjection, secondViewport);

            Assert.Single(sink.Runs);
            Assert.Equal(firstProjection.M11, sink.Cameras[0].M11);
            Assert.Equal(new float4(0f, 0f, 640f, 480f), sink.Viewports[0]);
            Assert.Equal(secondProjection.M11, sink.CurrentCamera.M11);
            Assert.Equal(secondViewport, sink.CurrentViewport);
            Assert.Equal("SetCamera:1", sink.Events[0]);
            Assert.Equal("Submit:0", sink.Events[1]);
            Assert.Equal("SetCamera:2", sink.Events[2]);
            writer.AppendQuad(run, CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.Flush();
            Assert.Equal(2, sink.Runs.Count);
        }

        /// <summary>
        /// Confirms equal camera values still delimit separate sessions and flush the prior run first.
        /// </summary>
        [Fact]
        public void ConfigureCamera_WhenValuesAreUnchanged_StillCreatesSessionBoundary() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 4);
            float4x4 projection = float4x4.Identity;
            float4 viewport = new float4(10f, 20f, 640f, 480f);
            Batch2DRun run = CreateRun(Batch2DVariant.RoundedShape, null, 7, 0, 640, 480);
            writer.ConfigureCamera(projection, viewport);
            writer.AppendQuad(run, CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));

            writer.ConfigureCamera(projection, viewport);

            writer.AppendQuad(run, CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.Flush();

            Assert.Equal(2, sink.Runs.Count);
            Assert.Equal("SetCamera:1", sink.Events[0]);
            Assert.Equal("Submit:7", sink.Events[1]);
            Assert.Equal("SetCamera:1", sink.Events[2]);
            Assert.Equal("Submit:7", sink.Events[3]);
        }

        /// <summary>
        /// Confirms that flushing an empty writer produces no sink submission.
        /// </summary>
        [Fact]
        public void Flush_WhenBufferIsEmpty_DoesNotSubmit() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));

            writer.Flush();

            Assert.Empty(sink.Runs);
        }

        /// <summary>
        /// Confirms that each scissor field and the shader variant participate in run identity.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenScissorOrVariantChanges_SplitsRuns() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 8);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            ManagedRuntimeTexture texture = new ManagedRuntimeTexture();

            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, texture, 0, 0, 640, 480), CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, texture, 1, 0, 640, 480), CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f));
            writer.AppendQuad(CreateRun(Batch2DVariant.RoundedShape, null, 1, 0, 640, 480), CreateVertex(9f), CreateVertex(10f), CreateVertex(11f), CreateVertex(12f));
            writer.Flush();

            Assert.Equal(3, sink.Runs.Count);
            Assert.Equal(0, sink.Runs[0].ScissorX);
            Assert.Equal(1, sink.Runs[1].ScissorX);
            Assert.Equal(Batch2DVariant.RoundedShape, sink.Runs[2].Variant);
        }

        /// <summary>
        /// Confirms that copying vertices at append time isolates queued data from later mutation.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenSourceVertexChanges_PreservesTheOriginalValue() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            Batch2DVertex original = CreateVertex(3f);
            writer.AppendQuad(CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480), original, CreateVertex(4f), CreateVertex(5f), CreateVertex(6f));
            original.Position.X = 99f;
            original.Color.X = 0.125f;

            writer.Flush();

            Assert.Equal(3f, sink.Vertices[0][0].Position.X);
            Assert.Equal(1f, sink.Vertices[0][0].Color.X);
        }

        /// <summary>
        /// Confirms that camera setup is mandatory before any draw can enter the buffer.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenCameraIsNotConfigured_ThrowsInvalidOperationException() {
            using Batch2DWriter writer = new Batch2DWriter(new RecordingBatch2DSink(), 1);

            Assert.Throws<InvalidOperationException>(() => writer.AppendQuad(
                CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f)));
        }

        /// <summary>
        /// Confirms viewport origins are finite and viewport dimensions are positive.
        /// </summary>
        [Theory]
        [InlineData(float.NaN, 0f, 640f, 480f)]
        [InlineData(0f, float.PositiveInfinity, 640f, 480f)]
        [InlineData(0f, 0f, 0f, 480f)]
        [InlineData(0f, 0f, 640f, -1f)]
        public void ConfigureCamera_WhenViewportIsInvalid_ThrowsArgumentException(float x, float y, float width, float height) {
            using Batch2DWriter writer = new Batch2DWriter(new RecordingBatch2DSink(), 1);

            Assert.Throws<ArgumentException>(() => writer.ConfigureCamera(
                float4x4.Identity, new float4(x, y, width, height)));
        }

        /// <summary>
        /// Confirms that zero-area scissors drop a draw instead of widening it to the full viewport.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenScissorHasZeroArea_SkipsDraw() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));

            writer.AppendQuad(CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 0, 480), CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.Flush();

            Assert.Empty(sink.Runs);
        }

        /// <summary>
        /// Confirms that malformed capacities are rejected before a writer can allocate its arrays.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(16385)]
        public void Constructor_WhenCapacityIsOutsideSupportedRange_ThrowsArgumentOutOfRangeException(int capacity) {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Batch2DWriter(new RecordingBatch2DSink(), capacity));
        }

        /// <summary>
        /// Confirms that disposed textures cannot be queued for a textured shader run.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenTextureIsDisposed_ThrowsObjectDisposedException() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            ManagedRuntimeTexture texture = new ManagedRuntimeTexture();
            texture.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.AppendQuad(
                CreateRun(Batch2DVariant.Textured, texture, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f)));
        }

        /// <summary>
        /// Confirms a borrowed texture disposed after append is never submitted to the sink.
        /// </summary>
        [Fact]
        public void Flush_WhenTextureWasDisposedAfterAppend_ThrowsWithoutSubmitting() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            ManagedRuntimeTexture texture = new ManagedRuntimeTexture();
            writer.AppendQuad(CreateRun(Batch2DVariant.Textured, texture, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            texture.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.Flush());
            Assert.Empty(sink.Runs);
        }

        /// <summary>
        /// Confirms that non-finite vertex components are rejected before buffer mutation.
        /// </summary>
        [Fact]
        public void AppendQuad_WhenVertexContainsNonFiniteValue_ThrowsArgumentException() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            Batch2DVertex invalid = CreateVertex(1f);
            invalid.Color.W = float.NaN;

            Assert.Throws<ArgumentException>(() => writer.AppendQuad(
                CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                invalid, CreateVertex(2f), CreateVertex(3f), CreateVertex(4f)));
            Assert.Empty(sink.Runs);
        }

        /// <summary>
        /// Confirms that negative scissor extents are rejected rather than silently normalized.
        /// </summary>
        [Fact]
        public void Run_WhenScissorExtentIsNegative_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, -1, 10));
        }

        /// <summary>
        /// Confirms that disposed writers reject both additional input and explicit flush requests.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalled_RejectsAppendAndFlush() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            writer.AppendQuad(CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            writer.Dispose();

            Assert.Empty(sink.Runs);
            Assert.Throws<ObjectDisposedException>(() => writer.AppendQuad(
                CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f)));
            Assert.Throws<ObjectDisposedException>(() => writer.Flush());
        }

        /// <summary>
        /// Confirms repeated disposal discards queued geometry, preserves borrowed textures, and keeps disposed operations rejected.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalledTwice_DiscardsQueuedDataAndPreservesBorrowedTexture() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            ManagedRuntimeTexture texture = new ManagedRuntimeTexture();
            Batch2DRun run = CreateRun(Batch2DVariant.Textured, texture, 0, 0, 640, 480);
            writer.AppendQuad(run, CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));

            writer.Dispose();
            writer.Dispose();

            Assert.Empty(sink.Runs);
            Assert.False(texture.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => writer.AppendQuad(
                run, CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f)));
            Assert.Throws<ObjectDisposedException>(() => writer.Flush());
        }

        /// <summary>
        /// Confirms a failed submit faults the writer and prevents a second attempt at the pending chunk.
        /// </summary>
        [Fact]
        public void Flush_WhenSinkThrows_FaultsWriterWithoutRetryingSubmission() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink();
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);
            writer.ConfigureCamera(float4x4.Identity, new float4(0f, 0f, 640f, 480f));
            writer.AppendQuad(CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                CreateVertex(1f), CreateVertex(2f), CreateVertex(3f), CreateVertex(4f));
            sink.SubmitFailure = new InvalidOperationException("submission failed");

            Assert.Throws<InvalidOperationException>(() => writer.Flush());
            sink.SubmitFailure = null;
            Assert.Throws<InvalidOperationException>(() => writer.Flush());
            Assert.Throws<InvalidOperationException>(() => writer.AppendQuad(
                CreateRun(Batch2DVariant.RoundedShape, null, 0, 0, 640, 480),
                CreateVertex(5f), CreateVertex(6f), CreateVertex(7f), CreateVertex(8f)));
            Assert.Equal(1, sink.SubmitCalls);
        }

        /// <summary>
        /// Confirms a failed camera update faults the writer and rejects further camera calls.
        /// </summary>
        [Fact]
        public void ConfigureCamera_WhenSinkThrows_FaultsWriterWithoutRetryingCameraUpdate() {
            RecordingBatch2DSink sink = new RecordingBatch2DSink {
                CameraFailure = new InvalidOperationException("camera update failed")
            };
            using Batch2DWriter writer = new Batch2DWriter(sink, 1);

            Assert.Throws<InvalidOperationException>(() => writer.ConfigureCamera(
                float4x4.Identity, new float4(0f, 0f, 640f, 480f)));
            sink.CameraFailure = null;
            Assert.Throws<InvalidOperationException>(() => writer.ConfigureCamera(
                float4x4.Identity, new float4(0f, 0f, 640f, 480f)));
            Assert.Equal(1, sink.CameraCalls);
            Assert.Empty(sink.Runs);
        }

        /// <summary>
        /// Builds a batch run with the exact fields supplied by the calling test.
        /// </summary>
        /// <param name="variant">The shader variant required by the run.</param>
        /// <param name="texture">The borrowed texture, when the variant samples one.</param>
        /// <param name="scissorX">The scissor left edge.</param>
        /// <param name="scissorY">The scissor top edge.</param>
        /// <param name="scissorWidth">The scissor width.</param>
        /// <param name="scissorHeight">The scissor height.</param>
        /// <returns>A run initialized with the supplied key.</returns>
        static Batch2DRun CreateRun(Batch2DVariant variant, RuntimeTexture texture, int scissorX, int scissorY, int scissorWidth, int scissorHeight) {
            return new Batch2DRun(variant, texture, scissorX, scissorY, scissorWidth, scissorHeight);
        }

        /// <summary>
        /// Creates a complete vertex whose position makes its original value easy to identify.
        /// </summary>
        /// <param name="positionX">The identifying X coordinate.</param>
        /// <returns>A finite vertex with all six float4 fields initialized.</returns>
        static Batch2DVertex CreateVertex(float positionX) {
            return new Batch2DVertex {
                Position = new float4(positionX, 2f, 3f, 1f),
                TexLocal = new float4(0.25f, 0.75f, 4f, 5f),
                Color = new float4(1f, 0.5f, 0.25f, 1f),
                Shape = new float4(8f, 9f, 2f, 1f),
                Corners = new float4(1f, 0f, 1f, 0f),
                BorderColor = new float4(0f, 0f, 0f, 1f)
            };
        }
    }
}

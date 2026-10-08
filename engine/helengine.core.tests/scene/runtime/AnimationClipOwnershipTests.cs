using helengine;

namespace helengine.core.tests.scene.runtime {
    /// <summary>Checks packaged animation lifetime from materialization through the final scene release.</summary>
    public sealed class AnimationClipOwnershipTests {
        /// <summary>Checks path reuse, two load cycles, and preservation of the payload until the last owner releases it.</summary>
        [Fact]
        public void Resolver_TwoLoadCyclesShareReferencesAndReleaseAfterFinalOwner() {
            using Core core = CreateCore();
            using ContentManager content = new ContentManager(new MemorySource());
            ClipProcessor processor = new ClipProcessor();
            content.RegisterProcessor(RuntimeContentProcessorIds.AnimationClipAsset, processor);
            using RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, content);
            SceneAssetReference reference = SceneAssetReferenceFactory.CreateFileSystemAnimationClip("animations/logo.hele");
            SceneOwnedAssetReferenceTable<AnimationClipAsset> table = new SceneOwnedAssetReferenceTable<AnimationClipAsset>("clip", asset => NativeOwnership.DisposeAndDelete(asset));
            for (int cycle = 0; cycle < 2; cycle++) {
                resolver.BeginOwnedAssetTracking();
                AnimationClipAsset clip = resolver.ResolveAnimationClip(reference);
                Assert.Same(clip, resolver.ResolveAnimationClip(reference));
                RuntimeSceneOwnedAssetSet first = resolver.CompleteOwnedAssetTracking();
                RuntimeSceneOwnedAssetSet second = CreateSet(clip);
                Assert.Single(first.OwnedAnimationClips);
                table.Register(first.OwnedAnimationClips);
                table.Register(second.OwnedAnimationClips);

                table.Release(first.OwnedAnimationClips);
                first.Dispose();

                Assert.Equal(0, ((CountingClip)clip).DisposeCalls);
                Assert.Equal(7f, clip.PositionTracks[0].Keyframes[0].Value.X);
                table.Release(second.OwnedAnimationClips);
                second.Dispose();
                Assert.Equal(1, ((CountingClip)clip).DisposeCalls);
                Assert.Null(clip.PositionTracks);
                Assert.Equal(0, table.Count);
            }
            Assert.Equal(2, processor.ReadCount);
        }

        /// <summary>Checks cancelled materialization disposes the deduplicated clip once and does not retain its cache.</summary>
        [Fact]
        public void Resolver_CancellationDisposesClipOnceAndAllowsFreshScope() {
            using Core core = CreateCore();
            using ContentManager content = new ContentManager(new MemorySource());
            ClipProcessor processor = new ClipProcessor();
            content.RegisterProcessor(RuntimeContentProcessorIds.AnimationClipAsset, processor);
            using RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(core, content);
            SceneAssetReference reference = SceneAssetReferenceFactory.CreateFileSystemAnimationClip("animations/logo.hele");
            resolver.BeginOwnedAssetTracking();
            CountingClip clip = (CountingClip)resolver.ResolveAnimationClip(reference);
            Assert.Same(clip, resolver.ResolveAnimationClip(reference));
            resolver.CancelOwnedAssetTracking();
            resolver.CancelOwnedAssetTracking();
            Assert.Equal(1, clip.DisposeCalls);
            resolver.BeginOwnedAssetTracking();
            CountingClip next = (CountingClip)resolver.ResolveAnimationClip(reference);
            Assert.NotSame(clip, next);
            resolver.CancelOwnedAssetTracking();
            Assert.Equal(1, next.DisposeCalls);
        }

        /// <summary>Checks every base and platform track releases its own keyframe tree, including repeated disposal.</summary>
        [Fact]
        public void Clip_DisposeClearsAllTrackTreesAndPreservesBorrowedIdentityStrings() {
            PositionKeyframeTrackAsset position = new PositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset { Value = new float3(7, 8, 9) } } };
            PositionOffsetKeyframeTrackAsset offset = new PositionOffsetKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset() } };
            ScaleKeyframeTrackAsset scale = new ScaleKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset() } };
            RotationKeyframeTrackAsset rotation = new RotationKeyframeTrackAsset { Keyframes = new[] { new RotationKeyframeAsset() } };
            PlatformPositionKeyframeTrackAsset platformPosition = new PlatformPositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset() } };
            PlatformPositionKeyframeTrackAsset platformOffset = new PlatformPositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset() } };
            PlatformPositionKeyframeTrackAsset platformScale = new PlatformPositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset() } };
            PlatformRotationKeyframeTrackAsset platformRotation = new PlatformRotationKeyframeTrackAsset { Keyframes = new[] { new RotationKeyframeAsset() } };
            AnimationClipPlatformOverrideAsset platform = new AnimationClipPlatformOverrideAsset {
                PositionTracks = new[] { platformPosition }, PositionOffsetTracks = new[] { platformOffset },
                ScaleTracks = new[] { platformScale }, RotationTracks = new[] { platformRotation }
            };
            string identity = "former-authoring-id";
            AnimationClipAsset clip = new AnimationClipAsset {
                PositionTracks = new[] { position }, PositionOffsetTracks = new[] { offset }, ScaleTracks = new[] { scale },
                RotationTracks = new[] { rotation }, PlatformOverrides = new[] { platform }, FormerAuthoringAssetIds = new[] { identity }
            };
            Assert.Equal(7f, position.Keyframes[0].Value.X);

            clip.Dispose();
            clip.Dispose();

            Assert.Null(position.Keyframes);
            Assert.Null(offset.Keyframes);
            Assert.Null(scale.Keyframes);
            Assert.Null(rotation.Keyframes);
            Assert.Null(platformPosition.Keyframes);
            Assert.Null(platformOffset.Keyframes);
            Assert.Null(platformScale.Keyframes);
            Assert.Null(platformRotation.Keyframes);
            Assert.Null(platform.PositionTracks);
            Assert.Null(platform.PositionOffsetTracks);
            Assert.Null(platform.ScaleTracks);
            Assert.Null(platform.RotationTracks);
            Assert.Null(clip.PlatformOverrides);
            Assert.Null(clip.FormerAuthoringAssetIds);
            Assert.Equal("former-authoring-id", identity);
        }

        /// <summary>Checks default empty-array singletons survive cleanup and borrowed clip elements outlive their containers.</summary>
        [Fact]
        public void SetDispose_DoesNotDisposeBorrowedClipAndDefaultArraysRemainUsable() {
            CountingClip clip = new CountingClip();
            RuntimeSceneOwnedAssetSet set = CreateSet(clip);
            set.Dispose();
            set.Dispose();
            Assert.Equal(0, clip.DisposeCalls);
            clip.Dispose();
            clip.Dispose();
            Assert.Empty(Array.Empty<PositionKeyframeTrackAsset>());
            Assert.Empty(Array.Empty<AnimationClipPlatformOverrideAsset>());
            Assert.Empty(Array.Empty<string>());
        }

        /// <summary>Creates a set whose containers borrow one clip.</summary>
        /// <param name="clip">Clip reference registered with the scene owner.</param>
        /// <returns>Reference-container set without element ownership.</returns>
        static RuntimeSceneOwnedAssetSet CreateSet(AnimationClipAsset clip) {
            return new RuntimeSceneOwnedAssetSet(Array.Empty<RuntimeTexture>(), Array.Empty<FontAsset>(), Array.Empty<AudioAsset>(), Array.Empty<RuntimeModel>(), Array.Empty<RuntimeMaterial>(), new[] { clip });
        }

        /// <summary>Creates the headless core required by the resolver.</summary>
        /// <returns>Core with an explicit host stream source.</returns>
        static Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new MemorySource() });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>Provides a fresh stream for each requested packaged path.</summary>
        sealed class MemorySource : IContentStreamSource {
            /// <summary>Returns an empty processor input stream.</summary>
            /// <param name="assetPath">Requested packaged path.</param>
            /// <returns>Fresh stream owned by the content load.</returns>
            public Stream OpenRead(string assetPath) => new MemoryStream();
        }

        /// <summary>Creates a distinct animation tree on each actual processor call.</summary>
        sealed class ClipProcessor : IContentProcessor<AnimationClipAsset> {
            /// <summary>Gets the number of deserialization calls.</summary>
            public int ReadCount { get; private set; }
            /// <summary>Gets the processor output type.</summary>
            public Type OutputType => typeof(AnimationClipAsset);
            /// <summary>Creates an owned clip tree used to verify path sharing.</summary>
            /// <param name="stream">Borrowed input stream.</param>
            /// <returns>New clip with intact keyframe data.</returns>
            public AnimationClipAsset Read(Stream stream) {
                ReadCount++;
                return new CountingClip { PositionTracks = new[] { new PositionKeyframeTrackAsset { Keyframes = new[] { new PositionKeyframeAsset { Value = new float3(7, 8, 9) } } } } };
            }
            /// <summary>Reads the same owned clip as an untyped content result.</summary>
            /// <param name="stream">Borrowed processor input.</param>
            /// <returns>New clip tree.</returns>
            public object ReadObject(Stream stream) => Read(stream);
        }

        /// <summary>Records disposal while retaining the actual deep cleanup behavior.</summary>
        sealed class CountingClip : AnimationClipAsset {
            /// <summary>Gets the number of owner disposal calls.</summary>
            public int DisposeCalls { get; private set; }
            /// <summary>Records the owner release and clears the inherited clip tree.</summary>
            public override void Dispose() {
                DisposeCalls++;
                base.Dispose();
            }
        }
    }
}

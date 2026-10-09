using helengine.files;
using helengine.timeline.runtime;

namespace helengine.timeline.tests {
    /// <summary>
    /// Verifies the cooked timeline binary form: the tools writer and the packaged runtime reader agree, a game content
    /// manager loads it after the runtime module registers, and broken cooked data is rejected before writing.
    /// </summary>
    public class CookedTimelineAssetPayloadSerializerTests {
        /// <summary>
        /// Registration makes the cooked serializer resolvable for value kind 15.
        /// </summary>
        [Fact]
        public void Register_resolvesCookedTimelinesToValueKind15() {
            TimelineSerialization.Register();

            IEditorAssetPayloadSerializer serializer = EditorAssetPayloadSerializerRegistry.FindByAsset(new CookedTimelineAsset());

            Assert.IsType<CookedTimelineAssetPayloadSerializer>(serializer);
            Assert.Equal((EditorAssetBinaryValueKind)15, serializer.ValueKind);
            Assert.Same(serializer, EditorAssetPayloadSerializerRegistry.FindByValueKind(EditorAssetBinaryValueKind.CookedTimelineAsset));
        }

        /// <summary>
        /// Every field survives the editor round trip and the packaged runtime reader reads the same bytes identically.
        /// </summary>
        [Fact]
        public void SerializeThenRead_editorAndPackagedReadersAgree() {
            TimelineSerialization.Register();
            CookedTimelineAsset original = Sample();
            byte[] bytes = Serialize(original);

            CookedTimelineAsset editorCopy;
            using (MemoryStream stream = new MemoryStream(bytes)) {
                editorCopy = Assert.IsType<CookedTimelineAsset>(EditorAssetBinarySerializer.Deserialize(stream));
            }
            CookedTimelineAsset packagedCopy;
            using (MemoryStream stream = new MemoryStream(bytes)) {
                packagedCopy = CookedTimelineAssetReader.Deserialize(stream);
            }

            Assert.Equal(CookedTimelineDump.Describe(original), CookedTimelineDump.Describe(editorCopy));
            Assert.Equal(CookedTimelineDump.Describe(original), CookedTimelineDump.Describe(packagedCopy));
            Assert.Equal("timelines/cooked_sample", packagedCopy.Id);
            Assert.Equal(original.RuntimeAssetId, packagedCopy.RuntimeAssetId);
            Assert.NotEqual(0ul, packagedCopy.RuntimeAssetId);
        }

        /// <summary>
        /// A game core loads a <c>.hctimeline</c> file through its content manager once the runtime module registered.
        /// </summary>
        [Fact]
        public void ContentManager_loadsCookedFileAfterRuntimeRegistration() {
            TimelineSerialization.Register();
            string root = Path.Combine(AppContext.BaseDirectory, "cooked-timeline-content");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "intro" + CookedTimelineAsset.FileExtension), Serialize(Sample()));
            using Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(root) });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            TimelineRuntimeRegistration.Register(core);
            CookedTimelineAsset loaded = core.GetContentManager().Load<CookedTimelineAsset>("intro" + CookedTimelineAsset.FileExtension);

            Assert.Equal(CookedTimelineDump.Describe(Sample()), CookedTimelineDump.Describe(loaded));
        }

        /// <summary>
        /// The packaged reader refuses a payload of another value kind.
        /// </summary>
        [Fact]
        public void PackagedReader_rejectsOtherValueKinds() {
            TimelineSerialization.Register();
            using MemoryStream stream = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(stream, TimelineSamples.Pop());
            stream.Position = 0;

            InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => CookedTimelineAssetReader.Deserialize(stream));

            Assert.Contains("not a cooked timeline", failure.Message);
        }

        /// <summary>
        /// Overlapping segments are rejected before any byte is written.
        /// </summary>
        [Fact]
        public void Serialize_overlappingSegments_isRejected() {
            TimelineSerialization.Register();
            CookedTimelineAsset asset = Sample();
            asset.Tracks[0].Segments[1].StartTick = 5;
            using MemoryStream stream = new MemoryStream();

            Assert.Throws<InvalidOperationException>(() => EditorAssetBinarySerializer.Serialize(stream, asset));
            Assert.Equal(0, stream.Length);
        }

        /// <summary>
        /// Writes an asset with the editor serializer.
        /// </summary>
        /// <param name="asset">Asset to write.</param>
        /// <returns>The bytes.</returns>
        static byte[] Serialize(CookedTimelineAsset asset) {
            using MemoryStream stream = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(stream, asset);
            return stream.ToArray();
        }

        /// <summary>
        /// Builds a cooked timeline using every track kind and awkward float values.
        /// </summary>
        /// <returns>The cooked timeline.</returns>
        static CookedTimelineAsset Sample() {
            return new CookedTimelineAsset {
                Id = "timelines/cooked_sample",
                AuthoringAssetId = new string('c', 32),
                TickRate = 50,
                DurationTicks = 180,
                SlotNames = new string[] { "hero", "lamp" },
                Strings = new string[] { "", "shake", "0.3" },
                Tracks = new CookedTimelineTrack[] {
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Transform, SlotIndex = 0, ChannelIndex = (int)CookedTimelineTransformChannel.RotationZ, Mode = CookedTimelineTransformMode.Offset,
                        Segments = new CookedTimelineSegment[] {
                            new CookedTimelineSegment { StartTick = 0, EndTick = 10, From = 0.1f, To = -4.5f, CurveCode = CurveCatalog.EaseOutBackCode },
                            new CookedTimelineSegment { StartTick = 10, EndTick = 10, From = 1f / 3f, To = 1f / 3f }
                        }
                    },
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Value, SlotIndex = 1, ReceiverId = 12, ChannelIndex = 1,
                        Segments = new CookedTimelineSegment[] { new CookedTimelineSegment { StartTick = 3, EndTick = 40, From = 0, To = 1, CurveCode = CurveCatalog.SmoothstepCode } }
                    },
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Activation, SlotIndex = 0,
                        Intervals = new CookedTimelineInterval[] { new CookedTimelineInterval { StartTick = 0, EndTick = 90 }, new CookedTimelineInterval { StartTick = 100, EndTick = 180 } }
                    },
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Audio,
                        AudioClips = new CookedTimelineAudioClip[] { new CookedTimelineAudioClip { StartTick = 20, EndTick = 45, ClipInTicks = 3, Gain = 0.8f, Audio = SceneAssetReferenceFactory.CreateFileSystemAudio("assets/sfx/whoosh.wav") } }
                    },
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Animation, SlotIndex = 0,
                        AnimationClips = new CookedTimelineAnimationClip[] { new CookedTimelineAnimationClip { StartTick = 0, EndTick = 100, ClipInTicks = 5, Speed = 1.25f, Animation = SceneAssetReferenceFactory.CreateFileSystemAnimationClip("assets/anim/idle.hanim") } }
                    },
                    new CookedTimelineTrack {
                        Kind = CookedTimelineTrackKind.Event,
                        Markers = new CookedTimelineMarker[] { new CookedTimelineMarker { Tick = 133, NameIndex = 1, ValueIndex = 2 } }
                    }
                }
            };
        }
    }
}

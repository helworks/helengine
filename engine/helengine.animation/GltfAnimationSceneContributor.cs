using helengine.animation.runtime;
using helengine.editor;

namespace helengine.animation {
    /// <summary>
    /// Scene contributor of the animation plugin. Every imported animation becomes a <c>.hanimclip</c> under <c>anim/</c> in
    /// the generated folder, with one track per channel targeting the channel's node path and key times in seconds. The first
    /// animation also gets a paused, non-looping <see cref="HierarchyAnimationPlayerComponent"/> on the blueprint root, whose
    /// track targets reference the blueprint entities of the tracks in track order.
    /// </summary>
    public sealed class GltfAnimationSceneContributor : IImportedSceneContributor {
        /// <summary>
        /// Identifier of this contributor.
        /// </summary>
        public const string AnimationContributorId = "helengine.animation.gltf";

        /// <summary>
        /// Folder inside the generated folder that holds the clips.
        /// </summary>
        const string ClipFolder = "anim";

        /// <summary>
        /// Gets the stable identifier of this contributor.
        /// </summary>
        public string ContributorId => AnimationContributorId;

        /// <summary>
        /// Adds one clip per animation and the player for the first one.
        /// </summary>
        /// <param name="scene">Neutral scene data read from the source file.</param>
        /// <param name="output">Output whose entities the core contributor already created.</param>
        public void Contribute(ImportedSceneData scene, ImportedSceneOutput output) {
            if (scene == null) {
                throw new ArgumentNullException(nameof(scene));
            } else if (output == null) {
                throw new ArgumentNullException(nameof(output));
            }

            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < scene.Animations.Length; index++) {
                ImportedSceneAnimation animation = scene.Animations[index];
                string name = ImportedSceneNameUtils.Disambiguate(ImportedSceneNameUtils.MakeSafeName(animation.Name), usedNames);
                string path = ClipFolder + "/" + name + HierarchyAnimationClipAsset.FileExtension;
                output.AddGeneratedFile(path, BuildClip(animation));
                output.MarkAnimationConsumed(index);
                if (index == 0) {
                    AddPlayer(animation, path, output);
                }
            }
        }

        /// <summary>
        /// Converts one animation to a clip with one linear track per channel, in channel order.
        /// </summary>
        /// <param name="animation">Imported animation.</param>
        /// <returns>The clip.</returns>
        static HierarchyAnimationClipAsset BuildClip(ImportedSceneAnimation animation) {
            HierarchyAnimationTrack[] tracks = new HierarchyAnimationTrack[animation.Channels.Length];
            for (int index = 0; index < tracks.Length; index++) {
                ImportedSceneAnimationChannel channel = animation.Channels[index];
                HierarchyAnimationTrack track = new HierarchyAnimationTrack {
                    TargetPath = channel.NodePath,
                    Interpolation = AnimationInterpolationMode.Linear,
                    PositionTimes = new double[channel.PositionKeys.Length],
                    PositionValues = new float3[channel.PositionKeys.Length],
                    RotationTimes = new double[channel.RotationKeys.Length],
                    RotationValues = new float4[channel.RotationKeys.Length],
                    ScaleTimes = new double[channel.ScaleKeys.Length],
                    ScaleValues = new float3[channel.ScaleKeys.Length]
                };
                for (int key = 0; key < channel.PositionKeys.Length; key++) {
                    track.PositionTimes[key] = channel.PositionKeys[key].TimeSeconds;
                    track.PositionValues[key] = channel.PositionKeys[key].Value;
                }
                for (int key = 0; key < channel.RotationKeys.Length; key++) {
                    track.RotationTimes[key] = channel.RotationKeys[key].TimeSeconds;
                    track.RotationValues[key] = channel.RotationKeys[key].Value;
                }
                for (int key = 0; key < channel.ScaleKeys.Length; key++) {
                    track.ScaleTimes[key] = channel.ScaleKeys[key].TimeSeconds;
                    track.ScaleValues[key] = channel.ScaleKeys[key].Value;
                }

                tracks[index] = track;
            }

            return new HierarchyAnimationClipAsset {
                Duration = animation.DurationSeconds,
                Tracks = tracks
            };
        }

        /// <summary>
        /// Adds a paused player for one clip to the blueprint root. The clip path is the packaged path of the clip (see HierarchyAnimationClipCooker.GetPackagedPath),
        /// which is where the cooker writes it, and each track target references the blueprint entity of the track's node.
        /// </summary>
        /// <param name="animation">Animation the clip was built from; its channels are the clip's tracks in order.</param>
        /// <param name="clipPathInFolder">Path of the clip inside the generated folder.</param>
        /// <param name="output">Output holding the blueprint entities.</param>
        static void AddPlayer(ImportedSceneAnimation animation, string clipPathInFolder, ImportedSceneOutput output) {
            SceneAssetReference clipReference = output.ReferenceGeneratedFile(clipPathInFolder, AssetEntryKind.File);
            SceneEntityReference[] targets = new SceneEntityReference[animation.Channels.Length];
            for (int index = 0; index < targets.Length; index++) {
                targets[index] = new SceneEntityReference {
                    EntityId = output.GetNodeEntity(animation.Channels[index].NodePath).Id
                };
            }

            HierarchyAnimationPlayerComponent player = new HierarchyAnimationPlayerComponent {
                ClipPath = HierarchyAnimationClipCooker.GetPackagedPath(clipReference.RelativePath),
                TrackTargets = targets,
                Playing = false,
                Loop = false
            };
            SceneEntityAsset root = output.GetNodeEntity(string.Empty);
            SceneComponentAssetRecord[] existing = root.Components ?? Array.Empty<SceneComponentAssetRecord>();
            SceneComponentAssetRecord[] components = new SceneComponentAssetRecord[existing.Length + 1];
            Array.Copy(existing, components, existing.Length);
            components[existing.Length] = new ComponentPersistenceRegistry()
                .GetDescriptor(player)
                .SerializeComponent(player, existing.Length, new EntityComponentSaveState());
            root.Components = components;
        }
    }
}

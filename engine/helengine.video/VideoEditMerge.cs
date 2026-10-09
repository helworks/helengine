using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Combines an AI proposal with the current edit so that everything carrying a kept origin survives re-planning. By
    /// default only what a person locked (<c>"by": "human"</c>) is kept; incremental passes can also keep earlier AI work
    /// (<c>"by": "ai"</c>). Kept scene objects (duration, take, entry, voice, arrangement), layers, motions, overlays, audio tracks and caption overrides are copied from
    /// the current edit into the proposal, re-inserted when the proposal dropped them. Other content follows the proposal.
    /// </summary>
    public static class VideoEditMerge {
        /// <summary>
        /// Lock marker value that protects an object from AI re-planning.
        /// </summary>
        public const string Human = "human";

        /// <summary>
        /// Lock marker value for objects written by an earlier AI pass, kept when an incremental re-plan must not disturb them.
        /// </summary>
        public const string Ai = "ai";

        /// <summary>
        /// Origins kept by the two-argument re-plan: only what a person locked.
        /// </summary>
        static readonly string[] HumanOnly = [Human];

        /// <summary>
        /// Returns the proposal with every human-locked object of the current edit restored.
        /// </summary>
        /// <param name="current">Edit as it is now, including human locks.</param>
        /// <param name="proposal">New AI proposal.</param>
        /// <returns>New edit; neither input is modified.</returns>
        public static VideoEdit Replan(VideoEdit current, VideoEdit proposal) {
            return Replan(current, proposal, HumanOnly);
        }

        /// <summary>
        /// Returns the proposal with every object of the current edit restored whose origin is one of the kept origins.
        /// </summary>
        /// <param name="current">Edit as it is now, including origin markers.</param>
        /// <param name="proposal">New AI proposal.</param>
        /// <param name="keptOrigins">Origin markers (for example <see cref="Human"/> and <see cref="Ai"/>) whose objects survive.</param>
        /// <returns>New edit; neither input is modified.</returns>
        public static VideoEdit Replan(VideoEdit current, VideoEdit proposal, IReadOnlyCollection<string> keptOrigins) {
            if (keptOrigins == null) {
                throw new ArgumentNullException(nameof(keptOrigins));
            }
            if (current == null) {
                throw new ArgumentNullException(nameof(current));
            }
            if (proposal == null) {
                throw new ArgumentNullException(nameof(proposal));
            }
            VideoEdit result = Clone(proposal);
            for (int index = 0; index < current.Scenes.Count; index++) {
                VideoScene locked = current.Scenes[index];
                VideoScene target = result.Scenes.FirstOrDefault(scene => scene.Id == locked.Id);
                if (target == null) {
                    if (HasKept(locked, keptOrigins)) {
                        result.Scenes.Insert(Math.Min(index, result.Scenes.Count), Clone(locked));
                    }
                    continue;
                }
                MergeScene(locked, target, keptOrigins);
            }
            for (int index = 0; index < current.Tracks.Audio.Count; index++) {
                VideoAudioTrack track = current.Tracks.Audio[index];
                if (IsKept(track.By, keptOrigins)) {
                    ReplaceOrInsert(result.Tracks.Audio, Clone(track), item => item.Id == track.Id, index);
                }
            }
            List<VideoCaptionOverride> overrides = current.Tracks.Captions?.Overrides.Where(item => IsKept(item.By, keptOrigins)).ToList() ?? [];
            if (overrides.Count > 0) {
                if (result.Tracks.Captions == null) {
                    result.Tracks.Captions = Clone(current.Tracks.Captions);
                } else {
                    foreach (VideoCaptionOverride item in overrides) {
                        result.Tracks.Captions.Overrides.RemoveAll(other => other.Scene == item.Scene && other.Cue == item.Cue);
                        result.Tracks.Captions.Overrides.Add(Clone(item));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Restores the locked objects of one scene into its proposed counterpart.
        /// </summary>
        /// <param name="locked">Current scene.</param>
        /// <param name="target">Proposed scene, modified in place.</param>
        /// <param name="keptOrigins">Origins to keep.</param>
        static void MergeScene(VideoScene locked, VideoScene target, IReadOnlyCollection<string> keptOrigins) {
            if (IsKept(locked.Duration?.By, keptOrigins)) {
                target.Duration = Clone(locked.Duration);
            }
            if (IsKept(locked.Take?.By, keptOrigins)) {
                target.Take = Clone(locked.Take);
            }
            if (IsKept(locked.Entry?.By, keptOrigins)) {
                target.Entry = Clone(locked.Entry);
            }
            if (IsKept(locked.Voice?.By, keptOrigins)) {
                target.Voice = Clone(locked.Voice);
            }
            if (IsKept(locked.Arrangement?.By, keptOrigins)) {
                target.Arrangement = Clone(locked.Arrangement);
            }
            for (int index = 0; index < locked.Layers.Count; index++) {
                VideoLayer layer = locked.Layers[index];
                if (IsKept(layer.By, keptOrigins)) {
                    ReplaceOrInsert(target.Layers, Clone(layer), item => item.Id == layer.Id, index);
                } else if (IsKept(layer.Motion?.By, keptOrigins) && target.Layers.FirstOrDefault(item => item.Id == layer.Id) is VideoLayer proposed) {
                    proposed.Motion = Clone(layer.Motion);
                }
            }
            for (int index = 0; index < locked.Overlays.Count; index++) {
                VideoOverlay overlay = locked.Overlays[index];
                if (IsKept(overlay.By, keptOrigins)) {
                    ReplaceOrInsert(target.Overlays, Clone(overlay), item => item.Id == overlay.Id, index);
                }
            }
        }

        /// <summary>
        /// Reports whether a scene holds any locked object.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="keptOrigins">Origins to keep.</param>
        /// <returns>True when something in the scene carries a kept origin.</returns>
        static bool HasKept(VideoScene scene, IReadOnlyCollection<string> keptOrigins) {
            return IsKept(scene.Duration?.By, keptOrigins) || IsKept(scene.Take?.By, keptOrigins) || IsKept(scene.Entry?.By, keptOrigins) || IsKept(scene.Voice?.By, keptOrigins) || IsKept(scene.Arrangement?.By, keptOrigins)
                || scene.Layers.Any(layer => IsKept(layer.By, keptOrigins) || IsKept(layer.Motion?.By, keptOrigins)) || scene.Overlays.Any(overlay => IsKept(overlay.By, keptOrigins));
        }

        /// <summary>
        /// Reports whether an origin marker is one of the kept origins.
        /// </summary>
        /// <param name="by">Origin marker of an object; null when unmarked.</param>
        /// <param name="keptOrigins">Origins to keep.</param>
        /// <returns>True when the object must survive re-planning.</returns>
        static bool IsKept(string by, IReadOnlyCollection<string> keptOrigins) {
            return by != null && keptOrigins.Contains(by);
        }

        /// <summary>
        /// Replaces the matching item or inserts the locked one at its original position.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Proposed list, modified in place.</param>
        /// <param name="item">Locked item to keep.</param>
        /// <param name="matches">Identity test.</param>
        /// <param name="index">Original index in the current edit.</param>
        static void ReplaceOrInsert<T>(List<T> items, T item, Predicate<T> matches, int index) {
            int existing = items.FindIndex(matches);
            if (existing >= 0) {
                items[existing] = item;
            } else {
                items.Insert(Math.Min(index, items.Count), item);
            }
        }

        /// <summary>
        /// Deep-copies a model object through the edit JSON contract.
        /// </summary>
        /// <typeparam name="T">Model type.</typeparam>
        /// <param name="value">Object to copy.</param>
        /// <returns>Independent copy.</returns>
        static T Clone<T>(T value) {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, VideoEditJson.Options), VideoEditJson.Options);
        }
    }
}

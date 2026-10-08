using System.Text.Json;

namespace helengine.video {
    /// <summary>
    /// Combines an AI proposal with the current edit so that everything a person locked (<c>"by": "human"</c>) survives
    /// re-planning: locked scene objects, layers, motions, overlays, audio tracks and caption overrides are copied from the
    /// current edit into the proposal, re-inserted when the proposal dropped them. Unlocked content follows the proposal.
    /// </summary>
    public static class VideoEditMerge {
        /// <summary>
        /// Lock marker value that protects an object from AI re-planning.
        /// </summary>
        public const string Human = "human";

        /// <summary>
        /// Returns the proposal with every locked object of the current edit restored.
        /// </summary>
        /// <param name="current">Edit as it is now, including human locks.</param>
        /// <param name="proposal">New AI proposal.</param>
        /// <returns>New edit; neither input is modified.</returns>
        public static VideoEdit Replan(VideoEdit current, VideoEdit proposal) {
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
                    if (HasLocks(locked)) {
                        result.Scenes.Insert(Math.Min(index, result.Scenes.Count), Clone(locked));
                    }
                    continue;
                }
                MergeScene(locked, target);
            }
            for (int index = 0; index < current.Tracks.Audio.Count; index++) {
                VideoAudioTrack track = current.Tracks.Audio[index];
                if (track.By == Human) {
                    ReplaceOrInsert(result.Tracks.Audio, Clone(track), item => item.Id == track.Id, index);
                }
            }
            List<VideoCaptionOverride> overrides = current.Tracks.Captions?.Overrides.Where(item => item.By == Human).ToList() ?? [];
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
        static void MergeScene(VideoScene locked, VideoScene target) {
            if (locked.Duration?.By == Human) {
                target.Duration = Clone(locked.Duration);
            }
            if (locked.Take?.By == Human) {
                target.Take = Clone(locked.Take);
            }
            if (locked.Entry?.By == Human) {
                target.Entry = Clone(locked.Entry);
            }
            if (locked.Voice?.By == Human) {
                target.Voice = Clone(locked.Voice);
            }
            for (int index = 0; index < locked.Layers.Count; index++) {
                VideoLayer layer = locked.Layers[index];
                if (layer.By == Human) {
                    ReplaceOrInsert(target.Layers, Clone(layer), item => item.Id == layer.Id, index);
                } else if (layer.Motion?.By == Human && target.Layers.FirstOrDefault(item => item.Id == layer.Id) is VideoLayer proposed) {
                    proposed.Motion = Clone(layer.Motion);
                }
            }
            for (int index = 0; index < locked.Overlays.Count; index++) {
                VideoOverlay overlay = locked.Overlays[index];
                if (overlay.By == Human) {
                    ReplaceOrInsert(target.Overlays, Clone(overlay), item => item.Id == overlay.Id, index);
                }
            }
        }

        /// <summary>
        /// Reports whether a scene holds any locked object.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <returns>True when something in the scene is locked.</returns>
        static bool HasLocks(VideoScene scene) {
            return scene.Duration?.By == Human || scene.Take?.By == Human || scene.Entry?.By == Human || scene.Voice?.By == Human
                || scene.Layers.Any(layer => layer.By == Human || layer.Motion?.By == Human) || scene.Overlays.Any(overlay => overlay.By == Human);
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

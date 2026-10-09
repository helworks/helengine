using System.Text.Json;
using helengine.timeline;

namespace helengine.video {
    /// <summary>
    /// Validates an overlay timeline: the definition is an inline <c>helengine.timeline.v1</c> timeline that is valid on
    /// its own (its diagnostics come back as <c>invalid_timeline</c> with paths into the definition), uses only what a
    /// video can show (text, media and rect slots; transform, opacity, reveal-on-rect, activation, event and inline nested
    /// timeline tracks), binds every slot exactly once to an element of its kind and maps its cues to valid moments.
    /// Messages name the fix so a planner can correct itself.
    /// </summary>
    public static class VideoTimelineValidator {
        /// <summary>
        /// Longest text a binding may show.
        /// </summary>
        public const int MaxTextLength = 120;

        /// <summary>
        /// Largest rectangle side, in box units, before scaling.
        /// </summary>
        public const double MaxRectSide = 2;

        /// <summary>
        /// Value channels a video timeline may drive.
        /// </summary>
        public static readonly string[] ValueChannels = [TimelineKnownChannels.Opacity, TimelineKnownChannels.Reveal];

        /// <summary>
        /// Slot kinds a video can bind.
        /// </summary>
        public static readonly string[] SlotKinds = ["text", "media", "rect"];

        /// <summary>
        /// Validates one overlay timeline.
        /// </summary>
        /// <param name="scene">Owning scene.</param>
        /// <param name="timeline">Overlay timeline.</param>
        /// <param name="path">JSON path of the overlay timeline.</param>
        /// <param name="media">Edit media by id.</param>
        /// <param name="errors">Diagnostics sink.</param>
        public static void Validate(VideoScene scene, VideoOverlayTimeline timeline, string path, Dictionary<string, VideoMedia> media, List<VideoDiagnostic> errors) {
            if (timeline.Definition.HasValue && timeline.Library != null) {
                Error(errors, scene, path, "Give either definition or library, not both; the host replaces library with the definition it references.");
                return;
            } else if (timeline.Library != null) {
                errors.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "unresolved_timeline", scene.Id, path + ".library", $"Library timeline '{timeline.Library.Id}' v{timeline.Library.Version} must be inlined as definition by the host before validation."));
                return;
            } else if (!timeline.Definition.HasValue || timeline.Definition.Value.ValueKind != JsonValueKind.Object) {
                Error(errors, scene, path + ".definition", "A timeline overlay needs definition: an inline helengine.timeline.v1 timeline object.");
                return;
            }
            TimelineAsset asset = Parse(timeline.Definition.Value, out IReadOnlyList<TimelineDiagnostic> diagnostics);
            if (asset == null) {
                foreach (TimelineDiagnostic diagnostic in diagnostics) {
                    Error(errors, scene, DefinitionPath(path, diagnostic.Path), diagnostic.Message);
                }
                return;
            }
            ValidateContent(scene, asset, path + ".definition", "", errors);
            ValidateBindings(scene, asset, timeline, path, media, errors);
            foreach (KeyValuePair<string, VideoMoment> cue in timeline.Cues) {
                if (!asset.TryGetCueTime(cue.Key, out double _)) {
                    string known = asset.Cues.Count == 0 ? "the timeline declares no cues" : "declared cues: " + string.Join(", ", asset.Cues.Select(item => item.Name));
                    Error(errors, scene, $"{path}.cues.{cue.Key}", $"Unknown cue '{cue.Key}'; {known}.");
                } else {
                    VideoEditValidator.Moment(scene, cue.Value, $"{path}.cues.{cue.Key}", true, errors);
                }
            }
        }

        /// <summary>
        /// Parses and validates a timeline definition.
        /// </summary>
        /// <param name="definition">Inline timeline JSON.</param>
        /// <param name="diagnostics">The located problems when the definition is invalid; empty otherwise.</param>
        /// <returns>The validated timeline, or null when the definition is invalid.</returns>
        public static TimelineAsset Parse(JsonElement definition, out IReadOnlyList<TimelineDiagnostic> diagnostics) {
            try {
                TimelineAsset asset = TimelineJson.Parse(definition, null);
                diagnostics = [];
                return asset;
            } catch (TimelineFormatException exception) {
                diagnostics = exception.Diagnostics;
                return null;
            }
        }

        /// <summary>
        /// Builds the edit path of a location inside the definition.
        /// </summary>
        /// <param name="path">JSON path of the overlay timeline.</param>
        /// <param name="inner">Path inside the timeline JSON; empty for the timeline itself.</param>
        /// <returns>Edit JSON path.</returns>
        public static string DefinitionPath(string path, string inner) {
            return string.IsNullOrEmpty(inner) ? path + ".definition" : path + ".definition." + inner;
        }

        /// <summary>
        /// Rejects the slot kinds, track kinds, channels and references a video cannot show, in a timeline and its inline
        /// nested timelines.
        /// </summary>
        /// <param name="scene">Owning scene.</param>
        /// <param name="asset">Validated timeline.</param>
        /// <param name="root">Edit path of the root definition.</param>
        /// <param name="inner">Path of this timeline inside the root definition; empty for the root.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateContent(VideoScene scene, TimelineAsset asset, string root, string inner, List<VideoDiagnostic> errors) {
            string prefix = inner.Length == 0 ? root + "." : root + "." + inner + ".";
            for (int index = 0; index < asset.Slots.Count; index++) {
                if (asset.Slots[index].Kind == TimelineSlotKind.Entity) {
                    Error(errors, scene, $"{prefix}slots[{index}].kind", $"Slot '{asset.Slots[index].Name}' is an entity slot, which only games can bind; a video binds text, media or rect slots.");
                }
            }
            for (int index = 0; index < asset.Tracks.Count; index++) {
                TimelineTrackAsset track = asset.Tracks[index];
                string trackPath = $"{prefix}tracks[{index}]";
                if (track is TimelineAudioTrackAsset) {
                    Error(errors, scene, trackPath + ".kind", "Audio tracks play engine audio assets, which a video edit cannot reference; remove the track and add the sound as an edit audio track (tracks.audio).");
                } else if (track is TimelineAnimationTrackAsset) {
                    Error(errors, scene, trackPath + ".kind", "Animation tracks drive game entities and have no meaning in a video; remove the track.");
                } else if (track is TimelineValueTrackAsset value) {
                    TimelineSlotAsset slot = asset.FindSlot(value.Slot);
                    if (value.Channel == TimelineKnownChannels.Reveal && slot != null && slot.Kind != TimelineSlotKind.Rect) {
                        Error(errors, scene, trackPath + ".channel", $"The reveal channel only applies to rect slots; '{value.Slot}' is a {TimelineJson.SlotKindName(slot.Kind)} slot. Use opacity or a transform track instead.");
                    } else if (!ValueChannels.Contains(value.Channel)) {
                        Error(errors, scene, trackPath + ".channel", $"Channel '{value.Channel}' has no meaning in a video; use opacity, reveal (rect slots) or a transform track.");
                    }
                } else if (track is TimelineNestedTrackAsset nested) {
                    for (int clip = 0; clip < nested.Clips.Count; clip++) {
                        string clipPath = $"{(inner.Length == 0 ? "" : inner + ".")}tracks[{index}].clips[{clip}]";
                        if (nested.Clips[clip].Definition == null) {
                            Error(errors, scene, $"{root}.{clipPath}.timeline", "A video timeline can only nest inline timelines; replace the timeline reference with a definition.");
                        } else {
                            ValidateContent(scene, nested.Clips[clip].Definition, root, clipPath + ".definition", errors);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks that every slot is bound exactly once to an element of its kind and that every binding names a slot.
        /// </summary>
        /// <param name="scene">Owning scene.</param>
        /// <param name="asset">Validated timeline.</param>
        /// <param name="timeline">Overlay timeline.</param>
        /// <param name="path">JSON path of the overlay timeline.</param>
        /// <param name="media">Edit media by id.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateBindings(VideoScene scene, TimelineAsset asset, VideoOverlayTimeline timeline, string path, Dictionary<string, VideoMedia> media, List<VideoDiagnostic> errors) {
            foreach (TimelineSlotAsset slot in asset.Slots.Where(slot => slot.Kind != TimelineSlotKind.Entity)) {
                if (!timeline.Bindings.ContainsKey(slot.Name)) {
                    Error(errors, scene, path + ".bindings", $"Slot '{slot.Name}' ({TimelineJson.SlotKindName(slot.Kind)}) has no binding; add bindings.{slot.Name} = {Example(slot.Kind)}.");
                }
            }
            foreach (KeyValuePair<string, VideoTimelineBinding> entry in timeline.Bindings) {
                string bindingPath = $"{path}.bindings.{entry.Key}";
                TimelineSlotAsset slot = asset.FindSlot(entry.Key);
                VideoTimelineBinding binding = entry.Value;
                if (slot == null) {
                    Error(errors, scene, bindingPath, $"Unknown slot '{entry.Key}'; declared slots: {string.Join(", ", asset.Slots.Select(item => item.Name))}.");
                    continue;
                } else if (slot.Kind == TimelineSlotKind.Entity) {
                    continue;
                } else if (binding == null || (binding.Text != null ? 1 : 0) + (binding.Media != null ? 1 : 0) + (binding.Rect != null ? 1 : 0) != 1) {
                    Error(errors, scene, bindingPath, $"A binding gives exactly one of text, media or rect; slot '{entry.Key}' needs {Example(slot.Kind)}.");
                    continue;
                }
                string kind = binding.Text != null ? "text" : binding.Media != null ? "media" : "rect";
                if (kind != TimelineJson.SlotKindName(slot.Kind)) {
                    Error(errors, scene, bindingPath, $"Slot '{entry.Key}' is a {TimelineJson.SlotKindName(slot.Kind)} slot but is bound to {kind}; bind it to {Example(slot.Kind)}.");
                    continue;
                }
                if (binding.Size.HasValue && kind != "rect" && (!double.IsFinite(binding.Size.Value) || binding.Size.Value <= 0 || binding.Size.Value > 1)) {
                    Error(errors, scene, bindingPath + ".size", "size is a fraction of the box height, greater than 0 and at most 1.");
                }
                if (binding.Color.HasValue && (kind != "text" || !VideoTimelineColors.TryParse(binding.Color.Value, out string _))) {
                    Error(errors, scene, bindingPath + ".color", kind != "text" ? "Only text bindings take a color; a rect gives rect.color." : "Colors are \"#RRGGBB\", \"#RRGGBBAA\" or [r, g, b(, a)] in 0..1.");
                }
                if (kind == "text" && (string.IsNullOrWhiteSpace(binding.Text) || binding.Text.Length > MaxTextLength)) {
                    Error(errors, scene, bindingPath + ".text", $"A text binding needs text of at most {MaxTextLength} characters.");
                } else if (kind == "media" && (!media.TryGetValue(binding.Media, out VideoMedia source) || source.Kind is not ("image" or "video"))) {
                    Error(errors, scene, bindingPath + ".media", $"'{binding.Media}' is not an image or video in the edit media.");
                } else if (kind == "rect") {
                    ValidateRect(scene, asset, binding, bindingPath, errors);
                }
            }
        }

        /// <summary>
        /// Checks a rectangle binding: no size, a valid color, sides within range and a matched slot that is a text slot.
        /// </summary>
        /// <param name="scene">Owning scene.</param>
        /// <param name="asset">Validated timeline.</param>
        /// <param name="binding">Rect binding.</param>
        /// <param name="path">JSON path of the binding.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateRect(VideoScene scene, TimelineAsset asset, VideoTimelineBinding binding, string path, List<VideoDiagnostic> errors) {
            VideoTimelineRect rect = binding.Rect;
            if (binding.Size.HasValue) {
                Error(errors, scene, path + ".size", "A rect is sized by rect.width and rect.height, not size.");
            }
            if (!VideoTimelineColors.TryParse(rect.Color, out string _)) {
                Error(errors, scene, path + ".rect.color", "A rect needs color: \"#RRGGBB\", \"#RRGGBBAA\" or [r, g, b(, a)] in 0..1.");
            }
            if (rect.Width.HasValue && (!double.IsFinite(rect.Width.Value) || rect.Width.Value <= 0 || rect.Width.Value > MaxRectSide)) {
                Error(errors, scene, path + ".rect.width", $"rect.width is a fraction of the box width, greater than 0 and at most {MaxRectSide}.");
            }
            if (rect.Height.HasValue && (!double.IsFinite(rect.Height.Value) || rect.Height.Value <= 0 || rect.Height.Value > MaxRectSide)) {
                Error(errors, scene, path + ".rect.height", $"rect.height is a fraction of the box height, greater than 0 and at most {MaxRectSide}.");
            }
            if (rect.Match != null && asset.FindSlot(rect.Match)?.Kind != TimelineSlotKind.Text) {
                Error(errors, scene, path + ".rect.match", $"rect.match names the text slot whose width the rect takes; '{rect.Match}' is not a text slot.");
            } else if (rect.Match != null && rect.Width.HasValue) {
                Error(errors, scene, path + ".rect", "Give rect.width or rect.match, not both.");
            }
        }

        /// <summary>
        /// Shows the binding shape a slot kind needs, for messages.
        /// </summary>
        /// <param name="kind">Slot kind.</param>
        /// <returns>Example binding.</returns>
        static string Example(TimelineSlotKind kind) {
            return kind switch {
                TimelineSlotKind.Text => "{\"text\": \"...\"}",
                TimelineSlotKind.Media => "{\"media\": \"<image or video id>\"}",
                TimelineSlotKind.Rect => "{\"rect\": {\"color\": \"#FFD400\"}}",
                _ => "nothing (entity slots cannot be bound in a video)"
            };
        }

        /// <summary>
        /// Adds one <c>invalid_timeline</c> error.
        /// </summary>
        /// <param name="errors">Diagnostics sink.</param>
        /// <param name="scene">Owning scene.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="message">Explanation.</param>
        static void Error(List<VideoDiagnostic> errors, VideoScene scene, string path, string message) {
            errors.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "invalid_timeline", scene.Id, path, message));
        }
    }
}

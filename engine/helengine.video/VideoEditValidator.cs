using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Structural validation of an edit document against itself and the engine capability catalog: identities and
    /// references, take and duration sources, moments, entries, layouts, effects, animations and caption settings.
    /// Timing problems that depend on analysis (unresolved words, missing handles) are left to the compiler.
    /// </summary>
    public static class VideoEditValidator {
        /// <summary>
        /// Effects every scene may use as its entry without a catalog transition.
        /// </summary>
        static readonly string[] BuiltInEntries = ["cut", "appear"];

        /// <summary>
        /// Layout presets known to the compiler.
        /// </summary>
        static readonly string[] LayoutPresets = ["full_frame", "inset", "side_by_side"];

        /// <summary>
        /// Validates one edit.
        /// </summary>
        /// <param name="edit">Edit to check.</param>
        /// <param name="capabilities">Engine catalog the edit must respect.</param>
        /// <returns>Error diagnostics; empty when the edit is valid.</returns>
        public static IReadOnlyList<VideoDiagnostic> Validate(VideoEdit edit, MediaCapabilities capabilities) {
            if (edit == null) {
                throw new ArgumentNullException(nameof(edit));
            }
            if (capabilities == null) {
                throw new ArgumentNullException(nameof(capabilities));
            }
            List<VideoDiagnostic> errors = new List<VideoDiagnostic>();
            if (edit.Schema != VideoEditJson.SchemaId) {
                Error(errors, "invalid_schema", null, "schema", $"Expected schema '{VideoEditJson.SchemaId}'.");
            }
            if (edit.Format == null || edit.Format.Width <= 0 || edit.Format.Height <= 0 || edit.Format.FrameRate == null || edit.Format.FrameRate.Numerator <= 0 || edit.Format.FrameRate.Denominator <= 0) {
                Error(errors, "invalid_format", null, "format", "The output format needs a positive size and frame rate.");
            }
            Dictionary<string, VideoMedia> media = new Dictionary<string, VideoMedia>(StringComparer.Ordinal);
            for (int index = 0; index < edit.Media.Count; index++) {
                VideoMedia item = edit.Media[index];
                if (string.IsNullOrWhiteSpace(item?.Id) || !media.TryAdd(item.Id, item)) {
                    Error(errors, "duplicate_id", null, $"media[{index}].id", "Media ids must be present and unique.");
                }
                if (item != null && item.Kind is not ("video" or "audio" or "image" or "font")) {
                    Error(errors, "invalid_media", null, $"media[{index}].kind", "Media kind must be video, audio, image or font.");
                }
            }
            HashSet<string> sceneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < edit.Scenes.Count; index++) {
                VideoScene scene = edit.Scenes[index];
                if (string.IsNullOrWhiteSpace(scene?.Id) || !sceneIds.Add(scene.Id)) {
                    Error(errors, "duplicate_id", scene?.Id, $"scenes[{index}].id", "Scene ids must be present and unique.");
                    continue;
                }
                ValidateScene(edit, scene, index, media, capabilities, errors);
            }
            ValidateTracks(edit, sceneIds, media, capabilities, errors);
            return errors;
        }

        /// <summary>
        /// Validates one scene.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="scene">Scene.</param>
        /// <param name="index">Scene index.</param>
        /// <param name="media">Media by id.</param>
        /// <param name="capabilities">Engine catalog.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateScene(VideoEdit edit, VideoScene scene, int index, Dictionary<string, VideoMedia> media, MediaCapabilities capabilities, List<VideoDiagnostic> errors) {
            string path = $"scenes[{index}]";
            Lock(errors, scene, path + ".duration", scene.Duration?.By);
            Lock(errors, scene, path + ".take", scene.Take?.By);
            double? length = null;
            if (scene.Take != null) {
                if (!media.TryGetValue(scene.Take.Media ?? "", out VideoMedia take) || take.Kind is not ("video" or "audio")) {
                    Error(errors, "missing_media", scene.Id, path + ".take.media", "The take must reference a video or audio media.");
                } else if (!double.IsFinite(scene.Take.InSec) || !double.IsFinite(scene.Take.OutSec) || scene.Take.InSec < 0 || scene.Take.OutSec <= scene.Take.InSec || (take.DurationSec > 0 && scene.Take.OutSec > take.DurationSec)) {
                    Error(errors, "invalid_take", scene.Id, path + ".take", "The take interval must be positive and inside its media.");
                } else {
                    length = scene.Take.OutSec - scene.Take.InSec;
                }
            }
            string mode = scene.Duration?.Mode;
            if (mode == "from_take") {
                if (scene.Take == null) {
                    Error(errors, "invalid_duration", scene.Id, path + ".duration", "from_take needs a take.");
                }
            } else if (mode is "fixed" or "estimate") {
                double sec = scene.Duration.Sec ?? double.NaN;
                if (!double.IsFinite(sec) || sec <= 0) {
                    Error(errors, "invalid_duration", scene.Id, path + ".duration.sec", "fixed and estimate durations need positive seconds.");
                } else {
                    length = sec;
                }
            } else {
                Error(errors, "invalid_duration", scene.Id, path + ".duration.mode", "Duration mode must be from_take, fixed or estimate.");
            }
            ValidateEntry(scene, index, length, capabilities, errors);
            if (scene.Voice != null) {
                Lock(errors, scene, path + ".voice", scene.Voice.By);
                if (!double.IsFinite(scene.Voice.Gain) || scene.Voice.Gain < 0) {
                    Error(errors, "invalid_voice", scene.Id, path + ".voice.gain", "Voice gain must be zero or more.");
                }
                for (int envelope = 0; envelope < scene.Voice.Envelopes.Count; envelope++) {
                    ValidateEnvelope(scene, scene.Voice.Envelopes[envelope], $"{path}.voice.envelopes[{envelope}]", errors);
                }
            }
            HashSet<string> layerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; layerIndex < scene.Layers.Count; layerIndex++) {
                VideoLayer layer = scene.Layers[layerIndex];
                string layerPath = $"{path}.layers[{layerIndex}]";
                if (string.IsNullOrWhiteSpace(layer?.Id) || !layerIds.Add(layer.Id)) {
                    Error(errors, "duplicate_id", scene.Id, layerPath + ".id", "Layer ids must be present and unique within the scene.");
                    continue;
                }
                ValidateLayer(scene, layer, layerPath, media, capabilities, errors);
            }
            HashSet<string> overlayIds = new HashSet<string>(StringComparer.Ordinal);
            for (int overlayIndex = 0; overlayIndex < scene.Overlays.Count; overlayIndex++) {
                VideoOverlay overlay = scene.Overlays[overlayIndex];
                string overlayPath = $"{path}.overlays[{overlayIndex}]";
                if (string.IsNullOrWhiteSpace(overlay?.Id) || !overlayIds.Add(overlay.Id)) {
                    Error(errors, "duplicate_id", scene.Id, overlayPath + ".id", "Overlay ids must be present and unique within the scene.");
                    continue;
                }
                Lock(errors, scene, overlayPath, overlay.By);
                if (string.IsNullOrWhiteSpace(overlay.Text) || overlay.Style is not ("graphic" or "caption")) {
                    Error(errors, "invalid_overlay", scene.Id, overlayPath, "Overlays need text and a graphic or caption style.");
                }
                Moment(scene, overlay.At, overlayPath + ".at", true, errors);
                Moment(scene, overlay.Until, overlayPath + ".until", false, errors);
            }
        }

        /// <summary>
        /// Validates the scene entry.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="index">Scene index.</param>
        /// <param name="length">Scene length when already known.</param>
        /// <param name="capabilities">Engine catalog.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateEntry(VideoScene scene, int index, double? length, MediaCapabilities capabilities, List<VideoDiagnostic> errors) {
            VideoEntry entry = scene.Entry;
            if (entry == null) {
                return;
            }
            string path = $"scenes[{index}].entry";
            Lock(errors, scene, path, entry.By);
            if (BuiltInEntries.Contains(entry.Effect)) {
                if (entry.Effect == "appear" && (!double.IsFinite(entry.DurationSec) || entry.DurationSec <= 0)) {
                    Error(errors, "invalid_entry", scene.Id, path + ".duration_sec", "An appear entry needs a positive duration.");
                }
                if (entry.Parameters.Count > 0) {
                    Error(errors, "invalid_entry", scene.Id, path + ".parameters", "cut and appear take no parameters.");
                }
                return;
            }
            MediaEffectDescriptor descriptor = capabilities.Effects.FirstOrDefault(effect => effect.Id == entry.Effect && effect.Version == entry.Version);
            if (descriptor == null || descriptor.Category != "transition") {
                Error(errors, "invalid_entry", scene.Id, path + ".effect", $"'{entry.Effect}' v{entry.Version} is not a catalog transition.");
                return;
            }
            if (index == 0) {
                Error(errors, "invalid_entry", scene.Id, path, "The first scene cannot transition from a previous scene.");
            }
            if (!double.IsFinite(entry.DurationSec) || entry.DurationSec <= 0 || (length.HasValue && entry.DurationSec > length.Value)) {
                Error(errors, "invalid_entry", scene.Id, path + ".duration_sec", "A transition needs a positive duration no longer than its scene.");
            }
            if (!double.IsFinite(entry.AudioFadeSec) || entry.AudioFadeSec < 0 || entry.AudioFadeSec > 2) {
                Error(errors, "invalid_entry", scene.Id, path + ".audio_fade_sec", "The voice fade must be between 0 and 2 seconds.");
            }
            foreach (KeyValuePair<string, System.Text.Json.JsonElement> parameter in entry.Parameters) {
                if (!descriptor.Parameters.TryGetValue(parameter.Key, out MediaParameterDescriptor shape) || !shape.Accepts(parameter.Value)) {
                    Error(errors, "invalid_entry", scene.Id, $"{path}.parameters.{parameter.Key}", $"Parameter '{parameter.Key}' is unknown or outside its range.");
                }
            }
        }

        /// <summary>
        /// Validates one layer.
        /// </summary>
        /// <param name="scene">Owning scene.</param>
        /// <param name="layer">Layer.</param>
        /// <param name="path">Layer JSON path.</param>
        /// <param name="media">Media by id.</param>
        /// <param name="capabilities">Engine catalog.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateLayer(VideoScene scene, VideoLayer layer, string path, Dictionary<string, VideoMedia> media, MediaCapabilities capabilities, List<VideoDiagnostic> errors) {
            Lock(errors, scene, path, layer.By);
            if (layer.Kind == "take") {
                if (scene.Take == null) {
                    Error(errors, "invalid_layer", scene.Id, path + ".kind", "A take layer needs a scene take.");
                }
            } else if (layer.Kind == "media") {
                if (!media.TryGetValue(layer.Media ?? "", out VideoMedia source) || source.Kind is not ("image" or "video")) {
                    Error(errors, "missing_media", scene.Id, path + ".media", "A media layer must reference an image or video.");
                }
            } else if (layer.Kind == "text") {
                if (string.IsNullOrWhiteSpace(layer.Text) || layer.TextStyle is not ("caption" or "graphic")) {
                    Error(errors, "invalid_layer", scene.Id, path, "A text layer needs text and a caption or graphic style.");
                }
            } else {
                Error(errors, "invalid_layer", scene.Id, path + ".kind", "Layer kind must be take, media or text.");
            }
            if (layer.Fit is not ("contain" or "cover")) {
                Error(errors, "invalid_layer", scene.Id, path + ".fit", "Fit must be contain or cover.");
            }
            if (layer.Layout != null) {
                bool preset = layer.Layout.Preset != null, viewport = layer.Layout.Viewport != null;
                if (preset == viewport || (preset && !LayoutPresets.Contains(layer.Layout.Preset))) {
                    Error(errors, "invalid_layout", scene.Id, path + ".layout", "Layout needs exactly one known preset or a viewport.");
                } else if (viewport) {
                    VideoViewport box = layer.Layout.Viewport;
                    if (box.X < 0 || box.Y < 0 || box.Width <= 0 || box.Height <= 0 || box.X + box.Width > 1 || box.Y + box.Height > 1) {
                        Error(errors, "invalid_layout", scene.Id, path + ".layout.viewport", "The viewport must stay inside the frame.");
                    }
                }
            }
            if (layer.Motion != null) {
                Lock(errors, scene, path + ".motion", layer.Motion.By);
                if (layer.Motion.Preset is not ("none" or "zoom_to_focus")) {
                    Error(errors, "invalid_motion", scene.Id, path + ".motion.preset", "Motion preset must be none or zoom_to_focus.");
                } else if (layer.Motion.Preset == "zoom_to_focus") {
                    MediaParameterDescriptor zoom = capabilities.LayerProperties["zoom"];
                    if (layer.Motion.FromScale < zoom.Minimum || layer.Motion.FromScale > zoom.Maximum || layer.Motion.ToScale < zoom.Minimum || layer.Motion.ToScale > zoom.Maximum) {
                        Error(errors, "invalid_motion", scene.Id, path + ".motion", $"Zoom scales must be between {zoom.Minimum} and {zoom.Maximum}.");
                    }
                    if (!capabilities.Curves.Contains(layer.Motion.Curve)) {
                        Error(errors, "invalid_motion", scene.Id, path + ".motion.curve", $"Curve '{layer.Motion.Curve}' is not in the catalog.");
                    }
                    if (layer.Motion.Focus != null && layer.Motion.Focus.Type is not ("center" or "point" or "text_region")) {
                        Error(errors, "invalid_motion", scene.Id, path + ".motion.focus.type", "Focus must be center, point or text_region.");
                    }
                    Moment(scene, layer.Motion.Start, path + ".motion.start", false, errors);
                    Moment(scene, layer.Motion.End, path + ".motion.end", false, errors);
                }
            }
            for (int animation = 0; animation < layer.Animations.Count; animation++) {
                VideoAnimation item = layer.Animations[animation];
                string animationPath = $"{path}.animations[{animation}]";
                if (!capabilities.LayerProperties.TryGetValue(item.Property ?? "", out MediaParameterDescriptor range)) {
                    Error(errors, "invalid_animation", scene.Id, animationPath + ".property", $"'{item.Property}' is not an animatable property.");
                    continue;
                }
                for (int keyframe = 0; keyframe < item.Keyframes.Count; keyframe++) {
                    VideoKeyframe frame = item.Keyframes[keyframe];
                    if (!double.IsFinite(frame.Value) || frame.Value < range.Minimum || frame.Value > range.Maximum || !capabilities.Curves.Contains(frame.Curve)) {
                        Error(errors, "invalid_animation", scene.Id, $"{animationPath}.keyframes[{keyframe}]", "Keyframe value or curve is outside the catalog.");
                    }
                    Moment(scene, frame.At, $"{animationPath}.keyframes[{keyframe}].at", true, errors);
                }
            }
            for (int effect = 0; effect < layer.Effects.Count; effect++) {
                ValidateLayerEffect(scene, layer.Effects[effect], $"{path}.effects[{effect}]", media, capabilities, errors);
            }
            if (layer.Mask != null && (!media.ContainsKey(layer.Mask.Media ?? "") || layer.Mask.Channel is not ("alpha" or "luma"))) {
                Error(errors, "invalid_mask", scene.Id, path + ".mask", "A mask needs existing media and an alpha or luma channel.");
            }
        }

        /// <summary>
        /// Validates one layer effect against the catalog.
        /// </summary>
        /// <param name="scene">Owning scene, or null for caption effects.</param>
        /// <param name="effect">Effect.</param>
        /// <param name="path">Effect JSON path.</param>
        /// <param name="media">Media by id.</param>
        /// <param name="capabilities">Engine catalog.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateLayerEffect(VideoScene scene, VideoEffect effect, string path, Dictionary<string, VideoMedia> media, MediaCapabilities capabilities, List<VideoDiagnostic> errors) {
            MediaEffectDescriptor descriptor = capabilities.Effects.FirstOrDefault(item => item.Id == effect.Id && item.Version == effect.Version);
            if (descriptor == null || descriptor.Category != "layer") {
                Error(errors, "invalid_effect", scene?.Id, path, $"'{effect.Id}' v{effect.Version} is not a catalog layer effect.");
                return;
            }
            foreach (KeyValuePair<string, System.Text.Json.JsonElement> parameter in effect.Parameters) {
                if (!descriptor.Parameters.TryGetValue(parameter.Key, out MediaParameterDescriptor shape) || !shape.Accepts(parameter.Value)) {
                    Error(errors, "invalid_effect", scene?.Id, $"{path}.parameters.{parameter.Key}", $"Parameter '{parameter.Key}' is unknown or outside its range.");
                }
            }
            foreach (string role in descriptor.InputRoles.Where(role => role != descriptor.MainInputRole)) {
                if (!effect.Inputs.TryGetValue(role, out string mediaId) || !media.ContainsKey(mediaId)) {
                    Error(errors, "invalid_effect", scene?.Id, $"{path}.inputs.{role}", $"Input '{role}' needs existing media.");
                }
            }
        }

        /// <summary>
        /// Validates the global tracks.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="sceneIds">Known scene ids.</param>
        /// <param name="media">Media by id.</param>
        /// <param name="capabilities">Engine catalog.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateTracks(VideoEdit edit, HashSet<string> sceneIds, Dictionary<string, VideoMedia> media, MediaCapabilities capabilities, List<VideoDiagnostic> errors) {
            HashSet<string> trackIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < edit.Tracks.Audio.Count; index++) {
                VideoAudioTrack track = edit.Tracks.Audio[index];
                string path = $"tracks.audio[{index}]";
                if (string.IsNullOrWhiteSpace(track?.Id) || !trackIds.Add(track.Id)) {
                    Error(errors, "duplicate_id", null, path + ".id", "Track ids must be present and unique.");
                    continue;
                }
                Lock(errors, null, path, track.By);
                if (!media.TryGetValue(track.Media ?? "", out VideoMedia source) || source.Kind is not ("audio" or "video")) {
                    Error(errors, "missing_media", null, path + ".media", "An audio track must reference audio or video media.");
                }
                if (!double.IsFinite(track.InSec) || track.InSec < 0 || !double.IsFinite(track.Gain) || track.Gain < 0) {
                    Error(errors, "invalid_track", null, path, "Track source start and gain must be zero or more.");
                }
                if (track.Start == null) {
                    Error(errors, "invalid_track", null, path + ".start", "A track needs a start.");
                } else {
                    TrackMoment(track.Start, path + ".start", sceneIds, errors);
                }
                if (track.End != null) {
                    TrackMoment(track.End, path + ".end", sceneIds, errors);
                }
                for (int envelope = 0; envelope < track.Envelopes.Count; envelope++) {
                    ValidateEnvelope(null, track.Envelopes[envelope], $"{path}.envelopes[{envelope}]", errors);
                }
            }
            VideoCaptionTrack captions = edit.Tracks.Captions;
            if (captions != null) {
                if (captions.Source != "voice" || captions.WordsPerCue is < 1 or > 16 || captions.WordsPerLine is < 1 or > 8) {
                    Error(errors, "invalid_captions", null, "tracks.captions", "Captions need source voice, 1-16 words per cue and 1-8 words per line.");
                }
                for (int effect = 0; effect < captions.Effects.Count; effect++) {
                    ValidateLayerEffect(null, captions.Effects[effect], $"tracks.captions.effects[{effect}]", media, capabilities, errors);
                }
                foreach (VideoCaptionOverride item in captions.Overrides) {
                    if (!sceneIds.Contains(item.Scene ?? "") || item.Cue < 0 || string.IsNullOrWhiteSpace(item.Text)) {
                        Error(errors, "invalid_captions", item.Scene, "tracks.captions.overrides", "Caption overrides need an existing scene, a cue index and text.");
                    }
                    Lock(errors, null, "tracks.captions.overrides", item.By);
                }
            }
        }

        /// <summary>
        /// Validates a global track position.
        /// </summary>
        /// <param name="moment">Track position.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="sceneIds">Known scene ids.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void TrackMoment(VideoTrackMoment moment, string path, HashSet<string> sceneIds, List<VideoDiagnostic> errors) {
            if (!sceneIds.Contains(moment.Scene ?? "")) {
                Error(errors, "invalid_track", null, path + ".scene", "Track positions must name an existing scene.");
            }
            Moment(null, moment.At, path + ".at", true, errors);
        }

        /// <summary>
        /// Validates one gain envelope.
        /// </summary>
        /// <param name="scene">Owning scene, or null for tracks.</param>
        /// <param name="envelope">Envelope.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void ValidateEnvelope(VideoScene scene, VideoEnvelope envelope, string path, List<VideoDiagnostic> errors) {
            if (envelope.Type is not ("linear" or "equal_power_in" or "equal_power_out") || !double.IsFinite(envelope.DurationSec) || envelope.DurationSec <= 0
                || !double.IsFinite(envelope.From) || !double.IsFinite(envelope.To) || envelope.From < 0 || envelope.To < 0) {
                Error(errors, "invalid_envelope", scene?.Id, path, "Envelopes need a known type, a positive duration and non-negative gains.");
            }
            Moment(scene, envelope.At, path + ".at", true, errors);
        }

        /// <summary>
        /// Validates that a moment uses exactly one well-formed form.
        /// </summary>
        /// <param name="scene">Owning scene, or null.</param>
        /// <param name="moment">Moment; may be null when optional.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="required">Whether the moment must be present.</param>
        /// <param name="errors">Diagnostics sink.</param>
        static void Moment(VideoScene scene, VideoMoment moment, string path, bool required, List<VideoDiagnostic> errors) {
            if (moment == null) {
                if (required) {
                    Error(errors, "invalid_moment", scene?.Id, path, "A moment is required here.");
                }
                return;
            }
            int forms = (moment.Sec.HasValue ? 1 : 0) + (moment.FromEnd.HasValue ? 1 : 0) + (moment.Fraction.HasValue ? 1 : 0) + (string.IsNullOrWhiteSpace(moment.Word) ? 0 : 1);
            bool finite = (!moment.Sec.HasValue || double.IsFinite(moment.Sec.Value)) && (!moment.FromEnd.HasValue || double.IsFinite(moment.FromEnd.Value))
                && (!moment.Fraction.HasValue || (moment.Fraction.Value >= 0 && moment.Fraction.Value <= 1)) && double.IsFinite(moment.OffsetSec) && moment.Occurrence >= 1;
            if (forms != 1 || !finite) {
                Error(errors, "invalid_moment", scene?.Id, path, "A moment needs exactly one of sec, from_end, fraction (0-1) or word, with finite values.");
            }
        }

        /// <summary>
        /// Validates a lock marker.
        /// </summary>
        /// <param name="errors">Diagnostics sink.</param>
        /// <param name="scene">Owning scene, or null.</param>
        /// <param name="path">JSON path of the locked object.</param>
        /// <param name="by">Lock marker value.</param>
        static void Lock(List<VideoDiagnostic> errors, VideoScene scene, string path, string by) {
            if (by != null && by is not ("human" or "ai")) {
                Error(errors, "invalid_lock", scene?.Id, path + ".by", "by must be human or ai.");
            }
        }

        /// <summary>
        /// Adds one error.
        /// </summary>
        /// <param name="errors">Diagnostics sink.</param>
        /// <param name="code">Code.</param>
        /// <param name="scene">Scene id or null.</param>
        /// <param name="path">JSON path.</param>
        /// <param name="message">Explanation.</param>
        static void Error(List<VideoDiagnostic> errors, string code, string scene, string path, string message) {
            errors.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, code, scene, path, message));
        }
    }
}

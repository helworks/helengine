using System.Text.Json;
using System.Text.Json.Nodes;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Builds the overlay timeline edits the tests share: the "LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR" contrast authored as a
    /// timeline (three stacked terms that pop on their spoken words, two ≠ separators that fade in just before the next
    /// term, a nested pop with a tilt on the last term and a bar that strikes it), placed in the top region of a stack
    /// arrangement over the take of <see cref="GraphicEditSamples.ContrastChain"/>.
    /// </summary>
    static class TimelineEditSamples {
        /// <summary>
        /// The contrast timeline definition: cues a, b and c are moved to the spoken terms.
        /// </summary>
        public const string ContrastDefinition = """
            {
              "schema": "helengine.timeline.v1",
              "id": "contrast_three_terms_video",
              "duration": 3.6,
              "slots": [
                { "name": "term_a", "kind": "text" },
                { "name": "sep_ab", "kind": "text" },
                { "name": "term_b", "kind": "text" },
                { "name": "sep_bc", "kind": "text" },
                { "name": "term_c", "kind": "text" },
                { "name": "strike", "kind": "rect" }
              ],
              "cues": [ { "name": "a", "time": 0.15 }, { "name": "b", "time": 1.2 }, { "name": "c", "time": 2.4 } ],
              "tracks": [
                { "kind": "activation", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 3.45 } ] },
                { "kind": "transform", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 3.45,
                  "position": [ { "time": 0, "value": [0, -0.4, 0] } ],
                  "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },

                { "kind": "activation", "slot": "sep_ab", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 2.6 } ] },
                { "kind": "transform", "slot": "sep_ab", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, -0.2, 0] } ] } ] },
                { "kind": "value", "slot": "sep_ab", "channel": "opacity", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 0.2,
                  "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },

                { "kind": "activation", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 2.4 } ] },
                { "kind": "transform", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 2.4,
                  "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },

                { "kind": "activation", "slot": "sep_bc", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 1.4 } ] },
                { "kind": "transform", "slot": "sep_bc", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.2, 0] } ] } ] },
                { "kind": "value", "slot": "sep_bc", "channel": "opacity", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 0.2,
                  "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },

                { "kind": "activation", "slot": "term_c", "clips": [ { "start": { "cue": "c" }, "duration": 1.2 } ] },
                { "kind": "transform", "slot": "term_c", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.4, 0] } ] } ] },
                { "kind": "timeline", "name": "pop term c", "clips": [ { "start": { "cue": "c" }, "duration": 0.45, "slots": { "target": "term_c" },
                  "definition": {
                    "id": "pop", "duration": 0.45,
                    "slots": [ { "name": "target", "kind": "text" } ],
                    "tracks": [ { "kind": "transform", "slot": "target", "mode": "offset", "clips": [ { "start": 0, "duration": 0.45,
                      "scale": [ { "time": 0, "value": [0.5, 0.5, 1], "curve": "ease_out_back.v1" }, { "time": 0.35, "value": [1, 1, 1] } ],
                      "rotation": [ { "time": 0, "value": [0, 0, -6], "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": [0, 0, 0] } ] } ] } ]
                  } } ] },

                { "kind": "activation", "slot": "strike", "clips": [ { "start": 3, "duration": 0.6 } ] },
                { "kind": "transform", "slot": "strike", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.4, 0] } ] } ] },
                { "kind": "value", "slot": "strike", "channel": "reveal", "clips": [ { "start": 3, "duration": 0.35,
                  "keyframes": [ { "time": 0, "value": 0, "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": 1 } ] } ] },
                { "kind": "event", "markers": [ { "time": { "cue": "c" }, "name": "emphasis" } ] }
              ]
            }
            """;

        /// <summary>
        /// Builds the contrast edit with the timeline overlay in the top region of a stack arrangement.
        /// </summary>
        /// <param name="first">First term.</param>
        /// <param name="second">Second term.</param>
        /// <param name="third">Third term.</param>
        /// <returns>A fresh edit.</returns>
        public static VideoEdit Contrast(string first, string second, string third) {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoScene scene = edit.Scenes[0];
            scene.Arrangement = new VideoArrangement { Preset = "stack" };
            scene.Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Region = "main" }];
            scene.Overlays = [
                new VideoOverlay {
                    Id = "contrast", Region = "top", Text = "",
                    Timeline = new VideoOverlayTimeline {
                        Definition = Definition(ContrastDefinition),
                        Bindings = new(StringComparer.Ordinal) {
                            ["term_a"] = new VideoTimelineBinding { Text = first },
                            ["sep_ab"] = new VideoTimelineBinding { Text = "≠", Size = 0.12, Color = JsonSerializer.SerializeToElement("#FFD400") },
                            ["term_b"] = new VideoTimelineBinding { Text = second },
                            ["sep_bc"] = new VideoTimelineBinding { Text = "≠", Size = 0.12, Color = JsonSerializer.SerializeToElement("#FFD400") },
                            ["term_c"] = new VideoTimelineBinding { Text = third },
                            ["strike"] = new VideoTimelineBinding { Rect = new VideoTimelineRect { Color = JsonSerializer.SerializeToElement("#FF3030"), Height = 0.035, Match = "term_c" } }
                        },
                        Cues = new(StringComparer.Ordinal) {
                            ["a"] = new VideoMoment { Word = "legalizar" },
                            ["b"] = new VideoMoment { Word = "descriminalizar" },
                            ["c"] = new VideoMoment { Word = "tratar" }
                        }
                    }
                }
            ];
            return edit;
        }

        /// <summary>
        /// Builds the contrast edit with the usual terms.
        /// </summary>
        /// <returns>A fresh edit.</returns>
        public static VideoEdit Contrast() {
            return Contrast("LEGALIZAR", "DESCRIMINALIZAR", "TRATAR");
        }

        /// <summary>
        /// Parses timeline JSON into the element an overlay carries.
        /// </summary>
        /// <param name="json">Timeline JSON.</param>
        /// <returns>Detached JSON element.</returns>
        public static JsonElement Definition(string json) {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        /// <summary>
        /// Applies a change to the definition of the first overlay timeline of an edit.
        /// </summary>
        /// <param name="edit">Edit to change.</param>
        /// <param name="change">Change applied to the definition as a mutable node.</param>
        public static void ChangeDefinition(VideoEdit edit, Action<JsonObject> change) {
            VideoOverlayTimeline timeline = edit.Scenes[0].Overlays[0].Timeline;
            JsonObject node = JsonNode.Parse(timeline.Definition.Value.GetRawText()).AsObject();
            change(node);
            timeline.Definition = JsonSerializer.SerializeToElement(node);
        }

        /// <summary>
        /// Creates a compile context with the built-in catalog and a measurer.
        /// </summary>
        /// <param name="measurer">Text measurer.</param>
        /// <returns>Context.</returns>
        public static VideoCompileContext Context(IVideoTextMeasurer measurer) {
            return GraphicEditSamples.Context(measurer);
        }

        /// <summary>
        /// Finds a compiled layer of the contrast overlay by slot name.
        /// </summary>
        /// <param name="composition">Compiled composition.</param>
        /// <param name="slot">Slot name.</param>
        /// <returns>Layer.</returns>
        public static VisualLayer Slot(CompositionDocument composition, string slot) {
            return composition.Layers.Single(layer => layer.Id == "contrast-overlay-contrast-" + slot);
        }
    }
}

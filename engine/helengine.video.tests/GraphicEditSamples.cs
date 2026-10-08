using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Builds edits with graphic overlays: a 9:16 take scene whose speaker says "legalizar não é descriminalizar, nem é
    /// tratar", with a contrast chain anchored on the three terms, plus the matching catalog and compile context.
    /// </summary>
    static class GraphicEditSamples {
        /// <summary>
        /// Start of the take inside its media, in seconds.
        /// </summary>
        public const double TakeIn = 10;

        /// <summary>
        /// Builds a one-scene portrait edit with a contrast chain overlay.
        /// </summary>
        /// <returns>Valid edit.</returns>
        public static VideoEdit ContrastChain() {
            return new VideoEdit {
                Id = "graphic-edit",
                Format = new VideoFormat { Width = 1080, Height = 1920, BackgroundColor = "#202020FF" },
                TextStyles = new(StringComparer.Ordinal) {
                    ["caption"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 64, CenterY = 0.78 }),
                    ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 96, CenterY = 0.4, TextColor = "#FFFFFF", HighlightColor = "#FFD400" })
                },
                Media = [
                    new VideoMedia {
                        Id = "take", Kind = "video", Path = "take.mp4", Sha256 = new string('a', 64), Width = 1080, Height = 1920, DurationSec = 20,
                        Analysis = new VideoMediaAnalysis {
                            Method = "test",
                            Words = [
                                new VideoWord { Text = "Legalizar", StartSec = 10.5, EndSec = 11.0 },
                                new VideoWord { Text = "não", StartSec = 11.1, EndSec = 11.3 },
                                new VideoWord { Text = "é", StartSec = 11.3, EndSec = 11.4 },
                                new VideoWord { Text = "descriminalizar,", StartSec = 11.5, EndSec = 12.3 },
                                new VideoWord { Text = "nem", StartSec = 12.5, EndSec = 12.7 },
                                new VideoWord { Text = "é", StartSec = 12.7, EndSec = 12.8 },
                                new VideoWord { Text = "tratar.", StartSec = 12.9, EndSec = 13.4 }
                            ]
                        }
                    }
                ],
                Scenes = [
                    new VideoScene {
                        Id = "contrast",
                        Duration = new VideoSceneDuration { Mode = "from_take" },
                        Take = new VideoTake { Media = "take", InSec = TakeIn, OutSec = 15 },
                        Overlays = [
                            new VideoOverlay {
                                Id = "chain", Text = "LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR", At = new VideoMoment(),
                                Graphic = new VideoGraphic {
                                    Template = "contrast_chain", Version = 1,
                                    Items = ["Legalizar", "Descriminalizar", "Tratar"],
                                    At = [new VideoMoment { Word = "legalizar" }, new VideoMoment { Word = "descriminalizar" }, new VideoMoment { Word = "tratar" }]
                                }
                            }
                        ]
                    }
                ],
                Tracks = new VideoTracks { Captions = new VideoCaptionTrack { WordsPerCue = 4, WordsPerLine = 2 } }
            };
        }

        /// <summary>
        /// Builds a catalog with the basic operations and the built-in graphic templates.
        /// </summary>
        /// <returns>Capabilities.</returns>
        public static MediaCapabilities Catalog() {
            MediaCapabilities catalog = MediaCapabilities.Basic();
            GraphicTemplateCatalog.CreateBuiltIn().Publish(catalog);
            return catalog;
        }

        /// <summary>
        /// Builds a compile context with the built-in templates and a fixed-advance measurer.
        /// </summary>
        /// <param name="measurer">Measurer to use; null keeps the estimate.</param>
        /// <returns>Compile context.</returns>
        public static VideoCompileContext Context(IVideoTextMeasurer measurer) {
            return new VideoCompileContext { Capabilities = Catalog(), GraphicTemplates = GraphicTemplateCatalog.CreateBuiltIn(), TextMeasurer = measurer };
        }
    }
}

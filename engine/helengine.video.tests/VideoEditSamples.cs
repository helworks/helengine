using System.Text.Json;

namespace helengine.video.tests {
    /// <summary>
    /// Builds the sample edit used across the tests: an image scene with an inset zooming picture, then a recorded take
    /// scene entering with a push transition, with an overlay anchored on a spoken word, a music track and captions.
    /// </summary>
    static class VideoEditSamples {
        /// <summary>
        /// Builds a fresh two-scene sample edit.
        /// </summary>
        /// <returns>Valid sample edit.</returns>
        public static VideoEdit TwoScenes() {
            return new VideoEdit {
                Id = "video-1",
                Revision = 3,
                Format = new VideoFormat { Width = 540, Height = 960, BackgroundColor = "#F7F5FAFF" },
                ProjectProfile = "depois-do-slogan",
                Media = [
                    new VideoMedia { Id = "post", Kind = "image", Path = "media/post.png", Sha256 = new string('a', 64), Width = 800, Height = 600 },
                    new VideoMedia {
                        Id = "take", Kind = "video", Path = "media/take.mp4", Sha256 = new string('b', 64), Width = 540, Height = 960, DurationSec = 20,
                        Analysis = new VideoMediaAnalysis {
                            Method = "whisper_cpp_token_timestamps",
                            Words = [
                                new VideoWord { Text = "Qual", StartSec = 12.4, EndSec = 12.6 },
                                new VideoWord { Text = "é", StartSec = 12.6, EndSec = 12.7 },
                                new VideoWord { Text = "a", StartSec = 12.7, EndSec = 12.8 },
                                new VideoWord { Text = "lei?", StartSec = 12.8, EndSec = 13.2 },
                                new VideoWord { Text = "Política,", StartSec = 13.5, EndSec = 14.0 },
                                new VideoWord { Text = "política", StartSec = 14.4, EndSec = 14.9 }
                            ]
                        }
                    },
                    new VideoMedia { Id = "music", Kind = "audio", Path = "media/music.wav", Sha256 = new string('c', 64), DurationSec = 60 }
                ],
                Scenes = [
                    new VideoScene {
                        Id = "hook", Section = "abertura",
                        Duration = new VideoSceneDuration { Mode = "fixed", Sec = 3 },
                        Layers = [
                            new VideoLayer {
                                Id = "picture", Kind = "media", Media = "post", Order = 10, Layout = new VideoLayout { Preset = "inset" },
                                Motion = new VideoMotion { Preset = "zoom_to_focus", FromScale = 1, ToScale = 1.4, Start = new VideoMoment { Fraction = 0.2 }, Focus = new VideoFocus { Type = "point", X = 0.3, Y = 0.4 } }
                            }
                        ]
                    },
                    new VideoScene {
                        Id = "question", Section = "pergunta",
                        Duration = new VideoSceneDuration { Mode = "from_take" },
                        Take = new VideoTake { Media = "take", InSec = 12.3, OutSec = 15.3 },
                        Entry = new VideoEntry { Effect = "push", Version = 1, DurationSec = 0.4, Parameters = new(StringComparer.Ordinal) { ["Direction"] = JsonSerializer.SerializeToElement("Left") }, AudioFadeSec = 0.15, By = "human" },
                        Overlays = [new VideoOverlay { Id = "q", Text = "QUAL LEI?", At = new VideoMoment { Word = "lei" } }]
                    }
                ],
                Tracks = new VideoTracks {
                    Audio = [new VideoAudioTrack { Id = "bed", Media = "music", Start = new VideoTrackMoment { Scene = "question", At = new VideoMoment { Sec = 0.5 } }, Gain = 0.3 }],
                    Captions = new VideoCaptionTrack { WordsPerCue = 4, Style = JsonSerializer.SerializeToElement(new { FontFamily = "Anton" }), GraphicStyle = JsonSerializer.SerializeToElement(new { FontFamily = "Anton" }) }
                }
            };
        }
    }
}

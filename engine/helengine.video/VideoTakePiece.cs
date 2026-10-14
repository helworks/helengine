using helengine.media;

namespace helengine.video {
    /// <summary>
    /// One take segment placed in scene time.
    /// </summary>
    public sealed class VideoTakePiece {
        /// <summary>
        /// Gets or sets the source segment.
        /// </summary>
        public VideoTakeSegment Segment { get; set; }

        /// <summary>
        /// Gets or sets the exact source start.
        /// </summary>
        public MediaTime SourceIn { get; set; }

        /// <summary>
        /// Gets or sets the exact source end.
        /// </summary>
        public MediaTime SourceOut { get; set; }

        /// <summary>
        /// Gets or sets the scene-local start.
        /// </summary>
        public MediaTime Start { get; set; }

        /// <summary>
        /// Gets or sets the scene length of the piece.
        /// </summary>
        public MediaTime Duration { get; set; }

        /// <summary>
        /// Gets the scene-local end.
        /// </summary>
        public MediaTime End {
            get {
                return Start + Duration;
            }
        }

        /// <summary>
        /// Gets or sets the take speed.
        /// </summary>
        public double Speed { get; set; } = 1;

        /// <summary>
        /// Maps a source instant of this segment to scene-local seconds, clamped to the piece.
        /// </summary>
        /// <param name="sourceSec">Source seconds.</param>
        /// <returns>Scene-local seconds.</returns>
        public double SceneSec(double sourceSec) {
            return Start.ToSeconds() + Math.Clamp((sourceSec - Segment.InSec) / Speed, 0, Duration.ToSeconds());
        }

        /// <summary>
        /// Maps a source instant of this segment to scene-local time, clamped to the piece.
        /// </summary>
        /// <param name="sourceSec">Source seconds.</param>
        /// <returns>Scene-local time.</returns>
        public MediaTime SceneTime(double sourceSec) {
            MediaTime offset = MediaTime.FromSeconds(Math.Max(0, (sourceSec - Segment.InSec) / Speed));
            return Start + (offset > Duration ? Duration : offset);
        }
    }
}

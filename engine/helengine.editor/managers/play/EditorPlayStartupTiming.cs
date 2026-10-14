using System.Globalization;

namespace helengine.editor {
    /// <summary>
    /// Durations of the editor-side Play stages (scene save, cook, script build and player launch) measured for one
    /// launched player, plus the clock reading at which Play started. Once the player reports its first frame, or exits
    /// without one, it formats the single startup summary line the editor console shows, for example
    /// <c>[play] save 0.1s · cook 8.4s · scripts 0.0s (skipped) · launch 0.3s · first frame 2.7s · total 11.5s</c>.
    /// Every duration is written in seconds with one decimal and the invariant culture.
    /// </summary>
    public sealed class EditorPlayStartupTiming {
        /// <summary>
        /// Prefix of every startup summary line.
        /// </summary>
        public const string SummaryPrefix = "[play] ";

        /// <summary>
        /// Separator placed between the stages of the summary line.
        /// </summary>
        const string StageSeparator = " · ";

        /// <summary>
        /// Initializes the measured stage durations of one Play.
        /// </summary>
        /// <param name="playStartedAt">Play clock reading taken when the save stage began.</param>
        /// <param name="saveDuration">Time spent saving the open scene.</param>
        /// <param name="cookDuration">Time spent cooking content.</param>
        /// <param name="scriptBuildDuration">Time spent building, or deciding to skip building, the runtime scripts.</param>
        /// <param name="scriptBuildSkipped">Whether the script builder found the scripts up to date and skipped the compile.</param>
        /// <param name="launchDuration">Time spent starting the player process.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any duration is negative.</exception>
        public EditorPlayStartupTiming(
            TimeSpan playStartedAt,
            TimeSpan saveDuration,
            TimeSpan cookDuration,
            TimeSpan scriptBuildDuration,
            bool scriptBuildSkipped,
            TimeSpan launchDuration) {
            if (saveDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(saveDuration), saveDuration, "Stage durations cannot be negative.");
            } else if (cookDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(cookDuration), cookDuration, "Stage durations cannot be negative.");
            } else if (scriptBuildDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(scriptBuildDuration), scriptBuildDuration, "Stage durations cannot be negative.");
            } else if (launchDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(launchDuration), launchDuration, "Stage durations cannot be negative.");
            }

            PlayStartedAt = playStartedAt;
            SaveDuration = saveDuration;
            CookDuration = cookDuration;
            ScriptBuildDuration = scriptBuildDuration;
            ScriptBuildSkipped = scriptBuildSkipped;
            LaunchDuration = launchDuration;
        }

        /// <summary>
        /// Gets the Play clock reading taken when the save stage began; the summary total is measured from it.
        /// </summary>
        public TimeSpan PlayStartedAt { get; }

        /// <summary>
        /// Gets the time spent saving the open scene.
        /// </summary>
        public TimeSpan SaveDuration { get; }

        /// <summary>
        /// Gets the time spent cooking content.
        /// </summary>
        public TimeSpan CookDuration { get; }

        /// <summary>
        /// Gets the time spent in the script build stage.
        /// </summary>
        public TimeSpan ScriptBuildDuration { get; }

        /// <summary>
        /// Gets whether the script build stage skipped the compile because the scripts were up to date.
        /// </summary>
        public bool ScriptBuildSkipped { get; }

        /// <summary>
        /// Gets the time spent starting the player process.
        /// </summary>
        public TimeSpan LaunchDuration { get; }

        /// <summary>
        /// Formats the summary for a player that presented its first frame.
        /// </summary>
        /// <param name="firstFrameDuration">Time the player reported from its process start to its first presented frame.</param>
        /// <param name="totalDuration">Time from the start of Play until the editor received the first-frame report.</param>
        /// <returns>The summary line, including every stage, the first frame and the total.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either duration is negative.</exception>
        public string FormatFirstFrameSummary(TimeSpan firstFrameDuration, TimeSpan totalDuration) {
            if (firstFrameDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(firstFrameDuration), firstFrameDuration, "The first-frame duration cannot be negative.");
            } else if (totalDuration < TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(totalDuration), totalDuration, "The total duration cannot be negative.");
            }

            return FormatStages()
                + StageSeparator + "first frame " + FormatSeconds(firstFrameDuration)
                + StageSeparator + "total " + FormatSeconds(totalDuration);
        }

        /// <summary>
        /// Formats the summary for a player that exited before presenting a frame: the measured stages followed by the
        /// exit code.
        /// </summary>
        /// <param name="exitCode">Exit code of the player process.</param>
        /// <returns>The summary line ending in <c>no frame (exit code N)</c>.</returns>
        public string FormatNoFrameSummary(int exitCode) {
            return FormatStages() + StageSeparator + "no frame (exit code " + exitCode.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>
        /// Formats the prefix and the four editor-side stages shared by both summary forms.
        /// </summary>
        /// <returns>The prefix followed by the save, cook, scripts and launch stages.</returns>
        string FormatStages() {
            string scripts = "scripts " + FormatSeconds(ScriptBuildDuration);
            if (ScriptBuildSkipped) {
                scripts += " (skipped)";
            }

            return SummaryPrefix
                + "save " + FormatSeconds(SaveDuration)
                + StageSeparator + "cook " + FormatSeconds(CookDuration)
                + StageSeparator + scripts
                + StageSeparator + "launch " + FormatSeconds(LaunchDuration);
        }

        /// <summary>
        /// Writes a duration as seconds with one decimal and an <c>s</c> suffix, using the invariant culture.
        /// </summary>
        /// <param name="duration">Duration to write.</param>
        /// <returns>The formatted duration, for example <c>8.4s</c>.</returns>
        static string FormatSeconds(TimeSpan duration) {
            return duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }
    }
}

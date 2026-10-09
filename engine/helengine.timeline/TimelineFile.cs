using helengine.files;

namespace helengine.timeline {
    /// <summary>
    /// Reads and writes <c>.htimeline</c> files through the editor asset format, validating on both sides so no tool
    /// persists or loads a timeline the cooker or the video compiler would reject.
    /// </summary>
    public static class TimelineFile {
        /// <summary>
        /// Loads and validates one timeline file.
        /// </summary>
        /// <param name="path">Path of the <c>.htimeline</c> file.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; null leaves references unfollowed.</param>
        /// <returns>The valid timeline.</returns>
        /// <exception cref="InvalidDataException">The file holds another asset type.</exception>
        /// <exception cref="TimelineFormatException">The timeline fails validation.</exception>
        public static TimelineAsset Load(string path, ITimelineAssetResolver resolver) {
            TimelineSerialization.Register();
            using FileStream stream = File.OpenRead(path);
            if (EditorAssetBinarySerializer.Deserialize(stream) is not TimelineAsset timeline) {
                throw new InvalidDataException($"'{path}' is not a timeline asset.");
            }
            TimelineValidator.EnsureValid(timeline, resolver);
            return timeline;
        }

        /// <summary>
        /// Validates a timeline and writes it as a <c>.htimeline</c> file, replacing any existing file.
        /// </summary>
        /// <param name="path">Destination path; must end in <see cref="TimelineAsset.FileExtension"/>.</param>
        /// <param name="timeline">Timeline to persist.</param>
        /// <param name="resolver">Loads timelines referenced by nested clips; null leaves references unfollowed.</param>
        /// <exception cref="TimelineFormatException">The timeline fails validation.</exception>
        public static void Save(string path, TimelineAsset timeline, ITimelineAssetResolver resolver) {
            if (!string.Equals(Path.GetExtension(path), TimelineAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Timeline files must use the {TimelineAsset.FileExtension} extension.", nameof(path));
            }
            TimelineSerialization.Register();
            TimelineValidator.EnsureValid(timeline, resolver);
            using MemoryStream buffer = new MemoryStream();
            EditorAssetBinarySerializer.Serialize(buffer, timeline);
            File.WriteAllBytes(path, buffer.ToArray());
        }
    }
}

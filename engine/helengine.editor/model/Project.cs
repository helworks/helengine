using System.Text.Json.Serialization;

namespace helengine.ui {
    /// <summary>
    /// Represents persisted metadata for a helengine project.
    /// </summary>
    public class Project {
        /// <summary>
        /// Gets or sets the project name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute path to the project root.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last time the project was opened.
        /// </summary>
        public DateTime LastOpened { get; set; }

        /// <summary>
        /// Gets or sets the creation timestamp for the project.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets or sets the number of times the project has been opened.
        /// </summary>
        public int TimesOpened { get; set; } = 0;

        /// <summary>
        /// Gets or sets a short description of the project.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project version string.
        /// </summary>
        public string Version { get; set; } = "1.0.0";
        
        /// <summary>
        /// Gets a display-friendly name for the project, falling back to the folder name.
        /// </summary>
        [JsonIgnore]
        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : System.IO.Path.GetFileName(Path);
        
        /// <summary>
        /// Gets a human-readable representation of the last opened time.
        /// </summary>
        [JsonIgnore]
        public string RelativeTime => GetRelativeTime(LastOpened);
        
        /// <summary>
        /// Converts a timestamp to a relative age string.
        /// </summary>
        /// <param name="dateTime">Timestamp to convert.</param>
        /// <returns>Relative time string.</returns>
        private string GetRelativeTime(DateTime dateTime) {
            var timeSpan = DateTime.UtcNow - dateTime;
            
            if (timeSpan.TotalDays > 7) {
                return dateTime.ToString("MMM dd, yyyy");
            } else if (timeSpan.TotalDays >= 1) {
                return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
            } else if (timeSpan.TotalHours >= 1) {
                return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
            } else if (timeSpan.TotalMinutes >= 1) {
                return $"{(int)timeSpan.TotalMinutes} minute{(timeSpan.TotalMinutes >= 2 ? "s" : "")} ago";
            } else {
                return "Just now";
            }
        }
    }
}

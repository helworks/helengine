using System.Text.Json.Serialization;

namespace helengine.ui {
    public class Project {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime LastOpened { get; set; }
        public DateTime Created { get; set; }
        public int TimesOpened { get; set; } = 0;
        public string Description { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        
        [JsonIgnore]
        public string DisplayName => !string.IsNullOrEmpty(Name) ? Name : System.IO.Path.GetFileName(Path);
        
        [JsonIgnore]
        public string RelativeTime => GetRelativeTime(LastOpened);
        
        private string GetRelativeTime(DateTime dateTime) {
            var timeSpan = DateTime.Now - dateTime;
            
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

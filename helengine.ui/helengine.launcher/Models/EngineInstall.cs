using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace helengine.editor.launcher.Models {
    public sealed class EngineInstall {
        public string Version { get; set; } = "unknown";
        public string InstallPath { get; set; } = string.Empty;
        public string Source { get; set; } = "local";
        public DateTime InstalledAt { get; set; } = DateTime.Now;
        public string? DetectedFrom { get; set; }
        public string? Name { get; set; }

        [JsonIgnore]
        public string DisplayName {
            get {
                if (!string.IsNullOrWhiteSpace(Name)) {
                    return Name!;
                }

                string trimmed = InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.IsNullOrWhiteSpace(trimmed) ? "engine" : Path.GetFileName(trimmed);
            }
        }

        [JsonIgnore]
        public string Summary => $"{DisplayName} • v{Version}";
    }

    public sealed class EngineInstallManifest {
        public List<EngineInstall> Engines { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string SchemaVersion { get; set; } = "1.0";
    }
}

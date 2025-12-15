using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using helengine.editor.launcher.Models;

namespace helengine.editor.launcher.Services {
    public class EngineInstallManager {
        readonly string settingsFolder;
        readonly string enginesFilePath;
        readonly List<EngineInstall> installs = new();

        public EngineInstallManager() {
            var roamingFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var helengineFolder = Path.Combine(roamingFolder, "helengine");
            settingsFolder = Path.Combine(helengineFolder, "settings");
            enginesFilePath = Path.Combine(settingsFolder, "engines.json");

            Directory.CreateDirectory(settingsFolder);
            Load();
        }

        public IReadOnlyList<EngineInstall> InstalledEngines => installs;

        public void Load() {
            try {
                if (!File.Exists(enginesFilePath)) {
                    installs.Clear();
                    return;
                }

                var json = File.ReadAllText(enginesFilePath);
                var manifest = JsonSerializer.Deserialize<EngineInstallManifest>(json);
                installs.Clear();
                if (manifest?.Engines != null) {
                    installs.AddRange(manifest.Engines);
                }
            } catch {
                installs.Clear();
            }
        }

        public void Save() {
            try {
                var manifest = new EngineInstallManifest {
                    Engines = installs.ToList(),
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(enginesFilePath, json);
            } catch {
            }
        }

        public EngineInstall AddLocalInstall(string folderPath, EngineVersionInfo versionInfo) {
            string fullPath = Path.GetFullPath(folderPath);
            var existing = installs.FirstOrDefault(i => PathsEqual(i.InstallPath, fullPath));
            if (existing != null) {
                existing.Version = versionInfo.DisplayVersion;
                existing.DetectedFrom = versionInfo.AssemblyName;
                existing.InstalledAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(versionInfo.FriendlyName)) {
                    existing.Name = versionInfo.FriendlyName;
                }
                Save();
                return existing;
            }

            var install = new EngineInstall {
                InstallPath = fullPath,
                Version = versionInfo.DisplayVersion,
                DetectedFrom = versionInfo.AssemblyName,
                InstalledAt = DateTime.Now,
                Source = "local",
                Name = versionInfo.FriendlyName
            };

            installs.Insert(0, install);
            Save();
            return install;
        }

        public void ReplaceInstalls(IEnumerable<EngineInstall> newInstalls) {
            installs.Clear();
            installs.AddRange(newInstalls);
            Save();
        }

        static bool PathsEqual(string a, string b) {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}

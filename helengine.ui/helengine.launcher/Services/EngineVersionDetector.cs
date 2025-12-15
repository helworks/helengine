using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace helengine.editor.launcher.Services {
    public sealed class EngineVersionInfo {
        public EngineVersionInfo(string version, string assemblyPath, string assemblyName, string? productVersion = null) {
            Version = version;
            AssemblyPath = assemblyPath;
            AssemblyName = assemblyName;
            ProductVersion = productVersion;
        }

        public string Version { get; }
        public string AssemblyPath { get; }
        public string AssemblyName { get; }
        public string? ProductVersion { get; }
        public string? FriendlyName { get; set; }

        public string DisplayVersion => string.IsNullOrWhiteSpace(ProductVersion) ? Version : ProductVersion!;
    }

    public static class EngineVersionDetector {
        static readonly string[] PreferredAssemblyNames = new[] {
            "helengine.editor.dll",
            "helengine.editor.app.dll",
            "helengine.core.dll",
            "helengine.core.windows.dll"
        };

        public static bool TryDetect(string rootPath, out EngineVersionInfo? versionInfo, out string error) {
            versionInfo = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rootPath)) {
                error = "No folder selected.";
                return false;
            }

            if (!Directory.Exists(rootPath)) {
                error = "Folder does not exist.";
                return false;
            }

            var candidate = FindAssembly(rootPath);
            if (candidate == null) {
                error = "No helengine assemblies found in the selected folder.";
                return false;
            }

            try {
                var assemblyName = AssemblyName.GetAssemblyName(candidate);
                var fileInfo = FileVersionInfo.GetVersionInfo(candidate);

                string version = assemblyName.Version?.ToString() ?? fileInfo.FileVersion ?? "unknown";
                versionInfo = new EngineVersionInfo(
                    string.IsNullOrWhiteSpace(version) ? "unknown" : version,
                    candidate,
                    assemblyName.Name ?? Path.GetFileName(candidate),
                    string.IsNullOrWhiteSpace(fileInfo.ProductVersion) ? null : fileInfo.ProductVersion);

                var parent = Path.GetDirectoryName(candidate);
                if (!string.IsNullOrEmpty(parent)) {
                    versionInfo.FriendlyName = Path.GetFileName(parent);
                }

                return true;
            } catch (Exception ex) {
                error = $"Failed to read version from assembly: {ex.Message}";
                return false;
            }
        }

        static string? FindAssembly(string rootPath) {
            foreach (var preferred in PreferredAssemblyNames) {
                var candidate = Path.Combine(rootPath, preferred);
                if (File.Exists(candidate)) {
                    return candidate;
                }
            }

            var dllMatch = Directory.EnumerateFiles(rootPath, "*.dll", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(p => Path.GetFileName(p).StartsWith("helengine", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(dllMatch)) {
                return dllMatch;
            }

            return Directory.EnumerateFiles(rootPath, "*.exe", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(p => Path.GetFileName(p).StartsWith("helengine", StringComparison.OrdinalIgnoreCase));
        }
    }
}

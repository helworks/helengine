using System;

namespace helengine.editor.launcher.Models;

public sealed class RecentProject {
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
    public DateTime Created { get; set; }
    public int TimesOpened { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
}

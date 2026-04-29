using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using helengine.editor.launcher.Theme;

namespace helengine.editor.launcher.Views;

public sealed class LauncherWindow : Window {
    public LauncherWindow() {
        Title = "helengine launcher";
        Width = 1100;
        Height = 720;
        MinWidth = 820;
        MinHeight = 580;
        SystemDecorations = SystemDecorations.Full;
        Background = LauncherTheme.AppBackground;

        using var iconStream = AssetLoader.Open(new Uri("avares://helengine.editor.launcher/Assets/helengine.ico"));
        Icon = new WindowIcon(iconStream);

        Content = new LauncherShell();
    }
}

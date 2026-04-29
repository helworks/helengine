using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using helengine.editor.launcher.Views;

namespace helengine.editor.launcher {
    public class App : Application {
        public override void Initialize() {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = ThemeVariant.Dark;
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new LauncherWindow();
            } else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView) {
                singleView.MainView = new LauncherShell();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

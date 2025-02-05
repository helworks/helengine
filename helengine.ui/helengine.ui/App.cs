using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using helengine.ui.Views;

namespace helengine.ui {
    public class App : Application {
        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is ISingleViewApplicationLifetime browserLifetime) {
                // Browser/WebAssembly setup
                browserLifetime.MainView = new MainView();
            }
            else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                // Desktop setup
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

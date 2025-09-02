using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using helengine.ui.Views;
using System;
using Avalonia.Themes.Fluent;

namespace helengine.ui {
    public class App : Application {
        public override void Initialize() {
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted() {
            if (ApplicationLifetime is ISingleViewApplicationLifetime browserLifetime) {
                // Browser/WebAssembly setup
                browserLifetime.MainView = new MainView();
            }
            else if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                // Desktop setup - show project chooser first
                ShowProjectChooser(desktop);

            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ShowProjectChooser(IClassicDesktopStyleApplicationLifetime desktop) {
            var projectChooser = new ProjectChooserWindow();
            bool projectSelected = false;
            
            projectChooser.ProjectSelected += (sender, project) => {
                // Mark that a project was selected
                projectSelected = true;
                
                // Open main editor with selected project first
                OpenMainEditor(desktop, project);
                
                // Then close project chooser
                projectChooser.Close();
            };

            // Note: NewProjectRequested is now handled internally by the window
            // It will directly call ProjectSelected when a project is created

            projectChooser.BrowseProjectRequested += async (sender, e) => {
                // Show browse project dialog
                var browseProject = await projectChooser.ShowBrowseProjectDialogAsync();
                if (browseProject != null) {
                    // Mark that a project was selected
                    projectSelected = true;
                    
                    // Open main editor first
                    OpenMainEditor(desktop, browseProject);
                    
                    // Then close project chooser
                    projectChooser.Close();
                }
            };

            projectChooser.Closed += (sender, e) => {
                // If project chooser is closed without selection, exit application
                if (!projectSelected) {
                    desktop.Shutdown();
                }
            };

            // Show the project chooser as the initial window
            desktop.MainWindow = projectChooser;
        }

        private void OpenMainEditor(IClassicDesktopStyleApplicationLifetime desktop, Project project) {
            var mainWindow = new MainWindow(project);
            
            // TODO: Pass project information to main window
            // For now, just update the title
            mainWindow.Title = $"helengine - {project.DisplayName}";
            
            // Set the main window and show it
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}

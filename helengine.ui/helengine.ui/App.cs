using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using helengine.ui.Views;
using helengine.ui.Models;
using System;

namespace helengine.ui {
    public class App : Application {
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

            projectChooser.NewProjectRequested += (sender, e) => {
                // Mark that a project was selected
                projectSelected = true;
                
                // TODO: Show new project dialog
                // For now, just create a temporary project
                var newProject = new Project {
                    Name = "New Project",
                    Path = @"C:\Projects\NewProject",
                    LastOpened = DateTime.Now,
                    Description = "A new project"
                };
                
                // Open main editor first
                OpenMainEditor(desktop, newProject);
                
                // Then close project chooser
                projectChooser.Close();
            };

            projectChooser.BrowseProjectRequested += (sender, e) => {
                // Mark that a project was selected
                projectSelected = true;
                
                // TODO: Show folder browser dialog
                // For now, just create a temporary project
                var browseProject = new Project {
                    Name = "Browsed Project",
                    Path = @"C:\Projects\BrowsedProject",
                    LastOpened = DateTime.Now,
                    Description = "A project opened via browse"
                };
                
                // Open main editor first
                OpenMainEditor(desktop, browseProject);
                
                // Then close project chooser
                projectChooser.Close();
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
            var mainWindow = new MainWindow();
            
            // TODO: Pass project information to main window
            // For now, just update the title
            mainWindow.Title = $"HelEngine - {project.DisplayName}";
            
            // Set the main window and show it
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }
}

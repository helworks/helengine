using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using helengine.ui.Controls;
using helengine.ui.Models;
using helengine.ui.Services;
using helengine.ui.Theming;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace helengine.ui.Views {
    public class ProjectChooserWindow : Window {
        private readonly List<Project> _recentProjects = new();
        private readonly ProjectManager _projectManager;

        // Components
        private ProjectListControl? _projectListControl;
        private NewProjectControl? _newProjectControl;
        private Grid? _mainGrid;
        private bool _showingNewProject = false;

        public event EventHandler<Project>? ProjectSelected;
        public event EventHandler? BrowseProjectRequested;

        public ProjectChooserWindow() {
            _projectManager = new ProjectManager();
            InitializeWindow();
            SetupContent();
            _ = LoadRecentProjectsAsync(); // Fire and forget async loading
        }

        private void InitializeWindow() {
            Title = "helengine - select project";
            Width = 900;
            Height = 700;
            MinWidth = 800;
            MinHeight = 600;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            // themed background
            Background = ThemeManager.Brushes.BackgroundPrimary;

            // Hide system titlebar and extend client area
            SystemDecorations = SystemDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
            ExtendClientAreaTitleBarHeightHint = 36;
        }

        private async Task LoadRecentProjectsAsync() {
            try {
                var projects = await _projectManager.LoadProjectsAsync();
                _recentProjects.Clear();
                _recentProjects.AddRange(projects);

                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                    _projectListControl?.SetProjects(_recentProjects);
                });
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error loading projects: {ex.Message}");
            }
        }

        private void ShowNewProject() {
            if (_projectListControl != null && _newProjectControl != null) {
                _projectListControl.IsVisible = false;
                _newProjectControl.IsVisible = true;
                _showingNewProject = true;
                _newProjectControl.FocusNameField();
            }
        }

        private void ShowProjectList() {
            if (_projectListControl != null && _newProjectControl != null) {
                _projectListControl.IsVisible = true;
                _newProjectControl.IsVisible = false;
                _showingNewProject = false;
                _newProjectControl.ClearFields();
            }
        }

        private async void OnProjectSelected(object? sender, Project project) {
            try {
                await _projectManager.UpdateProjectLastOpenedAsync(project);
                ProjectSelected?.Invoke(this, project);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error updating project: {ex.Message}");
            }
        }

        private async void OnNewProjectCreated(object? sender, Project project) {
            try {
                // Refresh the project list
                _recentProjects.Clear();
                _recentProjects.AddRange(await _projectManager.LoadProjectsAsync());
                _projectListControl?.SetProjects(_recentProjects);

                // Go back to project list and trigger selection
                ShowProjectList();
                ProjectSelected?.Invoke(this, project);
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error after creating project: {ex.Message}");
            }
        }

        private void OnBackToProjectList(object? sender, EventArgs e) {
            ShowProjectList();
        }

        private void SetupContent() {
            _mainGrid = new Grid();
            _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // TitleBar
            _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header actions
            _mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star)); // Content

            // Custom TitleBar
            var titleBar = new TitleBar { Title = "helengine" };
            Grid.SetRow(titleBar, 0);
            _mainGrid.Children.Add(titleBar);

            // Resize overlay across content row
            var resize = new ResizeOverlay(rowSpan: 1);
            Grid.SetRow(resize, 2);
            resize.ZIndex = 10000; // Ensure it's above content
            _mainGrid.Children.Add(resize);

            // Minimalist header (actions)
            var headerPanel = CreateMinimalistHeader();
            Grid.SetRow(headerPanel, 1);
            _mainGrid.Children.Add(headerPanel);

            // Create components
            _projectListControl = new ProjectListControl();
            _projectListControl.ProjectSelected += OnProjectSelected;
            Grid.SetRow(_projectListControl, 2);
            _mainGrid.Children.Add(_projectListControl);

            _newProjectControl = new NewProjectControl();
            _newProjectControl.ProjectCreated += OnNewProjectCreated;
            _newProjectControl.BackRequested += OnBackToProjectList;
            Grid.SetRow(_newProjectControl, 2);
            _newProjectControl.IsVisible = false;
            _mainGrid.Children.Add(_newProjectControl);

            Content = _mainGrid;
        }

        private Panel CreateMinimalistHeader() {
            var panel = new DockPanel {
                Margin = new Thickness(20),
                Background = ThemeManager.Brushes.BackgroundPrimary
            };
            panel.LastChildFill = false; // ensure right-docked stack doesn't stretch

            // Title on the left
            var titleText = new TextBlock {
                Text = "helengine",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = ThemeManager.Brushes.AccentPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas")
            };
            DockPanel.SetDock(titleText, Dock.Left);
            panel.Children.Add(titleText);

            // Buttons on the right
            var buttonPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(buttonPanel, Dock.Right);

            var newProjectBtn = ThemedButton.Create(
                text: "create",
                normalBg: ThemeManager.Colors.AccentSecondary,  // Inactive tab color
                normalBorder: ThemeManager.Colors.AccentTertiary, // Tab border color
                normalFore: ThemeManager.Colors.AccentQuaternary, // Inactive tab text
                hoverBg: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverBorder: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            newProjectBtn.Margin = new Thickness(5);
            newProjectBtn.Click += (s, e) => ShowNewProject();
            buttonPanel.Children.Add(newProjectBtn);

            var findProjectBtn = ThemedButton.Create(
                text: "find",
                normalBg: ThemeManager.Colors.AccentSecondary,  // Inactive tab color
                normalBorder: ThemeManager.Colors.AccentTertiary, // Tab border color
                normalFore: ThemeManager.Colors.AccentQuaternary, // Inactive tab text
                hoverBg: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverBorder: ThemeManager.Colors.AccentPrimary, // Active tab color
                hoverFore: ThemeManager.Colors.TextOnAccent
            );
            findProjectBtn.Margin = new Thickness(5);
            findProjectBtn.Click += (s, e) => BrowseProjectRequested?.Invoke(this, EventArgs.Empty);
            buttonPanel.Children.Add(findProjectBtn);

            panel.Children.Add(buttonPanel);

            return panel;
        }

        public async Task<Project?> ShowBrowseProjectDialogAsync() {
            try {
                var dialog = new OpenFolderDialog {
                    Title = "select project folder"
                };

                var result = await dialog.ShowAsync(this);
                if (!string.IsNullOrEmpty(result)) {
                    var project = await _projectManager.AddExistingProjectAsync(result);

                    // Refresh the project list
                    _recentProjects.Clear();
                    _recentProjects.AddRange(await _projectManager.LoadProjectsAsync());
                    _projectListControl?.SetProjects(_recentProjects);

                    return project;
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Error adding project: {ex.Message}");
            }

            return null;
        }
    }
}